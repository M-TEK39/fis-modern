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
/// Compatibility access for the original dbo.Logsheets table. The original
/// table has required transaction columns that the modern CRUD contract does
/// not expose, so inserts supply safe legacy defaults while optional audit
/// columns are negotiated at runtime.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility projections and all submitted values are parameters."
)]
internal sealed class LegacyLogsheetRepository : ILogsheetRepository
{
    private const string TableName = "Logsheets";

    private static readonly string[] RequiredColumns =
    [
        "log_code",
        "vmf_code",
        "start_odo",
        "end_odo",
        "month",
        "site_code",
        "rek_num",
    ];

    private readonly FisDbContext _context;

    public LegacyLogsheetRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Logsheet?> GetByIdAsync(int logCode) =>
        (
            await QueryAsync(
                "l.[log_code] = @logCode",
                command => AddParameter(command, "@logCode", DbType.Int32, logCode)
            )
        ).SingleOrDefault();

    public Task<IEnumerable<Logsheet>> GetAllAsync() => QueryAsEnumerableAsync();

    public async Task<LogsheetPage> GetPageAsync(LogsheetPageQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var requestedPage = Math.Max(1, query.Page);
        var columns = await GetAvailableColumnsAsync();
        var conditions = BuildListConditions(columns);
        if (query.VmfCode is > 0)
            conditions.Add("l.[vmf_code] = @vmfCode");
        if (!string.IsNullOrWhiteSpace(query.RequisitionNumber))
            conditions.Add("l.[rek_num] = @requisitionNumber");
        var whereClause = string.Join(" AND ", conditions);

        await using var scope = await OpenConnectionAsync();

        await using var countCommand = scope.Connection.CreateCommand();
        countCommand.Transaction = CurrentTransaction;
        countCommand.CommandText = $"""
            SELECT COUNT(1)
            FROM [dbo].[{TableName}] l
            LEFT JOIN [dbo].[vehicle_master] v ON v.[vmf_code] = l.[vmf_code]
            LEFT JOIN [dbo].[site] s ON s.[Site_code] = l.[site_code]
            WHERE {whereClause}
            """;
        AddPageParameters(countCommand, query);
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var page = Math.Min(requestedPage, totalPages);
        var skip = checked((long)(page - 1) * pageSize);

        await using var dataCommand = scope.Connection.CreateCommand();
        dataCommand.Transaction = CurrentTransaction;
        dataCommand.CommandText = $"""
            SELECT
                {BuildProjection(columns)}
            FROM [dbo].[{TableName}] l
            LEFT JOIN [dbo].[vehicle_master] v ON v.[vmf_code] = l.[vmf_code]
            LEFT JOIN [dbo].[site] s ON s.[Site_code] = l.[site_code]
            WHERE {whereClause}
            ORDER BY l.[month] DESC, l.[log_code] DESC
            OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
            """;
        AddParameter(dataCommand, "@skip", DbType.Int64, skip);
        AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);
        AddPageParameters(dataCommand, query);

        var items = new List<Logsheet>();
        await using var reader = await dataCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(Map(reader));

