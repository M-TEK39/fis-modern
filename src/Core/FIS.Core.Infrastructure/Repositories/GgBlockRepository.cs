using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using FIS.Core.Application.Interfaces;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads and writes GG block ranges against both generations of the legacy
/// schema. The table and column names are fixed; only their presence is
/// negotiated at runtime so a client database without the expanded objects
/// remains usable.
/// </summary>
public sealed class GgBlockRepository : IGgBlockRepository
{
    private const string ModernTableName = "GG_Block";
    private const string LegacyTableName = "GG_Blocks";
    private const string NumberTableName = "block_gg_numbers";
    private const string UserProfileTableName = "user_access_old1";

    private static readonly string[] RequiredBlockColumns =
    [
        "Block_ID",
        "Creation_Date",
        "Created_By_User_Code",
        "Vch_Start_Reg",
        "Vch_End_Reg",
    ];

    private static readonly string[] RequiredNumberColumns =
    [
        "Block_ID",
        "Creation_Date",
        "Created_By_User_Code",
        "GG_Number",
    ];

    private static readonly Regex GgNumberPattern = new(
        "^[A-Z]{3}[0-9]{3}G$",
        RegexOptions.CultureInvariant
    );

    private readonly FisDbContext _context;

    public GgBlockRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<GgBlockHistoryPage> GetHistoryAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var blockTables = await GetUsableBlockTablesAsync(connection, null);
            EnsureBlockTableAvailable(blockTables);
            var userProfileColumns = await GetOptionalUserProfileColumnsAsync(connection, null);
            var history = new List<GgBlockHistoryRecord>();

            foreach (var table in blockTables)
            {
                await ReadHistoryAsync(connection, null, table, userProfileColumns, history);
            }

