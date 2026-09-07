using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads and writes notice schedules through the expanded table when its
/// business columns exist, and preserves the client-era stored procedures
/// when the legacy table shape is connected.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers and stored procedure names are fixed; all submitted values are parameters.")]
public sealed class NoticeScheduleRepository : INoticeScheduleRepository
{
    private const string ScheduleTableName = "NoticeSchedule";

    private static readonly string[] ScheduleColumns =
    [
        "notice_schedule_id", "notice_id", "title_field", "start_date", "end_date", "sort_order"
    ];

    private static readonly string[] OptionalScheduleColumns =
    [
        "date_created", "date_updated", "created_by_user_code", "modified_by_user_code", "is_deleted"
    ];

    private readonly FisDbContext _context;

    public NoticeScheduleRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<NoticeSchedule?> GetByIdAsync(int noticeScheduleId)
        => await WithSchemaAsync(async (connection, transaction, schema) =>
            schema.IsModern
                ? (await ReadModernAsync(
                    connection,
                    transaction,
                    schema.ScheduleColumns,
                    $"WHERE [s].[notice_schedule_id] = @scheduleId AND {ActiveFilter("s", schema.ScheduleColumns.Contains("is_deleted"))}",
                    command => NoticeRepositorySupport.AddParameter(command, "@scheduleId", DbType.Int32, noticeScheduleId),
                    single: true)).SingleOrDefault()
                : await ReadStoredScheduleAsync(connection, transaction, "DEV_SEL_NoticeSchedule", noticeScheduleId));

    public async Task<IEnumerable<NoticeSchedule>> GetAllAsync()
        => await WithSchemaAsync(async (connection, transaction, schema) =>
            schema.IsModern
                ? await ReadModernAsync(
                    connection,
                    transaction,
                    schema.ScheduleColumns,
                    $"WHERE {ActiveFilter("s", schema.ScheduleColumns.Contains("is_deleted"))} ORDER BY COALESCE([s].[sort_order], 2147483647), [s].[start_date], [s].[notice_schedule_id]",
                    configure: null,
                    single: false)
                : await ReadStoredSchedulesAsync(connection, transaction, "DEV_SEL_ActiveNoticeSchedules"));

    public async Task<IEnumerable<NoticeSchedule>> GetByNoticeIdAsync(int noticeId)
        => await WithSchemaAsync(async (connection, transaction, schema) =>
            schema.IsModern
                ? await ReadModernAsync(
                    connection,
                    transaction,
                    schema.ScheduleColumns,
                    $"WHERE [s].[notice_id] = @noticeId AND {ActiveFilter("s", schema.ScheduleColumns.Contains("is_deleted"))} ORDER BY COALESCE([s].[sort_order], 2147483647), [s].[start_date], [s].[notice_schedule_id]",
                    command => NoticeRepositorySupport.AddParameter(command, "@noticeId", DbType.Int32, noticeId),
                    single: false)
                : (await ReadStoredSchedulesAsync(connection, transaction, "DEV_SEL_ActiveNoticeSchedules"))
                    .Where(schedule => schedule.notice_id == noticeId)
                    .ToList());

    public async Task<IEnumerable<NoticeSchedule>> GetActiveSchedulesAsync()
        => await WithSchemaAsync(async (connection, transaction, schema) =>
            schema.IsModern
                ? await ReadModernAsync(
                    connection,
                    transaction,
                    schema.ScheduleColumns,
                    $"WHERE {ActiveFilter("s", schema.ScheduleColumns.Contains("is_deleted"))} AND [s].[start_date] <= CAST(GETDATE() AS date) AND ([s].[end_date] IS NULL OR [s].[end_date] >= CAST(GETDATE() AS date)) ORDER BY COALESCE([s].[sort_order], 2147483647), [s].[start_date], [s].[notice_schedule_id]",
                    configure: null,
                    single: false)
                : (await ReadStoredSchedulesAsync(connection, transaction, "DEV_SEL_ActiveNoticeSchedules"))
                    .Where(schedule => schedule.start_date <= DateTime.Today && (!schedule.end_date.HasValue || schedule.end_date.Value >= DateTime.Today))
                    .ToList());

