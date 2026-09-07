using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using MonitorEntity = FIS.Core.Domain.Entities.Monitor;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads and writes the legacy Monitor table without statically requiring the
/// audit columns introduced by the modern schema. The original table remains
/// the source of truth; modern audit fields are used when present.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility projections and all submitted values are parameters.")]
public class MonitorRepository : IMonitorRepository
{
    private const string TableName = "Monitor";
    private static readonly string[] RequiredColumns = ["monitor_code"];
    private readonly FisDbContext _context;

    public MonitorRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<MonitorEntity?> GetByIdAsync(short monitorCode)
        => (await QueryAsync(
            "m.[monitor_code] = @monitorCode",
            command => AddParameter(command, "@monitorCode", DbType.Int16, monitorCode)))
            .SingleOrDefault();

    public async Task<IEnumerable<MonitorEntity>> GetAllAsync() => await QueryAsync();

    public async Task<IEnumerable<MonitorEntity>> GetByVehicleAsync(int vmfCode)
        => await QueryAsync(
            "m.[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode));

    public async Task<IEnumerable<MonitorEntity>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        => await QueryAsync(
            "m.[Capture_dat] >= @startDate AND m.[Capture_dat] < @endDateExclusive",
            command =>
            {
                AddParameter(command, "@startDate", DbType.DateTime, startDate);
                AddParameter(command, "@endDateExclusive", DbType.DateTime, endDate);
            });

    public async Task<MonitorEntity> CreateAsync(MonitorEntity monitor, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        var columns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        var values = new List<WriteValue>();

        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, monitor.vmf_code, true);
        AddValue(values, columns, "Capture_dat", "@captureDate", DbType.DateTime, monitor.Capture_dat ?? now, true);
        AddValue(values, columns, "User_access_code", "@userAccessCode", DbType.Int16, UserAccessCode(currentUserId), true);
        AddValue(values, columns, "Inquiry_type", "@inquiryType", DbType.String, monitor.Inquiry_type, true);
        AddValue(values, columns, "Inquiry_Desc", "@inquiryDescription", DbType.String, monitor.Inquiry_Desc, true);
        AddValue(values, columns, "Driver_name", "@driverName", DbType.String, monitor.Driver_name, true);
        AddValue(values, columns, "Driver_persalno", "@driverPersalNo", DbType.String, monitor.Driver_persalno, true);
        AddValue(values, columns, "Driver_Site", "@driverSite", DbType.Int16, monitor.Driver_Site, true);
        AddValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, now, false);
        AddValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, now, false);
        AddValue(values, columns, "created_by_user_code", "@createdBy", DbType.Int32, UserIdOrNull(currentUserId), false);
        AddValue(values, columns, "modified_by_user_code", "@modifiedBy", DbType.Int32, UserIdOrNull(currentUserId), false);
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false, false);

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[monitor_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        var id = Convert.ToInt16(await command.ExecuteScalarAsync());
        return await GetByIdAsync(id) ?? throw new InvalidOperationException("Created monitor inquiry could not be read.");
    }

    public async Task<MonitorEntity> UpdateAsync(MonitorEntity monitor, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();

        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, monitor.vmf_code, true);
        AddValue(values, columns, "Capture_dat", "@captureDate", DbType.DateTime, monitor.Capture_dat, true);
        // Next payloads do not send this legacy ownership field. Leaving it
        // out prevents an edit from erasing the original user reference.
        AddValue(values, columns, "User_access_code", "@userAccessCode", DbType.Int16, monitor.User_access_code, false);
        AddValue(values, columns, "Inquiry_type", "@inquiryType", DbType.String, monitor.Inquiry_type, true);
        AddValue(values, columns, "Inquiry_Desc", "@inquiryDescription", DbType.String, monitor.Inquiry_Desc, true);
        AddValue(values, columns, "Driver_name", "@driverName", DbType.String, monitor.Driver_name, true);
        AddValue(values, columns, "Driver_persalno", "@driverPersalNo", DbType.String, monitor.Driver_persalno, true);
        AddValue(values, columns, "Driver_Site", "@driverSite", DbType.Int16, monitor.Driver_Site, true);
        AddValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, DateTime.UtcNow, false);
        AddValue(values, columns, "modified_by_user_code", "@modifiedBy", DbType.Int32, UserIdOrNull(currentUserId), false);

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [monitor_code] = @monitorCode";
        AddParameters(command, values);
        AddParameter(command, "@monitorCode", DbType.Int16, monitor.monitor_code);
        if (await command.ExecuteNonQueryAsync() == 0)
            throw new KeyNotFoundException($"Monitor inquiry not found with code: {monitor.monitor_code}");

        return await GetByIdAsync(monitor.monitor_code)
            ?? throw new KeyNotFoundException($"Monitor inquiry not found with code: {monitor.monitor_code}");
    }

    public async Task DeleteAsync(short monitorCode, int currentUserId)
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

            command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", assignments)} WHERE [monitor_code] = @monitorCode";
        }
        else
        {
            command.CommandText = $"DELETE FROM [dbo].[{TableName}] WHERE [monitor_code] = @monitorCode";
        }

        AddParameter(command, "@monitorCode", DbType.Int16, monitorCode);
        if (await command.ExecuteNonQueryAsync() == 0)
            throw new KeyNotFoundException($"Monitor inquiry not found with code: {monitorCode}");
    }

    private async Task<List<MonitorEntity>> QueryAsync(string? predicate = null, Action<DbCommand>? configure = null)
    {
        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = $"""
            SELECT
                {BuildProjection(columns)}
            FROM [dbo].[{TableName}] m
            LEFT JOIN [dbo].[vehicle_master] v ON v.[vmf_code] = m.[vmf_code]
            {(string.IsNullOrWhiteSpace(predicate) ? "" : $"WHERE {predicate}")}
            ORDER BY m.[Capture_dat] DESC, m.[monitor_code] DESC
            """;
        configure?.Invoke(command);

        var results = new List<MonitorEntity>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) results.Add(Map(reader));
        return results;
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
            throw new InvalidOperationException("The Monitor compatibility table is missing its required legacy columns.");
        return columns;
    }

    private static string BuildProjection(IReadOnlyDictionary<string, ColumnInfo> columns)
        => string.Join(",\n                ",
        [
            "m.[monitor_code] AS [monitor_code]",
            OptionalExpression(columns, "vmf_code", "int", "m") + " AS [vmf_code]",
            OptionalExpression(columns, "Capture_dat", "datetime2", "m") + " AS [Capture_dat]",
            OptionalExpression(columns, "User_access_code", "smallint", "m") + " AS [User_access_code]",
            OptionalExpression(columns, "Inquiry_type", "nvarchar(100)", "m") + " AS [Inquiry_type]",
            OptionalExpression(columns, "Inquiry_Desc", "nvarchar(max)", "m") + " AS [Inquiry_Desc]",
            OptionalExpression(columns, "Driver_name", "nvarchar(100)", "m") + " AS [Driver_name]",
            OptionalExpression(columns, "Driver_persalno", "nvarchar(50)", "m") + " AS [Driver_persalno]",
            OptionalExpression(columns, "Driver_Site", "smallint", "m") + " AS [Driver_Site]",
            DateCreatedExpression(columns) + " AS [date_created]",
            OptionalExpression(columns, "date_updated", "datetime2", "m") + " AS [date_updated]",
            CreatedByExpression(columns) + " AS [created_by_user_code]",
            OptionalExpression(columns, "modified_by_user_code", "int", "m") + " AS [modified_by_user_code]",
            OptionalExpression(columns, "is_deleted", "bit", "m") + " AS [is_deleted]",
            "v.[fleet_number] AS [fleet_number]",
            "v.[registration_number] AS [registration_number]"
        ]);

    private static string DateCreatedExpression(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("date_created")
            ? "COALESCE(m.[date_created], m.[Capture_dat])"
            : OptionalExpression(columns, "Capture_dat", "datetime2", "m");

    private static string CreatedByExpression(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("created_by_user_code")
            ? "COALESCE(m.[created_by_user_code], m.[User_access_code])"
            : OptionalExpression(columns, "User_access_code", "smallint", "m");

    private static string OptionalExpression(IReadOnlyDictionary<string, ColumnInfo> columns, string column, string sqlType, string alias)
        => columns.ContainsKey(column) ? $"{alias}.[{column}]" : $"CAST(NULL AS {sqlType})";

    private static MonitorEntity Map(DbDataReader reader)
    {
        var vmfCode = ReadInt(reader, "vmf_code");
        return new MonitorEntity
        {
            monitor_code = ReadShort(reader, "monitor_code") ?? 0,
            vmf_code = vmfCode,
            Capture_dat = ReadDate(reader, "Capture_dat"),
            User_access_code = ReadShort(reader, "User_access_code"),
            Inquiry_type = ReadString(reader, "Inquiry_type"),
            Inquiry_Desc = ReadString(reader, "Inquiry_Desc"),
            Driver_name = ReadString(reader, "Driver_name"),
            Driver_persalno = ReadString(reader, "Driver_persalno"),
            Driver_Site = ReadShort(reader, "Driver_Site"),
            date_created = ReadDate(reader, "date_created") ?? default,
            date_updated = ReadDate(reader, "date_updated"),
            created_by_user_code = ReadInt(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt(reader, "modified_by_user_code"),
            is_deleted = ReadBool(reader, "is_deleted"),
            Vehicle = new Vehicle
            {
                vmf_code = vmfCode ?? 0,
                fleet_number = ReadString(reader, "fleet_number"),
                registration_number = ReadString(reader, "registration_number")
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

    private static DateTime? ReadDate(DbDataReader reader, string name)
        => reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToDateTime(reader[name]);

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
