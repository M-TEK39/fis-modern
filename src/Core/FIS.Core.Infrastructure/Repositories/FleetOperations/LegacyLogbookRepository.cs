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
/// Reads and writes the original dbo.logbook table without requiring the
/// modern audit columns that were added later. The public API still returns
/// the modern Logbook contract, while every identifier and submitted value is
/// kept parameterized.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility projections and all submitted values are parameters."
)]
internal sealed class LegacyLogbookRepository : ILogbookRepository
{
    private const string TableName = "logbook";

    private static readonly string[] RequiredColumns =
    [
        "logbookcode",
        "vmf_code",
        "begin_num",
        "end_num",
        "handout_date",
        "site_code",
        "lb_receiver_name",
        "lb_tel_num",
        "lb_comment",
    ];

    private readonly FisDbContext _context;

    public LegacyLogbookRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Logbook?> GetByIdAsync(short logbookCode) =>
        (
            await QueryAsync(
                columns => "l.[logbookcode] = @logbookCode",
                command => AddParameter(command, "@logbookCode", DbType.Int16, logbookCode)
            )
        ).SingleOrDefault();

    public Task<IEnumerable<Logbook>> GetAllAsync() => QueryAsEnumerableAsync();

    public async Task<LogbookPage> GetPageAsync(LogbookPageQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var requestedPage = Math.Max(1, query.Page);
        var searchTerm = query.SearchTerm?.ToLowerInvariant() ?? string.Empty;
        var columns = await GetAvailableColumnsAsync();
        var vmfCode =
            query.VmfCode is > 0 && columns.ContainsKey("vmf_code") ? query.VmfCode : null;
        var whereClause = BuildPageWhereClause(columns, searchTerm, vmfCode);

        await using var scope = await OpenConnectionAsync();

        await using var countCommand = scope.Connection.CreateCommand();
        countCommand.Transaction = CurrentTransaction;
        countCommand.CommandText = $"""
                SELECT COUNT(1)
                FROM [dbo].[{TableName}] l
                LEFT JOIN [dbo].[vehicle_master] v ON v.[vmf_code] = l.[vmf_code]
                LEFT JOIN [dbo].[site] s ON s.[Site_code] = l.[site_code]
                WHERE {whereClause}
            """;
        AddPageParameters(countCommand, searchTerm, vmfCode);
        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var page = Math.Min(requestedPage, totalPages);
        var skip = checked((long)(page - 1) * pageSize);

        await using var dataCommand = scope.Connection.CreateCommand();
        dataCommand.Transaction = CurrentTransaction;
        dataCommand.CommandText = $"""
                SELECT
                    {BuildProjection(columns)}
                FROM [dbo].[{TableName}] l
                LEFT JOIN [dbo].[vehicle_master] v ON v.[vmf_code] = l.[vmf_code]
                LEFT JOIN [dbo].[site] s ON s.[Site_code] = l.[site_code]
                WHERE {whereClause}
                ORDER BY l.[handout_date] DESC, l.[logbookcode] DESC
                OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
            """;
        AddPageParameters(dataCommand, searchTerm, vmfCode);
        AddParameter(dataCommand, "@skip", DbType.Int64, skip);
        AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

        var items = new List<Logbook>();
        await using var reader = await dataCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(Map(reader));

        return new LogbookPage(items, page, pageSize, total);
    }

    public async Task<IEnumerable<Logbook>> GetByVehicleAsync(int vmfCode) =>
        await QueryAsync(
            _ => "l.[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode)
        );

    public async Task<IEnumerable<Logbook>> GetBySiteAsync(short siteCode) =>
        await QueryAsync(
            _ => "l.[site_code] = @siteCode",
            command => AddParameter(command, "@siteCode", DbType.Int16, siteCode)
        );

