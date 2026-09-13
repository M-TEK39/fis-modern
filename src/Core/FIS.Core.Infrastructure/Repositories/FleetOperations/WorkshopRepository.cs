using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Persists workshop records against both the original client table and the
/// expanded table. The original schema stores time-only values and contains
/// operational fields that are absent from the expanded EF model.
/// </summary>
public sealed class WorkshopRepository : IWorkshopRepository
{
    private const string TableName = "workshop";
    private const string VehicleTableName = "vehicle_master";
    private const string ModelTableName = "model";
    private const int MaximumSnapshotRows = 500;
    private const int MaximumReportPageSize = 100;

    private static readonly string[] LegacyColumns =
    [
        "ww_code",
        "vmf_code",
        "receive_time",
        "receive_date",
        "complete_time",
        "complete_date",
        "fetch_time",
        "fetch_date",
        "driver_name",
        "contact_name",
        "contact_tel",
        "ww_remarks",
        "ww_reason",
        "accid_mech",
        "ww_site",
        "contact_fax",
        "contact_email",
        "call_refer",
        "ww_km",
        "recover_cost",
        "rem_other_repair",
        "tow_comp",
        "tow_amount",
        "spare_wheel",
        "jack",
        "wheel_spanner",
        "radio",
        "2way",
        "gear_lock",
        "keys",
        "fuel",
        "inter_exter",
        "merch_code",
        "date_to_merch",
        "km_to_merch",
        "date_from_merch",
        "km_from_merch",
        "cost_repair",
        "points",
        "fa_auth_num",
        "test_name",
        "inform_admin",
        "date_from_ww",
        "time_from_ww",
        "fetch_name",
        "job_close",
        "garage",
        "blue_light",
        "monitor_refer",
    ];

