using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security;
using System.Text;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads and writes trip authorities against both the original client table
/// and databases containing the later audit columns. The legacy trip fields
/// remain the source of truth; optional columns are selected only when the
/// connected database contains them.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility allowlists; submitted values are parameters."
)]
public sealed class TripRepository : ITripRepository
{
    private const string TableName = "trip_authorities";
    private const string ContractTableName = "contract";
    private const string VehicleTableName = "vehicle_master";
    private const string ModelTableName = "model";
    private const string MakeTableName = "make";
    private static readonly string[] TripDriverTableNames = ["trip_driver", "trip_drivers"];
    private const string TripPassengerTableName = "trip_passengers";
    private const string RouteDetailTableName = "route_details";

    private static readonly string[] TripMutationTriggerNames =
    ["TRG_INS_UPD_RejectIncompleteTrip"];

    private static readonly string[] RouteInsertTriggerNames =
    [
        "TRG_INS_RouteJournalDetailRecord",
        "TRG_INS_UPD_CheckOverLapping_RouteDetailsKilos",
        "TRG_INS_UPD_RouteDetails_CheckOverLapping_ManualLogsheets",
    ];

    private static readonly string[] RouteUpdateTriggerNames =
    [
        "TRG_INS_RouteJournalDetailRecord",
        "TRG_UPD_RouteJournalDetailRecord",
        "TRG_INS_UPD_CheckOverLapping_RouteDetailsKilos",
        "TRG_INS_UPD_RouteDetails_CheckOverLapping_ManualLogsheets",
    ];

    private static readonly string[] RequiredColumns =
    [
        "trip_authority_code",
        "contract_code",
        "approver_name",
        "approver_rank",
        "approver_tel",
        "end_odo_meter",
        "expiry_date",
        "trip_reason",
        "trip_request_number",
        "issue_date",
        "trip_type_code",
        "trip_incident_type_code",
        "user_access_code",
        "locked_for_transfer",
        "Trip_Is_Monthly",
    ];

    private static readonly string[] OptionalColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly string[] RequiredContractColumns =
    [
        "contract_code",
        "vmf_code",
        "site_code",
    ];

    private static readonly string[] TripAuthorityVehicleContractColumns =
    [
        "contract_code",
        "vmf_code",
        "site_code",
        "still_current",
        "contract_type",
    ];

    private static readonly string[] TripAuthorityVehicleColumns =
    [
        "vmf_code",
        "vehicle_status_code",
        "fleet_number",
        "registration_number",
        "licence_due_date",
    ];

    private readonly FisDbContext _context;

    public TripRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Trip?> GetByIdAsync(
        int tripId,
        IReadOnlySet<short>? allowedSiteCodes = null
    ) =>
        (
            await QueryAsync(
                "[t].[trip_authority_code] = @tripId",
                command => AddParameter(command, "@tripId", DbType.Int32, tripId),
                includeDeleted: false,
                allowedSiteCodes: allowedSiteCodes,
                take: 1
            )
        ).SingleOrDefault();

    public async Task<TripAuthorityDetails?> GetDetailsAsync(
        int tripId,
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        var trip = await GetByIdAsync(tripId, allowedSiteCodes);
        if (trip is null)
        {
            return null;
        }

        var drivers = await GetTripDriversAsync(tripId);
        var passengers = await GetTripPassengersAsync(tripId);
        var routes = await GetRouteDetailsAsync(tripId);
        return new TripAuthorityDetails(trip, drivers, passengers, routes);
    }

    public async Task<IEnumerable<Trip>> GetAllAsync(IReadOnlySet<short>? allowedSiteCodes = null) =>
        await QueryAsync(
            orderBy: "[t].[issue_date] DESC, [t].[trip_authority_code] DESC",
            allowedSiteCodes: allowedSiteCodes
        );

