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
/// Persists loss descriptions against the original Loss_type table while
/// negotiating expanded audit and soft-delete columns at runtime.
/// </summary>
public sealed class LossTypeRepository : ILossTypeRepository
{
    private const string TableName = "Loss_type";

    private static readonly string[] RequiredColumns = ["loss_type_code", "loss_description"];

    private static readonly string[] OptionalColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;

    public LossTypeRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<LossType?> GetByIdAsync(short lossTypeCode) =>
        (
            await QueryAsync(
                "[loss_type_code] = @lossTypeCode",
                command => AddParameter(command, "@lossTypeCode", DbType.Int16, lossTypeCode)
            )
        ).SingleOrDefault();

    public async Task<LossType?> GetByDescriptionAsync(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        return (
            await QueryAsync(
                "LOWER(RTRIM([loss_description])) = @description",
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

    public async Task<IEnumerable<LossType>> GetAllAsync() => await QueryAsync();

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The page query uses fixed compatibility columns and a parameterized search term and offset."
    )]
    public async Task<LossTypePage> GetPageAsync(LossTypePageQuery query)
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
            conditions.Add("LOWER(COALESCE([loss_description], '')) LIKE @searchTerm");
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
                ORDER BY [loss_description], [loss_type_code]
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

            var items = new List<LossType>();
            await using var reader = await dataCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(MapLossType(reader));
            }

            return new LossTypePage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<LossType>> SearchAsync(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return await GetAllAsync();
        }

