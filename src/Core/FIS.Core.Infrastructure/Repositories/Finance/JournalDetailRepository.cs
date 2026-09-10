using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Financial;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Reads and writes journal_detail through an allow-listed compatibility
/// projection. The client table predates the expanded audit columns and uses
/// decimal/smalldatetime legacy types, while clean installs may contain the
/// expanded columns. EF's static projection cannot safely serve both shapes.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "All identifiers are fixed compatibility columns and all submitted values are parameters."
)]
public sealed class JournalDetailRepository : IJournalDetailRepository
{
    private const string TableName = "journal_detail";

    private static readonly string[] RequiredColumns =
    [
        "journal_detail_id",
        "journal_detail_code",
        "journal_code",
        "department_code",
        "site_code",
        "vmf_code",
        "journal_detail_type_code",
        "journal_detail_isdebit",
        "journal_detail_quantity",
        "journal_detail_tariff",
        "journal_detail_amount",
        "journal_detail_description",
        "journal_detail_reversalof",
        "journal_detail_date_created",
        "journal_detail_isaccepted",
        "journal_detail_financial_year",
        "journal_detail_date",
    ];

    private readonly FisDbContext _context;
    private readonly ILogger<JournalDetailRepository> _logger;
    private IReadOnlyDictionary<string, ColumnInfo>? _columns;

    public JournalDetailRepository(FisDbContext context, ILogger<JournalDetailRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IEnumerable<JournalDetail>> GetAllAsync() => await QueryAsync();

    public async Task<JournalDetail?> GetByIdAsync(int journalDetailId) =>
        (
            await QueryAsync(
                "j.[journal_detail_id] = @journalDetailId",
                command => AddParameter(command, "@journalDetailId", DbType.Int32, journalDetailId)
            )
        ).SingleOrDefault();

    public async Task<JournalDetail?> GetByCodeAsync(Guid journalDetailCode) =>
        (
            await QueryAsync(
                "j.[journal_detail_code] = @journalDetailCode",
                command => AddParameter(
                    command,
                    "@journalDetailCode",
                    DbType.Guid,
                    journalDetailCode
                )
            )
        ).SingleOrDefault();

    public async Task<IEnumerable<JournalDetail>> GetByVehicleAsync(int vmfCode) =>
        await QueryAsync(
            "j.[vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode)
        );

    public async Task<IEnumerable<JournalDetail>> GetBySiteAsync(short siteCode) =>
        await QueryAsync(
            "j.[site_code] = @siteCode",
            command => AddParameter(command, "@siteCode", DbType.Int16, siteCode)
        );

    public async Task<IEnumerable<JournalDetail>> GetByDepartmentAsync(int departmentCode) =>
        await QueryAsync(
            "j.[department_code] = @departmentCode",
            command => AddParameter(command, "@departmentCode", DbType.Int16, departmentCode)
        );

    public async Task<IEnumerable<JournalDetail>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate
    ) =>
        await QueryAsync(
            $"{DateExpression()} >= @startDate AND {DateExpression()} <= @endDate",
            command =>
            {
                AddParameter(command, "@startDate", DbType.DateTime2, startDate);
                AddParameter(command, "@endDate", DbType.DateTime2, endDate);
            }
        );

    public async Task<IEnumerable<JournalDetail>> GetByFinancialYearAsync(string financialYear) =>
        await QueryAsync(
            "j.[journal_detail_financial_year] = @financialYear",
            command => AddParameter(command, "@financialYear", DbType.String, financialYear)
        );

    public async Task<IEnumerable<JournalDetail>> GetReversalsForJournalAsync(
        Guid journalDetailCode
    ) =>
        await QueryAsync(
            "j.[journal_detail_reversalof] = @journalDetailCode",
            command => AddParameter(
                command,
                "@journalDetailCode",
                DbType.Guid,
                journalDetailCode
            )
        );

    public async Task<JournalDetail> CreateAsync(JournalDetail journalDetail, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(journalDetail);
        var columns = await GetColumnsAsync();

        if (journalDetail.journal_detail_code == Guid.Empty)
            journalDetail.journal_detail_code = Guid.NewGuid();
        if (journalDetail.journal_detail_date_created == DateTime.MinValue)
            journalDetail.journal_detail_date_created = DateTime.UtcNow;
        if (journalDetail.journal_detail_date == DateTime.MinValue)
            journalDetail.journal_detail_date = journalDetail.journal_detail_date_created;

        var values = BuildValues(journalDetail, columns, currentUserId, includeCreatedValues: true);
        var scope = await OpenConnectionAsync();
        try
        {
            await using var command = scope.Connection.CreateCommand();
            command.Transaction = CurrentTransaction;
            command.CommandText = $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[journal_detail_id] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
            AddParameters(command, values);
            journalDetail.journal_detail_id = Convert.ToInt32(await command.ExecuteScalarAsync());
            return journalDetail;
        }
        finally
        {
            await CloseConnectionAsync(scope);
        }
    }