    public async Task<Logbook> CreateAsync(Logbook logbook, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(logbook);
        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();

        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, logbook.vmf_code, true);
        AddValue(values, columns, "begin_num", "@beginNum", DbType.String, logbook.begin_num, true);
        AddValue(values, columns, "end_num", "@endNum", DbType.String, logbook.end_num, true);
        AddValue(
            values,
            columns,
            "handout_date",
            "@handoutDate",
            DbType.DateTime,
            logbook.handout_date,
            true
        );
        AddValue(values, columns, "site_code", "@siteCode", DbType.Int16, logbook.site_code, true);
        AddValue(
            values,
            columns,
            "lb_receiver_name",
            "@receiverName",
            DbType.String,
            logbook.lb_receiver_name,
            true
        );
        AddValue(
            values,
            columns,
            "lb_tel_num",
            "@telephone",
            DbType.String,
            logbook.lb_tel_num,
            true
        );
        AddValue(
            values,
            columns,
            "lb_comment",
            "@comment",
            DbType.String,
            logbook.lb_comment,
            true
        );
        AddValue(
            values,
            columns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            DateTime.UtcNow,
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
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false, true);

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[logbookcode] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        var id = Convert.ToInt16(await command.ExecuteScalarAsync());
        return await GetByIdAsync(id)
            ?? throw new InvalidOperationException("Created logbook could not be read.");
    }

    public async Task<Logbook> UpdateAsync(Logbook logbook, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(logbook);
        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();

        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, logbook.vmf_code, true);
        AddValue(values, columns, "begin_num", "@beginNum", DbType.String, logbook.begin_num, true);
        AddValue(values, columns, "end_num", "@endNum", DbType.String, logbook.end_num, true);
        AddValue(
            values,
            columns,
            "handout_date",
            "@handoutDate",
            DbType.DateTime,
            logbook.handout_date,
            true
        );
        AddValue(values, columns, "site_code", "@siteCode", DbType.Int16, logbook.site_code, true);
        AddValue(
            values,
            columns,
            "lb_receiver_name",
            "@receiverName",
            DbType.String,
            logbook.lb_receiver_name,
            true
        );
        AddValue(
            values,
            columns,
            "lb_tel_num",
            "@telephone",
            DbType.String,
            logbook.lb_tel_num,
            true
        );
        AddValue(
            values,
            columns,
            "lb_comment",
            "@comment",
            DbType.String,
            logbook.lb_comment,
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

        await ExecuteUpdateAsync(logbook.logbookcode, columns, values);
        return await GetByIdAsync(logbook.logbookcode)
            ?? throw new KeyNotFoundException(
                $"Logbook not found with code: {logbook.logbookcode}"
            );
    }

    public async Task DeleteAsync(short logbookCode, int currentUserId)
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
                $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", assignments)} WHERE [logbookcode] = @logbookCode";
        }
        else
        {
            // The original maintenance flow deleted the handout row.
            command.CommandText =
                $"DELETE FROM [dbo].[{TableName}] WHERE [logbookcode] = @logbookCode";
        }

        AddParameter(command, "@logbookCode", DbType.Int16, logbookCode);
        if (await command.ExecuteNonQueryAsync() == 0)
            throw new KeyNotFoundException($"Logbook not found with code: {logbookCode}");
    }

    private async Task<IEnumerable<Logbook>> QueryAsEnumerableAsync() => await QueryAsync();

    private static string BuildPageWhereClause(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string searchTerm,
        int? vmfCode
    )
    {
        var conditions = new List<string>();
        if (columns.ContainsKey("is_deleted"))
            conditions.Add("ISNULL(l.[is_deleted], 0) = 0");
        if (vmfCode.HasValue)
            conditions.Add("l.[vmf_code] = @vmfCode");

        if (searchTerm.Length == 0)
            return conditions.Count == 0 ? "1 = 1" : string.Join(" AND ", conditions);

        var searchPredicates = new List<string>
        {
            ContainsSearch("l.[vmf_code]", "nvarchar(50)"),
            ContainsSearch("v.[fleet_number]", "nvarchar(max)"),
            ContainsSearch("v.[registration_number]", "nvarchar(max)"),
            ContainsSearch("l.[begin_num]", "nvarchar(max)"),
            ContainsSearch("l.[end_num]", "nvarchar(max)"),
            ContainsSearch("l.[lb_receiver_name]", "nvarchar(max)"),
            ContainsSearch("l.[lb_comment]", "nvarchar(max)"),
            ContainsSearch("s.[description]", "nvarchar(max)"),
        };

        conditions.Add($"({string.Join(" OR ", searchPredicates)})");
        return string.Join(" AND ", conditions);
    }

    private static void AddPageParameters(DbCommand command, string searchTerm, int? vmfCode)
    {
        if (searchTerm.Length > 0)
            AddParameter(command, "@search", DbType.String, searchTerm);
        if (vmfCode.HasValue)
            AddParameter(command, "@vmfCode", DbType.Int32, vmfCode.Value);
    }

    private static string ContainsSearch(string expression, string sqlType) =>
        $"CHARINDEX(@search, LOWER(LTRIM(RTRIM(COALESCE(CONVERT({sqlType}, {expression}), N''))))) > 0";

    private async Task<List<Logbook>> QueryAsync(
        Func<IReadOnlyDictionary<string, ColumnInfo>, string?>? predicateFactory = null,
        Action<DbCommand>? configure = null
    )
    {
        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;

        var conditions = new List<string>();
        var predicate = predicateFactory?.Invoke(columns);
        if (!string.IsNullOrWhiteSpace(predicate))
            conditions.Add($"({predicate})");
        if (columns.ContainsKey("is_deleted"))
            conditions.Add("ISNULL(l.[is_deleted], 0) = 0");

        command.CommandText = $"""
            SELECT
                {BuildProjection(columns)}
            FROM [dbo].[{TableName}] l
            LEFT JOIN [dbo].[vehicle_master] v ON v.[vmf_code] = l.[vmf_code]
            LEFT JOIN [dbo].[site] s ON s.[Site_code] = l.[site_code]
            WHERE {string.Join(" AND ", conditions.DefaultIfEmpty("1 = 1"))}
            ORDER BY l.[handout_date] DESC, l.[logbookcode] DESC
            """;
        configure?.Invoke(command);

        var results = new List<Logbook>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(Map(reader));
        return results;
    }

    private async Task ExecuteUpdateAsync(
        short logbookCode,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        IReadOnlyCollection<WriteValue> values
    )
    {
        if (values.Count == 0)
            return;
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [logbookcode] = @logbookCode";
        AddParameters(command, values);
        AddParameter(command, "@logbookCode", DbType.Int16, logbookCode);
        if (await command.ExecuteNonQueryAsync() == 0)
            throw new KeyNotFoundException($"Logbook not found with code: {logbookCode}");
    }

    private async Task<Dictionary<string, ColumnInfo>> GetAvailableColumnsAsync()
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            "SELECT [COLUMN_NAME], [DATA_TYPE] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table";
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, TableName);

        var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns[reader.GetString(0)] = new ColumnInfo(reader.GetString(0), reader.GetString(1));
        if (RequiredColumns.Any(column => !columns.ContainsKey(column)))
            throw new InvalidOperationException(
                "The logbook compatibility table is missing required legacy columns."
            );
        return columns;
    }

    private static string BuildProjection(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        string.Join(
            ",\n                ",
            [
                "l.[logbookcode] AS [logbookcode]",
                "l.[vmf_code] AS [vmf_code]",
                "l.[begin_num] AS [begin_num]",
                "l.[end_num] AS [end_num]",
                "l.[handout_date] AS [handout_date]",
                "l.[site_code] AS [site_code]",
                "l.[lb_receiver_name] AS [lb_receiver_name]",
                "l.[lb_tel_num] AS [lb_tel_num]",
                "l.[lb_comment] AS [lb_comment]",
                "v.[fleet_number] AS [fleet_number]",
                "v.[registration_number] AS [registration_number]",
                "s.[description] AS [site_description]",
                DateCreatedExpression(columns) + " AS [date_created]",
                OptionalExpression(columns, "date_updated", "datetime2") + " AS [date_updated]",
                OptionalExpression(columns, "created_by_user_code", "int")
                    + " AS [created_by_user_code]",
                OptionalExpression(columns, "modified_by_user_code", "int")
                    + " AS [modified_by_user_code]",
                OptionalExpression(columns, "is_deleted", "bit") + " AS [is_deleted]",
            ]
        );

    private static string DateCreatedExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("date_created")
            ? "l.[date_created]"
            : "COALESCE(l.[handout_date], CONVERT(datetime2, '19000101', 112))";

    private static string OptionalExpression(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string sqlType
    ) => columns.ContainsKey(column) ? $"l.[{column}]" : $"CAST(NULL AS {sqlType})";

    private static Logbook Map(DbDataReader reader)
    {
        var vmfCode = ReadInt(reader, "vmf_code");
        var siteCode = ReadShort(reader, "site_code");
        var fleetNumber = ReadString(reader, "fleet_number");
        var registrationNumber = ReadString(reader, "registration_number");
        var siteDescription = ReadString(reader, "site_description");

        return new Logbook
        {
            logbookcode = ReadShort(reader, "logbookcode") ?? 0,
            vmf_code = vmfCode,
            begin_num = ReadString(reader, "begin_num"),
            end_num = ReadString(reader, "end_num"),
            handout_date = ReadDate(reader, "handout_date"),
            site_code = siteCode,
            lb_receiver_name = ReadString(reader, "lb_receiver_name"),
            lb_tel_num = ReadString(reader, "lb_tel_num"),
            lb_comment = ReadString(reader, "lb_comment"),
            date_created = ReadDate(reader, "date_created") ?? default,
            date_updated = ReadDate(reader, "date_updated"),
            created_by_user_code = ReadInt(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt(reader, "modified_by_user_code"),
            is_deleted = ReadBool(reader, "is_deleted"),
            Vehicle =
                vmfCode.HasValue || fleetNumber is not null || registrationNumber is not null
                    ? new Vehicle
                    {
                        vmf_code = vmfCode ?? 0,
                        fleet_number = fleetNumber,
                        registration_number = registrationNumber,
                    }
                    : null,
            Site =
                siteCode.HasValue || siteDescription is not null
                    ? new Site { Site_code = siteCode ?? 0, description = siteDescription }
                    : null,
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

    private sealed record ColumnInfo(string Name, string DataType = "");

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
