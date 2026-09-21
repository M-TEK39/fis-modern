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
/// Compatibility implementation for the client-era dbo.JobCard table.
///
/// The client database predates the expanded job_cards shape. Its identity is
/// JobCard_code and its workflow fields use the original names (captured_by,
/// HandedOverTo, reviewed_by_Authoriser, and DateClosed). The API exposes the
/// modern contract while this repository keeps those legacy fields usable.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers come only from fixed compatibility projections and all submitted values are parameters."
)]
internal sealed class LegacyJobCardRepository : IJobCardRepository
{
    private const string DefaultTableName = "Jobcards";
    private const string VehicleTableName = "vehicle_master";
    private const string ExtraCodeTableName = "extra_codes";

    private readonly FisDbContext _context;
    private string? _resolvedTableName;

    public LegacyJobCardRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<JobCard?> GetByIdAsync(int jobCardId) =>
        (
            await QueryAsync(
                command => AddParameter(command, "@jobCardId", DbType.Int32, jobCardId),
                columns => $"j.[{GetIdColumn(columns)}] = @jobCardId"
            )
        ).SingleOrDefault();

    public async Task<JobCard?> GetByVehicleAndExtraAsync(int vmfCode, short extraCode) =>
        (
            await QueryAsync(
                command =>
                {
                    AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
                    AddParameter(command, "@extraCode", DbType.Int16, extraCode);
                },
                _ => "j.[vmf_code] = @vmfCode AND j.[extra_code] = @extraCode"
            )
        ).SingleOrDefault();

    public async Task<IEnumerable<JobCard>> GetAllAsync() => await QueryAsync();

    public async Task<JobCardPage> GetPageAsync(JobCardPageQuery query)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var searchTerm = query.SearchTerm?.Trim() ?? string.Empty;
        var searchType = string.Equals(query.SearchType, "GP", StringComparison.OrdinalIgnoreCase)
            ? "GP"
            : "GG";
        var statusCodes = query.StatusCodes?.Distinct().ToArray() ?? [];
        var columns = await GetAvailableColumnsAsync();
        var vehicleColumns = await GetAvailableColumnsAsync(VehicleTableName);
        var vehicleProjectionColumns = vehicleColumns.ContainsKey("vmf_code")
            ? vehicleColumns
            : new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
        var conditions = new List<string>();

        if (columns.ContainsKey("is_deleted"))
            conditions.Add("ISNULL(j.[is_deleted], 0) = 0");

        if (statusCodes.Length > 0)
        {
            var statusParameters = string.Join(
                ", ",
                statusCodes.Select((_, index) => $"@statusCode{index}")
            );
            conditions.Add($"j.[status_code] IN ({statusParameters})");
        }

        var searchId = int.TryParse(searchTerm, out var parsedSearchId)
            ? parsedSearchId
            : (int?)null;
        if (query.JobCardId.HasValue)
            conditions.Add($"{IdExpression(columns)} = @jobCardId");

        if (searchTerm.Length > 0)
        {
            var vehicleColumn = searchType == "GP" ? "registration_number" : "fleet_number";
            var vehicleSearch =
                vehicleColumns.ContainsKey("vmf_code") && vehicleColumns.ContainsKey(vehicleColumn)
                    ? $"LOWER(LTRIM(RTRIM(COALESCE(vm.[{vehicleColumn}], '')))) LIKE @searchTerm"
                    : "1 = 0";
            var searchPredicate = searchId.HasValue
                ? $"({vehicleSearch} OR {IdExpression(columns)} = @searchId)"
                : vehicleSearch;
            conditions.Add($"({searchPredicate})");
        }

        AddAllowedVehicleCondition(conditions, query.AllowedVmfCodes, "j");

        var whereClause = string.Join(" AND ", conditions.DefaultIfEmpty("1 = 1"));
        var orderBy = $"{DateExpression(columns)} DESC, {IdExpression(columns)} DESC";
        var vehicleJoin = vehicleColumns.ContainsKey("vmf_code")
            ? $"LEFT JOIN [dbo].[{VehicleTableName}] vm ON vm.[vmf_code] = j.[vmf_code]"
            : string.Empty;
        var extraColumns = await GetAvailableColumnsAsync(ExtraCodeTableName);
        var extraJoin = extraColumns.ContainsKey("extra_code")
            ? $"LEFT JOIN [dbo].[{ExtraCodeTableName}] ec ON ec.[extra_code] = j.[extra_code]"
            : string.Empty;
        await using var scope = await OpenConnectionAsync();

