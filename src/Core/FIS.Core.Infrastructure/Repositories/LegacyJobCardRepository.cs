using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities;
using FIS.Core.Domain.Entities.Operations;
using FIS.Core.Domain.Entities.ReferenceData;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Compatibility implementation for the original dbo.Jobcards table.
///
/// The client database predates the expanded job_cards shape. Its identity is
/// jc_code and its workflow fields use the original names (captured_by,
/// hhandover_name, reviewed_by_Authorizer, and DateClosed). The API exposes the
/// modern contract while this repository keeps those legacy fields usable.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility projections and all submitted values are parameters.")]
internal sealed class LegacyJobCardRepository : IJobCardRepository
{
    private const string TableName = "Jobcards";

    private readonly FisDbContext _context;

    public LegacyJobCardRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<JobCard?> GetByIdAsync(int jobCardId)
        => (await QueryAsync(
            command => AddParameter(command, "@jobCardId", DbType.Int32, jobCardId),
            columns => $"j.[{GetIdColumn(columns)}] = @jobCardId"))
            .SingleOrDefault();

    public async Task<JobCard?> GetByVehicleAndExtraAsync(int vmfCode, short extraCode)
        => (await QueryAsync(
            command =>
            {
                AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
                AddParameter(command, "@extraCode", DbType.Int16, extraCode);
            },
            _ => "j.[vmf_code] = @vmfCode AND j.[extra_code] = @extraCode"))
            .SingleOrDefault();

    public async Task<IEnumerable<JobCard>> GetAllAsync()
        => await QueryAsync();

    public async Task<IEnumerable<JobCard>> GetByGGNumberAsync(string ggNumber)
        => await QueryAsync(
            command => AddParameter(command, "@ggNumber", DbType.String, ggNumber.Trim()),
            _ => "(vm.[fleet_number] = @ggNumber OR vm.[registration_number] = @ggNumber)");

    public async Task<IEnumerable<JobCard>> GetPriorityUnassignedAsync()
        => await QueryAsync(
            null,
            columns => columns.ContainsKey("priority") && columns.ContainsKey("reviewed_by_Authorizer")
                ? "j.[priority] = 'Y' AND j.[reviewed_by_Authorizer] = 'Y' AND j.[status_code] IN (1, 2)"
                : "j.[priority] = 'H' AND j.[assigned_to] IS NULL AND j.[status_code] NOT IN (5, 7)");

    public async Task<IEnumerable<JobCard>> GetAssignedPriorityAsync()
        => await QueryAsync(
            null,
            columns => columns.ContainsKey("reviewed_by_Authorizer") && columns.ContainsKey("jc_number")
                ? "j.[priority] = 'Y' AND j.[status_code] = 3 AND j.[jc_number] <> 'Not Assigned'"
                : "j.[priority] = 'H' AND j.[assigned_to] IS NOT NULL AND j.[status_code] NOT IN (5, 7)");

    public async Task<IEnumerable<JobCard>> GetByStatusAsync(int statusCode)
        => await QueryAsync(
            command => AddParameter(command, "@statusCode", DbType.Int32, statusCode),
            _ => "j.[status_code] = @statusCode");

    public async Task<IEnumerable<JobCard>> GetByAuthorizerAsync(int authorizerUserId)
        => await QueryAsync(
            command => AddParameter(command, "@authorizer", DbType.Int32, authorizerUserId),
            _ => "j.[authorizer] = @authorizer");

