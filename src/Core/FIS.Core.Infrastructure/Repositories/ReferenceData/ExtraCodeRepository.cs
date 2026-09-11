using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Persists extra codes against the original extra_codes table while
/// negotiating expanded audit and soft-delete columns at runtime.
/// </summary>
public sealed class ExtraCodeRepository : IExtraCodeRepository
{
    private const string TableName = "extra_codes";

    private static readonly string[] RequiredColumns =
    [
        "extra_code",
        "extra_description",
        "category_type_code",
        "specific",
        "Additional",
    ];

    private static readonly string[] OptionalColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;

    public ExtraCodeRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ExtraCode?> GetByIdAsync(short extraCode) =>
        (
            await QueryAsync(
                "[extra_code] = @extraCode",
                command => AddParameter(command, "@extraCode", DbType.Int16, extraCode)
            )
        ).SingleOrDefault();

    public async Task<ExtraCode?> GetByDescriptionAsync(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        return (
            await QueryAsync(
                "LOWER([extra_description]) = @description",
                command =>
                    AddParameter(
                        command,
                        "@description",
                        DbType.String,
                        description.Trim().ToLowerInvariant()
                    )
            )
        ).SingleOrDefault();
    }

    public async Task<IEnumerable<ExtraCode>> GetAllAsync() => await QueryAsync();

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The page query uses fixed compatibility columns and a parameterized search term and offset."
    )]
    public async Task<ExtraCodePage> GetPageAsync(ExtraCodePageQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var requestedPage = Math.Max(1, query.Page);
        var searchTerm = query.SearchTerm?.Trim() ?? string.Empty;
        var availableColumns = await GetAvailableColumnsAsync();
        var projection = RequiredColumns
            .Select(column => $"[{column}] AS [{column}]")
            .Concat(
                OptionalColumns.Select(column => GetOptionalProjection(availableColumns, column))
            )
            .ToArray();
        var conditions = new List<string> { GetNotDeletedFilter(availableColumns) };
        if (searchTerm.Length > 0)
        {
            conditions.Add("LOWER(COALESCE([extra_description], '')) LIKE @searchTerm");
        }

        var whereClause = string.Join(" AND ", conditions);
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            await using var countCommand = connection.CreateCommand();
            countCommand.Transaction = transaction;
            countCommand.CommandText = $"""
                SELECT COUNT(1)
                FROM [dbo].[{TableName}]
                WHERE {whereClause}
                """;
            if (searchTerm.Length > 0)
            {
                AddParameter(
                    countCommand,
                    "@searchTerm",
                    DbType.String,
                    $"%{searchTerm.ToLowerInvariant()}%"
                );
            }

            var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = Math.Min(requestedPage, totalPages);
            var offset = checked((long)(page - 1) * pageSize);

            await using var dataCommand = connection.CreateCommand();
            dataCommand.Transaction = transaction;
            dataCommand.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{TableName}]
                WHERE {whereClause}
                ORDER BY [extra_description], [extra_code]
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY
                """;
            if (searchTerm.Length > 0)
            {
                AddParameter(
                    dataCommand,
                    "@searchTerm",
                    DbType.String,
                    $"%{searchTerm.ToLowerInvariant()}%"
                );
            }

            AddParameter(dataCommand, "@offset", DbType.Int64, offset);
            AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

            var items = new List<ExtraCode>();
            await using var reader = await dataCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(MapExtraCode(reader));
            }

            return new ExtraCodePage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<ExtraCode>> SearchAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync();
        }

        return await QueryAsync(
            "LOWER(COALESCE([extra_description], '')) LIKE @searchTerm",
            command =>
                AddParameter(
                    command,
                    "@searchTerm",
                    DbType.String,
                    $"%{searchTerm.Trim().ToLowerInvariant()}%"
                )
        );
    }

    public async Task<IEnumerable<ExtraCode>> GetByCategoryAsync(int categoryTypeCode) =>
        await QueryAsync(
            "[category_type_code] = @categoryTypeCode",
            command => AddParameter(command, "@categoryTypeCode", DbType.Int32, categoryTypeCode)
        );

    public async Task<ExtraCodeDeleteCheck> GetDeleteCheckAsync(short extraCode)
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
            var extrasColumns = await GetTableColumnsAsync(
                connection,
                "dbo",
                "extras",
                transaction
            );
            var vehicleColumns = await GetTableColumnsAsync(
                connection,
                "dbo",
                "vehicle_master",
                transaction
            );
            var canInspectDependencies =
                new[] { "extra_code", "vmf_code" }.All(extrasColumns.Contains)
                && new[] { "vmf_code", "fleet_number" }.All(vehicleColumns.Contains);

            if (!canInspectDependencies)
            {
                return new ExtraCodeDeleteCheck(0, [], false);
            }

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            var activeExtraPredicate = extrasColumns.Contains("is_deleted")
                ? " AND ([extra].[is_deleted] = 0 OR [extra].[is_deleted] IS NULL)"
                : string.Empty;
            command.CommandText = $"""
                SELECT DISTINCT [vehicle].[fleet_number]
                FROM [dbo].[extras] AS [extra]
                INNER JOIN [dbo].[vehicle_master] AS [vehicle]
                    ON [vehicle].[vmf_code] = [extra].[vmf_code]
                WHERE [extra].[extra_code] = @extraCode{activeExtraPredicate}
                ORDER BY [vehicle].[fleet_number]
                """;
            AddParameter(command, "@extraCode", DbType.Int16, extraCode);

            var fleetNumbers = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var fleetNumber = ReadString(reader, "fleet_number");
                if (!string.IsNullOrWhiteSpace(fleetNumber))
                {
                    fleetNumbers.Add(fleetNumber);
                }
            }

            return new ExtraCodeDeleteCheck(fleetNumbers.Count, fleetNumbers);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<ExtraCode> CreateAsync(ExtraCode extraCode, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(extraCode);

        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        extraCode.date_created = now;
        extraCode.date_updated = now;
        extraCode.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        extraCode.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        extraCode.is_deleted = false;

        extraCode.extra_code = await ExecuteInsertAsync(BuildValues(extraCode, availableColumns));
        return extraCode;
    }

    public async Task UpdateAsync(ExtraCode extraCode, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(extraCode);

        var existing =
            await FindByIdAsync(extraCode.extra_code)
            ?? throw new InvalidOperationException(
                $"ExtraCode with extra_code {extraCode.extra_code} not found"
            );
        var now = DateTime.UtcNow;

        extraCode.date_created = existing.Code.date_created;
        extraCode.created_by_user_code = existing.Code.created_by_user_code;
        extraCode.date_updated = now;
        extraCode.modified_by_user_code =
            currentUserId > 0 ? currentUserId : existing.Code.modified_by_user_code;
        extraCode.is_deleted = existing.Code.is_deleted;

        await ExecuteUpdateAsync(
            existing.Columns,
            extraCode.extra_code,
            BuildValues(extraCode, existing.Columns, includeCreateAudit: false)
        );
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The delete statement uses the fixed extra_codes table and a parameterized extra code."
    )]
    public async Task DeleteAsync(short extraCode, int currentUserId)
    {
        var existing = await FindByIdAsync(extraCode);
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
            var availableColumns = existing.Value.Columns;
            if (availableColumns.Contains("is_deleted"))
            {
                var updates = new List<string> { "[is_deleted] = @isDeleted" };
                AddParameter(command, "@isDeleted", DbType.Boolean, true);
                if (availableColumns.Contains("date_updated"))
                {
                    updates.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (availableColumns.Contains("modified_by_user_code"))
                {
                    updates.Add("[modified_by_user_code] = @modifiedByUserCode");
                    AddParameter(
                        command,
                        "@modifiedByUserCode",
                        DbType.Int32,
                        currentUserId > 0 ? currentUserId : null
                    );
                }

                command.CommandText =
                    $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", updates)} WHERE [extra_code] = @extraCode";
            }
            else
            {
                command.CommandText =
                    $"DELETE FROM [dbo].[{TableName}] WHERE [extra_code] = @extraCode";
            }

            AddParameter(command, "@extraCode", DbType.Int16, extraCode);
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

    private async Task<(ExtraCode Code, HashSet<string> Columns)?> FindByIdAsync(short extraCode)
    {
        var columns = await GetAvailableColumnsAsync();
        var result = (
            await QueryAsync(
                columns,
                "[extra_code] = @extraCode",
                command => AddParameter(command, "@extraCode", DbType.Int16, extraCode)
            )
        ).SingleOrDefault();
        return result is null ? null : (result, columns);
    }

    private static List<WriteValue> BuildValues(
        ExtraCode extraCode,
        IReadOnlySet<string> availableColumns,
        bool includeCreateAudit = true
    )
    {
        var values = new List<WriteValue>
        {
            new("extra_description", "@description", DbType.String, extraCode.extra_description),
            new(
                "category_type_code",
                "@categoryTypeCode",
                DbType.Int32,
                extraCode.category_type_code
            ),
            new("specific", "@specific", DbType.Int32, extraCode.specific),
            new("Additional", "@additional", DbType.Int32, extraCode.Additional),
            new("date_updated", "@dateUpdated", DbType.DateTime2, extraCode.date_updated),
            new(
                "modified_by_user_code",
                "@modifiedByUserCode",
                DbType.Int32,
                extraCode.modified_by_user_code
            ),
        };

        if (includeCreateAudit)
        {
            values.Add(
                new("date_created", "@dateCreated", DbType.DateTime2, extraCode.date_created)
            );
            values.Add(
                new(
                    "created_by_user_code",
                    "@createdByUserCode",
                    DbType.Int32,
                    extraCode.created_by_user_code
                )
            );
            values.Add(new("is_deleted", "@isDeleted", DbType.Boolean, false));
        }

        return values.Where(value => availableColumns.Contains(value.Column)).ToList();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list and table name use only fixed compatibility columns and table names; predicates and values are parameterized."
    )]
    private async Task<List<ExtraCode>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    ) => await QueryAsync(await GetAvailableColumnsAsync(), predicate, configure);

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list and table name use only fixed compatibility columns; predicates and values are parameterized."
    )]
    private async Task<List<ExtraCode>> QueryAsync(
        IReadOnlySet<string> availableColumns,
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
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
                $"SELECT {string.Join(", ", projection)} FROM [dbo].[{TableName}] WHERE {string.Join(" AND ", conditions)} ORDER BY [extra_description], [extra_code]";
            configure?.Invoke(command);

            var results = new List<ExtraCode>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapExtraCode(reader));
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
        Justification = "The INSERT statement uses the fixed extra_codes table and parameterized values."
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
                $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[extra_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
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
        Justification = "The UPDATE statement uses the fixed extra_codes table and parameterized values."
    )]
    private async Task ExecuteUpdateAsync(
        IReadOnlySet<string> availableColumns,
        short extraCode,
        IReadOnlyList<WriteValue> values
    )
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
                $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [extra_code] = @extraCode";
            AddParameters(command, values);
            AddParameter(command, "@extraCode", DbType.Int16, extraCode);
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
                    $"The extra_codes table is missing required columns: {string.Join(", ", missing)}"
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

    private static ExtraCode MapExtraCode(DbDataReader reader) =>
        new()
        {
            extra_code = ReadInt16(reader, "extra_code") ?? 0,
            extra_description = ReadString(reader, "extra_description"),
            category_type_code = ReadInt32(reader, "category_type_code"),
            specific = ReadInt32(reader, "specific"),
            Additional = ReadInt32(reader, "Additional"),
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
            "date_created" or "date_updated" => "datetime2",
            "created_by_user_code" or "modified_by_user_code" => "int",
            "is_deleted" => "bit",
            _ => "varchar(1)",
        };
        return $"CAST(NULL AS {sqlType}) AS [{column}]";
    }

    private static string GetNotDeletedFilter(IReadOnlySet<string> columns) =>
        columns.Contains("is_deleted") ? "([is_deleted] = 0 OR [is_deleted] IS NULL)" : "1 = 1";

    private static async Task<HashSet<string>> GetTableColumnsAsync(
        DbConnection connection,
        string schema,
        string table,
        DbTransaction? transaction
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT [COLUMN_NAME]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = @schema
              AND [TABLE_NAME] = @table
            """;
        AddParameter(command, "@schema", DbType.String, schema);
        AddParameter(command, "@table", DbType.String, table);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(Convert.ToString(reader.GetValue(0)) ?? string.Empty);
        }

        return columns;
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

    private static string? ReadString(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToString(reader[column])?.TrimEnd();

    private static short? ReadInt16(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToInt16(reader[column]);

    private static int? ReadInt32(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToInt32(reader[column]);

    private static DateTime? ReadDateTime(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToDateTime(reader[column]);

    private static bool? ReadBoolean(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToBoolean(reader[column]);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
