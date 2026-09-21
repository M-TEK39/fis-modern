using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads fin.Overhead against both the 2012 TariffParameterYear shape and the
/// later TariffParameterID shape. Updates go through fin.DEV_UPD_Overhead when
/// that procedure exists.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Schema, table, and column identifiers are fixed compatibility allowlists; values are parameterized."
)]
public class OverheadRepository : IOverheadRepository
{
    private const string SchemaName = "fin";
    private const string TableName = "Overhead";
    private static readonly string[] RequiredColumns =
    [
        "OverheadId",
        "OverheadDescription",
        "OverheadAmount",
        "OverheadTypeId",
    ];
    private static readonly string[] OptionalColumns =
    [
        "OverheadNote",
        "TariffParameterYear",
        "TariffParameterID",
        "CaptureDate",
        "ModifiedDate",
        "user_access_code",
        "user_access_name",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;

    public OverheadRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<Overhead>> GetByTariffParameterAsync(int tariffParameterId)
    {
        var columns = await GetAvailableColumnsAsync();
        var (predicate, configure) = await BuildTariffParameterPredicateAsync(
            columns,
            tariffParameterId
        );
        return await QueryAsync(predicate, configure, columns, "[OverheadTypeId], [OverheadId]");
    }

    public async Task<Overhead?> GetByTypeAsync(int tariffParameterId, int overheadTypeId)
    {
        var columns = await GetAvailableColumnsAsync();
        var (predicate, configure) = await BuildTariffParameterPredicateAsync(
            columns,
            tariffParameterId
        );
        predicate += " AND [OverheadTypeId] = @overheadTypeId";
        Action<DbCommand> typedConfigure = command =>
        {
            configure(command);
            AddParameter(command, "@overheadTypeId", DbType.Byte, (byte)overheadTypeId);
        };
        var rows = await QueryAsync(
            predicate,
            typedConfigure,
            columns,
            "[OverheadId]",
            firstOnly: true
        );
        return rows.SingleOrDefault();
    }

    public async Task<Overhead?> GetByIdAsync(int overheadId)
    {
        var columns = await GetAvailableColumnsAsync();
        var rows = await QueryAsync(
            $"{GetNotDeletedPredicate(columns)} AND [OverheadId] = @id",
            command => AddParameter(command, "@id", DbType.Int32, overheadId),
            columns,
            "[OverheadId]",
            firstOnly: true
        );
        return rows.SingleOrDefault();
    }

    public async Task<decimal> GetTotalAsync(int tariffParameterId)
    {
        var rows = await GetByTariffParameterAsync(tariffParameterId);
        return rows.Sum(row => row.OverheadAmount);
    }

    public async Task<Overhead> CreateAsync(Overhead overhead, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(overhead);
        return await UpsertAsync(overhead, currentUserId);
    }

    public async Task<Overhead> UpdateAsync(Overhead overhead, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(overhead);
        return await UpsertAsync(overhead, currentUserId);
    }

    public async Task DeleteAsync(int overheadId, int currentUserId)
    {
        var existing =
            await GetByIdAsync(overheadId)
            ?? throw new InvalidOperationException($"Overhead with OverheadId {overheadId} not found");
        existing.OverheadAmount = -1;
        await UpsertAsync(existing, currentUserId);
    }

    public async Task DeleteByTariffParameterAsync(int tariffParameterId)
    {
        var columns = await GetAvailableColumnsAsync();
        var (predicate, configure) = await BuildTariffParameterPredicateAsync(
            columns,
            tariffParameterId
        );
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = columns.Contains("is_deleted")
            ? $"UPDATE [{SchemaName}].[{TableName}] SET [is_deleted] = 1 WHERE {predicate}"
            : $"DELETE FROM [{SchemaName}].[{TableName}] WHERE {predicate}";
        configure(command);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Overhead> UpsertAsync(Overhead overhead, int currentUserId)
    {
        if (await ProcedureExistsAsync("DEV_UPD_Overhead"))
        {
            await ExecuteUpdateProcedureAsync(overhead, currentUserId);
            return overhead.OverheadAmount == -1m
                ? overhead
                : await GetByIdAsync(overhead.OverheadId)
                    ?? (
                        await GetByTypeAsync(overhead.TariffParameterID, overhead.OverheadTypeId)
                    )
                    ?? throw new InvalidOperationException(
                        "fin.DEV_UPD_Overhead completed but the overhead could not be read."
                    );
        }

        if (overhead.OverheadAmount == -1m && overhead.OverheadId > 0)
        {
            await DeleteDirectAsync(overhead.OverheadId);
            return overhead;
        }

        return overhead.OverheadId > 0
            ? await UpdateDirectAsync(overhead, currentUserId)
            : await InsertDirectAsync(overhead, currentUserId);
    }

    private async Task ExecuteUpdateProcedureAsync(Overhead overhead, int currentUserId)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "[fin].[DEV_UPD_Overhead]";
        AddParameter(command, "@OverheadId", DbType.Int32, overhead.OverheadId);
        AddParameter(command, "@OverheadDescription", DbType.String, overhead.OverheadDescription);
        AddParameter(command, "@OverheadAmount", DbType.Decimal, overhead.OverheadAmount);
        AddParameter(command, "@OverheadTypeId", DbType.Byte, overhead.OverheadTypeId);
        AddParameter(command, "@OverheadNote", DbType.String, overhead.OverheadNote);
        AddParameter(command, "@TariffParameterID", DbType.Int32, overhead.TariffParameterID);
        AddParameter(
            command,
            "@user_access_code",
            DbType.Int16,
            (short)Math.Clamp(currentUserId, short.MinValue, short.MaxValue)
        );
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Overhead> InsertDirectAsync(Overhead overhead, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
        var values = BuildWriteValues(overhead, currentUserId, columns, includeIdentity: false);
        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                "No compatible columns are available for inserting into fin.Overhead."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            INSERT INTO [{SchemaName}].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))})
            OUTPUT INSERTED.[OverheadId]
            VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
            """;
        AddParameters(command, values);
        overhead.OverheadId = Convert.ToInt32(await command.ExecuteScalarAsync());
        return await GetByIdAsync(overhead.OverheadId)
            ?? throw new InvalidOperationException("Created overhead could not be read.");
    }

    private async Task<Overhead> UpdateDirectAsync(Overhead overhead, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
        var values = BuildWriteValues(overhead, currentUserId, columns, includeIdentity: false);
        if (values.Count == 0)
        {
            throw new InvalidOperationException(
                "No compatible columns are available for updating fin.Overhead."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            UPDATE [{SchemaName}].[{TableName}]
            SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))}
            WHERE [OverheadId] = @id
            """;
        AddParameters(command, values);
        AddParameter(command, "@id", DbType.Int32, overhead.OverheadId);
        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new InvalidOperationException(
                $"Overhead with OverheadId {overhead.OverheadId} not found"
            );
        }

        return await GetByIdAsync(overhead.OverheadId)
            ?? throw new InvalidOperationException("Updated overhead could not be read.");
    }

    private async Task DeleteDirectAsync(int overheadId)
    {
        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = columns.Contains("is_deleted")
            ? """
                UPDATE [fin].[Overhead]
                SET [is_deleted] = 1
                WHERE [OverheadId] = @id
                """
            : """
                DELETE FROM [fin].[Overhead]
                WHERE [OverheadId] = @id
                """;
        AddParameter(command, "@id", DbType.Int32, overheadId);
        await command.ExecuteNonQueryAsync();
    }

    private static List<WriteValue> BuildWriteValues(
        Overhead overhead,
        int currentUserId,
        IReadOnlySet<string> columns,
        bool includeIdentity
    )
    {
        var values = new List<WriteValue>();
        if (includeIdentity)
            AddValue(values, columns, "OverheadId", "@overheadId", DbType.Int32, overhead.OverheadId);
        AddValue(values, columns, "OverheadDescription", "@description", DbType.String, overhead.OverheadDescription);
        AddValue(values, columns, "OverheadAmount", "@amount", DbType.Decimal, overhead.OverheadAmount);
        AddValue(values, columns, "OverheadTypeId", "@typeId", DbType.Byte, overhead.OverheadTypeId);
        AddValue(values, columns, "OverheadNote", "@note", DbType.String, overhead.OverheadNote);
        AddValue(values, columns, "TariffParameterID", "@tariffParameterId", DbType.Int32, overhead.TariffParameterID);
        AddValue(values, columns, "TariffParameterYear", "@tariffParameterYear", DbType.Int32, overhead.TariffParameterYear);
        AddValue(values, columns, "CaptureDate", "@captureDate", DbType.DateTime2, DateTime.Now);
        AddValue(values, columns, "ModifiedDate", "@modifiedDate", DbType.DateTime2, DateTime.Now);
        AddValue(
            values,
            columns,
            "user_access_code",
            "@userCode",
            DbType.Int16,
            (short)Math.Clamp(currentUserId, short.MinValue, short.MaxValue)
        );
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);
        return values;
    }

    private async Task<(string Predicate, Action<DbCommand> Configure)> BuildTariffParameterPredicateAsync(
        IReadOnlySet<string> columns,
        int tariffParameterId
    )
    {
        if (columns.Contains("TariffParameterID"))
        {
            return (
                $"{GetNotDeletedPredicate(columns)} AND [TariffParameterID] = @tariffParameterId",
                command => AddParameter(command, "@tariffParameterId", DbType.Int32, tariffParameterId)
            );
        }

        if (columns.Contains("TariffParameterYear"))
        {
            var year = await ResolveTariffParameterYearAsync(tariffParameterId);
            if (year.HasValue)
            {
                return (
                    $"{GetNotDeletedPredicate(columns)} AND [TariffParameterYear] = @tariffParameterYear",
                    command => AddParameter(command, "@tariffParameterYear", DbType.Int32, year.Value)
                );
            }

            return (
                $"{GetNotDeletedPredicate(columns)} AND [TariffParameterYear] = @tariffParameterYear",
                command => AddParameter(command, "@tariffParameterYear", DbType.Int32, tariffParameterId)
            );
        }

        return (GetNotDeletedPredicate(columns), _ => { });
    }

    private async Task<int?> ResolveTariffParameterYearAsync(int tariffParameterId)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT TOP (1) [TariffParameterYear]
            FROM [fin].[TariffParameter]
            WHERE [TariffParameterID] = @id
            """;
        AddParameter(command, "@id", DbType.Int32, tariffParameterId);
        try
        {
            var result = await command.ExecuteScalarAsync();
            return result is null or DBNull ? null : Convert.ToInt32(result);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<bool> ProcedureExistsAsync(string procedureName)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT COUNT(1)
            FROM [sys].[procedures] AS [p]
            INNER JOIN [sys].[schemas] AS [s] ON [s].[schema_id] = [p].[schema_id]
            WHERE [s].[name] = @schema AND [p].[name] = @name
            """;
        AddParameter(command, "@schema", DbType.String, SchemaName);
        AddParameter(command, "@name", DbType.String, procedureName);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private async Task<List<Overhead>> QueryAsync(
        string predicate,
        Action<DbCommand>? configure,
        IReadOnlySet<string> columns,
        string orderBy,
        bool firstOnly = false
    )
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText =
            $"SELECT {(firstOnly ? "TOP (1) " : string.Empty)}{BuildProjection(columns)} FROM [{SchemaName}].[{TableName}] WHERE {predicate} ORDER BY {orderBy}";
        configure?.Invoke(command);
        var result = new List<Overhead>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(MapOverhead(reader));
        return result;
    }

    private static string BuildProjection(IReadOnlySet<string> columns) =>
        string.Join(
            ", ",
            RequiredColumns
                .Concat(OptionalColumns)
                .Select(column =>
                    columns.Contains(column)
                        ? $"[{column}] AS [{column}]"
                        : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]"
                )
        );

    private static string GetNotDeletedPredicate(IReadOnlySet<string> columns) =>
        columns.Contains("is_deleted")
            ? "([is_deleted] = 0 OR [is_deleted] IS NULL)"
            : "1 = 1";

    private static string GetSqlType(string column) =>
        column switch
        {
            "OverheadId" or "TariffParameterYear" or "TariffParameterID"
                or "created_by_user_code" or "modified_by_user_code" => "int",
            "OverheadTypeId" => "tinyint",
            "user_access_code" => "smallint",
            "is_deleted" => "bit",
            "OverheadDescription" or "OverheadNote" or "user_access_name" => "varchar(1000)",
            "CaptureDate" or "ModifiedDate" or "date_created" or "date_updated" => "datetime2",
            _ => "decimal(18, 4)",
        };

    private static Overhead MapOverhead(DbDataReader reader) =>
        new()
        {
            OverheadId = Convert.ToInt32(reader["OverheadId"]),
            OverheadDescription = reader["OverheadDescription"] is DBNull
                ? string.Empty
                : reader["OverheadDescription"].ToString() ?? string.Empty,
            OverheadAmount = reader["OverheadAmount"] is DBNull
                ? 0m
                : Convert.ToDecimal(reader["OverheadAmount"]),
            OverheadTypeId = reader["OverheadTypeId"] is DBNull
                ? (byte)0
                : Convert.ToByte(reader["OverheadTypeId"]),
            OverheadNote = reader["OverheadNote"] is DBNull
                ? null
                : reader["OverheadNote"]?.ToString(),
            TariffParameterID = reader["TariffParameterID"] is DBNull
                ? 0
                : Convert.ToInt32(reader["TariffParameterID"]),
            TariffParameterYear = reader["TariffParameterYear"] is DBNull
                ? null
                : Convert.ToInt32(reader["TariffParameterYear"]),
            CaptureDate = reader["CaptureDate"] is DBNull
                ? default
                : Convert.ToDateTime(reader["CaptureDate"]),
            ModifiedDate = reader["ModifiedDate"] is DBNull
                ? null
                : Convert.ToDateTime(reader["ModifiedDate"]),
            user_access_code = reader["user_access_code"] is DBNull
                ? null
                : Convert.ToInt16(reader["user_access_code"]),
            user_access_name = reader["user_access_name"] is DBNull
                ? null
                : reader["user_access_name"]?.ToString(),
            date_created = reader["date_created"] is DBNull
                ? default
                : Convert.ToDateTime(reader["date_created"]),
            date_updated = reader["date_updated"] is DBNull
                ? null
                : Convert.ToDateTime(reader["date_updated"]),
            is_deleted = reader["is_deleted"] is not DBNull && Convert.ToBoolean(reader["is_deleted"]),
        };

    private async Task<HashSet<string>> GetAvailableColumnsAsync()
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText =
            "SELECT [COLUMN_NAME] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table";
        AddParameter(command, "@schema", DbType.String, SchemaName);
        AddParameter(command, "@table", DbType.String, TableName);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));

        if (columns.Count == 0)
        {
            throw new InvalidOperationException(
                "The legacy fin.Overhead table is unavailable. No modern overhead fallback was run."
            );
        }

        var missing = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"The legacy fin.Overhead table is missing required columns: {string.Join(", ", missing)}"
            );
        }

        return columns;
    }

    private async Task<ConnectionScope> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();
        return new ConnectionScope(connection, shouldClose);
    }

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        DbType dbType,
        object? value
    )
    {
        if (columns.Contains(column))
            values.Add(new WriteValue(column, parameter, dbType, value));
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values)
            AddParameter(command, value.Parameter, value.DbType, value.Value);
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private sealed record WriteValue(string Column, string Parameter, DbType DbType, object? Value);

    private sealed class ConnectionScope(DbConnection connection, bool shouldClose) : IAsyncDisposable
    {
        public DbConnection Connection => connection;

        public async ValueTask DisposeAsync()
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }
}