    public async Task<JobCard> CreateAsync(JobCard jobCard, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(jobCard);
        var columns = await GetAvailableColumnsAsync();

        if (columns.ContainsKey("job_card_id"))
        {
            var values = new List<WriteValue>();
            AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, jobCard.vmf_code, true);
            AddValue(values, columns, "extra_code", "@extraCode", DbType.Int16, jobCard.extra_code, true);
            AddValue(values, columns, "status_code", "@statusCode", DbType.Int32, jobCard.status_code == 0 ? 1 : jobCard.status_code, true);
            AddValue(values, columns, "priority", "@priority", DbType.String, jobCard.priority, true);
            AddValue(values, columns, "jcs_comment", "@jcsComment", DbType.String, jobCard.jcs_comment, true);
            AddValue(values, columns, "damages", "@damages", DbType.String, jobCard.damages, true);
            AddValue(values, columns, "reviewed", "@reviewed", DbType.String, jobCard.reviewed ?? "N", true);
            AddValue(values, columns, "date_created", "@dateCreated", DbType.DateTime2, DateTime.UtcNow, true);
            AddValue(values, columns, "created_by_user_code", "@createdBy", DbType.Int32, UserIdOrNull(currentUserId), true);
            AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false, true);

            await using var scope = await OpenConnectionAsync();
            await using var command = scope.Connection.CreateCommand();
            command.Transaction = CurrentTransaction;
            command.CommandText = $"INSERT INTO [dbo].[{TableName}] ({string.Join(", ", values.Select(x => $"[{x.Column}]"))}) OUTPUT INSERTED.[job_card_id] VALUES ({string.Join(", ", values.Select(x => x.Parameter))})";
            AddParameters(command, values);
            var id = Convert.ToInt32(await command.ExecuteScalarAsync());
            return await GetByIdAsync(id) ?? throw new InvalidOperationException("Created job card could not be read.");
        }

        var legacyId = await CreateLegacyAsync(jobCard, currentUserId);
        return await GetByIdAsync(legacyId) ?? throw new InvalidOperationException("Created legacy job card could not be read.");
    }

    public async Task<JobCard> UpdateAsync(JobCard jobCard, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(jobCard);
        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();

        AddValue(values, columns, "jcs_comment", "@jcsComment", DbType.String, jobCard.jcs_comment, true);
        AddValue(values, columns, "damages", "@damages", DbType.String, jobCard.damages, true);
        AddValue(values, columns, "comments", "@comments", DbType.String, jobCard.comments, true);
        AddValue(values, columns, "priority", "@priority", DbType.String, NormalizePriority(jobCard.priority, columns), true);
        AddValue(values, columns, "assigned_to", "@assignedTo", DbType.Int32, jobCard.assigned_to, false);
        AddValue(values, columns, "assigned_date", "@assignedDate", DbType.DateTime2, jobCard.assigned_date, false);
        AddValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, DateTime.UtcNow, true);
        AddValue(values, columns, "modified_by_user_code", "@modifiedBy", DbType.Int32, UserIdOrNull(currentUserId), true);

        if (columns.ContainsKey("reviewed_by_Authorizer"))
        {
            AddValue(values, columns, "hhandover_name", "@handoverName", DbType.String, jobCard.assigned_to?.ToString(), false);
            AddValue(values, columns, "hhandover_date", "@handoverDate", DbType.String, jobCard.assigned_date?.ToString("yyyy/MM/dd"), false);
            AddValue(values, columns, "authorizer_jobcard_comments", "@authorizerComments", DbType.String, jobCard.comments, false);
        }

        await ExecuteUpdateAsync(jobCard.job_card_id, columns, values);
        return await GetByIdAsync(jobCard.job_card_id) ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCard.job_card_id}");
    }

    public async Task DeleteAsync(int jobCardId, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;

        if (columns.ContainsKey("is_deleted"))
        {
            command.CommandText = $"UPDATE [dbo].[{TableName}] SET [is_deleted] = 1, [date_updated] = @dateUpdated, [modified_by_user_code] = @modifiedBy WHERE [job_card_id] = @jobCardId";
            AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
            AddParameter(command, "@modifiedBy", DbType.Int32, UserIdOrNull(currentUserId));
        }
        else
        {
            command.CommandText = $"DELETE FROM [dbo].[{TableName}] WHERE [jc_code] = @jobCardId";
        }

        AddParameter(command, "@jobCardId", DbType.Int32, jobCardId);
        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");
        }
    }

    public Task<JobCard> AuthorizeAsync(int jobCardId, int authorizerUserId, string? comment)
        => UpdateWorkflowAsync(jobCardId, authorizerUserId, 3, "Y", comment, null);

    public Task<JobCard> DeclineAsync(int jobCardId, int authorizerUserId, string declineReason)
        => UpdateWorkflowAsync(jobCardId, authorizerUserId, 1, "Y", $"Declined: {declineReason}", null);

    public Task<JobCard> CancelAsync(int jobCardId, int currentUserId, string? cancelReason)
        => UpdateWorkflowAsync(jobCardId, currentUserId, 7, null, cancelReason is null ? null : $"Canceled: {cancelReason}", currentUserId);

    public Task<JobCard> CloseAsync(
        int jobCardId,
        int currentUserId,
        string? closeNotes,
        decimal? labourCost = null,
        decimal? partsCost = null,
        decimal? otherCost = null,
        string? invoiceNumber = null,
        DateTime? invoiceDate = null,
        string? serviceProvider = null)
        => UpdateWorkflowAsync(jobCardId, currentUserId, 5, null, closeNotes is null ? null : $"Closed: {closeNotes}", null,
            labourCost, partsCost, otherCost, invoiceNumber, invoiceDate, serviceProvider);

    public Task<JobCard> UpdateStatusAsync(int jobCardId, int newStatusCode, int currentUserId)
        => UpdateWorkflowAsync(jobCardId, currentUserId, newStatusCode, null, null, null);

    public async Task<JobCard> UpdateCostsAsync(
        int jobCardId,
        int currentUserId,
        decimal? labourCost,
        decimal? partsCost,
        decimal? otherCost,
        string? invoiceNumber,
        DateTime? invoiceDate,
        string? serviceProvider)
    {
        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();
        AddValue(values, columns, "labour_cost", "@labourCost", DbType.Decimal, labourCost, false);
        AddValue(values, columns, "parts_cost", "@partsCost", DbType.Decimal, partsCost, false);
        AddValue(values, columns, "other_cost", "@otherCost", DbType.Decimal, otherCost, false);
        AddValue(values, columns, "invoice_number", "@invoiceNumber", DbType.String, invoiceNumber, false);
        AddValue(values, columns, "invoice_date", "@invoiceDate", DbType.DateTime2, invoiceDate, false);
        AddValue(values, columns, "service_provider", "@serviceProvider", DbType.String, serviceProvider, false);
        AddValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, DateTime.UtcNow, false);
        AddValue(values, columns, "modified_by_user_code", "@modifiedBy", DbType.Int32, UserIdOrNull(currentUserId), false);

        if (values.Count > 0)
        {
            await ExecuteUpdateAsync(jobCardId, columns, values);
        }

        // The original table has no repair-cost columns. Returning the
        // existing record keeps the workflow available without pretending
        // that expanded values were persisted in a legacy field.
        return await GetByIdAsync(jobCardId) ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");
    }

    private async Task<JobCard> UpdateWorkflowAsync(
        int jobCardId,
        int currentUserId,
        int statusCode,
        string? reviewed,
        string? comment,
        int? capturedBy,
        decimal? labourCost = null,
        decimal? partsCost = null,
        decimal? otherCost = null,
        string? invoiceNumber = null,
        DateTime? invoiceDate = null,
        string? serviceProvider = null)
    {
        var existing = await GetByIdAsync(jobCardId)
            ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");
        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();

        AddValue(values, columns, "status_code", "@statusCode", DbType.Int32, statusCode, true);
        AddValue(values, columns, "authorizer", "@authorizer", DbType.Int32, statusCode is 3 or 1 ? currentUserId : existing.authorizer, false);
        AddValue(values, columns, "reviewed", "@reviewed", DbType.String, reviewed, false);
        AddValue(values, columns, "date_updated", "@dateUpdated", DbType.DateTime2, DateTime.UtcNow, false);
        AddValue(values, columns, "modified_by_user_code", "@modifiedBy", DbType.Int32, UserIdOrNull(currentUserId), false);
        AddValue(values, columns, "comments", "@comments", DbType.String, Append(existing.comments, comment), comment is not null);
        AddValue(values, columns, "labour_cost", "@labourCost", DbType.Decimal, labourCost, false);
        AddValue(values, columns, "parts_cost", "@partsCost", DbType.Decimal, partsCost, false);
        AddValue(values, columns, "other_cost", "@otherCost", DbType.Decimal, otherCost, false);
        AddValue(values, columns, "invoice_number", "@invoiceNumber", DbType.String, invoiceNumber, false);
        AddValue(values, columns, "invoice_date", "@invoiceDate", DbType.DateTime2, invoiceDate, false);
        AddValue(values, columns, "service_provider", "@serviceProvider", DbType.String, serviceProvider, false);

        if (columns.ContainsKey("total_cost") && (labourCost.HasValue || partsCost.HasValue || otherCost.HasValue))
        {
            var total = (existing.labour_cost ?? 0m) + (existing.parts_cost ?? 0m) + (existing.other_cost ?? 0m);
            total += labourCost.HasValue ? labourCost.Value - (existing.labour_cost ?? 0m) : 0m;
            total += partsCost.HasValue ? partsCost.Value - (existing.parts_cost ?? 0m) : 0m;
            total += otherCost.HasValue ? otherCost.Value - (existing.other_cost ?? 0m) : 0m;
            AddValue(values, columns, "total_cost", "@totalCost", DbType.Decimal, total, true);
        }

        if (capturedBy.HasValue)
        {
            AddValue(values, columns, "created_by_user_code", "@capturedBy", DbType.Int32, capturedBy.Value, false);
        }

        if (columns.ContainsKey("reviewed_by_Authorizer"))
        {
            AddValue(values, columns, "Authorizer", "@legacyAuthorizer", DbType.Int32, statusCode is 3 or 1 ? currentUserId : existing.authorizer, false);
            AddValue(values, columns, "reviewed_by_Authorizer", "@legacyReviewed", DbType.String, reviewed, false);
            AddValue(values, columns, "authorizer_update_date", "@legacyAuthorizerDate", DbType.DateTime, DateTime.Now, false);
            AddValue(values, columns, "authorizer_jobcard_comments", "@legacyComment", DbType.String, comment, comment is not null);
            AddValue(values, columns, "DateClosed", "@dateClosed", DbType.DateTime, statusCode == 5 ? DateTime.Now : null, statusCode == 5);
            AddValue(values, columns, "captured_by", "@legacyCapturedBy", DbType.Int32, capturedBy, false);
            if (comment is not null && (statusCode is 5 or 7))
            {
                AddValue(values, columns, "jcs_comment", "@legacyWorkflowComment", DbType.String, Append(existing.jcs_comment, comment), true);
            }
        }

        await ExecuteUpdateAsync(jobCardId, columns, values);
        return await GetByIdAsync(jobCardId) ?? throw new InvalidOperationException("Updated job card could not be read.");
    }

    private async Task<int> CreateLegacyAsync(JobCard jobCard, int currentUserId)
    {
        var vehicleNumber = await FindVehicleNumberAsync(jobCard);
        if (string.IsNullOrWhiteSpace(vehicleNumber))
        {
            return await InsertLegacyDirectAsync(jobCard, currentUserId);
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "[dbo].[DEV_INS_NewJobCards]";
        AddParameter(command, "@GGNumber", DbType.String, vehicleNumber);
        AddParameter(command, "@extraCode", DbType.Int32, jobCard.extra_code);
        AddParameter(command, "@CaptureBy", DbType.Int32, currentUserId);

        try
        {
            await command.ExecuteNonQueryAsync();
        }
        catch (DbException ex) when (IsMissingProcedure(ex))
        {
            // Some client databases retained Jobcards but not the original
            // helper procedure. The parameterized insert below is the safe
            // equivalent for that shape.
            await scope.Connection.CloseAsync();
            return await InsertLegacyDirectAsync(jobCard, currentUserId);
        }

        var createdId = await FindLatestLegacyIdAsync(scope.Connection, jobCard.vmf_code, jobCard.extra_code, currentUserId);
        if (createdId > 0)
        {
            var columns = await GetAvailableColumnsAsync();
            var values = new List<WriteValue>();
            AddValue(values, columns, "jcs_comment", "@jcsComment", DbType.String, jobCard.jcs_comment, false);
            AddValue(values, columns, "priority", "@priority", DbType.String, NormalizePriority(jobCard.priority, columns), false);
            if (values.Count > 0)
            {
                await ExecuteUpdateAsync(createdId, columns, values);
            }
            return createdId;
        }

        await scope.Connection.CloseAsync();
        return await InsertLegacyDirectAsync(jobCard, currentUserId);
    }

    private async Task<int> InsertLegacyDirectAsync(JobCard jobCard, int currentUserId)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = $"""
            INSERT INTO [dbo].[{TableName}]
                ([jc_counter], [year], [jc_number], [vmf_code], [extra_code], [jcs_comment], [jcs_date], [captured_by], [status_code], [priority], [reviewed_by_Authorizer])
            OUTPUT INSERTED.[jc_code]
            VALUES
                ((SELECT ISNULL(MAX([jc_counter]), 0) + 1 FROM [dbo].[{TableName}]), CONVERT(nchar(10), YEAR(GETDATE())), 'Not Assigned', @vmfCode, @extraCode, @jcsComment, CONVERT(varchar(10), GETDATE(), 111), @capturedBy, 1, @priority, 'N')
            """;
        AddParameter(command, "@vmfCode", DbType.Int32, jobCard.vmf_code);
        AddParameter(command, "@extraCode", DbType.Int32, jobCard.extra_code);
        AddParameter(command, "@jcsComment", DbType.String, jobCard.jcs_comment);
        AddParameter(command, "@capturedBy", DbType.Int32, currentUserId);
        AddParameter(command, "@priority", DbType.String, NormalizePriority(jobCard.priority, new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase) { ["reviewed_by_Authorizer"] = new("reviewed_by_Authorizer") }));
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> FindLatestLegacyIdAsync(DbConnection connection, int vmfCode, short extraCode, int currentUserId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = $"SELECT TOP (1) [jc_code] FROM [dbo].[{TableName}] WHERE [vmf_code] = @vmfCode AND [extra_code] = @extraCode AND [captured_by] = @capturedBy ORDER BY [jc_code] DESC";
        AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
        AddParameter(command, "@extraCode", DbType.Int32, extraCode);
        AddParameter(command, "@capturedBy", DbType.Int32, currentUserId);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    private async Task<string?> FindVehicleNumberAsync(JobCard jobCard)
    {
        if (!string.IsNullOrWhiteSpace(jobCard.Vehicle?.fleet_number))
            return jobCard.Vehicle.fleet_number.Trim();

        if (!string.IsNullOrWhiteSpace(jobCard.Vehicle?.registration_number))
            return jobCard.Vehicle.registration_number.Trim();

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = "SELECT TOP (1) COALESCE(NULLIF([fleet_number], ''), [registration_number]) FROM [dbo].[vehicle_master] WHERE [vmf_code] = @vmfCode";
        AddParameter(command, "@vmfCode", DbType.Int32, jobCard.vmf_code);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value)?.Trim();
    }

    private static bool IsMissingProcedure(DbException exception)
        => exception.Message.Contains("could not find stored procedure", StringComparison.OrdinalIgnoreCase)
            || exception.Message.Contains("does not exist", StringComparison.OrdinalIgnoreCase);

    private async Task<List<JobCard>> QueryAsync(Action<DbCommand>? configure = null, Func<IReadOnlyDictionary<string, ColumnInfo>, string?>? predicateFactory = null)
    {
        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        var predicate = predicateFactory?.Invoke(columns);
        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(predicate)) conditions.Add($"({predicate})");
        if (columns.ContainsKey("is_deleted")) conditions.Add("ISNULL(j.[is_deleted], 0) = 0");

        command.CommandText = $"""
            SELECT
                {BuildProjection(columns)}
            FROM [dbo].[{TableName}] j
            LEFT JOIN [dbo].[vehicle_master] vm ON vm.[vmf_code] = j.[vmf_code]
            LEFT JOIN [dbo].[extra_codes] ec ON ec.[extra_code] = j.[extra_code]
            WHERE {string.Join(" AND ", conditions.DefaultIfEmpty("1 = 1"))}
            ORDER BY {DateExpression(columns)} DESC, {IdExpression(columns)} DESC
            """;
        configure?.Invoke(command);

        var results = new List<JobCard>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) results.Add(Map(reader));
        return results;
    }

    private async Task ExecuteUpdateAsync(int jobCardId, IReadOnlyDictionary<string, ColumnInfo> columns, IReadOnlyCollection<WriteValue> values)
    {
        if (values.Count == 0) return;
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        var idColumn = GetIdColumn(columns);
        command.CommandText = $"UPDATE [dbo].[{TableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [{idColumn}] = @jobCardId";
        AddParameters(command, values);
        AddParameter(command, "@jobCardId", DbType.Int32, jobCardId);
        if (await command.ExecuteNonQueryAsync() == 0) throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");
    }

    private async Task<Dictionary<string, ColumnInfo>> GetAvailableColumnsAsync()
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = "SELECT [COLUMN_NAME], [DATA_TYPE] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table";
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, TableName);

        var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) columns[reader.GetString(0)] = new ColumnInfo(reader.GetString(0), reader.GetString(1));
        if (!columns.ContainsKey("vmf_code") || !columns.ContainsKey("extra_code") || !columns.ContainsKey("status_code"))
            throw new InvalidOperationException("The Jobcards compatibility table is missing required workflow columns.");
        return columns;
    }

    private static string BuildProjection(IReadOnlyDictionary<string, ColumnInfo> columns)
        => string.Join(",\n                ",
        [
            $"{IdExpression(columns)} AS [job_card_id]",
            "j.[vmf_code] AS [vmf_code]",
            "vm.[fleet_number] AS [gg_number]",
            "vm.[registration_number] AS [registration_number]",
            "j.[extra_code] AS [extra_code]",
            "ec.[extra_description] AS [extra_description]",
            "j.[status_code] AS [status_code]",
            PriorityExpression(columns) + " AS [priority]",
            AssignedToExpression(columns) + " AS [assigned_to]",
            AssignedNameExpression(columns) + " AS [assigned_to_name]",
            AssignedDateExpression(columns) + " AS [assigned_date]",
            CommentExpression(columns, "jcs_comment") + " AS [jcs_comment]",
            OptionalExpression(columns, "damages", "varchar(2000)") + " AS [damages]",
            CommentExpression(columns, "comments") + " AS [comments]",
            OptionalExpression(columns, "authorizer", "int") + " AS [authorizer]",
            AuthorizerNameExpression(columns) + " AS [authorizer_name]",
            ReviewedExpression(columns) + " AS [reviewed]",
            CapturedByExpression(columns) + " AS [captured_by_user_code]",
            OptionalExpression(columns, "labour_cost", "decimal(18, 2)") + " AS [labour_cost]",
            OptionalExpression(columns, "parts_cost", "decimal(18, 2)") + " AS [parts_cost]",
            OptionalExpression(columns, "other_cost", "decimal(18, 2)") + " AS [other_cost]",
            OptionalExpression(columns, "total_cost", "decimal(18, 2)") + " AS [total_cost]",
            OptionalExpression(columns, "invoice_number", "varchar(200)") + " AS [invoice_number]",
            OptionalExpression(columns, "invoice_date", "datetime2") + " AS [invoice_date]",
            OptionalExpression(columns, "service_provider", "varchar(200)") + " AS [service_provider]",
            DateExpression(columns) + " AS [date_created]",
            UpdatedDateExpression(columns) + " AS [date_updated]",
            CapturedByExpression(columns) + " AS [created_by_user_code]",
            ModifiedByExpression(columns) + " AS [modified_by_user_code]"
        ]);

    private static string IdExpression(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("job_card_id") ? "j.[job_card_id]" : "j.[jc_code]";

    private static string DateExpression(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("date_created") ? "j.[date_created]" : "TRY_CONVERT(datetime2, NULLIF(j.[jcs_date], ''), 111)";

    private static string UpdatedDateExpression(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("date_updated")
            ? "j.[date_updated]"
            : columns.ContainsKey("DateClosed") && columns.ContainsKey("authorizer_update_date")
                ? "COALESCE(j.[DateClosed], j.[authorizer_update_date])"
                : OptionalExpression(columns, "DateClosed", "datetime2");

    private static string PriorityExpression(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("priority") ? "j.[priority]" : "CAST(NULL AS varchar(1))";

    private static string AssignedToExpression(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("assigned_to") ? "j.[assigned_to]" : "CAST(NULL AS int)";

    private static string AssignedNameExpression(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("hhandover_name")
            ? "j.[hhandover_name]"
            : columns.ContainsKey("assigned_to") ? "CONVERT(varchar(50), j.[assigned_to])" : "CAST(NULL AS varchar(50))";

    private static string AssignedDateExpression(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("assigned_date")
            ? "j.[assigned_date]"
            : "TRY_CONVERT(datetime2, NULLIF(j.[hhandover_date], ''), 111)";

    private static string CommentExpression(IReadOnlyDictionary<string, ColumnInfo> columns, string modernColumn)
        => columns.ContainsKey(modernColumn)
            ? $"j.[{modernColumn}]"
            : modernColumn.Equals("comments", StringComparison.OrdinalIgnoreCase) && columns.ContainsKey("authorizer_jobcard_comments")
                ? "j.[authorizer_jobcard_comments]"
                : "CAST(NULL AS varchar(2000))";

    private static string ReviewedExpression(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("reviewed") ? "j.[reviewed]" : OptionalExpression(columns, "reviewed_by_Authorizer", "varchar(1)");

    private static string CapturedByExpression(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("created_by_user_code") ? "j.[created_by_user_code]" : OptionalExpression(columns, "captured_by", "int");

    private static string ModifiedByExpression(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("modified_by_user_code") ? "j.[modified_by_user_code]" : OptionalExpression(columns, "Authorizer", "int");

    private static string AuthorizerNameExpression(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("authorizer") ? "CONVERT(varchar(50), j.[authorizer])" : "CAST(NULL AS varchar(50))";

    private static string OptionalExpression(IReadOnlyDictionary<string, ColumnInfo> columns, string column, string sqlType)
        => columns.ContainsKey(column) ? $"j.[{column}]" : $"CAST(NULL AS {sqlType})";

    private static JobCard Map(DbDataReader reader)
    {
        var assignedTo = ReadInt(reader, "assigned_to");
        var assignedName = ReadString(reader, "assigned_to_name");
        var authorizer = ReadInt(reader, "authorizer");
        var authorizerName = ReadString(reader, "authorizer_name");
        var capturedBy = ReadInt(reader, "captured_by_user_code");

        return new JobCard
        {
            job_card_id = ReadInt(reader, "job_card_id") ?? 0,
            vmf_code = ReadInt(reader, "vmf_code") ?? 0,
            extra_code = (short)(ReadInt(reader, "extra_code") ?? 0),
            status_code = ReadInt(reader, "status_code") ?? 0,
            priority = ReadString(reader, "priority"),
            assigned_to = assignedTo,
            assigned_date = ReadDate(reader, "assigned_date"),
            jcs_comment = ReadString(reader, "jcs_comment"),
            damages = ReadString(reader, "damages"),
            comments = ReadString(reader, "comments"),
            authorizer = authorizer,
            reviewed = ReadString(reader, "reviewed"),
            labour_cost = ReadDecimal(reader, "labour_cost"),
            parts_cost = ReadDecimal(reader, "parts_cost"),
            other_cost = ReadDecimal(reader, "other_cost"),
            total_cost = ReadDecimal(reader, "total_cost"),
            invoice_number = ReadString(reader, "invoice_number"),
            invoice_date = ReadDate(reader, "invoice_date"),
            service_provider = ReadString(reader, "service_provider"),
            date_created = ReadDate(reader, "date_created") ?? default,
            date_updated = ReadDate(reader, "date_updated"),
            created_by_user_code = capturedBy,
            modified_by_user_code = ReadInt(reader, "modified_by_user_code"),
            Vehicle = new Vehicle
            {
                vmf_code = ReadInt(reader, "vmf_code") ?? 0,
                fleet_number = ReadString(reader, "gg_number"),
                registration_number = ReadString(reader, "registration_number")
            },
            ExtraCodeRef = new ExtraCode
            {
                extra_code = (short)(ReadInt(reader, "extra_code") ?? 0),
                extra_description = ReadString(reader, "extra_description")
            },
            AssignedToUser = assignedTo.HasValue || !string.IsNullOrWhiteSpace(assignedName)
                ? new User { user_access_code = assignedTo ?? 0, email = assignedName }
                : null,
            AuthorizerUser = authorizer.HasValue || !string.IsNullOrWhiteSpace(authorizerName)
                ? new User { user_access_code = authorizer ?? 0, email = authorizerName }
                : null
        };
    }

    private async Task<ConnectionScope> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync();
        return new ConnectionScope(connection, shouldClose);
    }

    private DbTransaction? CurrentTransaction => _context.Database.CurrentTransaction?.GetDbTransaction();

    private static string? NormalizePriority(string? priority, IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("reviewed_by_Authorizer")
            ? priority switch { "H" => "Y", "N" => "N", _ => priority }
            : priority;

    private static string? Append(string? current, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return current;
        var combined = string.IsNullOrWhiteSpace(current) ? value.Trim() : $"{current.Trim()}\n{value.Trim()}";
        return combined.Length > 150 ? combined[..150] : combined;
    }

    private static int? UserIdOrNull(int value) => value > 0 ? value : null;

    private static void AddValue(ICollection<WriteValue> values, IReadOnlyDictionary<string, ColumnInfo> columns, string column, string parameter, DbType type, object? value, bool includeNull)
    {
        if (columns.ContainsKey(column) && (includeNull || value is not null)) values.Add(new WriteValue(column, parameter, type, value));
    }

    private static void AddParameters(DbCommand command, IEnumerable<WriteValue> values)
    {
        foreach (var value in values) AddParameter(command, value.Parameter, value.Type, value.Value);
    }

    private static void AddParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = type;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string? ReadString(DbDataReader reader, string name)
        => reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToString(reader[name]);

    private static int? ReadInt(DbDataReader reader, string name)
        => reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToInt32(reader[name]);

    private static decimal? ReadDecimal(DbDataReader reader, string name)
        => reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToDecimal(reader[name]);

    private static DateTime? ReadDate(DbDataReader reader, string name)
    {
        if (reader.IsDBNull(reader.GetOrdinal(name))) return null;
        return DateTime.TryParse(Convert.ToString(reader[name]), out var value) ? value : null;
    }

    private static string GetIdColumn(IReadOnlyDictionary<string, ColumnInfo> columns)
        => columns.ContainsKey("job_card_id") ? "job_card_id" : "jc_code";

    private sealed record ColumnInfo(string Name, string DataType = "");
    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

    private sealed class ConnectionScope(DbConnection connection, bool shouldClose) : IAsyncDisposable
    {
        public DbConnection Connection { get; } = connection;

        public async ValueTask DisposeAsync()
        {
            if (shouldClose) await Connection.CloseAsync();
        }
    }
}
