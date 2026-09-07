using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

internal static class NoticeRepositorySupport
{
    public static DbTransaction? GetCurrentTransaction(Microsoft.EntityFrameworkCore.DbContext context)
        => context.Database.CurrentTransaction?.GetDbTransaction();

    public static async Task<HashSet<string>> GetColumnsAsync(
        DbConnection connection,
        DbTransaction? transaction,
        string tableName)
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

    public static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    public static void AddStringParameter(DbCommand command, string name, object? value)
        => AddParameter(command, name, DbType.String, value);

    public static int? ReadInt32(DbDataReader reader, params string[] names)
    {
        var ordinal = FindOrdinal(reader, names);
        return ordinal < 0 || reader.IsDBNull(ordinal)
            ? null
            : Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    public static int? ReadInt32IfAvailable(DbDataReader reader, string name)
        => ReadInt32(reader, name);

    public static DateTime? ReadDateTime(DbDataReader reader, params string[] names)
    {
        var ordinal = FindOrdinal(reader, names);
        return ordinal < 0 || reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDateTime(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    public static string? ReadString(DbDataReader reader, params string[] names)
    {
        var ordinal = FindOrdinal(reader, names);
        return ordinal < 0 || reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)?.TrimEnd();
    }

    public static bool ReadBoolean(DbDataReader reader, string name)
    {
        var ordinal = FindOrdinal(reader, name);
        return ordinal >= 0 && !reader.IsDBNull(ordinal) && Convert.ToBoolean(reader.GetValue(ordinal), CultureInfo.InvariantCulture);
    }

    public static bool HasModernSchema(
        IReadOnlySet<string> noticeColumns,
        IReadOnlySet<string> scheduleColumns)
        => RequiredNoticeColumns.All(noticeColumns.Contains)
            && RequiredScheduleColumns.All(scheduleColumns.Contains);

    public static async Task<NoticeSchema> GetSchemaAsync(
        DbConnection connection,
        DbTransaction? transaction)
        => new(
            await GetColumnsAsync(connection, transaction, "Notices"),
            await GetColumnsAsync(connection, transaction, "NoticeSchedule"));

    internal sealed record NoticeSchema(
        HashSet<string> NoticeColumns,
        HashSet<string> ScheduleColumns)
    {
        public bool IsModern => HasModernSchema(NoticeColumns, ScheduleColumns);
    }

    private static readonly string[] RequiredNoticeColumns =
    [
        "notice_id", "notice_date", "notice_from", "notice_title", "notice_body", "notice_person", "notice_person_title"
    ];

    private static readonly string[] RequiredScheduleColumns =
    [
        "notice_schedule_id", "notice_id", "title_field", "start_date", "end_date", "sort_order"
    ];

    private static int FindOrdinal(DbDataReader reader, params string[] names)
    {
        for (var index = 0; index < reader.FieldCount; index++)
        {
            foreach (var name in names)
            {
                if (string.Equals(reader.GetName(index), name, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }
        }

        return -1;
    }
}
