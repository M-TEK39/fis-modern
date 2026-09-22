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
/// Reads and writes taxi requests against both the original client table and the
/// expanded table. Legacy business columns are preserved; modern audit columns are
/// used only when the connected database actually provides them.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility allowlists; all submitted values are parameters."
)]
public sealed class TaxiRepository : ITaxiRepository
{
    private const string TableName = "Taxis";
    private const string DepartmentTableName = "department";
    private const string SiteTableName = "site";
    private const string VehicleTableName = "vehicle_master";
    private const string RequisitionProcedureName = "DEV_INS_Requisition";
    private const string RequisitionNumberTableName = "Req_num";
    private const int MaximumReportPageSize = 100;

    private static readonly string[] RequisitionProcedureParameters =
    [
        "@reqnum",
        "@contractor_id",
        "@vmf_code",
        "@reg_num",
        "@site_code",
        "@department_code",
        "@date_required",
        "@time_required",
        "@official",
        "@rank",
        "@address_1",
        "@address_2",
        "@address_3",
        "@flight",
        "@instructions",
        "@destination_1",
        "@destination_2",
        "@destination_3",
        "@user_access_code",
        "@request_date",
        "@resp_code",
        "@object_code",
        "@fund_code",
        "@fms_code",
        "@project",
        "@trans_man_name",
        "@trans_man_date",
        "@trans_man_rank",
        "@trans_man_tel",
        "@booking_by",
        "@driver",
        "@arrival_time",
        "@driver_available",
        "@persal",
        "@jia_pickup",
        "@official_tel_num",
        "@vehicle_type_code",
    ];

    private static readonly string[] CreateTriggerNames =
    [
        "TRG_INS_Taxi_RejectDuplicateRequisition",
    ];

    private static readonly string[] UpdateTriggerNames =
    [
        "TRG_UPD_TaxiJournalDetailRecord",
        "TRG_UPD_TaxiVIPBillingRecord",
    ];

    private static readonly string[] DeleteTriggerNames =
    [
        "TRG_DEL_Taxis",
    ];

    private static readonly string[] BusinessColumns =
    [
        "request_id",
        "rek_num",
        "contractor_id",
        "vmf_code",
        "department_code",
        "site_code",
        "date_required",
        "time_required",
        "vehicle_type_code",
        "official",
        "rank",
        "address_1",
        "address_2",
        "address_3",
        "flight",
        "instructions",
        "destination_1",
        "destination_2",
        "destination_3",
        "user_access_code",
        "request_date",
        "confirmed",
        "sub_contractor_id",
        "cancelled",
        "resp_code",
        "object_code",
        "fms_code",
        "date_required_2",
        "time_required_2",
        "address_12",
        "address_22",
        "address_32",
        "destination_12",
        "destination_22",
        "destination_32",
        "trans_man_name",
        "trans_man_date",
        "trans_man_rank",
        "trans_man_tel",
        "booking_by",
        "driver",
        "arrival_time",
        "reg_num",
        "project",
        "driver_available",
        "persal",
        "JIA_pickup",
        "official_tel_num",
        "parent_taxi_code",
        "fund_code",
    ];

    private static readonly string[] RequiredColumns =
    [
        "request_id",
        "rek_num",
        "site_code",
        "date_required",
        "time_required",
        "official",
    ];

    private static readonly string[] SearchColumns = ["rek_num", "vmf_code", "reg_num", "official"];

