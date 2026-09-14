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
/// Traffic department persistence across the legacy and expanded table shapes.
/// The legacy table has Traf_cell; the expanded table has audit columns instead.
/// </summary>
public class TrafficDeptRepository : ITrafficDeptRepository
{
    private static readonly string[] CommonColumns =
    [
        "Traffic_dept_code",
        "Traf_name",
        "Traf_res_person",
        "Traf_post_address1",
        "Traf_post_address2",
        "Traf_post_code",
        "Traf_telephone",
        "Traf_fax",
        "Traf_email",
    ];

    private static readonly string[] OptionalColumns =
    [
        "Traf_cell",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;

    public TrafficDeptRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<TrafficDept?> GetByIdAsync(short deptCode)
    {
        return (
            await QueryAsync(
                "WHERE [Traffic_dept_code] = @deptCode",
                command => AddParameter(command, "@deptCode", DbType.Int16, deptCode)
            )
        ).SingleOrDefault();
    }

    public async Task<IEnumerable<TrafficDept>> GetAllAsync()
    {
        return await QueryAsync();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The paged query is composed only from fixed table/column allowlists and fixed predicates; request values are parameters."
    )]
    public async Task<TrafficDeptPage> GetPageAsync(TrafficDeptPageQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var requestedPage = Math.Max(1, query.Page);
        var searchQuery = query.SearchQuery?.Trim() ?? string.Empty;
        var availableColumns = await GetAvailableColumnsAsync();
        var conditions = new List<string>();

        if (availableColumns.Contains("is_deleted"))
        {
            conditions.Add("ISNULL([d].[is_deleted], 0) = 0");
        }

        if (searchQuery.Length > 0)
        {
            conditions.Add(
                "CHARINDEX(@searchQuery, LOWER(LTRIM(RTRIM(COALESCE([d].[Traf_name], N''))))) > 0"
            );
        }

        var whereClause = conditions.Count == 0 ? "1 = 1" : string.Join(" AND ", conditions);
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var countCommand = connection.CreateCommand();
            countCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            countCommand.CommandText = $"""
                SELECT COUNT(1)
                FROM [dbo].[Traffic_Dept] AS [d]
                WHERE {whereClause}
                """;
            AddSearchParameter(countCommand, searchQuery, searchQuery.Length > 0);
            var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = Math.Min(requestedPage, totalPages);
            var skip = checked((long)(page - 1) * pageSize);

            await using var dataCommand = connection.CreateCommand();
            dataCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            dataCommand.CommandText = $"""
                SELECT {BuildPageProjection(availableColumns)}
                FROM [dbo].[Traffic_Dept] AS [d]
                WHERE {whereClause}
                ORDER BY COALESCE([d].[Traf_name], N''), [d].[Traffic_dept_code]
                OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
                """;
            AddSearchParameter(dataCommand, searchQuery, searchQuery.Length > 0);
            AddParameter(dataCommand, "@skip", DbType.Int64, skip);
            AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

            var items = new List<TrafficDept>();
            await using var reader = await dataCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(MapTrafficDept(reader, availableColumns));
            }

