using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility projections and all submitted values are parameters."
)]
public class TrackingRepository : ITrackingRepository
{
    private const string TableName = "Tracking";
    private static readonly string[] RequiredColumns = ["track_code"];
    private readonly FisDbContext _context;

    public TrackingRepository(FisDbContext context) =>
        _context = context ?? throw new ArgumentNullException(nameof(context));

    public async Task<Tracking?> GetByIdAsync(short trackCode) =>
        (
            await QueryAsync(
                "t.[track_code] = @trackCode",
                command => AddParameter(command, "@trackCode", DbType.Int16, trackCode)
            )
        ).SingleOrDefault();

    public async Task<IEnumerable<Tracking>> GetAllAsync() => await QueryAsync();

    public async Task<IEnumerable<Tracking>> GetByVehicleAsync(int vmfCode) =>
        await QueryAsync(
            "t.[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode)
        );

    public async Task<IEnumerable<Tracking>> GetActiveTrackingAsync() =>
        (await QueryAsync("t.[remove_date] IS NULL")).Where(item => !item.is_deleted);

    public async Task<Tracking> CreateAsync(Tracking tracking, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(tracking);
        var columns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        var values = new List<WriteValue>();
        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, tracking.vmf_code, true);
        AddValue(
            values,
            columns,
            "track_num",
            "@trackNum",
            DbType.String,
            tracking.track_num,
            true
        );
        AddValue(
            values,
            columns,
            "gg_previous",
            "@ggPrevious",
            DbType.String,
            tracking.gg_previous,
            true
        );
        AddValue(
            values,
            columns,
            "gg_follow",
            "@ggFollow",
            DbType.String,
            tracking.gg_follow,
            true
        );
        AddValue(
            values,
            columns,
            "install_date",
            "@installDate",
            DbType.DateTime,
            tracking.install_date,
            true
        );
        AddValue(
            values,
            columns,
            "remove_date",
            "@removeDate",
            DbType.DateTime,
            tracking.remove_date,
            true
        );
        AddValue(
            values,
            columns,
            "track_status",
            "@trackStatus",
            DbType.String,
            tracking.track_status,
            true
        );
        AddValue(
            values,
            columns,
            "track_type",
            "@trackType",
            DbType.String,
            tracking.track_type,
            true
        );
        AddValue(
            values,
            columns,
            "track_note",
            "@trackNote",
            DbType.String,
            tracking.track_note,
            true
        );
        AddValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, now, false);
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
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false, false);

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[track_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        var id = Convert.ToInt16(await command.ExecuteScalarAsync());
        return await GetByIdAsync(id)
            ?? throw new InvalidOperationException("Created tracking record could not be read.");
    }

    public async Task<Tracking> UpdateAsync(Tracking tracking, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(tracking);
        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();
        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, tracking.vmf_code, true);
        AddValue(
            values,
            columns,
            "track_num",
            "@trackNum",
            DbType.String,
            tracking.track_num,
            true
        );
        AddValue(
            values,
            columns,
            "gg_previous",
            "@ggPrevious",
            DbType.String,
            tracking.gg_previous,
            true
        );
        AddValue(
            values,
            columns,
            "gg_follow",
            "@ggFollow",
            DbType.String,
            tracking.gg_follow,
            true
        );
        AddValue(
            values,
            columns,
            "install_date",
            "@installDate",
            DbType.DateTime,
            tracking.install_date,
            true
        );
        AddValue(
            values,
            columns,
            "remove_date",
            "@removeDate",
            DbType.DateTime,
            tracking.remove_date,
            true
        );
        AddValue(
            values,
            columns,
            "track_status",
            "@trackStatus",
            DbType.String,
            tracking.track_status,
            true
        );
        AddValue(
            values,
            columns,
            "track_type",
            "@trackType",
            DbType.String,
            tracking.track_type,
            true
        );
        AddValue(
            values,
            columns,
            "track_note",
            "@trackNote",
            DbType.String,
            tracking.track_note,
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

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [track_code] = @trackCode";
        AddParameters(command, values);
        AddParameter(command, "@trackCode", DbType.Int16, tracking.track_code);
        if (await command.ExecuteNonQueryAsync() == 0)
            throw new KeyNotFoundException(
                $"Tracking record not found with code: {tracking.track_code}"
            );
        return await GetByIdAsync(tracking.track_code)
            ?? throw new KeyNotFoundException(
                $"Tracking record not found with code: {tracking.track_code}"
            );
    }

    public async Task DeleteAsync(short trackCode, int currentUserId)
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
            command.CommandText =
                $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", assignments)} WHERE [track_code] = @trackCode";
        }
        else
            command.CommandText =
                $"DELETE FROM [dbo].[{TableName}] WHERE [track_code] = @trackCode";
        AddParameter(command, "@trackCode", DbType.Int16, trackCode);
        if (await command.ExecuteNonQueryAsync() == 0)
            throw new KeyNotFoundException($"Tracking record not found with code: {trackCode}");
    }

    private async Task<List<Tracking>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = $"""
            SELECT
                {BuildProjection(columns)}
            FROM [dbo].[{TableName}] t
            LEFT JOIN [dbo].[vehicle_master] v ON v.[vmf_code] = t.[vmf_code]
            {(string.IsNullOrWhiteSpace(predicate) ? "" : $"WHERE {predicate}")}
            ORDER BY t.[install_date] DESC, t.[track_code] DESC
            """;
        configure?.Invoke(command);
        var results = new List<Tracking>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(Map(reader));
        return results;
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
                "The Tracking compatibility table is missing its required legacy columns."
            );
        return columns;
    }

    private static string BuildProjection(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        string.Join(
            ",\n                ",
            [
                "t.[track_code] AS [track_code]",
                OptionalExpression(columns, "vmf_code", "int") + " AS [vmf_code]",
                OptionalExpression(columns, "track_num", "nvarchar(50)") + " AS [track_num]",
                OptionalExpression(columns, "gg_previous", "nvarchar(50)") + " AS [gg_previous]",
                OptionalExpression(columns, "gg_follow", "nvarchar(50)") + " AS [gg_follow]",
                OptionalExpression(columns, "install_date", "datetime2") + " AS [install_date]",
                OptionalExpression(columns, "remove_date", "datetime2") + " AS [remove_date]",
                OptionalExpression(columns, "track_status", "nvarchar(100)") + " AS [track_status]",
                OptionalExpression(columns, "track_type", "nvarchar(100)") + " AS [track_type]",
                OptionalExpression(columns, "track_note", "nvarchar(max)") + " AS [track_note]",
                DateCreatedExpression(columns) + " AS [date_created]",
                OptionalExpression(columns, "date_updated", "datetime2") + " AS [date_updated]",
                CreatedByExpression(columns) + " AS [created_by_user_code]",
                OptionalExpression(columns, "modified_by_user_code", "int")
                    + " AS [modified_by_user_code]",
                OptionalExpression(columns, "is_deleted", "bit") + " AS [is_deleted]",
                "v.[fleet_number] AS [fleet_number]",
                "v.[registration_number] AS [registration_number]",
            ]
        );

    private static string OptionalExpression(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string sqlType
    ) => columns.ContainsKey(column) ? $"t.[{column}]" : $"CAST(NULL AS {sqlType})";

    private static string DateCreatedExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("date_created")
            ? "COALESCE(t.[date_created], t.[install_date])"
            : OptionalExpression(columns, "install_date", "datetime2");

    private static string CreatedByExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("created_by_user_code")
            ? "t.[created_by_user_code]"
            : "CAST(NULL AS int)";

    private static Tracking Map(DbDataReader reader)
    {
        var vmfCode = ReadInt(reader, "vmf_code");
        return new Tracking
        {
            track_code = ReadShort(reader, "track_code") ?? 0,
            vmf_code = vmfCode,
            track_num = ReadString(reader, "track_num"),
            gg_previous = ReadString(reader, "gg_previous"),
            gg_follow = ReadString(reader, "gg_follow"),
            install_date = ReadDate(reader, "install_date"),
            remove_date = ReadDate(reader, "remove_date"),
            track_status = ReadString(reader, "track_status"),
            track_type = ReadString(reader, "track_type"),
            track_note = ReadString(reader, "track_note"),
            date_created = ReadDate(reader, "date_created") ?? default,
            date_updated = ReadDate(reader, "date_updated"),
            created_by_user_code = ReadInt(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt(reader, "modified_by_user_code"),
            is_deleted = ReadBool(reader, "is_deleted"),
            Vehicle = new Vehicle
            {
                vmf_code = vmfCode ?? 0,
                fleet_number = ReadString(reader, "fleet_number"),
                registration_number = ReadString(reader, "registration_number"),
            },
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

    private static DateTime? ReadDate(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToDateTime(reader[name]);

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