        int totalRecords;
        await using (var countCommand = scope.Connection.CreateCommand())
        {
            countCommand.Transaction = CurrentTransaction;
            countCommand.CommandText = $"""
                SELECT COUNT(DISTINCT {IdExpression(columns)})
                FROM [dbo].[{CurrentTableName}] j
                {vehicleJoin}
                WHERE {whereClause}
                """;
            AddPageParameters(
                countCommand,
                statusCodes,
                searchTerm,
                searchId,
                query.JobCardId,
                query.AllowedVmfCodes
            );
            totalRecords = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
        }

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize));
        page = Math.Min(page, totalPages);
        var skip = (long)(page - 1) * pageSize;
        await using var dataCommand = scope.Connection.CreateCommand();
        dataCommand.Transaction = CurrentTransaction;
        dataCommand.CommandText = $"""
            SELECT
                {BuildProjection(columns, vehicleProjectionColumns, extraColumns)}
            FROM [dbo].[{CurrentTableName}] j
            {vehicleJoin}
            {extraJoin}
            WHERE {whereClause}
            ORDER BY {orderBy}
            OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
            """;
        AddPageParameters(
            dataCommand,
            statusCodes,
            searchTerm,
            searchId,
            query.JobCardId,
            query.AllowedVmfCodes
        );
        AddParameter(dataCommand, "@skip", DbType.Int64, skip);
        AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

        var items = new List<JobCard>();
        await using var reader = await dataCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(Map(reader));

        return new JobCardPage(items, page, pageSize, totalRecords);
    }

    public async Task<JobCardPage> GetPriorityUnassignedPageAsync(
        PriorityUnassignedJobCardPageQuery query
    )
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var columns = await GetAvailableColumnsAsync();
        var vehicleColumns = await GetAvailableColumnsAsync(VehicleTableName);
        var vehicleProjectionColumns = vehicleColumns.ContainsKey("vmf_code")
            ? vehicleColumns
            : new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
        var extraColumns = await GetAvailableColumnsAsync(ExtraCodeTableName);
        var conditions = new List<string> { PriorityUnassignedPredicate(columns) };

        if (columns.ContainsKey("is_deleted"))
            conditions.Add("ISNULL(j.[is_deleted], 0) = 0");
        AddAllowedVehicleCondition(conditions, query.AllowedVmfCodes, "j");

        var whereClause = string.Join(" AND ", conditions);
        var vehicleJoin = vehicleColumns.ContainsKey("vmf_code")
            ? $"LEFT JOIN [dbo].[{VehicleTableName}] vm ON vm.[vmf_code] = j.[vmf_code]"
            : string.Empty;
        var extraJoin = extraColumns.ContainsKey("extra_code")
            ? $"LEFT JOIN [dbo].[{ExtraCodeTableName}] ec ON ec.[extra_code] = j.[extra_code]"
            : string.Empty;
        var orderBy = $"{DateExpression(columns)} DESC, {IdExpression(columns)} DESC";

        await using var scope = await OpenConnectionAsync();
        int totalRecords;
        await using (var countCommand = scope.Connection.CreateCommand())
        {
            countCommand.Transaction = CurrentTransaction;
            countCommand.CommandText = $"""
                SELECT COUNT(DISTINCT {IdExpression(columns)})
                FROM [dbo].[{CurrentTableName}] j
                WHERE {whereClause}
                """;
            AddAllowedVehicleParameters(countCommand, query.AllowedVmfCodes, "j");
            totalRecords = Convert.ToInt32(await countCommand.ExecuteScalarAsync());
        }

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize));
        page = Math.Min(page, totalPages);
        var skip = (long)(page - 1) * pageSize;
        await using var dataCommand = scope.Connection.CreateCommand();
        dataCommand.Transaction = CurrentTransaction;
        dataCommand.CommandText = $"""
            SELECT
                {BuildProjection(columns, vehicleProjectionColumns, extraColumns)}
            FROM [dbo].[{CurrentTableName}] j
            {vehicleJoin}
            {extraJoin}
            WHERE {whereClause}
            ORDER BY {orderBy}
            OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
            """;
        AddParameter(dataCommand, "@skip", DbType.Int64, skip);
        AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);
        AddAllowedVehicleParameters(dataCommand, query.AllowedVmfCodes, "j");

        var items = new List<JobCard>();
        await using var reader = await dataCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(Map(reader));

        return new JobCardPage(items, page, pageSize, totalRecords);
    }

    public async Task<RepairCostReportPage> GetRepairCostReportPageAsync(
        RepairCostReportPageQuery query
    )
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var columns = await GetAvailableColumnsAsync();
        var vehicleColumns = await GetAvailableColumnsAsync(VehicleTableName);
        var vehicleProjectionColumns = vehicleColumns.ContainsKey("vmf_code")
            ? vehicleColumns
            : new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
        var extraColumns = await GetAvailableColumnsAsync(ExtraCodeTableName);
        var vmfCodes = query.VmfCodes?.Distinct().ToArray();
        var conditions = new List<string> { "j.[status_code] = 5" };

        if (columns.ContainsKey("is_deleted"))
            conditions.Add("ISNULL(j.[is_deleted], 0) = 0");
        if (query.VmfCode.HasValue)
            conditions.Add("j.[vmf_code] = @vmfCode");
        if (vmfCodes is not null)
        {
            if (vmfCodes.Length == 0)
                conditions.Add("1 = 0");
            else
            {
                var vmfCodeParameters = string.Join(
                    ", ",
                    vmfCodes.Select((_, index) => $"@siteVmfCode{index}")
                );
                conditions.Add($"j.[vmf_code] IN ({vmfCodeParameters})");
            }
        }
        if (query.FromDate.HasValue)
            conditions.Add($"{UpdatedDateExpression(columns)} >= @fromDate");
        if (query.ToDate.HasValue)
            conditions.Add($"{UpdatedDateExpression(columns)} <= @toDate");

        var whereClause = string.Join(" AND ", conditions);
        var vehicleJoin = vehicleColumns.ContainsKey("vmf_code")
            ? $"LEFT JOIN [dbo].[{VehicleTableName}] vm ON vm.[vmf_code] = j.[vmf_code]"
            : string.Empty;
        var extraJoin = extraColumns.ContainsKey("extra_code")
            ? $"LEFT JOIN [dbo].[{ExtraCodeTableName}] ec ON ec.[extra_code] = j.[extra_code]"
            : string.Empty;
        var orderBy = $"{UpdatedDateExpression(columns)} DESC, {IdExpression(columns)} DESC";

        await using var scope = await OpenConnectionAsync();
        int totalRecords;
        decimal grandTotal = 0;
        decimal totalLabour = 0;
        decimal totalParts = 0;
        decimal totalOther = 0;
        await using (var summaryCommand = scope.Connection.CreateCommand())
        {
            summaryCommand.Transaction = CurrentTransaction;
            summaryCommand.CommandText = $"""
                SELECT
                    COUNT(DISTINCT {IdExpression(columns)}) AS [total_records],
                    COALESCE(SUM({OptionalExpression(
                    columns,
                    "total_cost",
                    "decimal(18, 2)"
                )}), CAST(0 AS decimal(18, 2))) AS [grand_total],
                    COALESCE(SUM({OptionalExpression(
                    columns,
                    "labour_cost",
                    "decimal(18, 2)"
                )}), CAST(0 AS decimal(18, 2))) AS [total_labour],
                    COALESCE(SUM({OptionalExpression(
                    columns,
                    "parts_cost",
                    "decimal(18, 2)"
                )}), CAST(0 AS decimal(18, 2))) AS [total_parts],
                    COALESCE(SUM({OptionalExpression(
                    columns,
                    "other_cost",
                    "decimal(18, 2)"
                )}), CAST(0 AS decimal(18, 2))) AS [total_other]
                FROM [dbo].[{CurrentTableName}] j
                WHERE {whereClause}
                """;
            AddRepairCostParameters(summaryCommand, query, vmfCodes);
            await using var summaryReader = await summaryCommand.ExecuteReaderAsync();
            if (await summaryReader.ReadAsync())
            {
                totalRecords = ReadInt(summaryReader, "total_records") ?? 0;
                grandTotal = ReadDecimal(summaryReader, "grand_total") ?? 0;
                totalLabour = ReadDecimal(summaryReader, "total_labour") ?? 0;
                totalParts = ReadDecimal(summaryReader, "total_parts") ?? 0;
                totalOther = ReadDecimal(summaryReader, "total_other") ?? 0;
            }
            else
            {
                totalRecords = 0;
            }
        }

        var totalPages = Math.Max(1, (int)Math.Ceiling(totalRecords / (double)pageSize));
        page = Math.Min(page, totalPages);
        var skip = (long)(page - 1) * pageSize;
        await using var dataCommand = scope.Connection.CreateCommand();
        dataCommand.Transaction = CurrentTransaction;
        dataCommand.CommandText = $"""
            SELECT
                {BuildProjection(columns, vehicleProjectionColumns, extraColumns)}
            FROM [dbo].[{CurrentTableName}] j
            {vehicleJoin}
            {extraJoin}
            WHERE {whereClause}
            ORDER BY {orderBy}
            OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
            """;
        AddRepairCostParameters(dataCommand, query, vmfCodes);
        AddParameter(dataCommand, "@skip", DbType.Int64, skip);
        AddParameter(dataCommand, "@pageSize", DbType.Int32, pageSize);

        var items = new List<JobCard>();
        await using var reader = await dataCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            items.Add(Map(reader));

        return new RepairCostReportPage(
            items,
            page,
            pageSize,
            totalRecords,
            grandTotal,
            totalLabour,
            totalParts,
            totalOther
        );
    }

    public async Task<IEnumerable<JobCard>> GetByGGNumberAsync(string ggNumber) =>
        await QueryAsync(
            command => AddParameter(command, "@ggNumber", DbType.String, ggNumber.Trim()),
            _ => "(vm.[fleet_number] = @ggNumber OR vm.[registration_number] = @ggNumber)"
        );

    public async Task<IEnumerable<JobCard>> GetPriorityUnassignedAsync() =>
        await QueryAsync(null, PriorityUnassignedPredicate);

    public async Task<IEnumerable<JobCard>> GetAssignedPriorityAsync() =>
        await QueryAsync(
            null,
            columns =>
                HasLegacyReviewedColumn(columns) && GetNumberColumn(columns) is { } numberColumn
                    ? IsBitColumn(columns, "priority")
                        ? $"j.[priority] = 1 AND j.[status_code] = 3 AND j.[{numberColumn}] <> 'Not Assigned'"
                        : $"j.[priority] = 'Y' AND j.[status_code] = 3 AND j.[{numberColumn}] <> 'Not Assigned'"
                    : "j.[priority] = 'H' AND j.[assigned_to] IS NOT NULL AND j.[status_code] NOT IN (5, 7)"
        );

    public async Task<IEnumerable<JobCard>> GetByStatusAsync(int statusCode) =>
        await QueryAsync(
            command => AddParameter(command, "@statusCode", DbType.Int32, statusCode),
            _ => "j.[status_code] = @statusCode"
        );

    public async Task<IEnumerable<JobCard>> GetByAuthorizerAsync(int authorizerUserId) =>
        await QueryAsync(
            command => AddParameter(command, "@authorizer", DbType.Int32, authorizerUserId),
            columns => $"{AuthorizerExpression(columns)} = @authorizer"
        );

    public async Task<JobCard> CreateAsync(JobCard jobCard, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(jobCard);

        if (
            await IsLegacyProcedureAvailableAsync(
                "DEV_INS_NewJobCards",
                "@GGNumber",
                "@extraCode",
                "@CaptureBy"
            )
        )
        {
            var createdId = await CreateLegacyAsync(jobCard, currentUserId);
            return await GetByIdAsync(createdId)
                ?? throw new InvalidOperationException(
                    "The legacy DEV_INS_NewJobCards procedure completed without creating a readable job card."
                );
        }

        var columns = await GetAvailableColumnsAsync();

        // Compatibility fallback only where DEV_INS_NewJobCards is genuinely absent.
        if (columns.ContainsKey("job_card_id"))
        {
            var values = new List<WriteValue>();
            AddValue(values, columns, "vmf_code", "@vmfCode", DbType.Int32, jobCard.vmf_code, true);
            AddValue(
                values,
                columns,
                "extra_code",
                "@extraCode",
                DbType.Int16,
                jobCard.extra_code,
                true
            );
            AddValue(
                values,
                columns,
                "status_code",
                "@statusCode",
                DbType.Int32,
                jobCard.status_code == 0 ? 1 : jobCard.status_code,
                true
            );
            AddValue(
                values,
                columns,
                "priority",
                "@priority",
                DbType.String,
                jobCard.priority,
                true
            );
            AddValue(
                values,
                columns,
                "jcs_comment",
                "@jcsComment",
                DbType.String,
                jobCard.jcs_comment,
                true
            );
            AddValue(values, columns, "damages", "@damages", DbType.String, jobCard.damages, true);
            AddValue(
                values,
                columns,
                "reviewed",
                "@reviewed",
                DbType.String,
                jobCard.reviewed ?? "N",
                true
            );
            AddValue(
                values,
                columns,
                "date_created",
                "@dateCreated",
                DbType.DateTime2,
                DateTime.UtcNow,
                true
            );
            AddValue(
                values,
                columns,
                "created_by_user_code",
                "@createdBy",
                DbType.Int32,
                UserIdOrNull(currentUserId),
                true
            );
            AddValue(values, columns, "is_deleted", "@isDeleted", DbType.Boolean, false, true);

            await using var scope = await OpenConnectionAsync();
            await using var command = scope.Connection.CreateCommand();
            command.Transaction = CurrentTransaction;
            command.CommandText =
                $"INSERT INTO [dbo].[{CurrentTableName}] ({string.Join(", ", values.Select(x => $"[{x.Column}]"))}) OUTPUT INSERTED.[job_card_id] VALUES ({string.Join(", ", values.Select(x => x.Parameter))})";
            AddParameters(command, values);
            var id = Convert.ToInt32(await command.ExecuteScalarAsync());
            return await GetByIdAsync(id)
                ?? throw new InvalidOperationException("Created job card could not be read.");
        }

        var legacyId = await InsertLegacyDirectAsync(jobCard, currentUserId);
        return await GetByIdAsync(legacyId)
            ?? throw new InvalidOperationException("Created legacy job card could not be read.");
    }

    public async Task<JobCard> UpdateAsync(JobCard jobCard, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(jobCard);
        var existing =
            await GetByIdAsync(jobCard.job_card_id)
            ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCard.job_card_id}");

        var columns = await GetAvailableColumnsAsync();
        var mutationProcedure = await GetJobCardMutationProcedureAsync(columns);
        if (mutationProcedure is not null)
        {
            await ExecuteLegacyProcedurePreservingCapturerAsync(
                mutationProcedure.Name,
                jobCard.job_card_id,
                existing.created_by_user_code,
                new ProcedureParameter("@VmfCode", DbType.Int32, existing.vmf_code),
                new ProcedureParameter("@extra_code", DbType.Int16, existing.extra_code),
                new ProcedureParameter(
                    mutationProcedure.CommentParameter,
                    DbType.String,
                    jobCard.jcs_comment
                ),
                new ProcedureParameter(
                    "@AssignedTo",
                    DbType.String,
                    jobCard.assigned_to?.ToString()
                ),
                new ProcedureParameter(
                    "@AssignedDate",
                    DbType.String,
                    jobCard.assigned_date?.ToString("yyyy/MM/dd")
                ),
                new ProcedureParameter("@Damages", DbType.String, NormalizeDamage(jobCard.damages)),
                new ProcedureParameter("@comments", DbType.String, jobCard.comments),
                new ProcedureParameter(
                    mutationProcedure.StatusParameter,
                    DbType.Byte,
                    existing.status_code
                ),
                new ProcedureParameter("@UserID", DbType.Int32, currentUserId),
                new ProcedureParameter("@barcode", DbType.String, null),
                new ProcedureParameter("@dateclosed", DbType.DateTime, null)
            );
            return await GetByIdAsync(jobCard.job_card_id)
                ?? throw new InvalidOperationException(
                    "The legacy job-card update procedure removed the selected job card."
                );
        }

        // Compatibility fallback only where the matching client-era procedure is genuinely absent.
        var values = new List<WriteValue>();

        AddValue(
            values,
            columns,
            "jcs_comment",
            "@jcsComment",
            DbType.String,
            jobCard.jcs_comment,
            true
        );
        AddValue(values, columns, "damages", "@damages", DbType.String, jobCard.damages, true);
        AddValue(values, columns, "comments", "@comments", DbType.String, jobCard.comments, true);
        AddValue(
            values,
            columns,
            "priority",
            "@priority",
            DbType.String,
            NormalizePriority(jobCard.priority, columns),
            true
        );
        AddValue(
            values,
            columns,
            "assigned_to",
            "@assignedTo",
            DbType.Int32,
            jobCard.assigned_to,
            false
        );
        AddValue(
            values,
            columns,
            "assigned_date",
            "@assignedDate",
            DbType.DateTime2,
            jobCard.assigned_date,
            false
        );
        AddValue(
            values,
            columns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            DateTime.UtcNow,
            true
        );
        AddValue(
            values,
            columns,
            "modified_by_user_code",
            "@modifiedBy",
            DbType.Int32,
            UserIdOrNull(currentUserId),
            true
        );

        if (HasLegacyReviewedColumn(columns))
        {
            var handoverNameColumn = columns.ContainsKey("HandedOverTo")
                ? "HandedOverTo"
                : "hhandover_name";
            var handoverDateColumn = columns.ContainsKey("HandedOverOn")
                ? "HandedOverOn"
                : "hhandover_date";
            var authorizerCommentColumn = columns.ContainsKey("Authoriser_comments")
                ? "Authoriser_comments"
                : "authorizer_jobcard_comments";
            AddValue(
                values,
                columns,
                handoverNameColumn,
                "@handoverName",
                DbType.String,
                jobCard.assigned_to?.ToString(),
                false
            );
            AddValue(
                values,
                columns,
                handoverDateColumn,
                "@handoverDate",
                DbType.String,
                jobCard.assigned_date?.ToString("yyyy/MM/dd"),
                false
            );
            AddValue(
                values,
                columns,
                authorizerCommentColumn,
                "@authorizerComments",
                DbType.String,
                jobCard.comments,
                false
            );
        }

        await ExecuteUpdateAsync(jobCard.job_card_id, columns, values);
        return await GetByIdAsync(jobCard.job_card_id)
            ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCard.job_card_id}");
    }

    public async Task DeleteAsync(int jobCardId, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
        var existing = await GetByIdAsync(jobCardId)
            ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");

        if (
            await IsLegacyProcedureAvailableAsync(
                "DEV_DEL_Jobcard",
                "@vmf_code",
                "@extraDescription"
            )
        )
        {
            var extraDescription = await FindExtraDescriptionAsync(existing.extra_code);
            if (string.IsNullOrWhiteSpace(extraDescription))
            {
                throw new InvalidOperationException(
                    "The legacy job-card delete procedure requires the selected category description."
                );
            }

            await ExecuteLegacyProcedureAsync(
                "DEV_DEL_Jobcard",
                new ProcedureParameter("@vmf_code", DbType.Int32, existing.vmf_code),
                new ProcedureParameter("@extraDescription", DbType.String, extraDescription)
            );
            if (await GetByIdAsync(jobCardId) is not null)
            {
                throw new InvalidOperationException(
                    "The legacy job-card delete procedure completed without deleting the selected job card."
                );
            }
            return;
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;

        if (columns.ContainsKey("is_deleted"))
        {
            command.CommandText =
                $"UPDATE [dbo].[{CurrentTableName}] SET [is_deleted] = 1, [date_updated] = @dateUpdated, [modified_by_user_code] = @modifiedBy WHERE [job_card_id] = @jobCardId";
            AddParameter(command, "@dateUpdated", DbType.DateTime2, DateTime.UtcNow);
            AddParameter(command, "@modifiedBy", DbType.Int32, UserIdOrNull(currentUserId));
        }
        else
        {
            command.CommandText =
                $"DELETE FROM [dbo].[{CurrentTableName}] WHERE [{GetIdColumn(columns)}] = @jobCardId";
        }

        AddParameter(command, "@jobCardId", DbType.Int32, jobCardId);
        if (await command.ExecuteNonQueryAsync() == 0)
        {
            throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");
        }
    }

    public async Task<JobCard> AuthorizeAsync(
        int jobCardId,
        int authorizerUserId,
        string? comment
    ) =>
        await UpdateLegacyAuthorizerWorkflowAsync(
            jobCardId,
            authorizerUserId,
            3,
            comment
        );

    public async Task<JobCard> DeclineAsync(
        int jobCardId,
        int authorizerUserId,
        string declineReason
    ) =>
        await UpdateLegacyAuthorizerWorkflowAsync(
            jobCardId,
            authorizerUserId,
            1,
            declineReason
        );

    private async Task<JobCard> UpdateLegacyAuthorizerWorkflowAsync(
        int jobCardId,
        int authorizerUserId,
        int statusCode,
        string? comment
    )
    {
        var columns = await GetAvailableColumnsAsync();
        var authorizerProcedure = await GetJobCardAuthorizerProcedureAsync(columns);
        if (authorizerProcedure is not null)
        {
            var existing =
                await GetByIdAsync(jobCardId)
                ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");
            var fleetNumber = await FindFleetNumberAsync(existing);
            if (string.IsNullOrWhiteSpace(fleetNumber))
            {
                throw new InvalidOperationException(
                    "The selected legacy job card has no fleet number required for authorisation."
                );
            }

            await ExecuteLegacyProcedureAsync(
                authorizerProcedure.Name,
                new ProcedureParameter("@ggnumber", DbType.String, fleetNumber),
                new ProcedureParameter("@extra_code", DbType.Int16, existing.extra_code),
                new ProcedureParameter(
                    "@priority",
                    authorizerProcedure.UsesBitFlags ? DbType.Boolean : DbType.String,
                    authorizerProcedure.UsesBitFlags
                        ? PriorityAsBoolean(existing.priority)
                        : NormalizePriority(existing.priority, columns)
                ),
                new ProcedureParameter(
                    authorizerProcedure.AuthorizerParameter,
                    DbType.Int32,
                    authorizerUserId
                ),
                new ProcedureParameter("@comment", DbType.String, comment),
                new ProcedureParameter(
                    "@reviewed",
                    authorizerProcedure.UsesBitFlags ? DbType.Boolean : DbType.String,
                    authorizerProcedure.UsesBitFlags ? true : "Y"
                ),
                new ProcedureParameter(
                    authorizerProcedure.StatusParameter,
                    DbType.Byte,
                    statusCode
                ),
                new ProcedureParameter(
                    "@AssignedTo",
                    DbType.String,
                    existing.assigned_to?.ToString()
                ),
                new ProcedureParameter(
                    "@AssignedDate",
                    DbType.String,
                    existing.assigned_date?.ToString("yyyy/MM/dd")
                )
            );
            return await GetByIdAsync(jobCardId)
                ?? throw new InvalidOperationException(
                    "The legacy job-card authoriser procedure removed the selected job card."
                );
        }

        // Compatibility fallback only where the matching client-era procedure is absent.
        return await UpdateWorkflowAsync(jobCardId, authorizerUserId, statusCode, "Y", comment, null);
    }

    public async Task<JobCard> CancelAsync(
        int jobCardId,
        int currentUserId,
        string? cancelReason
    )
    {
        var existing = await GetByIdAsync(jobCardId)
            ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");
        if (
            await IsLegacyProcedureAvailableAsync(
                "DEV_UPD_JobcardCancelRequest",
                "@jcnumber",
                "@userid"
            )
        )
        {
            var jobCardNumber = await FindLegacyJobCardNumberAsync(jobCardId);
            if (string.IsNullOrWhiteSpace(jobCardNumber))
            {
                throw new InvalidOperationException(
                    "The selected legacy job card has no job-card number required for cancellation."
                );
            }

            await ExecuteLegacyProcedurePreservingCapturerAsync(
                "DEV_UPD_JobcardCancelRequest",
                jobCardId,
                existing.created_by_user_code,
                new ProcedureParameter("@jcnumber", DbType.String, jobCardNumber),
                new ProcedureParameter("@userid", DbType.Int32, currentUserId)
            );
            return await GetByIdAsync(jobCardId)
                ?? throw new InvalidOperationException(
                    "The legacy job-card cancellation procedure removed the selected job card."
                );
        }

        // Compatibility fallback only where DEV_UPD_JobcardCancelRequest is absent.
        return await UpdateWorkflowAsync(
            jobCardId,
            currentUserId,
            7,
            null,
            cancelReason is null ? null : $"Canceled: {cancelReason}",
            existing.created_by_user_code
        );
    }

    public async Task<JobCard> CloseAsync(
        int jobCardId,
        int currentUserId,
        string? closeNotes,
        decimal? labourCost = null,
        decimal? partsCost = null,
        decimal? otherCost = null,
        string? invoiceNumber = null,
        DateTime? invoiceDate = null,
        string? serviceProvider = null,
        string? damages = null,
        string? damageComment = null,
        string? barcode = null,
        DateTime? closeDate = null
    )
    {
        var columns = await GetAvailableColumnsAsync();
        EnsureLegacyCostFieldsAreAvailable(
            columns,
            labourCost,
            partsCost,
            otherCost,
            invoiceNumber,
            invoiceDate,
            serviceProvider
        );

        var mutationProcedure = await GetJobCardMutationProcedureAsync(columns);
        if (mutationProcedure is not null)
        {
            var existing =
                await GetByIdAsync(jobCardId)
                ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");
            await ExecuteLegacyProcedurePreservingCapturerAsync(
                mutationProcedure.Name,
                jobCardId,
                existing.created_by_user_code,
                new ProcedureParameter("@VmfCode", DbType.Int32, existing.vmf_code),
                new ProcedureParameter("@extra_code", DbType.Int16, existing.extra_code),
                new ProcedureParameter(
                    mutationProcedure.CommentParameter,
                    DbType.String,
                    closeNotes
                ),
                new ProcedureParameter("@AssignedTo", DbType.String, null),
                new ProcedureParameter("@AssignedDate", DbType.String, null),
                new ProcedureParameter(
                    "@Damages",
                    DbType.String,
                    NormalizeDamage(damages ?? existing.damages)
                ),
                // The legacy close form accepts a fresh damage comment. The modern
                // close DTO has no equivalent field, so do not replay a historic
                // comment and create a duplicate vehicle-damage side effect.
                new ProcedureParameter("@comments", DbType.String, damageComment),
                new ProcedureParameter(mutationProcedure.StatusParameter, DbType.Byte, 5),
                new ProcedureParameter("@UserID", DbType.Int32, currentUserId),
                new ProcedureParameter("@barcode", DbType.String, barcode),
                new ProcedureParameter("@dateclosed", DbType.DateTime, closeDate ?? DateTime.Now)
            );
            return await GetByIdAsync(jobCardId)
                ?? throw new InvalidOperationException(
                    "The legacy job-card close procedure removed the selected job card."
                );
        }

        // Compatibility fallback only where the matching client-era procedure is genuinely absent.
        return await UpdateWorkflowAsync(
            jobCardId,
            currentUserId,
            5,
            null,
            closeNotes is null ? null : $"Closed: {closeNotes}",
            null,
            labourCost,
            partsCost,
            otherCost,
            invoiceNumber,
            invoiceDate,
            serviceProvider
        );
    }

    public async Task<JobCard> UpdateStatusAsync(
        int jobCardId,
        int newStatusCode,
        int currentUserId
    )
    {
        if (
            newStatusCode == 4
            && await IsLegacyProcedureAvailableAsync(
                "DEV_UPD_JobcardUpdateStatusToInProgress",
                "@ggnumber",
                "@jcnumber"
            )
        )
        {
            var existing =
                await GetByIdAsync(jobCardId)
                ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");
            var fleetNumber = await FindFleetNumberAsync(existing);
            var jobCardNumber = await FindLegacyJobCardNumberAsync(jobCardId);
            if (string.IsNullOrWhiteSpace(fleetNumber) || string.IsNullOrWhiteSpace(jobCardNumber))
            {
                throw new InvalidOperationException(
                    "The selected legacy job card lacks the fleet or job-card number required to mark it in progress."
                );
            }

            await ExecuteLegacyProcedureAsync(
                "DEV_UPD_JobcardUpdateStatusToInProgress",
                new ProcedureParameter("@ggnumber", DbType.String, fleetNumber),
                new ProcedureParameter("@jcnumber", DbType.String, jobCardNumber)
            );
            return await GetByIdAsync(jobCardId)
                ?? throw new InvalidOperationException(
                    "The legacy in-progress procedure removed the selected job card."
                );
        }

        // Compatibility fallback only where the legacy in-progress procedure is absent.
        return await UpdateWorkflowAsync(jobCardId, currentUserId, newStatusCode, null, null, null);
    }

    public async Task<JobCard> UpdateCostsAsync(
        int jobCardId,
        int currentUserId,
        decimal? labourCost,
        decimal? partsCost,
        decimal? otherCost,
        string? invoiceNumber,
        DateTime? invoiceDate,
        string? serviceProvider
    )
    {
        var columns = await GetAvailableColumnsAsync();
        EnsureLegacyCostFieldsAreAvailable(
            columns,
            labourCost,
            partsCost,
            otherCost,
            invoiceNumber,
            invoiceDate,
            serviceProvider
        );
        var values = new List<WriteValue>();
        AddValue(values, columns, "labour_cost", "@labourCost", DbType.Decimal, labourCost, false);
        AddValue(values, columns, "parts_cost", "@partsCost", DbType.Decimal, partsCost, false);
        AddValue(values, columns, "other_cost", "@otherCost", DbType.Decimal, otherCost, false);
        AddValue(
            values,
            columns,
            "invoice_number",
            "@invoiceNumber",
            DbType.String,
            invoiceNumber,
            false
        );
        AddValue(
            values,
            columns,
            "invoice_date",
            "@invoiceDate",
            DbType.DateTime2,
            invoiceDate,
            false
        );
        AddValue(
            values,
            columns,
            "service_provider",
            "@serviceProvider",
            DbType.String,
            serviceProvider,
            false
        );
        AddValue(
            values,
            columns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            DateTime.UtcNow,
            false
        );
        AddValue(
            values,
            columns,
            "modified_by_user_code",
            "@modifiedBy",
            DbType.Int32,
            UserIdOrNull(currentUserId),
            false
        );

        if (values.Count > 0)
        {
            await ExecuteUpdateAsync(jobCardId, columns, values);
        }

        // An empty amendment remains a no-op. Any supplied cost value was
        // checked above and is never reported as saved when the original
        // table cannot persist it.
        return await GetByIdAsync(jobCardId)
            ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");
    }

    private static void EnsureLegacyCostFieldsAreAvailable(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        decimal? labourCost,
        decimal? partsCost,
        decimal? otherCost,
        string? invoiceNumber,
        DateTime? invoiceDate,
        string? serviceProvider
    )
    {
        var containsCostInput = labourCost.HasValue
            || partsCost.HasValue
            || otherCost.HasValue
            || invoiceNumber is not null
            || invoiceDate.HasValue
            || serviceProvider is not null;
        if (!containsCostInput)
            return;

        var unavailableFields = new List<string>();
        if (labourCost.HasValue && !columns.ContainsKey("labour_cost"))
            unavailableFields.Add("labour cost");
        if (partsCost.HasValue && !columns.ContainsKey("parts_cost"))
            unavailableFields.Add("parts cost");
        if (otherCost.HasValue && !columns.ContainsKey("other_cost"))
            unavailableFields.Add("other cost");
        if (invoiceNumber is not null && !columns.ContainsKey("invoice_number"))
            unavailableFields.Add("invoice number");
        if (invoiceDate.HasValue && !columns.ContainsKey("invoice_date"))
            unavailableFields.Add("invoice date");
        if (serviceProvider is not null && !columns.ContainsKey("service_provider"))
            unavailableFields.Add("service provider");

        if (unavailableFields.Count > 0)
        {
            throw new NotSupportedException(
                $"Repair-cost capture cannot save {string.Join(", ", unavailableFields)} in this Jobcards schema."
            );
        }
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
        string? serviceProvider = null
    )
    {
        var existing =
            await GetByIdAsync(jobCardId)
            ?? throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");
        var columns = await GetAvailableColumnsAsync();
        var values = new List<WriteValue>();

        AddValue(values, columns, "status_code", "@statusCode", DbType.Int32, statusCode, true);
        AddValue(
            values,
            columns,
            "authorizer",
            "@authorizer",
            DbType.Int32,
            statusCode is 3 or 1 ? currentUserId : existing.authorizer,
            false
        );
        AddValue(values, columns, "reviewed", "@reviewed", DbType.String, reviewed, false);
        AddValue(
            values,
            columns,
            "date_updated",
            "@dateUpdated",
            DbType.DateTime2,
            DateTime.UtcNow,
            false
        );
        AddValue(
            values,
            columns,
            "modified_by_user_code",
            "@modifiedBy",
            DbType.Int32,
            UserIdOrNull(currentUserId),
            false
        );
        AddValue(
            values,
            columns,
            "comments",
            "@comments",
            DbType.String,
            Append(existing.comments, comment),
            comment is not null
        );
        AddValue(values, columns, "labour_cost", "@labourCost", DbType.Decimal, labourCost, false);
        AddValue(values, columns, "parts_cost", "@partsCost", DbType.Decimal, partsCost, false);
        AddValue(values, columns, "other_cost", "@otherCost", DbType.Decimal, otherCost, false);
        AddValue(
            values,
            columns,
            "invoice_number",
            "@invoiceNumber",
            DbType.String,
            invoiceNumber,
            false
        );
        AddValue(
            values,
            columns,
            "invoice_date",
            "@invoiceDate",
            DbType.DateTime2,
            invoiceDate,
            false
        );
        AddValue(
            values,
            columns,
            "service_provider",
            "@serviceProvider",
            DbType.String,
            serviceProvider,
            false
        );

        if (
            columns.ContainsKey("total_cost")
            && (labourCost.HasValue || partsCost.HasValue || otherCost.HasValue)
        )
        {
            var total =
                (existing.labour_cost ?? 0m)
                + (existing.parts_cost ?? 0m)
                + (existing.other_cost ?? 0m);
            total += labourCost.HasValue ? labourCost.Value - (existing.labour_cost ?? 0m) : 0m;
            total += partsCost.HasValue ? partsCost.Value - (existing.parts_cost ?? 0m) : 0m;
            total += otherCost.HasValue ? otherCost.Value - (existing.other_cost ?? 0m) : 0m;
            AddValue(values, columns, "total_cost", "@totalCost", DbType.Decimal, total, true);
        }

        if (capturedBy.HasValue)
        {
            AddValue(
                values,
                columns,
                "created_by_user_code",
                "@capturedBy",
                DbType.Int32,
                capturedBy.Value,
                false
            );
        }

        if (HasLegacyReviewedColumn(columns))
        {
            var reviewedColumn = GetLegacyReviewedColumn(columns)!;
            var authorizerColumn = columns.ContainsKey("Authoriser")
                ? "Authoriser"
                : "Authorizer";
            var authorizerCommentColumn = columns.ContainsKey("Authoriser_comments")
                ? "Authoriser_comments"
                : "authorizer_jobcard_comments";
            var authorizerDateColumn = GetAuthorizerUpdateDateColumn(columns);
            AddValue(
                values,
                columns,
                authorizerColumn,
                "@legacyAuthorizer",
                DbType.Int32,
                statusCode is 3 or 1 ? currentUserId : existing.authorizer,
                false
            );
            AddValue(
                values,
                columns,
                reviewedColumn,
                "@legacyReviewed",
                IsBitColumn(columns, reviewedColumn) ? DbType.Boolean : DbType.String,
                IsBitColumn(columns, reviewedColumn)
                    ? string.Equals(reviewed, "Y", StringComparison.OrdinalIgnoreCase)
                    : reviewed,
                false
            );
            if (authorizerDateColumn is not null)
                AddValue(
                    values,
                    columns,
                    authorizerDateColumn,
                    "@legacyAuthorizerDate",
                    DbType.DateTime,
                    DateTime.Now,
                    false
                );
            AddValue(
                values,
                columns,
                authorizerCommentColumn,
                "@legacyComment",
                DbType.String,
                comment,
                comment is not null
            );
            AddValue(
                values,
                columns,
                "DateClosed",
                "@dateClosed",
                DbType.DateTime,
                statusCode == 5 ? DateTime.Now : null,
                statusCode == 5
            );
            AddValue(
                values,
                columns,
                "captured_by",
                "@legacyCapturedBy",
                DbType.Int32,
                capturedBy,
                false
            );
            if (comment is not null && (statusCode is 5 or 7))
            {
                AddValue(
                    values,
                    columns,
                    columns.ContainsKey("Status_comment") ? "Status_comment" : "jcs_comment",
                    "@legacyWorkflowComment",
                    DbType.String,
                    Append(existing.jcs_comment, comment),
                    true
                );
            }
        }

        await ExecuteUpdateAsync(jobCardId, columns, values);
        return await GetByIdAsync(jobCardId)
            ?? throw new InvalidOperationException("Updated job card could not be read.");
    }

    private async Task<int> CreateLegacyAsync(JobCard jobCard, int currentUserId)
    {
        var vehicleNumber = await FindVehicleNumberAsync(jobCard);
        if (string.IsNullOrWhiteSpace(vehicleNumber))
        {
            throw new InvalidOperationException(
                "The selected vehicle has no fleet number required by the legacy job-card procedure."
            );
        }

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "[dbo].[DEV_INS_NewJobCards]";
        AddParameter(command, "@GGNumber", DbType.String, vehicleNumber);
        AddParameter(command, "@extraCode", DbType.Int32, jobCard.extra_code);
        AddParameter(command, "@CaptureBy", DbType.Int32, currentUserId);

        await command.ExecuteNonQueryAsync();

        var createdId = await FindLatestLegacyIdAsync(
            scope.Connection,
            jobCard.vmf_code,
            jobCard.extra_code,
            currentUserId
        );
        if (createdId > 0)
            return createdId;

        throw new InvalidOperationException(
            "The legacy job-card procedure completed without creating a readable job card."
        );
    }

    private async Task<int> InsertLegacyDirectAsync(JobCard jobCard, int currentUserId)
    {
        var columns = await GetAvailableColumnsAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;

        if (columns.ContainsKey("JobCard_code"))
        {
            // DEV_INS_NewJobCards is absent from the restored client database.
            // This fallback keeps the archived procedure's initial workflow
            // state (unassigned, status 1) while using its current column names.
            command.CommandText = $"""
                INSERT INTO [dbo].[{CurrentTableName}]
                    ([Counter], [year], [Number], [vmf_code], [extra_code], [Status_date], [captured_by], [Status_code], [priority], [reviewed_by_Authoriser])
                OUTPUT INSERTED.[JobCard_code]
                VALUES
                    (0, '0', 'Not Assigned', @vmfCode, @extraCode, CONVERT(varchar(10), GETDATE(), 111), @capturedBy, 1, @priority, 0)
                """;
            AddParameter(command, "@priority", DbType.Boolean, PriorityAsBoolean(jobCard.priority));
        }
        else
        {
            command.CommandText = $"""
                INSERT INTO [dbo].[{CurrentTableName}]
                    ([jc_counter], [year], [jc_number], [vmf_code], [extra_code], [jcs_comment], [jcs_date], [captured_by], [status_code], [priority], [reviewed_by_Authorizer])
                OUTPUT INSERTED.[jc_code]
                VALUES
                    ((SELECT ISNULL(MAX([jc_counter]), 0) + 1 FROM [dbo].[{CurrentTableName}]), CONVERT(nchar(10), YEAR(GETDATE())), 'Not Assigned', @vmfCode, @extraCode, @jcsComment, CONVERT(varchar(10), GETDATE(), 111), @capturedBy, 1, @priority, 'N')
                """;
            AddParameter(
                command,
                "@priority",
                DbType.String,
                NormalizePriority(
                    jobCard.priority,
                    new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["reviewed_by_Authorizer"] = new("reviewed_by_Authorizer"),
                    }
                )
            );
            AddParameter(command, "@jcsComment", DbType.String, jobCard.jcs_comment);
        }

        AddParameter(command, "@vmfCode", DbType.Int32, jobCard.vmf_code);
        AddParameter(command, "@extraCode", DbType.Int32, jobCard.extra_code);
        AddParameter(command, "@capturedBy", DbType.Int32, currentUserId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private async Task<int> FindLatestLegacyIdAsync(
        DbConnection connection,
        int vmfCode,
        short extraCode,
        int currentUserId
    )
    {
        await using var command = connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        var columns = await GetAvailableColumnsAsync();
        var idColumn = GetIdColumn(columns);
        command.CommandText =
            $"SELECT TOP (1) [{idColumn}] FROM [dbo].[{CurrentTableName}] WHERE [vmf_code] = @vmfCode AND [extra_code] = @extraCode AND [captured_by] = @capturedBy ORDER BY [{idColumn}] DESC";
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
        command.CommandText =
            "SELECT TOP (1) COALESCE(NULLIF([fleet_number], ''), [registration_number]) FROM [dbo].[vehicle_master] WHERE [vmf_code] = @vmfCode";
        AddParameter(command, "@vmfCode", DbType.Int32, jobCard.vmf_code);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value)?.Trim();
    }

    private async Task<string?> FindFleetNumberAsync(JobCard jobCard)
    {
        if (!string.IsNullOrWhiteSpace(jobCard.Vehicle?.fleet_number))
            return jobCard.Vehicle.fleet_number.Trim();

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            "SELECT TOP (1) NULLIF([fleet_number], '') FROM [dbo].[vehicle_master] WHERE [vmf_code] = @vmfCode";
        AddParameter(command, "@vmfCode", DbType.Int32, jobCard.vmf_code);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value)?.Trim();
    }

    private async Task<string?> FindExtraDescriptionAsync(short extraCode)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            $"SELECT TOP (1) [extra_description] FROM [dbo].[{ExtraCodeTableName}] WHERE [extra_code] = @extraCode";
        AddParameter(command, "@extraCode", DbType.Int16, extraCode);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value)?.Trim();
    }

    private async Task<string?> FindLegacyJobCardNumberAsync(int jobCardId)
    {
        var columns = await GetAvailableColumnsAsync();
        var numberColumn = GetNumberColumn(columns);
        if (numberColumn is null)
            return null;

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            $"SELECT [{numberColumn}] FROM [dbo].[{CurrentTableName}] WHERE {IdExpression(columns)} = @jobCardId";
        AddParameter(command, "@jobCardId", DbType.Int32, jobCardId);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? null : Convert.ToString(value)?.Trim();
    }

    private async Task<bool> StoredProcedureExistsAsync(string procedureName)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = """
            SELECT CASE WHEN EXISTS
            (
                SELECT 1
                FROM [sys].[procedures] AS p
                INNER JOIN [sys].[schemas] AS s ON s.[schema_id] = p.[schema_id]
                WHERE s.[name] = N'dbo' AND p.[name] = @procedureName
            ) THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END
            """;
        AddParameter(command, "@procedureName", DbType.String, procedureName);
        return Convert.ToBoolean(await command.ExecuteScalarAsync());
    }

    private async Task<bool> IsLegacyProcedureAvailableAsync(
        string procedureName,
        params string[] expectedParameters
    )
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = """
            SELECT [parameterObject].[name]
            FROM [sys].[procedures] AS [procedureObject]
            INNER JOIN [sys].[schemas] AS [schemaObject]
                ON [schemaObject].[schema_id] = [procedureObject].[schema_id]
            LEFT JOIN [sys].[parameters] AS [parameterObject]
                ON [parameterObject].[object_id] = [procedureObject].[object_id]
            WHERE [schemaObject].[name] = N'dbo'
              AND [procedureObject].[name] = @procedureName
            ORDER BY [parameterObject].[parameter_id]
            """;
        AddParameter(command, "@procedureName", DbType.String, procedureName);

        var actualParameters = new List<string>();
        var procedureFound = false;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            procedureFound = true;
            if (!reader.IsDBNull(0))
                actualParameters.Add(reader.GetString(0));
        }

        if (!procedureFound)
            return false;

        if (!actualParameters.SequenceEqual(expectedParameters, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The deployed legacy procedure {procedureName} does not match the archived parameter contract. No direct-DML fallback was run."
            );
        }

        return true;
    }

    private async Task<JobCardMutationProcedure?> GetJobCardMutationProcedureAsync(
        IReadOnlyDictionary<string, ColumnInfo> columns
    )
    {
        if (columns.ContainsKey("JobCard_code"))
        {
            return await IsLegacyProcedureAvailableAsync(
                "DEV_UPD_JobCard",
                "@VmfCode",
                "@extra_code",
                "@Status_comment",
                "@AssignedTo",
                "@AssignedDate",
                "@Damages",
                "@comments",
                "@Status_code",
                "@UserID",
                "@barcode",
                "@dateclosed"
            )
                ? new JobCardMutationProcedure(
                    "DEV_UPD_JobCard",
                    "@Status_comment",
                    "@Status_code"
                )
                : null;
        }

        return await IsLegacyProcedureAvailableAsync(
            "DEV_UPD_Jobcards",
            "@VmfCode",
            "@extra_code",
            "@jcs_comment",
            "@AssignedTo",
            "@AssignedDate",
            "@Damages",
            "@comments",
            "@status_code",
            "@UserID",
            "@barcode",
            "@dateclosed"
        )
            ? new JobCardMutationProcedure("DEV_UPD_Jobcards", "@jcs_comment", "@status_code")
            : null;
    }

    private async Task<JobCardAuthorizerProcedure?> GetJobCardAuthorizerProcedureAsync(
        IReadOnlyDictionary<string, ColumnInfo> columns
    )
    {
        if (columns.ContainsKey("JobCard_code"))
        {
            return await IsLegacyProcedureAvailableAsync(
                "DEV_UPD_JobcardAuthorisersUpdates",
                "@ggnumber",
                "@extra_code",
                "@priority",
                "@Authoriser",
                "@comment",
                "@reviewed",
                "@JobCardtatus",
                "@AssignedTo",
                "@AssignedDate"
            )
                ? new JobCardAuthorizerProcedure(
                    "DEV_UPD_JobcardAuthorisersUpdates",
                    "@Authoriser",
                    "@JobCardtatus",
                    UsesBitFlags: true
                )
                : null;
        }

        return await IsLegacyProcedureAvailableAsync(
            "DEV_UPD_JobcardAuthorizersUpdates",
            "@ggnumber",
            "@extra_code",
            "@priority",
            "@Authorizer",
            "@comment",
            "@reviewed",
            "@JobcardStatus",
            "@AssignedTo",
            "@AssignedDate"
        )
            ? new JobCardAuthorizerProcedure(
                "DEV_UPD_JobcardAuthorizersUpdates",
                "@Authorizer",
                "@JobcardStatus",
                UsesBitFlags: false
            )
            : null;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Procedure names are fixed archived legacy contracts, never request input."
    )]
    private async Task ExecuteLegacyProcedureAsync(
        string procedureName,
        params ProcedureParameter[] parameters
    )
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = procedureName;
        command.CommandTimeout = 0;
        foreach (var parameter in parameters)
            AddParameter(command, parameter.Name, parameter.Type, parameter.Value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task ExecuteLegacyProcedurePreservingCapturerAsync(
        string procedureName,
        int jobCardId,
        int? originalCapturer,
        params ProcedureParameter[] parameters
    )
    {
        var existingTransaction = _context.Database.CurrentTransaction;
        var ownsTransaction = existingTransaction is null;
        var transaction = ownsTransaction
            ? await _context.Database.BeginTransactionAsync()
            : null;
        const string savepointName = "FIS_JobCardCapturer";
        if (!ownsTransaction)
            await existingTransaction!.CreateSavepointAsync(savepointName);
        try
        {
            await ExecuteLegacyProcedureAsync(procedureName, parameters);
            await RestoreLegacyCapturerAsync(jobCardId, originalCapturer);
            if (transaction is not null)
                await transaction.CommitAsync();
        }
        catch
        {
            if (transaction is not null)
                await transaction.RollbackAsync();
            else if (existingTransaction is not null)
                await existingTransaction.RollbackToSavepointAsync(savepointName);
            throw;
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    private async Task RestoreLegacyCapturerAsync(int jobCardId, int? originalCapturer)
    {
        if (originalCapturer is not > 0)
            return;

        var columns = await GetAvailableColumnsAsync();
        var ownerColumns = new[] { "captured_by", "created_by_user_code" }
            .Where(columns.ContainsKey)
            .ToArray();
        if (ownerColumns.Length == 0)
            return;

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = $"UPDATE [dbo].[{CurrentTableName}] SET {string.Join(
            ", ",
            ownerColumns.Select(column => $"[{column}] = @originalCapturer")
        )} WHERE [{GetIdColumn(columns)}] = @jobCardId";
        AddParameter(command, "@originalCapturer", DbType.Int32, originalCapturer.Value);
        AddParameter(command, "@jobCardId", DbType.Int32, jobCardId);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<List<JobCard>> QueryAsync(
        Action<DbCommand>? configure = null,
        Func<IReadOnlyDictionary<string, ColumnInfo>, string?>? predicateFactory = null
    )
    {
        var columns = await GetAvailableColumnsAsync();
        var vehicleColumns = await GetAvailableColumnsAsync(VehicleTableName);
        var extraColumns = await GetAvailableColumnsAsync(ExtraCodeTableName);
        var vehicleProjectionColumns = vehicleColumns.ContainsKey("vmf_code")
            ? vehicleColumns
            : new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
        var vehicleJoin = vehicleColumns.ContainsKey("vmf_code")
            ? $"LEFT JOIN [dbo].[{VehicleTableName}] vm ON vm.[vmf_code] = j.[vmf_code]"
            : string.Empty;
        var extraJoin = extraColumns.ContainsKey("extra_code")
            ? $"LEFT JOIN [dbo].[{ExtraCodeTableName}] ec ON ec.[extra_code] = j.[extra_code]"
            : string.Empty;
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        var predicate = predicateFactory?.Invoke(columns);
        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(predicate))
            conditions.Add($"({predicate})");
        if (columns.ContainsKey("is_deleted"))
            conditions.Add("ISNULL(j.[is_deleted], 0) = 0");

        command.CommandText = $"""
            SELECT
                {BuildProjection(columns, vehicleProjectionColumns, extraColumns)}
            FROM [dbo].[{CurrentTableName}] j
            {vehicleJoin}
            {extraJoin}
            WHERE {string.Join(" AND ", conditions.DefaultIfEmpty("1 = 1"))}
            ORDER BY {DateExpression(columns)} DESC, {IdExpression(columns)} DESC
            """;
        configure?.Invoke(command);

        var results = new List<JobCard>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(Map(reader));
        return results;
    }

    private static void AddPageParameters(
        DbCommand command,
        IReadOnlyList<int> statusCodes,
        string searchTerm,
        int? searchId,
        int? jobCardId,
        IReadOnlyCollection<int>? allowedVmfCodes
    )
    {
        for (var index = 0; index < statusCodes.Count; index++)
            AddParameter(command, $"@statusCode{index}", DbType.Int32, statusCodes[index]);

        if (searchTerm.Length > 0)
            AddParameter(
                command,
                "@searchTerm",
                DbType.String,
                $"%{searchTerm.ToLowerInvariant()}%"
            );
        if (searchId.HasValue)
            AddParameter(command, "@searchId", DbType.Int32, searchId.Value);
        if (jobCardId.HasValue)
            AddParameter(command, "@jobCardId", DbType.Int32, jobCardId.Value);
        AddAllowedVehicleParameters(command, allowedVmfCodes, "j");
    }

    private static void AddAllowedVehicleCondition(
        ICollection<string> conditions,
        IReadOnlyCollection<int>? allowedVmfCodes,
        string alias
    )
    {
        if (allowedVmfCodes is null)
            return;
        var codes = allowedVmfCodes.Where(code => code > 0).Distinct().ToArray();
        conditions.Add(codes.Length == 0
            ? "1 = 0"
            : $"[{alias}].[vmf_code] IN ({string.Join(", ", codes.Select((_, index) => $"@allowedVmf{index}"))})");
    }

    private static void AddAllowedVehicleParameters(
        DbCommand command,
        IReadOnlyCollection<int>? allowedVmfCodes,
        string _
    )
    {
        if (allowedVmfCodes is null)
            return;
        foreach (var (code, index) in allowedVmfCodes.Where(code => code > 0).Distinct().Select((code, index) => (code, index)))
            AddParameter(command, $"@allowedVmf{index}", DbType.Int32, code);
    }

    private static void AddRepairCostParameters(
        DbCommand command,
        RepairCostReportPageQuery query,
        IReadOnlyList<int>? vmfCodes
    )
    {
        if (query.VmfCode.HasValue)
            AddParameter(command, "@vmfCode", DbType.Int32, query.VmfCode.Value);

        for (var index = 0; index < (vmfCodes?.Count ?? 0); index++)
            AddParameter(command, $"@siteVmfCode{index}", DbType.Int32, vmfCodes![index]);

        if (query.FromDate.HasValue)
            AddParameter(command, "@fromDate", DbType.DateTime2, query.FromDate.Value);
        if (query.ToDate.HasValue)
            AddParameter(command, "@toDate", DbType.DateTime2, query.ToDate.Value.AddDays(1));
    }

    private static string PriorityUnassignedPredicate(
        IReadOnlyDictionary<string, ColumnInfo> columns
    ) =>
        columns.ContainsKey("priority") && HasLegacyReviewedColumn(columns)
            ? IsBitColumn(columns, "priority")
                ? $"j.[priority] = 1 AND j.[{GetLegacyReviewedColumn(columns)}] = 1 AND j.[status_code] IN (1, 2)"
                : $"j.[priority] = 'Y' AND j.[{GetLegacyReviewedColumn(columns)}] = 'Y' AND j.[status_code] IN (1, 2)"
            : "j.[priority] = 'H' AND j.[assigned_to] IS NULL AND j.[status_code] NOT IN (5, 7)";

    private async Task ExecuteUpdateAsync(
        int jobCardId,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        IReadOnlyCollection<WriteValue> values
    )
    {
        if (values.Count == 0)
            return;
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        var idColumn = GetIdColumn(columns);
        command.CommandText =
            $"UPDATE [dbo].[{CurrentTableName}] SET {string.Join(", ", values.Select(value => $"[{value.Column}] = {value.Parameter}"))} WHERE [{idColumn}] = @jobCardId";
        AddParameters(command, values);
        AddParameter(command, "@jobCardId", DbType.Int32, jobCardId);
        if (await command.ExecuteNonQueryAsync() == 0)
            throw new KeyNotFoundException($"JobCard not found with ID: {jobCardId}");
    }

    private async Task<Dictionary<string, ColumnInfo>> GetAvailableColumnsAsync(
        string? tableName = null
    )
    {
        await using var scope = await OpenConnectionAsync();
        tableName ??= await ResolveTableNameAsync(scope.Connection);
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            "SELECT [COLUMN_NAME], [DATA_TYPE] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] = @table";
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@table", DbType.String, tableName);

        var columns = new Dictionary<string, ColumnInfo>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            columns[reader.GetString(0)] = new ColumnInfo(reader.GetString(0), reader.GetString(1));
        if (
            tableName.Equals(CurrentTableName, StringComparison.OrdinalIgnoreCase)
            && (
                !columns.ContainsKey("vmf_code")
                || !columns.ContainsKey("extra_code")
                || !columns.ContainsKey("status_code")
            )
        )
            throw new InvalidOperationException(
                "The Jobcards compatibility table is missing required workflow columns."
            );
        return columns;
    }

    private string CurrentTableName => _resolvedTableName ?? DefaultTableName;

    private async Task<string> ResolveTableNameAsync(DbConnection connection)
    {
        if (_resolvedTableName is not null)
            return _resolvedTableName;

        await using var command = connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText = """
            SELECT TOP (1) [TABLE_NAME]
            FROM [INFORMATION_SCHEMA].[TABLES]
            WHERE [TABLE_SCHEMA] = N'dbo'
              AND [TABLE_NAME] IN (N'Jobcards', N'JobCard', N'job_cards')
            ORDER BY CASE
                WHEN [TABLE_NAME] = N'Jobcards' THEN 0
                WHEN [TABLE_NAME] = N'JobCard' THEN 1
                ELSE 2
            END
            """;
        var value = await command.ExecuteScalarAsync();
        if (value is null or DBNull)
            throw new InvalidOperationException(
                "Neither the legacy dbo.Jobcards / dbo.JobCard table nor the expanded dbo.job_cards table is available."
            );

        _resolvedTableName = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)!;
        return _resolvedTableName;
    }

    private static string BuildProjection(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        IReadOnlyDictionary<string, ColumnInfo> vehicleColumns,
        IReadOnlyDictionary<string, ColumnInfo> extraColumns
    ) =>
        string.Join(
            ",\n                ",
            [
                $"{IdExpression(columns)} AS [job_card_id]",
                "j.[vmf_code] AS [vmf_code]",
                VehicleColumnExpression(vehicleColumns, "fleet_number", "varchar(50)")
                    + " AS [gg_number]",
                VehicleColumnExpression(vehicleColumns, "registration_number", "varchar(50)")
                    + " AS [registration_number]",
                "j.[extra_code] AS [extra_code]",
                ExtraDescriptionExpression(extraColumns) + " AS [extra_description]",
                "j.[status_code] AS [status_code]",
                PriorityExpression(columns) + " AS [priority]",
                AssignedToExpression(columns) + " AS [assigned_to]",
                AssignedNameExpression(columns) + " AS [assigned_to_name]",
                AssignedDateExpression(columns) + " AS [assigned_date]",
                CommentExpression(columns, "jcs_comment") + " AS [jcs_comment]",
                OptionalExpression(columns, "damages", "varchar(2000)") + " AS [damages]",
                CommentExpression(columns, "comments") + " AS [comments]",
                AuthorizerExpression(columns) + " AS [authorizer]",
                AuthorizerNameExpression(columns) + " AS [authorizer_name]",
                ReviewedExpression(columns) + " AS [reviewed]",
                CapturedByExpression(columns) + " AS [captured_by_user_code]",
                OptionalExpression(columns, "labour_cost", "decimal(18, 2)") + " AS [labour_cost]",
                OptionalExpression(columns, "parts_cost", "decimal(18, 2)") + " AS [parts_cost]",
                OptionalExpression(columns, "other_cost", "decimal(18, 2)") + " AS [other_cost]",
                OptionalExpression(columns, "total_cost", "decimal(18, 2)") + " AS [total_cost]",
                OptionalExpression(columns, "invoice_number", "varchar(200)")
                    + " AS [invoice_number]",
                OptionalExpression(columns, "invoice_date", "datetime2") + " AS [invoice_date]",
                OptionalExpression(columns, "service_provider", "varchar(200)")
                    + " AS [service_provider]",
                DateExpression(columns) + " AS [date_created]",
                UpdatedDateExpression(columns) + " AS [date_updated]",
                CapturedByExpression(columns) + " AS [created_by_user_code]",
                ModifiedByExpression(columns) + " AS [modified_by_user_code]",
            ]
        );

    private static string VehicleColumnExpression(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string sqlType
    ) => columns.ContainsKey(column) ? $"vm.[{column}]" : $"CAST(NULL AS {sqlType})";

    private static string ExtraDescriptionExpression(
        IReadOnlyDictionary<string, ColumnInfo> columns
    ) =>
        columns.ContainsKey("extra_code") && columns.ContainsKey("extra_description")
            ? "ec.[extra_description]"
            : "CAST(NULL AS varchar(255))";

    private static string IdExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        $"j.[{GetIdColumn(columns)}]";

    private static string DateExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("date_created") ? "j.[date_created]"
        : columns.ContainsKey("jcs_date") ? "TRY_CONVERT(datetime2, NULLIF(j.[jcs_date], ''), 111)"
        : columns.ContainsKey("Status_date")
            ? "TRY_CONVERT(datetime2, NULLIF(j.[Status_date], ''), 111)"
        : "CAST(NULL AS datetime2)";

    private static string UpdatedDateExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("date_updated") ? "j.[date_updated]"
        : columns.ContainsKey("DateClosed") && GetAuthorizerUpdateDateColumn(columns) is { } authorizerDate
            ? $"COALESCE(j.[DateClosed], j.[{authorizerDate}])"
        : OptionalExpression(columns, "DateClosed", "datetime2");

    private static string PriorityExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        !columns.ContainsKey("priority") ? "CAST(NULL AS varchar(1))"
        : IsBitColumn(columns, "priority") ? "CASE WHEN j.[priority] = 1 THEN 'Y' ELSE 'N' END"
        : "j.[priority]";

    private static string AssignedToExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("assigned_to") ? "j.[assigned_to]" : "CAST(NULL AS int)";

    private static string AssignedNameExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("hhandover_name") ? "j.[hhandover_name]"
        : columns.ContainsKey("HandedOverTo") ? "j.[HandedOverTo]"
        : columns.ContainsKey("assigned_to") ? "CONVERT(varchar(50), j.[assigned_to])"
        : "CAST(NULL AS varchar(50))";

    private static string AssignedDateExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("assigned_date") ? "j.[assigned_date]"
        : columns.ContainsKey("hhandover_date")
            ? "TRY_CONVERT(datetime2, NULLIF(j.[hhandover_date], ''), 111)"
        : columns.ContainsKey("HandedOverOn")
            ? "TRY_CONVERT(datetime2, NULLIF(j.[HandedOverOn], ''), 111)"
        : "CAST(NULL AS datetime2)";

    private static string CommentExpression(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string modernColumn
    ) =>
        columns.ContainsKey(modernColumn) ? $"j.[{modernColumn}]"
        : modernColumn.Equals("jcs_comment", StringComparison.OrdinalIgnoreCase)
        && columns.ContainsKey("Status_comment")
            ? "j.[Status_comment]"
        : modernColumn.Equals("comments", StringComparison.OrdinalIgnoreCase)
        && columns.ContainsKey("authorizer_jobcard_comments")
            ? "j.[authorizer_jobcard_comments]"
        : modernColumn.Equals("comments", StringComparison.OrdinalIgnoreCase)
        && columns.ContainsKey("Authoriser_comments")
            ? "j.[Authoriser_comments]"
        : "CAST(NULL AS varchar(2000))";

    private static string ReviewedExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("reviewed")
            ? "j.[reviewed]"
            : HasLegacyReviewedColumn(columns)
                ? IsBitColumn(columns, GetLegacyReviewedColumn(columns)!)
                    ? $"CASE WHEN j.[{GetLegacyReviewedColumn(columns)}] = 1 THEN 'Y' ELSE 'N' END"
                    : $"j.[{GetLegacyReviewedColumn(columns)}]"
                : "CAST(NULL AS varchar(1))";

    private static string CapturedByExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("created_by_user_code")
            ? columns.ContainsKey("captured_by")
                ? "COALESCE(j.[created_by_user_code], j.[captured_by])"
                : "j.[created_by_user_code]"
            : OptionalExpression(columns, "captured_by", "int");

    private static string ModifiedByExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("modified_by_user_code")
            ? "j.[modified_by_user_code]"
            : AuthorizerExpression(columns);

    private static string AuthorizerNameExpression(
        IReadOnlyDictionary<string, ColumnInfo> columns
    ) =>
        $"CONVERT(varchar(50), {AuthorizerExpression(columns)})";

    private static string AuthorizerExpression(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("authorizer") ? "j.[authorizer]"
        : columns.ContainsKey("Authorizer") ? "j.[Authorizer]"
        : columns.ContainsKey("Authoriser") ? "j.[Authoriser]"
        : "CAST(NULL AS int)";

    private static string OptionalExpression(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string sqlType
    ) => columns.ContainsKey(column) ? $"j.[{column}]" : $"CAST(NULL AS {sqlType})";

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
                registration_number = ReadString(reader, "registration_number"),
            },
            ExtraCodeRef = new ExtraCode
            {
                extra_code = (short)(ReadInt(reader, "extra_code") ?? 0),
                extra_description = ReadString(reader, "extra_description"),
            },
            AssignedToUser =
                assignedTo.HasValue || !string.IsNullOrWhiteSpace(assignedName)
                    ? new User { user_access_code = assignedTo ?? 0, email = assignedName }
                    : null,
            AuthorizerUser =
                authorizer.HasValue || !string.IsNullOrWhiteSpace(authorizerName)
                    ? new User { user_access_code = authorizer ?? 0, email = authorizerName }
                    : null,
        };
    }

    private async Task<ConnectionScope> OpenConnectionAsync()
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();
        return new ConnectionScope(connection, shouldClose);
    }

    private DbTransaction? CurrentTransaction =>
        _context.Database.CurrentTransaction?.GetDbTransaction();

    private static string? NormalizePriority(
        string? priority,
        IReadOnlyDictionary<string, ColumnInfo> columns
    ) =>
        columns.ContainsKey("reviewed_by_Authorizer")
            ? priority switch
            {
                "H" => "Y",
                "N" => "N",
                _ => priority,
            }
            : priority;

    private static string? NormalizeDamage(string? damages)
    {
        if (string.IsNullOrWhiteSpace(damages))
            return null;

        return damages.Trim().StartsWith("Y", StringComparison.OrdinalIgnoreCase) ? "Y" : "N";
    }

    private static string? Append(string? current, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return current;
        var combined = string.IsNullOrWhiteSpace(current)
            ? value.Trim()
            : $"{current.Trim()}\n{value.Trim()}";
        return combined.Length > 150 ? combined[..150] : combined;
    }

    private static int? UserIdOrNull(int value) => value > 0 ? value : null;

    private static void AddValue(
        ICollection<WriteValue> values,
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column,
        string parameter,
        DbType type,
        object? value,
        bool includeNull
    )
    {
        if (columns.ContainsKey(column) && (includeNull || value is not null))
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

    private static string? ReadString(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToString(reader[name]);

    private static int? ReadInt(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToInt32(reader[name]);

    private static decimal? ReadDecimal(DbDataReader reader, string name) =>
        reader.IsDBNull(reader.GetOrdinal(name)) ? null : Convert.ToDecimal(reader[name]);

    private static DateTime? ReadDate(DbDataReader reader, string name)
    {
        if (reader.IsDBNull(reader.GetOrdinal(name)))
            return null;
        return DateTime.TryParse(Convert.ToString(reader[name]), out var value) ? value : null;
    }

    private static string GetIdColumn(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("job_card_id") ? "job_card_id"
        : columns.ContainsKey("jc_code") ? "jc_code"
        : columns.ContainsKey("JobCard_code") ? "JobCard_code"
        : throw new InvalidOperationException("The Job Card table has no supported identity column.");

    private static string? GetNumberColumn(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("jc_number") ? "jc_number"
        : columns.ContainsKey("Number") ? "Number"
        : null;

    private static bool HasLegacyReviewedColumn(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        GetLegacyReviewedColumn(columns) is not null;

    private static string? GetLegacyReviewedColumn(IReadOnlyDictionary<string, ColumnInfo> columns) =>
        columns.ContainsKey("reviewed_by_Authorizer") ? "reviewed_by_Authorizer"
        : columns.ContainsKey("reviewed_by_Authoriser") ? "reviewed_by_Authoriser"
        : null;

    private static string? GetAuthorizerUpdateDateColumn(
        IReadOnlyDictionary<string, ColumnInfo> columns
    ) =>
        columns.ContainsKey("authorizer_update_date") ? "authorizer_update_date"
        : columns.ContainsKey("Authoriser_update_date") ? "Authoriser_update_date"
        : null;

    private static bool IsBitColumn(
        IReadOnlyDictionary<string, ColumnInfo> columns,
        string column
    ) => columns.TryGetValue(column, out var info)
        && string.Equals(info.DataType, "bit", StringComparison.OrdinalIgnoreCase);

    private static bool PriorityAsBoolean(string? priority) =>
        string.Equals(priority, "Y", StringComparison.OrdinalIgnoreCase)
        || string.Equals(priority, "H", StringComparison.OrdinalIgnoreCase)
        || string.Equals(priority, "true", StringComparison.OrdinalIgnoreCase);

    private sealed record ColumnInfo(string Name, string DataType = "");

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

    private sealed record ProcedureParameter(string Name, DbType Type, object? Value);

    private sealed record JobCardMutationProcedure(
        string Name,
        string CommentParameter,
        string StatusParameter
    );

    private sealed record JobCardAuthorizerProcedure(
        string Name,
        string AuthorizerParameter,
        string StatusParameter,
        bool UsesBitFlags
    );

    private sealed class ConnectionScope(DbConnection connection, bool shouldClose)
        : IAsyncDisposable
    {
        public DbConnection Connection { get; } = connection;

        public async ValueTask DisposeAsync()
        {
            if (shouldClose)
                await Connection.CloseAsync();
        }
    }
}
