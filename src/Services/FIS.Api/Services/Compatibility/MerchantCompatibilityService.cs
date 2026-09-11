using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Api.Services;

/// <summary>
/// Pages the legacy Merchant lookup without requiring the optional audit
/// columns that may only exist in the expanded database schema.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "The command text uses fixed table and column names; search and paging values are parameterized."
)]
public sealed class MerchantCompatibilityService
{
    private const string TableName = "Merchant";

    private static readonly string[] RequiredColumns = ["Merchant_code", "Merchant_Name"];

    private readonly FisDbContext _context;

    public MerchantCompatibilityService(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<MerchantPage> GetPageAsync(
        string? search,
        int page = 1,
        int pageSize = 24,
        CancellationToken cancellationToken = default
    )
    {
        var columns = await GetAvailableColumnsAsync(cancellationToken);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var normalizedSearch = search?.Trim();

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            return await ReadPageAsync(
                connection,
                columns,
                normalizedSearch,
                page,
                pageSize,
                cancellationToken
            );
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<MerchantPage> ReadPageAsync(
        DbConnection connection,
        IReadOnlySet<string> columns,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken
    )
    {
        var predicates = BuildPredicates(columns, search);
        var whereClause = string.Join(" AND ", predicates);
        var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

        await using var countCommand = connection.CreateCommand();
        countCommand.Transaction = transaction;
        countCommand.CommandText = $"""
            SELECT COUNT(*)
            FROM [dbo].[{TableName}]
            WHERE {whereClause}
            """;
        AddSearchParameter(countCommand, search);

        var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync(cancellationToken));
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);
        var offset = checked((long)(page - 1) * pageSize);

        await using var pageCommand = connection.CreateCommand();
        pageCommand.Transaction = transaction;
        pageCommand.CommandText = $"""
            SELECT [Merchant_code] AS [Merchant_code],
                   [Merchant_Name] AS [Merchant_Name]
            FROM [dbo].[{TableName}]
            WHERE {whereClause}
            ORDER BY [Merchant_Name], [Merchant_code]
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY
            """;
        AddSearchParameter(pageCommand, search);
        AddParameter(pageCommand, "@offset", DbType.Int64, offset);
        AddParameter(pageCommand, "@pageSize", DbType.Int32, pageSize);

        var items = new List<MerchantOption>();
        await using var reader = await pageCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(
                new MerchantOption(
                    Convert.ToInt32(reader.GetValue(reader.GetOrdinal("Merchant_code"))),
                    ReadString(reader, "Merchant_Name")
                )
            );
        }

        return new MerchantPage(items, page, pageSize, total);
    }

    private static List<string> BuildPredicates(IReadOnlySet<string> columns, string? search)
    {
        var predicates = new List<string> { GetActiveFilter(columns) };
        if (!string.IsNullOrWhiteSpace(search))
        {
            predicates.Add("[Merchant_Name] LIKE @search ESCAPE '\\'");
        }

        return predicates;
    }

    private static void AddSearchParameter(DbCommand command, string? search)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            AddParameter(
                command,
                "@search",
                DbType.String,
                $"%{EscapeLikePattern(search.Trim())}%"
            );
        }
    }

    private async Task<HashSet<string>> GetAvailableColumnsAsync(
        CancellationToken cancellationToken
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
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
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(0));
            }

            var missingColumns = RequiredColumns
                .Where(column => !columns.Contains(column))
                .ToArray();
            if (missingColumns.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The required Merchant compatibility columns are not available: {string.Join(", ", missingColumns)}"
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

    private static string GetActiveFilter(IReadOnlySet<string> columns) =>
        columns.Contains("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal))?.TrimEnd();
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}

public sealed record MerchantOption(int MerchantCode, string? MerchantName);

public sealed record MerchantPage(
    IReadOnlyList<MerchantOption> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}
