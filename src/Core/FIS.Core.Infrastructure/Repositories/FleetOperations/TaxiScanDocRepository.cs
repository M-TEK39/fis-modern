using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Persists taxi requisition scans against the client's original table or the
/// expanded table. The legacy schema has only the certificate data columns;
/// modern audit columns are negotiated at runtime when they exist.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility allowlists; all submitted values are parameters."
)]
public sealed class TaxiScanDocRepository : ITaxiScanDocRepository
{
    private static readonly string[] TableCandidates = ["Taxi_ScanDocs", "TaxiScanDocs"];
    private const string VehicleTableName = "vehicle_master";
    private static readonly string[] RequiredColumns =
    [
        "taxi_scandoc_code",
        "vmf_code",
        "image",
        "period_begin",
        "period_end",
    ];
    private static readonly string[] AuditColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;

    public TaxiScanDocRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TaxiScanDoc?> GetByIdAsync(int scanDocCode) =>
        (
            await QueryAsync(
                "scan.[taxi_scandoc_code] = @scanDocCode",
                command => AddParameter(command, "@scanDocCode", DbType.Int32, scanDocCode)
            )
        ).SingleOrDefault();

    public async Task<IEnumerable<TaxiScanDoc>> GetAllAsync() => await QueryAsync();

    public async Task<TaxiScanDocPage> GetPageAsync(TaxiScanDocPageQuery query)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var requestedPage = Math.Max(1, query.Page);
        var schema = await GetSchemaAsync();
        var searchTerm = query.SearchTerm?.Trim() ?? string.Empty;
        var conditions = new List<string> { GetActiveFilter(schema.Columns, "scan") };
        var hasSearch = searchTerm.Length > 0;
        var vehicleColumns = await GetTableColumnsAsync(VehicleTableName);
        var vehicleJoin = vehicleColumns.Contains("vmf_code")
            ? $"LEFT JOIN [dbo].[{VehicleTableName}] vehicle ON vehicle.[vmf_code] = scan.[vmf_code]"
            : string.Empty;
        var fleetProjection = vehicleColumns.Contains("fleet_number")
            ? "vehicle.[fleet_number] AS [__fleet_number]"
            : "CAST(NULL AS varchar(1)) AS [__fleet_number]";
        var registrationProjection = vehicleColumns.Contains("registration_number")
            ? "vehicle.[registration_number] AS [__registration_number]"
            : "CAST(NULL AS varchar(1)) AS [__registration_number]";

        if (hasSearch)
        {
            var searchPredicates = new List<string>
            {
                "CHARINDEX(@searchTerm, LOWER(LTRIM(RTRIM(COALESCE(scan.[image], ''))))) > 0",
                "CHARINDEX(@searchTerm, LOWER(LTRIM(RTRIM(CONVERT(varchar(50), scan.[vmf_code]))))) > 0",
            };
            var vehiclePredicates = new List<string>();
            if (vehicleColumns.Contains("fleet_number"))
            {
                vehiclePredicates.Add(
                    "CHARINDEX(@searchTerm, LOWER(LTRIM(RTRIM(COALESCE(vehicle.[fleet_number], ''))))) > 0"
                );
            }
            if (vehicleColumns.Contains("registration_number"))
            {
                vehiclePredicates.Add(
                    "CHARINDEX(@searchTerm, LOWER(LTRIM(RTRIM(COALESCE(vehicle.[registration_number], ''))))) > 0"
                );
            }
            if (vehiclePredicates.Count > 0 && vehicleColumns.Contains("vmf_code"))
            {
                searchPredicates.Add(
                    "EXISTS ("
                        + $"SELECT 1 FROM [dbo].[{VehicleTableName}] vehicle "
                        + "WHERE vehicle.[vmf_code] = scan.[vmf_code] "
                        + $"AND ({string.Join(" OR ", vehiclePredicates)})"
                        + ")"
                );
            }

            conditions.Add($"({string.Join(" OR ", searchPredicates)})");
        }

        var whereClause = string.Join(" AND ", conditions);
        await using var scope = await OpenConnectionAsync();
        await using var countCommand = scope.Connection.CreateCommand();
        countCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        countCommand.CommandText = $"""
            SELECT COUNT(1)
            FROM [dbo].[{schema.TableName}] scan
            WHERE {whereClause}
            """;
        AddSearchParameter(countCommand, searchTerm, hasSearch);
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var page = Math.Min(requestedPage, totalPages);
        var skip = checked((long)(page - 1) * pageSize);