    private static readonly string[] OptionalAuditColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly HashSet<string> TimeColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "receive_time",
        "complete_time",
        "fetch_time",
        "time_from_ww",
    };

    private static readonly HashSet<string> DateColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "receive_date",
        "complete_date",
        "fetch_date",
        "date_to_merch",
        "date_from_merch",
        "date_from_ww",
        "date_created",
        "date_updated",
    };

    private readonly FisDbContext _context;

    public WorkshopRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Workshop?> GetByIdAsync(short code) =>
        (
            await QueryAsync(
                "[ww_code] = @wwCode",
                command => AddParameter(command, "@wwCode", DbType.Int16, code)
            )
        ).SingleOrDefault();

    public async Task<IEnumerable<Workshop>> GetAllAsync() => await QueryAsync();

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The page query is composed only from fixed compatibility identifiers and predicates; request values are parameters."
    )]
    public async Task<WorkshopPage> GetPageAsync(WorkshopPageQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var requestedPage = Math.Max(1, query.Page);
        var searchTerm = query.SearchTerm?.Trim().ToLowerInvariant() ?? string.Empty;
        var status = NormalizeStatus(query.Status);
        var searchField = NormalizeSearchField(query.SearchField);
        var workshopColumns = await GetAvailableColumnsAsync();
        var vehicleColumns = await GetTableColumnsAsync(VehicleTableName);
        var hasVehicleJoin = vehicleColumns.ContainsKey("vmf_code");
        var vehicleJoin = BuildVehicleJoin(vehicleColumns, hasVehicleJoin);
        var whereClause = BuildPageWhereClause(
            workshopColumns,
            vehicleColumns,
            hasVehicleJoin,
            searchTerm,
            status,
            searchField
        );

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var countCommand = connection.CreateCommand();
            countCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            countCommand.CommandText = $"""
                SELECT COUNT(1)
                FROM [dbo].[{TableName}] AS [w]
                {vehicleJoin}
                WHERE {whereClause}
                """;
            AddSearchParameter(countCommand, searchTerm);
            var matchingTotal = Convert.ToInt32(
                await countCommand.ExecuteScalarAsync(),
                CultureInfo.InvariantCulture
            );
            var total = Math.Min(matchingTotal, MaximumSnapshotRows);
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = Math.Min(requestedPage, totalPages);
            var skip = checked((long)(page - 1) * pageSize);
            var pageTake = (int)Math.Max(1L, Math.Min((long)pageSize, total - skip));

            var order = workshopColumns.ContainsKey("receive_date")
                ? "[w].[receive_date] DESC, "
                : string.Empty;
            var projection = string.Join(
                ", ",
                [
                    GetProjection(workshopColumns, "ww_code", "w"),
                    GetProjection(workshopColumns, "vmf_code", "w"),
                    GetProjection(workshopColumns, "receive_date", "w"),
                    GetCompleteTimeProjection(workshopColumns, "w"),
                    GetCompleteDateProjection(workshopColumns, "w"),
                    GetVehicleProjection(vehicleColumns, hasVehicleJoin, "fleet_number"),
                    GetVehicleProjection(vehicleColumns, hasVehicleJoin, "registration_number"),
                    GetVehicleProjection(vehicleColumns, hasVehicleJoin, "location_code"),
                    $"CASE WHEN {GetClosedPredicate(workshopColumns, "w")} THEN 'Closed' ELSE 'Open' END AS [status]",
                ]
            );

            await using var dataCommand = connection.CreateCommand();
            dataCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            dataCommand.CommandText = $"""
                SELECT {projection}
                FROM [dbo].[{TableName}] AS [w]
                {vehicleJoin}
                WHERE {whereClause}
                ORDER BY {order}[w].[ww_code] DESC
                OFFSET @skip ROWS FETCH NEXT @pageTake ROWS ONLY
                """;
            AddSearchParameter(dataCommand, searchTerm);
            AddParameter(dataCommand, "@skip", DbType.Int64, skip);
            AddParameter(dataCommand, "@pageTake", DbType.Int32, pageTake);

            var items = new List<WorkshopPageItem>();
            await using var reader = await dataCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(
                    new WorkshopPageItem(
                        ReadInt16(reader, "ww_code") ?? 0,
                        ReadInt32(reader, "vmf_code"),
                        ReadDateTime(reader, "receive_date"),
                        ReadTimeSpan(reader, "complete_time"),
                        ReadDateTime(reader, "complete_date"),
                        ReadString(reader, "fleet_number"),
                        ReadString(reader, "registration_number"),
                        ReadInt16(reader, "location_code"),
                        ReadString(reader, "status") ?? "Open"
                    )
                );
            }

            return new WorkshopPage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The report query uses fixed compatibility table, column, join, and ordering identifiers; every report filter and pagination value is parameterized."
    )]
    public async Task<WorkshopReportPage> GetReportPageAsync(WorkshopReportPageQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateReportKind(query.ReportKind);

        var pageSize = Math.Clamp(query.PageSize, 1, MaximumReportPageSize);
        var requestedPage = Math.Max(1, query.Page);
        var (startDate, endDate) = NormalizeReportDateRange(query.StartDate, query.EndDate);
        var vehicleSearch = NormalizeReportSearch(query.VehicleSearch);
        var garage = NormalizeReportGarage(query.Garage);
        var category = NormalizeReportCategory(query.Category);
        var workshopColumns = await GetAvailableColumnsAsync();
        var vehicleColumns = await GetTableColumnsAsync(VehicleTableName);
        var modelColumns = await GetTableColumnsAsync(ModelTableName);
        var hasVehicleJoin =
            workshopColumns.ContainsKey("vmf_code") && vehicleColumns.ContainsKey("vmf_code");
        var hasModelJoin =
            hasVehicleJoin
            && vehicleColumns.ContainsKey("model_code")
            && modelColumns.ContainsKey("model_code");
        var joins = BuildReportJoins(hasVehicleJoin, hasModelJoin);
        var whereClause = BuildReportWhereClause(
            query,
            workshopColumns,
            vehicleColumns,
            hasVehicleJoin,
            startDate,
            endDate,
            vehicleSearch,
            garage,
            category
        );
        var orderBy = BuildReportOrderBy(
            query.ReportKind,
            vehicleColumns,
            hasVehicleJoin,
            workshopColumns
        );
        var projection = BuildReportProjection(
            workshopColumns,
            vehicleColumns,
            modelColumns,
            hasVehicleJoin,
            hasModelJoin
        );

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var countCommand = connection.CreateCommand();
            countCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            countCommand.CommandText = $"""
                SELECT COUNT(1)
                FROM [dbo].[{TableName}] AS [w]
                {joins}
                WHERE {whereClause}
                """;
            AddReportParameters(
                countCommand,
                query,
                startDate,
                endDate,
                vehicleSearch,
                garage,
                category
            );
            var total = Convert.ToInt32(
                await countCommand.ExecuteScalarAsync(),
                CultureInfo.InvariantCulture
            );
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = total == 0 ? 1 : Math.Min(requestedPage, totalPages);
            var skip = checked((long)(page - 1) * pageSize);

            await using var dataCommand = connection.CreateCommand();
            dataCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            dataCommand.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{TableName}] AS [w]
                {joins}
                WHERE {whereClause}
                ORDER BY {orderBy}
                OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
                """;
            AddReportParameters(
                dataCommand,
                query,
                startDate,
                endDate,
                vehicleSearch,
                garage,
                category
            );
            AddParameter(dataCommand, "@skip", DbType.Int64, skip);
            AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

            var items = new List<WorkshopReportPageItem>();
            await using var reader = await dataCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(MapWorkshopReport(reader, workshopColumns));
            }

            return new WorkshopReportPage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<Workshop>> GetByVehicleAsync(int vmfCode)
    {
        var columns = await GetAvailableColumnsAsync();
        if (!columns.ContainsKey("vmf_code"))
        {
            return Array.Empty<Workshop>();
        }

        return await QueryAsync(
            "[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode),
            columns
        );
    }

    private static void ValidateReportKind(WorkshopReportKind reportKind)
    {
        if (!Enum.IsDefined(reportKind))
        {
            throw new ArgumentOutOfRangeException(nameof(reportKind), reportKind, null);
        }
    }

    private static (DateTime StartDate, DateTime EndDate) NormalizeReportDateRange(
        DateTime? startDate,
        DateTime? endDate
    )
    {
        var start = (startDate ?? DateTime.Today.AddMonths(-1)).Date;
        var end = (endDate ?? DateTime.Today).Date;
        return end < start ? (end, start) : (start, end);
    }

    private static string? NormalizeReportSearch(string? search)
    {
        var value = search?.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value.ToLowerInvariant();
    }

    private static string? NormalizeReportGarage(string? garage) =>
        garage?.Trim().ToLowerInvariant() switch
        {
            "radiojhb" or "jhb" or "j" => "J",
            "radiopta" or "pta" or "p" => "P",
            _ => null,
        };

    private static string? NormalizeReportCategory(string? category) =>
        category?.Trim().ToLowerInvariant() switch
        {
            "radioacc" or "accident" or "accidents" => "accident",
            "radiomec" or "mechanical" or "mechanic" => "mechanical",
            _ => null,
        };

    private static string BuildReportJoins(bool hasVehicleJoin, bool hasModelJoin)
    {
        var joins = new List<string>();
        if (hasVehicleJoin)
        {
            joins.Add(
                $"LEFT JOIN [dbo].[{VehicleTableName}] AS [v] ON [v].[vmf_code] = [w].[vmf_code]"
            );
        }

        if (hasModelJoin)
        {
            joins.Add(
                $"LEFT JOIN [dbo].[{ModelTableName}] AS [m] ON [m].[model_code] = [v].[model_code]"
            );
        }

        return string.Join(Environment.NewLine, joins);
    }

    private static string BuildReportWhereClause(
        WorkshopReportPageQuery query,
        IReadOnlyDictionary<string, ColumnInfo> workshopColumns,
        IReadOnlyDictionary<string, ColumnInfo> vehicleColumns,
        bool hasVehicleJoin,
        DateTime startDate,
        DateTime endDate,
        string? vehicleSearch,
        string? garage,
        string? category
    )
    {
        var conditions = new List<string> { GetActiveFilter(workshopColumns, "w") };

        if (query.ReportKind == WorkshopReportKind.Period)
        {
            conditions.Add(
                workshopColumns.ContainsKey("receive_date")
                    ? "[w].[receive_date] >= @startDate AND [w].[receive_date] < DATEADD(day, 1, @endDate)"
                    : "1 = 0"
            );
        }

        if (garage is not null)
        {
            conditions.Add(
                workshopColumns.ContainsKey("garage")
                    ? "UPPER(LTRIM(RTRIM(COALESCE([w].[garage], '')))) = @garage"
                    : "1 = 0"
            );
        }

        if (category is not null)
        {
            conditions.Add(BuildReportCategoryPredicate(workshopColumns, category));
        }

        if (query.ReportKind == WorkshopReportKind.InShop || query.OpenOnly)
        {
            conditions.Add(BuildReportOpenPredicate(workshopColumns, "w"));
        }

        switch (query.ReportKind)
        {
            case WorkshopReportKind.OneVehicle:
                conditions.Add(
                    BuildReportVehicleSearchPredicate(
                        vehicleColumns,
                        hasVehicleJoin,
                        query.VehicleSearchField,
                        vehicleSearch
                    )
                );
                break;
            case WorkshopReportKind.PrintJobCard:
                conditions.Add(
                    query.WorkshopCode.HasValue
                        ? "[w].[ww_code] = @workshopCode"
                        : BuildReportVehicleSearchPredicate(
                            vehicleColumns,
                            hasVehicleJoin,
                            query.VehicleSearchField,
                            vehicleSearch
                        )
                );
                break;
        }

        return string.Join(" AND ", conditions);
    }

    private static string BuildReportCategoryPredicate(
        IReadOnlyDictionary<string, ColumnInfo> workshopColumns,
        string category
    )
    {
        if (!workshopColumns.ContainsKey("accid_mech"))
        {
            return "1 = 0";
        }

        return category == "accident"
            ? "NULLIF(LTRIM(RTRIM(COALESCE([w].[accid_mech], ''))), '') IS NOT NULL AND UPPER(LTRIM(RTRIM([w].[accid_mech]))) < @category"
            : "NULLIF(LTRIM(RTRIM(COALESCE([w].[accid_mech], ''))), '') IS NOT NULL AND UPPER(LTRIM(RTRIM([w].[accid_mech]))) > @category";
    }

    private static string BuildReportOpenPredicate(
        IReadOnlyDictionary<string, ColumnInfo> workshopColumns,
        string alias
    )
    {
        var jobClose = workshopColumns.ContainsKey("job_close")
            ? $"UPPER(LTRIM(RTRIM(COALESCE([{alias}].[job_close], '')))) = 'N'"
            : "1 = 0";
        var noJobClose = workshopColumns.ContainsKey("job_close")
            ? $"NULLIF(LTRIM(RTRIM(COALESCE([{alias}].[job_close], ''))), '') IS NULL"
            : "1 = 1";
        var incompleteChecks = new List<string>();
        if (workshopColumns.ContainsKey("complete_date"))
        {
            incompleteChecks.Add($"[{alias}].[complete_date] IS NULL");
        }

        if (workshopColumns.ContainsKey("complete_time"))
        {
            incompleteChecks.Add($"[{alias}].[complete_time] IS NULL");
        }

        var incomplete =
            incompleteChecks.Count == 0 ? "1 = 1" : string.Join(" AND ", incompleteChecks);
        return $"({jobClose} OR ({noJobClose} AND {incomplete}))";
    }

    private static string BuildReportVehicleSearchPredicate(
        IReadOnlyDictionary<string, ColumnInfo> vehicleColumns,
        bool hasVehicleJoin,
        WorkshopReportVehicleField searchField,
        string? vehicleSearch
    )
    {
        if (vehicleSearch is null || !hasVehicleJoin)
        {
            return "1 = 0";
        }

        var column = searchField switch
        {
            WorkshopReportVehicleField.FleetNumber => "fleet_number",
            WorkshopReportVehicleField.RegistrationNumber => "registration_number",
            _ => throw new ArgumentOutOfRangeException(nameof(searchField), searchField, null),
        };
        return vehicleColumns.ContainsKey(column)
            ? $"CHARINDEX(@vehicleSearch, LOWER(LTRIM(RTRIM(COALESCE(CONVERT(nvarchar(max), [v].[{column}]), ''))))) > 0"
            : "1 = 0";
    }

    private static string BuildReportOrderBy(
        WorkshopReportKind reportKind,
        IReadOnlyDictionary<string, ColumnInfo> vehicleColumns,
        bool hasVehicleJoin,
        IReadOnlyDictionary<string, ColumnInfo> workshopColumns
    )
    {
        var receiveDate = workshopColumns.ContainsKey("receive_date")
            ? "[w].[receive_date]"
            : "[w].[ww_code]";
        var receiveTime = GetReportTimeOrderExpression(workshopColumns);

        return reportKind switch
        {
            WorkshopReportKind.OneVehicle =>
                $"{GetFleetOrderExpression(vehicleColumns, hasVehicleJoin)} ASC, {receiveDate} DESC, [w].[ww_code] DESC",
            WorkshopReportKind.InShop => $"{receiveDate} ASC, {receiveTime} ASC, [w].[ww_code] ASC",
            _ => $"{receiveDate} DESC, [w].[ww_code] DESC",
        };
    }

    private static string GetFleetOrderExpression(
        IReadOnlyDictionary<string, ColumnInfo> vehicleColumns,
        bool hasVehicleJoin
    ) =>
        hasVehicleJoin && vehicleColumns.ContainsKey("fleet_number")
            ? "[v].[fleet_number]"
            : "CAST(NULL AS varchar(1))";

    private static string GetReportTimeOrderExpression(
        IReadOnlyDictionary<string, ColumnInfo> workshopColumns
    )
    {
        if (!workshopColumns.TryGetValue("receive_time", out var info))
        {
            return "CAST(NULL AS time)";
        }

        return string.Equals(info.DataType, "time", StringComparison.OrdinalIgnoreCase)
            ? "[w].[receive_time]"
            : "CAST([w].[receive_time] AS time)";
    }

    private static string BuildReportProjection(
        IReadOnlyDictionary<string, ColumnInfo> workshopColumns,
        IReadOnlyDictionary<string, ColumnInfo> vehicleColumns,
        IReadOnlyDictionary<string, ColumnInfo> modelColumns,
        bool hasVehicleJoin,
        bool hasModelJoin
    ) =>
        string.Join(
            ", ",
            [
                GetProjection(workshopColumns, "ww_code", "w"),
                GetProjection(workshopColumns, "vmf_code", "w"),
                GetReportVehicleProjection(
                    vehicleColumns,
                    hasVehicleJoin,
                    "fleet_number",
                    "varchar(255)"
                ),
                GetReportVehicleProjection(
                    vehicleColumns,
                    hasVehicleJoin,
                    "registration_number",
                    "varchar(255)"
                ),
                GetReportModelProjection(modelColumns, hasModelJoin),
                GetReportVehicleProjection(vehicleColumns, hasVehicleJoin, "current_odo", "int"),
                GetProjection(workshopColumns, "receive_date", "w"),
                GetTimeProjection(workshopColumns, "receive_time", "w"),
                GetCompleteDateProjection(workshopColumns, "w"),
                GetCompleteTimeProjection(workshopColumns, "w"),
                GetProjection(workshopColumns, "contact_name", "w"),
                GetProjection(workshopColumns, "contact_tel", "w"),
                GetProjection(workshopColumns, "contact_fax", "w"),
                GetProjection(workshopColumns, "contact_email", "w"),
                GetProjection(workshopColumns, "accid_mech", "w"),
                GetProjection(workshopColumns, "garage", "w"),
                GetProjection(workshopColumns, "driver_name", "w"),
                GetProjection(workshopColumns, "call_refer", "w"),
                GetProjection(workshopColumns, "ww_km", "w"),
                GetProjection(workshopColumns, "ww_remarks", "w"),
                GetProjection(workshopColumns, "ww_reason", "w"),
                GetProjection(workshopColumns, "merch_code", "w"),
                GetProjection(workshopColumns, "cost_repair", "w"),
                GetProjection(workshopColumns, "date_from_ww", "w"),
                GetProjection(workshopColumns, "job_close", "w"),
            ]
        );

    private static string GetReportVehicleProjection(
        IReadOnlyDictionary<string, ColumnInfo> vehicleColumns,
        bool hasVehicleJoin,
        string column,
        string sqlType
    ) =>
        hasVehicleJoin && vehicleColumns.ContainsKey(column)
            ? $"[v].[{column}] AS [{column}]"
            : $"CAST(NULL AS {sqlType}) AS [{column}]";

    private static string GetReportModelProjection(
        IReadOnlyDictionary<string, ColumnInfo> modelColumns,
        bool hasModelJoin
    ) =>
        hasModelJoin && modelColumns.ContainsKey("model_description")
            ? "[m].[model_description] AS [model_description]"
            : "CAST(NULL AS varchar(255)) AS [model_description]";

    private static string GetTimeProjection(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string alias
    )
    {
        if (!columns.TryGetValue(column, out var info))
        {
            return $"CAST(NULL AS time) AS [{column}]";
        }

        var expression = string.Equals(info.DataType, "time", StringComparison.OrdinalIgnoreCase)
            ? $"[{alias}].[{column}]"
            : $"CAST([{alias}].[{column}] AS time)";
        return $"{expression} AS [{column}]";
    }

    private static void AddReportParameters(
        DbCommand command,
        WorkshopReportPageQuery query,
        DateTime startDate,
        DateTime endDate,
        string? vehicleSearch,
        string? garage,
        string? category
    )
    {
        if (query.ReportKind == WorkshopReportKind.Period)
        {
            AddParameter(command, "@startDate", DbType.Date, startDate.Date);
            AddParameter(command, "@endDate", DbType.Date, endDate.Date);
        }

        if (
            vehicleSearch is not null
            && (
                query.ReportKind == WorkshopReportKind.OneVehicle
                || (
                    query.ReportKind == WorkshopReportKind.PrintJobCard
                    && !query.WorkshopCode.HasValue
                )
            )
        )
        {
            AddParameter(command, "@vehicleSearch", DbType.String, vehicleSearch);
        }

        if (garage is not null)
        {
            AddParameter(command, "@garage", DbType.String, garage);
        }

        if (category is not null)
        {
            AddParameter(command, "@category", DbType.String, category == "accident" ? "M" : "L");
        }

        if (query.ReportKind == WorkshopReportKind.PrintJobCard && query.WorkshopCode.HasValue)
        {
            AddParameter(command, "@workshopCode", DbType.Int16, query.WorkshopCode.Value);
        }
    }

    private static WorkshopReportPageItem MapWorkshopReport(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> workshopColumns
    ) =>
        new(
            ReadInt16(reader, "ww_code") ?? 0,
            ReadInt32(reader, "vmf_code"),
            ReadString(reader, "fleet_number"),
            ReadString(reader, "registration_number"),
            ReadString(reader, "model_description"),
            ReadInt32(reader, "current_odo"),
            ReadDateTime(reader, "receive_date"),
            ReadTimeSpan(reader, "receive_time"),
            ReadDateTime(reader, "complete_date"),
            ReadTimeSpan(reader, "complete_time"),
            ReadString(reader, "contact_name"),
            ReadString(reader, "contact_tel"),
            ReadString(reader, "contact_fax"),
            ReadString(reader, "contact_email"),
            ReadString(reader, "accid_mech"),
            ReadString(reader, "garage"),
            ReadString(reader, "driver_name"),
            ReadDecimalIfAvailable(reader, workshopColumns, "call_refer"),
            ReadDecimalIfAvailable(reader, workshopColumns, "ww_km"),
            ReadString(reader, "ww_remarks"),
            ReadString(reader, "ww_reason"),
            ReadInt32IfAvailable(reader, workshopColumns, "merch_code"),
            ReadDecimalIfAvailable(reader, workshopColumns, "cost_repair"),
            ReadDateTimeIfAvailable(reader, workshopColumns, "date_from_ww"),
            ReadString(reader, "job_close")
        );

    public async Task<Workshop> CreateAsync(Workshop item, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(item);

        var columns = await GetAvailableColumnsAsync();
        var values = BuildCoreValues(item, columns, includeNulls: false);
        AddLegacyValues(values, item, columns, includeNulls: false);

        AddValue(
            values,
            columns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            DateTime.UtcNow,
            includeNull: false
        );
        AddValue(
            values,
            columns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null,
            includeNull: false
        );
        AddValue(
            values,
            columns,
            "is_deleted",
            "@isDeleted",
            DbType.Boolean,
            false,
            includeNull: false
        );

        var code = await ExecuteInsertAsync(values);
        return await GetByIdAsync(code)
            ?? throw new InvalidOperationException(
                $"Workshop with ww_code {code} could not be read after creation."
            );
    }

    public async Task<Workshop> UpdateAsync(Workshop item, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(item);

        var existing =
            await GetByIdAsync(item.ww_code)
            ?? throw new InvalidOperationException(
                $"Workshop with ww_code {item.ww_code} not found"
            );
        var columns = await GetAvailableColumnsAsync();
        var values = BuildCoreValues(item, columns, includeNulls: true);

        // Only non-null legacy values are included. This preserves fields
        // captured by the client-era workflow when a modern form updates the
        // core fields without posting the rest of the legacy record.
        AddLegacyValues(values, item, columns, includeNulls: false);
        AddValue(
            values,
            columns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            DateTime.UtcNow,
            includeNull: false
        );
        AddValue(
            values,
            columns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null,
            includeNull: false
        );

        await ExecuteUpdateAsync(item.ww_code, values, columns);
        return await GetByIdAsync(item.ww_code) ?? existing;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "DeleteAsync selects between fixed legacy SQL statements and uses a parameter for the record identifier."
    )]
    public async Task DeleteAsync(short code, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
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

            if (columns.ContainsKey("is_deleted"))
            {
                var assignments = new List<string> { "[is_deleted] = 1" };
                if (columns.ContainsKey("date_updated"))
                {
                    assignments.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (columns.ContainsKey("modified_by_user_code"))
                {
                    assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                    AddParameter(
                        command,
                        "@modifiedByUserCode",
                        DbType.Int32,
                        currentUserId > 0 ? currentUserId : null
                    );
                }

                command.CommandText = $"""
                    UPDATE [dbo].[{TableName}]
                    SET {string.Join(", ", assignments)}
                    WHERE [ww_code] = @wwCode
                      AND {GetActiveFilter(columns)}
                    """;
            }
            else
            {
                command.CommandText = $"""
                    DELETE FROM [dbo].[{TableName}]
                    WHERE [ww_code] = @wwCode
                    """;
            }

            AddParameter(command, "@wwCode", DbType.Int16, code);
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

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list, predicates, and filters use only fixed legacy columns and parameterized values."
    )]
    private async Task<List<Workshop>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null,
        IReadOnlyDictionary<string, ColumnInfo>? suppliedColumns = null
    )
    {
        var columns = suppliedColumns ?? await GetAvailableColumnsAsync();
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
            var projection = LegacyColumns
                .Concat(OptionalAuditColumns)
                .Select(column => GetProjection(columns, column))
                .ToArray();
            var conditions = new List<string>();
            if (!string.IsNullOrWhiteSpace(predicate))
            {
                conditions.Add(predicate);
            }

            conditions.Add(GetActiveFilter(columns));
            var order = columns.ContainsKey("receive_date")
                ? "[receive_date] DESC, "
                : string.Empty;
            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{TableName}]
                WHERE {string.Join(" AND ", conditions)}
                ORDER BY {order}[ww_code] DESC
                """;
            configure?.Invoke(command);

            var results = new List<Workshop>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapWorkshop(reader, columns));
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

    private static List<WriteValue> BuildCoreValues(
        Workshop item,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        bool includeNulls
    )
    {
        var values = new List<WriteValue>();
        AddValue(
            values,
            columns,
            "vmf_code",
            "@vmfCode",
            DbType.Int32,
            item.vmf_code,
            includeNulls
        );
        AddTimeValue(
            values,
            columns,
            "receive_time",
            "@receiveTime",
            item.receive_time,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "receive_date",
            "@receiveDate",
            DbType.DateTime2,
            item.receive_date,
            includeNulls
        );
        AddTimeValue(
            values,
            columns,
            "complete_time",
            "@completeTime",
            item.complete_time,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "complete_date",
            "@completeDate",
            DbType.DateTime2,
            item.complete_date,
            includeNulls
        );
        return values;
    }

    private static void AddLegacyValues(
        ICollection<WriteValue> values,
        Workshop item,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        bool includeNulls
    )
    {
        AddTimeValue(values, columns, "fetch_time", "@fetchTime", item.fetch_time, includeNulls);
        AddValue(
            values,
            columns,
            "fetch_date",
            "@fetchDate",
            DbType.DateTime2,
            item.fetch_date,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "driver_name",
            "@driverName",
            DbType.String,
            item.driver_name,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "contact_name",
            "@contactName",
            DbType.String,
            item.contact_name,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "contact_tel",
            "@contactTel",
            DbType.String,
            item.contact_tel,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "ww_remarks",
            "@wwRemarks",
            DbType.String,
            item.ww_remarks,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "ww_reason",
            "@wwReason",
            DbType.String,
            item.ww_reason,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "accid_mech",
            "@accidMech",
            DbType.String,
            item.accid_mech,
            includeNulls
        );
        AddValue(values, columns, "ww_site", "@wwSite", DbType.Int16, item.ww_site, includeNulls);
        AddValue(
            values,
            columns,
            "contact_fax",
            "@contactFax",
            DbType.String,
            item.contact_fax,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "contact_email",
            "@contactEmail",
            DbType.String,
            item.contact_email,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "call_refer",
            "@callRefer",
            DbType.Decimal,
            item.call_refer,
            includeNulls
        );
        AddValue(values, columns, "ww_km", "@wwKm", DbType.Decimal, item.ww_km, includeNulls);
        AddValue(
            values,
            columns,
            "recover_cost",
            "@recoverCost",
            DbType.Decimal,
            item.recover_cost,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "rem_other_repair",
            "@remOtherRepair",
            DbType.String,
            item.rem_other_repair,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "tow_comp",
            "@towComp",
            DbType.Int32,
            item.tow_comp,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "tow_amount",
            "@towAmount",
            DbType.Decimal,
            item.tow_amount,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "spare_wheel",
            "@spareWheel",
            DbType.String,
            item.spare_wheel,
            includeNulls
        );
        AddValue(values, columns, "jack", "@jack", DbType.String, item.jack, includeNulls);
        AddValue(
            values,
            columns,
            "wheel_spanner",
            "@wheelSpanner",
            DbType.String,
            item.wheel_spanner,
            includeNulls
        );
        AddValue(values, columns, "radio", "@radio", DbType.String, item.radio, includeNulls);
        AddValue(values, columns, "2way", "@twoWay", DbType.String, item.two_way, includeNulls);
        AddValue(
            values,
            columns,
            "gear_lock",
            "@gearLock",
            DbType.String,
            item.gear_lock,
            includeNulls
        );
        AddValue(values, columns, "keys", "@keys", DbType.String, item.keys, includeNulls);
        AddValue(values, columns, "fuel", "@fuel", DbType.String, item.fuel, includeNulls);
        AddValue(
            values,
            columns,
            "inter_exter",
            "@interExter",
            DbType.String,
            item.inter_exter,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "merch_code",
            "@merchCode",
            DbType.Int32,
            item.merch_code,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "date_to_merch",
            "@dateToMerch",
            DbType.DateTime2,
            item.date_to_merch,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "km_to_merch",
            "@kmToMerch",
            DbType.Decimal,
            item.km_to_merch,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "date_from_merch",
            "@dateFromMerch",
            DbType.DateTime2,
            item.date_from_merch,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "km_from_merch",
            "@kmFromMerch",
            DbType.Decimal,
            item.km_from_merch,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "cost_repair",
            "@costRepair",
            DbType.Decimal,
            item.cost_repair,
            includeNulls
        );
        AddValue(values, columns, "points", "@points", DbType.Decimal, item.points, includeNulls);
        AddValue(
            values,
            columns,
            "fa_auth_num",
            "@faAuthNum",
            DbType.String,
            item.fa_auth_num,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "test_name",
            "@testName",
            DbType.String,
            item.test_name,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "inform_admin",
            "@informAdmin",
            DbType.String,
            item.inform_admin,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "date_from_ww",
            "@dateFromWw",
            DbType.DateTime2,
            item.date_from_ww,
            includeNulls
        );
        AddTimeValue(
            values,
            columns,
            "time_from_ww",
            "@timeFromWw",
            item.time_from_ww,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "fetch_name",
            "@fetchName",
            DbType.String,
            item.fetch_name,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "job_close",
            "@jobClose",
            DbType.String,
            item.job_close,
            includeNulls
        );
        AddValue(values, columns, "garage", "@garage", DbType.String, item.garage, includeNulls);
        AddValue(
            values,
            columns,
            "blue_light",
            "@blueLight",
            DbType.String,
            item.blue_light,
            includeNulls
        );
        AddValue(
            values,
            columns,
            "monitor_refer",
            "@monitorRefer",
            DbType.Int16,
            item.monitor_refer,
            includeNulls
        );
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The INSERT statement is composed from the fixed legacy column allowlist and every value is parameterized."
    )]
    private async Task<short> ExecuteInsertAsync(IReadOnlyList<WriteValue> values)
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
            command.CommandText =
                values.Count == 0
                    ? $"INSERT INTO [dbo].[{TableName}] DEFAULT VALUES OUTPUT INSERTED.[ww_code]"
                    : $"""
                        INSERT INTO [dbo].[{TableName}] ({string.Join(
                            ", ",
                            values.Select(value => $"[{value.Column}]")
                        )})
                        OUTPUT INSERTED.[ww_code]
                        VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                        """;
            AddParameters(command, values);
            return Convert.ToInt16(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The UPDATE statement is composed from the fixed legacy column allowlist and every value is parameterized."
    )]
    private async Task ExecuteUpdateAsync(
        short code,
        IReadOnlyList<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns
    )
    {
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
                WHERE [ww_code] = @wwCode
                  AND {GetActiveFilter(columns)}
                """;
            AddParameters(command, values);
            AddParameter(command, "@wwCode", DbType.Int16, code);
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

    private async Task<Dictionary<string, ColumnInfo>> GetAvailableColumnsAsync()
    {
        var columns = await GetTableColumnsAsync(TableName);
        if (!columns.ContainsKey("ww_code"))
        {
            throw new InvalidOperationException(
                "The required workshop compatibility column ww_code is not available."
            );
        }

        return columns;
    }

    private async Task<Dictionary<string, ColumnInfo>> GetTableColumnsAsync(string tableName)
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
                    SELECT [COLUMN_NAME], [DATA_TYPE]
                    FROM [INFORMATION_SCHEMA].[COLUMNS]
                    WHERE [TABLE_SCHEMA] = @schema
                      AND [TABLE_NAME] = @table
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, tableName);

            var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                columns[name] = new ColumnInfo(name, reader.GetString(1));
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

    private static Workshop MapWorkshop(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns
    )
    {
        var completeTime = ReadTimeSpanIfAvailable(reader, columns, "complete_time");
        var completeDate = ReadDateTimeIfAvailable(reader, columns, "complete_date");
        if (
            !completeDate.HasValue
            && columns.TryGetValue("complete_time", out var completeTimeColumn)
            && !string.Equals(
                completeTimeColumn.DataType,
                "time",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            completeDate = ReadDateTimeIfAvailable(reader, columns, "complete_time")?.Date;
        }

        return new Workshop
        {
            ww_code = ReadInt16(reader, "ww_code") ?? 0,
            vmf_code = ReadInt32IfAvailable(reader, columns, "vmf_code"),
            receive_time = ReadTimeSpanIfAvailable(reader, columns, "receive_time"),
            receive_date = ReadDateTimeIfAvailable(reader, columns, "receive_date"),
            complete_time = completeTime,
            complete_date = completeDate,
            fetch_time = ReadTimeSpanIfAvailable(reader, columns, "fetch_time"),
            fetch_date = ReadDateTimeIfAvailable(reader, columns, "fetch_date"),
            driver_name = ReadStringIfAvailable(reader, columns, "driver_name"),
            contact_name = ReadStringIfAvailable(reader, columns, "contact_name"),
            contact_tel = ReadStringIfAvailable(reader, columns, "contact_tel"),
            ww_remarks = ReadStringIfAvailable(reader, columns, "ww_remarks"),
            ww_reason = ReadStringIfAvailable(reader, columns, "ww_reason"),
            accid_mech = ReadStringIfAvailable(reader, columns, "accid_mech"),
            ww_site = ReadInt16IfAvailable(reader, columns, "ww_site"),
            contact_fax = ReadStringIfAvailable(reader, columns, "contact_fax"),
            contact_email = ReadStringIfAvailable(reader, columns, "contact_email"),
            call_refer = ReadDecimalIfAvailable(reader, columns, "call_refer"),
            ww_km = ReadDecimalIfAvailable(reader, columns, "ww_km"),
            recover_cost = ReadDecimalIfAvailable(reader, columns, "recover_cost"),
            rem_other_repair = ReadStringIfAvailable(reader, columns, "rem_other_repair"),
            tow_comp = ReadInt32IfAvailable(reader, columns, "tow_comp"),
            tow_amount = ReadDecimalIfAvailable(reader, columns, "tow_amount"),
            spare_wheel = ReadStringIfAvailable(reader, columns, "spare_wheel"),
            jack = ReadStringIfAvailable(reader, columns, "jack"),
            wheel_spanner = ReadStringIfAvailable(reader, columns, "wheel_spanner"),
            radio = ReadStringIfAvailable(reader, columns, "radio"),
            two_way = ReadStringIfAvailable(reader, columns, "2way"),
            gear_lock = ReadStringIfAvailable(reader, columns, "gear_lock"),
            keys = ReadStringIfAvailable(reader, columns, "keys"),
            fuel = ReadStringIfAvailable(reader, columns, "fuel"),
            inter_exter = ReadStringIfAvailable(reader, columns, "inter_exter"),
            merch_code = ReadInt32IfAvailable(reader, columns, "merch_code"),
            date_to_merch = ReadDateTimeIfAvailable(reader, columns, "date_to_merch"),
            km_to_merch = ReadDecimalIfAvailable(reader, columns, "km_to_merch"),
            date_from_merch = ReadDateTimeIfAvailable(reader, columns, "date_from_merch"),
            km_from_merch = ReadDecimalIfAvailable(reader, columns, "km_from_merch"),
            cost_repair = ReadDecimalIfAvailable(reader, columns, "cost_repair"),
            points = ReadDecimalIfAvailable(reader, columns, "points"),
            fa_auth_num = ReadStringIfAvailable(reader, columns, "fa_auth_num"),
            test_name = ReadStringIfAvailable(reader, columns, "test_name"),
            inform_admin = ReadStringIfAvailable(reader, columns, "inform_admin"),
            date_from_ww = ReadDateTimeIfAvailable(reader, columns, "date_from_ww"),
            time_from_ww = ReadTimeSpanIfAvailable(reader, columns, "time_from_ww"),
            fetch_name = ReadStringIfAvailable(reader, columns, "fetch_name"),
            job_close = ReadStringIfAvailable(reader, columns, "job_close"),
            garage = ReadStringIfAvailable(reader, columns, "garage"),
            blue_light = ReadStringIfAvailable(reader, columns, "blue_light"),
            monitor_refer = ReadInt16IfAvailable(reader, columns, "monitor_refer"),
            date_created =
                ReadDateTimeIfAvailable(reader, columns, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTimeIfAvailable(reader, columns, "date_updated"),
            created_by_user_code = ReadInt32IfAvailable(reader, columns, "created_by_user_code"),
            modified_by_user_code = ReadInt32IfAvailable(reader, columns, "modified_by_user_code"),
            is_deleted = ReadBooleanIfAvailable(reader, columns, "is_deleted") ?? false,
        };
    }

    private static string NormalizeStatus(string? status)
    {
        var normalized = status?.Trim().ToLowerInvariant() ?? string.Empty;
        return normalized switch
        {
            "" or "all" or "open" or "closed" or "vehicle" => normalized,
            _ => throw new ArgumentException(
                "Workshop status must be all, open, closed, or vehicle.",
                nameof(status)
            ),
        };
    }

    private static string BuildVehicleJoin(
        IReadOnlyDictionary<string, ColumnInfo> vehicleColumns,
        bool hasVehicleJoin
    )
    {
        if (!hasVehicleJoin)
        {
            return string.Empty;
        }

        var activeConditions = new List<string>();
        if (vehicleColumns.ContainsKey("vehicle_status_code"))
        {
            activeConditions.Add("[v].[vehicle_status_code] > 0");
        }

        if (vehicleColumns.ContainsKey("is_deleted"))
        {
            activeConditions.Add("ISNULL([v].[is_deleted], 0) = 0");
        }

        var activeFilter =
            activeConditions.Count == 0
                ? string.Empty
                : $" AND {string.Join(" AND ", activeConditions)}";
        return $"LEFT JOIN [dbo].[{VehicleTableName}] AS [v] ON [v].[vmf_code] = [w].[vmf_code]{activeFilter}";
    }

    private static string BuildPageWhereClause(
        IReadOnlyDictionary<string, ColumnInfo> workshopColumns,
        IReadOnlyDictionary<string, ColumnInfo> vehicleColumns,
        bool hasVehicleJoin,
        string searchTerm,
        string status,
        string searchField
    )
    {
        var conditions = new List<string> { GetActiveFilter(workshopColumns, "w") };
        var closedPredicate = GetClosedPredicate(workshopColumns, "w");

        switch (status)
        {
            case "open":
                conditions.Add($"NOT ({closedPredicate})");
                break;
            case "closed":
                conditions.Add(closedPredicate);
                break;
            case "vehicle":
                conditions.Add(
                    workshopColumns.ContainsKey("vmf_code") ? "[w].[vmf_code] IS NOT NULL" : "1 = 0"
                );
                break;
        }

        if (searchTerm.Length == 0)
        {
            return string.Join(" AND ", conditions);
        }

        if (searchField == "fleet")
        {
            conditions.Add(
                hasVehicleJoin && vehicleColumns.ContainsKey("fleet_number")
                    ? ContainsSearch("[v].[fleet_number]", "nvarchar(max)")
                    : "1 = 0"
            );
            return string.Join(" AND ", conditions);
        }

        if (searchField == "registration")
        {
            conditions.Add(
                hasVehicleJoin && vehicleColumns.ContainsKey("registration_number")
                    ? ContainsSearch("[v].[registration_number]", "nvarchar(max)")
                    : "1 = 0"
            );
            return string.Join(" AND ", conditions);
        }

        var searchPredicates = new List<string> { ContainsSearch("[w].[ww_code]", "nvarchar(50)") };
        if (workshopColumns.ContainsKey("vmf_code"))
        {
            searchPredicates.Add(ContainsSearch("[w].[vmf_code]", "nvarchar(50)"));
        }

        if (hasVehicleJoin && vehicleColumns.ContainsKey("fleet_number"))
        {
            searchPredicates.Add(ContainsSearch("[v].[fleet_number]", "nvarchar(max)"));
        }

        if (hasVehicleJoin && vehicleColumns.ContainsKey("registration_number"))
        {
            searchPredicates.Add(ContainsSearch("[v].[registration_number]", "nvarchar(max)"));
        }

        searchPredicates.Add(
            $"CHARINDEX(@search, LOWER(CASE WHEN {closedPredicate} THEN 'closed' ELSE 'open' END)) > 0"
        );
        conditions.Add($"({string.Join(" OR ", searchPredicates)})");
        return string.Join(" AND ", conditions);
    }

    private static string ContainsSearch(string expression, string sqlType) =>
        $"CHARINDEX(@search, LOWER(LTRIM(RTRIM(COALESCE(CONVERT({sqlType}, {expression}), ''))))) > 0";

    private static void AddSearchParameter(DbCommand command, string searchTerm) =>
        AddParameter(command, "@search", DbType.String, searchTerm);

    private static string NormalizeSearchField(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "fleet" => "fleet",
            "registration" => "registration",
            _ => string.Empty,
        };

    private static string GetClosedPredicate(
        IReadOnlyDictionary<string, ColumnInfo> workshopColumns,
        string alias
    )
    {
        var checks = new List<string>();
        if (workshopColumns.ContainsKey("complete_date"))
        {
            checks.Add($"[{alias}].[complete_date] IS NOT NULL");
        }

        if (workshopColumns.ContainsKey("complete_time"))
        {
            checks.Add($"[{alias}].[complete_time] IS NOT NULL");
        }

        return checks.Count == 0 ? "1 = 0" : $"({string.Join(" OR ", checks)})";
    }

    private static string GetCompleteTimeProjection(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string alias
    )
    {
        if (!columns.TryGetValue("complete_time", out var column))
        {
            return "CAST(NULL AS time) AS [complete_time]";
        }

        var expression = string.Equals(column.DataType, "time", StringComparison.OrdinalIgnoreCase)
            ? $"[{alias}].[complete_time]"
            : $"CAST([{alias}].[complete_time] AS time)";
        return $"{expression} AS [complete_time]";
    }

    private static string GetCompleteDateProjection(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string alias
    )
    {
        var hasDate = columns.ContainsKey("complete_date");
        var hasTime = columns.TryGetValue("complete_time", out var timeColumn);
        var hasDateTime =
            hasTime
            && !string.Equals(timeColumn?.DataType, "time", StringComparison.OrdinalIgnoreCase);

        if (hasDate && hasDateTime)
        {
            return $"COALESCE(CAST([{alias}].[complete_date] AS datetime2), CAST([{alias}].[complete_time] AS datetime2)) AS [complete_date]";
        }

        if (hasDate)
        {
            return $"CAST([{alias}].[complete_date] AS datetime2) AS [complete_date]";
        }

        if (hasDateTime)
        {
            return $"CAST([{alias}].[complete_time] AS datetime2) AS [complete_date]";
        }

        return "CAST(NULL AS datetime2) AS [complete_date]";
    }

    private static string GetVehicleProjection(
        IReadOnlyDictionary<string, ColumnInfo> vehicleColumns,
        bool hasVehicleJoin,
        string column
    ) =>
        hasVehicleJoin && vehicleColumns.ContainsKey(column)
            ? $"[v].[{column}] AS [{column}]"
            : $"CAST(NULL AS varchar(1)) AS [{column}]";

    private static void AddTimeValue(
        ICollection<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        TimeSpan? value,
        bool includeNull
    )
    {
        if (!columns.TryGetValue(column, out var info) || (!value.HasValue && !includeNull))
        {
            return;
        }

        if (string.Equals(info.DataType, "time", StringComparison.OrdinalIgnoreCase))
        {
            values.Add(new WriteValue(column, parameter, DbType.Time, value));
        }
        else
        {
            values.Add(
                new WriteValue(
                    column,
                    parameter,
                    DbType.DateTime2,
                    value.HasValue ? DateTime.Today.Add(value.Value) : null
                )
            );
        }
    }

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value,
        bool includeNull
    )
    {
        if (columns.ContainsKey(column) && (includeNull || value is not null))
        {
            values.Add(new WriteValue(column, parameter, type, value));
        }
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
        {
            AddParameter(command, value.Parameter, value.Type, value.Value);
        }
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string GetActiveFilter(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        GetActiveFilter(columns, null);

    private static string GetActiveFilter(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string? alias
    )
    {
        if (!columns.ContainsKey("is_deleted"))
        {
            return "1 = 1";
        }

        var column = string.IsNullOrWhiteSpace(alias) ? "[is_deleted]" : $"[{alias}].[is_deleted]";
        return $"ISNULL({column}, 0) = 0";
    }

    private static string GetProjection(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) =>
        columns.ContainsKey(column)
            ? $"[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static string GetProjection(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string alias
    ) =>
        columns.ContainsKey(column)
            ? $"[{alias}].[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static string GetSqlType(string column)
    {
        if (TimeColumns.Contains(column))
        {
            return "time";
        }

        if (DateColumns.Contains(column))
        {
            return "datetime2";
        }

        return column switch
        {
            "ww_code" or "ww_site" or "monitor_refer" => "smallint",
            "vmf_code"
            or "tow_comp"
            or "merch_code"
            or "created_by_user_code"
            or "modified_by_user_code" => "int",
            "call_refer"
            or "ww_km"
            or "recover_cost"
            or "tow_amount"
            or "km_to_merch"
            or "km_from_merch"
            or "cost_repair"
            or "points" => "decimal(18, 2)",
            _ => "varchar(1)",
        };
    }

    private static string? ReadStringIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) => columns.ContainsKey(column) ? ReadString(reader, column) : null;

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal))?.TrimEnd();
    }

    private static DateTime? ReadDateTimeIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) => columns.ContainsKey(column) ? ReadDateTime(reader, column) : null;

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
            _ when DateTime.TryParse(Convert.ToString(value), out var parsed) => parsed,
            _ => null,
        };
    }

    private static TimeSpan? ReadTimeSpanIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    )
    {
        if (!columns.ContainsKey(column))
        {
            return null;
        }

        return ReadTimeSpan(reader, column);
    }

    private static TimeSpan? ReadTimeSpan(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetValue(ordinal);
        if (value is TimeSpan time)
        {
            return time;
        }

        if (value is DateTime dateTime)
        {
            return dateTime.TimeOfDay;
        }

        if (value is DateTimeOffset dateTimeOffset)
        {
            return dateTimeOffset.TimeOfDay;
        }

        return TimeSpan.TryParse(Convert.ToString(value), out var parsed) ? parsed : null;
    }

    private static int? ReadInt32IfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) => columns.ContainsKey(column) ? ReadInt32(reader, column) : null;

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

    private static short? ReadInt16IfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) => columns.ContainsKey(column) ? ReadInt16(reader, column) : null;

    private static decimal? ReadDecimalIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    )
    {
        if (!columns.ContainsKey(column))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static bool? ReadBooleanIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    )
    {
        if (!columns.ContainsKey(column))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private sealed record ColumnInfo(string Name, string DataType);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
