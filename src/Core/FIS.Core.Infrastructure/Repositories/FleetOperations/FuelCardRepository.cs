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
/// Reads and writes Fuel_card against both the original client schema and
/// databases containing the optional modern audit columns. The legacy table
/// remains the source of truth; no schema change is required.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility allowlists; all submitted values are parameters."
)]
public sealed class FuelCardRepository : IFuelCardRepository
{
    private const string TableName = "Fuel_card";

    private static readonly string[] BusinessColumns =
    [
        "Fuel_card_code",
        "vmf_code",
        "Counter",
        "card_number",
        "PAN_number",
        "PetReceiver",
        "PetRecTel",
        "PetTaken",
        "PetExpire",
        "ExpReason",
        "PetComment",
        "LinkGGNum",
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

    public FuelCardRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<FuelCard?> GetByIdAsync(int fuelCardId) =>
        (
            await QueryAsync(
                "[Fuel_card_code] = @fuelCardCode",
                command => AddParameter(command, "@fuelCardCode", DbType.Int32, fuelCardId)
            )
        ).SingleOrDefault();

    public async Task<FuelCard?> GetByCardNumberAsync(string cardNumber)
    {
        if (string.IsNullOrWhiteSpace(cardNumber))
            return null;
        return (
            await QueryAsync(
                "[card_number] = @cardNumber",
                command => AddParameter(command, "@cardNumber", DbType.String, cardNumber.Trim())
            )
        ).SingleOrDefault();
    }

    public async Task<IEnumerable<FuelCard>> GetActiveFuelCardsAsync() => await QueryAsync();

    public async Task<IEnumerable<FuelCard>> GetFuelCardsByVehicleAsync(int vmfCode) =>
        await QueryAsync(
            "[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode)
        );

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "SQL identifiers come only from fixed compatibility allowlists; filter and pagination values are parameters."
    )]
    public async Task<FuelCardActivityPage> GetRecentActivityPageAsync(
        int? siteCode = null,
        int page = 1,
        int pageSize = 24,
        CancellationToken cancellationToken = default
    )
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var columns = await GetAvailableColumnsAsync(cancellationToken);
        await using var scope = await OpenConnectionAsync(cancellationToken);
        var transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        var conditions = new List<string>
        {
            GetActiveFilter(columns),
            "[Status_date] IS NOT NULL",
        };
        if (siteCode.HasValue)
        {
            conditions.Add("[Petrecsite] = @siteCode");
        }

        var whereClause = string.Join(" AND ", conditions);
        int total;
        await using (var countCommand = scope.Connection.CreateCommand())
        {
            countCommand.Transaction = transaction;
            countCommand.CommandText = $"""
                SELECT COUNT(1)
                FROM [dbo].[{TableName}]
                WHERE {whereClause}
                """;
            if (siteCode.HasValue)
            {
                AddParameter(countCommand, "@siteCode", DbType.Int32, siteCode.Value);
            }

            total = Convert.ToInt32(
                await countCommand.ExecuteScalarAsync(cancellationToken)
            );
        }

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);
        var offset = checked((long)(page - 1) * pageSize);
        var projection = BusinessColumns
            .Concat(OptionalAuditColumns)
            .Select(column => GetProjection(columns, column));

        await using var dataCommand = scope.Connection.CreateCommand();
        dataCommand.Transaction = transaction;
        dataCommand.CommandText = $"""
            SELECT {string.Join(", ", projection)}
            FROM [dbo].[{TableName}]
            WHERE {whereClause}
            ORDER BY [Status_date] DESC, [Fuel_card_code] DESC
            OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY
            """;
        if (siteCode.HasValue)
        {
            AddParameter(dataCommand, "@siteCode", DbType.Int32, siteCode.Value);
        }

        AddParameter(dataCommand, "@offset", DbType.Int64, offset);
        AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

        var items = new List<FuelCard>();
        await using var reader = await dataCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(MapFuelCard(reader, columns));
        }

        return new FuelCardActivityPage(items, page, pageSize, total);
    }

    public async Task<FuelCard> CreateAsync(FuelCard fuelCard, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(fuelCard);
        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();

        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, fuelCard.vmf_code);
        AddValue(values, columns, "Counter", "@counter", DbType.Int32, fuelCard.Counter);
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
        AddValue(values, columns, "LinkGGNum", "@linkGgNum", DbType.String, fuelCard.LinkGGNum);
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
                $"Fuel card {code} could not be read after creation."
            );
    }

    public async Task UpdateAsync(FuelCard fuelCard, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(fuelCard);
        _ =
            await GetByIdAsync(fuelCard.Fuel_card_code)
            ?? throw new InvalidOperationException(
                $"Fuel card {fuelCard.Fuel_card_code} not found"
            );

        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();
        // Do not overwrite legacy fields with null when a modern caller posts
        // only the fields it knows about.
        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, fuelCard.vmf_code);
        AddValue(values, columns, "Counter", "@counter", DbType.Int32, fuelCard.Counter);
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
        AddValue(values, columns, "LinkGGNum", "@linkGgNum", DbType.String, fuelCard.LinkGGNum);
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

        await ExecuteUpdateAsync(fuelCard.Fuel_card_code, values, columns);
    }

    public async Task DeleteAsync(int fuelCardId, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
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
                WHERE [Fuel_card_code] = @fuelCardCode
                  AND {GetActiveFilter(columns)}
                """;
        }
        else
        {
            command.CommandText = $"""
                DELETE FROM [dbo].[{TableName}]
                WHERE [Fuel_card_code] = @fuelCardCode
                """;
        }

        AddParameter(command, "@fuelCardCode", DbType.Int32, fuelCardId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<FuelCard>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();

        var conditions = new List<string> { GetActiveFilter(columns) };
        if (!string.IsNullOrWhiteSpace(predicate))
            conditions.Insert(0, predicate);
        var order = columns.ContainsKey("Status_date") ? "[Status_date] DESC, " : string.Empty;
        command.CommandText = $"""
            SELECT {string.Join(
                ", ",
                BusinessColumns.Concat(OptionalAuditColumns).Select(column =>
                    GetProjection(columns, column)
                )
            )}
            FROM [dbo].[{TableName}]
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY {order}[Fuel_card_code] DESC
            """;
        configure?.Invoke(command);

        var results = new List<FuelCard>();
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
            OUTPUT INSERTED.[Fuel_card_code]
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
            WHERE [Fuel_card_code] = @fuelCardCode
              AND {GetActiveFilter(columns)}
            """;
        AddParameters(command, values);
        AddParameter(command, "@fuelCardCode", DbType.Int32, fuelCardId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<Dictionary<string, ColumnInfo>> GetAvailableColumnsAsync(
        CancellationToken cancellationToken = default
    )
    {
        await using var scope = await OpenConnectionAsync(cancellationToken);
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandText = """
            SELECT [COLUMN_NAME], [DATA_TYPE]
            FROM [INFORMATION_SCHEMA].[COLUMNS]
            WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table
            """;
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, TableName);

        var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(0);
            columns[name] = new ColumnInfo(name, reader.GetString(1));
        }
        foreach (
            var required in BusinessColumns.Where(column =>
                column != "Fuel_card_code" && !columns.ContainsKey(column)
            )
        )
        {
            throw new InvalidOperationException(
                $"The required Fuel_card compatibility column {required} is not available."
            );
        }
        if (!columns.ContainsKey("Fuel_card_code"))
        {
            throw new InvalidOperationException(
                "The required Fuel_card compatibility key is not available."
            );
        }
        return columns;
    }

    private static FuelCard MapFuelCard(
        DbDataReader reader,
        IReadOnlyDictionary<string, ColumnInfo> columns
    ) =>
        new()
        {
            Fuel_card_code = ReadInt32(reader, "Fuel_card_code") ?? 0,
            vmf_code = ReadInt32(reader, "vmf_code"),
            Counter = ReadInt16(reader, "Counter"),
            card_number = ReadString(reader, "card_number"),
            PAN_number = ReadString(reader, "PAN_number"),
            PetReceiver = ReadString(reader, "PetReceiver"),
            PetRecTel = ReadString(reader, "PetRecTel"),
            PetTaken = ReadDateTime(reader, "PetTaken"),
            PetExpire = ReadDateTime(reader, "PetExpire"),
            ExpReason = ReadString(reader, "ExpReason"),
            PetComment = ReadString(reader, "PetComment"),
            LinkGGNum = ReadString(reader, "LinkGGNum"),
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

    private static string GetActiveFilter(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

    private static string GetProjection(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) =>
        columns.ContainsKey(column)
            ? $"[{column}] AS [{column}]"
            : $"CAST(NULL AS {GetSqlType(column)}) AS [{column}]";

    private static string GetSqlType(string column) =>
        DateColumns.Contains(column)
            ? "datetime2"
            : column switch
            {
                "Fuel_card_code"
                or "vmf_code"
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

    private async Task<ConnectionScope> OpenConnectionAsync(
        CancellationToken cancellationToken = default
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync(cancellationToken);
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