        await using var dataCommand = scope.Connection.CreateCommand();
        dataCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        dataCommand.CommandText = $"""
            SELECT {string.Join(
                ", ",
                RequiredColumns.Concat(AuditColumns).Select(column =>
                    GetProjection(schema.Columns, column, "scan")
                )
            )}
            , {fleetProjection}, {registrationProjection}
            FROM [dbo].[{schema.TableName}] scan
            {vehicleJoin}
            WHERE {whereClause}
            ORDER BY scan.[period_begin] DESC, scan.[taxi_scandoc_code] DESC
            OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
            """;
        AddSearchParameter(dataCommand, searchTerm, hasSearch);
        AddParameter(dataCommand, "@skip", DbType.Int64, skip);
        AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

        var items = new List<TaxiScanDocPageItem>();
        await using var reader = await dataCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(
                new TaxiScanDocPageItem(
                    MapDocument(reader),
                    ReadString(reader, "__fleet_number"),
                    ReadString(reader, "__registration_number")
                )
            );
        }

        return new TaxiScanDocPage(items, page, pageSize, total);
    }

    public async Task<IEnumerable<TaxiScanDoc>> GetByVehicleAsync(int vmfCode) =>
        await QueryAsync(
            "scan.[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode)
        );

    public async Task<TaxiScanDoc> CreateAsync(TaxiScanDoc document, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (document.vmf_code <= 0)
            throw new ArgumentException("Vehicle code is required.", nameof(document));
        if (string.IsNullOrWhiteSpace(document.image))
            throw new ArgumentException("Stored image name is required.", nameof(document));
        if (
            document.period_begin.HasValue
            && document.period_end.HasValue
            && document.period_end < document.period_begin
        )
            throw new ArgumentException(
                "The certificate end date must be on or after the begin date.",
                nameof(document)
            );

        var schema = await GetSchemaAsync();
        var values = new List<WriteValue>
        {
            RequiredValue(schema.Columns, "vmf_code", "@vmfCode", DbType.Int32, document.vmf_code),
            RequiredValue(schema.Columns, "image", "@image", DbType.String, document.image.Trim()),
            RequiredValue(
                schema.Columns,
                "period_begin",
                "@periodBegin",
                DbType.DateTime,
                document.period_begin
            ),
            RequiredValue(
                schema.Columns,
                "period_end",
                "@periodEnd",
                DbType.DateTime,
                document.period_end
            ),
        };

        AddValue(
            values,
            schema.Columns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddValue(
            values,
            schema.Columns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            UserIdOrNull(currentUserId)
        );
        AddValue(values, schema.Columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            INSERT INTO [dbo].[{schema.TableName}] ({string.Join(
                ", ",
                values.Select(value => $"[{value.Column}]")
            )})
            OUTPUT INSERTED.[taxi_scandoc_code]
            VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
            """;
        AddParameters(command, values);
        document.taxi_scandoc_code = Convert.ToInt32(await command.ExecuteScalarAsync());

        return await GetByIdAsync(document.taxi_scandoc_code)
            ?? throw new InvalidOperationException(
                $"Taxi scan document {document.taxi_scandoc_code} could not be read after creation."
            );
    }

    public async Task DeleteAsync(int scanDocCode, int currentUserId)
    {
        var schema = await GetSchemaAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

        if (schema.Columns.ContainsKey("is_deleted"))
        {
            var assignments = new List<string> { "[is_deleted] = @isDeleted" };
            AddParameter(command, "@isDeleted", DbType.Boolean, true);
            AddOptionalAssignment(
                assignments,
                command,
                schema.Columns,
                "date_updated",
                "@dateUpdated",
                DbType.DateTime2,
                DateTime.UtcNow
            );
            AddOptionalAssignment(
                assignments,
                command,
                schema.Columns,
                "modified_by_user_code",
                "@modifiedByUserCode",
                DbType.Int32,
                UserIdOrNull(currentUserId)
            );
            command.CommandText =
                $"UPDATE [dbo].[{schema.TableName}] SET {string.Join(", ", assignments)} WHERE [taxi_scandoc_code] = @scanDocCode AND {GetActiveFilter(schema.Columns)}";
        }
        else
        {
            command.CommandText =
                $"DELETE FROM [dbo].[{schema.TableName}] WHERE [taxi_scandoc_code] = @scanDocCode";
        }

        AddParameter(command, "@scanDocCode", DbType.Int32, scanDocCode);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<TaxiScanDoc>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var schema = await GetSchemaAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        var conditions = new List<string> { GetActiveFilter(schema.Columns, "scan") };
        if (!string.IsNullOrWhiteSpace(predicate))
            conditions.Add($"({predicate})");
        command.CommandText = $"""
            SELECT {string.Join(
                ", ",
                RequiredColumns.Concat(AuditColumns).Select(column =>
                    GetProjection(schema.Columns, column, "scan")
                )
            )}
            FROM [dbo].[{schema.TableName}] scan
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY scan.[period_begin] DESC, scan.[taxi_scandoc_code] DESC
            """;
        configure?.Invoke(command);

        var results = new List<TaxiScanDoc>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapDocument(reader));
        return results;
    }

    private async Task<TableSchema> GetSchemaAsync()
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT [TABLE_NAME], [COLUMN_NAME], [DATA_TYPE]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] IN (@tableOne, @tableTwo)
            ORDER BY CASE WHEN [TABLE_NAME] = @tableOne THEN 0 ELSE 1 END
            """;
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@tableOne", DbType.String, TableCandidates[0]);
        AddParameter(command, "@tableTwo", DbType.String, TableCandidates[1]);

        var tables = new Dictionary<string, Dictionary<string, ColumnInfo>>(
            StringComparer.OrdinalIgnoreCase
        );
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var tableName = reader.GetString(0);
            if (!tables.TryGetValue(tableName, out var columns))
            {
                columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
                tables[tableName] = columns;
            }
            columns[reader.GetString(1)] = new ColumnInfo(reader.GetString(1), reader.GetString(2));
        }

        var table = TableCandidates.FirstOrDefault(candidate => tables.ContainsKey(candidate));
        if (table is null)
            throw new InvalidOperationException(
                "Neither Taxi_ScanDocs nor TaxiScanDocs is available in the connected database."
            );
        var schema = new TableSchema(table, tables[table]);
        foreach (
            var required in RequiredColumns.Where(column => !schema.Columns.ContainsKey(column))
        )
            throw new InvalidOperationException(
                $"The required taxi scan compatibility column {required} is not available."
            );
        return schema;
    }

    private async Task<HashSet<string>> GetTableColumnsAsync(string tableName)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT [COLUMN_NAME]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table
            """;
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns.Add(reader.GetString(0));
        return columns;
    }

    private static TaxiScanDoc MapDocument(DbDataReader reader) =>
        new()
        {
            taxi_scandoc_code = ReadInt32(reader, "taxi_scandoc_code") ?? 0,
            vmf_code = ReadInt32(reader, "vmf_code") ?? 0,
            image = ReadString(reader, "image"),
            period_begin = ReadDateTime(reader, "period_begin"),
            period_end = ReadDateTime(reader, "period_end"),
            date_created = ReadDateTime(reader, "date_created") ?? default,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code = ReadInt32(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted") ?? false,
        };

    private static string GetActiveFilter(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string alias = ""
    ) =>
        columns.ContainsKey("is_deleted")
            ? $"ISNULL({(string.IsNullOrWhiteSpace(alias) ? string.Empty : $"{alias}.")}[is_deleted], 0) = 0"
            : "1 = 1";

    private static string GetProjection(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string alias
    ) =>
        columns.ContainsKey(column)
            ? $"{alias}.[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static string GetSqlType(string column) =>
        column switch
        {
            "taxi_scandoc_code"
            or "vmf_code"
            or "created_by_user_code"
            or "modified_by_user_code" => "int",
            "period_begin" or "period_end" or "date_created" or "date_updated" => "datetime2",
            "is_deleted" => "bit",
            _ => "varchar(1)",
        };

    private static WriteValue RequiredValue(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (!columns.ContainsKey(column))
            throw new InvalidOperationException(
                $"The required taxi scan compatibility column {column} is not available."
            );
        return new WriteValue(column, parameter, type, value);
    }

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (columns.ContainsKey(column))
            values.Add(new WriteValue(column, parameter, type, value));
    }

    private static void AddOptionalAssignment(
        List<string> assignments,
        DbCommand command,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (!columns.ContainsKey(column))
            return;
        assignments.Add($"[{column}] = {parameter}");
        AddParameter(command, parameter, type, value);
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

    private static void AddSearchParameter(DbCommand command, string searchTerm, bool hasSearch)
    {
        if (hasSearch)
            AddParameter(command, "@searchTerm", DbType.String, searchTerm.ToLowerInvariant());
    }

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

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        if (reader.IsDBNull(ordinal))
            return null;
        var value = reader.GetValue(ordinal);
        return value switch
        {
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
            _ when DateTime.TryParse(Convert.ToString(value), out var parsed) => parsed,
            _ => null,
        };
    }

    private static bool? ReadBoolean(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private async Task<ConnectionScope> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();
        return new ConnectionScope(connection, shouldClose);
    }

    private static int? UserIdOrNull(int currentUserId) => currentUserId > 0 ? currentUserId : null;

    private sealed record ColumnInfo(string Name, string DataType);

    private sealed record TableSchema(string TableName, Dictionary<string, ColumnInfo> Columns);

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
