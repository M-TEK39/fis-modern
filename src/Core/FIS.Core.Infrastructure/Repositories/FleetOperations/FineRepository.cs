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
/// Fine persistence across the legacy and expanded Fines table shapes.
/// The legacy table contains the business columns Dept_person_name,
/// Dept_person_id, Document_type, and Traffic_dept_code; the expanded table
/// contains audit columns instead. Optional columns are selected at runtime.
/// </summary>
public class FineRepository : IFineRepository
{
    private const string VehicleTableName = "vehicle_master";
    private const string RegistrationTableName = "Registrations";

    private static readonly string[] CommonColumns =
    [
        "Fine_code",
        "vmf_code",
        "Offence_date",
        "Offence_reference",
        "Offence_issuer",
        "Fine_amount",
        "Appear_date",
        "Receive_gg_date",
        "Notify_dept_date",
        "Site_code",
        "Offence_name",
        "Fine_pay_date",
        "Withdraw_date",
        "Pay_due_date",
        "Issuer_notify_date",
    ];

    private static readonly string[] OptionalColumns =
    [
        "Dept_person_name",
        "Dept_person_id",
        "Document_type",
        "Traffic_dept_code",
        "date_created",
        "date_updated",
        "created_by_user_code",
        "modified_by_user_code",
        "is_deleted",
    ];

    private readonly FisDbContext _context;

    public FineRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<Fine?> GetByIdAsync(int fineCode)
    {
        return (
            await QueryAsync(
                "WHERE [Fine_code] = @fineCode",
                command => AddParameter(command, "@fineCode", DbType.Int32, fineCode)
            )
        ).SingleOrDefault();
    }

