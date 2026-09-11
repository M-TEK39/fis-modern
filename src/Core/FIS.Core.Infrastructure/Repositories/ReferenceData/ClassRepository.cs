using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Vehicles;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Persists vehicle classes against both the original class table and the
/// expanded schema. Optional columns are negotiated at runtime so the API can
/// run against an unexpanded client database without losing legacy fields.
/// </summary>
public sealed class ClassRepository : IClassRepository
{
    private const string TableName = "class";

    private static readonly string[] RequiredColumns =
    [
        "class_code",
        "description",
        "date_created",
        "date_updated",
        "modified_by_user_code",
    ];

    private static readonly string[] OptionalColumns =
    [
        "class_number",
        "bank_number",
        "months_life",
        "depreciation_percent",
        "odometer_life",
        "appreciate_percent",
        "replacement_cost",
        "created_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;

    public ClassRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Class?> GetByIdAsync(short classCode) =>
        (
            await QueryAsync(
                "[class_code] = @classCode",
                command => AddParameter(command, "@classCode", DbType.Int16, classCode)
            )
        ).SingleOrDefault();

    public async Task<IEnumerable<Class>> GetAllAsync() => await QueryAsync();

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The page queries use fixed compatibility columns, filters, ordering, and parameterized pagination values."
    )]
    public async Task<ClassPage> GetPageAsync(int page = 1, int pageSize = 24)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var availableColumns = await GetAvailableColumnsAsync();
        var notDeletedFilter = GetNotDeletedFilter(availableColumns);
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
                countCommand.CommandText =
                    $"SELECT COUNT(1) FROM [dbo].[{TableName}] WHERE {notDeletedFilter}";
                total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
            }

            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Min(page, totalPages);
            var skip = checked((long)(page - 1) * pageSize);

            var projection = RequiredColumns
                .Select(column => $"[{column}] AS [{column}]")
                .Concat(
                    OptionalColumns.Select(column =>
                        GetOptionalProjection(availableColumns, column)
                    )
                )
                .ToArray();
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                $"SELECT {string.Join(", ", projection)} FROM [dbo].[{TableName}] WHERE {notDeletedFilter} ORDER BY [description], [class_code] OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY";
            AddParameter(command, "@skip", DbType.Int64, skip);
            AddParameter(command, "@pageSize", DbType.Int32, pageSize);

            var items = new List<Class>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(MapClass(reader));
            }

            return new ClassPage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<Class>> SearchAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync();
        }

        var availableColumns = await GetAvailableColumnsAsync();
        var searchColumns = new[] { "description", "class_number", "bank_number" }
            .Where(availableColumns.Contains)
            .ToArray();

        var predicate = string.Join(
            " OR ",
            searchColumns.Select(column => $"LOWER(COALESCE([{column}], '')) LIKE @searchTerm")
        );
        return await QueryAsync(
            $"({predicate})",
            command =>
                AddParameter(
                    command,
                    "@searchTerm",
                    DbType.String,
                    $"%{searchTerm.Trim().ToLowerInvariant()}%"
                )
        );
    }

    public async Task<ClassDeleteCheck> GetDeleteCheckAsync(short classCode)
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
            var modelExists = await TableExistsAsync(connection, "dbo", "model", transaction);
            var modelCount = modelExists
                ? await CountAsync(
                    connection,
                    "SELECT COUNT(1) FROM [dbo].[model] WHERE [class_code] = @classCode",
                    classCode,
                    transaction
                )
                : 0;

            var vehicleCount =
                modelExists
                && await TableExistsAsync(connection, "dbo", "vehicle_master", transaction)
                    ? await CountAsync(
                        connection,
                        "SELECT COUNT(1) FROM [dbo].[vehicle_master] AS [vehicle] "
                            + "INNER JOIN [dbo].[model] AS [model] ON [model].[model_code] = [vehicle].[model_code] "
                            + "WHERE [model].[class_code] = @classCode",
                        classCode,
                        transaction
                    )
                    : 0;

            return new ClassDeleteCheck(modelCount, vehicleCount);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<Class> CreateAsync(Class classEntity, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(classEntity);

        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        classEntity.date_created = now;
        classEntity.date_updated = now;
        classEntity.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        classEntity.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        classEntity.is_deleted = false;

        var values = BuildValues(classEntity, availableColumns);
        classEntity.class_code = await ExecuteInsertAsync(values);
        return classEntity;
    }

    public async Task<Class> UpdateAsync(Class classEntity, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(classEntity);

        var existing =
            await GetByIdAsync(classEntity.class_code)
            ?? throw new InvalidOperationException(
                $"Class with class_code {classEntity.class_code} not found"
            );
        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;

        classEntity.date_created = existing.date_created;
        classEntity.created_by_user_code = existing.created_by_user_code;
        classEntity.date_updated = now;
        classEntity.modified_by_user_code =
            currentUserId > 0 ? currentUserId : existing.modified_by_user_code;
        classEntity.is_deleted = existing.is_deleted;

        var values = BuildValues(classEntity, availableColumns, includeCreateAudit: false);
        await ExecuteUpdateAsync(classEntity.class_code, values);
        return classEntity;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The delete statements use fixed compatibility branches and parameterized class identifiers."
    )]
    public async Task DeleteAsync(short classCode, int currentUserId)
    {
        var availableColumns = await GetAvailableColumnsAsync();
        IDbContextTransaction? transaction = null;
        if (_context.Database.CurrentTransaction is null)
        {
            transaction = await _context.Database.BeginTransactionAsync();
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var dbTransaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            if (await TableExistsAsync(connection, "dbo", "tariff", dbTransaction))
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "DELETE FROM [dbo].[tariff] WHERE [class_code] = @classCode",
                    classCode,
                    dbTransaction
                );
            }

            if (availableColumns.Contains("is_deleted"))
            {
                await using var command = connection.CreateCommand();
                command.Transaction = dbTransaction;
                command.CommandText =
                    "UPDATE [dbo].[class] SET [is_deleted] = @isDeleted, "
                    + "[date_updated] = @dateUpdated, "
                    + "[modified_by_user_code] = @modifiedByUserCode "
                    + "WHERE [class_code] = @classCode";
                AddParameter(command, "@isDeleted", DbType.Boolean, true);
                AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                AddParameter(
                    command,
                    "@modifiedByUserCode",
                    DbType.Int32,
                    currentUserId > 0 ? currentUserId : null
                );
                AddParameter(command, "@classCode", DbType.Int16, classCode);
                await command.ExecuteNonQueryAsync();
            }
            else
            {
                await ExecuteNonQueryAsync(
                    connection,
                    "DELETE FROM [dbo].[class] WHERE [class_code] = @classCode",
                    classCode,
                    dbTransaction
                );
            }

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
            if (shouldClose)
            {
                await connection.CloseAsync();
            }

            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static List<WriteValue> BuildValues(
        Class classEntity,
        IReadOnlySet<string> availableColumns,
        bool includeCreateAudit = true
    )
    {
        var values = new List<WriteValue>
        {
            new("description", "@description", DbType.String, classEntity.description),
            new("date_updated", "@dateUpdated", DbType.DateTime2, classEntity.date_updated),
            new(
                "modified_by_user_code",
                "@modifiedByUserCode",
                DbType.Int32,
                classEntity.modified_by_user_code
            ),
        };

        AddOptionalValue(
            values,
            availableColumns,
            "class_number",
            "@classNumber",
            DbType.String,
            classEntity.class_number
        );
        AddOptionalValue(
            values,
            availableColumns,
            "bank_number",
            "@bankNumber",
            DbType.String,
            classEntity.bank_number
        );
        AddOptionalValue(
            values,
            availableColumns,
            "months_life",
            "@monthsLife",
            DbType.Int16,
            classEntity.months_life
        );
        AddOptionalValue(
            values,
            availableColumns,
            "depreciation_percent",
            "@depreciationPercent",
            DbType.Decimal,
            classEntity.depreciation_percent
        );
        AddOptionalValue(
            values,
            availableColumns,
            "odometer_life",
            "@odometerLife",
            DbType.Decimal,
            classEntity.odometer_life
        );
        AddOptionalValue(
            values,
            availableColumns,
            "appreciate_percent",
            "@appreciatePercent",
            DbType.Int16,
            classEntity.appreciate_percent
        );
        AddOptionalValue(
            values,
            availableColumns,
            "replacement_cost",
            "@replacementCost",
            DbType.Decimal,
            classEntity.replacement_cost
        );

        if (includeCreateAudit)
        {
            AddOptionalValue(
                values,
                availableColumns,
                "date_created",
                "@dateCreated",
                DbType.DateTime2,
                classEntity.date_created
            );
            AddOptionalValue(
                values,
                availableColumns,
                "created_by_user_code",
                "@createdByUserCode",
                DbType.Int32,
                classEntity.created_by_user_code
            );
            AddOptionalValue(
                values,
                availableColumns,
                "is_deleted",
                "@isDeleted",
                DbType.Boolean,
                false
            );
        }

        return values.Where(value => availableColumns.Contains(value.Column)).ToList();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list is composed only from fixed legacy columns and allowlisted optional columns; predicates and values are parameterized."
    )]
    private async Task<List<Class>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
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
                .Concat(
                    OptionalColumns.Select(column =>
                        GetOptionalProjection(availableColumns, column)
                    )
                )
                .ToArray();
            var conditions = new List<string> { GetNotDeletedFilter(availableColumns) };
            if (!string.IsNullOrWhiteSpace(predicate))
            {
                conditions.Add(predicate);
            }

            command.CommandText =
                $"SELECT {string.Join(", ", projection)} FROM [dbo].[{TableName}] WHERE {string.Join(" AND ", conditions)} ORDER BY [description], [class_code]";
            configure?.Invoke(command);

            var results = new List<Class>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapClass(reader));
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
        Justification = "The INSERT statement is composed from fixed and allowlisted columns; all values are parameters."
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
                $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[class_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
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
        Justification = "The UPDATE statement is composed from fixed and allowlisted columns; all values are parameters."
    )]
    private async Task ExecuteUpdateAsync(short classCode, IReadOnlyList<WriteValue> values)
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
                $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [class_code] = @classCode";
            AddParameters(command, values);
            AddParameter(command, "@classCode", DbType.Int16, classCode);
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
                columns.Add(Convert.ToString(reader.GetValue(0)) ?? string.Empty);
            }

            var missing = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The legacy class table is missing required columns: {string.Join(", ", missing)}"
                );
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

    private static Class MapClass(DbDataReader reader) =>
        new()
        {
            class_code = ReadInt16(reader, "class_code") ?? 0,
            description = ReadString(reader, "description"),
            class_number = ReadString(reader, "class_number"),
            bank_number = ReadString(reader, "bank_number"),
            months_life = ReadInt16(reader, "months_life"),
            depreciation_percent = ReadDecimal(reader, "depreciation_percent"),
            odometer_life = ReadDecimal(reader, "odometer_life"),
            appreciate_percent = ReadInt16(reader, "appreciate_percent"),
            replacement_cost = ReadDecimal(reader, "replacement_cost"),
            date_created = ReadDateTime(reader, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code = ReadInt32(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted") ?? false,
        };

    private static string GetOptionalProjection(IReadOnlySet<string> columns, string column)
    {
        if (columns.Contains(column))
        {
            return $"[{column}] AS [{column}]";
        }

        var sqlType = column switch
        {
            "class_number" or "bank_number" => "varchar(30)",
            "months_life" or "appreciate_percent" => "smallint",
            "depreciation_percent" => "decimal(4,2)",
            "odometer_life" => "decimal(6,0)",
            "replacement_cost" => "decimal(8,0)",
            "created_by_user_code" => "int",
            "is_deleted" => "bit",
            _ => "varchar(1)",
        };
        return $"CAST(NULL AS {sqlType}) AS [{column}]";
    }

    private static string GetNotDeletedFilter(IReadOnlySet<string> columns) =>
        columns.Contains("is_deleted") ? "([is_deleted] = 0 OR [is_deleted] IS NULL)" : "1 = 1";

    private static void AddOptionalValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
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

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The helper is called only with fixed repository statements and the class code is parameterized."
    )]
    private static async Task ExecuteNonQueryAsync(
        DbConnection connection,
        string sql,
        short classCode,
        DbTransaction? transaction
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameter(command, "@classCode", DbType.Int16, classCode);
        await command.ExecuteNonQueryAsync();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The helper is called only with fixed repository statements and the class code is parameterized."
    )]
    private static async Task<int> CountAsync(
        DbConnection connection,
        string sql,
        short classCode,
        DbTransaction? transaction
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameter(command, "@classCode", DbType.Int16, classCode);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string schema,
        string table,
        DbTransaction? transaction
    )
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

    private static string? ReadString(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToString(reader[column])?.TrimEnd();

    private static short? ReadInt16(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToInt16(reader[column]);

    private static int? ReadInt32(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToInt32(reader[column]);

    private static decimal? ReadDecimal(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToDecimal(reader[column]);

    private static DateTime? ReadDateTime(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToDateTime(reader[column]);

    private static bool? ReadBoolean(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToBoolean(reader[column]);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