    private static readonly string[] AuditColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly HashSet<string> DateColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "date_required",
        "time_required",
        "request_date",
        "date_required_2",
        "time_required_2",
        "trans_man_date",
        "arrival_time",
        "date_created",
        "date_updated",
    };

    private readonly FisDbContext _context;

    public TaxiRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Taxi?> GetByIdAsync(
        int requestId,
        IReadOnlySet<short>? allowedSiteCodes = null
    ) =>
        (
            await QueryAsync(
                "t.[request_id] = @requestId",
                command => AddParameter(command, "@requestId", DbType.Int32, requestId),
                allowedSiteCodes
            )
        ).SingleOrDefault();

    public async Task<Taxi?> GetLatestByRequisitionAsync(
        string rekNum,
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        var normalized = NormalizeKey(rekNum);
        var overlay = await TryOverlayLatestByRequisitionAsync(normalized, allowedSiteCodes);
        if (overlay.overlay)
        {
            return overlay.taxi;
        }

        return (
            await QueryAsync(
                "UPPER(RTRIM(t.[rek_num])) = @rekNum AND NOT EXISTS ("
                    + "SELECT 1 FROM [dbo].[Taxis] child WHERE child.[parent_taxi_code] = t.[request_id] AND {CHILD_ACTIVE})",
                command => AddParameter(command, "@rekNum", DbType.String, normalized),
                allowedSiteCodes
            )
        )
            .OrderByDescending(taxi => taxi.request_id)
            .FirstOrDefault();
    }

    /// <summary>
    /// Taxis/RPT_detail.aspx reprint membership is DEV_SEL_RequisitionNumberDetails
    /// @RequisitionNumber. Leftover Taxis query hydrates the modern entity.
    /// </summary>
    private async Task<(bool overlay, Taxi? taxi)> TryOverlayLatestByRequisitionAsync(
        string normalizedRekNum,
        IReadOnlySet<short>? allowedSiteCodes
    )
    {
        if (normalizedRekNum.Length == 0)
        {
            return (false, null);
        }

        var rows = await LegacySelectorProcedure.TryReadRowsAsync(
            _context,
            "DEV_SEL_RequisitionNumberDetails",
            [["@RequisitionNumber"]],
            actual =>
                command => AddParameter(command, actual[0], DbType.String, normalizedRekNum),
            "dbo"
        );
        if (rows is null)
        {
            return (false, null);
        }

        if (rows.Count == 0)
        {
            return (true, null);
        }

        var requestIds = new List<int>();
        var seenRequestIds = new HashSet<int>();
        var rekKeys = new List<string>();
        var seenRekKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var requestId = LegacySelectorProcedure.ReadInt32(
                row,
                "request_id",
                "Request_id",
                "RequestID"
            );
            if (requestId is > 0 && seenRequestIds.Add(requestId.Value))
            {
                requestIds.Add(requestId.Value);
            }

            var rekKey = NormalizeKey(
                LegacySelectorProcedure.ReadString(
                    row,
                    "rek_num",
                    "RekNum",
                    "Rek Num",
                    "RequisitionNumber",
                    "Requisition Number"
                )
            );
            if (rekKey.Length > 0 && seenRekKeys.Add(rekKey))
            {
                rekKeys.Add(rekKey);
            }
        }

        if (requestIds.Count == 0 && rekKeys.Count == 0)
        {
            return (false, null);
        }

        List<Taxi> hydrated;
        if (requestIds.Count > 0)
        {
            var placeholders = string.Join(
                ", ",
                requestIds.Select((_, index) => $"@overlayRequestId{index}")
            );
            hydrated = await QueryAsync(
                $"t.[request_id] IN ({placeholders})",
                command =>
                {
                    for (var index = 0; index < requestIds.Count; index++)
                    {
                        AddParameter(
                            command,
                            $"@overlayRequestId{index}",
                            DbType.Int32,
                            requestIds[index]
                        );
                    }
                },
                allowedSiteCodes
            );
            hydrated = LegacySelectorProcedure
                .OrderByKeys(hydrated, requestIds, taxi => taxi.request_id)
                .ToList();
        }
        else
        {
            var placeholders = string.Join(
                ", ",
                rekKeys.Select((_, index) => $"@overlayRek{index}")
            );
            hydrated = await QueryAsync(
                $"UPPER(RTRIM(t.[rek_num])) IN ({placeholders})",
                command =>
                {
                    for (var index = 0; index < rekKeys.Count; index++)
                    {
                        AddParameter(command, $"@overlayRek{index}", DbType.String, rekKeys[index]);
                    }
                },
                allowedSiteCodes
            );
            hydrated = LegacySelectorProcedure
                .OrderByKeys(hydrated, rekKeys, taxi => NormalizeKey(taxi.rek_num))
                .ToList();
        }

        return (true, hydrated.FirstOrDefault());
    }

    public Task<IEnumerable<Taxi>> GetAllAsync(IReadOnlySet<short>? allowedSiteCodes = null) =>
        QueryAsEnumerableAsync(allowedSiteCodes: allowedSiteCodes);

    public async Task<TaxiPage> GetPageAsync(TaxiPageQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var requestedPage = Math.Max(1, query.Page);
        var columns = await GetAvailableColumnsAsync(RequiredColumns);
        var searchTerm = query.Search?.Trim() ?? string.Empty;
        var conditions = new List<string> { GetActiveFilter(columns, "t") };
        var allowedSiteParameters = new List<ReportParameter>();
        AddAllowedSiteScope(
            conditions,
            allowedSiteParameters,
            query.AllowedSiteCodes,
            "t.[site_code]"
        );

        if (searchTerm.Length > 0)
        {
            var searchPredicates = SearchColumns
                .Where(column => columns.ContainsKey(column))
                .Select(column =>
                    $"CHARINDEX(@search, LOWER(LTRIM(RTRIM(COALESCE(CONVERT(nvarchar(max), t.[{column}]), N''))))) > 0"
                )
                .ToArray();

            if (searchPredicates.Length > 0)
                conditions.Add($"({string.Join(" OR ", searchPredicates)})");
        }

        if (query.PendingOnly && !query.JiaPickupOnly)
        {
            var overlayPage = await TryGetPendingOverlayPageAsync(
                columns,
                conditions,
                allowedSiteParameters,
                searchTerm,
                pageSize,
                requestedPage
            );
            if (overlayPage is not null)
                return overlayPage;
        }

        if (query.PendingOnly)
        {
            if (columns.ContainsKey("cancelled"))
            {
                conditions.Add("NULLIF(LTRIM(RTRIM(t.[cancelled])), '') IS NULL");
            }

            if (query.JiaPickupOnly)
            {
                conditions.Add(
                    columns.ContainsKey("JIA_pickup") ? "ISNULL(t.[JIA_pickup], 0) = 1" : "1 = 0"
                );
            }
        }

        var whereClause = string.Join(" AND ", conditions);

        await using var scope = await OpenConnectionAsync();
        await using var countCommand = scope.Connection.CreateCommand();
        countCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        countCommand.CommandText = $"""
            SELECT COUNT(1)
            FROM [dbo].[{TableName}] t
            LEFT JOIN [dbo].[department] d ON d.[department_code] = t.[department_code]
            LEFT JOIN [dbo].[site] s ON s.[Site_code] = t.[site_code]
            WHERE {whereClause}
            """;
        if (searchTerm.Length > 0)
            AddParameter(countCommand, "@search", DbType.String, searchTerm.ToLowerInvariant());
        AddReportParameters(countCommand, allowedSiteParameters);
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var page = Math.Min(requestedPage, totalPages);
        var skip = checked((long)(page - 1) * pageSize);

        await using var dataCommand = scope.Connection.CreateCommand();
        dataCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        dataCommand.CommandText = $"""
            SELECT {string.Join(
                ", ",
                BusinessColumns.Concat(AuditColumns).Select(column =>
                    GetProjection(columns, column, "t")
                )
            )},
                   d.[department_code] AS [__department_code], d.[description] AS [__department_description],
                   s.[Site_code] AS [__site_code], s.[description] AS [__site_description]
            FROM [dbo].[{TableName}] t
            LEFT JOIN [dbo].[department] d ON d.[department_code] = t.[department_code]
            LEFT JOIN [dbo].[site] s ON s.[Site_code] = t.[site_code]
            WHERE {whereClause}
            ORDER BY t.[request_id] DESC
            OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
            """;
        if (searchTerm.Length > 0)
            AddParameter(dataCommand, "@search", DbType.String, searchTerm.ToLowerInvariant());
        AddReportParameters(dataCommand, allowedSiteParameters);
        AddParameter(dataCommand, "@skip", DbType.Int64, skip);
        AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

        var items = new List<Taxi>();
        await using var reader = await dataCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(MapTaxi(reader));

        return new TaxiPage(items, page, pageSize, total);
    }

    /// <summary>
    /// DEV_RPT_Today_Reqs decides pending membership and order; the leftover
    /// Taxis query hydrates the modern entity. RPT_today_reqs.aspx:37-45 sends
    /// an ignored site_code parameter.
    /// </summary>
    private async Task<TaxiPage?> TryGetPendingOverlayPageAsync(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        IReadOnlyList<string> baseConditions,
        IReadOnlyList<ReportParameter> allowedSiteParameters,
        string searchTerm,
        int pageSize,
        int requestedPage
    )
    {
        var rows = await LegacySelectorProcedure.TryReadRowsAsync(
            _context,
            "DEV_RPT_Today_Reqs",
            [[], ["@site_code"]],
            actual =>
                actual.Count == 0
                    ? null
                    : command => AddParameter(command, actual[0], DbType.Int16, DBNull.Value),
            "dbo"
        );
        if (rows is null)
        {
            // Absent or unreadable procedure: labelled leftover fallback (Overlay=false).
            return null;
        }

        if (rows.Count == 0)
            return new TaxiPage([], 1, pageSize, 0) { Overlay = true };

        var keys = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var normalized = NormalizeKey(
                LegacySelectorProcedure.ReadString(row, "rek_num", "RekNum", "Rek Num")
            );
            if (normalized.Length == 0 || !seen.Add(normalized))
                continue;
            keys.Add(normalized);
        }

        if (keys.Count == 0)
        {
            // Rows without a readable key: labelled leftover fallback (Overlay=false).
            return null;
        }

        const int overlayBatchSize = 500;
        var projection = string.Join(
            ", ",
            BusinessColumns
                .Concat(AuditColumns)
                .Select(column => GetProjection(columns, column, "t"))
        );

        var hydrated = new List<Taxi>();
        await using var scope = await OpenConnectionAsync();
        for (var offset = 0; offset < keys.Count; offset += overlayBatchSize)
        {
            var batch = keys.GetRange(offset, Math.Min(overlayBatchSize, keys.Count - offset));
            var hydratedConditions = new List<string>(baseConditions)
            {
                $"UPPER(RTRIM(t.[rek_num])) IN ({string.Join(", ", batch.Select((_, index) => $"@overlayKey{index}"))})",
            };
            await using var command = scope.Connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                SELECT {projection},
                       d.[department_code] AS [__department_code], d.[description] AS [__department_description],
                       s.[Site_code] AS [__site_code], s.[description] AS [__site_description]
                FROM [dbo].[{TableName}] t
                LEFT JOIN [dbo].[department] d ON d.[department_code] = t.[department_code]
                LEFT JOIN [dbo].[site] s ON s.[Site_code] = t.[site_code]
                WHERE {string.Join(" AND ", hydratedConditions)}
                """;
            if (searchTerm.Length > 0)
                AddParameter(command, "@search", DbType.String, searchTerm.ToLowerInvariant());
            AddReportParameters(command, allowedSiteParameters);
            for (var index = 0; index < batch.Count; index++)
                AddParameter(command, $"@overlayKey{index}", DbType.String, batch[index]);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                hydrated.Add(MapTaxi(reader));
        }

        var ordered = LegacySelectorProcedure
            .OrderByKeys(hydrated, keys, taxi => NormalizeKey(taxi.rek_num))
            .ToList();
        var total = ordered.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var page = Math.Min(requestedPage, totalPages);
        var skip = (page - 1) * pageSize;
        var items = ordered.Skip(skip).Take(pageSize).ToList();

        return new TaxiPage(items, page, pageSize, total) { Overlay = true };
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The report query uses fixed compatibility table/column allowlists; report values and pagination values are parameters."
    )]
    public async Task<TaxiReportPage> GetReportPageAsync(TaxiReportPageQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, MaximumReportPageSize);
        var requestedPage = Math.Max(1, query.Page);
        var connectionScope = await OpenConnectionAsync();

        await using (connectionScope)
        {
            var taxiColumns = await GetAvailableColumnsAsync(RequiredColumns);
            var departmentColumns = await GetAvailableColumnsAsync(
                DepartmentTableName,
                ["department_code", "description"]
            );
            var siteColumns = await GetAvailableColumnsAsync(
                SiteTableName,
                ["Site_code", "description"]
            );
            var vehicleColumns =
                query.ReportKind == TaxiReportKind.ListInServicePerDepartment
                    ? await GetAvailableColumnsAsync(
                        VehicleTableName,
                        ["vmf_code", "vehicle_status_code"]
                    )
                    : null;
            var reportSql = BuildReportSql(query, taxiColumns, departmentColumns, vehicleColumns);
            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var lookupJoins = BuildReportLookupJoins(departmentColumns, siteColumns);

            await using var countCommand = connectionScope.Connection.CreateCommand();
            countCommand.Transaction = transaction;
            countCommand.CommandText = $"""
                SELECT COUNT(1)
                FROM [dbo].[{TableName}] AS t
                {lookupJoins}
                WHERE {reportSql.WhereClause}
                """;
            AddReportParameters(countCommand, reportSql.Parameters);
            var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = Math.Min(requestedPage, totalPages);
            var skip = checked((long)(page - 1) * pageSize);
            var projection = string.Join(
                ", ",
                BusinessColumns
                    .Concat(AuditColumns)
                    .Select(column => GetProjection(taxiColumns, column, "t"))
            );

            await using var dataCommand = connectionScope.Connection.CreateCommand();
            dataCommand.Transaction = transaction;
            dataCommand.CommandText = $"""
                SELECT {projection},
                       d.[department_code] AS [__department_code],
                       d.[description] AS [__department_description],
                       s.[Site_code] AS [__site_code],
                       s.[description] AS [__site_description]
                FROM [dbo].[{TableName}] AS t
                {lookupJoins}
                WHERE {reportSql.WhereClause}
                ORDER BY {reportSql.OrderBy}
                OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
                """;
            AddReportParameters(dataCommand, reportSql.Parameters);
            AddParameter(dataCommand, "@skip", DbType.Int64, skip);
            AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

            var items = new List<Taxi>();
            await using var reader = await dataCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                items.Add(MapTaxi(reader));

            return new TaxiReportPage(items, page, pageSize, total);
        }
    }

    public Task<IEnumerable<Taxi>> GetBySiteAsync(
        short siteCode,
        IReadOnlySet<short>? allowedSiteCodes = null
    ) =>
        QueryAsEnumerableAsync(
            "t.[site_code] = @siteCode",
            command => AddParameter(command, "@siteCode", DbType.Int16, siteCode),
            allowedSiteCodes
        );

    public Task<IEnumerable<Taxi>> GetByDepartmentAsync(
        short departmentCode,
        IReadOnlySet<short>? allowedSiteCodes = null
    ) =>
        QueryAsEnumerableAsync(
            "t.[department_code] = @departmentCode",
            command => AddParameter(command, "@departmentCode", DbType.Int16, departmentCode),
            allowedSiteCodes
        );

    public Task<IEnumerable<Taxi>> GetByDateAsync(
        DateTime date,
        IReadOnlySet<short>? allowedSiteCodes = null
    ) =>
        QueryAsEnumerableAsync(
            "t.[date_required] >= @startDate AND t.[date_required] < @endDate",
            command =>
            {
                AddParameter(command, "@startDate", DbType.DateTime, date.Date);
                AddParameter(command, "@endDate", DbType.DateTime, date.Date.AddDays(1));
            },
            allowedSiteCodes
        );

    public async Task<Taxi> CreateAsync(Taxi taxi, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(taxi);
        taxi.user_access_code = ToLegacyShortUserId(currentUserId);
        ValidateTaxi(taxi, requireRequisitionNumber: false);
        await EnsureLegacyTriggersAsync(CreateTriggerNames);

        var legacyRequisition = await ExecuteLegacyRequisitionInsertAsync(taxi, currentUserId);
        if (legacyRequisition is not null)
        {
            taxi.rek_num = legacyRequisition;
            return await GetLatestByRequisitionAsync(legacyRequisition)
                ?? throw new InvalidOperationException(
                    $"Taxi requisition {legacyRequisition} could not be read after legacy creation."
                );
        }

        // A restored database without the original requisition procedure may still
        // support a compatibility direct insert, but only when the caller supplied
        // a concrete requisition number.  Never invent a number in the application;
        // the legacy procedure owns the sequence and its transaction.
        if (string.IsNullOrWhiteSpace(taxi.rek_num))
        {
            throw new LegacyTaxiWorkflowUnavailableException(
                $"The legacy requisition procedure {RequisitionProcedureName} is unavailable; a new taxi request cannot generate a safe requisition number."
            );
        }

        var columns = await GetAvailableColumnsAsync(RequiredColumns);
        var values = BuildValues(taxi, columns, currentUserId, isCreate: true);
        var requestId = await ExecuteInsertAsync(values);
        taxi.request_id = requestId;
        return await GetByIdAsync(requestId)
            ?? throw new InvalidOperationException(
                $"Taxi request {requestId} could not be read after creation."
            );
    }

    public async Task<IReadOnlyList<Taxi>> CreateRecurringAsync(
        Taxi taxi,
        DateTime startDate,
        DateTime endDate,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(taxi);
        taxi.user_access_code = ToLegacyShortUserId(currentUserId);
        ValidateTaxi(taxi, requireRequisitionNumber: false);

        var firstDate = startDate.Date;
        var lastDate = endDate.Date;
        if (lastDate < firstDate)
            throw new ArgumentException("The recurring booking end date must be on or after its start date.");
        if ((lastDate - firstDate).TotalDays > 366)
            throw new ArgumentException("A recurring taxi booking cannot span more than 367 calendar days.");

        await EnsureLegacyTriggersAsync(CreateTriggerNames);
        var columns = await GetAvailableColumnsAsync(RequiredColumns);
        IDbContextTransaction? ownedTransaction = null;
        if (_context.Database.CurrentTransaction is null)
        {
            ownedTransaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable
            );
        }

        var insertedIds = new List<int>();
        var committed = false;
        try
        {
            for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
            {
                // Request_GGVIP_Recurring.aspx deliberately skips Saturday and Sunday.
                if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    continue;

                var requisition = await AllocateLegacyRequisitionNumberAsync();
                var values = BuildRecurringValues(taxi, date, requisition, columns, currentUserId);
                insertedIds.Add(await ExecuteInsertAsync(values));
            }

            var created = new List<Taxi>(insertedIds.Count);
            foreach (var requestId in insertedIds)
            {
                var row = await GetByIdAsync(requestId)
                    ?? throw new InvalidOperationException(
                        $"Recurring taxi request {requestId} could not be read after creation."
                    );
                created.Add(row);
            }

            if (ownedTransaction is not null)
            {
                await ownedTransaction.CommitAsync();
                committed = true;
            }

            return created;
        }
        catch
        {
            if (ownedTransaction is not null && !committed)
                await ownedTransaction.RollbackAsync();
            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
                await ownedTransaction.DisposeAsync();
        }
    }

    public async Task<Taxi> UpdateAsync(Taxi taxi, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(taxi);
        await EnsureLegacyTriggersAsync(UpdateTriggerNames);
        var existing =
            await GetByIdAsync(taxi.request_id)
            ?? throw new InvalidOperationException(
                $"Taxi with request_id {taxi.request_id} not found"
            );

        MergeTaxi(taxi, existing);
        // A later edit is attributed to modified_by_user_code. The original
        // request capturer remains immutable unless a dedicated reassignment
        // workflow exists; never accept a caller-supplied owner.
        taxi.user_access_code = existing.user_access_code;
        ValidateTaxi(taxi, requireRequisitionNumber: true);
        var columns = await GetAvailableColumnsAsync(RequiredColumns);
        var values = BuildValues(taxi, columns, currentUserId, isCreate: false);
        values.RemoveAll(value =>
            value.Column.Equals("request_id", StringComparison.OrdinalIgnoreCase)
        );
        AddValue(
            values,
            columns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddValue(
            values,
            columns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            UserIdOrNull(currentUserId)
        );
        await ExecuteUpdateAsync(taxi.request_id, values, columns);

        return await GetByIdAsync(taxi.request_id)
            ?? throw new InvalidOperationException(
                $"Taxi request {taxi.request_id} could not be read after update."
            );
    }

    public async Task DeleteAsync(int requestId, int currentUserId)
    {
        await EnsureLegacyTriggersAsync(DeleteTriggerNames);
        var columns = await GetAvailableColumnsAsync(RequiredColumns);
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

        if (columns.ContainsKey("is_deleted"))
        {
            var assignments = new List<string> { "[is_deleted] = @isDeleted" };
            AddParameter(command, "@isDeleted", DbType.Boolean, true);
            AddOptionalAssignment(
                assignments,
                command,
                columns,
                "date_updated",
                "@dateUpdated",
                DbType.DateTime2,
                DateTime.UtcNow
            );
            AddOptionalAssignment(
                assignments,
                command,
                columns,
                "modified_by_user_code",
                "@modifiedByUserCode",
                DbType.Int32,
                UserIdOrNull(currentUserId)
            );
            command.CommandText =
                $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", assignments)} WHERE [request_id] = @requestId AND {GetActiveFilter(columns)}";
        }
        else
        {
            command.CommandText =
                $"DELETE FROM [dbo].[{TableName}] WHERE [request_id] = @requestId";
        }

        AddParameter(command, "@requestId", DbType.Int32, requestId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task EnsureLegacyTriggersAsync(IReadOnlyCollection<string> requiredTriggers)
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
                  AND [tb].[name] = N'Taxis'
                  AND [tr].[name] IN
                  (
                      N'TRG_INS_Taxi_RejectDuplicateRequisition',
                      N'TRG_UPD_TaxiJournalDetailRecord',
                      N'TRG_UPD_TaxiVIPBillingRecord',
                      N'TRG_DEL_Taxis'
                  );
                """;

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
                throw new LegacyTaxiWorkflowUnavailableException(
                    $"The legacy taxi-request trigger workflow is unavailable ({string.Join(", ", missing)}); no direct-DML fallback was run."
                );
            }
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<string?> ExecuteLegacyRequisitionInsertAsync(
        Taxi taxi,
        int currentUserId
    )
    {
        var actualParameters = await ResolveProcedureParametersAsync(RequisitionProcedureName);
        if (actualParameters is null)
            return null;

        if (
            !actualParameters.SequenceEqual(
                RequisitionProcedureParameters,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            throw new LegacyTaxiProcedureContractException(
                $"The deployed legacy procedure {RequisitionProcedureName} does not match the archived requisition contract. No direct-DML fallback was run."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = $"dbo.{RequisitionProcedureName}";
        command.CommandTimeout = 0;

        var output = command.CreateParameter();
        output.ParameterName = "@reqnum";
        output.DbType = DbType.String;
        output.Direction = ParameterDirection.Output;
        output.Size = 20;
        command.Parameters.Add(output);

        AddParameter(command, "@contractor_id", DbType.Int16, taxi.contractor_id);
        AddParameter(command, "@vmf_code", DbType.String, taxi.vmf_code, 10);
        AddParameter(command, "@reg_num", DbType.String, taxi.reg_num, 8);
        AddParameter(command, "@site_code", DbType.Int16, taxi.site_code);
        AddParameter(command, "@department_code", DbType.Int32, taxi.department_code);
        AddParameter(command, "@date_required", DbType.DateTime, taxi.date_required);
        AddParameter(command, "@time_required", DbType.DateTime, taxi.time_required);
        AddParameter(command, "@official", DbType.String, taxi.official, 50);
        AddParameter(command, "@rank", DbType.String, taxi.rank, 50);
        AddParameter(command, "@address_1", DbType.String, taxi.address_1, 50);
        AddParameter(command, "@address_2", DbType.String, taxi.address_2, 50);
        AddParameter(command, "@address_3", DbType.String, taxi.address_3, 50);
        AddParameter(command, "@flight", DbType.String, taxi.flight, 50);
        AddParameter(command, "@instructions", DbType.String, taxi.instructions, 250);
        AddParameter(command, "@destination_1", DbType.String, taxi.destination_1, 50);
        AddParameter(command, "@destination_2", DbType.String, taxi.destination_2, 50);
        AddParameter(command, "@destination_3", DbType.String, taxi.destination_3, 50);
        AddParameter(
            command,
            "@user_access_code",
            DbType.Int16,
            taxi.user_access_code ?? ToLegacyShortUserId(currentUserId)
        );
        AddParameter(command, "@request_date", DbType.DateTime, taxi.request_date ?? DateTime.Now);
        AddParameter(command, "@resp_code", DbType.String, taxi.resp_code, 15);
        AddParameter(command, "@object_code", DbType.String, taxi.object_code, 15);
        AddParameter(command, "@fund_code", DbType.String, taxi.fund_code, 15);
        AddParameter(command, "@fms_code", DbType.String, taxi.fms_code, 15);
        AddParameter(command, "@project", DbType.String, taxi.project, 15);
        AddParameter(command, "@trans_man_name", DbType.String, taxi.trans_man_name, 25);
        AddParameter(command, "@trans_man_date", DbType.DateTime, taxi.trans_man_date);
        AddParameter(command, "@trans_man_rank", DbType.String, taxi.trans_man_rank, 25);
        AddParameter(command, "@trans_man_tel", DbType.String, taxi.trans_man_tel, 25);
        AddParameter(command, "@booking_by", DbType.String, taxi.booking_by, 3);
        AddParameter(command, "@driver", DbType.String, taxi.driver, 50);
        AddParameter(command, "@arrival_time", DbType.DateTime, taxi.arrival_time);
        AddParameter(command, "@driver_available", DbType.Boolean, taxi.driver_available);
        AddParameter(command, "@persal", DbType.String, taxi.persal, 20);
        AddParameter(command, "@jia_pickup", DbType.Boolean, taxi.JIA_pickup);
        AddParameter(command, "@official_tel_num", DbType.String, taxi.official_tel_num, 25);
        AddParameter(command, "@vehicle_type_code", DbType.Int16, taxi.vehicle_type_code);

        await command.ExecuteNonQueryAsync();
        var requisition = Convert.ToString(output.Value)?.Trim();
        if (string.IsNullOrWhiteSpace(requisition))
        {
            throw new LegacyTaxiProcedureContractException(
                $"The legacy requisition procedure {RequisitionProcedureName} completed without returning a requisition number. No direct-DML fallback was run."
            );
        }

        return requisition;
    }

    private async Task<string> AllocateLegacyRequisitionNumberAsync()
    {
        await using var scope = await OpenConnectionAsync();
        await using var select = scope.Connection.CreateCommand();
        select.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        select.CommandText = $"""
            SELECT TOP (1) [series], [number]
            FROM [dbo].[{RequisitionNumberTableName}] WITH (UPDLOCK, HOLDLOCK)
            """;
        await using var reader = await select.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.IsDBNull(0) || reader.IsDBNull(1))
        {
            throw new LegacyTaxiRecurringWorkflowUnavailableException(
                $"The legacy {RequisitionNumberTableName} sequence is unavailable; no recurring taxi requests were written."
            );
        }

        var series = Convert.ToString(reader.GetValue(0))?.Trim();
        var number = Convert.ToDecimal(reader.GetValue(1));
        if (string.IsNullOrWhiteSpace(series) || number < 0 || number != decimal.Truncate(number))
        {
            throw new LegacyTaxiRecurringWorkflowUnavailableException(
                $"The legacy {RequisitionNumberTableName} sequence contains an invalid value; no recurring taxi requests were written."
            );
        }

        var requisition = series + decimal.ToInt64(number).ToString(CultureInfo.InvariantCulture).PadLeft(7, '0');
        await reader.DisposeAsync();

        await using var update = scope.Connection.CreateCommand();
        update.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        update.CommandText = $"UPDATE [dbo].[{RequisitionNumberTableName}] SET [number] = @nextNumber";
        AddParameter(update, "@nextNumber", DbType.Decimal, number + 1m);
        if (await update.ExecuteNonQueryAsync() != 1)
        {
            throw new LegacyTaxiRecurringWorkflowUnavailableException(
                $"The legacy {RequisitionNumberTableName} sequence could not be advanced; no recurring taxi requests were written."
            );
        }

        return requisition;
    }

    private static List<WriteValue> BuildRecurringValues(
        Taxi taxi,
        DateTime date,
        string requisition,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        int currentUserId
    )
    {
        var values = new List<WriteValue>();
        AddRequiredValue(values, columns, "rek_num", "@rekNum", DbType.String, requisition);
        AddValue(values, columns, "reg_num", "@regNum", DbType.String, taxi.reg_num?.Trim().ToUpperInvariant());
        AddRequiredValue(values, columns, "site_code", "@siteCode", DbType.Int16, taxi.site_code);
        AddValue(values, columns, "department_code", "@departmentCode", DbType.Int16, taxi.department_code);
        AddRequiredValue(values, columns, "date_required", "@dateRequired", DbType.DateTime, date);
        AddRequiredValue(
            values,
            columns,
            "time_required",
            "@timeRequired",
            DbType.DateTime,
            date.Add(taxi.time_required.TimeOfDay)
        );
        AddRequiredValue(values, columns, "official", "@official", DbType.String, taxi.official?.Trim() ?? string.Empty);
        AddValue(values, columns, "rank", "@rank", DbType.String, taxi.rank);
        AddValue(values, columns, "address_1", "@address1", DbType.String, taxi.address_1);
        AddValue(values, columns, "address_2", "@address2", DbType.String, taxi.address_2);
        AddValue(values, columns, "address_3", "@address3", DbType.String, taxi.address_3);
        AddValue(values, columns, "flight", "@flight", DbType.String, taxi.flight);
        AddValue(values, columns, "instructions", "@instructions", DbType.String, taxi.instructions);
        AddValue(values, columns, "destination_1", "@destination1", DbType.String, taxi.destination_1);
        AddValue(values, columns, "destination_2", "@destination2", DbType.String, taxi.destination_2);
        AddValue(values, columns, "destination_3", "@destination3", DbType.String, taxi.destination_3);
        AddValue(
            values,
            columns,
            "user_access_code",
            "@userAccessCode",
            DbType.Int16,
            taxi.user_access_code ?? ToLegacyShortUserId(currentUserId)
        );
        AddValue(values, columns, "request_date", "@requestDate", DbType.DateTime, DateTime.Now);
        AddValue(values, columns, "resp_code", "@respCode", DbType.String, taxi.resp_code);
        AddValue(values, columns, "object_code", "@objectCode", DbType.String, taxi.object_code);
        AddValue(values, columns, "fund_code", "@fundCode", DbType.String, taxi.fund_code);
        AddValue(values, columns, "fms_code", "@fmsCode", DbType.String, taxi.fms_code);
        AddValue(values, columns, "project", "@project", DbType.String, taxi.project);
        AddValue(values, columns, "trans_man_name", "@transManName", DbType.String, taxi.trans_man_name);
        AddValue(values, columns, "trans_man_date", "@transManDate", DbType.DateTime, taxi.trans_man_date);
        AddValue(values, columns, "trans_man_rank", "@transManRank", DbType.String, taxi.trans_man_rank);
        AddValue(values, columns, "trans_man_tel", "@transManTel", DbType.String, taxi.trans_man_tel);
        AddValue(values, columns, "booking_by", "@bookingBy", DbType.String, taxi.booking_by);
        AddValue(values, columns, "driver", "@driver", DbType.String, taxi.driver);
        AddValue(values, columns, "arrival_time", "@arrivalTime", DbType.DateTime, taxi.arrival_time);
        AddValue(values, columns, "driver_available", "@driverAvailable", DbType.Boolean, taxi.driver_available ?? false);
        AddValue(values, columns, "persal", "@persal", DbType.String, taxi.persal);
        AddValue(values, columns, "JIA_pickup", "@jiaPickup", DbType.Boolean, taxi.JIA_pickup ?? false);
        AddValue(values, columns, "official_tel_num", "@officialTelNum", DbType.String, taxi.official_tel_num);
        return values;
    }

    private async Task<IReadOnlyList<string>?> ResolveProcedureParametersAsync(string procedureName)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT [p].[name]
            FROM [sys].[procedures] AS [sp]
            INNER JOIN [sys].[schemas] AS [s]
                ON [s].[schema_id] = [sp].[schema_id]
            LEFT JOIN [sys].[parameters] AS [p]
                ON [p].[object_id] = [sp].[object_id]
               AND [p].[parameter_id] > 0
            WHERE [s].[name] = N'dbo' AND [sp].[name] = @procedureName
            ORDER BY [p].[parameter_id]
            """;
        AddParameter(command, "@procedureName", DbType.String, procedureName);

        var found = false;
        var parameters = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            found = true;
            if (!reader.IsDBNull(0))
                parameters.Add(reader.GetString(0));
        }

        return found ? parameters : null;
    }

    private async Task<IEnumerable<Taxi>> QueryAsEnumerableAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null,
        IReadOnlySet<short>? allowedSiteCodes = null
    ) => await QueryAsync(predicate, configure, allowedSiteCodes);

    private async Task<List<Taxi>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null,
        IReadOnlySet<short>? allowedSiteCodes = null
    )
    {
        var columns = await GetAvailableColumnsAsync(RequiredColumns);
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

        var effectivePredicate =
            predicate?.Replace(
                "{CHILD_ACTIVE}",
                GetActiveFilter(columns, "child"),
                StringComparison.Ordinal
            ) ?? "1 = 1";
        var conditions = new List<string> { GetActiveFilter(columns, "t") };
        if (!string.IsNullOrWhiteSpace(effectivePredicate))
            conditions.Add($"({effectivePredicate})");
        var allowedSiteParameters = new List<ReportParameter>();
        AddAllowedSiteScope(conditions, allowedSiteParameters, allowedSiteCodes, "t.[site_code]");

        command.CommandText = $"""
            SELECT {string.Join(
                ", ",
                BusinessColumns.Concat(AuditColumns).Select(column =>
                    GetProjection(columns, column, "t")
                )
            )},
                   d.[department_code] AS [__department_code], d.[description] AS [__department_description],
                   s.[Site_code] AS [__site_code], s.[description] AS [__site_description]
            FROM [dbo].[{TableName}] t
            LEFT JOIN [dbo].[department] d ON d.[department_code] = t.[department_code]
            LEFT JOIN [dbo].[site] s ON s.[Site_code] = t.[site_code]
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY t.[request_id] DESC
        """;
        AddReportParameters(command, allowedSiteParameters);
        configure?.Invoke(command);

        var results = new List<Taxi>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapTaxi(reader));
        return results;
    }

    private static TaxiReportSql BuildReportSql(
        TaxiReportPageQuery query,
        IReadOnlyDictionary<string, ColumnInfo> taxiColumns,
        IReadOnlyDictionary<string, ColumnInfo> departmentColumns,
        IReadOnlyDictionary<string, ColumnInfo>? vehicleColumns
    )
    {
        var conditions = new List<string> { GetActiveFilter(taxiColumns, "t") };
        var parameters = new List<ReportParameter>();
        AddAllowedSiteScope(conditions, parameters, query.AllowedSiteCodes, "t.[site_code]");
        var search = query.Search?.Trim() ?? string.Empty;

        switch (query.ReportKind)
        {
            case TaxiReportKind.PreviousFinYearVipTaxi:
                var reportDate = (query.AsOfDate ?? DateTime.Today).Date;
                var currentFinancialYearStart = GetFinancialYearStart(reportDate);
                var nextFinancialYearStart = currentFinancialYearStart.AddYears(1);
                var capturedDate = GetCapturedDateExpression(taxiColumns);
                conditions.Add($"{capturedDate} >= @currentFinancialYearStart");
                conditions.Add($"{capturedDate} < @nextFinancialYearStart");
                conditions.Add("t.[date_required] < @currentFinancialYearStart");
                parameters.Add(
                    new ReportParameter(
                        "@currentFinancialYearStart",
                        DbType.DateTime,
                        currentFinancialYearStart
                    )
                );
                parameters.Add(
                    new ReportParameter(
                        "@nextFinancialYearStart",
                        DbType.DateTime,
                        nextFinancialYearStart
                    )
                );
                return new TaxiReportSql(
                    string.Join(" AND ", conditions),
                    $"{capturedDate} DESC, t.[request_id] DESC",
                    parameters
                );

            case TaxiReportKind.ListPerDepartment:
                AddSearchCondition(
                    conditions,
                    parameters,
                    search,
                    [
                        taxiColumns.ContainsKey("rek_num") ? "t.[rek_num]" : null,
                        departmentColumns.ContainsKey("description") ? "d.[description]" : null,
                        taxiColumns.ContainsKey("vmf_code") ? "t.[vmf_code]" : null,
                        taxiColumns.ContainsKey("request_id") ? "t.[request_id]" : null,
                    ]
                );
                return new TaxiReportSql(
                    string.Join(" AND ", conditions),
                    "d.[description] ASC, t.[rek_num] ASC, t.[request_id] DESC",
                    parameters
                );

            case TaxiReportKind.ListInServicePerDepartment:
                if (vehicleColumns is null)
                {
                    throw new InvalidOperationException(
                        "Vehicle compatibility columns are required for the in-service taxi report."
                    );
                }

                conditions.Add(
                    $"EXISTS (SELECT 1 FROM [dbo].[{VehicleTableName}] AS v "
                        + "WHERE LTRIM(RTRIM(CONVERT(nvarchar(50), v.[vmf_code]))) "
                        + "= LTRIM(RTRIM(COALESCE(CONVERT(nvarchar(50), t.[vmf_code]), N''))) "
                        + $"AND {GetActiveFilter(vehicleColumns, "v")} "
                        + "AND v.[vehicle_status_code] = @inServiceStatus)"
                );
                parameters.Add(new ReportParameter("@inServiceStatus", DbType.Int16, 1));
                AddSearchCondition(
                    conditions,
                    parameters,
                    search,
                    [
                        taxiColumns.ContainsKey("rek_num") ? "t.[rek_num]" : null,
                        departmentColumns.ContainsKey("description") ? "d.[description]" : null,
                        taxiColumns.ContainsKey("vmf_code") ? "t.[vmf_code]" : null,
                        taxiColumns.ContainsKey("request_id") ? "t.[request_id]" : null,
                    ]
                );
                return new TaxiReportSql(
                    string.Join(" AND ", conditions),
                    "d.[description] ASC, t.[rek_num] ASC, t.[request_id] DESC",
                    parameters
                );

            case TaxiReportKind.Financial:
                AddSearchCondition(
                    conditions,
                    parameters,
                    search,
                    [
                        taxiColumns.ContainsKey("request_id") ? "t.[request_id]" : null,
                        taxiColumns.ContainsKey("rek_num") ? "t.[rek_num]" : null,
                        taxiColumns.ContainsKey("official") ? "t.[official]" : null,
                        taxiColumns.ContainsKey("vmf_code") ? "t.[vmf_code]" : null,
                        taxiColumns.ContainsKey("address_1") ? "t.[address_1]" : null,
                    ]
                );
                return new TaxiReportSql(
                    string.Join(" AND ", conditions),
                    "t.[date_required] DESC, t.[request_id] DESC",
                    parameters
                );

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(query.ReportKind),
                    query.ReportKind,
                    "Unsupported taxi report type."
                );
        }
    }

    private static string BuildReportLookupJoins(
        IReadOnlyDictionary<string, ColumnInfo> departmentColumns,
        IReadOnlyDictionary<string, ColumnInfo> siteColumns
    ) =>
        $"""
            LEFT JOIN [dbo].[{DepartmentTableName}] AS d
                ON d.[department_code] = t.[department_code]
               AND {GetActiveFilter(departmentColumns, "d")}
            LEFT JOIN [dbo].[{SiteTableName}] AS s
                ON s.[Site_code] = t.[site_code]
               AND {GetActiveFilter(siteColumns, "s")}
            """;

    private static void AddSearchCondition(
        ICollection<string> conditions,
        ICollection<ReportParameter> parameters,
        string search,
        IEnumerable<string?> expressions
    )
    {
        if (search.Length == 0)
            return;

        var predicates = expressions
            .Where(expression => !string.IsNullOrWhiteSpace(expression))
            .Select(expression =>
                expression!.EndsWith("]", StringComparison.Ordinal)
                && expression.EndsWith("[request_id]", StringComparison.Ordinal)
                    ? $"CHARINDEX(@search, CONVERT(nvarchar(20), {expression})) > 0"
                    : $"CHARINDEX(@search, LOWER(LTRIM(RTRIM(COALESCE(CONVERT(nvarchar(max), {expression}), N''))))) > 0"
            )
            .ToArray();

        if (predicates.Length == 0)
        {
            conditions.Add("1 = 0");
            return;
        }

        conditions.Add($"({string.Join(" OR ", predicates)})");
        parameters.Add(new ReportParameter("@search", DbType.String, search.ToLowerInvariant()));
    }

    private static string GetCapturedDateExpression(
        IReadOnlyDictionary<string, ColumnInfo> taxiColumns
    )
    {
        var expressions = new List<string>();
        if (taxiColumns.ContainsKey("date_created"))
            expressions.Add("t.[date_created]");
        if (taxiColumns.ContainsKey("request_date"))
            expressions.Add("t.[request_date]");
        expressions.Add("t.[date_required]");
        return expressions.Count == 1
            ? expressions[0]
            : $"COALESCE({string.Join(", ", expressions)})";
    }

    private static DateTime GetFinancialYearStart(DateTime date)
    {
        var year = date.Month >= 4 ? date.Year : date.Year - 1;
        return new DateTime(year, 4, 1);
    }

    private Task<Dictionary<string, ColumnInfo>> GetAvailableColumnsAsync(
        IReadOnlyCollection<string> requiredColumns
    ) => GetAvailableColumnsAsync(TableName, requiredColumns);

    private async Task<Dictionary<string, ColumnInfo>> GetAvailableColumnsAsync(
        string tableName,
        IReadOnlyCollection<string> requiredColumns
    )
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT [COLUMN_NAME], [DATA_TYPE]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table
            """;
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, tableName);

        var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns[reader.GetString(0)] = new ColumnInfo(reader.GetString(0), reader.GetString(1));

        foreach (var required in requiredColumns.Where(column => !columns.ContainsKey(column)))
            throw new InvalidOperationException(
                $"The required {tableName} compatibility column {required} is not available."
            );
        return columns;
    }

    private async Task<int> ExecuteInsertAsync(IReadOnlyList<WriteValue> values)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            INSERT INTO [dbo].[{TableName}] ({string.Join(
                ", ",
                values.Select(value => $"[{value.Column}]")
            )})
            OUTPUT INSERTED.[request_id]
            VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
            """;
        AddParameters(command, values);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task ExecuteUpdateAsync(
        int requestId,
        IReadOnlyList<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns
    )
    {
        if (values.Count == 0)
            return;
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            UPDATE [dbo].[{TableName}]
            SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))}
            WHERE [request_id] = @requestId AND {GetActiveFilter(columns)}
            """;
        AddParameters(command, values);
        AddParameter(command, "@requestId", DbType.Int32, requestId);
        await command.ExecuteNonQueryAsync();
    }

    private static List<WriteValue> BuildValues(
        Taxi taxi,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        int currentUserId,
        bool isCreate
    )
    {
        var values = new List<WriteValue>();
        AddRequiredValue(
            values,
            columns,
            "rek_num",
            "@rekNum",
            DbType.String,
            taxi.rek_num?.Trim()
        );
        AddValue(
            values,
            columns,
            "contractor_id",
            "@contractorId",
            DbType.Int16,
            taxi.contractor_id
        );
        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.String, taxi.vmf_code);
        AddValue(
            values,
            columns,
            "department_code",
            "@departmentCode",
            DbType.Int16,
            taxi.department_code
        );
        AddRequiredValue(values, columns, "site_code", "@siteCode", DbType.Int16, taxi.site_code);
        AddRequiredValue(
            values,
            columns,
            "date_required",
            "@dateRequired",
            DbType.DateTime,
            taxi.date_required
        );
        AddRequiredValue(
            values,
            columns,
            "time_required",
            "@timeRequired",
            DbType.DateTime,
            taxi.time_required
        );
        AddValue(
            values,
            columns,
            "vehicle_type_code",
            "@vehicleTypeCode",
            DbType.Int16,
            taxi.vehicle_type_code
        );
        AddRequiredValue(
            values,
            columns,
            "official",
            "@official",
            DbType.String,
            taxi.official?.Trim() ?? string.Empty
        );
        AddValue(values, columns, "rank", "@rank", DbType.String, taxi.rank);
        AddValue(values, columns, "address_1", "@address1", DbType.String, taxi.address_1);
        AddValue(values, columns, "address_2", "@address2", DbType.String, taxi.address_2);
        AddValue(values, columns, "address_3", "@address3", DbType.String, taxi.address_3);
        AddValue(values, columns, "flight", "@flight", DbType.String, taxi.flight);
        AddValue(
            values,
            columns,
            "instructions",
            "@instructions",
            DbType.String,
            taxi.instructions
        );
        AddValue(
            values,
            columns,
            "destination_1",
            "@destination1",
            DbType.String,
            taxi.destination_1
        );
        AddValue(
            values,
            columns,
            "destination_2",
            "@destination2",
            DbType.String,
            taxi.destination_2
        );
        AddValue(
            values,
            columns,
            "destination_3",
            "@destination3",
            DbType.String,
            taxi.destination_3
        );
        short? legacyUserId = columns.ContainsKey("user_access_code")
            ? taxi.user_access_code ?? ToLegacyShortUserId(currentUserId)
            : null;
        AddValue(
            values,
            columns,
            "user_access_code",
            "@userAccessCode",
            DbType.Int16,
            legacyUserId
        );
        AddValue(
            values,
            columns,
            "request_date",
            "@requestDate",
            DbType.DateTime,
            taxi.request_date ?? DateTime.Now
        );
        AddValue(values, columns, "confirmed", "@confirmed", DbType.Int16, taxi.confirmed);
        AddValue(
            values,
            columns,
            "sub_contractor_id",
            "@subContractorId",
            DbType.Int16,
            taxi.sub_contractor_id
        );
        AddValue(values, columns, "cancelled", "@cancelled", DbType.String, taxi.cancelled);
        AddValue(values, columns, "resp_code", "@respCode", DbType.String, taxi.resp_code);
        AddValue(values, columns, "object_code", "@objectCode", DbType.String, taxi.object_code);
        AddValue(values, columns, "fms_code", "@fmsCode", DbType.String, taxi.fms_code);
        AddValue(
            values,
            columns,
            "date_required_2",
            "@dateRequired2",
            DbType.DateTime,
            taxi.date_required_2
        );
        AddValue(
            values,
            columns,
            "time_required_2",
            "@timeRequired2",
            DbType.DateTime,
            taxi.time_required_2
        );
        AddValue(values, columns, "address_12", "@address12", DbType.String, taxi.address_12);
        AddValue(values, columns, "address_22", "@address22", DbType.String, taxi.address_22);
        AddValue(values, columns, "address_32", "@address32", DbType.String, taxi.address_32);
        AddValue(
            values,
            columns,
            "destination_12",
            "@destination12",
            DbType.String,
            taxi.destination_12
        );
        AddValue(
            values,
            columns,
            "destination_22",
            "@destination22",
            DbType.String,
            taxi.destination_22
        );
        AddValue(
            values,
            columns,
            "destination_32",
            "@destination32",
            DbType.String,
            taxi.destination_32
        );
        AddValue(
            values,
            columns,
            "trans_man_name",
            "@transManName",
            DbType.String,
            taxi.trans_man_name
        );
        AddValue(
            values,
            columns,
            "trans_man_date",
            "@transManDate",
            DbType.DateTime,
            taxi.trans_man_date
        );
        AddValue(
            values,
            columns,
            "trans_man_rank",
            "@transManRank",
            DbType.String,
            taxi.trans_man_rank
        );
        AddValue(
            values,
            columns,
            "trans_man_tel",
            "@transManTel",
            DbType.String,
            taxi.trans_man_tel
        );
        AddValue(values, columns, "booking_by", "@bookingBy", DbType.String, taxi.booking_by);
        AddValue(values, columns, "driver", "@driver", DbType.String, taxi.driver);
        AddValue(
            values,
            columns,
            "arrival_time",
            "@arrivalTime",
            DbType.DateTime,
            taxi.arrival_time
        );
        AddValue(values, columns, "reg_num", "@regNum", DbType.String, taxi.reg_num);
        AddValue(values, columns, "project", "@project", DbType.String, taxi.project);
        AddValue(
            values,
            columns,
            "driver_available",
            "@driverAvailable",
            DbType.Boolean,
            taxi.driver_available
        );
        AddValue(values, columns, "persal", "@persal", DbType.String, taxi.persal);
        AddValue(values, columns, "JIA_pickup", "@jiaPickup", DbType.Boolean, taxi.JIA_pickup);
        AddValue(
            values,
            columns,
            "official_tel_num",
            "@officialTelNum",
            DbType.String,
            taxi.official_tel_num
        );
        AddValue(
            values,
            columns,
            "parent_taxi_code",
            "@parentTaxiCode",
            DbType.Int32,
            taxi.parent_taxi_code
        );
        AddValue(values, columns, "fund_code", "@fundCode", DbType.String, taxi.fund_code);

        if (isCreate)
        {
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
                UserIdOrNull(currentUserId)
            );
            AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);
        }

        return values;
    }

    private static void MergeTaxi(Taxi target, Taxi source)
    {
        target.rek_num = string.IsNullOrWhiteSpace(target.rek_num)
            ? source.rek_num
            : target.rek_num;
        if (target.site_code == 0)
            target.site_code = source.site_code;
        if (target.date_required == default)
            target.date_required = source.date_required;
        if (target.time_required == default)
            target.time_required = source.time_required;
        target.official = string.IsNullOrWhiteSpace(target.official)
            ? source.official
            : target.official;

        target.contractor_id ??= source.contractor_id;
        target.vmf_code ??= source.vmf_code;
        target.department_code ??= source.department_code;
        target.vehicle_type_code ??= source.vehicle_type_code;
        target.rank ??= source.rank;
        target.address_1 ??= source.address_1;
        target.address_2 ??= source.address_2;
        target.address_3 ??= source.address_3;
        target.flight ??= source.flight;
        target.instructions ??= source.instructions;
        target.destination_1 ??= source.destination_1;
        target.destination_2 ??= source.destination_2;
        target.destination_3 ??= source.destination_3;
        target.user_access_code ??= source.user_access_code;
        target.request_date ??= source.request_date;
        target.confirmed ??= source.confirmed;
        target.sub_contractor_id ??= source.sub_contractor_id;
        target.cancelled ??= source.cancelled;
        target.resp_code ??= source.resp_code;
        target.object_code ??= source.object_code;
        target.fms_code ??= source.fms_code;
        target.date_required_2 ??= source.date_required_2;
        target.time_required_2 ??= source.time_required_2;
        target.address_12 ??= source.address_12;
        target.address_22 ??= source.address_22;
        target.address_32 ??= source.address_32;
        target.destination_12 ??= source.destination_12;
        target.destination_22 ??= source.destination_22;
        target.destination_32 ??= source.destination_32;
        target.trans_man_name ??= source.trans_man_name;
        target.trans_man_date ??= source.trans_man_date;
        target.trans_man_rank ??= source.trans_man_rank;
        target.trans_man_tel ??= source.trans_man_tel;
        target.booking_by ??= source.booking_by;
        target.driver ??= source.driver;
        target.arrival_time ??= source.arrival_time;
        target.reg_num ??= source.reg_num;
        target.project ??= source.project;
        target.driver_available ??= source.driver_available;
        target.persal ??= source.persal;
        target.JIA_pickup ??= source.JIA_pickup;
        target.official_tel_num ??= source.official_tel_num;
        target.parent_taxi_code ??= source.parent_taxi_code;
        target.fund_code ??= source.fund_code;
    }

    private static void ValidateTaxi(Taxi taxi, bool requireRequisitionNumber)
    {
        if (requireRequisitionNumber && string.IsNullOrWhiteSpace(taxi.rek_num))
            throw new ArgumentException("Requisition number is required.", nameof(taxi));
        if (taxi.site_code == 0)
            throw new ArgumentException("Site code is required.", nameof(taxi));
        if (taxi.date_required == default)
            throw new ArgumentException("Required date is required.", nameof(taxi));
        if (taxi.time_required == default)
            throw new ArgumentException("Required time is required.", nameof(taxi));
        if (string.IsNullOrWhiteSpace(taxi.official))
            throw new ArgumentException("Official/passenger name is required.", nameof(taxi));
    }

    private static Taxi MapTaxi(DbDataReader reader)
    {
        var departmentCode = ReadInt16(reader, "__department_code");
        var siteCode = ReadInt16(reader, "__site_code");

        return new()
        {
            request_id = ReadInt32(reader, "request_id") ?? 0,
            rek_num = ReadString(reader, "rek_num"),
            contractor_id = ReadInt16(reader, "contractor_id"),
            vmf_code = ReadString(reader, "vmf_code"),
            department_code = ReadInt16(reader, "department_code"),
            site_code = siteCode ?? 0,
            date_required = ReadDateTime(reader, "date_required") ?? default,
            time_required = ReadDateTime(reader, "time_required") ?? default,
            vehicle_type_code = ReadInt16(reader, "vehicle_type_code"),
            official = ReadString(reader, "official"),
            rank = ReadString(reader, "rank"),
            confirmed = ReadInt16(reader, "confirmed"),
            sub_contractor_id = ReadInt16(reader, "sub_contractor_id"),
            cancelled = ReadString(reader, "cancelled"),
            driver = ReadString(reader, "driver"),
            reg_num = ReadString(reader, "reg_num"),
            parent_taxi_code = ReadInt32(reader, "parent_taxi_code"),
            address_1 = ReadString(reader, "address_1"),
            address_2 = ReadString(reader, "address_2"),
            address_3 = ReadString(reader, "address_3"),
            flight = ReadString(reader, "flight"),
            instructions = ReadString(reader, "instructions"),
            destination_1 = ReadString(reader, "destination_1"),
            destination_2 = ReadString(reader, "destination_2"),
            destination_3 = ReadString(reader, "destination_3"),
            user_access_code = ReadInt16(reader, "user_access_code"),
            request_date = ReadDateTime(reader, "request_date"),
            resp_code = ReadString(reader, "resp_code"),
            object_code = ReadString(reader, "object_code"),
            fms_code = ReadString(reader, "fms_code"),
            date_required_2 = ReadDateTime(reader, "date_required_2"),
            time_required_2 = ReadDateTime(reader, "time_required_2"),
            address_12 = ReadString(reader, "address_12"),
            address_22 = ReadString(reader, "address_22"),
            address_32 = ReadString(reader, "address_32"),
            destination_12 = ReadString(reader, "destination_12"),
            destination_22 = ReadString(reader, "destination_22"),
            destination_32 = ReadString(reader, "destination_32"),
            trans_man_name = ReadString(reader, "trans_man_name"),
            trans_man_date = ReadDateTime(reader, "trans_man_date"),
            trans_man_rank = ReadString(reader, "trans_man_rank"),
            trans_man_tel = ReadString(reader, "trans_man_tel"),
            booking_by = ReadString(reader, "booking_by"),
            arrival_time = ReadDateTime(reader, "arrival_time"),
            project = ReadString(reader, "project"),
            driver_available = ReadBoolean(reader, "driver_available"),
            persal = ReadString(reader, "persal"),
            JIA_pickup = ReadBoolean(reader, "JIA_pickup"),
            official_tel_num = ReadString(reader, "official_tel_num"),
            fund_code = ReadString(reader, "fund_code"),
            date_created = ReadDateTime(reader, "date_created") ?? default,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code = ReadInt32(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted") ?? false,
            Department = departmentCode.HasValue
                ? new Department
                {
                    department_code = departmentCode.Value,
                    description = ReadString(reader, "__department_description"),
                }
                : null,
            Site = siteCode.HasValue
                ? new Site
                {
                    Site_code = siteCode.Value,
                    description = ReadString(reader, "__site_description"),
                }
                : null,
        };
    }

    private static string GetActiveFilter(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string alias = ""
    ) =>
        columns.ContainsKey("is_deleted")
            ? $"ISNULL({(string.IsNullOrWhiteSpace(alias) ? "" : $"{alias}.")}[is_deleted], 0) = 0"
            : "1 = 1";

    private static string GetProjection(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string alias
    ) =>
        columns.ContainsKey(column)
            ? $"{alias}.[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static string GetSqlType(string column) =>
        DateColumns.Contains(column)
            ? "datetime2"
            : column switch
            {
                "request_id" or "parent_taxi_code" => "int",
                "contractor_id"
                or "department_code"
                or "site_code"
                or "vehicle_type_code"
                or "user_access_code"
                or "confirmed"
                or "sub_contractor_id" => "smallint",
                "driver_available" or "JIA_pickup" or "is_deleted" => "bit",
                "created_by_user_code" or "modified_by_user_code" => "int",
                _ => "varchar(1)",
            };

    private static void AddRequiredValue(
        ICollection<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (!columns.ContainsKey(column))
            throw new InvalidOperationException(
                $"The required compatibility column {column} is not available."
            );
        values.Add(new WriteValue(column, parameter, type, value));
    }

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (columns.ContainsKey(column) && value is not null)
            values.Add(new WriteValue(column, parameter, type, value));
    }

    private static void AddOptionalAssignment(
        List<string> assignments,
        DbCommand command,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (!columns.ContainsKey(column))
            return;
        assignments.Add($"[{column}] = {parameter}");
        AddParameter(command, parameter, type, value);
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
            AddParameter(command, value.Parameter, value.Type, value.Value);
    }

    private static void AddReportParameters(
        DbCommand command,
        IEnumerable<ReportParameter> parameters
    )
    {
        foreach (var parameter in parameters)
            AddParameter(command, parameter.Name, parameter.Type, parameter.Value);
    }

    private static void AddAllowedSiteScope(
        ICollection<string> conditions,
        ICollection<ReportParameter> parameters,
        IReadOnlySet<short>? allowedSiteCodes,
        string siteExpression
    )
    {
        if (allowedSiteCodes is null)
            return;

        var sites = allowedSiteCodes.Where(code => code > 0).Distinct().ToArray();
        if (sites.Length == 0)
        {
            conditions.Add("1 = 0");
            return;
        }

        var placeholders = sites
            .Select((_, index) => $"@allowedSite{index}")
            .ToArray();
        conditions.Add($"{siteExpression} IN ({string.Join(", ", placeholders)})");
        for (var index = 0; index < sites.Length; index++)
        {
            parameters.Add(new ReportParameter(placeholders[index], DbType.Int16, sites[index]));
        }
    }

    private static void AddParameter(
        DbCommand command,
        string name,
        DbType type,
        object? value,
        int? size = null
    )
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        if (size.HasValue)
            parameter.Size = size.Value;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal))?.TrimEnd();
    }

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
            return null;
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
            _ when DateTime.TryParse(Convert.ToString(value), out var parsed) => parsed,
            _ => null,
        };
    }

    private static bool? ReadBoolean(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private async Task<ConnectionScope> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();
        return new ConnectionScope(connection, shouldClose);
    }

    private static string NormalizeKey(string? value) =>
        (value ?? string.Empty).Trim().ToUpperInvariant();

    private static short ToLegacyShortUserId(int userId)
    {
        if (userId > short.MaxValue || userId < short.MinValue)
            throw new InvalidOperationException(
                $"User id {userId} cannot be stored in legacy taxi user_access_code column."
            );
        return (short)userId;
    }

    private static int? UserIdOrNull(int currentUserId) => currentUserId > 0 ? currentUserId : null;

    private sealed record ReportParameter(string Name, DbType Type, object? Value);

    private sealed record TaxiReportSql(
        string WhereClause,
        string OrderBy,
        IReadOnlyList<ReportParameter> Parameters
    );

    private sealed record ColumnInfo(string Name, string DataType);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

    private sealed class ConnectionScope : IAsyncDisposable
    {
        public ConnectionScope(DbConnection connection, bool shouldClose)
        {
            Connection = connection;
            _shouldClose = shouldClose;
        }

        public DbConnection Connection { get; }
        private readonly bool _shouldClose;

        public async ValueTask DisposeAsync()
        {
            if (_shouldClose)
                await Connection.CloseAsync();
        }
    }
}

/// <summary>
/// Indicates that a taxi-request mutation cannot safely preserve the legacy
/// journal/reversal/VIP billing trigger chain.
/// </summary>
public sealed class LegacyTaxiWorkflowUnavailableException : InvalidOperationException
{
    public LegacyTaxiWorkflowUnavailableException(string message)
        : base(message) { }
}

/// <summary>
/// Indicates that the deployed requisition procedure differs from the archived
/// legacy contract. A procedure with the right name but the wrong signature must
/// not be replaced with guessed direct DML.
/// </summary>
public sealed class LegacyTaxiProcedureContractException : InvalidOperationException
{
    public LegacyTaxiProcedureContractException(string message)
        : base(message) { }
}

public sealed class LegacyTaxiRecurringWorkflowUnavailableException : InvalidOperationException
{
    public LegacyTaxiRecurringWorkflowUnavailableException(string message)
        : base(message) { }
}
