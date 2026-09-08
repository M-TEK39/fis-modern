using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.System;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads and writes notices through the expanded tables when their business
/// columns exist, and preserves the client-era stored-procedure contract when
/// they do not. Audit columns are optional on the expanded path.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers and stored procedure names are fixed; all submitted values are parameters."
)]
public sealed class NoticeRepository : INoticeRepository
{
    private const string NoticeTableName = "Notices";
    private const string ScheduleTableName = "NoticeSchedule";

    private static readonly string[] NoticeColumns =
    [
        "notice_id",
        "notice_date",
        "notice_from",
        "notice_title",
        "notice_body",
        "notice_person",
        "notice_person_title",
    ];

    private static readonly string[] OptionalNoticeColumns =
    [
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;

    public NoticeRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Notice?> GetByIdAsync(int noticeId) =>
        await WithSchemaAsync(
            async (connection, transaction, schema) =>
                schema.IsModern
                    ? await ReadModernSingleAsync(
                        connection,
                        transaction,
                        schema.NoticeColumns,
                        noticeId
                    )
                    : await ReadStoredNoticeAsync(
                        connection,
                        transaction,
                        "DEV_SEL_Notice",
                        noticeId
                    )
        );

    public async Task<IEnumerable<Notice>> GetAllAsync() =>
        await WithSchemaAsync(
            async (connection, transaction, schema) =>
                schema.IsModern
                    ? await ReadModernAsync(
                        connection,
                        transaction,
                        schema.NoticeColumns,
                        $"WHERE {ActiveFilter("n", schema.NoticeColumns.Contains("is_deleted"))} ORDER BY COALESCE([n].[notice_date], {DateCreatedExpression("n", schema.NoticeColumns.Contains("date_created"))}) DESC, [n].[notice_id] DESC",
                        configure: null,
                        single: false
                    )
                    : await ReadStoredNoticesAsync(
                        connection,
                        transaction,
                        "DEV_SEL_ActiveNoticesForDisplay"
                    )
        );

    public async Task<IEnumerable<Notice>> GetActiveNoticesAsync() =>
        await WithSchemaAsync(
            async (connection, transaction, schema) =>
                schema.IsModern
                    ? await ReadModernAsync(
                        connection,
                        transaction,
                        schema.NoticeColumns,
                        $"WHERE {ActiveFilter("n", schema.NoticeColumns.Contains("is_deleted"))} AND EXISTS (SELECT 1 FROM [dbo].[{ScheduleTableName}] AS [s] WHERE [s].[notice_id] = [n].[notice_id] AND {ActiveFilter("s", schema.ScheduleColumns.Contains("is_deleted"))} AND [s].[start_date] <= CAST(GETDATE() AS date) AND ([s].[end_date] IS NULL OR [s].[end_date] >= CAST(GETDATE() AS date))) ORDER BY COALESCE((SELECT MIN(COALESCE([s].[sort_order], 2147483647)) FROM [dbo].[{ScheduleTableName}] AS [s] WHERE [s].[notice_id] = [n].[notice_id] AND {ActiveFilter("s", schema.ScheduleColumns.Contains("is_deleted"))} AND [s].[start_date] <= CAST(GETDATE() AS date) AND ([s].[end_date] IS NULL OR [s].[end_date] >= CAST(GETDATE() AS date))), 2147483647), COALESCE([n].[notice_date], {DateCreatedExpression("n", schema.NoticeColumns.Contains("date_created"))}) DESC",
                        configure: null,
                        single: false
                    )
                    : await ReadStoredNoticesAsync(
                        connection,
                        transaction,
                        "DEV_SEL_ActiveNoticesForDisplay"
                    )
        );

    public async Task<IEnumerable<Notice>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate
    ) =>
        await WithSchemaAsync<IEnumerable<Notice>>(
            async (connection, transaction, schema) =>
                schema.IsModern
                    ? await ReadModernAsync(
                        connection,
                        transaction,
                        schema.NoticeColumns,
                        $"WHERE {ActiveFilter("n", schema.NoticeColumns.Contains("is_deleted"))} AND [n].[notice_date] >= @startDate AND [n].[notice_date] <= @endDate ORDER BY [n].[notice_date] DESC, [n].[notice_id] DESC",
                        command =>
                        {
                            NoticeRepositorySupport.AddParameter(
                                command,
                                "@startDate",
                                DbType.DateTime2,
                                startDate
                            );
                            NoticeRepositorySupport.AddParameter(
                                command,
                                "@endDate",
                                DbType.DateTime2,
                                endDate
                            );
                        },
                        single: false
                    )
                    : await ReadStoredNoticesInDateRangeAsync(
                        connection,
                        transaction,
                        startDate,
                        endDate
                    )
        );