    public async Task<IEnumerable<Fine>> GetAllAsync()
    {
        return await QueryAsync();
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The paged query is composed only from fixed table/column allowlists and fixed predicates; request values are parameters."
    )]
    public async Task<FinePage> GetPageAsync(FinePageQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var requestedPage = Math.Max(1, query.Page);
        var searchType = string.Equals(query.SearchType, "GG", StringComparison.OrdinalIgnoreCase)
            ? "GG"
            : "GP";
        var searchQuery = query.SearchQuery?.Trim() ?? string.Empty;
        var allowedSiteCodes = query.AllowedSiteCodes is null
            ? null
            : query.AllowedSiteCodes.Where(code => code > 0).Distinct().ToArray();
        if (allowedSiteCodes is { Length: 0 })
        {
            return new FinePage([], 1, pageSize, 0);
        }

        var fineColumns = await GetAvailableColumnsAsync();
        var vehicleColumns =
            searchQuery.Length > 0
                ? await GetAvailableColumnsAsync(VehicleTableName)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var registrationColumns =
            searchType == "GP" && searchQuery.Length > 0
                ? await GetAvailableColumnsAsync(RegistrationTableName)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasVehicleJoin = searchQuery.Length > 0 && vehicleColumns.Contains("vmf_code");
        var hasHistoricalRegistration =
            searchType == "GP"
            && searchQuery.Length > 0
            && hasVehicleJoin
            && registrationColumns.Contains("vmf_code")
            && registrationColumns.Contains("RegistrationNumber");
        var vehicleJoin = hasVehicleJoin
            ? $"LEFT JOIN [dbo].[{VehicleTableName}] AS [v] ON [v].[vmf_code] = [f].[vmf_code]"
            : string.Empty;
        var conditions = new List<string>();

        if (fineColumns.Contains("is_deleted"))
        {
            conditions.Add("ISNULL([f].[is_deleted], 0) = 0");
        }

        if (allowedSiteCodes is not null && fineColumns.Contains("Site_code"))
        {
            conditions.Add(
                $"[f].[Site_code] IN ({string.Join(", ", allowedSiteCodes.Select((_, index) => $"@allowedSite{index}"))})"
            );
        }

        if (searchQuery.Length > 0)
        {
            var vehicleActivePredicate = vehicleColumns.Contains("is_deleted")
                ? "ISNULL([v].[is_deleted], 0) = 0"
                : "1 = 1";
            var vehicleSearchPredicate =
                !hasVehicleJoin ? "1 = 0"
                : searchType == "GG"
                    ? vehicleColumns.Contains("fleet_number")
                            ? $"{vehicleActivePredicate} AND LOWER(LTRIM(RTRIM(COALESCE([v].[fleet_number], N'')))) = @searchQuery"
                        : "1 = 0"
                : vehicleColumns.Contains("registration_number")
                    ? $"{vehicleActivePredicate} AND LOWER(LTRIM(RTRIM(COALESCE([v].[registration_number], N'')))) = @searchQuery"
                : "1 = 0";

            var searchPredicates = new List<string> { vehicleSearchPredicate };
            if (hasHistoricalRegistration)
            {
                var registrationActivePredicate = registrationColumns.Contains("is_deleted")
                    ? "ISNULL([r].[is_deleted], 0) = 0"
                    : "1 = 1";
                searchPredicates.Add(
                    $"EXISTS (SELECT 1 FROM [dbo].[{RegistrationTableName}] AS [r] WHERE [r].[vmf_code] = [f].[vmf_code] AND {vehicleActivePredicate} AND {registrationActivePredicate} AND LOWER(LTRIM(RTRIM(COALESCE([r].[RegistrationNumber], N'')))) = @searchQuery)"
                );
            }

            conditions.Add($"({string.Join(" OR ", searchPredicates)})");
        }

        var whereClause = conditions.Count == 0 ? "1 = 1" : string.Join(" AND ", conditions);
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var countCommand = connection.CreateCommand();
            countCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            countCommand.CommandText = $"""
                SELECT COUNT(1)
                FROM [dbo].[Fines] AS [f]
                {vehicleJoin}
                WHERE {whereClause}
                """;
            AddSearchParameter(countCommand, searchQuery, searchQuery.Length > 0);
            AddAllowedSiteParameters(countCommand, allowedSiteCodes);
            var total = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            var page = Math.Min(requestedPage, totalPages);
            var skip = checked((long)(page - 1) * pageSize);

            await using var dataCommand = connection.CreateCommand();
            dataCommand.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            dataCommand.CommandText = $"""
                SELECT {BuildPageProjection(fineColumns)}
                FROM [dbo].[Fines] AS [f]
                {vehicleJoin}
                WHERE {whereClause}
                ORDER BY COALESCE([f].[Offence_date], [f].[Receive_gg_date]) DESC, [f].[Fine_code] DESC
                OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
                """;
            AddSearchParameter(dataCommand, searchQuery, searchQuery.Length > 0);
            AddAllowedSiteParameters(dataCommand, allowedSiteCodes);
            AddParameter(dataCommand, "@skip", DbType.Int64, skip);
            AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

            var items = new List<Fine>();
            await using var reader = await dataCommand.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(MapFine(reader, fineColumns));
            }

            return new FinePage(items, page, pageSize, total);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IEnumerable<Fine>> GetByVehicleAsync(int vmfCode)
    {
        return await QueryAsync(
            "WHERE [vmf_code] = @vmfCode",
            command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode)
        );
    }

    public async Task<IEnumerable<Fine>> GetBySiteAsync(short siteCode)
    {
        return await QueryAsync(
            "WHERE [Site_code] = @siteCode",
            command => AddParameter(command, "@siteCode", DbType.Int16, siteCode)
        );
    }

    public async Task<IEnumerable<Fine>> GetUnpaidFinesAsync()
    {
        return await QueryAsync("WHERE [Fine_pay_date] IS NULL");
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SELECT list is composed only from fixed common columns and an allowlisted runtime column set; predicates are internal constants and values are parameters."
    )]
    private async Task<List<Fine>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
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
            command.CommandText = BuildSelectCommandText(availableColumns, predicate);
            configure?.Invoke(command);

            var results = new List<Fine>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapFine(reader, availableColumns));
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

    public async Task<Fine> CreateAsync(Fine fine, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(fine);
        await EnsureLegacyFineTriggersAsync();

        var availableColumns = await GetAvailableColumnsAsync();
        var now = DateTime.UtcNow;
        var values = BuildCommonWriteValues(fine);

        AddOptionalValue(
            values,
            availableColumns,
            "Dept_person_name",
            "@deptPersonName",
            DbType.String,
            fine.Dept_person_name
        );
        AddOptionalValue(
            values,
            availableColumns,
            "Dept_person_id",
            "@deptPersonId",
            DbType.String,
            fine.Dept_person_id
        );
        AddOptionalValue(
            values,
            availableColumns,
            "Document_type",
            "@documentType",
            DbType.String,
            fine.Document_type
        );
        AddOptionalValue(
            values,
            availableColumns,
            "Traffic_dept_code",
            "@trafficDeptCode",
            DbType.Int16,
            fine.Traffic_dept_code
        );
        AddOptionalValue(
            values,
            availableColumns,
            "date_created",
            "@dateCreated",
            DbType.DateTime2,
            now
        );
        AddOptionalValue(
            values,
            availableColumns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            fine.date_updated
        );
        AddOptionalValue(
            values,
            availableColumns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddOptionalValue(
            values,
            availableColumns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            null
        );
        AddOptionalValue(
            values,
            availableColumns,
            "is_deleted",
            "@isDeleted",
            DbType.Boolean,
            false
        );

        fine.date_created = now;
        fine.created_by_user_code = currentUserId > 0 ? currentUserId : null;
        fine.is_deleted = false;

        var fineCode = await ExecuteInsertAsync(values);
        fine.Fine_code = fineCode;
        return fine;
    }

    public async Task<Fine> UpdateAsync(Fine fine, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(fine);
        await EnsureLegacyFineTriggersAsync();

        var existing = await GetByIdAsync(fine.Fine_code);
        if (existing == null)
        {
            throw new InvalidOperationException($"Fine with Fine_code {fine.Fine_code} not found");
        }

        var availableColumns = await GetAvailableColumnsAsync();
        var values = BuildCommonWriteValues(fine);
        AddOptionalValue(
            values,
            availableColumns,
            "Dept_person_name",
            "@deptPersonName",
            DbType.String,
            fine.Dept_person_name
        );
        AddOptionalValue(
            values,
            availableColumns,
            "Dept_person_id",
            "@deptPersonId",
            DbType.String,
            fine.Dept_person_id
        );
        AddOptionalValue(
            values,
            availableColumns,
            "Document_type",
            "@documentType",
            DbType.String,
            fine.Document_type
        );
        AddOptionalValue(
            values,
            availableColumns,
            "Traffic_dept_code",
            "@trafficDeptCode",
            DbType.Int16,
            fine.Traffic_dept_code
        );

        var now = DateTime.UtcNow;
        AddOptionalValue(
            values,
            availableColumns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            now
        );
        AddOptionalValue(
            values,
            availableColumns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : null
        );
        AddOptionalValue(
            values,
            availableColumns,
            "is_deleted",
            "@isDeleted",
            DbType.Boolean,
            fine.is_deleted
        );

        await ExecuteUpdateAsync(fine.Fine_code, values);
        fine.date_updated = now;
        fine.modified_by_user_code = currentUserId > 0 ? currentUserId : null;
        return fine;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The legacy DELETE statement is fixed and the fine code is parameterized."
    )]
    public async Task DeleteAsync(int fineCode, int currentUserId)
    {
        await EnsureLegacyFineTriggersAsync();
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
            // MNT_findel_delete.aspx and MNT_findelj_delete.aspx both remove
            // the Fines row. The restored client database has the legacy
            // TRG_Audit_Fines_Delete trigger enabled; an expanded audit column
            // must not change the user action into a soft delete.
            command.CommandText = "DELETE FROM [dbo].[Fines] WHERE [Fine_code] = @fineCode";

            AddParameter(command, "@fineCode", DbType.Int32, fineCode);
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
        Justification = "The INSERT statement is composed only from the fixed allowlisted column/value pairs and every value is parameterized."
    )]
    private async Task<int> ExecuteInsertAsync(IReadOnlyList<WriteValue> values)
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
            command.CommandText =
                $"INSERT INTO [dbo].[Fines] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) VALUES ({string.Join(", ", values.Select(value => value.Parameter))}); SELECT CAST(SCOPE_IDENTITY() AS int);";
            AddParameters(command, values);
            return Convert.ToInt32(await command.ExecuteScalarAsync());
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
        Justification = "The UPDATE statement is composed only from the fixed allowlisted column/value pairs and every value is parameterized."
    )]
    private async Task ExecuteUpdateAsync(int fineCode, IReadOnlyList<WriteValue> values)
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
            command.CommandText =
                $"UPDATE [dbo].[Fines] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [Fine_code] = @fineCode";
            AddParameters(command, values);
            AddParameter(command, "@fineCode", DbType.Int32, fineCode);
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

    private async Task<HashSet<string>> GetAvailableColumnsAsync(string tableName = "Fines")
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

    private async Task EnsureLegacyFineTriggersAsync()
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
                SELECT COUNT(1)
                FROM [sys].[triggers] AS [tr]
                INNER JOIN [sys].[tables] AS [tb]
                    ON [tb].[object_id] = [tr].[parent_id]
                INNER JOIN [sys].[schemas] AS [sc]
                    ON [sc].[schema_id] = [tb].[schema_id]
                WHERE [sc].[name] = @schemaName
                  AND [tb].[name] = @tableName
                  AND [tr].[name] IN (@insertTrigger, @updateTrigger, @deleteTrigger)
                  AND [tr].[is_disabled] = 0;
                """;
            AddParameter(command, "@schemaName", DbType.String, "dbo");
            AddParameter(command, "@tableName", DbType.String, "Fines");
            AddParameter(command, "@insertTrigger", DbType.String, "TRG_Audit_Fines_Insert");
            AddParameter(command, "@updateTrigger", DbType.String, "TRG_Audit_Fines_Update");
            AddParameter(command, "@deleteTrigger", DbType.String, "TRG_Audit_Fines_Delete");

            if (Convert.ToInt32(await command.ExecuteScalarAsync()) != 3)
            {
                throw new LegacyFineWorkflowUnavailableException(
                    "The legacy Fines audit-trigger chain is unavailable or disabled; the fine mutation was not applied."
                );
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static string BuildPageProjection(IReadOnlySet<string> availableColumns)
    {
        return string.Join(
            ", ",
            CommonColumns
                .Concat(OptionalColumns.Where(availableColumns.Contains))
                .Select(column => $"[f].[{column}] AS [{column}]")
        );
    }

    private static void AddSearchParameter(DbCommand command, string searchQuery, bool include)
    {
        if (include)
        {
            AddParameter(command, "@searchQuery", DbType.String, searchQuery.ToLowerInvariant());
        }
    }

    private static void AddAllowedSiteParameters(
        DbCommand command,
        IReadOnlyList<short>? allowedSiteCodes
    )
    {
        if (allowedSiteCodes is null)
        {
            return;
        }

        for (var index = 0; index < allowedSiteCodes.Count; index++)
        {
            AddParameter(command, $"@allowedSite{index}", DbType.Int16, allowedSiteCodes[index]);
        }
    }

    private static string BuildSelectCommandText(
        IReadOnlySet<string> availableColumns,
        string? predicate
    )
    {
        var columns = CommonColumns
            .Concat(OptionalColumns.Where(availableColumns.Contains))
            .Select(column => $"[{column}]");
        return $"SELECT {string.Join(", ", columns)} FROM [dbo].[Fines] {predicate}";
    }

    private static Fine MapFine(DbDataReader reader, IReadOnlySet<string> availableColumns)
    {
        var offenceDate = ReadDateTime(reader, "Offence_date");
        var receiveDate = ReadDateTime(reader, "Receive_gg_date");
        var createdDate =
            ReadDateTimeIfAvailable(reader, availableColumns, "date_created")
            ?? offenceDate
            ?? receiveDate
            ?? DateTime.MinValue;

        return new Fine
        {
            Fine_code = ReadInt32(reader, "Fine_code") ?? 0,
            vmf_code = ReadInt32(reader, "vmf_code"),
            Offence_date = offenceDate,
            Offence_reference = ReadString(reader, "Offence_reference"),
            Offence_issuer = ReadString(reader, "Offence_issuer"),
            Fine_amount = ReadDecimal(reader, "Fine_amount"),
            Appear_date = ReadDateTime(reader, "Appear_date"),
            Receive_gg_date = receiveDate,
            Notify_dept_date = ReadDateTime(reader, "Notify_dept_date"),
            Site_code = ReadInt16(reader, "Site_code"),
            Offence_name = ReadString(reader, "Offence_name"),
            Fine_pay_date = ReadDateTime(reader, "Fine_pay_date"),
            Withdraw_date = ReadDateTime(reader, "Withdraw_date"),
            Pay_due_date = ReadDateTime(reader, "Pay_due_date"),
            Issuer_notify_date = ReadDateTime(reader, "Issuer_notify_date"),
            Dept_person_name = ReadStringIfAvailable(reader, availableColumns, "Dept_person_name"),
            Dept_person_id = ReadStringIfAvailable(reader, availableColumns, "Dept_person_id"),
            Document_type = ReadStringIfAvailable(reader, availableColumns, "Document_type"),
            Traffic_dept_code = ReadInt16IfAvailable(reader, availableColumns, "Traffic_dept_code"),
            date_created = createdDate,
            date_updated = ReadDateTimeIfAvailable(reader, availableColumns, "date_updated"),
            created_by_user_code = ReadInt32IfAvailable(
                reader,
                availableColumns,
                "created_by_user_code"
            ),
            modified_by_user_code = ReadInt32IfAvailable(
                reader,
                availableColumns,
                "modified_by_user_code"
            ),
            is_deleted = ReadBooleanIfAvailable(reader, availableColumns, "is_deleted") ?? false,
        };
    }

    private static List<WriteValue> BuildCommonWriteValues(Fine fine)
    {
        return
        [
            new("vmf_code", "@vmfCode", DbType.Int32, fine.vmf_code),
            new("Offence_date", "@offenceDate", DbType.DateTime2, fine.Offence_date),
            new("Offence_reference", "@offenceReference", DbType.String, fine.Offence_reference),
            new("Offence_issuer", "@offenceIssuer", DbType.String, fine.Offence_issuer),
            new("Fine_amount", "@fineAmount", DbType.Decimal, fine.Fine_amount),
            new("Appear_date", "@appearDate", DbType.DateTime2, fine.Appear_date),
            new("Receive_gg_date", "@receiveGgDate", DbType.DateTime2, fine.Receive_gg_date),
            new("Notify_dept_date", "@notifyDeptDate", DbType.DateTime2, fine.Notify_dept_date),
            new("Site_code", "@siteCode", DbType.Int16, fine.Site_code),
            new("Offence_name", "@offenceName", DbType.String, fine.Offence_name),
            new("Fine_pay_date", "@finePayDate", DbType.DateTime2, fine.Fine_pay_date),
            new("Withdraw_date", "@withdrawDate", DbType.DateTime2, fine.Withdraw_date),
            new("Pay_due_date", "@payDueDate", DbType.DateTime2, fine.Pay_due_date),
            new(
                "Issuer_notify_date",
                "@issuerNotifyDate",
                DbType.DateTime2,
                fine.Issuer_notify_date
            ),
        ];
    }

    private static void AddOptionalValue(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
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

    private static string? ReadString(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal).TrimEnd();
    }

    private static string? ReadStringIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    )
    {
        return columns.Contains(column) ? ReadString(reader, column) : null;
    }

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static int? ReadInt32IfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    )
    {
        return columns.Contains(column) ? ReadInt32(reader, column) : null;
    }

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
    }

    private static short? ReadInt16IfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    )
    {
        return columns.Contains(column) ? ReadInt16(reader, column) : null;
    }

    private static decimal? ReadDecimal(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDecimal(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTime(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static DateTime? ReadDateTimeIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    )
    {
        return columns.Contains(column) ? ReadDateTime(reader, column) : null;
    }

    private static bool? ReadBooleanIfAvailable(
        DbDataReader reader,
        IReadOnlySet<string> columns,
        string column
    )
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

public sealed class LegacyFineWorkflowUnavailableException : InvalidOperationException
{
    public LegacyFineWorkflowUnavailableException(string message)
        : base(message) { }
}
