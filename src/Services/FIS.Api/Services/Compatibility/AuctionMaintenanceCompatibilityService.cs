using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Api.Services;

/// <summary>
/// Pages the legacy auction-maintenance grid without assuming that optional
/// audit fields or vehicle presentation columns exist on the client database.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "The command text uses fixed compatibility allowlists and parameterized values."
)]
public sealed class AuctionMaintenanceCompatibilityService
{
    private const string AuctionTableName = "auction";
    private const string VehicleTableName = "vehicle_master";

    private static readonly string[] RequiredAuctionColumns =
    [
        "auction_code",
        "vmf_code",
        "auction_number",
        "camp",
        "auction_garage",
        "auth_date",
    ];

    private readonly FisDbContext _context;

    public AuctionMaintenanceCompatibilityService(FisDbContext context)
    {
        _context = context;
    }

    public async Task<AuctionMaintenancePage> GetPageAsync(
        string? searchTerm,
        bool searchByRegistration,
        int page = 1,
        int pageSize = 24,
        CancellationToken cancellationToken = default
    )
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var normalizedSearch = searchTerm?.Trim();
        var auctionColumns = await GetAvailableColumnsAsync(
            AuctionTableName,
            RequiredAuctionColumns,
            cancellationToken
        );
        var vehicleColumns = await GetAvailableColumnsAsync(
            VehicleTableName,
            ["vmf_code"],
            cancellationToken
        );
        var searchColumn = searchByRegistration ? "registration_number" : "fleet_number";

        if (!string.IsNullOrWhiteSpace(normalizedSearch) && !vehicleColumns.Contains(searchColumn))
        {
            return new AuctionMaintenancePage([], 1, pageSize, 0);
        }

        var predicates = new List<string> { GetActiveFilter(auctionColumns) };
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            predicates.Add($"[v].[{searchColumn}] LIKE @search ESCAPE '\\'");
        }

        var whereClause = string.Join(" AND ", predicates);
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            await using var countCommand = connection.CreateCommand();
            countCommand.Transaction = transaction;
            countCommand.CommandText = $"""
                SELECT COUNT(*)
                FROM [dbo].[{AuctionTableName}] AS [a]
                LEFT JOIN [dbo].[{VehicleTableName}] AS [v]
                    ON [v].[vmf_code] = [a].[vmf_code]
                WHERE {whereClause}
                """;
            AddSearchParameter(countCommand, normalizedSearch);
            var total = Convert.ToInt32(
                await countCommand.ExecuteScalarAsync(cancellationToken),
                System.Globalization.CultureInfo.InvariantCulture
            );
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Min(page, totalPages);
            var offset = checked((long)(page - 1) * pageSize);

            await using var pageCommand = connection.CreateCommand();
            pageCommand.Transaction = transaction;
            pageCommand.CommandText = $"""
                SELECT
                    [a].[auction_code] AS [auction_code],
                    [a].[vmf_code] AS [vmf_code],
                    [a].[auction_number] AS [auction_number],
                    [a].[camp] AS [camp],
                    [a].[auction_garage] AS [auction_garage],
                    {GetVehicleProjection(vehicleColumns, "fleet_number")},
                    {GetVehicleProjection(vehicleColumns, "registration_number")}
                FROM [dbo].[{AuctionTableName}] AS [a]
                LEFT JOIN [dbo].[{VehicleTableName}] AS [v]
                    ON [v].[vmf_code] = [a].[vmf_code]
                WHERE {whereClause}
                ORDER BY [a].[auth_date] DESC, [a].[auction_code] DESC
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY
                """;
            AddSearchParameter(pageCommand, normalizedSearch);
            AddParameter(pageCommand, "@offset", DbType.Int64, offset);
            AddParameter(pageCommand, "@pageSize", DbType.Int32, pageSize);

            var items = new List<AuctionMaintenanceRow>();
            await using var reader = await pageCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(
                    new AuctionMaintenanceRow(
                        Convert.ToInt16(reader.GetValue(reader.GetOrdinal("auction_code"))),
                        Convert.ToInt32(reader.GetValue(reader.GetOrdinal("vmf_code"))),
                        ReadString(reader, "auction_number"),
                        ReadString(reader, "camp"),
                        ReadInt16(reader, "auction_garage"),
                        ReadString(reader, "fleet_number"),
                        ReadString(reader, "registration_number")
                    )
                );
            }

            return new AuctionMaintenancePage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<HashSet<string>> GetAvailableColumnsAsync(
        string tableName,
        IReadOnlyCollection<string> requiredColumns,
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
            AddParameter(command, "@table", DbType.String, tableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(0));
            }

            var missingColumns = requiredColumns
                .Where(column => !columns.Contains(column))
                .ToArray();
            if (missingColumns.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The required Auction maintenance columns are not available on {tableName}: {string.Join(", ", missingColumns)}"
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

    private static void AddSearchParameter(DbCommand command, string? searchTerm)
    {
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            AddParameter(command, "@search", DbType.String, $"%{EscapeLikePattern(searchTerm)}%");
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

    private static string GetActiveFilter(IReadOnlySet<string> columns) =>
        columns.Contains("is_deleted") ? "ISNULL([a].[is_deleted], 0) = 0" : "1 = 1";

    private static string GetVehicleProjection(IReadOnlySet<string> columns, string column) =>
        columns.Contains(column)
            ? $"[v].[{column}] AS [{column}]"
            : $"CAST(NULL AS varchar(1)) AS [{column}]";

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal))?.TrimEnd();
    }

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }
}

public sealed record AuctionMaintenanceRow(
    short AuctionCode,
    int VmfCode,
    string? AuctionNumber,
    string? Camp,
    short? AuctionGarage,
    string? FleetNumber,
    string? RegistrationNumber
);

public sealed record AuctionMaintenancePage(
    IReadOnlyList<AuctionMaintenanceRow> Items,
    int Page,
    int PageSize,
    int Total
)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
}