    public async Task UpdateAsync(JournalDetail journalDetail, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(journalDetail);
        var columns = await GetColumnsAsync();
        journalDetail.journal_detail_date_updated = DateTime.UtcNow;

        var values = BuildValues(journalDetail, columns, currentUserId, includeCreatedValues: false);
        var scope = await OpenConnectionAsync();
        try
        {
            await using var command = scope.Connection.CreateCommand();
            command.Transaction = CurrentTransaction;
            command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [journal_detail_id] = @journalDetailId";
            AddParameters(command, values);
            AddParameter(command, "@journalDetailId", DbType.Int32, journalDetail.journal_detail_id);
            if (await command.ExecuteNonQueryAsync() == 0)
                throw new KeyNotFoundException(
                    $"JournalDetail with journal_detail_id {journalDetail.journal_detail_id} not found"
                );
        }
        finally
        {
            await CloseConnectionAsync(scope);
        }
    }

    public async Task DeleteAsync(int journalDetailId, int currentUserId)
    {
        var columns = await GetColumnsAsync();
        var values = new List<WriteValue>();
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, true);
        AddValue(values, columns, "journal_detail_inactive", "@inactive", DbType.Boolean, true);
        AddValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
        AddValue(
            values,
            columns,
            "journal_detail_date_updated",
            "@detailDateUpdated",
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

        if (values.Count == 0)
            throw new InvalidOperationException("The journal_detail table has no supported delete column.");

        var scope = await OpenConnectionAsync();
        try
        {
            await using var command = scope.Connection.CreateCommand();
            command.Transaction = CurrentTransaction;
            command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [journal_detail_id] = @journalDetailId";
            AddParameters(command, values);
            AddParameter(command, "@journalDetailId", DbType.Int32, journalDetailId);
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            await CloseConnectionAsync(scope);
        }
    }