        return await QueryAsync(
            "LOWER(COALESCE([loss_description], '')) LIKE @searchTerm",
            command =>
                AddParameter(
                    command,
                    "@searchTerm",
                    DbType.String,
                    $"%{searchTerm.Trim().ToLowerInvariant()}%"
                )
        );
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The dependency query uses only fixed compatibility columns and table names; the loss type code is parameterized."
    )]
    public async Task<LossTypeDeleteCheck> GetDeleteCheckAsync(short lossTypeCode)
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
            var lossColumns = await GetTableColumnsAsync(connection, "dbo", "losses", transaction);
            var vehicleColumns = await GetTableColumnsAsync(
                connection,
                "dbo",
                "vehicle_master",
                transaction
            );
            var canInspectDependencies =
                new[] { "loss_type_code", "vmf_code" }.All(lossColumns.Contains)
                && new[] { "vmf_code", "fleet_number" }.All(vehicleColumns.Contains);

            if (!canInspectDependencies)
            {
                return new LossTypeDeleteCheck(0, [], false);
            }

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            var lossProjection = new[]
            {
                GetColumnProjection(vehicleColumns, "fleet_number", "vehicle"),
                GetColumnProjection(lossColumns, "loss_date", "loss"),
                GetColumnProjection(lossColumns, "loss_reference", "loss"),
            };
            var activeLossPredicate = lossColumns.Contains("is_deleted")
                ? " AND ([loss].[is_deleted] = 0 OR [loss].[is_deleted] IS NULL)"
                : string.Empty;
            var activeVehiclePredicate = vehicleColumns.Contains("is_deleted")
                ? " AND ([vehicle].[is_deleted] = 0 OR [vehicle].[is_deleted] IS NULL)"
                : string.Empty;
            var ordering = new List<string> { "[vehicle].[fleet_number]" };
            if (lossColumns.Contains("loss_date"))
            {
                ordering.Add("[loss].[loss_date]");
            }

            if (lossColumns.Contains("loss_reference"))
            {
                ordering.Add("[loss].[loss_reference]");
            }

            command.CommandText = $"""
                SELECT {string.Join(", ", lossProjection)}
                FROM [dbo].[losses] AS [loss]
                INNER JOIN [dbo].[vehicle_master] AS [vehicle]
                    ON [vehicle].[vmf_code] = [loss].[vmf_code]
                WHERE [loss].[loss_type_code] = @lossTypeCode{activeLossPredicate}{activeVehiclePredicate}
                ORDER BY {string.Join(", ", ordering)}
                """;
            AddParameter(command, "@lossTypeCode", DbType.Int16, lossTypeCode);

            var dependencies = new List<LossTypeDeleteDependency>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                dependencies.Add(
                    new LossTypeDeleteDependency(
                        ReadString(reader, "fleet_number"),
                        ReadDateTime(reader, "loss_date"),
                        ReadString(reader, "loss_reference")
                    )
                );
            }

            return new LossTypeDeleteCheck(dependencies.Count, dependencies);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<LossType> CreateAsync(LossType lossType, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(lossType);

        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        lossType.date_created = now;
        lossType.date_updated = now;
        lossType.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        lossType.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        lossType.is_deleted = false;

        lossType.loss_type_code = await ExecuteInsertAsync(BuildValues(lossType, availableColumns));
        return lossType;
    }

    public async Task UpdateAsync(LossType lossType, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(lossType);

        var existing =
            await FindByIdAsync(lossType.loss_type_code)
            ?? throw new InvalidOperationException(
                $"LossType with loss_type_code {lossType.loss_type_code} not found"
            );
        var now = DateTime.UtcNow;

        lossType.date_created = existing.Type.date_created;
        lossType.created_by_user_code = existing.Type.created_by_user_code;
        lossType.date_updated = now;
        lossType.modified_by_user_code =
            currentUserId > 0 ? currentUserId : existing.Type.modified_by_user_code;
        lossType.is_deleted = existing.Type.is_deleted;

        await ExecuteUpdateAsync(
            existing.Columns,
            lossType.loss_type_code,
            BuildValues(lossType, existing.Columns, includeCreateAudit: false)
        );
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The delete statement uses the fixed Loss_type table and a parameterized loss type code."
    )]
    public async Task DeleteAsync(short lossTypeCode, int currentUserId)
    {
        var existing = await FindByIdAsync(lossTypeCode);
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
                    $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", updates)} WHERE [loss_type_code] = @lossTypeCode";
            }
            else
            {
                command.CommandText =
                    $"DELETE FROM [dbo].[{TableName}] WHERE [loss_type_code] = @lossTypeCode";
            }

            AddParameter(command, "@lossTypeCode", DbType.Int16, lossTypeCode);
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

    private async Task<(LossType Type, HashSet<string> Columns)?> FindByIdAsync(short lossTypeCode)
    {
        var columns = await GetAvailableColumnsAsync();
        var result = (
            await QueryAsync(
                columns,
                "[loss_type_code] = @lossTypeCode",
                command => AddParameter(command, "@lossTypeCode", DbType.Int16, lossTypeCode)
            )
        ).SingleOrDefault();
        return result is null ? null : (result, columns);
    }

    private static List<WriteValue> BuildValues(
        LossType lossType,
        IReadOnlySet<string> availableColumns,
        bool includeCreateAudit = true
    )
    {
        var values = new List<WriteValue>
        {
            new("loss_description", "@description", DbType.String, lossType.loss_description),
            new("date_updated", "@dateUpdated", DbType.DateTime2, lossType.date_updated),
            new(
                "modified_by_user_code",
                "@modifiedByUserCode",
                DbType.Int32,
                lossType.modified_by_user_code
            ),
        };

        if (includeCreateAudit)
        {
            values.Add(
                new("date_created", "@dateCreated", DbType.DateTime2, lossType.date_created)
            );
            values.Add(
                new(
                    "created_by_user_code",
                    "@createdByUserCode",
                    DbType.Int32,
                    lossType.created_by_user_code
                )
            );
            values.Add(new("is_deleted", "@isDeleted", DbType.Boolean, false));
        }

        return values.Where(value => availableColumns.Contains(value.Column)).ToList();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list and table name use only fixed compatibility columns; predicates and values are parameterized."
    )]
    private async Task<List<LossType>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    ) => await QueryAsync(await GetAvailableColumnsAsync(), predicate, configure);

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list and table name use only fixed compatibility columns; predicates and values are parameterized."
    )]
    private async Task<List<LossType>> QueryAsync(
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
                $"SELECT {string.Join(", ", projection)} FROM [dbo].[{TableName}] WHERE {string.Join(" AND ", conditions)} ORDER BY [loss_description], [loss_type_code]";
            configure?.Invoke(command);

            var results = new List<LossType>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapLossType(reader));
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
        Justification = "The INSERT statement uses the fixed Loss_type table and parameterized values."
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
                $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[loss_type_code] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
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
        Justification = "The UPDATE statement uses the fixed Loss_type table and parameterized values."
    )]
    private async Task ExecuteUpdateAsync(
        IReadOnlySet<string> availableColumns,
        short lossTypeCode,
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
                $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [loss_type_code] = @lossTypeCode";
            AddParameters(command, values);
            AddParameter(command, "@lossTypeCode", DbType.Int16, lossTypeCode);
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
            var columns = await GetTableColumnsAsync(
                connection,
                "dbo",
                TableName,
                _context.Database.CurrentTransaction?.GetDbTransaction()
            );
            var missing = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The Loss_type table is missing required columns: {string.Join(", ", missing)}"
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

    private static LossType MapLossType(DbDataReader reader) =>
        new()
        {
            loss_type_code = ReadInt16(reader, "loss_type_code") ?? 0,
            loss_description = ReadString(reader, "loss_description"),
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

    private static string GetColumnProjection(
        IReadOnlySet<string> columns,
        string column,
        string alias
    )
    {
        if (columns.Contains(column))
        {
            return $"[{alias}].[{column}] AS [{column}]";
        }

        var sqlType = column switch
        {
            "loss_date" => "datetime2",
            _ => "varchar(255)",
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