        return new LogsheetPage(items, page, pageSize, total);
    }

    private static void AddPageParameters(DbCommand command, LogsheetPageQuery query)
    {
        if (query.VmfCode is > 0)
            AddParameter(command, "@vmfCode", DbType.Int32, query.VmfCode.Value);
        if (!string.IsNullOrWhiteSpace(query.RequisitionNumber))
            AddParameter(
                command,
                "@requisitionNumber",
                DbType.String,
                query.RequisitionNumber.Trim()
            );
    }

    public async Task<IEnumerable<Logsheet>> GetByVehicleAsync(int vmfCode) =>
        await QueryAsync(
            "l.[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode)
        );

    public async Task<IEnumerable<Logsheet>> GetByMonthAsync(DateTime month)
    {
        var startOfMonth = new DateTime(month.Year, month.Month, 1);
        var endOfMonth = startOfMonth.AddMonths(1);
        return await QueryAsync(
            "l.[month] >= @startMonth AND l.[month] < @endMonth",
            command =>
            {
                AddParameter(command, "@startMonth", DbType.DateTime, startOfMonth);
                AddParameter(command, "@endMonth", DbType.DateTime, endOfMonth);
            }
        );
    }

    public async Task<Logsheet> CreateAsync(Logsheet logsheet, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(logsheet);
        IDbContextTransaction? ownedTransaction = null;
        if (_context.Database.CurrentTransaction is null)
        {
            ownedTransaction = await _context.Database.BeginTransactionAsync(
                IsolationLevel.ReadCommitted
            );
        }

        var committed = false;
        try
        {
            await EnsureLegacyTriggersAsync(
                "TRG_INS_LogsheetJournalDetailRecord",
                "TRG_INS_UPD_Logsheet_Check_Overlap",
                "TRG_INS_UPD_Logsheet_CheckOverlappingOpenELsTrip"
            );
            var columns = await GetAvailableColumnsAsync();
            var now = DateTime.Now;
            if (logsheet.trans_date == default)
            {
                logsheet.trans_date = now;
            }

            var insertProcedure = await ResolveProcedureParametersAsync("DEV_INS_Logsheets");
            int insertedCode;
            if (insertProcedure is not null)
            {
                EnsureProcedureContract("DEV_INS_Logsheets", insertProcedure, InsertProcedureParameters);
                insertedCode = await ExecuteLegacyInsertProcedureAsync(logsheet, currentUserId);
            }
            else
            {
                // Explicit compatibility fallback: only older databases without the
                // original insert procedure may use the parameterized trigger-backed
                // DML path below.
                var values = new List<WriteValue>();

                AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, logsheet.vmf_code, true);
                AddValue(
                    values,
                    columns,
                    "start_odo",
                    "@startOdo",
                    DbType.Double,
                    logsheet.start_odo,
                    true
                );
                AddValue(values, columns, "end_odo", "@endOdo", DbType.Double, logsheet.end_odo, true);
                AddValue(values, columns, "month", "@month", DbType.DateTime, logsheet.month, true);
                AddValue(values, columns, "site_code", "@siteCode", DbType.Int16, logsheet.site_code, true);
                AddValue(values, columns, "rek_num", "@requisition", DbType.String, logsheet.rek_num, true);
                AddValue(values, columns, "days_used", "@daysUsed", DbType.Int32, logsheet.days_used, true);
                AddValue(
                    values,
                    columns,
                    "bund_num",
                    "@bundleNumber",
                    DbType.Int32,
                    logsheet.bund_num,
                    true
                );
                // Log_Entry_flow derives these accounting/ownership fields from the
                // selected legacy contract and the current user. They are added once
                // below; repeating a column in an INSERT is rejected by SQL Server.
                AddValue(
                    values,
                    columns,
                    "trans_date",
                    "@transDate",
                    DbType.DateTime,
                    logsheet.trans_date,
                    true
                );
                AddValue(
                    values,
                    columns,
                    "driver_time",
                    "@driverTime",
                    DbType.Double,
                    logsheet.driver_time,
                    false
                );
                AddValue(
                    values,
                    columns,
                    "FBS_comp",
                    "@fbsComp",
                    DbType.DateTime,
                    logsheet.FBS_comp,
                    false
                );
                AddValue(
                    values,
                    columns,
                    "user_access_code",
                    "@userAccessCode",
                    DbType.Int16,
                    UserAccessCode(currentUserId),
                    true
                );
                AddValue(
                    values,
                    columns,
                    "trans_time",
                    "@transTime",
                    DbType.Time,
                    logsheet.trans_time == default ? now.TimeOfDay : logsheet.trans_time,
                    true
                );
                AddValue(
                    values,
                    columns,
                    "department_code",
                    "@departmentCode",
                    DbType.Int16,
                    logsheet.department_code > 0 ? logsheet.department_code : logsheet.site_code,
                    true
                );
                AddValue(
                    values,
                    columns,
                    "contract_code",
                    "@contractCode",
                    DbType.Int32,
                    logsheet.contract_code,
                    false
                );
                // Logsheets defines its own NEWID() default. The legacy entry page did
                // not manufacture a journal detail key, so preserve that database-owned
                // default unless a genuine legacy caller supplied a key.
                AddValue(
                    values,
                    columns,
                    "journal_detail_code",
                    "@journalDetailCode",
                    DbType.Guid,
                    logsheet.journal_detail_code == Guid.Empty ? null : logsheet.journal_detail_code,
                    false
                );
                AddValue(
                    values,
                    columns,
                    "parent_log_code",
                    "@parentLogCode",
                    DbType.Int32,
                    logsheet.parent_log_code,
                    false
                );
                AddValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, now, true);
                AddValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, now, false);
                AddValue(
                    values,
                    columns,
                    "created_by_user_code",
                    "@createdBy",
                    DbType.Int32,
                    UserIdOrNull(currentUserId),
                    false
                );
                AddValue(
                    values,
                    columns,
                    "modified_by_user_code",
                    "@modifiedBy",
                    DbType.Int32,
                    UserIdOrNull(currentUserId),
                    false
                );
                AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false, true);

                await using var scope = await OpenConnectionAsync();
                await using var command = scope.Connection.CreateCommand();
                command.Transaction = CurrentTransaction;
                command.CommandText =
                    $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[log_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
                AddParameters(command, values);
                insertedCode = Convert.ToInt32(await command.ExecuteScalarAsync());
            }

            // Legacy Log_Entry_ACT1.aspx updates the vehicle's current odometer
            // and its odometer-update date after a successful logsheet insert.
            // Keep this in the same transaction as the insert/procedure call so
            // billing and vehicle state cannot diverge.
            await UpdateVehicleOdometerAsync(logsheet);

            // Read the inserted row while the owned transaction is still
            // active. A committed DbTransaction must not be reused for the
            // reload after CommitAsync; disposing it happens in finally.
            var inserted = await GetByIdAsync(insertedCode)
                ?? throw new InvalidOperationException("The legacy logsheet procedure inserted a row that could not be read.");

            if (ownedTransaction is not null)
            {
                await ownedTransaction.CommitAsync();
                committed = true;
            }

            return inserted;
        }
        catch
        {
            if (ownedTransaction is not null && !committed)
            {
                await ownedTransaction.RollbackAsync();
            }

            throw;
        }
        finally
        {
            if (ownedTransaction is not null)
            {
                await ownedTransaction.DisposeAsync();
            }
        }
    }

    private async Task UpdateVehicleOdometerAsync(Logsheet logsheet)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = """
            UPDATE [dbo].[vehicle_master]
            SET [current_odo] = @currentOdo,
                [odo_update_date] = @odoUpdateDate
            WHERE [vmf_code] = @vmfCode
              AND [current_odo] < @currentOdo
            """;
        AddParameter(command, "@currentOdo", DbType.Int32, Convert.ToInt32(logsheet.end_odo));
        AddParameter(command, "@odoUpdateDate", DbType.DateTime, logsheet.trans_date);
        AddParameter(command, "@vmfCode", DbType.Int32, logsheet.vmf_code);
        // Legacy Log_Entry_ACT1.aspx only advances current_odo when the new
        // end reading is greater than the vehicle's existing value. An
        // out-of-order historical log is still accepted; it must not roll
        // the master odometer backwards or turn a successful log insert into
        // a false failure.
        await command.ExecuteNonQueryAsync();
    }

    public async Task<Logsheet> UpdateAsync(Logsheet logsheet, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(logsheet);
        await EnsureLegacyTriggersAsync(
            "TRG_UPD_LogsheetJournalDetailRecord",
            "TRG_INS_UPD_Logsheet_Check_Overlap",
            "TRG_INS_UPD_Logsheet_CheckOverlappingOpenELsTrip"
        );
        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();

        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, logsheet.vmf_code, true);
        AddValue(
            values,
            columns,
            "start_odo",
            "@startOdo",
            DbType.Double,
            logsheet.start_odo,
            true
        );
        AddValue(values, columns, "end_odo", "@endOdo", DbType.Double, logsheet.end_odo, true);
        AddValue(values, columns, "month", "@month", DbType.DateTime, logsheet.month, true);
        AddValue(values, columns, "site_code", "@siteCode", DbType.Int16, logsheet.site_code, true);
        AddValue(values, columns, "rek_num", "@requisition", DbType.String, logsheet.rek_num, true);
        AddValue(values, columns, "days_used", "@daysUsed", DbType.Int32, logsheet.days_used, true);
        AddValue(
            values,
            columns,
            "bund_num",
            "@bundleNumber",
            DbType.Int32,
            logsheet.bund_num,
            true
        );
        // Log_Edit3.aspx updates these transaction fields together with the
        // odometer range. Keep the selected contract/department and editor
        // aligned with the row that the legacy journal trigger sees.
        AddValue(
            values,
            columns,
            "driver_time",
            "@driverTime",
            DbType.Double,
            logsheet.driver_time,
            true
        );
        AddValue(
            values,
            columns,
            "user_access_code",
            "@userAccessCode",
            DbType.Int16,
            UserAccessCode(currentUserId),
            true
        );
        AddValue(
            values,
            columns,
            "department_code",
            "@departmentCode",
            DbType.Int16,
            logsheet.department_code > 0 ? logsheet.department_code : logsheet.site_code,
            true
        );
        AddValue(
            values,
            columns,
            "contract_code",
            "@contractCode",
            DbType.Int32,
            logsheet.contract_code,
            true
        );
        AddValue(
            values,
            columns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            DateTime.UtcNow,
            false
        );
        AddValue(
            values,
            columns,
            "modified_by_user_code",
            "@modifiedBy",
            DbType.Int32,
            UserIdOrNull(currentUserId),
            false
        );
        await ExecuteUpdateAsync(logsheet.log_code, values);
        return await GetByIdAsync(logsheet.log_code)
            ?? throw new KeyNotFoundException($"Logsheet not found with code: {logsheet.log_code}");
    }

    public async Task DeleteAsync(int logCode, int currentUserId)
    {
        await EnsureLegacyTriggersAsync("TRG_DEL_Logsheet");
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;

        // Log_Delete_3.aspx physically deletes the logsheet row. An optional
        // modern is_deleted column is not a license to change that business
        // action; retain the legacy delete/audit-trigger path.
        command.CommandText = $"DELETE FROM [dbo].[{TableName}] WHERE [log_code] = @logCode";

        AddParameter(command, "@logCode", DbType.Int32, logCode);
        if (await command.ExecuteNonQueryAsync() == 0)
            throw new KeyNotFoundException($"Logsheet not found with code: {logCode}");
    }

    private async Task<IEnumerable<Logsheet>> QueryAsEnumerableAsync() => await QueryAsync();

    private async Task<List<Logsheet>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        var conditions = BuildListConditions(columns, predicate);
        command.CommandText = $"""
            SELECT
                {BuildProjection(columns)}
            FROM [dbo].[{TableName}] l
            LEFT JOIN [dbo].[vehicle_master] v ON v.[vmf_code] = l.[vmf_code]
            LEFT JOIN [dbo].[site] s ON s.[Site_code] = l.[site_code]
            WHERE {string.Join(" AND ", conditions.DefaultIfEmpty("1 = 1"))}
            ORDER BY l.[month] DESC, l.[log_code] DESC
            """;
        configure?.Invoke(command);

        var results = new List<Logsheet>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(Map(reader));
        return results;
    }

    private static List<string> BuildListConditions(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string? predicate = null
    )
    {
        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(predicate))
            conditions.Add($"({predicate})");
        if (columns.ContainsKey("is_deleted"))
            conditions.Add("ISNULL(l.[is_deleted], 0) = 0");
        if (conditions.Count == 0)
            conditions.Add("1 = 1");
        return conditions;
    }

    private async Task ExecuteUpdateAsync(int logCode, IReadOnlyCollection<WriteValue> values)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [log_code] = @logCode";
        AddParameters(command, values);
        AddParameter(command, "@logCode", DbType.Int32, logCode);
        if (await command.ExecuteNonQueryAsync() == 0)
            throw new KeyNotFoundException($"Logsheet not found with code: {logCode}");
    }

    private async Task<Dictionary<string, ColumnInfo>> GetAvailableColumnsAsync()
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            "SELECT [COLUMN_NAME] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table";
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, TableName);

        var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns[reader.GetString(0)] = new ColumnInfo(reader.GetString(0));
        if (RequiredColumns.Any(column => !columns.ContainsKey(column)))
            throw new InvalidOperationException(
                "The logsheet compatibility table is missing required legacy columns."
            );
        return columns;
    }

    private async Task EnsureLegacyTriggersAsync(params string[] triggerNames)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = CurrentTransaction;
            command.CommandText = """
                SELECT [tr].[name], [tr].[is_disabled]
                FROM [sys].[triggers] AS [tr]
                INNER JOIN [sys].[tables] AS [tb]
                    ON [tb].[object_id] = [tr].[parent_id]
                INNER JOIN [sys].[schemas] AS [sc]
                    ON [sc].[schema_id] = [tb].[schema_id]
                WHERE [sc].[name] = N'dbo'
                  AND [tb].[name] = N'Logsheets'
                  AND [tr].[name] IN (N'TRG_INS_LogsheetJournalDetailRecord', N'TRG_UPD_LogsheetJournalDetailRecord', N'TRG_INS_UPD_Logsheet_Check_Overlap', N'TRG_INS_UPD_Logsheet_CheckOverlappingOpenELsTrip', N'TRG_DEL_Logsheet');
                """;
            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(0) && !reader.IsDBNull(1) && !reader.GetBoolean(1))
                    present.Add(reader.GetString(0));
            }

            var missing = triggerNames.Where(name => !present.Contains(name)).ToArray();
            if (missing.Length > 0)
            {
                throw new NotSupportedException(
                    $"The legacy logsheet trigger workflow is unavailable ({string.Join(", ", missing)}); no direct-DML fallback was run."
                );
            }
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static readonly string[] InsertProcedureParameters =
    [
        "@LogCode",
        "@VMFCode",
        "@StartOdoMeter",
        "@EndOdoMeter",
        "@Month",
        "@SiteCode",
        "@RekNum",
        "@DaysUsed",
        "@BundNum",
        "@TransactionDate",
        "@DriverTime",
        "@FBSComp",
        "@UserAccessCode",
        "@TransactionTime",
        "@DepartmentCode",
        "@ContractCode",
        "@ParentLogCode",
    ];

    private async Task<IReadOnlyList<string>?> ResolveProcedureParametersAsync(string procedureName)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = """
            SELECT [p].[name]
            FROM [sys].[procedures] AS [sp]
            INNER JOIN [sys].[schemas] AS [s] ON [s].[schema_id] = [sp].[schema_id]
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

    private static void EnsureProcedureContract(
        string procedureName,
        IReadOnlyList<string> actual,
        IReadOnlyList<string> expected
    )
    {
        if (!actual.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The deployed legacy procedure {procedureName} does not match its archived parameter contract; no direct-DML fallback was run."
            );
        }
    }

    private async Task<int> ExecuteLegacyInsertProcedureAsync(Logsheet logsheet, int currentUserId)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "dbo.DEV_INS_Logsheets";

        var output = command.CreateParameter();
        output.ParameterName = "@LogCode";
        output.DbType = DbType.Int32;
        output.Direction = ParameterDirection.Output;
        command.Parameters.Add(output);
        AddParameter(command, "@VMFCode", DbType.Int32, logsheet.vmf_code);
        AddParameter(command, "@StartOdoMeter", DbType.Int32, Convert.ToInt32(logsheet.start_odo));
        AddParameter(command, "@EndOdoMeter", DbType.Int32, Convert.ToInt32(logsheet.end_odo));
        AddParameter(command, "@Month", DbType.DateTime, logsheet.month);
        AddParameter(command, "@SiteCode", DbType.Int32, logsheet.site_code);
        AddParameter(command, "@RekNum", DbType.String, logsheet.rek_num);
        AddParameter(command, "@DaysUsed", DbType.Int32, logsheet.days_used);
        AddParameter(command, "@BundNum", DbType.Int32, logsheet.bund_num);
        AddParameter(command, "@TransactionDate", DbType.DateTime, logsheet.trans_date == default ? DateTime.Now : logsheet.trans_date);
        AddParameter(command, "@DriverTime", DbType.Double, logsheet.driver_time);
        AddParameter(command, "@FBSComp", DbType.DateTime, logsheet.FBS_comp);
        AddParameter(command, "@UserAccessCode", DbType.Int32, UserAccessCode(currentUserId));
        AddParameter(command, "@TransactionTime", DbType.DateTime, DateTime.Today.Add(logsheet.trans_time == default ? DateTime.Now.TimeOfDay : logsheet.trans_time));
        AddParameter(command, "@DepartmentCode", DbType.Int16, logsheet.department_code > 0 ? logsheet.department_code : logsheet.site_code);
        AddParameter(command, "@ContractCode", DbType.Int32, logsheet.contract_code);
        AddParameter(command, "@ParentLogCode", DbType.Int32, logsheet.parent_log_code);

        await command.ExecuteNonQueryAsync();
        if (output.Value is null or DBNull || !int.TryParse(output.Value.ToString(), out var logCode) || logCode <= 0)
        {
            throw new InvalidOperationException(
                "The legacy logsheet insert procedure did not return a log code."
            );
        }

        return logCode;
    }

    private static string BuildProjection(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        string.Join(
            ",\n                ",
            [
                "l.[log_code] AS [log_code]",
                "l.[vmf_code] AS [vmf_code]",
                "l.[start_odo] AS [start_odo]",
                "l.[end_odo] AS [end_odo]",
                "l.[month] AS [month]",
                "l.[site_code] AS [site_code]",
                "l.[rek_num] AS [rek_num]",
                OptionalExpression(columns, "days_used", "int") + " AS [days_used]",
                OptionalExpression(columns, "bund_num", "int") + " AS [bund_num]",
                OptionalExpression(columns, "trans_date", "datetime2") + " AS [trans_date]",
                OptionalExpression(columns, "driver_time", "float") + " AS [driver_time]",
                OptionalExpression(columns, "FBS_comp", "datetime2") + " AS [FBS_comp]",
                OptionalExpression(columns, "user_access_code", "int") + " AS [user_access_code]",
                OptionalExpression(columns, "trans_time", "time") + " AS [trans_time]",
                OptionalExpression(columns, "department_code", "int") + " AS [department_code]",
                OptionalExpression(columns, "contract_code", "int") + " AS [contract_code]",
                OptionalExpression(columns, "journal_detail_code", "uniqueidentifier")
                    + " AS [journal_detail_code]",
                OptionalExpression(columns, "parent_log_code", "int") + " AS [parent_log_code]",
                "v.[fleet_number] AS [fleet_number]",
                "v.[registration_number] AS [registration_number]",
                "s.[description] AS [site_description]",
                DateCreatedExpression(columns) + " AS [date_created]",
                OptionalExpression(columns, "date_updated", "datetime2") + " AS [date_updated]",
                CreatedByExpression(columns) + " AS [created_by_user_code]",
                OptionalExpression(columns, "modified_by_user_code", "int")
                    + " AS [modified_by_user_code]",
                OptionalExpression(columns, "is_deleted", "bit") + " AS [is_deleted]",
            ]
        );

    private static string DateCreatedExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("date_created")
            ? $"COALESCE(l.[date_created], {OptionalExpression(columns, "trans_date", "datetime2")}, l.[month])"
            : $"COALESCE({OptionalExpression(columns, "trans_date", "datetime2")}, l.[month])";

    private static string CreatedByExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("created_by_user_code")
            ? $"COALESCE(l.[created_by_user_code], {OptionalExpression(columns, "user_access_code", "int")})"
            : OptionalExpression(columns, "user_access_code", "int");

    private static string OptionalExpression(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string sqlType
    ) => columns.ContainsKey(column) ? $"l.[{column}]" : $"CAST(NULL AS {sqlType})";

    private static Logsheet Map(DbDataReader reader)
    {
        var vmfCode = ReadInt(reader, "vmf_code") ?? 0;
        var siteCode = ReadShort(reader, "site_code") ?? 0;
        var logCode = ReadInt(reader, "log_code") ?? 0;
        var fleetNumber = ReadString(reader, "fleet_number");
        var registrationNumber = ReadString(reader, "registration_number");
        var siteDescription = ReadString(reader, "site_description");

        return new Logsheet
        {
            log_code = logCode,
            vmf_code = vmfCode,
            start_odo = ReadDouble(reader, "start_odo"),
            end_odo = ReadDouble(reader, "end_odo"),
            month = ReadDate(reader, "month") ?? default,
            site_code = siteCode,
            rek_num = ReadString(reader, "rek_num"),
            days_used = ReadInt(reader, "days_used"),
            bund_num = ReadInt(reader, "bund_num"),
            trans_date = ReadDate(reader, "trans_date") ?? default,
            driver_time = ReadDoubleNullable(reader, "driver_time"),
            FBS_comp = ReadDate(reader, "FBS_comp"),
            user_access_code = ReadShort(reader, "user_access_code"),
            trans_time = ReadTime(reader, "trans_time"),
            department_code = ReadShort(reader, "department_code") ?? 0,
            contract_code = ReadInt(reader, "contract_code"),
            journal_detail_code = ReadGuid(reader, "journal_detail_code"),
            parent_log_code = ReadInt(reader, "parent_log_code"),
            date_created = ReadDate(reader, "date_created") ?? default,
            date_updated = ReadDate(reader, "date_updated"),
            created_by_user_code = ReadInt(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt(reader, "modified_by_user_code"),
            is_deleted = ReadBool(reader, "is_deleted"),
            Vehicle = new Vehicle
            {
                vmf_code = vmfCode,
                fleet_number = fleetNumber,
                registration_number = registrationNumber,
            },
            Site = new Site { Site_code = siteCode, description = siteDescription },
        };
    }

    private async Task<ConnectionScope> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();
        return new ConnectionScope(connection, shouldClose);
    }

    private DbTransaction? CurrentTransaction =>
        _context.Database.CurrentTransaction?.GetDbTransaction();

    private static short? UserAccessCode(int value) =>
        value is > 0 and <= short.MaxValue ? (short)value : null;

    private static int? UserIdOrNull(int value) => value > 0 ? value : null;

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
            values.Add(new WriteValue(column, parameter, type, value));
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
            AddParameter(command, value.Parameter, value.Type, value.Value);
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? ReadString(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToString(reader[name]);

    private static int? ReadInt(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToInt32(reader[name]);

    private static short? ReadShort(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToInt16(reader[name]);

    private static double ReadDouble(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? 0 : Convert.ToDouble(reader[name]);

    private static double? ReadDoubleNullable(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToDouble(reader[name]);

    private static DateTime? ReadDate(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToDateTime(reader[name]);

    private static TimeSpan ReadTime(DbDataReader reader, string name)
    {
        if (reader.IsDBNull(reader.GetOrdinal(name)))
            return default;
        var value = reader[name];
        return value is TimeSpan time ? time : Convert.ToDateTime(value).TimeOfDay;
    }

    private static Guid ReadGuid(DbDataReader reader, string name)
    {
        if (reader.IsDBNull(reader.GetOrdinal(name)))
            return Guid.Empty;
        var value = reader[name];
        return value is Guid guid ? guid
            : Guid.TryParse(Convert.ToString(value), out var parsed) ? parsed
            : Guid.Empty;
    }

    private static bool ReadBool(DbDataReader reader, string name) =>
        !reader.IsDBNull(reader.GetOrdinal(name)) && Convert.ToBoolean(reader[name]);

    private sealed record ColumnInfo(string Name);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

    private sealed class ConnectionScope(DbConnection connection, bool shouldClose)
        : IAsyncDisposable
    {
        public DbConnection Connection { get; } = connection;

        public async ValueTask DisposeAsync()
        {
            if (shouldClose)
                await Connection.CloseAsync();
        }
    }
}