    public async Task<Notice> CreateAsync(Notice notice, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(notice);

        return await WithSchemaAsync(
            async (connection, transaction, schema) =>
            {
                var noticeDate = notice.notice_date ?? DateTime.UtcNow;
                if (!schema.IsModern)
                {
                    notice.notice_id = await ExecuteLegacyNoticeWriteAsync(
                        connection,
                        transaction,
                        notice,
                        id: 0,
                        noticeDate
                    );
                    return notice;
                }

                var columns = schema.NoticeColumns;
                var now = DateTime.UtcNow;
                var values = new List<WriteValue>
                {
                    new("notice_date", "@noticeDate", DbType.DateTime2, noticeDate),
                    new("notice_from", "@noticeFrom", DbType.String, notice.notice_from),
                    new("notice_title", "@noticeTitle", DbType.String, notice.notice_title),
                    new("notice_body", "@noticeBody", DbType.String, notice.notice_body),
                    new("notice_person", "@noticePerson", DbType.String, notice.notice_person),
                    new(
                        "notice_person_title",
                        "@noticePersonTitle",
                        DbType.String,
                        notice.notice_person_title
                    ),
                };
                AddOptionalValue(
                    values,
                    columns,
                    "date_created",
                    "@dateCreated",
                    DbType.DateTime2,
                    now
                );
                AddOptionalValue(
                    values,
                    columns,
                    "created_by_user_code",
                    "@createdByUserCode",
                    DbType.Int32,
                    currentUserId
                );
                AddOptionalValue(
                    values,
                    columns,
                    "is_deleted",
                    "@isDeleted",
                    DbType.Boolean,
                    false
                );

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"""
                INSERT INTO [dbo].[{NoticeTableName}] ({string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}]")
                )})
                OUTPUT INSERTED.[notice_id]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
                AddParameters(command, values);
                notice.notice_id = Convert.ToInt32(await command.ExecuteScalarAsync());
                notice.notice_date = noticeDate;
                notice.date_created = now;
                notice.created_by_user_code = currentUserId;
                notice.is_deleted = false;
                return notice;
            }
        );
    }

    public async Task<Notice> UpdateAsync(Notice notice, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(notice);

        return await WithSchemaAsync(
            async (connection, transaction, schema) =>
            {
                if (!schema.IsModern)
                {
                    _ =
                        await ReadStoredNoticeAsync(
                            connection,
                            transaction,
                            "DEV_SEL_Notice",
                            notice.notice_id
                        )
                        ?? throw new InvalidOperationException(
                            $"Notice with notice_id {notice.notice_id} not found"
                        );
                    await ExecuteLegacyNoticeWriteAsync(
                        connection,
                        transaction,
                        notice,
                        notice.notice_id,
                        notice.notice_date ?? DateTime.UtcNow
                    );
                    return notice;
                }

                var columns = schema.NoticeColumns;
                var values = new List<WriteValue>
                {
                    new("notice_date", "@noticeDate", DbType.DateTime2, notice.notice_date),
                    new("notice_from", "@noticeFrom", DbType.String, notice.notice_from),
                    new("notice_title", "@noticeTitle", DbType.String, notice.notice_title),
                    new("notice_body", "@noticeBody", DbType.String, notice.notice_body),
                    new("notice_person", "@noticePerson", DbType.String, notice.notice_person),
                    new(
                        "notice_person_title",
                        "@noticePersonTitle",
                        DbType.String,
                        notice.notice_person_title
                    ),
                };
                AddOptionalValue(
                    values,
                    columns,
                    "date_updated",
                    "@dateUpdated",
                    DbType.DateTime2,
                    DateTime.UtcNow
                );
                AddOptionalValue(
                    values,
                    columns,
                    "modified_by_user_code",
                    "@modifiedByUserCode",
                    DbType.Int32,
                    currentUserId
                );

                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText = $"""
                UPDATE [dbo].[{NoticeTableName}]
                SET {string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}] = {value.Parameter}")
                )}
                WHERE [notice_id] = @noticeId
                  AND {ActiveFilter(string.Empty, columns.Contains("is_deleted"))}
                """;
                AddParameters(command, values);
                NoticeRepositorySupport.AddParameter(
                    command,
                    "@noticeId",
                    DbType.Int32,
                    notice.notice_id
                );
                if (await command.ExecuteNonQueryAsync() == 0)
                {
                    throw new InvalidOperationException(
                        $"Notice with notice_id {notice.notice_id} not found"
                    );
                }

                return notice;
            }
        );
    }

    public async Task DeleteAsync(int noticeId, int currentUserId) =>
        await WithSchemaAsync<int>(
            async (connection, transaction, schema) =>
            {
                if (!schema.IsModern)
                {
                    // The client-era FISNotices contract exposes schedule deletion
                    // only; it has no notice-delete procedure. Do not guess a
                    // destructive legacy table operation here.
                    return 0;
                }

                var columns = schema.NoticeColumns;
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                if (columns.Contains("is_deleted"))
                {
                    var assignments = new List<string> { "[is_deleted] = 1" };
                    if (columns.Contains("date_updated"))
                    {
                        assignments.Add("[date_updated] = @dateUpdated");
                        NoticeRepositorySupport.AddParameter(
                            command,
                            "@dateUpdated",
                            DbType.DateTime2,
                            DateTime.UtcNow
                        );
                    }

                    if (columns.Contains("modified_by_user_code"))
                    {
                        assignments.Add("[modified_by_user_code] = @modifiedByUserCode");
                        NoticeRepositorySupport.AddParameter(
                            command,
                            "@modifiedByUserCode",
                            DbType.Int32,
                            currentUserId
                        );
                    }

                    command.CommandText =
                        $"UPDATE [dbo].[{NoticeTableName}] SET {string.Join(", ", assignments)} WHERE [notice_id] = @noticeId AND {ActiveFilter(string.Empty, schema.NoticeColumns.Contains("is_deleted"))}";
                }
                else
                {
                    command.CommandText =
                        $"DELETE FROM [dbo].[{NoticeTableName}] WHERE [notice_id] = @noticeId";
                }

                NoticeRepositorySupport.AddParameter(command, "@noticeId", DbType.Int32, noticeId);
                await command.ExecuteNonQueryAsync();
                return 0;
            }
        );

    private async Task<T> WithSchemaAsync<T>(
        Func<DbConnection, DbTransaction?, NoticeRepositorySupport.NoticeSchema, Task<T>> operation
    )
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

    private static async Task<List<Notice>> ReadModernAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlySet<string> noticeColumns,
        string whereClause,
        Action<DbCommand>? configure,
        bool single,
        bool includeActiveSchedule = false
    )
    {
        var projection = NoticeColumns
            .Select(column => $"[n].[{column}] AS [{column}]")
            .Concat(
                OptionalNoticeColumns.Select(column =>
                    OptionalProjection("n", column, noticeColumns)
                )
            )
            .ToArray();
        var scheduleJoin = includeActiveSchedule
            ? $"INNER JOIN [dbo].[{ScheduleTableName}] AS [s] ON [s].[notice_id] = [n].[notice_id]"
            : string.Empty;
        var distinct = includeActiveSchedule ? "DISTINCT " : string.Empty;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            SELECT {distinct}{string.Join(", ", projection)}
            FROM [dbo].[{NoticeTableName}] AS [n]
            {scheduleJoin}
            {whereClause}
            """;
        configure?.Invoke(command);

