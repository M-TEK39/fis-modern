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
/// Persists licence fees against the expanded singular table and the original
/// plural legacy table. Reads fall back when the modern table is absent or has
/// no active records; writes target the modern table whenever it exists.
/// </summary>
public sealed class LicenseFeeRepository : ILicenseFeeRepository
{
    private const string ModernTableName = "licence_fee";
    private const string LegacyTableName = "licence_fees";

    private static readonly string[] RequiredColumns =
    [
        "licence_fee_code",
        "licence_description",
        "licence_fee",
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

    public LicenseFeeRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<LicenseFee?> GetByIdAsync(short licenceFeeCode)
    {
        var modern = await GetSourceAsync(ModernTableName);
        if (modern is not null)
        {
            var result = (
                await QueryAsync(
                    modern,
                    "[licence_fee_code] = @licenceFeeCode",
                    command =>
                        AddParameter(command, "@licenceFeeCode", DbType.Int16, licenceFeeCode)
                )
            ).SingleOrDefault();
            if (result is not null)
            {
                return result;
            }
        }

        var legacy = await GetSourceAsync(LegacyTableName);
        return legacy is null
            ? null
            : (
                await QueryAsync(
                    legacy,
                    "[licence_fee_code] = @licenceFeeCode",
                    command =>
                        AddParameter(command, "@licenceFeeCode", DbType.Int16, licenceFeeCode)
                )
            ).SingleOrDefault();
    }

    public async Task<LicenseFee?> GetByDescriptionAsync(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var predicate = "LOWER([licence_description]) = @description";
        var configure = new Action<DbCommand>(command =>
            AddParameter(
                command,
                "@description",
                DbType.String,
                description.Trim().ToLowerInvariant()
            )
        );
        var modern = await GetSourceAsync(ModernTableName);
        if (modern is not null)
        {
            var result = (await QueryAsync(modern, predicate, configure)).SingleOrDefault();
            if (result is not null)
            {
                return result;
            }
        }

        var legacy = await GetSourceAsync(LegacyTableName);
        return legacy is null
            ? null
            : (await QueryAsync(legacy, predicate, configure)).SingleOrDefault();
    }

    public async Task<IEnumerable<LicenseFee>> GetAllAsync()
    {
        var modern = await GetSourceAsync(ModernTableName);
        if (modern is not null)
        {
            var modernRows = await QueryAsync(modern);
            if (modernRows.Count > 0)
            {
                return modernRows;
            }
        }

        var legacy = await GetSourceAsync(LegacyTableName);
        if (legacy is not null)
        {
            return await QueryAsync(legacy);
        }

        return modern is null ? [] : await QueryAsync(modern);
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The page query uses fixed compatibility table sources and columns and parameterizes search and paging values."
    )]
    public async Task<LicenseFeePage> GetPageAsync(LicenseFeePageQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var requestedPage = Math.Max(1, query.Page);
        var searchTerm = query.SearchTerm?.Trim() ?? string.Empty;
        var modern = await GetSourceAsync(ModernTableName);
        var legacy = await GetSourceAsync(LegacyTableName);
        if (modern is not null)
        {
            var modernActiveTotal = await CountPageRowsAsync(modern, string.Empty);
            if (modernActiveTotal > 0 || legacy is null)
            {
                var total =
                    searchTerm.Length == 0
                        ? modernActiveTotal
                        : await CountPageRowsAsync(modern, searchTerm);
                return await ReadPageAsync(modern, searchTerm, requestedPage, pageSize, total);
            }
        }

        if (legacy is null)
        {
            return new LicenseFeePage([], 1, pageSize, 0);
        }

        var legacyTotal = await CountPageRowsAsync(legacy, searchTerm);
        return await ReadPageAsync(legacy, searchTerm, requestedPage, pageSize, legacyTotal);
    }

