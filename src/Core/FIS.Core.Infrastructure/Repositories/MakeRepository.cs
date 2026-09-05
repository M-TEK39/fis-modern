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
/// Persists the make table against both the original two-column schema and
/// the expanded schema. The legacy columns are always selected and written;
/// audit columns are negotiated at runtime.
/// </summary>
public sealed class MakeRepository : IMakeRepository
{
    private const string TableName = "make";

    private static readonly string[] RequiredColumns = ["make_code", "make_description"];

    private static readonly string[] OptionalColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted"
    ];

    private readonly FisDbContext _context;

    public MakeRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Make?> GetByIdAsync(short makeCode)
        => (await QueryAsync(
            "[make_code] = @makeCode",
            command => AddParameter(command, "@makeCode", DbType.Int16, makeCode)))
            .SingleOrDefault();

    public async Task<Make?> GetByNameAsync(string makeName)
    {
        if (string.IsNullOrWhiteSpace(makeName))
        {
            return null;
        }

        return (await QueryAsync(
            "LOWER([make_description]) = @makeDescription",
            command => AddParameter(command, "@makeDescription", DbType.String, makeName.Trim().ToLowerInvariant())))
            .SingleOrDefault();
    }

    public async Task<IEnumerable<Make>> GetAllMakesAsync()
        => await QueryAsync();

    public async Task<IEnumerable<Make>> SearchMakesAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllMakesAsync();
        }

        return await QueryAsync(
            "LOWER([make_description]) LIKE @searchTerm",
            command => AddParameter(command, "@searchTerm", DbType.String, $"%{searchTerm.Trim().ToLowerInvariant()}%"));
    }

    public async Task<MakeDeleteCheck> GetDeleteCheckAsync(short makeCode)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var modelCount = await TableExistsAsync(connection, "dbo", "model", transaction)
                ? await CountAsync(
                    connection,
                    "SELECT COUNT(1) FROM [dbo].[model] WHERE [make_code] = @makeCode",
                    makeCode,
                    transaction)
                : 0;
            return new MakeDeleteCheck(modelCount);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<Make> CreateAsync(Make make, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(make);

        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        var values = new List<WriteValue>
        {
            new("make_description", "@makeDescription", DbType.String, make.make_description)
        };
        AddCreateAuditValues(values, availableColumns, currentUserId, now);

        make.date_created = now;
        make.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        make.is_deleted = false;
        make.make_code = await ExecuteInsertAsync(values);
        return make;
    }

    public async Task<Make> UpdateAsync(Make make, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(make);

        var existing = await GetByIdAsync(make.make_code)
            ?? throw new InvalidOperationException($"Make with make_code {make.make_code} not found");
        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        var values = new List<WriteValue>
        {
            new("make_description", "@makeDescription", DbType.String, make.make_description)
        };
        AddUpdateAuditValues(values, availableColumns, currentUserId, now);

        await ExecuteUpdateAsync(make.make_code, values);
        make.date_created = existing.date_created;
        make.created_by_user_code = existing.created_by_user_code;
        make.date_updated = now;
        make.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        make.is_deleted = existing.is_deleted;
        return make;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The DELETE or soft-delete statement is selected from fixed compatibility branches and the make code is parameterized.")]
    public async Task DeleteAsync(short makeCode, int currentUserId)
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
                var assignments = new List<string> { "[is_deleted] = @isDeleted" };
                AddParameter(command, "@isDeleted", DbType.Boolean, true);

                if (availableColumns.Contains("date_updated"))
                {
                    assignments.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (availableColumns.Contains("modified_by_user_code"))
                {
                    assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                    AddParameter(command, "@modifiedByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
                }

                command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", assignments)} WHERE [make_code] = @makeCode";
            }
            else
            {
                command.CommandText = $"DELETE FROM [dbo].[{TableName}] WHERE [make_code] = @makeCode";
            }

            AddParameter(command, "@makeCode", DbType.Int16, makeCode);
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
        Justification = "The SELECT list is composed only from fixed legacy columns and allowlisted audit columns; predicates and values are parameterized.")]
    private async Task<List<Make>> QueryAsync(string? predicate = null, Action<DbCommand>? configure = null)
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
            var projection = RequiredColumns
                .Select(column => $"[{column}] AS [{column}]")
                .Concat(OptionalColumns.Select(column => GetOptionalProjection(availableColumns, column)))
                .ToArray();
            var conditions = new List<string> { GetNotDeletedFilter(availableColumns) };
            if (!string.IsNullOrWhiteSpace(predicate))
            {
                conditions.Add(predicate);
            }

            command.CommandText = $"SELECT {string.Join(", ", projection)} FROM [dbo].[{TableName}] WHERE {string.Join(" AND ", conditions)} ORDER BY [make_description], [make_code]";
            configure?.Invoke(command);

            var results = new List<Make>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapMake(reader, availableColumns));
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

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The INSERT statement is composed from fixed legacy and allowlisted audit columns; all values are parameters.")]
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
            command.CommandText = $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[make_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
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
        Justification = "The UPDATE statement is composed from fixed legacy and allowlisted audit columns; all values are parameters.")]
    private async Task ExecuteUpdateAsync(short makeCode, IReadOnlyList<WriteValue> values)
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
            command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [make_code] = @makeCode";
            AddParameters(command, values);
            AddParameter(command, "@makeCode", DbType.Int16, makeCode);
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

    private async Task<HashSet<string>> GetAvailableColumnsAsync()
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
            AddParameter(command, "@table", DbType.String, TableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }

            var missingColumns = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
            if (missingColumns.Length > 0)
            {
                throw new InvalidOperationException($"The required make compatibility columns are not available: {string.Join(", ", missingColumns)}");
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

    private static void AddCreateAuditValues(ICollection<WriteValue> values, IReadOnlySet<string> columns, int currentUserId, DateTime now)
    {
        AddOptionalValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, now);
        AddOptionalValue(values, columns, "created_by_user_code", "@createdByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
        AddOptionalValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);
    }

    private static void AddUpdateAuditValues(ICollection<WriteValue> values, IReadOnlySet<string> columns, int currentUserId, DateTime now)
    {
        AddOptionalValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, now);
        AddOptionalValue(values, columns, "modified_by_user_code", "@modifiedByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
    }

    private static void AddOptionalValue(ICollection<WriteValue> values, IReadOnlySet<string> columns, string column, string parameter, DbType type, object? value)
    {
        if (columns.Contains(column))
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

    private static Make MapMake(DbDataReader reader, IReadOnlySet<string> availableColumns)
        => new()
        {
            make_code = ReadInt16(reader, "make_code") ?? 0,
            make_description = ReadString(reader, "make_description") ?? string.Empty,
            date_created = ReadDateTimeIfAvailable(reader, availableColumns, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTimeIfAvailable(reader, availableColumns, "date_updated"),
            created_by_user_code = ReadInt32IfAvailable(reader, availableColumns, "created_by_user_code"),
            modified_by_user_code = ReadInt32IfAvailable(reader, availableColumns, "modified_by_user_code"),
            is_deleted = ReadBooleanIfAvailable(reader, availableColumns, "is_deleted") ?? false
        };

    private static string GetOptionalProjection(IReadOnlySet<string> columns, string column)
    {
        if (columns.Contains(column))
        {
            return $"[{column}] AS [{column}]";
        }

        var sqlType = column switch
        {
            "date_created" or "date_updated" => "datetime2",
            "created_by_user_code" or "modified_by_user_code" => "int",
            "is_deleted" => "bit",
            _ => "varchar(1)"
        };
        return $"CAST(NULL AS {sqlType}) AS [{column}]";
    }

    private static string GetNotDeletedFilter(IReadOnlySet<string> columns)
        => columns.Contains("is_deleted") ? "([is_deleted] = 0 OR [is_deleted] IS NULL)" : "1 = 1";

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The count query is a fixed repository statement and the make code is parameterized.")]
    private static async Task<int> CountAsync(DbConnection connection, string sql, short makeCode, DbTransaction? transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameter(command, "@makeCode", DbType.Int16, makeCode);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string schema, string table, DbTransaction? transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT COUNT(1)
            FROM [INFORMATION_SCHEMA].[TABLES]
            WHERE [TABLE_SCHEMA] = @schema
              AND [TABLE_NAME] = @table
            """;
        AddParameter(command, "@schema", DbType.String, schema);
        AddParameter(command, "@table", DbType.String, table);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static string? ReadString(DbDataReader reader, string column) => reader[column] is DBNull ? null : reader[column]?.ToString();

    private static short? ReadInt16(DbDataReader reader, string column) => reader[column] is DBNull ? null : Convert.ToInt16(reader[column]);

    private static bool? ReadBooleanIfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
        => columns.Contains(column) && reader[column] is not DBNull ? Convert.ToBoolean(reader[column]) : null;

    private static int? ReadInt32IfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
        => columns.Contains(column) && reader[column] is not DBNull ? Convert.ToInt32(reader[column]) : null;

    private static DateTime? ReadDateTimeIfAvailable(DbDataReader reader, IReadOnlySet<string> columns, string column)
        => columns.Contains(column) && reader[column] is not DBNull ? Convert.ToDateTime(reader[column]) : null;

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
