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
/// Persists the original Auction workflow against both the client schema and
/// the expanded schema. Auction's audit columns are optional, and the legacy
/// maintenance form also edits four fields on vehicle_master, so EF
/// materialization would either omit business data or fail on the client DB.
/// Runtime column inspection keeps the original tables and fields authoritative.
/// </summary>
public class AuctionRepository : IAuctionRepository
{
    private const string AuctionTableName = "auction";
    private const string VehicleTableName = "vehicle_master";

    private static readonly string[] LegacyColumns =
    [
        "auction_code",
        "vmf_code",
        "auction_number",
        "camp",
        "lot",
        "auction_garage",
        "auth_number",
        "auth_date",
        "auction_km",
        "garage_owner",
        "reason_sold",
        "estimate_amount",
        "reserve_amount",
        "sold_id",
        "remark"
    ];

    private static readonly string[] OptionalColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted"
    ];

    private static readonly string[] VehicleProjectionColumns =
    [
        "fleet_number",
        "registration_number",
        "barcode",
        "sold_to",
        "sold_date",
        "sold_amount"
    ];

    private static readonly string[] RequiredAuctionColumns =
    [
        "auction_code",
        "vmf_code",
        "auction_number",
        "camp",
        "lot",
        "auction_garage",
        "auth_number",
        "auth_date",
        "auction_km",
        "garage_owner",
        "reason_sold",
        "estimate_amount",
        "reserve_amount",
        "sold_id",
        "remark"
    ];

    private readonly FisDbContext _context;

    public AuctionRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Auction?> GetByIdAsync(short auctionCode)
        => (await QueryAsync(
            "WHERE [a].[auction_code] = @auctionCode",
            command => AddParameter(command, "@auctionCode", DbType.Int16, auctionCode)))
            .SingleOrDefault();

    public async Task<IEnumerable<Auction>> GetAllAsync()
        => await QueryAsync();

    public async Task<IEnumerable<Auction>> GetByVehicleAsync(int vmfCode)
        => await QueryAsync(
            "WHERE [a].[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode));

    public async Task<Auction> CreateAsync(Auction auction, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(auction);

        var availableColumns = await GetAvailableColumnsAsync(AuctionTableName);
        var values = BuildAuctionWriteValues(auction)
            .Where(value => availableColumns.Contains(value.Column))
            .ToList();
        var now = DateTime.UtcNow;

        AddOptionalValue(values, availableColumns, "date_created", "@dateCreated", DbType.DateTime2, now);
        AddOptionalValue(
            values,
            availableColumns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null);
        AddOptionalValue(values, availableColumns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        auction.auction_code = await ExecuteInsertAsync(values);
        auction.date_created = now;
        auction.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        auction.is_deleted = false;
        return auction;
    }

    public async Task<Auction> UpdateAsync(Auction auction, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(auction);
        return await UpdateAuctionAsync(auction, currentUserId);
    }

    public async Task<Auction> UpdateMaintenanceAsync(Auction auction, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(auction);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var updated = await UpdateAuctionAsync(auction, currentUserId);
            await UpdateVehicleMaintenanceAsync(auction);
            await transaction.CommitAsync();
            return updated;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The DELETE statement is fixed SQL and uses a parameter for the record identifier.")]
    public async Task DeleteAsync(short auctionCode, int currentUserId)
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
            command.CommandText = $"DELETE FROM [dbo].[{AuctionTableName}] WHERE [auction_code] = @auctionCode";
            AddParameter(command, "@auctionCode", DbType.Int16, auctionCode);
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
        Justification = "The SELECT list and filters are composed only from fixed legacy columns and allowlisted optional columns; values are parameters.")]
    private async Task<List<Auction>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null)
    {
        var availableColumns = await GetAvailableColumnsAsync(AuctionTableName, RequiredAuctionColumns);
        var vehicleColumns = await GetAvailableColumnsAsync(VehicleTableName);
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
            var projection = LegacyColumns
                .Select(column => $"[a].[{column}] AS [{column}]")
                .Concat(OptionalColumns.Select(column => GetOptionalProjection(availableColumns, column, "a")))
                .Concat(VehicleProjectionColumns.Select(column => GetColumnProjection(vehicleColumns, column, "v")))
                .ToArray();
            var whereClause = string.IsNullOrWhiteSpace(predicate)
                ? $"WHERE {GetActiveFilter(availableColumns, "a")}"
                : $"{predicate} AND {GetActiveFilter(availableColumns, "a")}";

            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{AuctionTableName}] AS [a]
                LEFT JOIN [dbo].[{VehicleTableName}] AS [v]
                    ON [v].[vmf_code] = [a].[vmf_code]
                {whereClause}
                ORDER BY [a].[auth_date] DESC, [a].[auction_code] DESC
                """;
            configure?.Invoke(command);

            var results = new List<Auction>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapAuction(reader, availableColumns));
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

    private async Task<Auction> UpdateAuctionAsync(Auction auction, int currentUserId)
    {
        var existing = await GetByIdAsync(auction.auction_code);
        if (existing == null)
        {
            throw new InvalidOperationException(
                $"Auction with auction_code {auction.auction_code} not found");
        }

        var availableColumns = await GetAvailableColumnsAsync(AuctionTableName);
        var values = BuildAuctionWriteValues(auction)
            .Where(value => availableColumns.Contains(value.Column))
            .ToList();
        var now = DateTime.UtcNow;

        AddOptionalValue(values, availableColumns, "date_updated", "@dateUpdated", DbType.DateTime2, now);
        AddOptionalValue(
            values,
            availableColumns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null);
        AddOptionalValue(values, availableColumns, "is_deleted", "@isDeleted", DbType.Boolean, auction.is_deleted);

        await ExecuteUpdateAsync(auction.auction_code, values);
        auction.date_created = existing.date_created;
        auction.created_by_user_code = existing.created_by_user_code;
        auction.date_updated = now;
        auction.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        return auction;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The UPDATE statement is composed only from fixed legacy vehicle columns; values are parameters.")]
    private async Task UpdateVehicleMaintenanceAsync(Auction auction)
    {
        var availableColumns = await GetAvailableColumnsAsync(VehicleTableName);
        var values = BuildVehicleWriteValues(auction)
            .Where(value => availableColumns.Contains(value.Column))
            .ToList();

        if (availableColumns.Contains("vehicle_status_code") && !string.IsNullOrWhiteSpace(auction.sold_to))
        {
            values.Add(new WriteValue("vehicle_status_code", "@vehicleStatusCode", DbType.Int16, 5));
        }

        if (availableColumns.Contains("vehicle_status_date") && auction.sold_date.HasValue)
        {
            values.Add(new WriteValue(
                "vehicle_status_date",
                "@vehicleStatusDate",
                DbType.DateTime2,
                auction.sold_date.Value));
        }

        if (values.Count == 0)
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
            command.CommandText = $"""
                UPDATE [dbo].[{VehicleTableName}]
                SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))}
                WHERE [vmf_code] = @vmfCode
                """;
            AddParameters(command, values);
            AddParameter(command, "@vmfCode", DbType.Int32, auction.vmf_code);
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
        Justification = "The INSERT statement is composed only from fixed allowlisted column/value pairs and every value is parameterized.")]
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
            command.CommandText = $"""
                INSERT INTO [dbo].[{AuctionTableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))})
                OUTPUT INSERTED.[auction_code]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
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
        Justification = "The UPDATE statement is composed only from fixed allowlisted column/value pairs and every value is parameterized.")]
    private async Task ExecuteUpdateAsync(short auctionCode, IReadOnlyList<WriteValue> values)
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
            command.CommandText = $"""
                UPDATE [dbo].[{AuctionTableName}]
                SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))}
                WHERE [auction_code] = @auctionCode
                """;
            AddParameters(command, values);
            AddParameter(command, "@auctionCode", DbType.Int16, auctionCode);
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

    private async Task<HashSet<string>> GetAvailableColumnsAsync(
        string tableName,
        IReadOnlyCollection<string>? requiredColumns = null)
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
            AddParameter(command, "@table", DbType.String, tableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }

            if (requiredColumns is not null)
            {
                var missingColumns = requiredColumns.Where(column => !columns.Contains(column)).ToArray();
                if (missingColumns.Length > 0)
                {
                    throw new InvalidOperationException(
                        $"The required Auction compatibility columns are not available on {tableName}: {string.Join(", ", missingColumns)}");
                }
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

    private static Auction MapAuction(
        DbDataReader reader,
        IReadOnlySet<string> auctionColumns)
    {
        var authDate = ReadDateTime(reader, "auth_date");
        return new Auction
        {
            auction_code = ReadInt16(reader, "auction_code") ?? 0,
            vmf_code = ReadInt32(reader, "vmf_code") ?? 0,
            auction_number = ReadString(reader, "auction_number"),
            camp = ReadString(reader, "camp"),
            lot = ReadDecimal(reader, "lot"),
            auction_garage = ReadInt16(reader, "auction_garage"),
            auth_number = ReadString(reader, "auth_number"),
            auth_date = authDate,
            auction_km = ReadDecimal(reader, "auction_km"),
            garage_owner = ReadString(reader, "garage_owner"),
            reason_sold = ReadString(reader, "reason_sold"),
            estimate_amount = ReadDecimal(reader, "estimate_amount"),
            reserve_amount = ReadDecimal(reader, "reserve_amount"),
            sold_id = ReadString(reader, "sold_id"),
            remark = ReadString(reader, "remark"),
            barcode = ReadString(reader, "barcode"),
            sold_to = ReadString(reader, "sold_to"),
            sold_date = ReadDateTime(reader, "sold_date"),
            sold_amount = ReadDecimal(reader, "sold_amount"),
            date_created = ReadDateTimeIfAvailable(reader, auctionColumns, "date_created")
                ?? authDate
                ?? DateTime.MinValue,
            date_updated = ReadDateTimeIfAvailable(reader, auctionColumns, "date_updated"),
            created_by_user_code = ReadInt32IfAvailable(reader, auctionColumns, "created_by_user_code"),
            modified_by_user_code = ReadInt32IfAvailable(reader, auctionColumns, "modified_by_user_code"),
            is_deleted = ReadBooleanIfAvailable(reader, auctionColumns, "is_deleted") ?? false
        };
    }

    private static List<WriteValue> BuildAuctionWriteValues(Auction auction)
        =>
        [
            new("vmf_code", "@vmfCode", DbType.Int32, auction.vmf_code),
            new("auction_number", "@auctionNumber", DbType.String, auction.auction_number),
            new("camp", "@camp", DbType.String, auction.camp),
            new("lot", "@lot", DbType.Decimal, auction.lot),
            new("auction_garage", "@auctionGarage", DbType.Int16, auction.auction_garage),
            new("auth_number", "@authNumber", DbType.String, auction.auth_number),
            new("auth_date", "@authDate", DbType.DateTime2, auction.auth_date),
            new("auction_km", "@auctionKm", DbType.Decimal, auction.auction_km),
            new("garage_owner", "@garageOwner", DbType.String, auction.garage_owner),
            new("reason_sold", "@reasonSold", DbType.String, auction.reason_sold),
            new("estimate_amount", "@estimateAmount", DbType.Decimal, auction.estimate_amount),
            new("reserve_amount", "@reserveAmount", DbType.Decimal, auction.reserve_amount),
            new("sold_id", "@soldId", DbType.String, auction.sold_id),
            new("remark", "@remark", DbType.String, auction.remark)
        ];

    private static List<WriteValue> BuildVehicleWriteValues(Auction auction)
        =>
        [
            new("barcode", "@barcode", DbType.String, auction.barcode),
            new("sold_to", "@soldTo", DbType.String, auction.sold_to),
            new("sold_date", "@soldDate", DbType.DateTime2, auction.sold_date),
            new("sold_amount", "@soldAmount", DbType.Decimal, auction.sold_amount)
        ];

    private static void AddOptionalValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        string column,
        string parameter,
        DbType type,
        object? value)
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

    private static string GetActiveFilter(IReadOnlySet<string> availableColumns, string alias)
        => availableColumns.Contains("is_deleted")
            ? $"ISNULL([{alias}].[is_deleted], 0) = 0"
            : "1 = 1";

    private static string GetOptionalProjection(
        IReadOnlySet<string> columns,
        string column,
        string alias)
    {
        if (columns.Contains(column))
        {
            return $"[{alias}].[{column}] AS [{column}]";
        }

        var sqlType = column switch
        {
            "date_created" or "date_updated" => "datetime2",
            "created_by_user_code" or "modified_by_user_code" => "int",
            "is_deleted" => "bit",
            _ => "sql_variant"
        };
        return $"CAST(NULL AS {sqlType}) AS [{column}]";
    }

    private static string GetColumnProjection(
        IReadOnlySet<string> columns,
        string column,
        string alias)
        => columns.Contains(column)
            ? $"[{alias}].[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetLegacySqlType(column)}) AS [{column}]";

    private static string GetLegacySqlType(string column)
        => column switch
        {
            "fleet_number" or "registration_number" or "barcode" or "sold_to" => "varchar(1)",
            "sold_date" => "datetime",
            "sold_amount" => "numeric(18, 0)",
            _ => "varchar(1)"
        };

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal))?.TrimEnd();
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTimeIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column)
        => columns.Contains(column) ? ReadDateTime(reader, column) : null;

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static int? ReadInt32IfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column)
        => columns.Contains(column) ? ReadInt32(reader, column) : null;

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }

    private static decimal? ReadDecimal(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static bool? ReadBooleanIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column)
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