            return new TrafficDeptPage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<TrafficDept?> GetByNameAsync(string name)
    {
        return (
            await QueryAsync(
                "WHERE [Traf_name] = @name",
                command => AddParameter(command, "@name", DbType.String, name)
            )
        ).SingleOrDefault();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list is composed only from fixed common columns and an allowlisted runtime column set; predicates are internal constants and values are parameters."
    )]
    private async Task<List<TrafficDept>> QueryAsync(
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
            command.CommandText = BuildSelectCommandText(availableColumns, predicate);
            configure?.Invoke(command);

            var results = new List<TrafficDept>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapTrafficDept(reader, availableColumns));
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

    public async Task<TrafficDept> CreateAsync(TrafficDept dept, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(dept);

        var availableColumns = await GetAvailableColumnsAsync();
        var values = BuildCommonWriteValues(dept);
        AddOptionalValue(
            values,
            availableColumns,
            "Traf_cell",
            "@trafCell",
            DbType.String,
            dept.Traf_cell
        );
        AddOptionalValue(
            values,
            availableColumns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddOptionalValue(
            values,
            availableColumns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            dept.date_updated
        );
        AddOptionalValue(
            values,
            availableColumns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddOptionalValue(
            values,
            availableColumns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            null
        );
        AddOptionalValue(
            values,
            availableColumns,
            "is_deleted",
            "@isDeleted",
            DbType.Boolean,
            false
        );

        dept.date_created = DateTime.UtcNow;
        dept.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        dept.is_deleted = false;
        dept.Traffic_dept_code = await ExecuteInsertAsync(values);
        return dept;
    }

    public async Task<TrafficDept> UpdateAsync(TrafficDept dept, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(dept);

        var existing = await GetByIdAsync(dept.Traffic_dept_code);
        if (existing == null)
        {
            throw new InvalidOperationException(
                $"TrafficDept with Traffic_dept_code {dept.Traffic_dept_code} not found"
            );
        }

        var availableColumns = await GetAvailableColumnsAsync();
        var values = BuildCommonWriteValues(dept);
        AddOptionalValue(
            values,
            availableColumns,
            "Traf_cell",
            "@trafCell",
            DbType.String,
            dept.Traf_cell ?? existing.Traf_cell
        );

        var now = DateTime.UtcNow;
        AddOptionalValue(
            values,
            availableColumns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            now
        );
        AddOptionalValue(
            values,
            availableColumns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddOptionalValue(
            values,
            availableColumns,
            "is_deleted",
            "@isDeleted",
            DbType.Boolean,
            dept.is_deleted
        );

        await ExecuteUpdateAsync(dept.Traffic_dept_code, values);
        dept.date_updated = now;
        dept.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        return dept;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The legacy DELETE statement is fixed and the department code is parameterized."
    )]
    public async Task DeleteAsync(short deptCode, int currentUserId)
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
            // MNT_Traffic_delete.aspx physically removes the traffic department.
            // Optional modern audit columns must not turn this into a soft delete.
            command.CommandText =
                "DELETE FROM [dbo].[Traffic_Dept] WHERE [Traffic_dept_code] = @deptCode";

            AddParameter(command, "@deptCode", DbType.Int16, deptCode);
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
        Justification = "The INSERT statement is composed only from the fixed allowlisted column/value pairs and every value is parameterized."
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
                $"INSERT INTO [dbo].[Traffic_Dept] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) VALUES ({string.Join(", ", values.Select(value => value.Parameter))}); SELECT CAST(SCOPE_IDENTITY() AS smallint);";
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
        Justification = "The UPDATE statement is composed only from the fixed allowlisted column/value pairs and every value is parameterized."
    )]
    private async Task ExecuteUpdateAsync(short deptCode, IReadOnlyList<WriteValue> values)
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
                $"UPDATE [dbo].[Traffic_Dept] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [Traffic_dept_code] = @deptCode";
            AddParameters(command, values);
            AddParameter(command, "@deptCode", DbType.Int16, deptCode);
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
            AddParameter(command, "@table", DbType.String, "Traffic_Dept");

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
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

    private static string BuildSelectCommandText(
        IReadOnlySet<string> availableColumns,
        string? predicate
    )
    {
        var columns = CommonColumns
            .Concat(OptionalColumns.Where(availableColumns.Contains))
            .Select(column => $"[{column}]");
        return $"SELECT {string.Join(", ", columns)} FROM [dbo].[Traffic_Dept] {predicate}";
    }

    private static string BuildPageProjection(IReadOnlySet<string> availableColumns)
    {
        return string.Join(
            ", ",
            CommonColumns
                .Concat(OptionalColumns.Where(availableColumns.Contains))
                .Select(column => $"[d].[{column}] AS [{column}]")
        );
    }

    private static void AddSearchParameter(DbCommand command, string searchQuery, bool include)
    {
        if (include)
        {
            AddParameter(command, "@searchQuery", DbType.String, searchQuery.ToLowerInvariant());
        }
    }

    private static TrafficDept MapTrafficDept(
        DbDataReader reader,
        IReadOnlySet<string> availableColumns
    )
    {
        return new TrafficDept
        {
            Traffic_dept_code = ReadInt16(reader, "Traffic_dept_code") ?? 0,
            Traf_name = ReadString(reader, "Traf_name"),
            Traf_res_person = ReadString(reader, "Traf_res_person"),
            Traf_post_address1 = ReadString(reader, "Traf_post_address1"),
            Traf_post_address2 = ReadString(reader, "Traf_post_address2"),
            Traf_post_code = ReadString(reader, "Traf_post_code"),
            Traf_telephone = ReadString(reader, "Traf_telephone"),
            Traf_fax = ReadString(reader, "Traf_fax"),
            Traf_cell = ReadStringIfAvailable(reader, availableColumns, "Traf_cell"),
            Traf_email = ReadString(reader, "Traf_email"),
            date_created =
                ReadDateTimeIfAvailable(reader, availableColumns, "date_created")
                ?? DateTime.MinValue,
            date_updated = ReadDateTimeIfAvailable(reader, availableColumns, "date_updated"),
            created_by_user_code = ReadInt32IfAvailable(
                reader,
                availableColumns,
                "created_by_user_code"
            ),
            modified_by_user_code = ReadInt32IfAvailable(
                reader,
                availableColumns,
                "modified_by_user_code"
            ),
            is_deleted = ReadBooleanIfAvailable(reader, availableColumns, "is_deleted") ?? false,
        };
    }

    private static List<WriteValue> BuildCommonWriteValues(TrafficDept dept)
    {
        return
        [
            new("Traf_name", "@trafName", DbType.String, dept.Traf_name),
            new("Traf_res_person", "@trafResPerson", DbType.String, dept.Traf_res_person),
            new("Traf_post_address1", "@trafPostAddress1", DbType.String, dept.Traf_post_address1),
            new("Traf_post_address2", "@trafPostAddress2", DbType.String, dept.Traf_post_address2),
            new("Traf_post_code", "@trafPostCode", DbType.String, dept.Traf_post_code),
            new("Traf_telephone", "@trafTelephone", DbType.String, dept.Traf_telephone),
            new("Traf_fax", "@trafFax", DbType.String, dept.Traf_fax),
            new("Traf_email", "@trafEmail", DbType.String, dept.Traf_email),
        ];
    }

    private static void AddOptionalValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (availableColumns.Contains(column))
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

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static string? ReadStringIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    )
    {
        return columns.Contains(column) ? ReadString(reader, column) : null;
    }

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }

    private static int? ReadInt32IfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    )
    {
        if (!columns.Contains(column))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTimeIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    )
    {
        if (!columns.Contains(column))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static bool? ReadBooleanIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    )
    {
        if (!columns.Contains(column))
        {
            return null;
        }

        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