    public async Task<TripSummaryPage> GetTripSummaryPageAsync(TripSummaryPageQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var requestedPage = Math.Max(1, query.Page);
        var search = query.Search?.Trim().ToLowerInvariant() ?? string.Empty;
        var filter = query.Filter?.Trim().ToLowerInvariant() ?? string.Empty;
        var contractColumns = await GetTableColumnsAsync(ContractTableName);
        var hasContractProjection = RequiredContractColumns.All(contractColumns.Contains);

        // GetTripsByVehicleAsync returns no rows when the legacy contract link
        // is unavailable. Preserve that existing report behavior for the
        // optional VMF filter instead of guessing a replacement field.
        if (query.VmfCode.HasValue && !hasContractProjection)
        {
            return new TripSummaryPage([], 1, pageSize, 0);
        }

        var siteColumns = hasContractProjection
            ? await GetTableColumnsAsync("site")
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasSiteProjection = new[] { "Site_code", "description" }.All(siteColumns.Contains);
        var departmentExpression = hasSiteProjection
            ? "COALESCE(NULLIF(LTRIM(RTRIM([s].[description])), ''), '')"
            : "CAST('' AS varchar(1))";
        var contractJoin = hasContractProjection
            ? $"LEFT JOIN [dbo].[{ContractTableName}] AS [c] ON [c].[contract_code] = [t].[contract_code]"
            : string.Empty;
        var siteJoin = hasSiteProjection
            ? $"LEFT JOIN [dbo].[site] AS [s] ON [s].[Site_code] = [c].[site_code]"
                + (
                    siteColumns.Contains("is_deleted")
                        ? " AND ([s].[is_deleted] = 0 OR [s].[is_deleted] IS NULL)"
                        : string.Empty
                )
            : string.Empty;

        var availableColumns = await GetAvailableColumnsAsync();
        var conditions = new List<string>
        {
            "[t].[issue_date] >= @startDate",
            "[t].[issue_date] <= @endDate",
        };
        if (availableColumns.Contains("is_deleted"))
        {
            conditions.Add("([t].[is_deleted] = 0 OR [t].[is_deleted] IS NULL)");
        }

        if (query.VmfCode.HasValue)
        {
            conditions.Add("[c].[vmf_code] = @vmfCode");
        }

        var sourceSql = $"""
            SELECT
                [t].[contract_code] AS [contract_code],
                {departmentExpression} AS [department],
                COUNT(1) AS [trip_count],
                COALESCE(
                    SUM(CONVERT(decimal(19, 2), COALESCE([t].[end_odo_meter], 0))),
                    CAST(0 AS decimal(19, 2))
                ) AS [total_kilometers],
                MIN([t].[issue_date]) AS [first_trip],
                MAX([t].[issue_date]) AS [last_trip],
                MAX([t].[trip_authority_code]) AS [last_trip_id]
            FROM [dbo].[{TableName}] AS [t]
            {contractJoin}
            {siteJoin}
            WHERE {string.Join(" AND ", conditions)}
            GROUP BY [t].[contract_code], {departmentExpression}
            """;

        var summaryConditions = new List<string>();
        var keyExpression =
            "CASE WHEN [summary].[contract_code] > 0 THEN CONCAT('VMF ', CONVERT(varchar(50), [summary].[contract_code])) ELSE 'Unknown' END";
        var vehicleExpression = keyExpression;
        var departmentDisplayExpression =
            "CASE WHEN [summary].[department] = '' THEN '-' ELSE [summary].[department] END";

        if (search.Length > 0)
        {
            var searchableExpressions = new[]
            {
                keyExpression,
                vehicleExpression,
                departmentDisplayExpression,
                "CONVERT(varchar(50), [summary].[trip_count])",
                "CONVERT(varchar(50), CONVERT(bigint, [summary].[total_kilometers]))",
            };
            summaryConditions.Add(
                $"({string.Join(
                    " OR ",
                    searchableExpressions.Select(expression =>
                        $"CHARINDEX(@search, LOWER(CONVERT(varchar(250), {expression}))) > 0"
                    )
                )})"
            );
        }

        switch (filter)
        {
            case "vehicle":
                summaryConditions.Add("[summary].[contract_code] > 0");
                break;
            case "department":
                summaryConditions.Add("[summary].[department] <> ''");
                break;
            case "multiple":
                summaryConditions.Add("[summary].[trip_count] > 1");
                break;
        }

        var summaryWhere =
            summaryConditions.Count == 0
                ? string.Empty
                : $"WHERE {string.Join(" AND ", summaryConditions)}";

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            int total;
            await using (var countCommand = connection.CreateCommand())
            {
                countCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                countCommand.CommandText = $"""
                    SELECT COUNT(1)
                    FROM (
                        {sourceSql}
                    ) AS [summary]
                    {summaryWhere}
                    """;
                AddTripSummaryParameters(countCommand, query, search);
                total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
            }

            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = Math.Min(requestedPage, totalPages);
            var skip = checked((long)(page - 1) * pageSize);

            await using var dataCommand = connection.CreateCommand();
            dataCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            dataCommand.CommandText = $"""
                SELECT
                    [summary].[contract_code],
                    [summary].[department],
                    [summary].[trip_count],
                    [summary].[total_kilometers],
                    [summary].[first_trip],
                    [summary].[last_trip]
                FROM (
                    {sourceSql}
                ) AS [summary]
                {summaryWhere}
                ORDER BY [summary].[last_trip] DESC, [summary].[last_trip_id] DESC, [summary].[contract_code] DESC
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY
                """;
            AddTripSummaryParameters(dataCommand, query, search);
            AddParameter(dataCommand, "@offset", DbType.Int64, skip);
            AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

            var items = new List<TripSummaryLine>();
            await using var reader = await dataCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(
                    new TripSummaryLine
                    {
                        VmfCode = ReadInt32(reader, "contract_code") ?? 0,
                        RegistrationNumber = string.Empty,
                        TripCount = ReadInt32(reader, "trip_count") ?? 0,
                        TotalKilometers = ReadDecimal(reader, "total_kilometers") ?? 0,
                        Department = ReadString(reader, "department") ?? string.Empty,
                        FirstTrip = ReadDateTime(reader, "first_trip") ?? DateTime.MinValue,
                        LastTrip = ReadDateTime(reader, "last_trip") ?? DateTime.MinValue,
                    }
                );
            }

            return new TripSummaryPage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<TripAuthorityVehicle>> GetTripAuthorityVehiclesAsync(
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        var contractColumns = await GetTableColumnsAsync(ContractTableName);
        var vehicleColumns = await GetTableColumnsAsync(VehicleTableName);
        var requiredContractColumns = new[]
        {
            "contract_code",
            "vmf_code",
            "site_code",
            "still_current",
            "contract_type",
        };
        var requiredVehicleColumns = new[]
        {
            "vmf_code",
            "vehicle_status_code",
            "fleet_number",
            "registration_number",
            "licence_due_date",
        };

        var missingColumns = requiredContractColumns
            .Where(column => !contractColumns.Contains(column))
            .Concat(requiredVehicleColumns.Where(column => !vehicleColumns.Contains(column)))
            .ToArray();
        if (missingColumns.Length > 0)
        {
            throw new InvalidOperationException(
                $"The required Trip Authority vehicle compatibility columns are not available: {string.Join(", ", missingColumns)}"
            );
        }

        var modelColumns = await GetTableColumnsAsync(ModelTableName);
        var makeColumns = await GetTableColumnsAsync(MakeTableName);
        var hasModelProjection = new[] { "model_code", "make_code", "model_description" }.All(
            modelColumns.Contains
        );
        var hasMakeProjection = new[] { "make_code", "make_description" }.All(makeColumns.Contains);

        var projection = new List<string>
        {
            "[c].[vmf_code] AS [vmf_code]",
            "[c].[contract_code] AS [contract_code]",
            "[c].[site_code] AS [site_code]",
            "[v].[fleet_number] AS [fleet_number]",
            "[v].[registration_number] AS [registration_number]",
            "[v].[licence_due_date] AS [licence_due_date]",
            "[c].[contract_type] AS [contract_type]",
            hasModelProjection
                ? "[m].[model_description] AS [model_description]"
                : "CAST(NULL AS varchar(250)) AS [model_description]",
            hasMakeProjection && hasModelProjection
                ? "[mk].[make_description] AS [make_description]"
                : "CAST(NULL AS varchar(250)) AS [make_description]",
        };

        var joins = new List<string>();
        if (hasModelProjection)
        {
            joins.Add(
                $"LEFT JOIN [dbo].[{ModelTableName}] AS [m] ON [m].[model_code] = [v].[model_code]"
            );
        }

        if (hasMakeProjection && hasModelProjection)
        {
            joins.Add(
                $"LEFT JOIN [dbo].[{MakeTableName}] AS [mk] ON [mk].[make_code] = [m].[make_code]"
            );
        }

        var conditions = new List<string>
        {
            "[c].[still_current] = 'Y'",
            "[v].[vehicle_status_code] > 0",
        };
        if (contractColumns.Contains("is_deleted"))
        {
            conditions.Add("ISNULL([c].[is_deleted], 0) = 0");
        }

        if (vehicleColumns.Contains("is_deleted"))
        {
            conditions.Add("ISNULL([v].[is_deleted], 0) = 0");
        }

        var siteParameters = new List<(string Name, object? Value)>();
        if (allowedSiteCodes is not null)
        {
            var siteCodes = allowedSiteCodes.Where(code => code > 0).Distinct().ToArray();
            if (siteCodes.Length == 0)
            {
                return [];
            }

            var placeholders = siteCodes
                .Select((_, index) => $"@allowedSite{index}")
                .ToArray();
            conditions.Add($"[c].[site_code] IN ({string.Join(", ", placeholders)})");
            for (var index = 0; index < siteCodes.Length; index++)
            {
                siteParameters.Add((placeholders[index], siteCodes[index]));
            }
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{ContractTableName}] AS [c]
                INNER JOIN [dbo].[{VehicleTableName}] AS [v] ON [v].[vmf_code] = [c].[vmf_code]
                {string.Join(Environment.NewLine, joins)}
                WHERE {string.Join(" AND ", conditions)}
                ORDER BY [v].[fleet_number], [c].[contract_code]
                """;
            foreach (var parameter in siteParameters)
            {
                AddParameter(command, parameter.Name, DbType.Int16, parameter.Value);
            }

            var results = new List<TripAuthorityVehicle>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(
                    new TripAuthorityVehicle(
                        ReadInt32(reader, "vmf_code") ?? 0,
                        ReadInt32(reader, "contract_code") ?? 0,
                        ReadInt16(reader, "site_code") ?? 0,
                        ReadString(reader, "fleet_number"),
                        ReadString(reader, "registration_number"),
                        ReadDateTime(reader, "licence_due_date"),
                        ReadString(reader, "make_description"),
                        ReadString(reader, "model_description"),
                        ReadString(reader, "contract_type")
                    )
                );
            }

            return results;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public Task<TripAuthorityVehiclePage> GetTripAuthorityInServicePageAsync(
        TripAuthorityVehiclePageQuery query
    ) => GetTripAuthorityVehiclePageAsync(query, includeOutVehicles: false);

    public Task<TripAuthorityVehiclePage> GetTripAuthorityOutPageAsync(
        TripAuthorityVehiclePageQuery query
    ) => GetTripAuthorityVehiclePageAsync(query, includeOutVehicles: true);

    private async Task<TripAuthorityVehiclePage> GetTripAuthorityVehiclePageAsync(
        TripAuthorityVehiclePageQuery query,
        bool includeOutVehicles
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        var normalizedQuery = query with
        {
            Page = Math.Max(1, query.Page),
            PageSize = Math.Clamp(query.PageSize, 1, 100),
            SearchMode = string.Equals(query.SearchMode, "GP", StringComparison.OrdinalIgnoreCase)
                ? "GP"
                : "GG",
            SearchTerm = string.IsNullOrWhiteSpace(query.SearchTerm)
                ? null
                : query.SearchTerm.Trim(),
        };

        if (normalizedQuery.AllowedSiteCodes is { Count: 0 })
        {
            return EmptyTripAuthorityVehiclePage(normalizedQuery);
        }

        // The legacy page only shows OUT rows for an authority-number search.
        // Keep the IN result empty for that filter instead of silently ignoring
        // the selected value and returning an unfiltered page.
        if (!includeOutVehicles && normalizedQuery.TripAuthorityCode.HasValue)
        {
            return EmptyTripAuthorityVehiclePage(normalizedQuery);
        }

        var contractColumns = await GetTableColumnsAsync(ContractTableName);
        var vehicleColumns = await GetTableColumnsAsync(VehicleTableName);
        var missingColumns = TripAuthorityVehicleContractColumns
            .Where(column => !contractColumns.Contains(column))
            .Concat(TripAuthorityVehicleColumns.Where(column => !vehicleColumns.Contains(column)))
            .ToArray();
        if (missingColumns.Length > 0)
        {
            throw new InvalidOperationException(
                $"The required Trip Authority vehicle compatibility columns are not available: {string.Join(", ", missingColumns)}"
            );
        }

        var tripColumns = await GetAvailableColumnsAsync();
        var modelColumns = await GetTableColumnsAsync(ModelTableName);
        var makeColumns = await GetTableColumnsAsync(MakeTableName);
        var hasModelProjection =
            vehicleColumns.Contains("model_code")
            && new[] { "model_code", "make_code", "model_description" }.All(modelColumns.Contains);
        var hasMakeProjection =
            hasModelProjection
            && new[] { "make_code", "make_description" }.All(makeColumns.Contains);

        var siteColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (normalizedQuery.DepartmentCode.HasValue)
        {
            siteColumns = await GetTableColumnsAsync("site");
            if (!new[] { "Site_code", "Depatrment_code" }.All(siteColumns.Contains))
            {
                // A requested department filter cannot be safely applied when
                // the legacy site relationship is unavailable. Fail closed.
                return EmptyTripAuthorityVehiclePage(normalizedQuery);
            }
        }

        var projection = new List<string>
        {
            "[c].[vmf_code] AS [vmf_code]",
            "[c].[contract_code] AS [contract_code]",
            "[c].[site_code] AS [site_code]",
            "[v].[fleet_number] AS [fleet_number]",
            "[v].[registration_number] AS [registration_number]",
            "[v].[licence_due_date] AS [licence_due_date]",
            "[c].[contract_type] AS [contract_type]",
            hasModelProjection
                ? "[m].[model_description] AS [model_description]"
                : "CAST(NULL AS varchar(250)) AS [model_description]",
            hasMakeProjection
                ? "[mk].[make_description] AS [make_description]"
                : "CAST(NULL AS varchar(250)) AS [make_description]",
            normalizedQuery.DepartmentCode.HasValue
                ? "[s].[Depatrment_code] AS [department_code]"
                : "CAST(NULL AS smallint) AS [department_code]",
            includeOutVehicles
                ? "[ot].[trip_authority_code] AS [trip_authority_code]"
                : "CAST(NULL AS int) AS [trip_authority_code]",
            includeOutVehicles
                ? "[ot].[trip_issue_date] AS [trip_issue_date]"
                : "CAST(NULL AS datetime2) AS [trip_issue_date]",
            includeOutVehicles
                ? "[ot].[trip_contract_code] AS [trip_contract_code]"
                : "CAST(NULL AS int) AS [trip_contract_code]",
        };

        var joins = new List<string>();
        if (hasModelProjection)
        {
            joins.Add(
                $"LEFT JOIN [dbo].[{ModelTableName}] AS [m] ON [m].[model_code] = [v].[model_code]"
            );
        }

        if (hasMakeProjection)
        {
            joins.Add(
                $"LEFT JOIN [dbo].[{MakeTableName}] AS [mk] ON [mk].[make_code] = [m].[make_code]"
            );
        }

        if (normalizedQuery.DepartmentCode.HasValue)
        {
            joins.Add("INNER JOIN [dbo].[site] AS [s] ON [s].[Site_code] = [c].[site_code]");
        }

        var conditions = new List<string>
        {
            "[c].[still_current] = 'Y'",
            "[v].[vehicle_status_code] > 0",
        };
        if (contractColumns.Contains("is_deleted"))
        {
            conditions.Add("([c].[is_deleted] = 0 OR [c].[is_deleted] IS NULL)");
        }

        if (vehicleColumns.Contains("is_deleted"))
        {
            conditions.Add("([v].[is_deleted] = 0 OR [v].[is_deleted] IS NULL)");
        }

        if (normalizedQuery.DepartmentCode.HasValue)
        {
            if (siteColumns.Contains("site_active"))
            {
                conditions.Add("[s].[site_active] = 1");
            }

            if (siteColumns.Contains("is_deleted"))
            {
                conditions.Add("([s].[is_deleted] = 0 OR [s].[is_deleted] IS NULL)");
            }
        }

        var openTripNotDeletedForTripAlias = tripColumns.Contains("is_deleted")
            ? "AND ([open_trip].[is_deleted] = 0 OR [open_trip].[is_deleted] IS NULL)"
            : string.Empty;
        var openTripNotDeletedForContractAlias = contractColumns.Contains("is_deleted")
            ? "AND ([open_contract].[is_deleted] = 0 OR [open_contract].[is_deleted] IS NULL)"
            : string.Empty;

        var openTripsCte = includeOutVehicles
            ? $"""
                [OpenTrips] AS (
                    SELECT
                        [open_trip].[trip_authority_code] AS [trip_authority_code],
                        [open_trip].[contract_code] AS [trip_contract_code],
                        [open_trip].[issue_date] AS [trip_issue_date],
                        [open_contract].[vmf_code] AS [vmf_code],
                        ROW_NUMBER() OVER (
                            PARTITION BY [open_contract].[vmf_code]
                            ORDER BY [open_trip].[issue_date] DESC, [open_trip].[trip_authority_code] DESC
                        ) AS [trip_rank]
                    FROM [dbo].[{TableName}] AS [open_trip]
                    INNER JOIN [dbo].[{ContractTableName}] AS [open_contract]
                        ON [open_contract].[contract_code] = [open_trip].[contract_code]
                    WHERE [open_trip].[end_odo_meter] IS NULL
                      {openTripNotDeletedForTripAlias}
                      {openTripNotDeletedForContractAlias}
                ),
                """
            : string.Empty;

        if (includeOutVehicles)
        {
            joins.Add(
                "INNER JOIN [OpenTrips] AS [ot] ON [ot].[vmf_code] = [c].[vmf_code] AND [ot].[trip_rank] = 1"
            );
        }
        else
        {
            conditions.Add(
                $"""
                NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[{TableName}] AS [open_trip]
                    INNER JOIN [dbo].[{ContractTableName}] AS [open_contract]
                        ON [open_contract].[contract_code] = [open_trip].[contract_code]
                    WHERE [open_contract].[vmf_code] = [c].[vmf_code]
                      AND [open_trip].[end_odo_meter] IS NULL
                      {openTripNotDeletedForTripAlias}
                      {openTripNotDeletedForContractAlias}
                )
                """
            );
        }

        var rowsCte = $"""
            WITH {openTripsCte}[Rows] AS (
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{ContractTableName}] AS [c]
                INNER JOIN [dbo].[{VehicleTableName}] AS [v]
                    ON [v].[vmf_code] = [c].[vmf_code]
                {string.Join(Environment.NewLine, joins)}
                WHERE {string.Join(" AND ", conditions)}
            )
            """;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            int total;
            await using (var countCommand = connection.CreateCommand())
            {
                countCommand.Transaction = transaction;
                var filters = BuildTripAuthorityVehiclePageFilters(
                    countCommand,
                    normalizedQuery,
                    includeOutVehicles
                );
                countCommand.CommandText = $"""
                    {rowsCte}
                    SELECT COUNT(1)
                    FROM [Rows] AS [r]
                    WHERE {string.Join(" AND ", filters)}
                    """;
                total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
            }

            var totalPages = Math.Max(
                1,
                (int)Math.Ceiling(total / (double)normalizedQuery.PageSize)
            );
            var page = Math.Min(normalizedQuery.Page, totalPages);
            var skip = checked((long)(page - 1) * normalizedQuery.PageSize);

            await using var dataCommand = connection.CreateCommand();
            dataCommand.Transaction = transaction;
            var dataFilters = BuildTripAuthorityVehiclePageFilters(
                dataCommand,
                normalizedQuery,
                includeOutVehicles
            );
            var orderBy = includeOutVehicles
                ? "[r].[trip_issue_date] DESC, [r].[trip_authority_code] DESC, COALESCE([r].[fleet_number], ''), [r].[vmf_code], [r].[contract_code]"
                : "COALESCE([r].[fleet_number], ''), [r].[vmf_code], [r].[contract_code]";
            dataCommand.CommandText = $"""
                {rowsCte}
                SELECT
                    [r].[vmf_code],
                    [r].[contract_code],
                    [r].[site_code],
                    [r].[fleet_number],
                    [r].[registration_number],
                    [r].[licence_due_date],
                    [r].[make_description],
                    [r].[model_description],
                    [r].[contract_type],
                    [r].[trip_authority_code],
                    [r].[trip_contract_code]
                FROM [Rows] AS [r]
                WHERE {string.Join(" AND ", dataFilters)}
                ORDER BY {orderBy}
                OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
                """;
            AddParameter(dataCommand, "@skip", DbType.Int64, skip);
            AddParameter(dataCommand, "@pageSize", DbType.Int32, normalizedQuery.PageSize);

            var items = new List<TripAuthorityVehiclePageItem>();
            await using var reader = await dataCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(
                    new TripAuthorityVehiclePageItem(
                        ReadInt32(reader, "vmf_code") ?? 0,
                        includeOutVehicles
                            ? ReadInt32(reader, "trip_contract_code") ?? 0
                            : ReadInt32(reader, "contract_code") ?? 0,
                        ReadInt16(reader, "site_code") ?? 0,
                        ReadString(reader, "fleet_number"),
                        ReadString(reader, "registration_number"),
                        ReadDateTime(reader, "licence_due_date"),
                        ReadString(reader, "make_description"),
                        ReadString(reader, "model_description"),
                        ReadString(reader, "contract_type"),
                        includeOutVehicles ? ReadInt32(reader, "trip_authority_code") : null
                    )
                );
            }

            return new TripAuthorityVehiclePage(items, page, normalizedQuery.PageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static TripAuthorityVehiclePage EmptyTripAuthorityVehiclePage(
        TripAuthorityVehiclePageQuery query
    ) => new([], 1, query.PageSize, 0);

    private static List<string> BuildTripAuthorityVehiclePageFilters(
        DbCommand command,
        TripAuthorityVehiclePageQuery query,
        bool includeOutVehicles
    )
    {
        var filters = new List<string> { "1 = 1" };

        if (query.AllowedSiteCodes is not null)
        {
            var siteCodes = query.AllowedSiteCodes.Where(code => code > 0).Distinct().ToArray();
            if (siteCodes.Length == 0)
            {
                filters.Add("1 = 0");
            }
            else
            {
                var placeholders = siteCodes
                    .Select((_, index) => $"@allowedSite{index}")
                    .ToArray();
                filters.Add($"[r].[site_code] IN ({string.Join(", ", placeholders)})");
                for (var index = 0; index < siteCodes.Length; index++)
                {
                    AddParameter(command, placeholders[index], DbType.Int16, siteCodes[index]);
                }
            }
        }

        if (query.SiteCode.HasValue)
        {
            filters.Add("[r].[site_code] = @siteCode");
            AddParameter(command, "@siteCode", DbType.Int16, query.SiteCode.Value);
        }

        if (query.DepartmentCode.HasValue)
        {
            filters.Add("[r].[department_code] = @departmentCode");
            AddParameter(command, "@departmentCode", DbType.Int16, query.DepartmentCode.Value);
        }

        if (includeOutVehicles && query.TripAuthorityCode.HasValue)
        {
            filters.Add("[r].[trip_authority_code] = @tripAuthorityCode");
            AddParameter(
                command,
                "@tripAuthorityCode",
                DbType.Int32,
                query.TripAuthorityCode.Value
            );
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchColumn =
                query.SearchMode == "GP" ? "[r].[registration_number]" : "[r].[fleet_number]";
            filters.Add($"LOWER(COALESCE({searchColumn}, '')) LIKE @search ESCAPE '\\'");
            AddParameter(
                command,
                "@search",
                DbType.String,
                $"%{EscapeLikePattern(query.SearchTerm)}%"
            );
        }

        return filters;
    }

    private static string EscapeLikePattern(string value) =>
        value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("[", "\\[", StringComparison.Ordinal);

    public async Task<IEnumerable<Trip>> GetTripsByContractAsync(
        int contractCode,
        IReadOnlySet<short>? allowedSiteCodes = null
    ) =>
        await QueryAsync(
            "[t].[contract_code] = @contractCode",
            command => AddParameter(command, "@contractCode", DbType.Int32, contractCode),
            allowedSiteCodes: allowedSiteCodes
        );

    public async Task<bool> HasOpenTripAuthoritiesAsync(int contractCode)
    {
        var hasLegacyProcedure = await IsLegacyProcedureAvailableAsync(
            "NEW_DEV_VAL_OpenTripAuthority",
            "@contract_code"
        );

        if (!hasLegacyProcedure)
        {
            var now = DateTime.Now;
            var trips = await GetTripsByContractAsync(contractCode);
            return trips.Any(trip => trip.expiry_date.HasValue && trip.expiry_date.Value.Date >= now.Date);
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "NEW_DEV_VAL_OpenTripAuthority";
            command.CommandTimeout = 0;
            AddParameter(command, "@contract_code", DbType.Int32, contractCode);

            var now = DateTime.Now;
            await using var reader = await command.ExecuteReaderAsync();
            var expiryOrdinal = reader.GetOrdinal("expiry_date");
            while (await reader.ReadAsync())
            {
                if (
                    !reader.IsDBNull(expiryOrdinal)
                    && Convert.ToDateTime(reader.GetValue(expiryOrdinal), CultureInfo.InvariantCulture)
                        .Date
                        >= now.Date
                )
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<Trip>> GetTripsByVehicleAsync(
        int vmfCode,
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        var contractColumns = await GetTableColumnsAsync(ContractTableName);
        if (!RequiredContractColumns.All(contractColumns.Contains))
        {
            return [];
        }

        return await QueryAsync(
            "[c].[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode),
            allowedSiteCodes: allowedSiteCodes,
            contractJoin: true
        );
    }

    public async Task<IEnumerable<Trip>> GetTripsByDriverAsync(
        string driverId,
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        if (string.IsNullOrWhiteSpace(driverId))
        {
            return [];
        }

        var contractColumns = await GetTableColumnsAsync(ContractTableName);
        if (!RequiredContractColumns.All(contractColumns.Contains))
        {
            return [];
        }

        var driverColumn =
            contractColumns.Contains("site_driver_code") ? "[c].[site_driver_code] = @driverId"
            : contractColumns.Contains("Driver_id") ? "[c].[Driver_id] = @driverId"
            : null;
        if (driverColumn is null)
        {
            return [];
        }

        return await QueryAsync(
            driverColumn,
            command => AddParameter(command, "@driverId", DbType.String, driverId.Trim()),
            allowedSiteCodes: allowedSiteCodes,
            contractJoin: true
        );
    }

    public async Task<IEnumerable<Trip>> GetTripsByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        IReadOnlySet<short>? allowedSiteCodes = null
    ) =>
        await QueryAsync(
            "[t].[issue_date] >= @startDate AND [t].[issue_date] <= @endDate",
            command =>
            {
                AddParameter(command, "@startDate", DbType.DateTime, startDate);
                AddParameter(command, "@endDate", DbType.DateTime, endDate);
            },
            allowedSiteCodes: allowedSiteCodes
        );

    public async Task<Trip> CreateAsync(Trip trip, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(trip);

        // The archived create flow is DEV_INS_TripXML and requires drivers,
        // passengers, and routes. The scalar-only endpoint cannot produce
        // that document, so never let it bypass the procedure on a client
        // database that still exposes the legacy workflow. The full
        // CreateAuthorityAsync path performs the procedure-first mutation;
        // this method remains its explicit fallback when the procedure is
        // genuinely absent.
        if (
            await IsLegacyProcedureAvailableAsync(
                "DEV_INS_TripXML",
                "@Trip",
                "@XmlDocument"
            )
        )
        {
            throw new NotSupportedException(
                "The legacy trip-authority procedure requires the complete driver, passenger, and route workflow. Use the full trip-authority capture path."
            );
        }

        await EnsureLegacyTriggersAsync(TableName, TripMutationTriggerNames);

        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        trip.date_created = now;
        trip.date_updated = now;
        trip.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        trip.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        trip.is_deleted = false;

        var values = BuildValues(
            trip,
            availableColumns,
            includeKey: false,
            includeCreateAudit: true
        );
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                INSERT INTO [dbo].[{TableName}] ({string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}]")
                )})
                OUTPUT INSERTED.[trip_authority_code]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
            AddParameters(command, values);
            trip.trip_authority_code = Convert.ToInt32(await command.ExecuteScalarAsync());
            return trip;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<Trip> CreateAuthorityAsync(
        Trip trip,
        IReadOnlyList<TripAuthorityDriverInput> drivers,
        IReadOnlyList<TripAuthorityPassengerInput> passengers,
        IReadOnlyList<TripAuthorityRouteInput> routes,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(trip);
        ArgumentNullException.ThrowIfNull(drivers);
        ArgumentNullException.ThrowIfNull(passengers);
        ArgumentNullException.ThrowIfNull(routes);

        if (
            await IsLegacyProcedureAvailableAsync(
                "DEV_INS_TripXML",
                "@Trip",
                "@XmlDocument"
            )
        )
        {
            var xml = BuildLegacyCreateTripXml(trip, drivers, passengers, routes, currentUserId);
            var connection = _context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandType = CommandType.StoredProcedure;
                command.CommandText = "DEV_INS_TripXML";
                command.CommandTimeout = 0;

                var output = command.CreateParameter();
                output.ParameterName = "@Trip";
                output.DbType = DbType.Int32;
                output.Direction = ParameterDirection.Output;
                command.Parameters.Add(output);
                AddParameter(command, "@XmlDocument", DbType.String, xml);
                await command.ExecuteNonQueryAsync();

                if (output.Value is null or DBNull || Convert.ToInt32(output.Value) <= 0)
                {
                    throw new InvalidOperationException(
                        "The legacy DEV_INS_TripXML procedure completed without returning the created trip authority code."
                    );
                }

                return await GetByIdAsync(Convert.ToInt32(output.Value))
                    ?? throw new InvalidOperationException(
                        "The legacy DEV_INS_TripXML procedure created a trip that could not be read back."
                    );
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        var driverTable = await ResolveTripDriverTableAsync();
        if (driverTable is null)
        {
            throw new InvalidOperationException(
                "Neither trip_driver nor trip_drivers contains the required legacy trip-driver columns."
            );
        }

        var passengerColumns = await GetTableColumnsAsync(TripPassengerTableName);
        var routeColumns = await GetTableColumnsAsync(RouteDetailTableName);
        var requiredRouteColumns = new[] { "trip_authority_code", "start_date", "end_date" };
        if (!requiredRouteColumns.All(routeColumns.Contains))
        {
            throw new InvalidOperationException(
                "The route_details compatibility columns are not available for creating a trip authority."
            );
        }

        await EnsureLegacyTriggersAsync(RouteDetailTableName, RouteInsertTriggerNames);

        var existingTransaction = _context.Database.CurrentTransaction;
        var transaction = existingTransaction is null
            ? await _context.Database.BeginTransactionAsync()
            : null;

        try
        {
            var createdTrip = await CreateAsync(trip, currentUserId);
            foreach (var driver in drivers)
            {
                await InsertTripDriverAsync(
                    driverTable,
                    driver,
                    createdTrip.trip_authority_code,
                    currentUserId
                );
            }

            // Some later databases contain the lookup-shaped trip_passengers
            // table without the legacy trip_authority_code relationship. Do
            // not create orphan rows in that shape; the client-era table is
            // written when its relationship is available.
            if (
                new[] { "trip_passenger_name", "trip_authority_code" }.All(
                    passengerColumns.Contains
                )
            )
            {
                foreach (var passenger in passengers)
                {
                    await InsertTripPassengerAsync(
                        passenger,
                        createdTrip.trip_authority_code,
                        passengerColumns,
                        currentUserId
                    );
                }
            }

            foreach (var route in routes)
            {
                await InsertRouteDetailAsync(
                    route,
                    createdTrip.trip_authority_code,
                    routeColumns,
                    currentUserId
                );
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync();
            }

            return createdTrip;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task UpdateAsync(Trip trip, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(trip);

        var existing =
            await GetByIdAsync(trip.trip_authority_code)
            ?? throw new InvalidOperationException(
                $"Trip with trip_authority_code {trip.trip_authority_code} not found"
            );

        // A posted DTO is not an ownership-transfer mechanism. Preserve the
        // original capturer unless a dedicated reassignment workflow is used.
        trip.user_access_code = existing.user_access_code;

        // The archived SaveToDB path always submits the complete trip XML to
        // DEV_UPD_TripXML. That procedure replaces driver/passenger rows,
        // updates route rows by RouteDBId, and commits the scalar update in
        // one transaction. Prefer it whenever the exact procedure exists.
        if (
            await IsLegacyProcedureAvailableAsync(
                "DEV_UPD_TripXML",
                "@Trip",
                "@XmlDocument"
            )
        )
        {
            var details = await GetDetailsAsync(trip.trip_authority_code)
                ?? throw new InvalidOperationException(
                    $"Trip with trip_authority_code {trip.trip_authority_code} and its related records could not be read."
                );
            trip.Contract ??= existing.Contract;
            var xml = BuildLegacyUpdateTripXml(trip, details, currentUserId);
            await ExecuteInLegacyTransactionAsync(
                "FIS_TripUpdate",
                async () =>
                {
                    await ExecuteLegacyProcedureAsync(
                        "DEV_UPD_TripXML",
                        new ProcedureParameter("@Trip", DbType.Int32, trip.trip_authority_code),
                        new ProcedureParameter("@XmlDocument", DbType.String, xml)
                    );

                    // DEV_UPD_TripXML uses XML UserID for both the edit actor
                    // and trip_authorities.user_access_code. Keep the actor in
                    // route/audit fields, then restore the original owner in
                    // the same transaction.
                    await RestoreTripOwnerAsync(
                        trip.trip_authority_code,
                        existing.user_access_code,
                        currentUserId
                    );
                    return true;
                }
            );
            return;
        }

        // Compatibility fallback only where DEV_UPD_TripXML is genuinely
        // absent. Keep the legacy trigger required for this reduced path.
        await EnsureLegacyTriggersAsync(TableName, TripMutationTriggerNames);
        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        trip.date_created = existing.date_created;
        trip.created_by_user_code = existing.created_by_user_code;
        trip.date_updated = now;
        trip.modified_by_user_code =
            currentUserId > 0 ? currentUserId : existing.modified_by_user_code;
        trip.is_deleted = existing.is_deleted;

        var values = BuildValues(
            trip,
            availableColumns,
            includeKey: false,
            includeCreateAudit: false
        );
        if (values.Count == 0)
        {
            return;
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                UPDATE [dbo].[{TableName}]
                SET {string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}] = {value.Parameter}")
                )}
                WHERE [trip_authority_code] = @tripId
                """;
            AddParameters(command, values);
            AddParameter(command, "@tripId", DbType.Int32, trip.trip_authority_code);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<Trip> RenewAsync(
        int tripId,
        DateTime newExpiryDate,
        IReadOnlyList<TripAuthorityRouteUpdate> routes,
        int? endOdometer,
        int currentUserId,
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        ArgumentNullException.ThrowIfNull(routes);

        var details = await GetDetailsAsync(tripId, allowedSiteCodes)
            ?? throw new InvalidOperationException(
                $"Trip with trip_authority_code {tripId} and its related records could not be read."
            );
        if (details.Trip.Contract is null)
        {
            throw new InvalidOperationException(
                "The legacy trip-renewal workflow requires the linked contract and site context."
            );
        }

        var expectedParameters = new[] { "@IncommingTrip", "@XmlDocument", "@Tript" };
        if (
            !await IsLegacyProcedureAvailableAsync(
                "DEV_UPD_TripXMLForRenewalOfTrip",
                expectedParameters
            )
        )
        {
            throw new NotSupportedException(
                "The legacy trip-renewal procedure is unavailable; renewal cannot be approximated with direct DML."
            );
        }

        if (routes.Count != details.Routes.Count)
        {
            throw new InvalidOperationException(
                "The legacy trip-renewal procedure requires a complete set of route updates."
            );
        }

        var submittedRoutes = routes.ToDictionary(route => route.RouteCode);
        var orderedRoutes = new List<TripAuthorityRouteUpdate>(details.Routes.Count);
        foreach (var route in details.Routes)
        {
            if (!submittedRoutes.TryGetValue(route.RouteCode, out var submitted))
            {
                throw new InvalidOperationException(
                    $"Route {route.RouteCode} does not belong to trip {tripId}."
                );
            }

            orderedRoutes.Add(submitted);
        }

        var trip = details.Trip;
        var owner = trip.user_access_code;
        var resolvedEndOdometer = endOdometer ?? trip.end_odo_meter ?? orderedRoutes.Max(route => route.EndOdometer);
        trip.expiry_date = newExpiryDate;
        trip.end_odo_meter = resolvedEndOdometer;

        var drivers = details.Drivers
            .Select(driver => new TripAuthorityDriverInput(
                driver.Name,
                driver.IdentityNumber,
                driver.IsPrimary,
                driver.SiteCode,
                driver.LicenceTypeCode,
                driver.PassportNumber,
                driver.PersalNumber,
                driver.ContractNumber,
                driver.LicenceNumber,
                driver.LicenceIssueDate,
                driver.LicenceLastVerifiedDate,
                driver.HasPdp,
                driver.PdpExpiryDate,
                driver.LicenceExpiryDate,
                driver.IsActive
            ))
            .ToArray();
        var passengers = details.Passengers
            .Select(passenger => new TripAuthorityPassengerInput(passenger.Name ?? string.Empty))
            .ToArray();
        var renewalRoutes = details.Routes
            .Select(route =>
            {
                var submitted = submittedRoutes[route.RouteCode];
                return new TripAuthorityRouteInput(
                    route.StartDate ?? trip.issue_date,
                    route.EndDate ?? newExpiryDate,
                    route.StartLocation,
                    route.EndLocation,
                    route.EstimatedDistance,
                    route.ResponsibilityCode ?? string.Empty,
                    route.ObjectiveCode ?? string.Empty,
                    route.ProjectNumber ?? string.Empty,
                    route.FundCode ?? string.Empty,
                    submitted.StartOdometer ?? route.StartOdometer
                );
            })
            .ToArray();
        var routeCodes = details.Routes.Select(route => route.RouteCode).ToArray();
        var routeEndOdometers = orderedRoutes
            .Select(route => (int?)route.EndOdometer)
            .ToArray();
        var routeDistances = orderedRoutes
            .Select(route => (int?)route.Distance)
            .ToArray();
        var xml = BuildLegacyCreateTripXml(
            trip,
            drivers,
            passengers,
            renewalRoutes,
            currentUserId,
            routeCodes,
            routeEndOdometers,
            routeDistances,
            tripId,
            resolvedEndOdometer
        );

        return await ExecuteInLegacyTransactionAsync(
            "FIS_TripRenewal",
            async () =>
            {
                var renewedTripId = await ExecuteLegacyRenewalProcedureAsync(
                    tripId,
                    xml
                );

                // The archived procedure uses @UserID for both the action
                // actor and user_access_code. Restore ownership for both the
                // closed source authority and the newly issued authority in
                // the same transaction; an approver cannot become owner as a
                // side effect of renewing a trip.
                await RestoreTripOwnerAsync(tripId, owner);
                await RestoreTripOwnerAsync(renewedTripId, owner);

                return await GetByIdAsync(renewedTripId, allowedSiteCodes)
                    ?? throw new InvalidOperationException(
                        "The legacy trip-renewal procedure completed, but the new trip authority could not be read back."
                    );
            }
        );
    }

    public async Task CloseAsync(
        int tripId,
        IReadOnlyList<TripAuthorityRouteUpdate> routes,
        int? endOdometer,
        int currentUserId
    )
    {
        await EnsureLegacyTriggersAsync(RouteDetailTableName, RouteUpdateTriggerNames);
        await EnsureLegacyTriggersAsync(TableName, TripMutationTriggerNames);
        var routeColumns = await GetTableColumnsAsync(RouteDetailTableName);
        if (routes.Count > 0)
        {
            var requiredRouteColumns = new[]
            {
                "route_code",
                "trip_authority_code",
                "end_odo_meter",
            };
            if (!requiredRouteColumns.All(routeColumns.Contains))
            {
                throw new InvalidOperationException(
                    "The route_details compatibility columns are not available for closing this trip"
                );
            }
        }

        if (
            await IsLegacyProcedureAvailableAsync(
                "DEV_UPD_TripXMLForClosingOfTrip",
                "@Trip",
                "@XmlDocument"
            )
        )
        {
            var existingTripForClose = await GetByIdAsync(tripId)
                ?? throw new InvalidOperationException($"Trip {tripId} not found");
            var existingRoutes = await GetRouteDetailsAsync(tripId);
            if (existingRoutes.Count != routes.Count)
            {
                throw new InvalidOperationException(
                    "The legacy trip-close procedure requires a complete set of route end-odometer updates."
                );
            }

            var submittedRoutes = routes.ToDictionary(route => route.RouteCode);
            var xml = BuildLegacyCloseTripXml(
                tripId,
                existingRoutes,
                submittedRoutes,
                endOdometer,
                currentUserId
            );
            await ExecuteInLegacyTransactionAsync(
                "FIS_TripClose",
                async () =>
                {
                    await ExecuteLegacyProcedureAsync(
                        "DEV_UPD_TripXMLForClosingOfTrip",
                        new ProcedureParameter("@Trip", DbType.Int32, tripId),
                        new ProcedureParameter("@XmlDocument", DbType.String, xml)
                    );

                    // The archived close procedure uses XML @UserID for both
                    // the action actor and trip_authorities.user_access_code.
                    // Restore the original capturer in the same transaction;
                    // closing a trip is not an ownership-transfer workflow.
                    await RestoreTripOwnerAsync(tripId, existingTripForClose.user_access_code);
                    return true;
                }
            );
            return;
        }

        // Compatibility fallback only where DEV_UPD_TripXMLForClosingOfTrip is genuinely absent.
        var existingTrip = await GetByIdAsync(tripId)
            ?? throw new InvalidOperationException($"Trip {tripId} not found");
        // It must not call UpdateAsync below: when DEV_UPD_TripXML exists that
        // would replay every route through the XML update procedure after the
        // route-close writes, creating duplicate journal/rebill side effects.
        var existingTransaction = _context.Database.CurrentTransaction;
        var transaction = existingTransaction is null
            ? await _context.Database.BeginTransactionAsync()
            : null;

        try
        {
            foreach (var route in routes)
            {
                var updates = new List<string> { "[end_odo_meter] = @endOdometer" };
                await using var command = _context.Database.GetDbConnection().CreateCommand();
                command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                if (route.StartOdometer.HasValue && routeColumns.Contains("start_odo_meter"))
                {
                    updates.Add("[start_odo_meter] = @startOdometer");
                    AddParameter(command, "@startOdometer", DbType.Int32, route.StartOdometer.Value);
                }
                if (routeColumns.Contains("distance"))
                {
                    updates.Add("[distance] = @distance");
                    AddParameter(command, "@distance", DbType.Int32, route.Distance);
                }

                if (routeColumns.Contains("date_updated"))
                {
                    updates.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (routeColumns.Contains("modified_by_user_code"))
                {
                    updates.Add("[modified_by_user_code] = @modifiedByUserCode");
                    AddParameter(
                        command,
                        "@modifiedByUserCode",
                        DbType.Int32,
                        currentUserId > 0 ? currentUserId : null
                    );
                }

                var conditions = new List<string>
                {
                    "[route_code] = @routeCode",
                    "[trip_authority_code] = @tripId",
                };
                if (routeColumns.Contains("is_deleted"))
                {
                    conditions.Add("ISNULL([is_deleted], 0) = 0");
                }

                command.CommandText =
                    $"UPDATE [dbo].[{RouteDetailTableName}] SET {string.Join(", ", updates)} WHERE {string.Join(" AND ", conditions)}";
                AddParameter(command, "@endOdometer", DbType.Int32, route.EndOdometer);
                AddParameter(command, "@routeCode", DbType.Int32, route.RouteCode);
                AddParameter(command, "@tripId", DbType.Int32, tripId);
                var affected = await command.ExecuteNonQueryAsync();
                if (affected != 1)
                {
                    throw new InvalidOperationException(
                        $"Route {route.RouteCode} was not found for trip {tripId}"
                    );
                }
            }

            var tripColumns = await GetAvailableColumnsAsync();
            var tripUpdates = new List<string> { "[end_odo_meter] = @endOdometer" };
            await using (var tripCommand = _context.Database.GetDbConnection().CreateCommand())
            {
                tripCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
                AddParameter(
                    tripCommand,
                    "@endOdometer",
                    DbType.Int32,
                    endOdometer ?? existingTrip.end_odo_meter ?? 0
                );

                if (tripColumns.Contains("user_access_code"))
                {
                    tripUpdates.Add("[user_access_code] = @userAccessCode");
                    AddParameter(
                        tripCommand,
                        "@userAccessCode",
                        DbType.Int16,
                        existingTrip.user_access_code
                    );
                }
                if (tripColumns.Contains("date_updated"))
                {
                    tripUpdates.Add("[date_updated] = @dateUpdated");
                    AddParameter(tripCommand, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }
                if (tripColumns.Contains("modified_by_user_code"))
                {
                    tripUpdates.Add("[modified_by_user_code] = @modifiedByUserCode");
                    AddParameter(
                        tripCommand,
                        "@modifiedByUserCode",
                        DbType.Int32,
                        currentUserId > 0 ? currentUserId : null
                    );
                }

                tripCommand.CommandText =
                    $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", tripUpdates)} WHERE [trip_authority_code] = @tripId";
                AddParameter(tripCommand, "@tripId", DbType.Int32, tripId);
                if (await tripCommand.ExecuteNonQueryAsync() != 1)
                {
                    throw new InvalidOperationException($"Trip {tripId} was not found while closing it.");
                }
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync();
            }
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task DeleteAsync(int tripId, int currentUserId)
    {
        _ = currentUserId;
        var existing = await GetByIdAsync(tripId)
            ?? throw new KeyNotFoundException($"Trip {tripId} not found");
        var routes = await GetRouteDetailsAsync(tripId);

        // The legacy Trips menu has no arbitrary single-row delete action.
        // Its administrative cleanup procedure deletes only authorities that
        // have no route rows, and also preserves records already represented
        // in audit_trips. A universal modern DELETE would bypass those rules,
        // the trip-authority delete trigger, and route-cascade semantics.
        throw new NotSupportedException(
            routes.Count == 0
                ? $"Trip {existing.trip_authority_code} has no user-facing legacy delete workflow; use the administrative ADM_DEL_TripsWithoutRoutes procedure."
                : $"Trip {existing.trip_authority_code} has persisted routes and cannot be deleted through the legacy FIS workflow."
        );
    }

    private static string BuildLegacyCloseTripXml(
        int tripId,
        IReadOnlyList<TripAuthorityRoute> existingRoutes,
        IReadOnlyDictionary<int, TripAuthorityRouteUpdate> submittedRoutes,
        int? endOdometer,
        int currentUserId
    )
    {
        var tripEndOdometer = endOdometer ?? submittedRoutes.Values.Max(route => route.EndOdometer);
        var xml = new StringBuilder();
        xml.Append("<Root><Trips TripAuthorityNumber=\"")
            .Append(tripId.ToString(CultureInfo.InvariantCulture))
            .Append("\" EndODOMeter=\"")
            .Append(tripEndOdometer.ToString(CultureInfo.InvariantCulture))
            .Append("\" UserID=\"")
            .Append(currentUserId.ToString(CultureInfo.InvariantCulture))
            .Append("\">");

        foreach (var route in existingRoutes)
        {
            if (!submittedRoutes.TryGetValue(route.RouteCode, out var submitted))
            {
                throw new InvalidOperationException(
                    $"The legacy trip-close procedure requires route {route.RouteCode}."
                );
            }

            var startOdometer = submitted.StartOdometer ?? route.StartOdometer;
            if (
                !route.StartDate.HasValue
                || !route.EndDate.HasValue
                || !startOdometer.HasValue
                || string.IsNullOrWhiteSpace(route.StartLocation)
                || string.IsNullOrWhiteSpace(route.EndLocation)
                || !route.EstimatedDistance.HasValue
                || string.IsNullOrWhiteSpace(route.ObjectiveCode)
                || string.IsNullOrWhiteSpace(route.ResponsibilityCode)
                || string.IsNullOrWhiteSpace(route.ProjectNumber)
                || string.IsNullOrWhiteSpace(route.FundCode)
            )
            {
                throw new InvalidOperationException(
                    $"Route {route.RouteCode} lacks the persisted fields required by the legacy trip-close procedure."
                );
            }

            xml.Append("<Route RouteDBId=\"")
                .Append(route.RouteCode.ToString(CultureInfo.InvariantCulture))
                .Append("\" TripAuthorityNumber=\"")
                .Append(tripId.ToString(CultureInfo.InvariantCulture))
                .Append("\" StartLocationName=\"")
                .Append(EscapeXml(route.StartLocation))
                .Append("\" StartDate=\"")
                .Append(FormatXmlDate(route.StartDate.Value))
                .Append("\" StartODOMeter=\"")
                .Append(startOdometer.Value.ToString(CultureInfo.InvariantCulture))
                .Append("\" EndLocationName=\"")
                .Append(EscapeXml(route.EndLocation))
                .Append("\" EndDate=\"")
                .Append(FormatXmlDate(route.EndDate.Value))
                .Append("\" EndODOMeter=\"")
                .Append(submitted.EndOdometer.ToString(CultureInfo.InvariantCulture))
                .Append("\" EstimatedDistance=\"")
                .Append(route.EstimatedDistance.Value.ToString(CultureInfo.InvariantCulture))
                .Append("\" Objective=\"")
                .Append(EscapeXml(route.ObjectiveCode))
                .Append("\" Responsibility=\"")
                .Append(EscapeXml(route.ResponsibilityCode))
                .Append("\" ProjectNumber=\"")
                .Append(EscapeXml(route.ProjectNumber))
                .Append("\" Fund=\"")
                .Append(EscapeXml(route.FundCode))
                .Append("\" UserID=\"")
                .Append(currentUserId.ToString(CultureInfo.InvariantCulture))
                .Append("\" PrimaryDriver=\"0\" />");
        }

        xml.Append("</Trips></Root>");
        return xml.ToString();
    }

    private static string BuildLegacyCreateTripXml(
        Trip trip,
        IReadOnlyList<TripAuthorityDriverInput> drivers,
        IReadOnlyList<TripAuthorityPassengerInput> passengers,
        IReadOnlyList<TripAuthorityRouteInput> routes,
        int currentUserId,
        IReadOnlyList<int>? routeCodes = null,
        IReadOnlyList<int?>? routeEndOdometers = null,
        IReadOnlyList<int?>? routeDistances = null,
        int? tripAuthorityCode = null,
        int? tripEndOdometer = null
    )
    {
        var contract = trip.Contract
            ?? throw new InvalidOperationException(
                "A resolved contract is required to build the legacy trip-authority XML document."
            );
        var legacyEmptyDate = new DateTime(1900, 1, 1);
        var xml = new StringBuilder();
        xml.Append("<ROOT><Trip")
            .Append(" ApproverName=\"").Append(EscapeXml(trip.approver_name ?? string.Empty)).Append("\"")
            .Append(" ApproverRank=\"").Append(EscapeXml(trip.approver_rank ?? string.Empty)).Append("\"")
            .Append(" ApproverTelephone=\"").Append(EscapeXml(trip.approver_tel ?? string.Empty)).Append("\"")
            .Append(" IssueDate=\"").Append(FormatXmlDate(trip.issue_date)).Append("\"")
            .Append(" ExpiryDate=\"").Append(FormatXmlDate(trip.expiry_date ?? trip.issue_date)).Append("\"")
            .Append(" TripReason=\"").Append(EscapeXml(trip.trip_reason ?? string.Empty)).Append("\"")
            .Append(" TripRequestNumber=\"").Append(EscapeXml(trip.trip_request_number ?? string.Empty)).Append("\"")
            .Append(" TripAuthorityNumber=\"")
            .Append(tripAuthorityCode?.ToString(CultureInfo.InvariantCulture) ?? string.Empty)
            .Append("\"")
            .Append(" TripTypeName=\"\"")
            .Append(" IncidentTypeName=\"\"")
            .Append(" TripType=\"").Append(trip.trip_type_code.ToString(CultureInfo.InvariantCulture)).Append("\"")
            .Append(" IncidentType=\"").Append(trip.trip_incident_type_code.ToString(CultureInfo.InvariantCulture)).Append("\"")
            .Append(" IsMonthly=\"").Append(trip.Trip_Is_Monthly ? "1" : "0").Append("\"")
            .Append(" UserId=\"").Append(currentUserId.ToString(CultureInfo.InvariantCulture)).Append("\"")
            .Append(tripEndOdometer.HasValue
                ? $" EndODOMeter=\"{tripEndOdometer.Value.ToString(CultureInfo.InvariantCulture)}\""
                : string.Empty)
            .Append(" LockedForTransfer=\"").Append(trip.locked_for_transfer ? "1" : "0").Append("\">");

        xml.Append("<Contract")
            .Append(" ContractCode=\"").Append(trip.contract_code.ToString(CultureInfo.InvariantCulture)).Append("\"")
            .Append(" vmfCode=\"").Append(contract.vmf_code.ToString(CultureInfo.InvariantCulture)).Append("\"")
            .Append(" SiteCode=\"").Append(contract.site_code.ToString(CultureInfo.InvariantCulture)).Append("\"")
            .Append(" StartDate=\"").Append(FormatXmlDate(legacyEmptyDate)).Append("\"")
            .Append(" EndDate=\"").Append(FormatXmlDate(legacyEmptyDate)).Append("\"")
            .Append(" StartODOMeter=\"").Append(contract.start_odometer.ToString(CultureInfo.InvariantCulture)).Append("\"")
            .Append(" EndODOMeter=\"0\" IsCurrent=\"True\" ContractType=\"\" ChargedUntil=\"")
            .Append(FormatXmlDate(legacyEmptyDate)).Append("\" Responsibility=\"\" Objective=\"\" />");

        foreach (var driver in drivers)
        {
            xml.Append("<Driver")
                // DEV_INS/UPD_TripXML does not consume DriverDBId. The
                // archived page supplied a site-driver id, which is not part
                // of the modern trip-driver input contract; do not mislabel a
                // site code as a driver id.
                .Append(" DriverDBId=\"\"")
                .Append(" DriverName=\"").Append(EscapeXml(driver.Name ?? string.Empty)).Append("\"")
                .Append(" DriverSAID=\"").Append(EscapeXml(driver.IdentityNumber ?? string.Empty)).Append("\"")
                .Append(" DriverIsPrimary=\"").Append(driver.IsPrimary ? "True" : "False").Append("\"")
                .Append(" DriverLicenseTypeId=\"").Append(driver.LicenceTypeCode?.ToString(CultureInfo.InvariantCulture) ?? "").Append("\"")
                .Append(" DriverPassportNumber=\"").Append(EscapeXml(driver.PassportNumber ?? string.Empty)).Append("\"")
                .Append(" DriverPersalNumber=\"").Append(EscapeXml(driver.PersalNumber ?? string.Empty)).Append("\"")
                .Append(" DriverContractNo=\"").Append(EscapeXml(driver.ContractNumber ?? string.Empty)).Append("\"")
                .Append(" DriverLicenceNumber=\"").Append(EscapeXml(driver.LicenceNumber ?? string.Empty)).Append("\"")
                .Append(" DriverLicenceIssueDate=\"").Append(FormatXmlDate(driver.LicenceIssueDate ?? legacyEmptyDate)).Append("\"")
                .Append(" DriverLicenceLastVerifiedDate=\"").Append(FormatXmlDate(driver.LicenceLastVerifiedDate ?? legacyEmptyDate)).Append("\"")
                .Append(" DriverHasPDP=\"").Append(driver.HasPdp ? "YES" : "NO").Append("\"")
                .Append(" DriverPDPExpiryDate=\"").Append(FormatXmlDate(driver.PdpExpiryDate ?? legacyEmptyDate)).Append("\"")
                .Append(" DriverLicenseExpiryDate=\"").Append(FormatXmlDate(driver.LicenceExpiryDate ?? legacyEmptyDate)).Append("\"")
                .Append(" DriverIsActive=\"").Append(driver.IsActive ? "YES" : "NO").Append("\">")
                // The archived DEV_INS/UPD_TripXML OpenXML contract reads
                // SiteCode from ./Contract/@SiteCode relative to each Driver
                // node. Keep the trip-level Contract node for the authority
                // insert and also emit this child node so the legacy
                // procedure persists the validated driver site instead of
                // silently inserting NULL.
                .Append("<Contract SiteCode=\"")
                .Append((driver.SiteCode ?? contract.site_code).ToString(CultureInfo.InvariantCulture))
                .Append("\" />");
            xml.Append("</Driver>");
        }

        foreach (var passenger in passengers)
        {
            xml.Append("<Passenger PassengerDBId=\"\" PassengerName=\"")
                .Append(EscapeXml(passenger.Name))
                .Append("\" PersalNumber=\"\" />");
        }

        for (var index = 0; index < routes.Count; index++)
        {
            var route = routes[index];
            var routeCode = routeCodes is not null && index < routeCodes.Count
                ? routeCodes[index].ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            var endOdometer = routeEndOdometers is not null && index < routeEndOdometers.Count
                ? routeEndOdometers[index]?.ToString(CultureInfo.InvariantCulture) ?? string.Empty
                : string.Empty;
            var distance = routeDistances is not null && index < routeDistances.Count
                ? routeDistances[index]?.ToString(CultureInfo.InvariantCulture) ?? "0"
                : "0";
            xml.Append("<RouteItem RouteDBId=\"").Append(routeCode).Append("\"")
                .Append(" StartDate=\"").Append(FormatXmlDate(route.StartDate)).Append("\"")
                .Append(" EndDate=\"").Append(FormatXmlDate(route.EndDate)).Append("\"")
                .Append(" StartODOMeter=\"").Append(route.StartOdometer?.ToString(CultureInfo.InvariantCulture) ?? "0").Append("\"")
                .Append(" EndODOMeter=\"").Append(endOdometer).Append("\"")
                .Append(" Responsibility=\"").Append(EscapeXml(route.ResponsibilityCode ?? string.Empty)).Append("\"")
                .Append(" Objective=\"").Append(EscapeXml(route.ObjectiveCode ?? string.Empty)).Append("\"")
                .Append(" StartLocationName=\"").Append(EscapeXml(route.StartLocation ?? string.Empty)).Append("\"")
                .Append(" EndLocationName=\"").Append(EscapeXml(route.EndLocation ?? string.Empty)).Append("\"")
                .Append(" EstimatedDistance=\"").Append(route.EstimatedDistance?.ToString(CultureInfo.InvariantCulture) ?? "0").Append("\"")
                .Append(" Distance=\"").Append(distance).Append("\" EditedBy=\"").Append(currentUserId.ToString(CultureInfo.InvariantCulture)).Append("\"")
                .Append(" ProjectNumber=\"").Append(EscapeXml(route.ProjectNumber ?? string.Empty)).Append("\"")
                .Append(" Fund=\"").Append(EscapeXml(route.FundCode ?? string.Empty)).Append("\" />");
        }

        xml.Append("</Trip></ROOT>");
        return xml.ToString();
    }

    private static string BuildLegacyUpdateTripXml(
        Trip trip,
        TripAuthorityDetails details,
        int currentUserId
    )
    {
        var drivers = details.Drivers
            .Select(driver => new TripAuthorityDriverInput(
                driver.Name,
                driver.IdentityNumber,
                driver.IsPrimary,
                driver.SiteCode,
                driver.LicenceTypeCode,
                driver.PassportNumber,
                driver.PersalNumber,
                driver.ContractNumber,
                driver.LicenceNumber,
                driver.LicenceIssueDate,
                driver.LicenceLastVerifiedDate,
                driver.HasPdp,
                driver.PdpExpiryDate,
                driver.LicenceExpiryDate,
                driver.IsActive
            ))
            .ToArray();
        var passengers = details.Passengers
            .Select(passenger => new TripAuthorityPassengerInput(passenger.Name ?? string.Empty))
            .ToArray();
        var legacyEmptyDate = new DateTime(1900, 1, 1);
        var routes = details.Routes
            .Select(route => new TripAuthorityRouteInput(
                route.StartDate ?? legacyEmptyDate,
                route.EndDate ?? legacyEmptyDate,
                route.StartLocation,
                route.EndLocation,
                route.EstimatedDistance,
                route.ResponsibilityCode ?? string.Empty,
                route.ObjectiveCode ?? string.Empty,
                route.ProjectNumber ?? string.Empty,
                route.FundCode ?? string.Empty,
                route.StartOdometer
            ))
            .ToArray();
        var routeCodes = details.Routes.Select(route => route.RouteCode).ToArray();
        var routeEndOdometers = details.Routes.Select(route => route.EndOdometer).ToArray();
        var routeDistances = details.Routes.Select(route => route.Distance).ToArray();

        return BuildLegacyCreateTripXml(
            trip,
            drivers,
            passengers,
            routes,
            // The legacy procedure writes UserID to user_access_code. Pass the
            // authenticated actor so route-side modified_by_user_code and the
            // audit trigger identify the editor; the surrounding transaction
            // restores the original authority owner after the procedure.
            currentUserId,
            routeCodes,
            routeEndOdometers,
            routeDistances,
            trip.trip_authority_code
        );
    }

    private async Task<int> ExecuteLegacyRenewalProcedureAsync(
        int tripId,
        string xml
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = "DEV_UPD_TripXMLForRenewalOfTrip";
            command.CommandTimeout = 0;

            AddParameter(command, "@IncommingTrip", DbType.Int32, tripId);
            AddParameter(command, "@XmlDocument", DbType.String, xml);
            var output = command.CreateParameter();
            output.ParameterName = "@Tript";
            output.DbType = DbType.String;
            output.Size = 150;
            output.Direction = ParameterDirection.Output;
            command.Parameters.Add(output);

            await command.ExecuteNonQueryAsync();
            var raw = output.Value is null or DBNull
                ? null
                : Convert.ToString(output.Value, CultureInfo.InvariantCulture);
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var renewedTripId)
                || renewedTripId <= 0)
            {
                throw new InvalidOperationException(
                    $"The legacy trip-renewal procedure completed without returning a new trip authority code for trip {tripId}."
                );
            }

            return renewedTripId;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task RestoreTripOwnerAsync(
        int tripId,
        short? owner,
        int? modifiedByUserId = null
    )
    {
        if (tripId <= 0)
        {
            return;
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var columns = await GetTableColumnsAsync(TableName);
            var assignments = new List<string> { "[user_access_code] = @owner" };
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var actorUserId = modifiedByUserId.GetValueOrDefault();
            if (actorUserId > 0 && columns.Contains("modified_by_user_code"))
            {
                assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                AddParameter(
                    command,
                    "@modifiedByUserCode",
                    DbType.Int32,
                    actorUserId
                );
            }
            if (actorUserId > 0 && columns.Contains("date_updated"))
            {
                assignments.Add("[date_updated] = @dateUpdated");
                AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
            }
            command.CommandText = $"""
                UPDATE [dbo].[{TableName}]
                SET {string.Join(", ", assignments)}
                WHERE [trip_authority_code] = @tripId
                """;
            AddParameter(command, "@owner", DbType.Int16, owner);
            AddParameter(command, "@tripId", DbType.Int32, tripId);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<T> ExecuteInLegacyTransactionAsync<T>(
        string savepointName,
        Func<Task<T>> operation
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(savepointName);
        ArgumentNullException.ThrowIfNull(operation);

        var existingTransaction = _context.Database.CurrentTransaction;
        var ownsTransaction = existingTransaction is null;
        var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync()
            : null;

        if (!ownsTransaction)
        {
            await existingTransaction!.CreateSavepointAsync(savepointName);
        }

        try
        {
            var result = await operation();
            if (transaction is not null)
            {
                await transaction.CommitAsync();
            }

            return result;
        }
        catch
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync();
            }
            else if (existingTransaction is not null)
            {
                await existingTransaction.RollbackToSavepointAsync(savepointName);
            }

            throw;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private async Task<bool> IsLegacyProcedureAvailableAsync(
        string procedureName,
        params string[] expectedParameters
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT [parameterObject].[name]
                FROM [sys].[procedures] AS [procedureObject]
                INNER JOIN [sys].[schemas] AS [schemaObject]
                    ON [schemaObject].[schema_id] = [procedureObject].[schema_id]
                LEFT JOIN [sys].[parameters] AS [parameterObject]
                    ON [parameterObject].[object_id] = [procedureObject].[object_id]
                WHERE [schemaObject].[name] = N'dbo'
                  AND [procedureObject].[name] = @procedureName
                ORDER BY [parameterObject].[parameter_id]
                """;
            AddParameter(command, "@procedureName", DbType.String, procedureName);

            var actualParameters = new List<string>();
            var procedureFound = false;
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                procedureFound = true;
                if (!reader.IsDBNull(0))
                    actualParameters.Add(reader.GetString(0));
            }

            if (!procedureFound)
                return false;

            if (!actualParameters.SequenceEqual(expectedParameters, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The deployed legacy procedure {procedureName} does not match the archived parameter contract. No direct-DML fallback was run."
                );
            }

            return true;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Procedure names are fixed archived legacy contracts, never request input."
    )]
    private async Task ExecuteLegacyProcedureAsync(
        string procedureName,
        params ProcedureParameter[] parameters
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedureName;
            command.CommandTimeout = 0;
            foreach (var parameter in parameters)
                AddParameter(command, parameter.Name, parameter.DbType, parameter.Value);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static string EscapeXml(string value) => SecurityElement.Escape(value) ?? string.Empty;

    private static string FormatXmlDate(DateTime value) =>
        value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

    private async Task<string?> ResolveTripDriverTableAsync()
    {
        var requiredColumns = new[]
        {
            "trip_driver_name",
            "trip_driver_id",
            "trip_authority_code",
            "trip_driver_primary",
        };

        foreach (var tableName in TripDriverTableNames)
        {
            var columns = await GetTableColumnsAsync(tableName);
            if (requiredColumns.All(columns.Contains))
            {
                return tableName;
            }
        }

        return null;
    }

    private async Task InsertTripDriverAsync(
        string tableName,
        TripAuthorityDriverInput driver,
        int tripAuthorityCode,
        int currentUserId
    )
    {
        var columns = await GetTableColumnsAsync(tableName);
        var values = new List<WriteValue>();
        AddValue(
            values,
            columns,
            "trip_authority_code",
            "@tripAuthorityCode",
            DbType.Int32,
            tripAuthorityCode
        );
        AddValue(
            values,
            columns,
            "trip_driver_name",
            "@tripDriverName",
            DbType.String,
            driver.Name
        );
        AddValue(
            values,
            columns,
            "trip_driver_id",
            "@tripDriverId",
            DbType.String,
            driver.IdentityNumber
        );
        AddValue(
            values,
            columns,
            "trip_driver_primary",
            "@tripDriverPrimary",
            DbType.Boolean,
            driver.IsPrimary
        );
        AddValue(values, columns, "site_code", "@siteCode", DbType.Int32, driver.SiteCode);
        AddValue(
            values,
            columns,
            "driver_licence_type_id",
            "@licenceTypeCode",
            DbType.Int32,
            driver.LicenceTypeCode
        );
        AddValue(
            values,
            columns,
            "driver_passportnumber",
            "@passportNumber",
            DbType.String,
            driver.PassportNumber
        );
        AddValue(
            values,
            columns,
            "driver_persalnumber",
            "@persalNumber",
            DbType.String,
            driver.PersalNumber
        );
        AddValue(
            values,
            columns,
            "driver_contractnumber",
            "@contractNumber",
            DbType.String,
            driver.ContractNumber
        );
        AddValue(
            values,
            columns,
            "driver_licence_number",
            "@licenceNumber",
            DbType.String,
            driver.LicenceNumber
        );
        AddValue(
            values,
            columns,
            "driver_licence_issuedate",
            "@licenceIssueDate",
            DbType.DateTime,
            driver.LicenceIssueDate
        );
        AddValue(
            values,
            columns,
            "driver_licence_lastVerifiedDate",
            "@licenceLastVerifiedDate",
            DbType.DateTime,
            driver.LicenceLastVerifiedDate
        );
        AddValue(values, columns, "driver_hasPDP", "@hasPdp", DbType.Boolean, driver.HasPdp);
        AddValue(
            values,
            columns,
            "driver_PDP_ExpiryDate",
            "@pdpExpiryDate",
            DbType.DateTime,
            driver.PdpExpiryDate
        );
        AddValue(
            values,
            columns,
            "driver_licence_ExpiryDate",
            "@licenceExpiryDate",
            DbType.DateTime,
            driver.LicenceExpiryDate
        );
        AddValue(
            values,
            columns,
            "driver_active",
            "@driverActive",
            DbType.Boolean,
            driver.IsActive
        );
        AddValue(
            values,
            columns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddValue(
            values,
            columns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        await ExecuteInsertAsync(tableName, values);
    }

    private async Task InsertTripPassengerAsync(
        TripAuthorityPassengerInput passenger,
        int tripAuthorityCode,
        IReadOnlySet<string> columns,
        int currentUserId
    )
    {
        var values = new List<WriteValue>();
        AddValue(
            values,
            columns,
            "trip_authority_code",
            "@tripAuthorityCode",
            DbType.Int32,
            tripAuthorityCode
        );
        AddValue(
            values,
            columns,
            "trip_passenger_name",
            "@tripPassengerName",
            DbType.String,
            passenger.Name
        );
        AddValue(
            values,
            columns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddValue(
            values,
            columns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        await ExecuteInsertAsync(TripPassengerTableName, values);
    }

    private async Task InsertRouteDetailAsync(
        TripAuthorityRouteInput route,
        int tripAuthorityCode,
        IReadOnlySet<string> columns,
        int currentUserId
    )
    {
        var values = new List<WriteValue>();
        AddValue(
            values,
            columns,
            "trip_authority_code",
            "@tripAuthorityCode",
            DbType.Int32,
            tripAuthorityCode
        );
        AddValue(values, columns, "start_date", "@startDate", DbType.DateTime2, route.StartDate);
        AddValue(values, columns, "end_date", "@endDate", DbType.DateTime2, route.EndDate);
        AddValue(
            values,
            columns,
            "start_odo_meter",
            "@startOdometer",
            DbType.Int32,
            route.StartOdometer
        );
        AddValue(values, columns, "end_odo_meter", "@endOdometer", DbType.Int32, null);
        AddFirstAvailableValue(
            values,
            columns,
            "@responsibilityCode",
            DbType.String,
            route.ResponsibilityCode,
            "bas_responsibility_code",
            "responsibility_code"
        );
        AddFirstAvailableValue(
            values,
            columns,
            "@objectiveCode",
            DbType.String,
            route.ObjectiveCode,
            "bas_object_code",
            "bas_objective_code",
            "objective_code"
        );
        AddFirstAvailableValue(
            values,
            columns,
            "@startLocation",
            DbType.String,
            route.StartLocation,
            "start_route_location_name",
            "start_location_name"
        );
        AddFirstAvailableValue(
            values,
            columns,
            "@endLocation",
            DbType.String,
            route.EndLocation,
            "end_route_location_name",
            "end_location_name"
        );
        AddValue(
            values,
            columns,
            "estimated_distance",
            "@estimatedDistance",
            DbType.Int32,
            route.EstimatedDistance
        );
        AddValue(values, columns, "distance", "@distance", DbType.Int32, 0);
        AddFirstAvailableValue(
            values,
            columns,
            "@projectNumber",
            DbType.String,
            route.ProjectNumber,
            "bas_project_number",
            "project_number"
        );
        AddFirstAvailableValue(
            values,
            columns,
            "@fundCode",
            DbType.String,
            route.FundCode,
            "bas_fund_code",
            "fund_code"
        );
        AddValue(
            values,
            columns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddValue(
            values,
            columns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        await ExecuteInsertAsync(RouteDetailTableName, values);
    }

    private async Task ExecuteInsertAsync(string tableName, IReadOnlyList<WriteValue> values)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                $"No compatible columns are available for inserting into {tableName}."
            );
        }

        await using var command = _context.Database.GetDbConnection().CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText =
            $"INSERT INTO [dbo].[{tableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        await command.ExecuteNonQueryAsync();
    }

    private static void AddFirstAvailableValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        string parameter,
        DbType dbType,
        object? value,
        params string[] columns
    )
    {
        var column = columns.FirstOrDefault(availableColumns.Contains);
        if (column is not null)
        {
            values.Add(new WriteValue(column, parameter, dbType, value));
        }
    }

    private async Task<IReadOnlyList<TripAuthorityDriver>> GetTripDriversAsync(int tripId)
    {
        var results = new List<TripAuthorityDriver>();
        foreach (var tableName in TripDriverTableNames)
        {
            var columns = await GetTableColumnsAsync(tableName);
            var requiredColumns = new[]
            {
                "trip_driver_code",
                "trip_driver_name",
                "trip_authority_code",
                "trip_driver_primary",
            };
            if (!requiredColumns.All(columns.Contains))
            {
                continue;
            }

            var projection = new List<string>
            {
                GetColumnProjection("d", "trip_driver_code", columns),
                GetColumnProjection("d", "trip_driver_name", columns),
                GetColumnProjection("d", "trip_driver_id", columns),
                GetColumnProjection("d", "trip_driver_primary", columns),
                GetColumnProjection("d", "site_code", columns),
                GetColumnProjection("d", "driver_licence_type_id", columns),
                GetColumnProjection("d", "driver_passportnumber", columns),
                GetColumnProjection("d", "driver_persalnumber", columns),
                GetColumnProjection("d", "driver_contractnumber", columns),
                GetColumnProjection("d", "driver_licence_number", columns),
                GetColumnProjection("d", "driver_licence_issuedate", columns),
                GetColumnProjection("d", "driver_licence_lastVerifiedDate", columns),
                GetColumnProjection("d", "driver_hasPDP", columns),
                GetColumnProjection("d", "driver_PDP_ExpiryDate", columns),
                GetColumnProjection("d", "driver_licence_ExpiryDate", columns),
                GetColumnProjection("d", "driver_active", columns),
            };

            var conditions = new List<string> { "[d].[trip_authority_code] = @tripId" };
            if (columns.Contains("is_deleted"))
            {
                conditions.Add("ISNULL([d].[is_deleted], 0) = 0");
            }

            await ReadRowsAsync(
                $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{tableName}] AS [d]
                WHERE {string.Join(" AND ", conditions)}
                ORDER BY [d].[trip_driver_primary] DESC, [d].[trip_driver_code]
                """,
                command => AddParameter(command, "@tripId", DbType.Int32, tripId),
                reader =>
                    results.Add(
                        new TripAuthorityDriver(
                            ReadInt32(reader, "trip_driver_code") ?? 0,
                            ReadString(reader, "trip_driver_name"),
                            ReadString(reader, "trip_driver_id"),
                            ReadBoolean(reader, "trip_driver_primary"),
                            ReadInt32(reader, "site_code"),
                            ReadInt32(reader, "driver_licence_type_id"),
                            ReadString(reader, "driver_passportnumber"),
                            ReadString(reader, "driver_persalnumber"),
                            ReadString(reader, "driver_contractnumber"),
                            ReadString(reader, "driver_licence_number"),
                            ReadDateTime(reader, "driver_licence_issuedate"),
                            ReadDateTime(reader, "driver_licence_lastVerifiedDate"),
                            ReadBoolean(reader, "driver_hasPDP"),
                            ReadDateTime(reader, "driver_PDP_ExpiryDate"),
                            ReadDateTime(reader, "driver_licence_ExpiryDate"),
                            ReadBoolean(reader, "driver_active")
                        )
                    )
            );
        }

        return results
            .GroupBy(driver => driver.TripDriverCode)
            .Select(group => group.First())
            .ToArray();
    }

    private async Task<IReadOnlyList<TripAuthorityPassenger>> GetTripPassengersAsync(int tripId)
    {
        var columns = await GetTableColumnsAsync(TripPassengerTableName);
        var requiredColumns = new[]
        {
            "trip_passenger_code",
            "trip_passenger_name",
            "trip_authority_code",
        };
        if (!requiredColumns.All(columns.Contains))
        {
            // The later EF-created lookup table does not carry the legacy
            // trip_authority_code link. It cannot be safely attributed to a
            // trip, so leave the related records empty rather than guessing.
            return [];
        }

        var results = new List<TripAuthorityPassenger>();
        var conditions = new List<string> { "[p].[trip_authority_code] = @tripId" };
        if (columns.Contains("is_deleted"))
        {
            conditions.Add("ISNULL([p].[is_deleted], 0) = 0");
        }

        await ReadRowsAsync(
            $"""
            SELECT {GetColumnProjection("p", "trip_passenger_code", columns)},
                   {GetColumnProjection("p", "trip_passenger_name", columns)}
            FROM [dbo].[{TripPassengerTableName}] AS [p]
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY [p].[trip_passenger_code]
            """,
            command => AddParameter(command, "@tripId", DbType.Int32, tripId),
            reader =>
                results.Add(
                    new TripAuthorityPassenger(
                        ReadInt32(reader, "trip_passenger_code") ?? 0,
                        ReadString(reader, "trip_passenger_name")
                    )
                )
        );

        return results;
    }

    private async Task<IReadOnlyList<TripAuthorityRoute>> GetRouteDetailsAsync(int tripId)
    {
        var columns = await GetTableColumnsAsync(RouteDetailTableName);
        var requiredColumns = new[]
        {
            "route_code",
            "trip_authority_code",
            "start_date",
            "end_date",
        };
        if (!requiredColumns.All(columns.Contains))
        {
            return [];
        }

        var projection = new List<string>
        {
            GetColumnProjection("r", "route_code", columns),
            GetColumnProjection("r", "start_date", columns),
            GetColumnProjection("r", "end_date", columns),
            GetColumnProjection("r", "start_odo_meter", columns),
            GetColumnProjection("r", "end_odo_meter", columns),
            GetColumnProjection("r", "bas_responsibility_code", columns),
            GetColumnProjection("r", "bas_object_code", columns),
            GetColumnProjection("r", "start_route_location_name", columns),
            GetColumnProjection("r", "end_route_location_name", columns),
            GetColumnProjection("r", "estimated_distance", columns),
            GetColumnProjection("r", "distance", columns),
            GetFirstColumnProjection(
                "r",
                "project_number",
                columns,
                "bas_project_number",
                "project_number"
            ),
            GetFirstColumnProjection("r", "fund_code", columns, "bas_fund_code", "fund_code"),
            GetColumnProjection("r", "modified_by_user_code", columns),
        };

        var conditions = new List<string> { "[r].[trip_authority_code] = @tripId" };
        if (columns.Contains("is_deleted"))
        {
            conditions.Add("ISNULL([r].[is_deleted], 0) = 0");
        }

        var results = new List<TripAuthorityRoute>();
        await ReadRowsAsync(
            $"""
            SELECT {string.Join(", ", projection)}
            FROM [dbo].[{RouteDetailTableName}] AS [r]
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY [r].[start_date], [r].[route_code]
            """,
            command => AddParameter(command, "@tripId", DbType.Int32, tripId),
            reader =>
                results.Add(
                    new TripAuthorityRoute(
                        ReadInt32(reader, "route_code") ?? 0,
                        ReadDateTime(reader, "start_date"),
                        ReadDateTime(reader, "end_date"),
                        ReadInt32(reader, "start_odo_meter"),
                        ReadInt32(reader, "end_odo_meter"),
                        ReadString(reader, "bas_responsibility_code"),
                        ReadString(reader, "bas_object_code"),
                        ReadString(reader, "start_route_location_name"),
                        ReadString(reader, "end_route_location_name"),
                        ReadInt32(reader, "estimated_distance"),
                        ReadInt32(reader, "distance"),
                        ReadString(reader, "project_number"),
                        ReadString(reader, "fund_code"),
                        ReadInt32(reader, "modified_by_user_code")
                    )
                )
        );

        return results;
    }

    private async Task ReadRowsAsync(
        string commandText,
        Action<DbCommand> configure,
        Action<DbDataReader> readRow
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = commandText;
            configure(command);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                readRow(reader);
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<List<Trip>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null,
        bool includeDeleted = false,
        bool contractJoin = false,
        int? take = null,
        string orderBy = "[t].[issue_date] DESC, [t].[trip_authority_code] DESC",
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        var availableColumns = await GetAvailableColumnsAsync();
        var hasContractProjection = contractJoin || await HasContractProjectionAsync();
        if (allowedSiteCodes is { Count: 0 } || (allowedSiteCodes is not null && !hasContractProjection))
        {
            return [];
        }
        var projection = RequiredColumns
            .Concat(OptionalColumns)
            .Select(column => GetColumnProjection("t", column, availableColumns))
            .ToList();

        if (hasContractProjection)
        {
            projection.Add("[c].[vmf_code] AS [contract_vmf_code]");
            projection.Add("[c].[site_code] AS [contract_site_code]");
        }
        else
        {
            projection.Add("CAST(NULL AS int) AS [contract_vmf_code]");
            projection.Add("CAST(NULL AS smallint) AS [contract_site_code]");
        }

        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(predicate))
        {
            conditions.Add(predicate);
        }

        if (!includeDeleted && availableColumns.Contains("is_deleted"))
        {
            conditions.Add("([t].[is_deleted] = 0 OR [t].[is_deleted] IS NULL)");
        }

        var allowedSiteParameters = new List<(string Name, short Value)>();
        if (allowedSiteCodes is not null)
        {
            var placeholders = allowedSiteCodes
                .Where(code => code > 0)
                .Distinct()
                .Select((code, index) =>
                {
                    var name = $"@allowedSite{index}";
                    allowedSiteParameters.Add((name, code));
                    return name;
                })
                .ToArray();
            if (placeholders.Length == 0)
            {
                return [];
            }
            conditions.Add($"[c].[site_code] IN ({string.Join(", ", placeholders)})");
        }

        var whereClause =
            conditions.Count == 0 ? string.Empty : $"WHERE {string.Join(" AND ", conditions)}";
        var contractClause = hasContractProjection
            ? $"LEFT JOIN [dbo].[{ContractTableName}] AS [c] ON [c].[contract_code] = [t].[contract_code]"
            : string.Empty;
        var topClause = take.HasValue ? $"TOP ({take.Value}) " : string.Empty;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                SELECT {topClause}{string.Join(", ", projection)}
                FROM [dbo].[{TableName}] AS [t]
                {contractClause}
                {whereClause}
                ORDER BY {orderBy}
                """;
            foreach (var parameter in allowedSiteParameters)
            {
                AddParameter(command, parameter.Name, DbType.Int16, parameter.Value);
            }
            configure?.Invoke(command);

            var results = new List<Trip>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapTrip(reader, hasContractProjection));
            }

            return results;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<bool> HasContractProjectionAsync()
    {
        var columns = await GetTableColumnsAsync(ContractTableName);
        return RequiredContractColumns.All(columns.Contains);
    }

    private async Task<HashSet<string>> GetAvailableColumnsAsync()
    {
        var columns = await GetTableColumnsAsync(TableName);
        var missingColumns = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missingColumns.Length > 0)
        {
            throw new InvalidOperationException(
                $"The required trip_authorities compatibility columns are not available: {string.Join(", ", missingColumns)}"
            );
        }

        return columns;
    }

    private async Task<HashSet<string>> GetTableColumnsAsync(string tableName)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT [COLUMN_NAME]
                FROM [INFORMATION_SCHEMA].[COLUMNS]
                WHERE [TABLE_SCHEMA] = @schema
                  AND [TABLE_NAME] = @table
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, tableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }

            return columns;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static Trip MapTrip(DbDataReader reader, bool hasContractProjection)
    {
        var trip = new Trip
        {
            trip_authority_code = ReadInt32(reader, "trip_authority_code") ?? 0,
            contract_code = ReadInt32(reader, "contract_code") ?? 0,
            approver_name = ReadString(reader, "approver_name"),
            approver_rank = ReadString(reader, "approver_rank"),
            approver_tel = ReadString(reader, "approver_tel"),
            end_odo_meter = ReadInt32(reader, "end_odo_meter"),
            expiry_date = ReadDateTime(reader, "expiry_date"),
            trip_reason = ReadString(reader, "trip_reason"),
            trip_request_number = ReadString(reader, "trip_request_number"),
            issue_date = ReadDateTime(reader, "issue_date") ?? DateTime.MinValue,
            trip_type_code = ReadInt16(reader, "trip_type_code") ?? 0,
            trip_incident_type_code = ReadInt16(reader, "trip_incident_type_code") ?? 0,
            user_access_code = ReadInt16(reader, "user_access_code"),
            locked_for_transfer = ReadBoolean(reader, "locked_for_transfer"),
            Trip_Is_Monthly = ReadBoolean(reader, "Trip_Is_Monthly"),
            date_created = ReadDateTime(reader, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code = ReadInt32(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted"),
        };

        if (hasContractProjection)
        {
            var vmfCode = ReadInt32(reader, "contract_vmf_code");
            var siteCode = ReadInt16(reader, "contract_site_code");
            if (vmfCode.HasValue || siteCode.HasValue)
            {
                trip.Contract = new Contract
                {
                    contract_code = trip.contract_code,
                    vmf_code = vmfCode ?? 0,
                    site_code = siteCode ?? 0,
                };
            }
        }

        return trip;
    }

    private static List<WriteValue> BuildValues(
        Trip trip,
        IReadOnlySet<string> availableColumns,
        bool includeKey,
        bool includeCreateAudit
    )
    {
        var values = new List<WriteValue>();
        AddValue(
            values,
            availableColumns,
            "trip_authority_code",
            "@tripAuthorityCode",
            DbType.Int32,
            trip.trip_authority_code,
            includeKey
        );
        AddValue(
            values,
            availableColumns,
            "contract_code",
            "@contractCode",
            DbType.Int32,
            trip.contract_code
        );
        AddValue(
            values,
            availableColumns,
            "approver_name",
            "@approverName",
            DbType.String,
            trip.approver_name
        );
        AddValue(
            values,
            availableColumns,
            "approver_rank",
            "@approverRank",
            DbType.String,
            trip.approver_rank
        );
        AddValue(
            values,
            availableColumns,
            "approver_tel",
            "@approverTel",
            DbType.String,
            trip.approver_tel
        );
        AddValue(
            values,
            availableColumns,
            "end_odo_meter",
            "@endOdoMeter",
            DbType.Int32,
            trip.end_odo_meter
        );
        AddValue(
            values,
            availableColumns,
            "expiry_date",
            "@expiryDate",
            DbType.DateTime,
            trip.expiry_date
        );
        AddValue(
            values,
            availableColumns,
            "trip_reason",
            "@tripReason",
            DbType.String,
            trip.trip_reason
        );
        AddValue(
            values,
            availableColumns,
            "trip_request_number",
            "@tripRequestNumber",
            DbType.String,
            trip.trip_request_number
        );
        AddValue(
            values,
            availableColumns,
            "issue_date",
            "@issueDate",
            DbType.DateTime,
            trip.issue_date
        );
        AddValue(
            values,
            availableColumns,
            "trip_type_code",
            "@tripTypeCode",
            DbType.Int16,
            trip.trip_type_code
        );
        AddValue(
            values,
            availableColumns,
            "trip_incident_type_code",
            "@tripIncidentTypeCode",
            DbType.Int16,
            trip.trip_incident_type_code
        );
        AddValue(
            values,
            availableColumns,
            "user_access_code",
            "@userAccessCode",
            DbType.Int16,
            trip.user_access_code
        );
        AddValue(
            values,
            availableColumns,
            "locked_for_transfer",
            "@lockedForTransfer",
            DbType.Boolean,
            trip.locked_for_transfer
        );
        AddValue(
            values,
            availableColumns,
            "Trip_Is_Monthly",
            "@tripIsMonthly",
            DbType.Boolean,
            trip.Trip_Is_Monthly
        );

        if (includeCreateAudit)
        {
            AddValue(
                values,
                availableColumns,
                "date_created",
                "@dateCreated",
                DbType.DateTime2,
                trip.date_created
            );
            AddValue(
                values,
                availableColumns,
                "created_by_user_code",
                "@createdByUser",
                DbType.Int32,
                trip.created_by_user_code
            );
        }

        AddValue(
            values,
            availableColumns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            trip.date_updated
        );
        AddValue(
            values,
            availableColumns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            trip.modified_by_user_code
        );
        AddValue(
            values,
            availableColumns,
            "is_deleted",
            "@isDeleted",
            DbType.Boolean,
            trip.is_deleted
        );
        return values;
    }

    private static string GetColumnProjection(
        string alias,
        string column,
        IReadOnlySet<string> availableColumns
    ) =>
        availableColumns.Contains(column)
            ? $"[{alias}].[{column}] AS [{column}]"
            : $"CAST(NULL AS sql_variant) AS [{column}]";

    private async Task EnsureLegacyTriggersAsync(
        string tableName,
        IReadOnlyCollection<string> requiredTriggers
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT [tr].[name], [tr].[is_disabled]
                FROM [sys].[triggers] AS [tr]
                INNER JOIN [sys].[tables] AS [tb]
                    ON [tb].[object_id] = [tr].[parent_id]
                INNER JOIN [sys].[schemas] AS [sc]
                    ON [sc].[schema_id] = [tb].[schema_id]
                WHERE [sc].[name] = N'dbo'
                  AND [tb].[name] = @tableName
                  AND [tr].[name] IN
                  (
                      N'TRG_INS_UPD_RejectIncompleteTrip',
                      N'TRG_INS_RouteJournalDetailRecord',
                      N'TRG_UPD_RouteJournalDetailRecord',
                      N'TRG_INS_UPD_CheckOverLapping_RouteDetailsKilos',
                      N'TRG_INS_UPD_RouteDetails_CheckOverLapping_ManualLogsheets'
                  );
                """;
            AddParameter(command, "@tableName", DbType.String, tableName);

            var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0) && !reader.IsDBNull(1) && !reader.GetBoolean(1))
                    enabled.Add(reader.GetString(0));
            }

            var missing = requiredTriggers.Where(trigger => !enabled.Contains(trigger)).ToArray();
            if (missing.Length > 0)
            {
                throw new NotSupportedException(
                    $"The legacy trip/route trigger workflow is unavailable ({string.Join(", ", missing)}); no direct-DML fallback was run."
                );
            }
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static string GetFirstColumnProjection(
        string alias,
        string outputColumn,
        IReadOnlySet<string> availableColumns,
        params string[] candidates
    )
    {
        var sourceColumn = candidates.FirstOrDefault(availableColumns.Contains);
        return sourceColumn is null
            ? $"CAST(NULL AS sql_variant) AS [{outputColumn}]"
            : $"[{alias}].[{sourceColumn}] AS [{outputColumn}]";
    }

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        string column,
        string parameter,
        DbType dbType,
        object? value,
        bool include = true
    )
    {
        if (include && availableColumns.Contains(column))
        {
            values.Add(new WriteValue(column, parameter, dbType, value));
        }
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
        {
            AddParameter(command, value.Parameter, value.DbType, value.Value);
        }
    }

    private static void AddParameter(DbCommand command, string name, DbType dbType, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static void AddTripSummaryParameters(
        DbCommand command,
        TripSummaryPageQuery query,
        string search
    )
    {
        AddParameter(command, "@startDate", DbType.DateTime, query.StartDate);
        AddParameter(command, "@endDate", DbType.DateTime, query.EndDate);
        if (query.VmfCode.HasValue)
        {
            AddParameter(command, "@vmfCode", DbType.Int32, query.VmfCode.Value);
        }

        if (search.Length > 0)
        {
            AddParameter(command, "@search", DbType.String, search);
        }
    }

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
    }

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static decimal? ReadDecimal(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static bool ReadBoolean(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private readonly record struct WriteValue(
        string Column,
        string Parameter,
        DbType DbType,
        object? Value
    );

    private readonly record struct ProcedureParameter(string Name, DbType DbType, object? Value);
}
