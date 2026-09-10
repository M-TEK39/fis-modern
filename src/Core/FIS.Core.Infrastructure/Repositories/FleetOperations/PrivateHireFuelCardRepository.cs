using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Vehicles;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Compatibility access for PrivHireFuel_card. The original client table has
/// no modern audit columns and contains additional legacy card fields, so EF
/// cannot be used for this execution path.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility allowlists; all submitted values are parameters."
)]
public sealed class PrivateHireFuelCardRepository : IPrivateHireFuelCardRepository
{
    private const string TableName = "PrivHireFuel_card";
    private const string PrivateHireTableName = "Private_hire";

    private static readonly string[] BusinessColumns =
    [
        "PHFuel_card_code",
        "phv_code",
        "Counter",
        "card_number",
        "PAN_number",
        "PetReceiver",
        "PetRecTel",
        "PetTaken",
        "PetExpire",
        "ExpReason",
        "PetComment",
        "Status_date",
        "PetRecId",
        "PetRecFax",
        "Bank_cnt",
        "Inciddat",
        "Petrecsite",
        "Petprint",
        "Garage",
    ];

    private static readonly string[] OptionalAuditColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private static readonly HashSet<string> DateColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "PetTaken",
        "PetExpire",
        "Status_date",
        "Inciddat",
        "date_created",
        "date_updated",
    };

    private readonly FisDbContext _context;

    public PrivateHireFuelCardRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PrivateHireFuelCard?> GetByIdAsync(int privateHireFuelCardId) =>
        (
            await QueryCardsAsync(
                "[f].[PHFuel_card_code] = @fuelCardCode",
                command =>
                    AddParameter(command, "@fuelCardCode", DbType.Int32, privateHireFuelCardId)
            )
        ).SingleOrDefault();

    public async Task<PrivateHireFuelCard?> GetByCardNumberAsync(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return null;
        return (
            await QueryCardsAsync(
                "[f].[card_number] = @cardNumber",
                command => AddParameter(command, "@cardNumber", DbType.String, cardNumber.Trim())
            )
        ).SingleOrDefault();
    }

    public async Task<IEnumerable<PrivateHireFuelCard>> GetByPrivateHireCodeAsync(
        int privateHireCode
    ) =>
        await QueryCardsAsync(
            "[f].[phv_code] = @privateHireCode",
            command => AddParameter(command, "@privateHireCode", DbType.Int32, privateHireCode)
        );

    public async Task<IEnumerable<PrivateHireFuelCard>> GetByRegistrationNumberAsync(
        string registrationNumber
    )
    {
        if (string.IsNullOrWhiteSpace(registrationNumber))
            return Array.Empty<PrivateHireFuelCard>();
        return await QueryCardsAsync(
            "[ph].[registration_number] = @registrationNumber",
            command =>
                AddParameter(
                    command,
                    "@registrationNumber",
                    DbType.String,
                    registrationNumber.Trim()
                ),
            includePrivateHireJoin: true
        );
    }

    public async Task<IEnumerable<PrivateHireFuelCard>> GetActiveFuelCardsAsync() =>
        await QueryCardsAsync();

    public async Task<IEnumerable<PrivateHireFuelCard>> GetActiveFuelCardsBySiteAsync(
        int siteCode
    ) =>
        await QueryCardsAsync(
            "[ph].[site_code] = @siteCode",
            command => AddParameter(command, "@siteCode", DbType.Int32, siteCode),
            includePrivateHireJoin: true
        );

    public async Task<int?> GetPrivateHireCodeByRegistrationAsync(string registrationNumber)
    {
        if (string.IsNullOrWhiteSpace(registrationNumber))
            return null;
        var columns = await GetAvailableColumnsAsync(PrivateHireTableName);
        if (!columns.ContainsKey("PHV_code") || !columns.ContainsKey("registration_number"))
        {
            throw new InvalidOperationException(
                "Private_hire registration lookup columns are not available."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            SELECT TOP (1) [PHV_code]
            FROM [dbo].[{PrivateHireTableName}]
            WHERE [registration_number] = @registrationNumber
              AND {GetActiveFilter("", columns)}
            ORDER BY [PHV_code] DESC
            """;
        AddParameter(command, "@registrationNumber", DbType.String, registrationNumber.Trim());
        var result = await command.ExecuteScalarAsync();
        return result is null || result == DBNull.Value ? null : Convert.ToInt32(result);
    }

    public async Task<PrivateHireFuelCard> CreateAsync(
        PrivateHireFuelCard fuelCard,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(fuelCard);
        var columns = await GetAvailableColumnsAsync(TableName);
        var values = new List<WriteValue>();
        AddValue(values, columns, "phv_code", "@phvCode", DbType.Int32, fuelCard.phv_code);
        AddValue(values, columns, "Counter", "@counter", DbType.Int16, fuelCard.Counter);
        AddValue(
            values,
            columns,
            "card_number",
            "@cardNumber",
            DbType.String,
            fuelCard.card_number
        );
        AddValue(values, columns, "PAN_number", "@panNumber", DbType.String, fuelCard.PAN_number);
        AddValue(
            values,
            columns,
            "PetReceiver",
            "@petReceiver",
            DbType.String,
            fuelCard.PetReceiver
        );
        AddValue(values, columns, "PetRecTel", "@petRecTel", DbType.String, fuelCard.PetRecTel);
        AddValue(values, columns, "PetTaken", "@petTaken", DbType.DateTime2, fuelCard.PetTaken);
        AddValue(values, columns, "PetExpire", "@petExpire", DbType.DateTime2, fuelCard.PetExpire);
        AddValue(values, columns, "ExpReason", "@expReason", DbType.String, fuelCard.ExpReason);
        AddValue(values, columns, "PetComment", "@petComment", DbType.String, fuelCard.PetComment);
        AddValue(
            values,
            columns,
            "Status_date",
            "@statusDate",
            DbType.DateTime2,
            fuelCard.Status_date
        );
        AddValue(values, columns, "PetRecId", "@petRecId", DbType.String, fuelCard.PetRecId);
        AddValue(values, columns, "PetRecFax", "@petRecFax", DbType.String, fuelCard.PetRecFax);
        AddValue(values, columns, "Bank_cnt", "@bankCount", DbType.String, fuelCard.Bank_cnt);
        AddValue(values, columns, "Inciddat", "@incidentDate", DbType.DateTime2, fuelCard.Inciddat);
        AddValue(
            values,
            columns,
            "Petrecsite",
            "@petReceiverSite",
            DbType.Int16,
            fuelCard.Petrecsite
        );
        AddValue(values, columns, "Petprint", "@petPrint", DbType.String, fuelCard.Petprint);
        AddValue(values, columns, "Garage", "@garage", DbType.String, fuelCard.Garage);
        AddValue(
            values,
            columns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddValue(
            values,
            columns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        var code = await ExecuteInsertAsync(values);
        return await GetByIdAsync(code)
            ?? throw new InvalidOperationException(
                $"Private hire fuel card {code} could not be read after creation."
            );
    }

    public async Task UpdateAsync(PrivateHireFuelCard fuelCard, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(fuelCard);
        _ =
            await GetByIdAsync(fuelCard.PHFuel_card_code)
            ?? throw new InvalidOperationException(
                $"Private hire fuel card {fuelCard.PHFuel_card_code} not found"
            );
        var columns = await GetAvailableColumnsAsync(TableName);
        var values = new List<WriteValue>();
        AddValue(values, columns, "phv_code", "@phvCode", DbType.Int32, fuelCard.phv_code);
        AddValue(values, columns, "Counter", "@counter", DbType.Int16, fuelCard.Counter);
        AddValue(
            values,
            columns,
            "card_number",
            "@cardNumber",
            DbType.String,
            fuelCard.card_number
        );
        AddValue(values, columns, "PAN_number", "@panNumber", DbType.String, fuelCard.PAN_number);
        AddValue(
            values,
            columns,
            "PetReceiver",
            "@petReceiver",
            DbType.String,
            fuelCard.PetReceiver
        );
        AddValue(values, columns, "PetRecTel", "@petRecTel", DbType.String, fuelCard.PetRecTel);
        AddValue(values, columns, "PetTaken", "@petTaken", DbType.DateTime2, fuelCard.PetTaken);
        AddValue(values, columns, "PetExpire", "@petExpire", DbType.DateTime2, fuelCard.PetExpire);
        AddValue(values, columns, "ExpReason", "@expReason", DbType.String, fuelCard.ExpReason);
        AddValue(values, columns, "PetComment", "@petComment", DbType.String, fuelCard.PetComment);
        AddValue(
            values,
            columns,
            "Status_date",
            "@statusDate",
            DbType.DateTime2,
            fuelCard.Status_date
        );
        AddValue(values, columns, "PetRecId", "@petRecId", DbType.String, fuelCard.PetRecId);
        AddValue(values, columns, "PetRecFax", "@petRecFax", DbType.String, fuelCard.PetRecFax);
        AddValue(values, columns, "Bank_cnt", "@bankCount", DbType.String, fuelCard.Bank_cnt);
        AddValue(values, columns, "Inciddat", "@incidentDate", DbType.DateTime2, fuelCard.Inciddat);
        AddValue(
            values,
            columns,
            "Petrecsite",
            "@petReceiverSite",
            DbType.Int16,
            fuelCard.Petrecsite
        );
        AddValue(values, columns, "Petprint", "@petPrint", DbType.String, fuelCard.Petprint);
        AddValue(values, columns, "Garage", "@garage", DbType.String, fuelCard.Garage);
        AddValue(
            values,
            columns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            DateTime.UtcNow
        );
        AddValue(
            values,
            columns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        await ExecuteUpdateAsync(fuelCard.PHFuel_card_code, values, columns);
    }

    public async Task DeleteAsync(int privateHireFuelCardId, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync(TableName);
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        if (columns.ContainsKey("is_deleted"))
        {
            var assignments = new List<string> { "[is_deleted] = 1" };
            if (columns.ContainsKey("date_updated"))
            {
                assignments.Add("[date_updated] = @dateUpdated");
                AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
            }
            if (columns.ContainsKey("modified_by_user_code"))
            {
                assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                AddParameter(
                    command,
                    "@modifiedByUserCode",
                    DbType.Int32,
                    currentUserId > 0 ? currentUserId : null
                );
            }
            command.CommandText = $"""
                UPDATE [dbo].[{TableName}]
                SET {string.Join(", ", assignments)}
                WHERE [PHFuel_card_code] = @fuelCardCode
                  AND {GetActiveFilter("", columns)}
                """;
        }
        else
        {
            command.CommandText = $"""
                DELETE FROM [dbo].[{TableName}]
                WHERE [PHFuel_card_code] = @fuelCardCode
                """;
        }
        AddParameter(command, "@fuelCardCode", DbType.Int32, privateHireFuelCardId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<PrivateHireFuelCard>> QueryCardsAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null,
        bool includePrivateHireJoin = false
    )
    {
        var columns = await GetAvailableColumnsAsync(TableName);
        var privateHireColumns = includePrivateHireJoin
            ? await GetAvailableColumnsAsync(PrivateHireTableName)
            : null;
        if (
            includePrivateHireJoin
            && (privateHireColumns is null || !privateHireColumns.ContainsKey("PHV_code"))
        )
        {
            throw new InvalidOperationException(
                "Private_hire PHV_code is not available for the fuel card lookup."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        var conditions = new List<string> { GetActiveFilter("f", columns) };
        if (includePrivateHireJoin)
            conditions.Add(GetActiveFilter("ph", privateHireColumns!));
        if (!string.IsNullOrWhiteSpace(predicate))
            conditions.Insert(0, predicate);
        var from = includePrivateHireJoin
            ? $"FROM [dbo].[{TableName}] AS [f] INNER JOIN [dbo].[{PrivateHireTableName}] AS [ph] ON [ph].[PHV_code] = [f].[phv_code]"
            : $"FROM [dbo].[{TableName}] AS [f]";
        command.CommandText = $"""
            SELECT {string.Join(
                ", ",
                BusinessColumns.Concat(OptionalAuditColumns).Select(column =>
                    GetProjection("f", columns, column)
                )
            )}
            {from}
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY [f].[Counter] DESC, [f].[PHFuel_card_code] DESC
            """;
        configure?.Invoke(command);

        var results = new List<PrivateHireFuelCard>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapFuelCard(reader, columns));
        return results;
    }

    private async Task<int> ExecuteInsertAsync(IReadOnlyList<WriteValue> values)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            INSERT INTO [dbo].[{TableName}] ({string.Join(
                ", ",
                values.Select(value => $"[{value.Column}]")
            )})
            OUTPUT INSERTED.[PHFuel_card_code]
            VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
            """;
        AddParameters(command, values);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task ExecuteUpdateAsync(
        int fuelCardId,
        IReadOnlyList<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns
    )
    {
        if (values.Count == 0)
            return;
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = $"""
            UPDATE [dbo].[{TableName}]
            SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))}
            WHERE [PHFuel_card_code] = @fuelCardCode
              AND {GetActiveFilter("", columns)}
            """;
        AddParameters(command, values);
        AddParameter(command, "@fuelCardCode", DbType.Int32, fuelCardId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Dictionary<string, ColumnInfo>> GetAvailableColumnsAsync(string tableName)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT [COLUMN_NAME], [DATA_TYPE]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table
            """;
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, tableName);
        var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            columns[name] = new ColumnInfo(name, reader.GetString(1));
        }
        return columns;
    }

    private static PrivateHireFuelCard MapFuelCard(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns
    ) =>
        new()
        {
            PHFuel_card_code = ReadInt32(reader, "PHFuel_card_code") ?? 0,
            phv_code = ReadInt32(reader, "phv_code") ?? 0,
            Counter = ReadInt16(reader, "Counter"),
            card_number = ReadString(reader, "card_number"),
            PAN_number = ReadString(reader, "PAN_number"),
            PetReceiver = ReadString(reader, "PetReceiver"),
            PetRecTel = ReadString(reader, "PetRecTel"),
            PetTaken = ReadDateTime(reader, "PetTaken"),
            PetExpire = ReadDateTime(reader, "PetExpire"),
            ExpReason = ReadString(reader, "ExpReason"),
            PetComment = ReadString(reader, "PetComment"),
            Status_date = ReadDateTime(reader, "Status_date"),
            PetRecId = ReadString(reader, "PetRecId"),
            PetRecFax = ReadString(reader, "PetRecFax"),
            Bank_cnt = ReadString(reader, "Bank_cnt"),
            Inciddat = ReadDateTime(reader, "Inciddat"),
            Petrecsite = ReadInt16(reader, "Petrecsite"),
            Petprint = ReadString(reader, "Petprint"),
            Garage = ReadString(reader, "Garage"),
            date_created =
                ReadDateTimeIfAvailable(reader, columns, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTimeIfAvailable(reader, columns, "date_updated"),
            created_by_user_code = ReadInt32IfAvailable(reader, columns, "created_by_user_code"),
            modified_by_user_code = ReadInt32IfAvailable(reader, columns, "modified_by_user_code"),
            is_deleted = ReadBooleanIfAvailable(reader, columns, "is_deleted") ?? false,
        };

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (columns.ContainsKey(column) && value is not null)
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

    private static string GetActiveFilter(
        string alias,
        IReadOnlyDictionary<string, ColumnInfo> columns
    ) =>
        columns.ContainsKey("is_deleted")
            ? string.IsNullOrWhiteSpace(alias)
                ? "ISNULL([is_deleted], 0) = 0"
                : $"ISNULL([{alias}].[is_deleted], 0) = 0"
            : "1 = 1";

    private static string GetProjection(
        string alias,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) =>
        columns.ContainsKey(column)
            ? $"[{alias}].[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static string GetSqlType(string column) =>
        DateColumns.Contains(column)
            ? "datetime2"
            : column switch
            {
                "PHFuel_card_code"
                or "phv_code"
                or "created_by_user_code"
                or "modified_by_user_code" => "int",
                "Counter" or "Petrecsite" => "smallint",
                "is_deleted" => "bit",
                _ => "varchar(1)",
            };

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

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
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

    private static DateTime? ReadDateTimeIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) => columns.ContainsKey(column) ? ReadDateTime(reader, column) : null;

    private static int? ReadInt32IfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) => columns.ContainsKey(column) ? ReadInt32(reader, column) : null;

    private static bool? ReadBooleanIfAvailable(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    )
    {
        if (!columns.ContainsKey(column))
            return null;
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

    private sealed record ColumnInfo(string Name, string DataType);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

    private sealed class ConnectionScope : IAsyncDisposable
    {
        public ConnectionScope(DbConnection connection, bool shouldClose)
        {
            Connection = connection;
            _shouldClose = shouldClose;
        }

        public DbConnection Connection { get; }
        private readonly bool _shouldClose;

        public async ValueTask DisposeAsync()
        {
            if (_shouldClose)
                await Connection.CloseAsync();
        }
    }
}
