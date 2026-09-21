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
/// Reads and writes lease terms against both the original client schema and
/// the expanded schema. The original FML table uses UpdatedBy/UpdatedDate,
/// Comments, Rejected, and derived vehicle dates, while the expanded table
/// adds modern audit and explicit date columns. EF materialization is avoided
/// because a missing optional column would make an otherwise valid query fail.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL identifiers are selected only from fixed allowlists after schema inspection; all values are parameterized."
)]
public sealed class LeaseContractTermsRepository : ILeaseContractTermsRepository
{
    private const string TableName = "LeaseContractTerms";
    private const string VehicleTableName = "vehicle_master";

    private static readonly string[] RequiredColumns = ["VehicleContractTermID", "vmf_Code"];

    private readonly FisDbContext _context;

    public LeaseContractTermsRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<LeaseContractTerms?> GetByIdAsync(int termId) =>
        (
            await QueryAsync(
                "[l].[VehicleContractTermID] = @termId",
                command => AddParameter(command, "@termId", DbType.Int32, termId)
            )
        ).SingleOrDefault();

    public async Task<IEnumerable<LeaseContractTerms>> GetAllAsync() => await QueryAsync();

    public async Task<LeaseContractTermsPage> GetPageAsync(LeaseContractTermsPageQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var availableColumns = await GetAvailableColumnsAsync(TableName, RequiredColumns);
        var vehicleColumns = await GetAvailableColumnsAsync(VehicleTableName);
        var filter = BuildPageFilter(query, availableColumns, vehicleColumns);
        var total = await CountAsync(availableColumns, vehicleColumns, filter);
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);
        var skip = checked((long)(page - 1) * pageSize);
        var items = await QueryAsync(
            filter.Predicate,
            command => AddFilterParameters(command, filter),
            availableColumns,
            vehicleColumns,
            filter.IncludeVehicleJoin,
            skip,
            pageSize
        );