            var ordered = history
                .OrderByDescending(item => item.DateCreated ?? DateTime.MinValue)
                .ThenByDescending(item => item.StartGgNumber, StringComparer.Ordinal)
                .ToArray();
            var totalRecords = ordered.Length;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize));
            page = Math.Min(page, totalPages);
            var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArray();

            return new GgBlockHistoryPage(items, page, pageSize, totalRecords);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<GgBlockHistoryRecord> CreateAsync(
        string startGgNumber,
        string endGgNumber,
        int currentUserId
    )
    {
        var (startPrefix, startNumber, startSuffix) = ParseGgNumber(startGgNumber);
        var (endPrefix, endNumber, endSuffix) = ParseGgNumber(endGgNumber);
        if (
            !string.Equals(startPrefix, endPrefix, StringComparison.Ordinal)
            || startSuffix != endSuffix
        )
        {
            throw new ArgumentException(
                "The start and end GG numbers must use the same prefix and suffix."
            );
        }

        if (endNumber <= startNumber)
        {
            throw new ArgumentException(
                "The end GG number must be greater than the start GG number."
            );
        }

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        await using var transaction = await connection.BeginTransactionAsync(
            IsolationLevel.Serializable
        );
        try
        {
            var blockTables = await GetUsableBlockTablesAsync(connection, transaction);
            EnsureBlockTableAvailable(blockTables);
            var numberColumns = await GetAvailableColumnsAsync(
                connection,
                NumberTableName,
                transaction
            );
            EnsureRequiredColumns(numberColumns, RequiredNumberColumns, NumberTableName);

            if (
                await HasOverlappingRangeAsync(
                    connection,
                    transaction,
                    blockTables,
                    startGgNumber.Trim().ToUpperInvariant(),
                    endGgNumber.Trim().ToUpperInvariant()
                )
            )
            {
                throw new GgBlockRangeConflictException();
            }

            var selectedTable = blockTables[0];
            var now = DateTime.UtcNow;
            var blockId = await InsertBlockAsync(
                connection,
                transaction,
                selectedTable,
                startGgNumber.Trim().ToUpperInvariant(),
                endGgNumber.Trim().ToUpperInvariant(),
                currentUserId,
                now
            );

            await InsertGeneratedNumbersAsync(
                connection,
                transaction,
                numberColumns,
                blockId,
                startPrefix,
                startNumber,
                endNumber,
                startSuffix.ToString(),
                currentUserId,
                now
            );

            await transaction.CommitAsync();

            return new GgBlockHistoryRecord(
                blockId,
                currentUserId > 0 ? $"User {currentUserId}" : "Current User",
                now,
                startGgNumber.Trim().ToUpperInvariant(),
                endGgNumber.Trim().ToUpperInvariant()
            );
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
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
        Justification = "The table name and optional filter are selected from fixed compatibility values; there is no user-controlled SQL identifier and no unparameterized value."
    )]
    private static async Task ReadHistoryAsync(
        DbConnection connection,
        DbTransaction? transaction,
        BlockTable table,
        IReadOnlySet<string>? userProfileColumns,
        ICollection<GgBlockHistoryRecord> history
    )
    {
        var capturedBy = userProfileColumns is not null
            ? "COALESCE(NULLIF(LTRIM(RTRIM((SELECT TOP (1) CONVERT(nvarchar(255), u.[name]) FROM [dbo].[user_access_old1] AS u WHERE u.[user_access_code] = b.[Created_By_User_Code]))), ''), CONVERT(nvarchar(50), b.[Created_By_User_Code]))"
            : "CONVERT(nvarchar(50), b.[Created_By_User_Code])";
        var notDeleted = table.Columns.Contains("is_deleted")
            ? " AND (b.[is_deleted] = 0 OR b.[is_deleted] IS NULL)"
            : string.Empty;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT
                b.[Block_ID],
                {capturedBy} AS [CapturedBy],
                b.[Creation_Date],
                b.[Vch_Start_Reg],
                b.[Vch_End_Reg]
            FROM [dbo].[{table.Name}] AS b
            WHERE 1 = 1{notDeleted}
            """;

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            history.Add(
                new GgBlockHistoryRecord(
                    Convert.ToInt16(reader["Block_ID"]),
                    ReadString(reader, "CapturedBy") ?? "-",
                    ReadDateTime(reader, "Creation_Date"),
                    ReadString(reader, "Vch_Start_Reg") ?? "-",
                    ReadString(reader, "Vch_End_Reg") ?? "-"
                )
            );
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The table name and optional filter are selected from fixed compatibility values; range values are parameterized."
    )]
    private static async Task<bool> HasOverlappingRangeAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlyList<BlockTable> blockTables,
        string startGgNumber,
        string endGgNumber
    )
    {
        foreach (var table in blockTables)
        {
            var notDeleted = table.Columns.Contains("is_deleted")
                ? " AND ([is_deleted] = 0 OR [is_deleted] IS NULL)"
                : string.Empty;

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
                SELECT COUNT(1)
                FROM [dbo].[{table.Name}]
                WHERE [Vch_Start_Reg] <= @endGgNumber
                  AND [Vch_End_Reg] >= @startGgNumber{notDeleted}
                """;
            AddParameter(command, "@startGgNumber", DbType.String, startGgNumber);
            AddParameter(command, "@endGgNumber", DbType.String, endGgNumber);

            if (Convert.ToInt32(await command.ExecuteScalarAsync()) > 0)
            {
                return true;
            }
        }

        return false;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The table name is selected from fixed compatibility candidates and all values are parameters."
    )]
    private static async Task<short> InsertBlockAsync(
        DbConnection connection,
        DbTransaction transaction,
        BlockTable table,
        string startGgNumber,
        string endGgNumber,
        int currentUserId,
        DateTime now
    )
    {
        var values = new List<WriteValue>
        {
            new("Creation_Date", "@creationDate", DbType.DateTime2, now),
            new("Created_By_User_Code", "@createdByUserCode", DbType.Int32, currentUserId),
            new("Vch_Start_Reg", "@startGgNumber", DbType.String, startGgNumber),
            new("Vch_End_Reg", "@endGgNumber", DbType.String, endGgNumber),
        };

        AddOptionalValue(
            values,
            table.Columns,
            "Modified_User_Code",
            "@modifiedUserCode",
            DbType.Int32,
            currentUserId
        );
        AddOptionalValue(
            values,
            table.Columns,
            "audit_date_created",
            "@auditDateCreated",
            DbType.DateTime2,
            now
        );
        AddOptionalValue(
            values,
            table.Columns,
            "audit_created_by_user_code",
            "@auditCreatedByUserCode",
            DbType.Int32,
            currentUserId
        );
        AddOptionalValue(values, table.Columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"INSERT INTO [dbo].[{table.Name}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[Block_ID] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        return Convert.ToInt16(await command.ExecuteScalarAsync());
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The table name and optional columns are selected from fixed compatibility values and all values are parameters."
    )]
    private static async Task InsertGeneratedNumbersAsync(
        DbConnection connection,
        DbTransaction transaction,
        IReadOnlySet<string> numberColumns,
        short blockId,
        string prefix,
        int startNumber,
        int endNumber,
        string suffix,
        int currentUserId,
        DateTime now
    )
    {
        var columns = new List<string>
        {
            "[Block_ID]",
            "[Creation_Date]",
            "[Created_By_User_Code]",
            "[GG_Number]",
        };
        var selections = new List<string>
        {
            "@blockId",
            "@creationDate",
            "@createdByUserCode",
            "CONCAT(@prefix, RIGHT('000' + CONVERT(varchar(3), Number), 3), @suffix)",
        };

        AddOptionalInsertSelection(
            numberColumns,
            "audit_date_created",
            "[audit_date_created]",
            "@auditDateCreated",
            columns,
            selections
        );
        AddOptionalInsertSelection(
            numberColumns,
            "audit_created_by_user_code",
            "[audit_created_by_user_code]",
            "@auditCreatedByUserCode",
            columns,
            selections
        );
        AddOptionalInsertSelection(
            numberColumns,
            "is_deleted",
            "[is_deleted]",
            "@isDeleted",
            columns,
            selections
        );

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            ;WITH Numbers AS
            (
                SELECT @startNumber AS Number
                UNION ALL
                SELECT Number + 1
                FROM Numbers
                WHERE Number < @endNumber
            )
            INSERT INTO [dbo].[{NumberTableName}] ({string.Join(", ", columns)})
            SELECT {string.Join(", ", selections)}
            FROM Numbers
            OPTION (MAXRECURSION 1000)
            """;
        AddParameter(command, "@blockId", DbType.Int16, blockId);
        AddParameter(command, "@creationDate", DbType.DateTime2, now);
        AddParameter(command, "@createdByUserCode", DbType.Int32, currentUserId);
        AddParameter(command, "@prefix", DbType.String, prefix);
        AddParameter(command, "@suffix", DbType.String, suffix);
        AddParameter(command, "@startNumber", DbType.Int32, startNumber);
        AddParameter(command, "@endNumber", DbType.Int32, endNumber);
        if (numberColumns.Contains("audit_date_created"))
        {
            AddParameter(command, "@auditDateCreated", DbType.DateTime2, now);
        }

        if (numberColumns.Contains("audit_created_by_user_code"))
        {
            AddParameter(command, "@auditCreatedByUserCode", DbType.Int32, currentUserId);
        }

        if (numberColumns.Contains("is_deleted"))
        {
            AddParameter(command, "@isDeleted", DbType.Boolean, false);
        }

        await command.ExecuteNonQueryAsync();
    }

    private static void AddOptionalInsertSelection(
        IReadOnlySet<string> availableColumns,
        string column,
        string sqlColumn,
        string selection,
        ICollection<string> columns,
        ICollection<string> selections
    )
    {
        if (!availableColumns.Contains(column))
        {
            return;
        }

        columns.Add(sqlColumn);
        selections.Add(selection);
    }

    private static async Task<List<BlockTable>> GetUsableBlockTablesAsync(
        DbConnection connection,
        DbTransaction? transaction
    )
    {
        var tables = new List<BlockTable>();
        foreach (var tableName in new[] { ModernTableName, LegacyTableName })
        {
            var columns = await GetAvailableColumnsAsync(connection, tableName, transaction);
            if (RequiredBlockColumns.All(columns.Contains))
            {
                tables.Add(new BlockTable(tableName, columns));
            }
        }

        return tables;
    }

    private static async Task<HashSet<string>> GetAvailableColumnsAsync(
        DbConnection connection,
        string tableName,
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
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, tableName);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static async Task<HashSet<string>?> GetOptionalUserProfileColumnsAsync(
        DbConnection connection,
        DbTransaction? transaction
    )
    {
        var columns = await GetAvailableColumnsAsync(connection, UserProfileTableName, transaction);
        return columns.Contains("name") && columns.Contains("user_access_code") ? columns : null;
    }

    private static void EnsureBlockTableAvailable(IReadOnlyCollection<BlockTable> tables)
    {
        if (tables.Count == 0)
        {
            throw new InvalidOperationException("Neither supported GG block table is available.");
        }
    }

    private static void EnsureRequiredColumns(
        IReadOnlySet<string> availableColumns,
        IEnumerable<string> requiredColumns,
        string tableName
    )
    {
        var missingColumns = requiredColumns
            .Where(column => !availableColumns.Contains(column))
            .ToArray();
        if (missingColumns.Length > 0)
        {
            throw new InvalidOperationException(
                $"The required GG block compatibility columns are not available in {tableName}: {string.Join(", ", missingColumns)}"
            );
        }
    }

    private static (string Prefix, int Number, char Suffix) ParseGgNumber(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        if (!GgNumberPattern.IsMatch(normalized))
        {
            throw new ArgumentException("GG numbers must use the format ABC123G.");
        }

        return (normalized[..3], int.Parse(normalized[3..6]), normalized[6]);
    }

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

    private static string? ReadString(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : reader[column]?.ToString()?.Trim();

    private static DateTime? ReadDateTime(DbDataReader reader, string column) =>
        reader[column] is DBNull ? null : Convert.ToDateTime(reader[column]);

    private sealed record BlockTable(string Name, HashSet<string> Columns);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
