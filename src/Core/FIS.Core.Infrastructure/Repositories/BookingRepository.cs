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
/// Persists booking business data against both the original client schema and
/// the expanded schema. Audit columns are optional because the client database
/// may not have received the later expansion yet.
/// </summary>
public class BookingRepository : IBookingRepository
{
    private const string TableName = "bookings";

    private static readonly string[] LegacyColumns =
    [
        "booking_id",
        "site_code",
        "name",
        "start_date",
        "end_date",
        "class_code",
        "user_id",
        "booking_date",
        "telephone",
        "collected",
        "location_code",
        "vmf_code",
        "booking_status",
        "notes"
    ];

    private static readonly string[] OptionalColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted"
    ];

    private static readonly string[] RequiredColumns =
    [
        "booking_id",
        "start_date"
    ];

    private readonly FisDbContext _context;

    public BookingRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Booking?> GetByIdAsync(short bookingId)
    {
        return (await QueryAsync(
            columns => columns.Contains("booking_id")
                ? new QuerySpec(
                    "WHERE [booking_id] = @bookingId",
                    command => AddParameter(command, "@bookingId", DbType.Int16, bookingId))
                : QuerySpec.NoResults)).SingleOrDefault();
    }

    public async Task<IEnumerable<Booking>> GetAllAsync()
    {
        return await QueryAsync();
    }

    public async Task<IEnumerable<Booking>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
    {
        return await QueryAsync(columns =>
            columns.Contains("start_date")
                ? new QuerySpec(
                    "WHERE [start_date] >= @startDate AND [start_date] <= @endDate",
                    command =>
                    {
                        AddParameter(command, "@startDate", DbType.DateTime2, startDate);
                        AddParameter(command, "@endDate", DbType.DateTime2, endDate);
                    })
                : QuerySpec.NoResults);
    }

    public async Task<IEnumerable<Booking>> GetByStatusAsync(string status)
    {
        return await QueryAsync(columns =>
            columns.Contains("booking_status")
                ? new QuerySpec(
                    "WHERE [booking_status] = @status",
                    command => AddParameter(command, "@status", DbType.String, status))
                : QuerySpec.NoResults);
    }

    public async Task<IEnumerable<Booking>> GetByVehicleAsync(int vmfCode)
    {
        return await QueryAsync(columns =>
            columns.Contains("vmf_code")
                ? new QuerySpec(
                    "WHERE [vmf_code] = @vmfCode",
                    command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode))
                : QuerySpec.NoResults);
    }

    public async Task<Booking> CreateAsync(Booking booking, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(booking);

        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        var values = BuildLegacyWriteValues(booking)
            .Where(value => availableColumns.Contains(value.Column))
            .ToList();

        AddOptionalValue(values, availableColumns, "date_created", "@dateCreated", DbType.DateTime2, now);
        AddOptionalValue(
            values,
            availableColumns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null);
        AddOptionalValue(values, availableColumns, "is_deleted", "@isDeleted", DbType.Boolean, false);

        var bookingId = await ExecuteInsertAsync(values);
        booking.booking_id = bookingId;
        booking.date_created = now;
        booking.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        booking.is_deleted = false;
        return booking;
    }

    public async Task<Booking> UpdateAsync(Booking booking, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(booking);

        var existing = await GetByIdAsync(booking.booking_id);
        if (existing == null)
        {
            throw new InvalidOperationException(
                $"Booking with booking_id {booking.booking_id} not found");
        }

        var availableColumns = await GetAvailableColumnsAsync();
        var values = BuildLegacyWriteValues(booking)
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
        AddOptionalValue(values, availableColumns, "is_deleted", "@isDeleted", DbType.Boolean, booking.is_deleted);

        await ExecuteUpdateAsync(booking.booking_id, values, availableColumns);
        booking.date_created = existing.date_created;
        booking.created_by_user_code = existing.created_by_user_code;
        booking.date_updated = now;
        booking.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        return booking;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "DeleteAsync selects between fixed legacy SQL statements and uses a parameter for the record identifier.")]
    public async Task DeleteAsync(short bookingId, int currentUserId)
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

            if (availableColumns.Contains("is_deleted"))
            {
                var assignments = new List<string> { "[is_deleted] = 1" };
                if (availableColumns.Contains("date_updated"))
                {
                    assignments.Add("[date_updated] = @dateUpdated");
                    AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (availableColumns.Contains("modified_by_user_code"))
                {
                    assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                    AddParameter(
                        command,
                        "@modifiedByUserCode",
                        DbType.Int32,
                        currentUserId > 0 ? currentUserId : null);
                }

                command.CommandText = $"""
                    UPDATE [dbo].[{TableName}]
                    SET {string.Join(", ", assignments)}
                    WHERE [booking_id] = @bookingId
                    AND {GetActiveFilter(availableColumns)}
                    """;
            }
            else
            {
                command.CommandText = $"""
                    DELETE FROM [dbo].[{TableName}]
                    WHERE [booking_id] = @bookingId
                    """;
            }

            AddParameter(command, "@bookingId", DbType.Int16, bookingId);
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
    private async Task<List<Booking>> QueryAsync(
        Func<IReadOnlySet<string>, QuerySpec>? queryFactory = null)
    {
        var availableColumns = await GetAvailableColumnsAsync();
        var query = queryFactory?.Invoke(availableColumns) ?? QuerySpec.All;
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
                .Select(column => GetColumnProjection(availableColumns, column))
                .Concat(OptionalColumns.Select(column => GetOptionalProjection(availableColumns, column)))
                .ToArray();
            var whereClause = string.IsNullOrWhiteSpace(query.Predicate)
                ? $"WHERE {GetActiveFilter(availableColumns)}"
                : $"{query.Predicate} AND {GetActiveFilter(availableColumns)}";

            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{TableName}]
                {whereClause}
                ORDER BY [start_date] DESC, [booking_id] DESC
                """;
            query.Configure?.Invoke(command);

            var results = new List<Booking>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapBooking(reader, availableColumns));
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
        Justification = "The INSERT statement is composed only from fixed legacy columns and allowlisted optional values; every value is parameterized.")]
    private async Task<short> ExecuteInsertAsync(IReadOnlyList<WriteValue> values)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException("No compatible booking columns are available for insert.");
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
                INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))})
                OUTPUT INSERTED.[booking_id]
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
        Justification = "The UPDATE statement is composed only from fixed legacy columns and allowlisted optional values; every value is parameterized.")]
    private async Task ExecuteUpdateAsync(
        short bookingId,
        IReadOnlyList<WriteValue> values,
        IReadOnlySet<string> availableColumns)
    {
        if (values.Count == 0)
        {
            throw new InvalidOperationException("No compatible booking columns are available for update.");
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
                UPDATE [dbo].[{TableName}]
                SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))}
                WHERE [booking_id] = @bookingId
                AND {GetActiveFilter(availableColumns)}
                """;
            AddParameters(command, values);
            AddParameter(command, "@bookingId", DbType.Int16, bookingId);
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
            AddParameter(command, "@table", DbType.String, TableName);

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(0));
            }

            var missingColumns = RequiredColumns.Where(column => !columns.Contains(column)).ToArray();
            if (missingColumns.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The required bookings compatibility columns are not available: {string.Join(", ", missingColumns)}");
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

    private static Booking MapBooking(DbDataReader reader, IReadOnlySet<string> availableColumns)
    {
        var startDate = ReadDateTime(reader, "start_date") ?? DateTime.MinValue;
        return new Booking
        {
            booking_id = ReadInt16(reader, "booking_id") ?? 0,
            site_code = ReadString(reader, "site_code"),
            name = ReadString(reader, "name"),
            start_date = startDate,
            end_date = ReadDateTime(reader, "end_date"),
            class_code = ReadInt16(reader, "class_code") ?? 0,
            user_id = ReadInt16(reader, "user_id") ?? 0,
            booking_date = ReadDateTime(reader, "booking_date") ?? startDate,
            telephone = ReadString(reader, "telephone"),
            collected = ReadInt16(reader, "collected"),
            location_code = ReadInt32(reader, "location_code") ?? 0,
            vmf_code = ReadInt32(reader, "vmf_code"),
            booking_status = ReadString(reader, "booking_status"),
            notes = ReadString(reader, "notes"),
            date_created = ReadDateTimeIfAvailable(reader, availableColumns, "date_created") ?? startDate,
            date_updated = ReadDateTimeIfAvailable(reader, availableColumns, "date_updated"),
            created_by_user_code = ReadInt32IfAvailable(reader, availableColumns, "created_by_user_code"),
            modified_by_user_code = ReadInt32IfAvailable(reader, availableColumns, "modified_by_user_code"),
            is_deleted = ReadBooleanIfAvailable(reader, availableColumns, "is_deleted") ?? false
        };
    }

    private static List<WriteValue> BuildLegacyWriteValues(Booking booking)
    {
        return
        [
            new("site_code", "@siteCode", DbType.String, booking.site_code),
            new("name", "@name", DbType.String, booking.name),
            new("start_date", "@startDate", DbType.DateTime2, booking.start_date),
            new("end_date", "@endDate", DbType.DateTime2, booking.end_date),
            new("class_code", "@classCode", DbType.Int16, booking.class_code),
            new("user_id", "@userId", DbType.Int16, booking.user_id),
            new("booking_date", "@bookingDate", DbType.DateTime2, booking.booking_date),
            new("telephone", "@telephone", DbType.String, booking.telephone),
            new("collected", "@collected", DbType.Int16, booking.collected),
            new("location_code", "@locationCode", DbType.Int32, booking.location_code),
            new("vmf_code", "@vmfCode", DbType.Int32, booking.vmf_code),
            new("booking_status", "@bookingStatus", DbType.String, booking.booking_status),
            new("notes", "@notes", DbType.String, booking.notes)
        ];
    }

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

    private static string GetActiveFilter(IReadOnlySet<string> availableColumns)
        => availableColumns.Contains("is_deleted") ? "ISNULL([is_deleted], 0) = 0" : "1 = 1";

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
            _ => "sql_variant"
        };
        return $"CAST(NULL AS {sqlType}) AS [{column}]";
    }

    private static string GetColumnProjection(IReadOnlySet<string> columns, string column)
    {
        if (columns.Contains(column))
        {
            return $"[{column}] AS [{column}]";
        }

        return $"CAST(NULL AS {GetLegacySqlType(column)}) AS [{column}]";
    }

    private static string GetLegacySqlType(string column)
        => column switch
        {
            "booking_id" or "class_code" or "user_id" or "collected" => "smallint",
            "location_code" or "vmf_code" => "int",
            "start_date" or "end_date" or "booking_date" => "datetime2",
            _ => "varchar(1)"
        };

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal))?.TrimEnd();
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

    private sealed record QuerySpec(string Predicate, Action<DbCommand>? Configure)
    {
        public static QuerySpec All { get; } = new(string.Empty, null);
        public static QuerySpec NoResults { get; } = new("WHERE 1 = 0", null);
    }

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