        return new LeaseContractTermsPage(items, page, pageSize, total);
    }

    public async Task<LeaseContractTerms?> GetByVehicleAsync(int vmfCode) =>
        (
            await QueryAsync(
                "[l].[vmf_Code] = @vmfCode",
                command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode)
            )
        ).FirstOrDefault();

    public async Task<string?> GetCapturedByUsernameAsync(int vmfCode)
    {
        var availableColumns = await GetAvailableColumnsAsync(TableName, RequiredColumns);
        if (!availableColumns.Contains("CreatedBy"))
            return null;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                "SELECT TOP (1) NULLIF(LTRIM(RTRIM(CONVERT(varchar(200), [CreatedBy]))), '') FROM [dbo].[LeaseContractTerms] WHERE [vmf_Code] = @vmfCode";
            AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
            var value = await command.ExecuteScalarAsync();
            if (value is null or DBNull)
                return null;

            var capturedBy = Convert.ToString(value)?.Trim();
            if (!int.TryParse(capturedBy, out var userAccessCode) || userAccessCode <= 0)
                return capturedBy;

            // Do not return the numeric ID as if it were a username. The
            // controller treats an unresolved capturer as a hard stop so a
            // self-approval cannot slip through during a partial restore.
            return await ResolveLegacyUserNameAsync(connection, userAccessCode);
        }
        catch (DbException)
        {
            // CreatedBy is an optional expanded/legacy column. If the legacy
            // lookup tables are unavailable, return no identity so the caller
            // can fail closed on an unresolved self-approval.
            return null;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    public async Task<IEnumerable<LeaseContractTerms>> GetActiveTermsAsync() =>
        await QueryAsync("[l].[AuthorityStatus] = 2");

    public async Task<LeaseContractTerms> CreateAsync(LeaseContractTerms terms, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ValidateVehicleCode(terms);

        var availableColumns = await GetAvailableColumnsAsync(TableName, RequiredColumns);
        var now = DateTime.UtcNow;
        var values = BuildBaseWriteValues(terms, availableColumns).ToList();

        AddValue(
            values,
            availableColumns,
            "CreatedBy",
            "@createdBy",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : terms.CreatedBy
        );
        AddValue(values, availableColumns, "CreatedDate", "@createdDate", DbType.DateTime2, now);
        AddValue(values, availableColumns, "date_created", "@dateCreated", DbType.DateTime2, now);
        AddValue(
            values,
            availableColumns,
            "created_by_user_code",
            "@createdByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : terms.created_by_user_code
        );
        AddValue(values, availableColumns, "is_deleted", "@isDeleted", DbType.Boolean, false);
        AddWorkflowWriteValues(values, availableColumns, terms, currentUserId, now);

        terms.VehicleContractTermID = await ExecuteInsertAsync(values);
        terms.CreatedBy = currentUserId > 0 ? currentUserId : terms.CreatedBy;
        terms.CreatedDate = now;
        terms.date_created = now;
        terms.created_by_user_code = currentUserId > 0 ? currentUserId : terms.created_by_user_code;
        terms.is_deleted = false;

        return await GetByIdAsync(terms.VehicleContractTermID) ?? terms;
    }

    public async Task<LeaseContractTerms> UpdateAsync(LeaseContractTerms terms, int currentUserId)
    {
        ArgumentNullException.ThrowIfNull(terms);
        if (terms.VehicleContractTermID <= 0)
        {
            throw new ArgumentException("A valid lease term ID is required.", nameof(terms));
        }

        _ =
            await GetByIdAsync(terms.VehicleContractTermID)
            ?? throw new InvalidOperationException(
                $"LeaseContractTerms with VehicleContractTermID {terms.VehicleContractTermID} not found"
            );

        var availableColumns = await GetAvailableColumnsAsync(TableName, RequiredColumns);
        var now = DateTime.UtcNow;
        var values = BuildBaseWriteValues(terms, availableColumns).ToList();
        AddWorkflowWriteValues(values, availableColumns, terms, currentUserId, now);
        AddValue(values, availableColumns, "date_updated", "@dateUpdated", DbType.DateTime2, now);
        AddValue(
            values,
            availableColumns,
            "modified_by_user_code",
            "@modifiedByUserCode",
            DbType.Int32,
            currentUserId > 0 ? currentUserId : terms.modified_by_user_code
        );

        var modifiedByColumn = FirstAvailable(availableColumns, "ModifiedBy", "UpdatedBy");
        if (modifiedByColumn is not null)
        {
            AddValue(
                values,
                availableColumns,
                modifiedByColumn,
                "@modifiedBy",
                DbType.Int32,
                currentUserId > 0 ? currentUserId : terms.ModifiedBy
            );
        }

        var modifiedDateColumn = FirstAvailable(availableColumns, "ModifiedDate", "UpdatedDate");
        if (modifiedDateColumn is not null)
        {
            AddValue(
                values,
                availableColumns,
                modifiedDateColumn,
                "@modifiedDate",
                DbType.DateTime2,
                now
            );
        }

        await ExecuteUpdateAsync(terms.VehicleContractTermID, values, availableColumns);
        return await GetByIdAsync(terms.VehicleContractTermID) ?? terms;
    }

    public async Task<LeaseContractTerms> CaptureOrResubmitAsync(
        LeaseContractTerms terms,
        string username,
        int currentUserId = 0
    )
    {
        ArgumentNullException.ThrowIfNull(terms);
        ValidateVehicleCode(terms);
        var comment = FirstNonEmpty(terms.authority_comment, terms.lease_notes, terms.Comments) ?? string.Empty;

        var actualParameters = await GetLegacyProcedureParametersAsync("DEV_UPD_LeaseContractTerms");
        if (actualParameters is null)
        {
            throw new NotSupportedException(
                "The legacy lease-contract-term capture procedure is unavailable; capture cannot be approximated."
            );
        }

        var withAgreedKilos = new[]
        {
            "@vmf_Code", "@AgreedTerms", "@AgreedOverallKilo", "@AgreedKilos",
            "@AppliedInterest", "@FixedMonthlyAmount", "@ExcessKilos", "@RelieveVehicle",
            "@site_code", "@Comments", "@CreatedBy", "@UpdatedBy", "@AuthorisedBy",
        };
        var withoutAgreedKilos = withAgreedKilos
            .Where(parameter => !string.Equals(parameter, "@AgreedKilos", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var includesAgreedKilos = actualParameters.SequenceEqual(
            withAgreedKilos,
            StringComparer.OrdinalIgnoreCase
        );
        if (!includesAgreedKilos && !actualParameters.SequenceEqual(
                withoutAgreedKilos,
                StringComparer.OrdinalIgnoreCase
            ))
        {
            throw new InvalidOperationException(
                "The deployed legacy lease-contract-term capture procedure does not match its archived parameter contract. No direct-DML fallback was run."
            );
        }

        if (!includesAgreedKilos && terms.AgreedKilos.HasValue)
        {
            throw new NotSupportedException(
                "The deployed legacy lease-contract-term procedure has no AgreedKilos parameter; the supplied value was not silently dropped."
            );
        }

        var existing = await GetByVehicleAsync(terms.vmf_Code);
        var actorCode = await ResolveLegacyUserCodeAsync(username, currentUserId);
        if (actorCode <= 0)
        {
            throw new UnauthorizedAccessException(
                "The authenticated user could not be resolved to a legacy user access code for the lease workflow."
            );
        }

        var parameters = new List<ProcedureParameter>
        {
            new("@vmf_Code", DbType.Int32, terms.vmf_Code),
            new("@AgreedTerms", DbType.Int32, terms.AgreedTerms),
            new("@AgreedOverallKilo", DbType.Int32, terms.AgreedOverallKilo),
        };
        if (includesAgreedKilos)
            parameters.Add(new ProcedureParameter("@AgreedKilos", DbType.Int64, terms.AgreedKilos));
        parameters.AddRange(
        [
            new("@AppliedInterest", DbType.Decimal, terms.AppliedInterest),
            new("@FixedMonthlyAmount", DbType.Decimal, terms.FixedMonthlyAmount),
            new("@ExcessKilos", DbType.Decimal, terms.ExcessKilosTarrif),
            new("@RelieveVehicle", DbType.Boolean, terms.RelieveVehicle ?? false),
            new("@site_code", DbType.Int16, terms.lease_site_code),
            new("@Comments", DbType.String, comment),
            // The archived procedure stores IDs, not display names. Its
            // AuthorisedBy field is also populated during capture, so pass
            // the authenticated actor consistently for all three audit slots.
            new("@CreatedBy", DbType.Int32, existing?.CreatedBy ?? actorCode),
            new("@UpdatedBy", DbType.Int32, actorCode),
            new("@AuthorisedBy", DbType.Int32, actorCode),
        ]);

        await ExecuteLegacyProcedureAsync("DEV_UPD_LeaseContractTerms", parameters.ToArray());

        var updated = await GetByVehicleAsync(terms.vmf_Code)
            ?? throw new InvalidOperationException(
                "The legacy lease-contract-term capture procedure completed without a readable term."
            );
        await InsertLegacyCommentAsync(updated, comment, username);
        return await GetByIdAsync(updated.VehicleContractTermID) ?? updated;
    }

    public async Task<LeaseContractTerms> AuthorizeAsync(
        LeaseContractTerms terms,
        string comment,
        string username,
        int currentUserId = 0
    )
    {
        ArgumentNullException.ThrowIfNull(terms);
        ValidateVehicleCode(terms);
        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("A comment is required to authorise lease contract terms.", nameof(comment));

        if (!await IsLegacyProcedureAvailableAsync(
                "DEV_UPD_LeaseContractTermsAuthorisation",
                "@vmf_Code",
                "@Comments",
                "@AuthorisedBy"
            ))
        {
            throw new NotSupportedException(
                "The legacy lease-contract-term authorisation procedure is unavailable; authorisation cannot be approximated."
            );
        }

        var actorCode = await ResolveLegacyUserCodeAsync(username, currentUserId);
        if (actorCode <= 0)
            throw new UnauthorizedAccessException("The authenticated user could not be resolved to a legacy user access code.");

        await ExecuteLegacyProcedureAsync(
            "DEV_UPD_LeaseContractTermsAuthorisation",
            new ProcedureParameter("@vmf_Code", DbType.Int32, terms.vmf_Code),
            new ProcedureParameter("@Comments", DbType.String, comment.Trim()),
            new ProcedureParameter("@AuthorisedBy", DbType.Int32, actorCode)
        );
        var updated =
            await GetByVehicleAsync(terms.vmf_Code)
            ?? throw new InvalidOperationException(
                "The legacy lease-contract-term authorisation procedure completed without a readable term."
            );
        await InsertLegacyCommentAsync(updated, comment.Trim(), username);
        return await GetByIdAsync(updated.VehicleContractTermID) ?? updated;
    }

    public async Task<LeaseContractTerms> RejectAsync(
        LeaseContractTerms terms,
        string comment,
        string username,
        int currentUserId = 0
    )
    {
        ArgumentNullException.ThrowIfNull(terms);
        ValidateVehicleCode(terms);
        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("A comment is required to reject lease contract terms.", nameof(comment));

        if (!await IsLegacyProcedureAvailableAsync(
                "DEV_UPD_LeaseContractTermsRejection",
                "@vmf_code",
                "@Comments",
                "@UpdatedBy",
                "@Rejected"
            ))
        {
            throw new NotSupportedException(
                "The legacy lease-contract-term rejection procedure is unavailable; rejection cannot be approximated."
            );
        }

        var actorCode = await ResolveLegacyUserCodeAsync(username, currentUserId);
        if (actorCode <= 0)
            throw new UnauthorizedAccessException("The authenticated user could not be resolved to a legacy user access code.");

        await ExecuteLegacyProcedureAsync(
            "DEV_UPD_LeaseContractTermsRejection",
            new ProcedureParameter("@vmf_code", DbType.Int32, terms.vmf_Code),
            new ProcedureParameter("@Comments", DbType.String, comment.Trim()),
            new ProcedureParameter("@UpdatedBy", DbType.Int32, actorCode),
            new ProcedureParameter("@Rejected", DbType.Boolean, true)
        );
        var updated =
            await GetByVehicleAsync(terms.vmf_Code)
            ?? throw new InvalidOperationException(
                "The legacy lease-contract-term rejection procedure completed without a readable term."
            );
        await InsertLegacyCommentAsync(updated, comment.Trim(), username);
        return await GetByIdAsync(updated.VehicleContractTermID) ?? updated;
    }

    public async Task<LeaseContractTerms> RecallAsync(
        LeaseContractTerms terms,
        string username,
        int currentUserId = 0
    )
    {
        ArgumentNullException.ThrowIfNull(terms);
        ValidateVehicleCode(terms);
        if (string.IsNullOrWhiteSpace(username))
            throw new UnauthorizedAccessException(
                "The authenticated user does not include the legacy username required by the FML recall workflow."
            );
        _ = currentUserId;

        // VehicleLease.aspx.vb Getdata2 treats authorised terms as blocked and
        // already-recalled Rejected values as a no-op before any write.
        if (terms.AuthorityStatus == 2)
            throw new InvalidOperationException(
                "The vehicle has already been authorised. Kindly inform the relevant authorisor about the recalling of the vehicle."
            );

        var rejected = terms.Rejected ?? 0;
        if (rejected is 0 or 1 or 2)
            return await GetByIdAsync(terms.VehicleContractTermID) ?? terms;

        var registrationNumber = await FindVehicleRegistrationNumberAsync(terms.vmf_Code);
        if (string.IsNullOrWhiteSpace(registrationNumber))
            throw new InvalidOperationException(
                "The selected lease vehicle has no registration number required by the legacy recall procedure."
            );

        var authorityStatusAvailable = await IsLegacyProcedureAvailableAsync(
            "DEV_UPD_LeaseContractTermsAuthorityStatus",
            "@GG_Number",
            "@AuthorityStatus",
            "@Rejected",
            "@UpdatedBy"
        );
        if (authorityStatusAvailable)
        {
            // Getdata2 sets AuthorityStatus=0 and Rejected=2, then writes the
            // capturer recall comment. btnRecall only calls
            // DEV_UPD_LeaseContractTermsRecall when Rejected is still 3 after
            // that write, which this successful path never leaves in place.
            await ExecuteLegacyProcedureAsync(
                "DEV_UPD_LeaseContractTermsAuthorityStatus",
                new ProcedureParameter("@GG_Number", DbType.String, registrationNumber),
                new ProcedureParameter("@AuthorityStatus", DbType.Int32, 0),
                new ProcedureParameter("@Rejected", DbType.Int32, 2),
                new ProcedureParameter("@UpdatedBy", DbType.String, username.Trim())
            );

            var updated =
                await GetByIdAsync(terms.VehicleContractTermID)
                ?? throw new InvalidOperationException(
                    "The legacy lease-contract-term authority-status procedure completed without a readable term."
                );
            var comment =
                $"Recalled by the Capturer '{username.Trim().ToUpperInvariant()}' in order to review the tariff information for this vehicle.";
            await InsertLegacyCommentAsync(updated, comment, username.Trim());
            return await GetByIdAsync(updated.VehicleContractTermID) ?? updated;
        }

        if (
            rejected == 3
            && await IsLegacyProcedureAvailableAsync("DEV_UPD_LeaseContractTermsRecall", "@ggnum")
        )
        {
            // Labelled fallback only while DEV_UPD_LeaseContractTermsAuthorityStatus
            // is absent: VehicleLease.aspx.vb btnRecall still calls the recall
            // procedure for pending Rejected=3 terms.
            await ExecuteLegacyProcedureAsync(
                "DEV_UPD_LeaseContractTermsRecall",
                new ProcedureParameter("@ggnum", DbType.String, registrationNumber)
            );
            return await GetByIdAsync(terms.VehicleContractTermID)
                ?? throw new InvalidOperationException(
                    "The legacy lease-contract-term recall procedure removed the selected term."
                );
        }

        throw new NotSupportedException(
            "The legacy lease-contract-term authority-status procedure is unavailable; recall cannot be approximated."
        );
    }

    public async Task DeleteAsync(int termId, int currentUserId)
    {
        var availableColumns = await GetAvailableColumnsAsync(TableName, RequiredColumns);
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
                AddAssignment(
                    assignments,
                    command,
                    availableColumns,
                    "date_updated",
                    "@dateUpdated",
                    DbType.DateTime2,
                    DateTime.UtcNow
                );
                AddAssignment(
                    assignments,
                    command,
                    availableColumns,
                    "modified_by_user_code",
                    "@modifiedByUserCode",
                    DbType.Int32,
                    currentUserId > 0 ? currentUserId : null
                );
                command.CommandText = $"""
                    UPDATE [dbo].[{TableName}]
                    SET {string.Join(", ", assignments)}
                    WHERE [VehicleContractTermID] = @termId
                    AND ISNULL([is_deleted], 0) = 0
                    """;
            }
            else
            {
                command.CommandText = $"""
                    DELETE FROM [dbo].[{TableName}]
                    WHERE [VehicleContractTermID] = @termId
                    """;
            }

            AddParameter(command, "@termId", DbType.Int32, termId);
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

    private async Task InsertLegacyCommentAsync(
        LeaseContractTerms terms,
        string comment,
        string username
    )
    {
        var expectedParameters = new[]
        {
            "@comment",
            "@CommentDate",
            "@user_name",
            "@vmf_code",
            "@vehicle_contract_termID",
            "@Status",
            "@CommentCode",
        };
        // The archived SQL names this procedure DEV_INS_Comments. A later
        // application build used the more descriptive alias; accept that
        // alias only when it is actually deployed, while preferring the
        // verified legacy object.
        var procedureName = await ResolveCommentProcedureAsync(expectedParameters);
        if (procedureName is null)
        {
            throw new NotSupportedException(
                "The legacy lease-contract-term comment procedure DEV_INS_Comments is unavailable; the workflow action cannot be completed without its audit comment."
            );
        }

        await ExecuteLegacyProcedureAsync(
            procedureName,
            new ProcedureParameter("@comment", DbType.String, comment),
            new ProcedureParameter("@CommentDate", DbType.DateTime, DateTime.Now),
            new ProcedureParameter("@user_name", DbType.String, username),
            new ProcedureParameter("@vmf_code", DbType.Int32, terms.vmf_Code),
            new ProcedureParameter(
                "@vehicle_contract_termID",
                DbType.Int32,
                terms.VehicleContractTermID
            ),
            new ProcedureParameter("@Status", DbType.Int32, terms.Rejected ?? 0),
            new ProcedureParameter("@CommentCode", DbType.Int32, 0, ParameterDirection.Output)
        );
    }

    private async Task<string?> ResolveCommentProcedureAsync(
        IReadOnlyList<string> expectedParameters
    )
    {
        foreach (var procedureName in new[] { "DEV_INS_Comments", "DEV_INS_LeaseContractTermsComments" })
        {
            var actualParameters = await GetLegacyProcedureParametersAsync(procedureName);
            if (actualParameters is null)
                continue;
            if (!actualParameters.SequenceEqual(expectedParameters, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"The deployed legacy procedure {procedureName} does not match the archived comment parameter contract. No direct-DML fallback was run."
                );
            }

            return procedureName;
        }

        return null;
    }

    private async Task<string?> FindVehicleRegistrationNumberAsync(int vmfCode)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText =
                "SELECT TOP (1) NULLIF(LTRIM(RTRIM([registration_number])), '') FROM [dbo].[vehicle_master] WHERE [vmf_code] = @vmfCode";
            AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
            var value = await command.ExecuteScalarAsync();
            return value is null or DBNull ? null : Convert.ToString(value)?.Trim();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<int> ResolveLegacyUserCodeAsync(string username, int currentUserId)
    {
        if (currentUserId > 0)
            return currentUserId;
        if (int.TryParse(username, out var numericUserCode) && numericUserCode > 0)
            return numericUserCode;
        if (string.IsNullOrWhiteSpace(username))
            return 0;

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT TOP (1) [user_access_code]
                FROM [dbo].[user_access_old1]
                WHERE LOWER(LTRIM(RTRIM(COALESCE([name], '')))) = LOWER(LTRIM(RTRIM(@username)))
                   OR LOWER(LTRIM(RTRIM(COALESCE([E_Mail], '')))) = LOWER(LTRIM(RTRIM(@username)))
                ORDER BY [user_access_code]
                """;
            AddParameter(command, "@username", DbType.String, username.Trim());
            var value = await command.ExecuteScalarAsync();
            return value is null or DBNull ? 0 : Convert.ToInt32(value);
        }
        catch (DbException)
        {
            return 0;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<string?> ResolveLegacyUserNameAsync(
        DbConnection connection,
        int userAccessCode
    )
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT TOP (1)
                    COALESCE(
                        NULLIF(LTRIM(RTRIM([name])), ''),
                        NULLIF(LTRIM(RTRIM([E_Mail])), '')
                    )
                FROM [dbo].[user_access_old1]
                WHERE [user_access_code] = @userAccessCode
                """;
            AddParameter(command, "@userAccessCode", DbType.Int32, userAccessCode);
            var value = await command.ExecuteScalarAsync();
            return value is null or DBNull ? null : Convert.ToString(value)?.Trim();
        }
        catch (DbException)
        {
            return null;
        }
    }

    private async Task<bool> IsLegacyProcedureAvailableAsync(
        string procedureName,
        params string[] expectedParameters
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
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
                throw new InvalidOperationException(
                    $"The deployed legacy procedure {procedureName} does not match the verified parameter contract. No direct-DML fallback was run."
                );
            return true;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<IReadOnlyList<string>?> GetLegacyProcedureParametersAsync(
        string procedureName
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandText = """
                SELECT [parameterObject].[name]
                FROM [sys].[procedures] AS [procedureObject]
                INNER JOIN [sys].[schemas] AS [schemaObject]
                    ON [schemaObject].[schema_id] = [procedureObject].[schema_id]
                LEFT JOIN [sys].[parameters] AS [parameterObject]
                    ON [parameterObject].[object_id] = [procedureObject].[object_id]
                   AND [parameterObject].[parameter_id] > 0
                WHERE [schemaObject].[name] = N'dbo'
                  AND [procedureObject].[name] = @procedureName
                ORDER BY [parameterObject].[parameter_id]
                """;
            AddParameter(command, "@procedureName", DbType.String, procedureName);

            var found = false;
            var parameters = new List<string>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                found = true;
                if (!reader.IsDBNull(0))
                    parameters.Add(reader.GetString(0));
            }

            return found ? parameters : null;
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task ExecuteLegacyProcedureAsync(
        string procedureName,
        params ProcedureParameter[] parameters
    )
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
            await connection.OpenAsync();

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction();
            command.CommandType = CommandType.StoredProcedure;
            command.CommandText = procedureName;
            command.CommandTimeout = 0;
            foreach (var item in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = item.Name;
                parameter.DbType = item.Type;
                parameter.Direction = item.Direction;
                parameter.Value = item.Value ?? DBNull.Value;
                command.Parameters.Add(parameter);
            }
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }
    }

    private async Task<List<LeaseContractTerms>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null,
        IReadOnlySet<string>? availableColumnsOverride = null,
        IReadOnlySet<string>? vehicleColumnsOverride = null,
        bool includeVehicleJoin = false,
        long? skip = null,
        int? take = null
    )
    {
        var availableColumns =
            availableColumnsOverride ?? await GetAvailableColumnsAsync(TableName, RequiredColumns);
        var vehicleColumns =
            vehicleColumnsOverride ?? await GetAvailableColumnsAsync(VehicleTableName);
        var vehicleDateFallbackAvailable =
            vehicleColumns.Contains("vmf_code") && vehicleColumns.Contains("take_on_date");
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
            var projection = new[]
            {
                "[l].[VehicleContractTermID] AS [VehicleContractTermID]",
                "[l].[vmf_Code] AS [vmf_Code]",
                GetProjection(availableColumns, "AgreedTerms", "AgreedTerms", "int"),
                GetProjection(availableColumns, "AgreedKilos", "AgreedKilos", "bigint"),
                GetProjection(
                    availableColumns,
                    "AppliedInterest",
                    "AppliedInterest",
                    "decimal(18,2)"
                ),
                GetProjection(
                    availableColumns,
                    "FixedMonthlyAmount",
                    "FixedMonthlyAmount",
                    "decimal(18,2)"
                ),
                GetAuthorityStatusProjection(availableColumns),
                GetLegacyUserIdProjection(availableColumns, "CreatedBy", "CreatedBy"),
                GetLegacyUsernameProjection(
                    availableColumns,
                    "CreatedByUsername",
                    "CreatedBy"
                ),
                GetDateProjection(
                    availableColumns,
                    "CreatedDate",
                    "CreatedDate",
                    "CreatedDate",
                    "date_created"
                ),
                GetLegacyUserIdProjection(
                    availableColumns,
                    "ModifiedBy",
                    "ModifiedBy",
                    "UpdatedBy"
                ),
                GetLegacyUsernameProjection(
                    availableColumns,
                    "UpdatedByUsername",
                    "ModifiedBy",
                    "UpdatedBy"
                ),
                GetPreferredDateProjection(
                    availableColumns,
                    "ModifiedDate",
                    ["ModifiedDate", "UpdatedDate"]
                ),
                GetEffectiveStartProjection(availableColumns, vehicleDateFallbackAvailable),
                GetEffectiveEndProjection(availableColumns, vehicleDateFallbackAvailable),
                GetDateProjection(availableColumns, "date_created", "date_created", "CreatedDate"),
                GetPreferredDateProjection(
                    availableColumns,
                    "date_updated",
                    ["date_updated", "ModifiedDate", "UpdatedDate"]
                ),
                GetLegacyUserIdProjection(
                    availableColumns,
                    "created_by_user_code",
                    "created_by_user_code",
                    "CreatedBy"
                ),
                GetLegacyUserIdProjection(
                    availableColumns,
                    "modified_by_user_code",
                    "modified_by_user_code",
                    "ModifiedBy",
                    "UpdatedBy"
                ),
                GetOptionalProjection(availableColumns, "is_deleted", "bit", "0"),
                GetProjection(availableColumns, "AgreedOverallKilo", "AgreedOverallKilo", "int"),
                GetProjection(
                    availableColumns,
                    "ExcessKilosTarrif",
                    "ExcessKilosTarrif",
                    "decimal(18,2)"
                ),
                GetProjection(availableColumns, "RelieveVehicle", "RelieveVehicle", "bit"),
                GetPreferredProjection(
                    availableColumns,
                    "lease_site_code",
                    ["lease_site_code", "site_code"],
                    "smallint"
                ),
                GetProjection(availableColumns, "Comments", "Comments", "varchar(250)"),
                GetProjection(availableColumns, "Rejected", "Rejected", "int"),
                GetLegacyUserIdProjection(availableColumns, "AuthorisedBy", "AuthorisedBy"),
                GetLegacyUsernameProjection(
                    availableColumns,
                    "AuthorisedByUsername",
                    "AuthorisedBy"
                ),
                GetProjection(availableColumns, "AuthorisedDate", "AuthorisedDate", "datetime2"),
                GetPreferredProjection(
                    availableColumns,
                    "authority_comment",
                    ["authority_comment", "Comments"],
                    "varchar(250)"
                ),
                GetPreferredProjection(
                    availableColumns,
                    "lease_notes",
                    ["lease_notes", "Comments"],
                    "varchar(250)"
                ),
                GetRejectionReasonProjection(availableColumns),
                GetLeaseStatusProjection(availableColumns),
            };
            var joins = GetVehicleJoinClause(
                vehicleColumns,
                includeVehicleJoin,
                vehicleDateFallbackAvailable
            );
            var where = BuildWhereClause(availableColumns, predicate);
            var pagination =
                skip is not null && take is not null
                    ? " OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY"
                    : string.Empty;

            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{TableName}] AS [l]
                {joins}
                WHERE {where}
                ORDER BY [l].[VehicleContractTermID] DESC
                {pagination}
                """;
            configure?.Invoke(command);
            if (skip is not null && take is not null)
            {
                AddParameter(command, "@offset", DbType.Int64, skip.Value);
                AddParameter(command, "@pageSize", DbType.Int32, take.Value);
            }

            var results = new List<LeaseContractTerms>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(MapLeaseTerms(reader));
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

    private async Task<int> CountAsync(
        IReadOnlySet<string> availableColumns,
        IReadOnlySet<string> vehicleColumns,
        LeaseContractTermsPageFilter filter
    )
    {
        var vehicleDateFallbackAvailable =
            vehicleColumns.Contains("vmf_code") && vehicleColumns.Contains("take_on_date");
        var joins = GetVehicleJoinClause(
            vehicleColumns,
            filter.IncludeVehicleJoin,
            vehicleDateFallbackAvailable
        );
        var where = BuildWhereClause(availableColumns, filter.Predicate);
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
                SELECT COUNT_BIG(1)
                FROM [dbo].[{TableName}] AS [l]
                {joins}
                WHERE {where}
                """;
            AddFilterParameters(command, filter);
            var value = await command.ExecuteScalarAsync();
            return value is null || value == DBNull.Value
                ? 0
                : checked(Convert.ToInt32(Convert.ToInt64(value)));
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static LeaseContractTermsPageFilter BuildPageFilter(
        LeaseContractTermsPageQuery query,
        IReadOnlySet<string> availableColumns,
        IReadOnlySet<string> vehicleColumns
    )
    {
        var predicates = new List<string>();
        var allowedSiteCodes = query.AllowedSiteCodes?.Where(code => code > 0).Distinct().ToArray();
        var scopeNeedsVehicleJoin = false;
        if (allowedSiteCodes is not null)
        {
            if (allowedSiteCodes.Length == 0)
            {
                predicates.Add("1 = 0");
            }
            else
            {
                var scopeSiteParameters = allowedSiteCodes
                    .Select((_, index) => $"@scopeSite{index}")
                    .ToArray();
                var scopePredicates = new List<string>();
                foreach (var column in new[] { "lease_site_code", "site_code" }
                    .Where(availableColumns.Contains))
                {
                    scopePredicates.Add(
                        $"[l].[{column}] IN ({string.Join(", ", scopeSiteParameters)})"
                    );
                }

                foreach (var column in new[] { "veh_site_code", "initial_site_code", "default_site" }
                    .Where(vehicleColumns.Contains))
                {
                    scopeNeedsVehicleJoin = true;
                    scopePredicates.Add(
                        $"[v].[{column}] IN ({string.Join(", ", scopeSiteParameters)})"
                    );
                }

                if (vehicleColumns.Contains("vmf_code"))
                {
                    scopeNeedsVehicleJoin = true;
                    scopePredicates.Add(
                        $"EXISTS (SELECT 1 FROM [dbo].[contract] AS [scope_contract] WHERE [scope_contract].[vmf_code] = [l].[vmf_Code] AND [scope_contract].[still_current] = 'Y' AND [scope_contract].[site_code] IN ({string.Join(", ", scopeSiteParameters)}))"
                    );
                }

                if (query.CurrentUserId is > 0)
                {
                    foreach (var column in new[] { "CreatedBy", "created_by_user_code" }
                        .Where(availableColumns.Contains))
                    {
                        scopePredicates.Add($"TRY_CONVERT(int, [l].[{column}]) = @scopeUser");
                    }
                }

                predicates.Add(scopePredicates.Count == 0
                    ? "1 = 0"
                    : $"({string.Join(" OR ", scopePredicates)})");
            }
        }
        var statusPredicate = GetStatusPredicate(query.Status, availableColumns);
        if (statusPredicate is not null)
        {
            predicates.Add(statusPredicate);
        }

        var search = query.Search?.Trim();
        if (string.IsNullOrWhiteSpace(search))
        {
            return new LeaseContractTermsPageFilter(
                predicates.Count == 0 ? null : string.Join(" AND ", predicates),
                null,
                scopeNeedsVehicleJoin,
                allowedSiteCodes,
                query.CurrentUserId
            );
        }

        var searchColumn = string.Equals(query.Mode, "GP", StringComparison.OrdinalIgnoreCase)
            ? "registration_number"
            : "fleet_number";
        if (
            !vehicleColumns.Contains("vmf_code")
            || !vehicleColumns.Contains("vehicle_status_code")
            || !vehicleColumns.Contains(searchColumn)
        )
        {
            predicates.Add("1 = 0");
            return new LeaseContractTermsPageFilter(
                string.Join(" AND ", predicates),
                null,
                scopeNeedsVehicleJoin,
                allowedSiteCodes,
                query.CurrentUserId
            );
        }

        predicates.Add(
            $"[v].[vehicle_status_code] > 0 AND {GetVehicleActiveFilter(vehicleColumns)} AND LOWER(COALESCE([v].[{searchColumn}], '')) LIKE @vehicleSearch"
        );
        return new LeaseContractTermsPageFilter(
            string.Join(" AND ", predicates),
            $"%{search.ToLowerInvariant()}%",
            true,
            allowedSiteCodes,
            query.CurrentUserId
        );
    }

    private static string? GetStatusPredicate(string? status, IReadOnlySet<string> availableColumns)
    {
        var statusExpression = GetEffectiveAuthorityStatusExpression(availableColumns);
        if (statusExpression is null)
            return "1 = 0";

        return status switch
        {
            "pending" => $"{statusExpression} = 1",
            "approved" => $"{statusExpression} = 2",
            // The live DEV_UPD_LeaseContractTermsRejection procedure stores
            // AuthorityStatus = 0. Some expanded databases retained 4.
            "rejected" => $"{statusExpression} IN (0, 4)",
            _ => null,
        };
    }

    private static string GetVehicleJoinClause(
        IReadOnlySet<string> vehicleColumns,
        bool includeVehicleJoin,
        bool vehicleDateFallbackAvailable
    ) =>
        vehicleDateFallbackAvailable || (includeVehicleJoin && vehicleColumns.Contains("vmf_code"))
            ? "LEFT JOIN [dbo].[vehicle_master] AS [v] ON [v].[vmf_code] = [l].[vmf_Code]"
            : string.Empty;

    private static string GetVehicleActiveFilter(IReadOnlySet<string> columns) =>
        columns.Contains("is_deleted") ? "ISNULL([v].[is_deleted], 0) = 0" : "1 = 1";

    private static string BuildWhereClause(
        IReadOnlySet<string> availableColumns,
        string? predicate
    ) =>
        string.IsNullOrWhiteSpace(predicate)
            ? GetActiveFilter(availableColumns)
            : $"{predicate} AND {GetActiveFilter(availableColumns)}";

    private static void AddFilterParameters(DbCommand command, LeaseContractTermsPageFilter filter)
    {
        if (filter.AllowedSiteCodes is not null)
        {
            for (var index = 0; index < filter.AllowedSiteCodes.Count; index++)
            {
                AddParameter(command, $"@scopeSite{index}", DbType.Int16, filter.AllowedSiteCodes[index]);
            }
            if (filter.CurrentUserId is > 0)
                AddParameter(command, "@scopeUser", DbType.Int32, filter.CurrentUserId.Value);
        }
        if (filter.SearchTerm is not null)
        {
            AddParameter(command, "@vehicleSearch", DbType.String, filter.SearchTerm);
        }
    }

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
            command.CommandText = $"""
                INSERT INTO [dbo].[{TableName}] ({string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}]")
                )})
                OUTPUT INSERTED.[VehicleContractTermID]
                VALUES ({string.Join(", ", values.Select(value => value.Parameter))})
                """;
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

    private async Task ExecuteUpdateAsync(
        int termId,
        IReadOnlyList<WriteValue> values,
        IReadOnlySet<string> availableColumns
    )
    {
        if (values.Count == 0)
        {
            return;
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
                SET {string.Join(
                    ", ",
                    values.Select(value => $"[{value.Column}] = {value.Parameter}")
                )}
                WHERE [VehicleContractTermID] = @termId
                AND {GetActiveFilter(availableColumns)}
                """;
            AddParameters(command, values);
            AddParameter(command, "@termId", DbType.Int32, termId);
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

    private async Task<HashSet<string>> GetAvailableColumnsAsync(
        string tableName,
        IReadOnlyCollection<string>? requiredColumns = null
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

            var missingColumns = (requiredColumns ?? Array.Empty<string>())
                .Where(column => !columns.Contains(column))
                .ToArray();
            if (missingColumns.Length > 0)
            {
                throw new InvalidOperationException(
                    $"The required FML compatibility columns are not available on {tableName}: {string.Join(", ", missingColumns)}"
                );
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

    private static List<WriteValue> BuildBaseWriteValues(
        LeaseContractTerms terms,
        IReadOnlySet<string> availableColumns
    )
    {
        var values = new List<WriteValue>();
        AddValue(values, availableColumns, "vmf_Code", "@vmfCode", DbType.Int32, terms.vmf_Code);
        AddValue(
            values,
            availableColumns,
            "AgreedTerms",
            "@agreedTerms",
            DbType.Int32,
            terms.AgreedTerms
        );
        AddValue(
            values,
            availableColumns,
            "AgreedKilos",
            "@agreedKilos",
            DbType.Int64,
            terms.AgreedKilos
        );
        AddValue(
            values,
            availableColumns,
            "AppliedInterest",
            "@appliedInterest",
            DbType.Decimal,
            terms.AppliedInterest
        );
        AddValue(
            values,
            availableColumns,
            "FixedMonthlyAmount",
            "@fixedMonthlyAmount",
            DbType.Decimal,
            terms.FixedMonthlyAmount
        );
        AddValue(
            values,
            availableColumns,
            "StartDate",
            "@startDate",
            DbType.DateTime2,
            terms.StartDate
        );
        AddValue(values, availableColumns, "EndDate", "@endDate", DbType.DateTime2, terms.EndDate);
        AddValue(
            values,
            availableColumns,
            "AgreedOverallKilo",
            "@agreedOverallKilo",
            DbType.Int32,
            terms.AgreedOverallKilo
        );
        AddValue(
            values,
            availableColumns,
            "ExcessKilosTarrif",
            "@excessKilosTarrif",
            DbType.Decimal,
            terms.ExcessKilosTarrif
        );
        AddValue(
            values,
            availableColumns,
            "RelieveVehicle",
            "@relieveVehicle",
            DbType.Boolean,
            terms.RelieveVehicle
        );
        AddValue(
            values,
            availableColumns,
            "lease_site_code",
            "@leaseSiteCode",
            DbType.Int16,
            terms.lease_site_code
        );

        var comments = FirstNonEmpty(terms.authority_comment, terms.lease_notes, terms.Comments);
        AddValue(values, availableColumns, "Comments", "@comments", DbType.String, comments);
        AddValue(
            values,
            availableColumns,
            "authority_comment",
            "@authorityComment",
            DbType.String,
            terms.authority_comment
        );
        AddValue(
            values,
            availableColumns,
            "rejection_reason",
            "@rejectionReason",
            DbType.String,
            terms.rejection_reason
        );
        return values;
    }

    private static void AddWorkflowWriteValues(
        ICollection<WriteValue> values,
        IReadOnlySet<string> availableColumns,
        LeaseContractTerms terms,
        int currentUserId,
        DateTime now
    )
    {
        var status = terms.AuthorityStatus ?? 1;
        var legacyStatus = status == 4 ? 0 : status;
        var legacyRejected = status switch
        {
            1 => 3,
            2 => 4,
            4 => 1,
            _ => terms.Rejected ?? 0,
        };

        AddValue(
            values,
            availableColumns,
            "AuthorityStatus",
            "@authorityStatus",
            DbType.Int32,
            legacyStatus
        );
        AddValue(values, availableColumns, "Rejected", "@rejected", DbType.Int32, legacyRejected);
        AddValue(
            values,
            availableColumns,
            "AuthorisedBy",
            "@authorisedBy",
            DbType.Int32,
            status == 2 && currentUserId > 0 ? currentUserId : terms.AuthorisedBy
        );
        AddValue(
            values,
            availableColumns,
            "AuthorisedDate",
            "@authorisedDate",
            DbType.DateTime2,
            status == 2 ? now : terms.AuthorisedDate
        );
    }

    private static LeaseContractTerms MapLeaseTerms(DbDataReader reader) =>
        new()
        {
            VehicleContractTermID = ReadInt32(reader, "VehicleContractTermID") ?? 0,
            vmf_Code = ReadInt32(reader, "vmf_Code") ?? 0,
            AgreedTerms = ReadInt32(reader, "AgreedTerms"),
            AgreedKilos = ReadInt64(reader, "AgreedKilos"),
            AppliedInterest = ReadDecimal(reader, "AppliedInterest"),
            FixedMonthlyAmount = ReadDecimal(reader, "FixedMonthlyAmount"),
            AuthorityStatus = ReadInt32(reader, "AuthorityStatus"),
            CreatedBy = ReadInt32(reader, "CreatedBy"),
            CreatedByUsername = ReadString(reader, "CreatedByUsername"),
            CreatedDate = ReadDateTime(reader, "CreatedDate"),
            ModifiedBy = ReadInt32(reader, "ModifiedBy"),
            UpdatedByUsername = ReadString(reader, "UpdatedByUsername"),
            ModifiedDate = ReadDateTime(reader, "ModifiedDate"),
            StartDate = ReadDateTime(reader, "StartDate"),
            EndDate = ReadDateTime(reader, "EndDate"),
            date_created = ReadDateTime(reader, "date_created") ?? DateTime.MinValue,
            date_updated = ReadDateTime(reader, "date_updated"),
            created_by_user_code = ReadInt32(reader, "created_by_user_code"),
            modified_by_user_code = ReadInt32(reader, "modified_by_user_code"),
            is_deleted = ReadBoolean(reader, "is_deleted") ?? false,
            AgreedOverallKilo = ReadInt32(reader, "AgreedOverallKilo"),
            ExcessKilosTarrif = ReadDecimal(reader, "ExcessKilosTarrif"),
            RelieveVehicle = ReadBoolean(reader, "RelieveVehicle"),
            lease_site_code = ReadInt16(reader, "lease_site_code"),
            Comments = ReadString(reader, "Comments"),
            Rejected = ReadInt32(reader, "Rejected"),
            AuthorisedBy = ReadInt32(reader, "AuthorisedBy"),
            AuthorisedByUsername = ReadString(reader, "AuthorisedByUsername"),
            AuthorisedDate = ReadDateTime(reader, "AuthorisedDate"),
            authority_comment = ReadString(reader, "authority_comment"),
            rejection_reason = ReadString(reader, "rejection_reason"),
            lease_status = ReadString(reader, "lease_status"),
        };

    private static string GetAuthorityStatusProjection(IReadOnlySet<string> columns)
    {
        var expression = GetEffectiveAuthorityStatusExpression(columns);
        return expression is null
            ? "CAST(NULL AS int) AS [AuthorityStatus]"
            : $"{expression} AS [AuthorityStatus]";
    }

    private static string? GetEffectiveAuthorityStatusExpression(IReadOnlySet<string> columns)
    {
        if (!columns.Contains("AuthorityStatus"))
        {
            return null;
        }

        return columns.Contains("Rejected")
            ? "CASE WHEN [l].[AuthorityStatus] = 0 AND [l].[Rejected] = 1 THEN 4 ELSE [l].[AuthorityStatus] END"
            : "[l].[AuthorityStatus]";
    }

    private static string GetEffectiveStartProjection(
        IReadOnlySet<string> columns,
        bool vehicleDateFallbackAvailable
    )
    {
        if (columns.Contains("StartDate") && vehicleDateFallbackAvailable)
        {
            return "COALESCE([l].[StartDate], [v].[take_on_date]) AS [StartDate]";
        }

        if (columns.Contains("StartDate"))
        {
            return "[l].[StartDate] AS [StartDate]";
        }

        return vehicleDateFallbackAvailable
            ? "[v].[take_on_date] AS [StartDate]"
            : "CAST(NULL AS datetime2) AS [StartDate]";
    }

    private static string GetEffectiveEndProjection(
        IReadOnlySet<string> columns,
        bool vehicleDateFallbackAvailable
    )
    {
        var fallback =
            columns.Contains("AgreedTerms") && vehicleDateFallbackAvailable
                ? "CASE WHEN [l].[AgreedTerms] IS NULL OR [v].[take_on_date] IS NULL THEN NULL ELSE DATEADD(month, [l].[AgreedTerms], [v].[take_on_date]) END"
                : "CAST(NULL AS datetime2)";

        if (columns.Contains("EndDate") && vehicleDateFallbackAvailable)
        {
            return $"COALESCE([l].[EndDate], {fallback}) AS [EndDate]";
        }

        if (columns.Contains("EndDate"))
        {
            return "[l].[EndDate] AS [EndDate]";
        }

        return $"{fallback} AS [EndDate]";
    }

    private static string GetDateProjection(
        IReadOnlySet<string> columns,
        string alias,
        params string[] candidates
    )
    {
        var column = FirstAvailable(columns, candidates);
        return column is null
            ? $"CAST(NULL AS datetime2) AS [{alias}]"
            : $"[l].[{column}] AS [{alias}]";
    }

    private static string GetPreferredDateProjection(
        IReadOnlySet<string> columns,
        string alias,
        IReadOnlyList<string> candidates
    )
    {
        var available = candidates
            .Where(columns.Contains)
            .Select(column => $"[l].[{column}]")
            .ToArray();
        return available.Length == 0
            ? $"CAST(NULL AS datetime2) AS [{alias}]"
            : $"COALESCE({string.Join(", ", available)}) AS [{alias}]";
    }

    private static string GetPreferredProjection(
        IReadOnlySet<string> columns,
        string alias,
        IReadOnlyList<string> candidates,
        string sqlType
    )
    {
        var available = candidates
            .Where(columns.Contains)
            .Select(column => $"[l].[{column}]")
            .ToArray();
        return available.Length == 0
            ? $"CAST(NULL AS {sqlType}) AS [{alias}]"
            : $"COALESCE({string.Join(", ", available)}) AS [{alias}]";
    }

    private static string GetProjection(
        IReadOnlySet<string> columns,
        string alias,
        string column,
        string sqlType
    ) =>
        columns.Contains(column)
            ? $"[l].[{column}] AS [{alias}]"
            : $"CAST(NULL AS {sqlType}) AS [{alias}]";

    private static string GetLegacyUserIdProjection(
        IReadOnlySet<string> columns,
        string alias,
        params string[] candidates
    )
    {
        var available = candidates.Where(columns.Contains).ToArray();
        return available.Length == 0
            ? $"CAST(NULL AS int) AS [{alias}]"
            : $"COALESCE({string.Join(", ", available.Select(column => $"TRY_CONVERT(int, [l].[{column}])"))}) AS [{alias}]";
    }

    private static string GetLegacyUsernameProjection(
        IReadOnlySet<string> columns,
        string alias,
        params string[] candidates
    )
    {
        var available = candidates.Where(columns.Contains).ToArray();
        return available.Length == 0
            ? $"CAST(NULL AS varchar(200)) AS [{alias}]"
            : $"COALESCE({string.Join(", ", available.Select(column => $"NULLIF(LTRIM(RTRIM(CONVERT(varchar(200), [l].[{column}]))), '')"))}) AS [{alias}]";
    }

    private static string GetOptionalProjection(
        IReadOnlySet<string> columns,
        string column,
        string sqlType,
        string fallback
    ) =>
        columns.Contains(column)
            ? $"[l].[{column}] AS [{column}]"
            : $"CAST({fallback} AS {sqlType}) AS [{column}]";

    private static string GetRejectionReasonProjection(IReadOnlySet<string> columns)
    {
        if (columns.Contains("rejection_reason"))
        {
            return "[l].[rejection_reason] AS [rejection_reason]";
        }

        return columns.Contains("Comments") && columns.Contains("Rejected")
            ? "CASE WHEN [l].[Rejected] = 1 THEN [l].[Comments] ELSE NULL END AS [rejection_reason]"
            : "CAST(NULL AS varchar(250)) AS [rejection_reason]";
    }

    private static string GetLeaseStatusProjection(IReadOnlySet<string> columns) =>
        columns.Contains("AuthorityStatus")
            ? "CASE [l].[AuthorityStatus] WHEN 1 THEN 'Pending' WHEN 2 THEN 'Approved' WHEN 4 THEN 'Rejected' WHEN 0 THEN 'Rejected' ELSE 'Unknown' END AS [lease_status]"
            : "CAST(NULL AS varchar(32)) AS [lease_status]";

    private static string GetActiveFilter(IReadOnlySet<string> columns) =>
        columns.Contains("is_deleted") ? "ISNULL([l].[is_deleted], 0) = 0" : "1 = 1";

    private static string? FirstAvailable(
        IReadOnlySet<string> columns,
        params string[] candidates
    ) => candidates.FirstOrDefault(columns.Contains);

    private static void ValidateVehicleCode(LeaseContractTerms terms)
    {
        if (terms.vmf_Code <= 0)
        {
            throw new ArgumentException("A valid vehicle code is required.", nameof(terms));
        }
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static void AddValue(
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

    private static void AddAssignment(
        ICollection<string> assignments,
        DbCommand command,
        IReadOnlySet<string> availableColumns,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (!availableColumns.Contains(column))
        {
            return;
        }

        assignments.Add($"[{column}] = {parameter}");
        AddParameter(command, parameter, type, value);
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
        return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToString(reader.GetValue(ordinal))?.TrimEnd();
    }

    private static int? ReadInt32(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static long? ReadInt64(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt64(reader.GetValue(ordinal));
    }

    private static short? ReadInt16(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt16(reader.GetValue(ordinal));
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

    private static bool? ReadBoolean(DbDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToBoolean(reader.GetValue(ordinal));
    }

    private sealed record LeaseContractTermsPageFilter(
        string? Predicate,
        string? SearchTerm,
        bool IncludeVehicleJoin,
        IReadOnlyList<short>? AllowedSiteCodes = null,
        int? CurrentUserId = null
    );

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

    private sealed record ProcedureParameter(
        string Name,
        DbType Type,
        object? Value,
        ParameterDirection Direction = ParameterDirection.Input
    );
}