    public async Task<IEnumerable<NoticeSchedule>> GetSchedulesByDateRangeAsync(DateTime startDate, DateTime endDate)
        => await WithSchemaAsync(async (connection, transaction, schema) =>
            schema.IsModern
                ? await ReadModernAsync(
                    connection,
                    transaction,
                    schema.ScheduleColumns,
                    $"WHERE {ActiveFilter("s", schema.ScheduleColumns.Contains("is_deleted"))} AND [s].[start_date] <= @endDate AND ([s].[end_date] IS NULL OR [s].[end_date] >= @startDate) ORDER BY COALESCE([s].[sort_order], 2147483647), [s].[start_date], [s].[notice_schedule_id]",
                    command =>
                    {
                        NoticeRepositorySupport.AddParameter(command, "@startDate", DbType.DateTime2, startDate);
                        NoticeRepositorySupport.AddParameter(command, "@endDate", DbType.DateTime2, endDate);
                    },
                    single: false)
                : (await ReadStoredSchedulesAsync(connection, transaction, "DEV_SEL_ActiveNoticeSchedules"))
                    .Where(schedule => schedule.start_date <= endDate && (!schedule.end_date.HasValue || schedule.end_date.Value >= startDate))
                    .ToList());

    public async Task<NoticeSchedule> CreateAsync(NoticeSchedule noticeSchedule, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(noticeSchedule);

        return await WithSchemaAsync(async (connection, transaction, schema) =>
        {
            if (!schema.IsModern)
            {
                await ExecuteLegacyScheduleWriteAsync(connection, transaction, noticeSchedule, currentUserId);
                return noticeSchedule;
            }

            var columns = schema.ScheduleColumns;
            var now = DateTime.UtcNow;
            var values = new List<WriteValue>
            {
                new("notice_id", "@noticeId", DbType.Int32, noticeSchedule.notice_id),
                new("title_field", "@titleField", DbType.String, noticeSchedule.title_field),
                new("start_date", "@startDate", DbType.DateTime2, noticeSchedule.start_date),
                new("end_date", "@endDate", DbType.DateTime2, noticeSchedule.end_date),
                new("sort_order", "@sortOrder", DbType.Int32, noticeSchedule.sort_order)
            };
            AddOptionalValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, now);
            AddOptionalValue(values, columns, "created_by_user_code", "@createdByUserCode", DbType.Int32, currentUserId);
            AddOptionalValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false);

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
                INSERT INTO [dbo].[{ScheduleTableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))})
                OUTPUT INSERTED.[notice_schedule_id]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
            AddParameters(command, values);
            noticeSchedule.notice_schedule_id = Convert.ToInt32(await command.ExecuteScalarAsync());
            noticeSchedule.date_created = now;
            noticeSchedule.created_by_user_code = currentUserId;
            noticeSchedule.is_deleted = false;
            return noticeSchedule;
        });
    }

    public async Task<NoticeSchedule> UpdateAsync(NoticeSchedule noticeSchedule, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(noticeSchedule);

        return await WithSchemaAsync(async (connection, transaction, schema) =>
        {
            if (!schema.IsModern)
            {
                _ = await ReadStoredScheduleAsync(connection, transaction, "DEV_SEL_NoticeSchedule", noticeSchedule.notice_schedule_id)
                    ?? throw new InvalidOperationException($"NoticeSchedule with notice_schedule_id {noticeSchedule.notice_schedule_id} not found");
                await ExecuteLegacyScheduleWriteAsync(connection, transaction, noticeSchedule, currentUserId);
                return noticeSchedule;
            }

            var columns = schema.ScheduleColumns;
            var values = new List<WriteValue>
            {
                new("notice_id", "@noticeId", DbType.Int32, noticeSchedule.notice_id),
                new("title_field", "@titleField", DbType.String, noticeSchedule.title_field),
                new("start_date", "@startDate", DbType.DateTime2, noticeSchedule.start_date),
                new("end_date", "@endDate", DbType.DateTime2, noticeSchedule.end_date),
                new("sort_order", "@sortOrder", DbType.Int32, noticeSchedule.sort_order)
            };
            AddOptionalValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
            AddOptionalValue(values, columns, "modified_by_user_code", "@modifiedByUserCode", DbType.Int32, currentUserId);

            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"""
                UPDATE [dbo].[{ScheduleTableName}]
                SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))}
                WHERE [notice_schedule_id] = @scheduleId
                  AND {ActiveFilter(string.Empty, columns.Contains("is_deleted"))}
                """;
            AddParameters(command, values);
            NoticeRepositorySupport.AddParameter(command, "@scheduleId", DbType.Int32, noticeSchedule.notice_schedule_id);
            if (await command.ExecuteNonQueryAsync() == 0)
            {
                throw new InvalidOperationException($"NoticeSchedule with notice_schedule_id {noticeSchedule.notice_schedule_id} not found");
            }

            return noticeSchedule;
        });
    }

    public async Task DeleteAsync(int noticeScheduleId, int currentUserId)
        => await WithSchemaAsync<int>(async (connection, transaction, schema) =>
        {
            if (!schema.IsModern)
            {
                await ExecuteLegacyScheduleDeleteAsync(connection, transaction, noticeScheduleId);
                return 0;
            }

            var columns = schema.ScheduleColumns;
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            if (columns.Contains("is_deleted"))
            {
                var assignments = new List<string> { "[is_deleted] = 1" };
                if (columns.Contains("date_updated"))
                {
                    assignments.Add("[date_updated] = @dateUpdated");
                    NoticeRepositorySupport.AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
                }

                if (columns.Contains("modified_by_user_code"))
                {
                    assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                    NoticeRepositorySupport.AddParameter(command, "@modifiedByUserCode", DbType.Int32, currentUserId);
                }

                command.CommandText = $"UPDATE [dbo].[{ScheduleTableName}] SET {string.Join(", ", assignments)} WHERE [notice_schedule_id] = @scheduleId AND {ActiveFilter(string.Empty, schema.ScheduleColumns.Contains("is_deleted"))}";
            }
            else
            {
                command.CommandText = $"DELETE FROM [dbo].[{ScheduleTableName}] WHERE [notice_schedule_id] = @scheduleId";
            }

            NoticeRepositorySupport.AddParameter(command, "@scheduleId", DbType.Int32, noticeScheduleId);
            await command.ExecuteNonQueryAsync();
            return 0;
        });

    private async Task<T> WithSchemaAsync<T>(Func<DbConnection, DbTransaction?, NoticeRepositorySupport.NoticeSchema, Task<T>> operation)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            var transaction = NoticeRepositorySupport.GetCurrentTransaction(_context);
            var schema = await NoticeRepositorySupport.GetSchemaAsync(connection, transaction);
            return await operation(connection, transaction, schema);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<List<NoticeSchedule>> ReadModernAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlySet<string> scheduleColumns,
        string whereClause,
        Action<DbCommand>? configure,
        bool single)
    {
        var userColumns = await NoticeRepositorySupport.GetColumnsAsync(connection, transaction, "TS_Users");
        var hasUser = scheduleColumns.Contains("created_by_user_code")
            && userColumns.Contains("user_access_code")
            && userColumns.Contains("email");
        var projection = ScheduleColumns
            .Select(column => $"[s].[{column}] AS [{column}]")
            .Concat(OptionalScheduleColumns.Select(column => OptionalProjection("s", column, scheduleColumns)))
            .Append(hasUser ? "[u].[email] AS [created_by_email]" : "CAST(NULL AS varchar(255)) AS [created_by_email]")
            .ToArray();
        var userJoin = hasUser
            ? "LEFT JOIN [dbo].[TS_Users] AS [u] ON [u].[user_access_code] = [s].[created_by_user_code]"
            : string.Empty;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT {string.Join(", ", projection)}
            FROM [dbo].[{ScheduleTableName}] AS [s]
            {userJoin}
            {whereClause}
            """;
        configure?.Invoke(command);

        var results = new List<NoticeSchedule>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(MapModernSchedule(reader));
            if (single)
            {
                break;
            }
        }

        return results;
    }

    private static async Task<NoticeSchedule?> ReadStoredScheduleAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string procedure,
        int scheduleId)
    {
        await using var command = CreateStoredProcedure(connection, transaction, procedure);
        NoticeRepositorySupport.AddParameter(command, "@ID", DbType.Int32, scheduleId);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapStoredSchedule(reader) : null;
    }

    private static async Task<List<NoticeSchedule>> ReadStoredSchedulesAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string procedure)
    {
        await using var command = CreateStoredProcedure(connection, transaction, procedure);
        await using var reader = await command.ExecuteReaderAsync();
        var results = new List<NoticeSchedule>();
        while (await reader.ReadAsync())
        {
            results.Add(MapStoredSchedule(reader));
        }

        return results;
    }

    private static async Task ExecuteLegacyScheduleWriteAsync(
        DbConnection connection,
        DbTransaction? transaction,
        NoticeSchedule schedule,
        int currentUserId)
    {
        await using var command = CreateStoredProcedure(connection, transaction, "DEV_INS_Schedule");
        NoticeRepositorySupport.AddParameter(command, "@scheduleId", DbType.Int32, schedule.notice_schedule_id);
        NoticeRepositorySupport.AddParameter(command, "@noticeId", DbType.Int32, schedule.notice_id);
        NoticeRepositorySupport.AddParameter(command, "@startDate", DbType.DateTime2, schedule.start_date);
        NoticeRepositorySupport.AddParameter(command, "@endDate", DbType.DateTime2, schedule.end_date);
        NoticeRepositorySupport.AddParameter(command, "@sortOrder", DbType.Int32, schedule.sort_order);
        NoticeRepositorySupport.AddParameter(command, "@createDate", DbType.DateTime2, DateTime.Now);
        NoticeRepositorySupport.AddStringParameter(command, "@createBy", currentUserId.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ExecuteLegacyScheduleDeleteAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int scheduleId)
    {
        await using var command = CreateStoredProcedure(connection, transaction, "DEV_DEL_ActiveNoticeSchedules");
        NoticeRepositorySupport.AddParameter(command, "@NoticeScheduleId", DbType.Int32, scheduleId);
        await command.ExecuteNonQueryAsync();
    }

    private static DbCommand CreateStoredProcedure(DbConnection connection, DbTransaction? transaction, string procedure)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = procedure;
        command.CommandType = CommandType.StoredProcedure;
        return command;
    }

    private static NoticeSchedule MapModernSchedule(DbDataReader reader)
    {
        var createdBy = NoticeRepositorySupport.ReadString(reader, "created_by_email");
        return new NoticeSchedule
        {
            notice_schedule_id = NoticeRepositorySupport.ReadInt32(reader, "notice_schedule_id") ?? 0,
            notice_id = NoticeRepositorySupport.ReadInt32(reader, "notice_id") ?? 0,
            title_field = NoticeRepositorySupport.ReadString(reader, "title_field"),
            start_date = NoticeRepositorySupport.ReadDateTime(reader, "start_date"),
            end_date = NoticeRepositorySupport.ReadDateTime(reader, "end_date"),
            sort_order = NoticeRepositorySupport.ReadInt32(reader, "sort_order"),
            date_created = NoticeRepositorySupport.ReadDateTime(reader, "date_created") ?? DateTime.MinValue,
            date_updated = NoticeRepositorySupport.ReadDateTime(reader, "date_updated"),
            created_by_user_code = NoticeRepositorySupport.ReadInt32(reader, "created_by_user_code"),
            modified_by_user_code = NoticeRepositorySupport.ReadInt32(reader, "modified_by_user_code"),
            is_deleted = NoticeRepositorySupport.ReadBoolean(reader, "is_deleted"),
            CreatedByUser = string.IsNullOrWhiteSpace(createdBy) ? null : new User { email = createdBy }
        };
    }

    private static NoticeSchedule MapStoredSchedule(DbDataReader reader)
    {
        var createdBy = NoticeRepositorySupport.ReadString(reader, "created_by", "CreatedBy", "createBy");
        return new NoticeSchedule
        {
            notice_schedule_id = NoticeRepositorySupport.ReadInt32(reader, "notice_schedule_id", "NoticeScheduleId", "ScheduleID") ?? 0,
            notice_id = NoticeRepositorySupport.ReadInt32(reader, "notice_id", "NoticeId", "NoticeID") ?? 0,
            title_field = NoticeRepositorySupport.ReadString(reader, "title_field", "TitleField"),
            start_date = NoticeRepositorySupport.ReadDateTime(reader, "start_date", "StartDate"),
            end_date = NoticeRepositorySupport.ReadDateTime(reader, "end_date", "EndDate"),
            sort_order = NoticeRepositorySupport.ReadInt32(reader, "sort_order", "SortOrder"),
            date_created = NoticeRepositorySupport.ReadDateTime(reader, "date_created", "CreatedDate", "CreateDate") ?? DateTime.MinValue,
            CreatedByUser = string.IsNullOrWhiteSpace(createdBy) ? null : new User { email = createdBy }
        };
    }

    private static void AddOptionalValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> columns,
        string column,
        string parameter,
        DbType type,
        object? value)
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
            NoticeRepositorySupport.AddParameter(command, value.Parameter, value.Type, value.Value);
        }
    }

    private static string ActiveFilter(string alias, bool hasDeletedColumn)
        => !hasDeletedColumn
            ? "1 = 1"
            : string.IsNullOrWhiteSpace(alias)
                ? "ISNULL([is_deleted], 0) = 0"
                : $"ISNULL([{alias}].[is_deleted], 0) = 0";

    private static string OptionalProjection(string alias, string column, IReadOnlySet<string> columns)
        => columns.Contains(column)
            ? $"[{alias}].[{column}] AS [{column}]"
            : $"CAST(NULL AS {OptionalSqlType(column)}) AS [{column}]";

    private static string OptionalSqlType(string column)
        => column switch
        {
            "date_created" or "date_updated" => "datetime2",
            "created_by_user_code" or "modified_by_user_code" => "int",
            "is_deleted" => "bit",
            _ => "sql_variant"
        };

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
