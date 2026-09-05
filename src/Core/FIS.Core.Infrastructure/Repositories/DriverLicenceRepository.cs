using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Drivers;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Persists driver licence descriptions against the original client table and
/// its expanded audit-aware shape. The legacy table has only the required
/// licence code and description columns, so optional columns are negotiated at
/// runtime instead of being statically selected by EF Core.
/// </summary>
public sealed class DriverLicenceRepository : IDriverLicenceRepository
{
    private const string TableName = "driver_licence";

    private static readonly string[] RequiredColumns =
    [
        "licence_code",
        "description"
    ];

    private static readonly string[] OptionalColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted"
    ];

    private readonly FisDbContext _context;

    public DriverLicenceRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<DriverLicence?> GetByIdAsync(short licenceCode)
        => (await QueryAsync(
            "[licence_code] = @licenceCode",
            command => AddParameter(command, "@licenceCode", DbType.Int16, licenceCode)))
            .SingleOrDefault();

    public async Task<DriverLicence?> GetByDescriptionAsync(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        return (await QueryAsync(
            "LOWER([description]) = @description",
            command => AddParameter(command, "@description", DbType.String, description.Trim().ToLowerInvariant())))
            .SingleOrDefault();
    }

    public async Task<IEnumerable<DriverLicence>> GetAllAsync()
        => await QueryAsync();

    public async Task<IEnumerable<DriverLicence>> SearchAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync();
        }

        return await QueryAsync(
            "LOWER(COALESCE([description], '')) LIKE @searchTerm",
            command => AddParameter(command, "@searchTerm", DbType.String, $"%{searchTerm.Trim().ToLowerInvariant()}%"));
    }

    public async Task<DriverLicenceDeleteCheck> GetDeleteCheckAsync(short licenceCode)
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
                    "SELECT COUNT(1) FROM [dbo].[model] WHERE [licence_code] = @licenceCode",
                    licenceCode,
                    transaction)
                : 0;

            return new DriverLicenceDeleteCheck(modelCount);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<DriverLicence> CreateAsync(DriverLicence driverLicence, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(driverLicence);

        var source = await GetSourceAsync();
        var now = DateTime.UtcNow;
        driverLicence.date_created = now;
        driverLicence.date_updated = now;
        driverLicence.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        driverLicence.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        driverLicence.is_deleted = false;

        driverLicence.licence_code = await ExecuteInsertAsync(source, BuildValues(driverLicence, source.Columns));
        return driverLicence;
    }

    public async Task UpdateAsync(DriverLicence driverLicence, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(driverLicence);

        var existing = await FindByIdAsync(driverLicence.licence_code)
            ?? throw new InvalidOperationException($"DriverLicence with licence_code {driverLicence.licence_code} not found");
        var now = DateTime.UtcNow;
        driverLicence.date_created = existing.Licence.date_created;
        driverLicence.created_by_user_code = existing.Licence.created_by_user_code;
        driverLicence.date_updated = now;
        driverLicence.modified_by_user_code = currentUserId > 0
            ? currentUserId
            : existing.Licence.modified_by_user_code;
        driverLicence.is_deleted = existing.Licence.is_deleted;

        await ExecuteUpdateAsync(
            existing.Source,
            driverLicence.licence_code,
            BuildValues(driverLicence, existing.Source.Columns, includeCreateAudit: false));
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The delete statement uses the fixed driver licence table and the licence code is parameterized.")]
    public async Task DeleteAsync(short licenceCode, int currentUserId)
    {
        var existing = await FindByIdAsync(licenceCode);
        if (existing is null)
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
            if (existing.Value.Source.Columns.Contains("is_deleted"))
            {
                var assignments = new List<string> { "[is_deleted] = @isDeleted" };
                var values = new List<WriteValue> { new("is_deleted", "@isDeleted", DbType.Boolean, true) };
                AddOptionalAuditValues(existing.Value.Source.Columns, assignments, values, currentUserId);
                command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", assignments)} WHERE [licence_code] = @licenceCode";
                AddParameters(command, values);
            }
            else
            {
                command.CommandText = $"DELETE FROM [dbo].[{TableName}] WHERE [licence_code] = @licenceCode";
            }

            AddParameter(command, "@licenceCode", DbType.Int16, licenceCode);
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

    private async Task<(DriverLicence Licence, TableSource Source)?> FindByIdAsync(short licenceCode)
    {
        var source = await GetSourceAsync();
        var result = (await QueryAsync(
            source,
            "[licence_code] = @licenceCode",
            command => AddParameter(command, "@licenceCode", DbType.Int16, licenceCode)))
            .SingleOrDefault();

        return result is null ? null : (result, source);
    }

    private async Task<TableSource> GetSourceAsync()
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
            if (!await TableExistsAsync(connection, "dbo", TableName, transaction))
            {
                throw new InvalidOperationException("The driver licence table is not available.");
            }

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
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
                columns.Add(Convert.ToString(reader.GetValue(0)) ?? string.Empty);
            }

            var missing = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException($"The driver licence table is missing required columns: {string.Join(", ", missing)}");
            }

            return new TableSource(columns);
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
        Justification = "The SELECT list and table name are composed only from fixed compatibility columns; predicates and values are parameterized.")]
    private async Task<List<DriverLicence>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null)
        => await QueryAsync(await GetSourceAsync(), predicate, configure);

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list and table name are composed only from fixed compatibility columns; predicates and values are parameterized.")]
    private async Task<List<DriverLicence>> QueryAsync(
        TableSource source,
        string? predicate = null,
        Action<DbCommand>? configure = null)
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
            var projection = RequiredColumns
                .Select(column => $"[{column}] AS [{column}]")
                .Concat(OptionalColumns.Select(column => GetOptionalProjection(source.Columns, column)))
                .ToArray();
            var conditions = new List<string> { GetNotDeletedFilter(source.Columns) };
            if (!string.IsNullOrWhiteSpace(predicate))
            {
                conditions.Add(predicate);
            }

            command.CommandText = $"SELECT {string.Join(", ", projection)} FROM [dbo].[{TableName}] WHERE {string.Join(" AND ", conditions)} ORDER BY [description], [licence_code]";
            configure?.Invoke(command);

            var results = new List<DriverLicence>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapDriverLicence(reader));
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
        Justification = "The INSERT statement uses the fixed driver licence table and parameterized values.")]
    private async Task<short> ExecuteInsertAsync(TableSource source, IReadOnlyList<WriteValue> values)
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
            command.CommandText = $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[licence_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
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
        Justification = "The UPDATE statement uses the fixed driver licence table and parameterized values.")]
    private async Task ExecuteUpdateAsync(TableSource source, short licenceCode, IReadOnlyList<WriteValue> values)
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
            command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [licence_code] = @licenceCode";
            AddParameters(command, values);
            AddParameter(command, "@licenceCode", DbType.Int16, licenceCode);
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

    private static List<WriteValue> BuildValues(
        DriverLicence driverLicence,
        IReadOnlySet<string> availableColumns,
        bool includeCreateAudit = true)
    {
        var values = new List<WriteValue>
        {
            new("description", "@description", DbType.String, driverLicence.description),
            new("date_updated", "@dateUpdated", DbType.DateTime2, driverLicence.date_updated),
            new("modified_by_user_code", "@modifiedByUserCode", DbType.Int32, driverLicence.modified_by_user_code)
        };

        if (includeCreateAudit)
        {
            values.Add(new("date_created", "@dateCreated", DbType.DateTime2, driverLicence.date_created));
            values.Add(new("created_by_user_code", "@createdByUserCode", DbType.Int32, driverLicence.created_by_user_code));
            values.Add(new("is_deleted", "@isDeleted", DbType.Boolean, false));
        }

        return values.Where(value => availableColumns.Contains(value.Column)).ToList();
    }

    private static void AddOptionalAuditValues(
        IReadOnlySet<string> availableColumns,
        ICollection<string> assignments,
        ICollection<WriteValue> values,
        int currentUserId)
    {
        if (availableColumns.Contains("date_updated"))
        {
            assignments.Add("[date_updated] = @dateUpdated");
            values.Add(new WriteValue("date_updated", "@dateUpdated", DbType.DateTime2, DateTime.UtcNow));
        }

        if (availableColumns.Contains("modified_by_user_code"))
        {
            assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
            values.Add(new WriteValue("modified_by_user_code", "@modifiedByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null));
        }
    }

    private static DriverLicence MapDriverLicence(DbDataReader reader)
        => new()
        {
            licence_code = ReadInt16(reader, "licence_code") ?? 0,
            description = ReadString(reader, "description"),
            date_created = ReadDateTime(reader, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code = ReadInt32(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted") ?? false
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

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The helper is called with a fixed repository statement and the licence code is parameterized.")]
    private static async Task<int> CountAsync(
        DbConnection connection,
        string sql,
        short licenceCode,
        DbTransaction? transaction)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameter(command, "@licenceCode", DbType.Int16, licenceCode);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string schema,
        string table,
        DbTransaction? transaction)
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

    private static string? ReadString(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToString(reader[column])?.TrimEnd();

    private static short? ReadInt16(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToInt16(reader[column]);

    private static int? ReadInt32(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToInt32(reader[column]);

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToDateTime(reader[column]);

    private static bool? ReadBoolean(DbDataReader reader, string column)
        => reader[column] is DBNull ? null : Convert.ToBoolean(reader[column]);

    private sealed record TableSource(HashSet<string> Columns);
    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
