using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using WwMerchant = FIS.Core.Domain.Entities.WorkshopEntities.WwMerchant;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads and writes the workshop-specific legacy merchant table. Its audit
/// columns are optional because the client database contains only the
/// wwmerchant business columns.
/// </summary>
public sealed class WorkshopMerchantRepository : IWorkshopMerchantRepository
{
    private const string TableName = "wwmerchant";
    private static readonly string[] BusinessColumns =
    [
        "wwmerch_code",
        "wwmerch_name",
        "wwmerch_tel",
        "wwmerch_fax",
        "wwmerch_email",
    ];
    private static readonly string[] OptionalAuditColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];
    private readonly FisDbContext _context;

    public WorkshopMerchantRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<WwMerchant?> GetByIdAsync(int merchantCode) =>
        (
            await QueryAsync(
                "[wwmerch_code] = @merchantCode",
                command => AddParameter(command, "@merchantCode", DbType.Int32, merchantCode)
            )
        ).SingleOrDefault();

    public async Task<IEnumerable<WwMerchant>> GetAllAsync() => await QueryAsync();

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The table, columns, ordering, and parameter names are fixed. The merchant search and pagination values are always parameters."
    )]
    public async Task<WorkshopMerchantPage> GetPageAsync(WorkshopMerchantPageQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var requestedPage = Math.Max(1, query.Page);
        var search = query.Search?.Trim();
        if (string.IsNullOrEmpty(search))
        {
            search = null;
        }
        else if (search.Length > 40)
        {
            search = search[..40];
        }

        var columns = await GetAvailableColumnsAsync();
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            var conditions = new List<string> { GetActiveFilter(columns) };
            if (search is not null)
                conditions.Add("[wwmerch_name] LIKE @search ESCAPE '~'");
            var whereClause = string.Join(" AND ", conditions);

            await using var countCommand = connection.CreateCommand();
            countCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            countCommand.CommandText =
                $"SELECT COUNT(1) FROM [dbo].[{TableName}] WHERE {whereClause}";
            if (search is not null)
                AddParameter(countCommand, "@search", DbType.String, ContainsPattern(search));
            var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = Math.Min(requestedPage, totalPages);
            var skip = checked((long)(page - 1) * pageSize);

            await using var dataCommand = connection.CreateCommand();
            dataCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            dataCommand.CommandText = $"""
                SELECT {string.Join(
                    ", ",
                    BusinessColumns.Concat(OptionalAuditColumns).Select(column =>
                        GetProjection(columns, column)
                    )
                )}
                FROM [dbo].[{TableName}]
                WHERE {whereClause}
                ORDER BY [wwmerch_name], [wwmerch_code]
                OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
                """;
            if (search is not null)
                AddParameter(dataCommand, "@search", DbType.String, ContainsPattern(search));
            AddParameter(dataCommand, "@skip", DbType.Int64, skip);
            AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

            var items = new List<WwMerchant>();
            await using var reader = await dataCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                items.Add(MapMerchant(reader, columns));
            return new WorkshopMerchantPage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    public async Task<WwMerchant> CreateAsync(WwMerchant merchant, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(merchant);
        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();
        AddValue(
            values,
            columns,
            "wwmerch_name",
            "@merchantName",
            DbType.String,
            merchant.wwmerch_name
        );
        AddValue(
            values,
            columns,
            "wwmerch_tel",
            "@merchantTel",
            DbType.String,
            merchant.wwmerch_tel
        );
        AddValue(
            values,
            columns,
            "wwmerch_fax",
            "@merchantFax",
            DbType.String,
            merchant.wwmerch_fax
        );
        AddValue(
            values,
            columns,
            "wwmerch_email",
            "@merchantEmail",
            DbType.String,
            merchant.wwmerch_email
        );
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
            currentUserId > 0 ? currentUserId : null
        );
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        var code = await ExecuteInsertAsync(values);
        return await GetByIdAsync(code)
            ?? throw new InvalidOperationException(
                $"Workshop merchant {code} could not be read after creation."
            );
    }

    public async Task<WwMerchant> UpdateAsync(WwMerchant merchant, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(merchant);
        var existing =
            await GetByIdAsync(merchant.wwmerch_code)
            ?? throw new KeyNotFoundException(
                $"Workshop merchant {merchant.wwmerch_code} not found"
            );
        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();
        AddValue(
            values,
            columns,
            "wwmerch_name",
            "@merchantName",
            DbType.String,
            merchant.wwmerch_name,
            includeNull: true
        );
        AddValue(
            values,
            columns,
            "wwmerch_tel",
            "@merchantTel",
            DbType.String,
            merchant.wwmerch_tel,
            includeNull: true
        );
        AddValue(
            values,
            columns,
            "wwmerch_fax",
            "@merchantFax",
            DbType.String,
            merchant.wwmerch_fax,
            includeNull: true
        );
        AddValue(
            values,
            columns,
            "wwmerch_email",
            "@merchantEmail",
            DbType.String,
            merchant.wwmerch_email,
            includeNull: true
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
            currentUserId > 0 ? currentUserId : null
        );

        await ExecuteUpdateAsync(merchant.wwmerch_code, values, columns);
        return await GetByIdAsync(merchant.wwmerch_code) ?? existing;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "DeleteAsync selects between fixed legacy SQL statements and uses a parameter for the record identifier."
    )]
    public async Task DeleteAsync(int merchantCode, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

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
                    WHERE [wwmerch_code] = @merchantCode
                      AND {GetActiveFilter(columns)}
                    """;
            }
            else
            {
                command.CommandText = $"""
                    DELETE FROM [dbo].[{TableName}]
                    WHERE [wwmerch_code] = @merchantCode
                    """;
            }

            AddParameter(command, "@merchantCode", DbType.Int32, merchantCode);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list and filters use fixed legacy columns and parameterized values."
    )]
    private async Task<List<WwMerchant>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var columns = await GetAvailableColumnsAsync();
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var conditions = new List<string>();
            if (!string.IsNullOrWhiteSpace(predicate))
                conditions.Add(predicate);
            conditions.Add(GetActiveFilter(columns));
            command.CommandText = $"""
                SELECT {string.Join(
                    ", ",
                    BusinessColumns.Concat(OptionalAuditColumns).Select(column =>
                        GetProjection(columns, column)
                    )
                )}
                FROM [dbo].[{TableName}]
                WHERE {string.Join(" AND ", conditions)}
                ORDER BY [wwmerch_name]
                """;
            configure?.Invoke(command);

            var results = new List<WwMerchant>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                results.Add(MapMerchant(reader, columns));
            return results;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The INSERT statement is composed from the fixed legacy column allowlist and every value is parameterized."
    )]
    private async Task<int> ExecuteInsertAsync(IReadOnlyList<WriteValue> values)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = $"""
                INSERT INTO [dbo].[{TableName}] ({string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}]")
                )})
                OUTPUT INSERTED.[wwmerch_code]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
            AddParameters(command, values);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The UPDATE statement is composed from the fixed legacy column allowlist and every value is parameterized."
    )]
    private async Task ExecuteUpdateAsync(
        int merchantCode,
        IReadOnlyList<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();
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
                WHERE [wwmerch_code] = @merchantCode
                  AND {GetActiveFilter(columns)}
                """;
            AddParameters(command, values);
            AddParameter(command, "@merchantCode", DbType.Int32, merchantCode);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<Dictionary<string, ColumnInfo>> GetAvailableColumnsAsync()
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
                SELECT [COLUMN_NAME], [DATA_TYPE]
                FROM [INFORMATION_SCHEMA].[COLUMNS]
                WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, TableName);
            var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                columns[reader.GetString(0)] = new ColumnInfo(
                    reader.GetString(0),
                    reader.GetString(1)
                );
            foreach (var required in BusinessColumns.Where(column => !columns.ContainsKey(column)))
                throw new InvalidOperationException(
                    $"The required workshop merchant column {required} is not available."
                );
            return columns;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private static WwMerchant MapMerchant(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns
    ) =>
        new()
        {
            wwmerch_code = ReadInt32(reader, "wwmerch_code") ?? 0,
            wwmerch_name = ReadString(reader, "wwmerch_name"),
            wwmerch_tel = ReadString(reader, "wwmerch_tel"),
            wwmerch_fax = ReadString(reader, "wwmerch_fax"),
            wwmerch_email = ReadString(reader, "wwmerch_email"),
            date_created =
                ReadDateTimeIfAvailable(reader, columns, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTimeIfAvailable(reader, columns, "date_updated"),
            created_by_user_code = ReadInt32IfAvailable(reader, columns, "created_by_user_code"),
            modified_by_user_code = ReadInt32IfAvailable(reader, columns, "modified_by_user_code"),
            is_deleted = ReadBooleanIfAvailable(reader, columns, "is_deleted") ?? false,
        };

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value,
        bool includeNull = false
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

    private static string ContainsPattern(string value) =>
        $"%{value.Replace("~", "~~", StringComparison.Ordinal).Replace("%", "~%", StringComparison.Ordinal).Replace("_", "~_", StringComparison.Ordinal)}%";

    private static string GetActiveFilter(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

    private static string GetProjection(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) =>
        columns.ContainsKey(column)
            ? $"[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static string GetSqlType(string column) =>
        column switch
        {
            "wwmerch_code" => "int",
            "date_created" or "date_updated" => "datetime2",
            "created_by_user_code" or "modified_by_user_code" => "int",
            "is_deleted" => "bit",
            _ => "varchar(1)",
        };

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal))?.TrimEnd();
    }

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static int? ReadInt32IfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) => columns.ContainsKey(column) ? ReadInt32(reader, column) : null;

    private static DateTime? ReadDateTimeIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    )
    {
        if (!columns.ContainsKey(column))
            return null;
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static bool? ReadBooleanIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    )
    {
        if (!columns.ContainsKey(column))
            return null;
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private sealed record ColumnInfo(string Name, string DataType);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