    public async Task<IEnumerable<LicenseFee>> SearchAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync();
        }

        var predicate = "LOWER(COALESCE([licence_description], '')) LIKE @searchTerm";
        var configure = new Action<DbCommand>(command =>
            AddParameter(
                command,
                "@searchTerm",
                DbType.String,
                $"%{searchTerm.Trim().ToLowerInvariant()}%"
            )
        );
        var modern = await GetSourceAsync(ModernTableName);
        if (modern is not null)
        {
            var modernRows = await QueryAsync(modern, predicate, configure);
            if (modernRows.Count > 0)
            {
                return modernRows;
            }
        }

        var legacy = await GetSourceAsync(LegacyTableName);
        if (legacy is not null)
        {
            return await QueryAsync(legacy, predicate, configure);
        }

        return [];
    }

    public async Task<LicenseFeeDeleteCheck> GetDeleteCheckAsync(short licenceFeeCode)
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
                    "SELECT COUNT(1) FROM [dbo].[model] WHERE [licence_fee_code] = @licenceFeeCode",
                    licenceFeeCode,
                    transaction
                )
                : 0;
            return new LicenseFeeDeleteCheck(modelCount);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<LicenseFee> CreateAsync(LicenseFee licenseFee, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(licenseFee);

        var source = await GetSourceForWriteAsync();
        var now = DateTime.UtcNow;
        licenseFee.date_created = now;
        licenseFee.date_updated = now;
        licenseFee.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        licenseFee.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        licenseFee.is_deleted = false;

        licenseFee.licence_fee_code = await ExecuteInsertAsync(
            source,
            BuildValues(licenseFee, source.Columns)
        );
        return licenseFee;
    }

    public async Task UpdateAsync(LicenseFee licenseFee, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(licenseFee);

        var existing =
            await FindByIdAsync(licenseFee.licence_fee_code)
            ?? throw new InvalidOperationException(
                $"LicenseFee with licence_fee_code {licenseFee.licence_fee_code} not found"
            );
        var now = DateTime.UtcNow;
        licenseFee.date_created = existing.Fee.date_created;
        licenseFee.created_by_user_code = existing.Fee.created_by_user_code;
        licenseFee.date_updated = now;
        licenseFee.modified_by_user_code =
            currentUserId > 0 ? currentUserId : existing.Fee.modified_by_user_code;
        licenseFee.is_deleted = existing.Fee.is_deleted;

        await ExecuteUpdateAsync(
            existing.Source,
            licenseFee.licence_fee_code,
            BuildValues(licenseFee, existing.Source.Columns, includeCreateAudit: false)
        );
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The delete statement is selected from fixed table compatibility sources and the fee code is parameterized."
    )]
    public async Task DeleteAsync(short licenceFeeCode, int currentUserId)
    {
        var existing = await FindByIdAsync(licenceFeeCode);
        if (existing is null)
        {
            return;
        }

        var source = existing.Value.Source;
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
            if (source.Columns.Contains("is_deleted"))
            {
                command.CommandText =
                    $"UPDATE [dbo].[{source.TableName}] SET [is_deleted] = @isDeleted, [date_updated] = @dateUpdated, [modified_by_user_code] = @modifiedByUserCode WHERE [licence_fee_code] = @licenceFeeCode";
                AddParameter(command, "@isDeleted", DbType.Boolean, true);
                AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                AddParameter(
                    command,
                    "@modifiedByUserCode",
                    DbType.Int32,
                    currentUserId > 0 ? currentUserId : null
                );
            }
            else
            {
                command.CommandText =
                    $"DELETE FROM [dbo].[{source.TableName}] WHERE [licence_fee_code] = @licenceFeeCode";
            }

            AddParameter(command, "@licenceFeeCode", DbType.Int16, licenceFeeCode);
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

    private async Task<(LicenseFee Fee, TableSource Source)?> FindByIdAsync(short licenceFeeCode)
    {
        var modern = await GetSourceAsync(ModernTableName);
        if (modern is not null)
        {
            var result = (
                await QueryAsync(
                    modern,
                    "[licence_fee_code] = @licenceFeeCode",
                    command =>
                        AddParameter(command, "@licenceFeeCode", DbType.Int16, licenceFeeCode)
                )
            ).SingleOrDefault();
            if (result is not null)
            {
                return (result, modern);
            }
        }

        var legacy = await GetSourceAsync(LegacyTableName);
        if (legacy is null)
        {
            return null;
        }

        var legacyResult = (
            await QueryAsync(
                legacy,
                "[licence_fee_code] = @licenceFeeCode",
                command => AddParameter(command, "@licenceFeeCode", DbType.Int16, licenceFeeCode)
            )
        ).SingleOrDefault();
        return legacyResult is null ? null : (legacyResult, legacy);
    }

    private async Task<TableSource> GetSourceForWriteAsync() =>
        await GetSourceAsync(ModernTableName)
        ?? await GetSourceAsync(LegacyTableName)
        ?? throw new InvalidOperationException(
            "Neither the modern nor legacy licence fee table is available."
        );

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list and table name are composed only from fixed compatibility columns and table names; predicates and values are parameterized."
    )]
    private async Task<List<LicenseFee>> QueryAsync(
        TableSource source,
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
                    OptionalColumns.Select(column => GetOptionalProjection(source.Columns, column))
                )
                .ToArray();
            var conditions = new List<string> { GetNotDeletedFilter(source.Columns) };
            if (!string.IsNullOrWhiteSpace(predicate))
            {
                conditions.Add(predicate);
            }

            command.CommandText =
                $"SELECT {string.Join(", ", projection)} FROM [dbo].[{source.TableName}] WHERE {string.Join(" AND ", conditions)} ORDER BY [licence_description], [licence_fee_code]";
            configure?.Invoke(command);

            var results = new List<LicenseFee>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapLicenseFee(reader));
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
        Justification = "The INSERT statement uses a fixed compatibility table name and parameterized values."
    )]
    private async Task<short> ExecuteInsertAsync(
        TableSource source,
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
                $"INSERT INTO [dbo].[{source.TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[licence_fee_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
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
        Justification = "The UPDATE statement uses a fixed compatibility table name and parameterized values."
    )]
    private async Task ExecuteUpdateAsync(
        TableSource source,
        short licenceFeeCode,
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
                $"UPDATE [dbo].[{source.TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [licence_fee_code] = @licenceFeeCode";
            AddParameters(command, values);
            AddParameter(command, "@licenceFeeCode", DbType.Int16, licenceFeeCode);
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

    private async Task<TableSource?> GetSourceAsync(string tableName)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            if (
                !await TableExistsAsync(
                    connection,
                    "dbo",
                    tableName,
                    _context.Database.CurrentTransaction?.GetDbTransaction()
                )
            )
            {
                return null;
            }

            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT [COLUMN_NAME]
                FROM [INFORMATION_SCHEMA].[COLUMNS]
                WHERE [TABLE_SCHEMA] = @schema
                  AND [TABLE_NAME] = @table
                """;
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, tableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(Convert.ToString(reader.GetValue(0)) ?? string.Empty);
            }

            return RequiredColumns.All(columns.Contains)
                ? new TableSource(tableName, columns)
                : null;
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
        Justification = "The count query uses a table source selected from the fixed modern and legacy table names."
    )]
    private async Task<int> CountPageRowsAsync(TableSource source, string searchTerm)
    {
        var whereClause = BuildPageWhereClause(source, searchTerm);
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
            command.CommandText = $"""
                SELECT COUNT(1)
                FROM [dbo].[{source.TableName}]
                WHERE {whereClause}
                """;
            if (searchTerm.Length > 0)
            {
                AddParameter(
                    command,
                    "@searchTerm",
                    DbType.String,
                    $"%{searchTerm.ToLowerInvariant()}%"
                );
            }

            return Convert.ToInt32(await command.ExecuteScalarAsync());
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
        Justification = "The page query uses a table source selected from fixed compatibility tables and parameterized filter and paging values."
    )]
    private async Task<LicenseFeePage> ReadPageAsync(
        TableSource source,
        string searchTerm,
        int requestedPage,
        int pageSize,
        int total
    )
    {
        var projection = RequiredColumns
            .Select(column => $"[{column}] AS [{column}]")
            .Concat(OptionalColumns.Select(column => GetOptionalProjection(source.Columns, column)))
            .ToArray();
        var whereClause = BuildPageWhereClause(source, searchTerm);
        var searchPattern = $"%{searchTerm.ToLowerInvariant()}%";
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = Math.Min(requestedPage, totalPages);
            var offset = checked((long)(page - 1) * pageSize);

            await using var dataCommand = connection.CreateCommand();
            dataCommand.Transaction = transaction;
            dataCommand.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{source.TableName}]
                WHERE {whereClause}
                ORDER BY [licence_description], [licence_fee_code]
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY
                """;
            if (searchTerm.Length > 0)
            {
                AddParameter(dataCommand, "@searchTerm", DbType.String, searchPattern);
            }

            AddParameter(dataCommand, "@offset", DbType.Int64, offset);
            AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

            var items = new List<LicenseFee>();
            await using var reader = await dataCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(MapLicenseFee(reader));
            }

            return new LicenseFeePage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string BuildPageWhereClause(TableSource source, string searchTerm)
    {
        var conditions = new List<string> { GetNotDeletedFilter(source.Columns) };
        if (searchTerm.Length > 0)
        {
            conditions.Add("LOWER(COALESCE([licence_description], '')) LIKE @searchTerm");
        }

        return string.Join(" AND ", conditions);
    }

    private static List<WriteValue> BuildValues(
        LicenseFee licenseFee,
        IReadOnlySet<string> availableColumns,
        bool includeCreateAudit = true
    )
    {
        var values = new List<WriteValue>
        {
            new(
                "licence_description",
                "@description",
                DbType.String,
                licenseFee.licence_description
            ),
            new("licence_fee", "@fee", DbType.Decimal, licenseFee.licence_fee),
            new("date_updated", "@dateUpdated", DbType.DateTime2, licenseFee.date_updated),
            new(
                "modified_by_user_code",
                "@modifiedByUserCode",
                DbType.Int32,
                licenseFee.modified_by_user_code
            ),
        };

        if (includeCreateAudit)
        {
            values.Add(
                new("date_created", "@dateCreated", DbType.DateTime2, licenseFee.date_created)
            );
            values.Add(
                new(
                    "created_by_user_code",
                    "@createdByUserCode",
                    DbType.Int32,
                    licenseFee.created_by_user_code
                )
            );
            values.Add(new("is_deleted", "@isDeleted", DbType.Boolean, false));
        }

        return values.Where(value => availableColumns.Contains(value.Column)).ToList();
    }

    private static LicenseFee MapLicenseFee(DbDataReader reader) =>
        new()
        {
            licence_fee_code = ReadInt16(reader, "licence_fee_code") ?? 0,
            licence_description = ReadString(reader, "licence_description"),
            licence_fee = ReadDecimal(reader, "licence_fee"),
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
        Justification = "The helper is called with fixed repository statements and the fee code is parameterized."
    )]
    private static async Task<int> CountAsync(
        DbConnection connection,
        string sql,
        short licenceFeeCode,
        DbTransaction? transaction
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        AddParameter(command, "@licenceFeeCode", DbType.Int16, licenceFeeCode);
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

    private sealed record TableSource(string TableName, HashSet<string> Columns);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
