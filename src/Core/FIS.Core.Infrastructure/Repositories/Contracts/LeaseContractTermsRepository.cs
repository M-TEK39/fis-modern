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

    public async Task<LeaseContractTerms?> GetByVehicleAsync(int vmfCode) =>
        (
            await QueryAsync(
                "[l].[vmf_Code] = @vmfCode",
                command => AddParameter(command, "@vmfCode", DbType.Int32, vmfCode)
            )
        ).FirstOrDefault();

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

    private async Task<List<LeaseContractTerms>> QueryAsync(
        string? predicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var availableColumns = await GetAvailableColumnsAsync(TableName, RequiredColumns);
        var vehicleColumns = await GetAvailableColumnsAsync(VehicleTableName);
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
                GetProjection(availableColumns, "CreatedBy", "CreatedBy", "int"),
                GetDateProjection(
                    availableColumns,
                    "CreatedDate",
                    "CreatedDate",
                    "CreatedDate",
                    "date_created"
                ),
                GetPreferredProjection(
                    availableColumns,
                    "ModifiedBy",
                    ["ModifiedBy", "UpdatedBy"],
                    "int"
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
                GetPreferredProjection(
                    availableColumns,
                    "created_by_user_code",
                    ["created_by_user_code", "CreatedBy"],
                    "int"
                ),
                GetPreferredProjection(
                    availableColumns,
                    "modified_by_user_code",
                    ["modified_by_user_code", "ModifiedBy", "UpdatedBy"],
                    "int"
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
                GetProjection(availableColumns, "lease_site_code", "lease_site_code", "smallint"),
                GetProjection(availableColumns, "Comments", "Comments", "varchar(250)"),
                GetProjection(availableColumns, "Rejected", "Rejected", "int"),
                GetProjection(availableColumns, "AuthorisedBy", "AuthorisedBy", "int"),
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
            var joins = vehicleDateFallbackAvailable
                ? "LEFT JOIN [dbo].[vehicle_master] AS [v] ON [v].[vmf_code] = [l].[vmf_Code]"
                : string.Empty;
            var where = string.IsNullOrWhiteSpace(predicate)
                ? GetActiveFilter(availableColumns)
                : $"{predicate} AND {GetActiveFilter(availableColumns)}";

            command.CommandText = $"""
                SELECT {string.Join(", ", projection)}
                FROM [dbo].[{TableName}] AS [l]
                {joins}
                WHERE {where}
                ORDER BY [l].[VehicleContractTermID] DESC
                """;
            configure?.Invoke(command);

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
            CreatedDate = ReadDateTime(reader, "CreatedDate"),
            ModifiedBy = ReadInt32(reader, "ModifiedBy"),
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
            AuthorisedDate = ReadDateTime(reader, "AuthorisedDate"),
            authority_comment = ReadString(reader, "authority_comment"),
            rejection_reason = ReadString(reader, "rejection_reason"),
            lease_status = ReadString(reader, "lease_status"),
        };

    private static string GetAuthorityStatusProjection(IReadOnlySet<string> columns)
    {
        if (!columns.Contains("AuthorityStatus"))
        {
            return "CAST(NULL AS int) AS [AuthorityStatus]";
        }

        if (!columns.Contains("Rejected"))
        {
            return "[l].[AuthorityStatus] AS [AuthorityStatus]";
        }

        return "CASE WHEN [l].[AuthorityStatus] = 0 AND [l].[Rejected] = 1 THEN 4 ELSE [l].[AuthorityStatus] END AS [AuthorityStatus]";
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

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);
}