    private async Task<List<JournalDetail>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var columns = await GetColumnsAsync();
        var scope = await OpenConnectionAsync();
        try
        {
            await using var command = scope.Connection.CreateCommand();
            command.Transaction = CurrentTransaction;
            var conditions = new List<string> { NotDeletedExpression(columns) };
            if (!string.IsNullOrWhiteSpace(predicate))
                conditions.Add($"({predicate})");

            command.CommandText = $"SELECT {BuildProjection(columns)} FROM [dbo].[{TableName}] j WHERE {string.Join(" AND ", conditions)} ORDER BY {DateExpression(columns)} DESC, j.[journal_detail_id] DESC";
            configure?.Invoke(command);

            var results = new List<JournalDetail>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                results.Add(Map(reader));
            return results;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error querying journal_detail compatibility projection");
            throw;
        }
        finally
        {
            await CloseConnectionAsync(scope);
        }
    }

    private async Task<IReadOnlyDictionary<string, ColumnInfo>> GetColumnsAsync()
    {
        if (_columns is not null)
            return _columns;

        var scope = await OpenConnectionAsync();
        try
        {
            await using var command = scope.Connection.CreateCommand();
            command.Transaction = CurrentTransaction;
            command.CommandText = "SELECT [COLUMN_NAME], [DATA_TYPE] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table";
            AddParameter(command, "@schema", DbType.String, "dbo");
            AddParameter(command, "@table", DbType.String, TableName);

            var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                columns[name] = new ColumnInfo(name, reader.GetString(1));
            }

            var missing = RequiredColumns.Where(column => !columns.ContainsKey(column)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The journal_detail table is missing required legacy columns: {string.Join(", ", missing)}"
                );
            }

            _columns = columns;
            return columns;
        }
        finally
        {
            await CloseConnectionAsync(scope);
        }
    }

    private static List<WriteValue> BuildValues(
        JournalDetail journalDetail,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        int currentUserId,
        bool includeCreatedValues
    )
    {
        var values = new List<WriteValue>();
        AddValue(values, columns, "journal_detail_code", "@detailCode", DbType.Guid, journalDetail.journal_detail_code);
        AddValue(values, columns, "journal_code", "@journalCode", DbType.Int64, journalDetail.journal_code);
        AddValue(values, columns, "department_code", "@departmentCode", DbType.Int16, journalDetail.department_code);
        AddValue(values, columns, "site_code", "@siteCode", DbType.Int16, journalDetail.site_code == 0 ? null : journalDetail.site_code);
        AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, journalDetail.vmf_code == 0 ? null : journalDetail.vmf_code);
        AddValue(values, columns, "journal_detail_type_code", "@detailTypeCode", DbType.Byte, journalDetail.journal_detail_type_code);
        AddValue(values, columns, "journal_detail_isdebit", "@isDebit", DbType.Boolean, journalDetail.journal_detail_isdebit);
        AddValue(values, columns, "journal_detail_quantity", "@quantity", DbType.Decimal, journalDetail.journal_detail_quantity);
        AddValue(values, columns, "journal_detail_tariff", "@tariff", DbType.Decimal, journalDetail.journal_detail_tariff);
        AddValue(values, columns, "journal_detail_amount", "@amount", DbType.Decimal, journalDetail.journal_detail_amount);
        AddValue(values, columns, "journal_detail_description", "@description", DbType.String, journalDetail.journal_detail_description ?? string.Empty);
        AddValue(values, columns, "journal_detail_reversalof", "@reversalOf", DbType.Guid, journalDetail.journal_detail_reversalof);
        AddValue(values, columns, "journal_detail_date_created", "@detailDateCreated", DbType.DateTime2, journalDetail.journal_detail_date_created);
        AddValue(values, columns, "journal_detail_date_updated", "@detailDateUpdated", DbType.DateTime2, journalDetail.journal_detail_date_updated);
        AddValue(values, columns, "journal_detail_date_posted", "@detailDatePosted", DbType.DateTime2, journalDetail.journal_detail_date_posted);
        AddValue(values, columns, "journal_detail_isaccepted", "@isAccepted", DbType.Boolean, journalDetail.journal_detail_isaccepted);
        AddValue(values, columns, "journal_detail_financial_year", "@financialYear", DbType.String, journalDetail.journal_detail_financial_year);
        AddValue(values, columns, "journal_detail_date", "@detailDate", DbType.DateTime2, journalDetail.journal_detail_date);
        AddValue(values, columns, "journal_detail_date_approved", "@dateApproved", DbType.DateTime2, journalDetail.journal_detail_date_approved);
        AddValue(values, columns, "journal_detail_rebill_code", "@rebillCode", DbType.Guid, journalDetail.journal_detail_rebill_code);
        AddValue(values, columns, "journal_detail_isreversaldenied", "@isReversalDenied", DbType.Boolean, journalDetail.journal_detail_isreversaldenied);

        if (includeCreatedValues)
        {
            AddValue(values, columns, "created_by_user_code", "@createdByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : null);
            AddValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, journalDetail.date_created == DateTime.MinValue ? journalDetail.journal_detail_date_created : journalDetail.date_created);
        }

        AddValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, journalDetail.date_updated ?? journalDetail.journal_detail_date_updated);
        AddValue(values, columns, "modified_by_user_code", "@modifiedByUserCode", DbType.Int32, currentUserId > 0 ? currentUserId : journalDetail.modified_by_user_code);
        AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, journalDetail.is_deleted);
        AddValue(values, columns, "journal_detail_inactive", "@inactive", DbType.Boolean, journalDetail.is_deleted);
        AddValue(values, columns, "journal_detail_inactive_date", "@inactiveDate", DbType.DateTime2, journalDetail.is_deleted ? DateTime.UtcNow : null);
        return values;
    }

    private static string BuildProjection(IReadOnlyDictionary<string, ColumnInfo> columns)
    {
        var date = DateExpression(columns);
        var createdDate = columns.ContainsKey("date_created")
            ? "TRY_CONVERT(datetime2, j.[date_created])"
            : "TRY_CONVERT(datetime2, j.[journal_detail_date_created])";
        var updatedDate = columns.ContainsKey("date_updated")
            ? "TRY_CONVERT(datetime2, j.[date_updated])"
            : "TRY_CONVERT(datetime2, j.[journal_detail_date_updated])";
        var deleted = NotDeletedValueExpression(columns);
        var debitAmount = columns.ContainsKey("journal_detail_debitamount")
            ? "TRY_CONVERT(decimal(19, 5), j.[journal_detail_debitamount])"
            : "CASE WHEN j.[journal_detail_isdebit] = 1 THEN TRY_CONVERT(decimal(19, 5), j.[journal_detail_amount]) ELSE -TRY_CONVERT(decimal(19, 5), j.[journal_detail_amount]) END";

        return string.Join(
            ", ",
            [
                "j.[journal_detail_id] AS [journal_detail_id]",
                "j.[journal_detail_code] AS [journal_detail_code]",
                ValueExpression(columns, "journal_code", "bigint") + " AS [journal_code]",
                "j.[department_code] AS [department_code]",
                ValueExpression(columns, "site_code", "smallint") + " AS [site_code]",
                ValueExpression(columns, "vmf_code", "int") + " AS [vmf_code]",
                "j.[journal_detail_type_code] AS [journal_detail_type_code]",
                "j.[journal_detail_isdebit] AS [journal_detail_isdebit]",
                "TRY_CONVERT(int, j.[journal_detail_quantity]) AS [journal_detail_quantity]",
                "TRY_CONVERT(decimal(19, 5), j.[journal_detail_tariff]) AS [journal_detail_tariff]",
                "TRY_CONVERT(decimal(19, 5), j.[journal_detail_amount]) AS [journal_detail_amount]",
                "CONVERT(varchar(max), j.[journal_detail_description]) AS [journal_detail_description]",
                ValueExpression(columns, "journal_detail_reversalof", "uniqueidentifier") + " AS [journal_detail_reversalof]",
                "TRY_CONVERT(datetime2, j.[journal_detail_date_created]) AS [journal_detail_date_created]",
                ValueExpression(columns, "journal_detail_date_updated", "datetime2") + " AS [journal_detail_date_updated]",
                ValueExpression(columns, "journal_detail_date_posted", "datetime2") + " AS [journal_detail_date_posted]",
                "j.[journal_detail_isaccepted] AS [journal_detail_isaccepted]",
                ValueExpression(columns, "journal_detail_financial_year", "varchar(10)") + " AS [journal_detail_financial_year]",
                $"{date} AS [journal_detail_date]",
                ValueExpression(columns, "journal_detail_date_approved", "datetime2") + " AS [journal_detail_date_approved]",
                ValueExpression(columns, "journal_detail_rebill_code", "uniqueidentifier") + " AS [journal_detail_rebill_code]",
                ValueExpression(columns, "journal_detail_isreversaldenied", "bit") + " AS [journal_detail_isreversaldenied]",
                $"{debitAmount} AS [journal_detail_debitamount]",
                $"{createdDate} AS [date_created]",
                $"{updatedDate} AS [date_updated]",
                ValueExpression(columns, "created_by_user_code", "int") + " AS [created_by_user_code]",
                ValueExpression(columns, "modified_by_user_code", "int") + " AS [modified_by_user_code]",
                $"{deleted} AS [is_deleted]",
            ]
        );
    }

    private static string DateExpression() =>
        "COALESCE(TRY_CONVERT(datetime2, j.[journal_detail_date]), TRY_CONVERT(datetime2, j.[journal_detail_date_created]), SYSUTCDATETIME())";

    private static string DateExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("journal_detail_date")
            ? DateExpression()
            : "TRY_CONVERT(datetime2, j.[journal_detail_date_created])";

    private static string NotDeletedExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        $"({NotDeletedValueExpression(columns)} = 0)";

    private static string NotDeletedValueExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("is_deleted")
            ? "ISNULL(CONVERT(bit, j.[is_deleted]), 0)"
            : columns.ContainsKey("journal_detail_inactive")
                ? "ISNULL(CONVERT(bit, j.[journal_detail_inactive]), 0)"
                : "CONVERT(bit, 0)";

    private static string ValueExpression(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string sqlType
    ) => columns.ContainsKey(column) ? $"j.[{column}]" : $"CAST(NULL AS {sqlType})";

    private static JournalDetail Map(DbDataReader reader) =>
        new()
        {
            journal_detail_id = ReadInt32(reader, "journal_detail_id"),
            journal_detail_code = ReadGuid(reader, "journal_detail_code"),
            journal_code = ReadInt64(reader, "journal_code"),
            department_code = ReadInt16(reader, "department_code") ?? 0,
            site_code = ReadInt16(reader, "site_code") ?? 0,
            vmf_code = ReadInt32Nullable(reader, "vmf_code") ?? 0,
            journal_detail_type_code = ReadByte(reader, "journal_detail_type_code") ?? 0,
            journal_detail_isdebit = ReadBoolean(reader, "journal_detail_isdebit") ?? false,
            journal_detail_quantity = ReadInt32(reader, "journal_detail_quantity"),
            journal_detail_tariff = ReadDecimal(reader, "journal_detail_tariff") ?? 0,
            journal_detail_amount = ReadDecimal(reader, "journal_detail_amount") ?? 0,
            journal_detail_description = ReadString(reader, "journal_detail_description"),
            journal_detail_reversalof = ReadGuidNullable(reader, "journal_detail_reversalof"),
            journal_detail_date_created = ReadDateTime(reader, "journal_detail_date_created") ?? DateTime.MinValue,
            journal_detail_date_updated = ReadDateTime(reader, "journal_detail_date_updated"),
            journal_detail_date_posted = ReadDateTime(reader, "journal_detail_date_posted"),
            journal_detail_isaccepted = ReadBoolean(reader, "journal_detail_isaccepted") ?? false,
            journal_detail_financial_year = ReadString(reader, "journal_detail_financial_year")?.Trim(),
            journal_detail_date = ReadDateTime(reader, "journal_detail_date") ?? DateTime.MinValue,
            journal_detail_date_approved = ReadDateTime(reader, "journal_detail_date_approved"),
            journal_detail_rebill_code = ReadGuidNullable(reader, "journal_detail_rebill_code"),
            journal_detail_isreversaldenied = ReadBoolean(reader, "journal_detail_isreversaldenied") ?? false,
            journal_detail_debitamount = ReadDecimal(reader, "journal_detail_debitamount"),
            date_created = ReadDateTime(reader, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code = ReadInt32Nullable(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt32Nullable(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted") ?? false,
        };

    private DbTransaction? CurrentTransaction =>
        _context.Database.CurrentTransaction?.GetDbTransaction();

    private async Task<(DbConnection Connection, bool ShouldClose)> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();
        return (connection, shouldClose);
    }

    private static async Task CloseConnectionAsync((DbConnection Connection, bool ShouldClose) scope)
    {
        if (scope.ShouldClose)
            await scope.Connection.CloseAsync();
    }

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (columns.ContainsKey(column))
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

    private static object? ReadValue(DbDataReader reader, string name)
    {
        var value = reader[name];
        return value == DBNull.Value ? null : value;
    }

    private static string? ReadString(DbDataReader reader, string name) =>
        Convert.ToString(ReadValue(reader, name));

    private static int ReadInt32(DbDataReader reader, string name) =>
        Convert.ToInt32(ReadValue(reader, name) ?? 0);

    private static int? ReadInt32Nullable(DbDataReader reader, string name) =>
        ReadValue(reader, name) is null ? null : Convert.ToInt32(ReadValue(reader, name));

    private static long? ReadInt64(DbDataReader reader, string name) =>
        ReadValue(reader, name) is null ? null : Convert.ToInt64(ReadValue(reader, name));

    private static short? ReadInt16(DbDataReader reader, string name) =>
        ReadValue(reader, name) is null ? null : Convert.ToInt16(ReadValue(reader, name));

    private static byte? ReadByte(DbDataReader reader, string name) =>
        ReadValue(reader, name) is null ? null : Convert.ToByte(ReadValue(reader, name));

    private static bool? ReadBoolean(DbDataReader reader, string name) =>
        ReadValue(reader, name) is null ? null : Convert.ToBoolean(ReadValue(reader, name));

    private static decimal? ReadDecimal(DbDataReader reader, string name) =>
        ReadValue(reader, name) is null ? null : Convert.ToDecimal(ReadValue(reader, name));

    private static DateTime? ReadDateTime(DbDataReader reader, string name) =>
        ReadValue(reader, name) is null ? null : Convert.ToDateTime(ReadValue(reader, name));

    private static Guid ReadGuid(DbDataReader reader, string name) =>
        ReadGuidNullable(reader, name) ?? Guid.Empty;

    private static Guid? ReadGuidNullable(DbDataReader reader, string name)
    {
        var value = ReadValue(reader, name);
        if (value is null)
            return null;
        if (value is Guid guid)
            return guid;
        return Guid.TryParse(Convert.ToString(value), out var parsed) ? parsed : null;
    }

    private sealed record ColumnInfo(string Name, string DataType);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
