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
    Justification = "SQL identifiers come only from fixed compatibility projections and all submitted values are parameters.")]
internal sealed class LegacyLogsheetRepository : ILogsheetRepository
{
    private const string TableName = "Logsheets";

    private static readonly string[] RequiredColumns =
    [
        "log_code", "vmf_code", "start_odo", "end_odo", "month", "site_code", "rek_num",
        "days_used", "bund_num", "trans_date", "driver_time", "FBS_comp", "user_access_code",
        "trans_time", "department_code", "contract_code", "journal_detail_code", "parent_log_code"
    ];

    private readonly FisDbContext _context;

    public LegacyLogsheetRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Logsheet?> GetByIdAsync(int logCode)
        => (await QueryAsync(
            "l.[log_code] = @logCode",
            command => AddParameter(command, "@logCode", DbType.Int32, logCode)))
            .SingleOrDefault();

    public Task<IEnumerable<Logsheet>> GetAllAsync()
        => QueryAsEnumerableAsync();

    public async Task<IEnumerable<Logsheet>> GetByVehicleAsync(int vmfCode)
        => await QueryAsync(
            "l.[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode));

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
            });
    }

    public async Task<Logsheet> CreateAsync(Logsheet logsheet, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(logsheet);
        var columns = await GetAvailableColumnsAsync();
        var now = DateTime.Now;
        var values = new List<WriteValue>();

        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, logsheet.vmf_code, true);
        AddValue(values, columns, "start_odo", "@startOdo", DbType.Double, logsheet.start_odo, true);
        AddValue(values, columns, "end_odo", "@endOdo", DbType.Double, logsheet.end_odo, true);
        AddValue(values, columns, "month", "@month", DbType.DateTime, logsheet.month, true);
        AddValue(values, columns, "site_code", "@siteCode", DbType.Int16, logsheet.site_code, true);
        AddValue(values, columns, "rek_num", "@requisition", DbType.String, logsheet.rek_num, true);
        AddValue(values, columns, "days_used", "@daysUsed", DbType.Int32, logsheet.days_used, true);
        AddValue(values, columns, "bund_num", "@bundleNumber", DbType.Int32, logsheet.bund_num, true);
        AddValue(values, columns, "trans_date", "@transDate", DbType.DateTime, logsheet.trans_date == default ? now : logsheet.trans_date, true);
        AddValue(values, columns, "driver_time", "@driverTime", DbType.Double, logsheet.driver_time, false);
        AddValue(values, columns, "FBS_comp", "@fbsComp", DbType.DateTime, logsheet.FBS_comp, false);
        AddValue(values, columns, "user_access_code", "@userAccessCode", DbType.Int16, UserAccessCode(currentUserId), true);
        AddValue(values, columns, "trans_time", "@transTime", DbType.Time, logsheet.trans_time == default ? now.TimeOfDay : logsheet.trans_time, true);
        AddValue(values, columns, "department_code", "@departmentCode", DbType.Int16, logsheet.department_code > 0 ? logsheet.department_code : logsheet.site_code, true);
        AddValue(values, columns, "contract_code", "@contractCode", DbType.Int32, logsheet.contract_code, false);
        AddValue(values, columns, "journal_detail_code", "@journalDetailCode", DbType.Guid, logsheet.journal_detail_code == Guid.Empty ? Guid.NewGuid() : logsheet.journal_detail_code, true);
        AddValue(values, columns, "parent_log_code", "@parentLogCode", DbType.Int32, logsheet.parent_log_code, false);
        AddValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, now, true);
        AddValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, now, false);
        AddValue(values, columns, "created_by_user_code", "@createdBy", DbType.Int32, UserIdOrNull(currentUserId), false);
        AddValue(values, columns, "modified_by_user_code", "@modifiedBy", DbType.Int32, UserIdOrNull(currentUserId), false);
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false, true);

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[log_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        var id = Convert.ToInt32(await command.ExecuteScalarAsync());
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Created logsheet could not be read.");
    }

    public async Task<Logsheet> UpdateAsync(Logsheet logsheet, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(logsheet);
        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();

        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, logsheet.vmf_code, true);
        AddValue(values, columns, "start_odo", "@startOdo", DbType.Double, logsheet.start_odo, true);
        AddValue(values, columns, "end_odo", "@endOdo", DbType.Double, logsheet.end_odo, true);
        AddValue(values, columns, "month", "@month", DbType.DateTime, logsheet.month, true);
        AddValue(values, columns, "site_code", "@siteCode", DbType.Int16, logsheet.site_code, true);
        AddValue(values, columns, "rek_num", "@requisition", DbType.String, logsheet.rek_num, true);
        AddValue(values, columns, "days_used", "@daysUsed", DbType.Int32, logsheet.days_used, true);
        AddValue(values, columns, "bund_num", "@bundleNumber", DbType.Int32, logsheet.bund_num, true);
        AddValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, DateTime.UtcNow, false);
        AddValue(values, columns, "modified_by_user_code", "@modifiedBy", DbType.Int32, UserIdOrNull(currentUserId), false);
        await ExecuteUpdateAsync(logsheet.log_code, values);
        return await GetByIdAsync(logsheet.log_code)
            ?? throw new KeyNotFoundException($"Logsheet not found with code: {logsheet.log_code}");
    }

    public async Task DeleteAsync(int logCode, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;

        if (columns.ContainsKey("is_deleted"))
        {
            var assignments = new List<string> { "[is_deleted] = @isDeleted" };
            AddParameter(command, "@isDeleted", DbType.Boolean, true);
            if (columns.ContainsKey("date_updated"))
            {
                assignments.Add("[date_updated] = @dateUpdated");
                AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
            }

            if (columns.ContainsKey("modified_by_user_code"))
            {
                assignments.Add("[modified_by_user_code] = @modifiedBy");
                AddParameter(command, "@modifiedBy", DbType.Int32, UserIdOrNull(currentUserId));
            }

            command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", assignments)} WHERE [log_code] = @logCode";
        }
        else
        {
            command.CommandText = $"DELETE FROM [dbo].[{TableName}] WHERE [log_code] = @logCode";
        }

        AddParameter(command, "@logCode", DbType.Int32, logCode);
        if (await command.ExecuteNonQueryAsync() == 0)
            throw new KeyNotFoundException($"Logsheet not found with code: {logCode}");
    }

    private async Task<IEnumerable<Logsheet>> QueryAsEnumerableAsync()
        => await QueryAsync();

    private async Task<List<Logsheet>> QueryAsync(string? predicate = null, Action<DbCommand>? configure = null)
    {
        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(predicate)) conditions.Add($"({predicate})");
        if (columns.ContainsKey("is_deleted")) conditions.Add("ISNULL(l.[is_deleted], 0) = 0");
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
        while (await reader.ReadAsync()) results.Add(Map(reader));
        return results;
    }

    private async Task ExecuteUpdateAsync(int logCode, IReadOnlyCollection<WriteValue> values)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [log_code] = @logCode";
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
        command.CommandText = "SELECT [COLUMN_NAME] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table";
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, TableName);

        var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) columns[reader.GetString(0)] = new ColumnInfo(reader.GetString(0));
        if (RequiredColumns.Any(column => !columns.ContainsKey(column)))
            throw new InvalidOperationException("The logsheet compatibility table is missing required legacy columns.");
        return columns;
    }

    private static string BuildProjection(IReadOnlyDictionary<string, ColumnInfo> columns)
        => string.Join(",\n                ",
        [
            "l.[log_code] AS [log_code]",
            "l.[vmf_code] AS [vmf_code]",
            "l.[start_odo] AS [start_odo]",
            "l.[end_odo] AS [end_odo]",
            "l.[month] AS [month]",
            "l.[site_code] AS [site_code]",
            "l.[rek_num] AS [rek_num]",
            "l.[days_used] AS [days_used]",
            "l.[bund_num] AS [bund_num]",
            "l.[trans_date] AS [trans_date]",
            "l.[driver_time] AS [driver_time]",
            "l.[FBS_comp] AS [FBS_comp]",
            "l.[user_access_code] AS [user_access_code]",
            "l.[trans_time] AS [trans_time]",
            "l.[department_code] AS [department_code]",
            "l.[contract_code] AS [contract_code]",
            "l.[journal_detail_code] AS [journal_detail_code]",
            "l.[parent_log_code] AS [parent_log_code]",
            "v.[fleet_number] AS [fleet_number]",
            "v.[registration_number] AS [registration_number]",
            "s.[description] AS [site_description]",
            DateCreatedExpression(columns) + " AS [date_created]",
            OptionalExpression(columns, "date_updated", "datetime2") + " AS [date_updated]",
            CreatedByExpression(columns) + " AS [created_by_user_code]",
            OptionalExpression(columns, "modified_by_user_code", "int") + " AS [modified_by_user_code]",
            OptionalExpression(columns, "is_deleted", "bit") + " AS [is_deleted]"
        ]);

    private static string DateCreatedExpression(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("date_created") ? "COALESCE(l.[date_created], l.[trans_date])" : "l.[trans_date]";

    private static string CreatedByExpression(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("created_by_user_code")
            ? "COALESCE(l.[created_by_user_code], l.[user_access_code])"
            : "l.[user_access_code]";

    private static string OptionalExpression(IReadOnlyDictionary<string, ColumnInfo> columns, string column, string sqlType)
        => columns.ContainsKey(column) ? $"l.[{column}]" : $"CAST(NULL AS {sqlType})";

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
                registration_number = registrationNumber
            },
            Site = new Site
            {
                Site_code = siteCode,
                description = siteDescription
            }
        };
    }

    private async Task<ConnectionScope> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();
        return new ConnectionScope(connection, shouldClose);
    }

    private DbTransaction? CurrentTransaction => _context.Database.CurrentTransaction?.GetDbTransaction();

    private static short? UserAccessCode(int value) => value is > 0 and <= short.MaxValue ? (short)value : null;
    private static int? UserIdOrNull(int value) => value > 0 ? value : null;

    private static void AddValue(ICollection<WriteValue> values, IReadOnlyDictionary<string, ColumnInfo> columns, string column, string parameter, DbType type, object? value, bool includeNull)
    {
        if (columns.ContainsKey(column) && (includeNull || value is not null)) values.Add(new WriteValue(column, parameter, type, value));
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values) AddParameter(command, value.Parameter, value.Type, value.Value);
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? ReadString(DbDataReader reader, string name)
        => reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToString(reader[name]);

    private static int? ReadInt(DbDataReader reader, string name)
        => reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToInt32(reader[name]);

    private static short? ReadShort(DbDataReader reader, string name)
        => reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToInt16(reader[name]);

    private static double ReadDouble(DbDataReader reader, string name)
        => reader.IsDBNull(reader.GetOrdinal(name)) ? 0 : Convert.ToDouble(reader[name]);

    private static double? ReadDoubleNullable(DbDataReader reader, string name)
        => reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToDouble(reader[name]);

    private static DateTime? ReadDate(DbDataReader reader, string name)
        => reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToDateTime(reader[name]);

    private static TimeSpan ReadTime(DbDataReader reader, string name)
    {
        if (reader.IsDBNull(reader.GetOrdinal(name))) return default;
        var value = reader[name];
        return value is TimeSpan time ? time : Convert.ToDateTime(value).TimeOfDay;
    }

    private static Guid ReadGuid(DbDataReader reader, string name)
    {
        if (reader.IsDBNull(reader.GetOrdinal(name))) return Guid.Empty;
        var value = reader[name];
        return value is Guid guid ? guid : Guid.TryParse(Convert.ToString(value), out var parsed) ? parsed : Guid.Empty;
    }

    private static bool ReadBool(DbDataReader reader, string name)
        => !reader.IsDBNull(reader.GetOrdinal(name)) && Convert.ToBoolean(reader[name]);

    private sealed record ColumnInfo(string Name);
    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

    private sealed class ConnectionScope(DbConnection connection, bool shouldClose) : IAsyncDisposable
    {
        public DbConnection Connection { get; } = connection;

        public async ValueTask DisposeAsync()
        {
            if (shouldClose) await Connection.CloseAsync();
        }
    }
}
