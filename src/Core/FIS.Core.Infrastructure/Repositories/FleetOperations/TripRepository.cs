using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
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

    public async Task<Trip?> GetByIdAsync(int tripId) =>
        (
            await QueryAsync(
                "[t].[trip_authority_code] = @tripId",
                command => AddParameter(command, "@tripId", DbType.Int32, tripId),
                includeDeleted: false,
                take: 1
            )
        ).SingleOrDefault();

    public async Task<TripAuthorityDetails?> GetDetailsAsync(int tripId)
    {
        var trip = await GetByIdAsync(tripId);
        if (trip is null)
        {
            return null;
        }

        var drivers = await GetTripDriversAsync(tripId);
        var passengers = await GetTripPassengersAsync(tripId);
        var routes = await GetRouteDetailsAsync(tripId);
        return new TripAuthorityDetails(trip, drivers, passengers, routes);
    }

    public async Task<IEnumerable<Trip>> GetAllAsync() =>
        await QueryAsync(orderBy: "[t].[issue_date] DESC, [t].[trip_authority_code] DESC");

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

    public async Task<IEnumerable<TripAuthorityVehicle>> GetTripAuthorityVehiclesAsync()
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

    public async Task<IEnumerable<Trip>> GetTripsByContractAsync(int contractCode) =>
        await QueryAsync(
            "[t].[contract_code] = @contractCode",
            command => AddParameter(command, "@contractCode", DbType.Int32, contractCode)
        );

    public async Task<IEnumerable<Trip>> GetTripsByVehicleAsync(int vmfCode)
    {
        var contractColumns = await GetTableColumnsAsync(ContractTableName);
        if (!RequiredContractColumns.All(contractColumns.Contains))
        {
            return [];
        }

        return await QueryAsync(
            "[c].[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode),
            contractJoin: true
        );
    }

    public async Task<IEnumerable<Trip>> GetTripsByDriverAsync(string driverId)
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
            contractJoin: true
        );
    }

    public async Task<IEnumerable<Trip>> GetTripsByDateRangeAsync(
        DateTime startDate,
        DateTime endDate
    ) =>
        await QueryAsync(
            "[t].[issue_date] >= @startDate AND [t].[issue_date] <= @endDate",
            command =>
            {
                AddParameter(command, "@startDate", DbType.DateTime, startDate);
                AddParameter(command, "@endDate", DbType.DateTime, endDate);
            }
        );

    public async Task<Trip> CreateAsync(Trip trip, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(trip);

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

    public async Task CloseAsync(
        int tripId,
        IReadOnlyList<TripAuthorityRouteUpdate> routes,
        int? endOdometer,
        int currentUserId
    )
    {
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

            var trip =
                await GetByIdAsync(tripId)
                ?? throw new InvalidOperationException($"Trip {tripId} not found");
            if (endOdometer.HasValue)
            {
                trip.end_odo_meter = endOdometer;
            }

            await UpdateAsync(trip, currentUserId);
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
        var availableColumns = await GetAvailableColumnsAsync();
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
            if (availableColumns.Contains("is_deleted"))
            {
                var updates = new List<string> { "[is_deleted] = @isDeleted" };
                AddParameter(command, "@isDeleted", DbType.Boolean, true);
                if (availableColumns.Contains("date_updated"))
                {
                    updates.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (availableColumns.Contains("modified_by_user_code"))
                {
                    updates.Add("[modified_by_user_code] = @modifiedByUserCode");
                    AddParameter(
                        command,
                        "@modifiedByUserCode",
                        DbType.Int32,
                        currentUserId > 0 ? currentUserId : null
                    );
                }

                command.CommandText =
                    $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", updates)} WHERE [trip_authority_code] = @tripId";
            }
            else
            {
                command.CommandText =
                    $"DELETE FROM [dbo].[{TableName}] WHERE [trip_authority_code] = @tripId";
            }

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
        string orderBy = "[t].[issue_date] DESC, [t].[trip_authority_code] DESC"
    )
    {
        var availableColumns = await GetAvailableColumnsAsync();
        var hasContractProjection = contractJoin || await HasContractProjectionAsync();
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
}