        var results = new List<Notice>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(MapModernNotice(reader));
            if (single)
            {
                break;
            }
        }

        return results;
    }

    private static async Task<Notice?> ReadModernSingleAsync(
        DbConnection connection,
        DbTransaction? transaction,
        IReadOnlySet<string> noticeColumns,
        int noticeId
    )
    {
        var notices = await ReadModernAsync(
            connection,
            transaction,
            noticeColumns,
            $"WHERE [n].[notice_id] = @noticeId AND {ActiveFilter("n", noticeColumns.Contains("is_deleted"))}",
            command =>
                NoticeRepositorySupport.AddParameter(command, "@noticeId", DbType.Int32, noticeId),
            single: true
        );
        return notices.SingleOrDefault();
    }

    private static async Task<Notice?> ReadStoredNoticeAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string procedure,
        int noticeId
    )
    {
        await using var command = CreateStoredProcedure(connection, transaction, procedure);
        NoticeRepositorySupport.AddParameter(command, "@ID", DbType.Int32, noticeId);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapStoredNotice(reader) : null;
    }

    private static async Task<List<Notice>> ReadStoredNoticesAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string procedure
    )
    {
        await using var command = CreateStoredProcedure(connection, transaction, procedure);
        await using var reader = await command.ExecuteReaderAsync();
        var results = new List<Notice>();
        while (await reader.ReadAsync())
        {
            results.Add(MapStoredNotice(reader));
        }

        return results;
    }

    private static async Task<List<Notice>> ReadStoredNoticesInDateRangeAsync(
        DbConnection connection,
        DbTransaction? transaction,
        DateTime startDate,
        DateTime endDate
    ) =>
        (await ReadStoredNoticesAsync(connection, transaction, "DEV_SEL_ActiveNoticesForDisplay"))
            .Where(notice => notice.notice_date >= startDate && notice.notice_date <= endDate)
            .ToList();

    private static async Task<int> ExecuteLegacyNoticeWriteAsync(
        DbConnection connection,
        DbTransaction? transaction,
        Notice notice,
        int id,
        DateTime noticeDate
    )
    {
        await using var command = CreateStoredProcedure(connection, transaction, "DEV_INS_Notice");
        NoticeRepositorySupport.AddParameter(command, "@newDate", DbType.DateTime2, noticeDate);
        NoticeRepositorySupport.AddStringParameter(command, "@newFrom", notice.notice_from);
        NoticeRepositorySupport.AddStringParameter(command, "@newTitle", notice.notice_title);
        NoticeRepositorySupport.AddStringParameter(command, "@newBody", notice.notice_body);
        NoticeRepositorySupport.AddStringParameter(command, "@newResPerson", notice.notice_person);
        NoticeRepositorySupport.AddParameter(command, "@Id", DbType.Int32, id);
        NoticeRepositorySupport.AddStringParameter(
            command,
            "@newResPersonTItle",
            notice.notice_person_title
        );
        var result = await command.ExecuteScalarAsync();
        return result is null or DBNull ? id : Convert.ToInt32(result);
    }

    private static DbCommand CreateStoredProcedure(
        DbConnection connection,
        DbTransaction? transaction,
        string procedure
    )
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = procedure;
        command.CommandType = CommandType.StoredProcedure;
        return command;
    }

    private static Notice MapModernNotice(DbDataReader reader) =>
        new()
        {
            notice_id = NoticeRepositorySupport.ReadInt32(reader, "notice_id") ?? 0,
            notice_date = NoticeRepositorySupport.ReadDateTime(reader, "notice_date"),
            notice_from = NoticeRepositorySupport.ReadString(reader, "notice_from"),
            notice_title = NoticeRepositorySupport.ReadString(reader, "notice_title"),
            notice_body = NoticeRepositorySupport.ReadString(reader, "notice_body"),
            notice_person = NoticeRepositorySupport.ReadString(reader, "notice_person"),
            notice_person_title = NoticeRepositorySupport.ReadString(reader, "notice_person_title"),
            date_created =
                NoticeRepositorySupport.ReadDateTime(reader, "date_created") ?? DateTime.MinValue,
            date_updated = NoticeRepositorySupport.ReadDateTime(reader, "date_updated"),
            created_by_user_code = NoticeRepositorySupport.ReadInt32(
                reader,
                "created_by_user_code"
            ),
            modified_by_user_code = NoticeRepositorySupport.ReadInt32(
                reader,
                "modified_by_user_code"
            ),
            is_deleted = NoticeRepositorySupport.ReadBoolean(reader, "is_deleted"),
        };

    private static Notice MapStoredNotice(DbDataReader reader) =>
        new()
        {
            notice_id =
                NoticeRepositorySupport.ReadInt32(reader, "notice_id", "NoticeID", "ID") ?? 0,
            notice_date = NoticeRepositorySupport.ReadDateTime(reader, "notice_date", "Date"),
            notice_from = NoticeRepositorySupport.ReadString(
                reader,
                "notice_from",
                "From",
                "FromField"
            ),
            notice_title = NoticeRepositorySupport.ReadString(
                reader,
                "notice_title",
                "Title",
                "TitleField"
            ),
            notice_body = NoticeRepositorySupport.ReadString(
                reader,
                "notice_body",
                "Body",
                "BodyText"
            ),
            notice_person = NoticeRepositorySupport.ReadString(
                reader,
                "notice_person",
                "Person",
                "ResponsiblePerson"
            ),
            notice_person_title = NoticeRepositorySupport.ReadString(
                reader,
                "notice_person_title",
                "PersonTitle",
                "ResponsiblePersonTitle"
            ),
            date_created =
                NoticeRepositorySupport.ReadDateTime(
                    reader,
                    "date_created",
                    "CreatedDate",
                    "CreateDate"
                ) ?? DateTime.MinValue,
        };

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
            NoticeRepositorySupport.AddParameter(command, value.Parameter, value.Type, value.Value);
        }
    }

    private static string ActiveFilter(string alias, bool hasDeletedColumn)
    {
        if (!hasDeletedColumn)
        {
            return "1 = 1";
        }

        return string.IsNullOrWhiteSpace(alias)
            ? "ISNULL([is_deleted], 0) = 0"
            : $"ISNULL([{alias}].[is_deleted], 0) = 0";
    }

    private static string DateCreatedExpression(string alias, bool hasDateCreatedColumn) =>
        hasDateCreatedColumn ? $"[{alias}].[date_created]" : "CAST('1900-01-01' AS datetime2)";

    private static string OptionalProjection(
        string alias,
        string column,
        IReadOnlySet<string> columns
    ) =>
        columns.Contains(column)
            ? $"[{alias}].[{column}] AS [{column}]"
            : $"CAST(NULL AS {OptionalSqlType(column)}) AS [{column}]";

    private static string OptionalSqlType(string column) =>
        column switch
        {
            "date_created" or "date_updated" => "datetime2",
            "created_by_user_code" or "modified_by_user_code" => "int",
            "is_deleted" => "bit",
            _ => "sql_variant",
        };

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
