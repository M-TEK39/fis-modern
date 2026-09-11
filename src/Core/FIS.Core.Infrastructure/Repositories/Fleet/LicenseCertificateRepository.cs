using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using FIS.Core.Application.Interfaces;
using FIS.Core.Domain.Entities.Operations;
using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace FIS.Core.Infrastructure.Repositories;

/// <summary>
/// Compatibility repository for licence certificate scans. It never asks EF
/// to map either optional table, because the client database may have only
/// scan_docs while a modern database may also have vehicle_documents.
/// </summary>
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "Table and column identifiers come only from fixed compatibility schemas; values are parameters."
)]
public sealed class LicenseCertificateRepository : ILicenseCertificateRepository
{
    private static readonly string[] MissingVehicleRequiredColumns =
    [
        "vmf_code",
        "vehicle_status_code",
        "location_code",
        "fleet_number",
        "registration_number",
    ];

    private static readonly Regex ModernPeriodPattern = new(
        @"\[period:(?<begin>\d{4}-\d{2}-\d{2})?\.\.(?<end>\d{4}-\d{2}-\d{2})?\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase
    );

    private readonly FisDbContext _context;

    public LicenseCertificateRepository(FisDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IEnumerable<LicenseCertificateDocument>> GetAllAsync()
    {
        var schema = await GetSchemaAsync();
        var result = new List<LicenseCertificateDocument>();
        if (schema.Modern is not null)
            result.AddRange(await QueryModernAsync(schema.Modern));
        if (schema.Legacy is not null)
            result.AddRange(await QueryLegacyAsync(schema.Legacy));
        return result
            .OrderByDescending(document =>
                document.period_begin ?? document.date_created ?? DateTime.MinValue
            )
            .ThenBy(document => document.vmf_code)
            .ThenBy(document => document.DocumentKey);
    }

    public async Task<LicenseCertificatePage> GetPageAsync(int page = 1, int pageSize = 24)
    {
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var requestedPage = Math.Max(1, page);
        var schema = await GetSchemaAsync();
        if (schema.Modern is null && schema.Legacy is null)
            return new LicenseCertificatePage([], 1, normalizedPageSize, 0);

        var vehicleColumns = await GetTableColumnsAsync("vehicle_master");
        EnsureVehicleColumns(vehicleColumns);
        var unionQuery = BuildCertificateUnionQuery(schema);
        await using var scope = await OpenConnectionAsync();

        await using var countCommand = scope.Connection.CreateCommand();
        countCommand.Transaction = CurrentTransaction;
        countCommand.CommandText = $"""
            WITH [certificate_documents] AS (
                {unionQuery}
            )
            SELECT COUNT(1)
            FROM [certificate_documents]
            """;
        AddCertificateCategoryParameter(countCommand, schema);
        var total = Convert.ToInt32(
            await countCommand.ExecuteScalarAsync(),
            CultureInfo.InvariantCulture
        );

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)normalizedPageSize));
        var normalizedPage = Math.Min(requestedPage, totalPages);
        var skip = checked((long)(normalizedPage - 1) * normalizedPageSize);

        await using var dataCommand = scope.Connection.CreateCommand();
        dataCommand.Transaction = CurrentTransaction;
        dataCommand.CommandText = $"""
            WITH [certificate_documents] AS (
                {unionQuery}
            )
            SELECT
                [certificates].[source],
                [certificates].[document_key],
                [certificates].[vmf_code],
                [certificates].[image],
                [certificates].[document_description],
                [certificates].[original_file_name],
                [certificates].[stored_file_path],
                [certificates].[mime_type],
                [certificates].[file_size_bytes],
                [certificates].[period_begin],
                [certificates].[period_end],
                [certificates].[date_created],
                [certificates].[date_updated],
                [vehicles].[fleet_number] AS [fleet_number],
                [vehicles].[registration_number] AS [registration_number]
            FROM [certificate_documents] AS [certificates]
            LEFT JOIN [dbo].[vehicle_master] AS [vehicles]
                ON [vehicles].[vmf_code] = [certificates].[vmf_code]
            ORDER BY
                COALESCE([vehicles].[fleet_number], ''),
                [certificates].[vmf_code],
                [certificates].[sort_date] DESC,
                [certificates].[source],
                [certificates].[document_sort_key]
            OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
            """;
        AddCertificateCategoryParameter(dataCommand, schema);
        AddParameter(dataCommand, "@skip", DbType.Int64, skip);
        AddParameter(dataCommand, "@pageSize", DbType.Int32, normalizedPageSize);

        var items = await ReadPageAsync(dataCommand);

        return new LicenseCertificatePage(items, normalizedPage, normalizedPageSize, total);
    }

    public async Task<MissingLicenseCertificatePage> GetMissingPageAsync(
        short? locationCode,
        int page = 1,
        int pageSize = 24
    )
    {
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);
        var requestedPage = Math.Max(1, page);
        var schema = await GetSchemaAsync();
        var vehicleColumns = await GetTableColumnsAsync("vehicle_master");
        EnsureVehicleColumns(vehicleColumns);
        var whereClause = BuildMissingVehicleWhereClause(schema, vehicleColumns, locationCode);

        await using var scope = await OpenConnectionAsync();

        await using var countCommand = scope.Connection.CreateCommand();
        countCommand.Transaction = CurrentTransaction;
        countCommand.CommandText = $"""
            SELECT COUNT(1)
            FROM [dbo].[vehicle_master] AS [vehicles]
            WHERE {whereClause}
            """;
        AddMissingVehicleParameters(countCommand, schema, locationCode);
        var total = Convert.ToInt32(
            await countCommand.ExecuteScalarAsync(),
            CultureInfo.InvariantCulture
        );

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)normalizedPageSize));
        var normalizedPage = Math.Min(requestedPage, totalPages);
        var skip = checked((long)(normalizedPage - 1) * normalizedPageSize);

        await using var dataCommand = scope.Connection.CreateCommand();
        dataCommand.Transaction = CurrentTransaction;
        dataCommand.CommandText = $"""
            SELECT
                [vehicles].[vmf_code],
                [vehicles].[fleet_number],
                [vehicles].[registration_number],
                [vehicles].[location_code]
            FROM [dbo].[vehicle_master] AS [vehicles]
            WHERE {whereClause}
            ORDER BY COALESCE([vehicles].[fleet_number], ''), [vehicles].[vmf_code]
            OFFSET @skip ROWS FETCH NEXT @pageSize ROWS ONLY
            """;
        AddMissingVehicleParameters(dataCommand, schema, locationCode);
        AddParameter(dataCommand, "@skip", DbType.Int64, skip);
        AddParameter(dataCommand, "@pageSize", DbType.Int32, normalizedPageSize);

        var items = new List<MissingLicenseCertificatePageItem>();
        await using var reader = await dataCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(
                new MissingLicenseCertificatePageItem(
                    ReadInt(reader, "vmf_code") ?? 0,
                    ReadString(reader, "fleet_number"),
                    ReadString(reader, "registration_number"),
                    Convert.ToInt16(reader["location_code"], CultureInfo.InvariantCulture)
                )
            );
        }

        return new MissingLicenseCertificatePage(items, normalizedPage, normalizedPageSize, total);
    }

    public async Task<IEnumerable<LicenseCertificateDocument>> GetByVehicleAsync(int vmfCode)
    {
        var schema = await GetSchemaAsync();
        var result = new List<LicenseCertificateDocument>();
        if (schema.Modern is not null)
            result.AddRange(await QueryModernAsync(schema.Modern, vmfCode));
        if (schema.Legacy is not null)
            result.AddRange(await QueryLegacyAsync(schema.Legacy, vmfCode));
        return result
            .OrderBy(document => document.period_begin ?? DateTime.MaxValue)
            .ThenBy(document => document.DocumentKey);
    }

    public async Task<LicenseCertificateDocument?> GetByKeyAsync(
        string source,
        int vmfCode,
        string documentKey
    )
    {
        var schema = await GetSchemaAsync();
        if (
            source.Equals("modern", StringComparison.OrdinalIgnoreCase)
            && schema.Modern is not null
            && int.TryParse(documentKey, out var documentId)
        )
        {
            return (
                await QueryModernAsync(
                    schema.Modern,
                    vmfCode,
                    "documents.[document_id] = @documentId",
                    command => AddParameter(command, "@documentId", DbType.Int32, documentId)
                )
            ).SingleOrDefault();
        }

        if (
            source.Equals("legacy", StringComparison.OrdinalIgnoreCase) && schema.Legacy is not null
        )
        {
            return (
                await QueryLegacyAsync(
                    schema.Legacy,
                    vmfCode,
                    "documents.[image] = @image",
                    command => AddParameter(command, "@image", DbType.String, documentKey)
                )
            ).SingleOrDefault();
        }

        return null;
    }

    public async Task<bool> HasAnyForVehicleAsync(int vmfCode) =>
        (await GetByVehicleAsync(vmfCode)).Any();

    public async Task<string?> GetPreferredWriteSourceAsync()
    {
        var schema = await GetSchemaAsync();
        return schema.Modern is not null ? "modern"
            : schema.Legacy is not null ? "legacy"
            : null;
    }

    public async Task<LicenseCertificateDocument> CreateAsync(
        LicenseCertificateDocument document,
        int currentUserId
    )
    {
        ArgumentNullException.ThrowIfNull(document);
        var schema = await GetSchemaAsync();
        if (
            document.Source.Equals("modern", StringComparison.OrdinalIgnoreCase)
            && schema.Modern is not null
        )
        {
            return await CreateModernAsync(schema.Modern, document, currentUserId);
        }

        if (
            document.Source.Equals("legacy", StringComparison.OrdinalIgnoreCase)
            && schema.Legacy is not null
        )
        {
            return await CreateLegacyAsync(schema.Legacy, document, currentUserId);
        }

        throw new InvalidOperationException(
            "Neither the modern vehicle_documents table nor the legacy scan_docs table is available."
        );
    }

    public async Task DeleteAsync(string source, int vmfCode, string documentKey, int currentUserId)
    {
        var schema = await GetSchemaAsync();
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;

        if (
            source.Equals("modern", StringComparison.OrdinalIgnoreCase)
            && schema.Modern is not null
            && int.TryParse(documentKey, out var documentId)
        )
        {
            if (schema.Modern.Columns.Contains("is_deleted"))
            {
                var assignments = new List<string> { "[is_deleted] = @isDeleted" };
                AddParameter(command, "@isDeleted", DbType.Boolean, true);
                AddOptionalAssignment(
                    assignments,
                    command,
                    schema.Modern,
                    "date_updated",
                    "@dateUpdated",
                    DbType.DateTime2,
                    DateTime.UtcNow
                );
                AddOptionalAssignment(
                    assignments,
                    command,
                    schema.Modern,
                    "modified_by_user_code",
                    "@modifiedBy",
                    DbType.Int32,
                    UserIdOrNull(currentUserId)
                );
                command.CommandText =
                    $"UPDATE [dbo].[vehicle_documents] SET {string.Join(", ", assignments)} WHERE [document_id] = @documentId AND [vmf_code] = @vmfCode AND {ActiveFilter(schema.Modern, "")}";
            }
            else
            {
                command.CommandText =
                    "DELETE FROM [dbo].[vehicle_documents] WHERE [document_id] = @documentId AND [vmf_code] = @vmfCode";
            }

            AddParameter(command, "@documentId", DbType.Int32, documentId);
            AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
            await command.ExecuteNonQueryAsync();
            return;
        }

        if (
            source.Equals("legacy", StringComparison.OrdinalIgnoreCase) && schema.Legacy is not null
        )
        {
            if (schema.Legacy.Columns.Contains("is_deleted"))
            {
                var assignments = new List<string> { "[is_deleted] = @isDeleted" };
                AddParameter(command, "@isDeleted", DbType.Boolean, true);
                AddOptionalAssignment(
                    assignments,
                    command,
                    schema.Legacy,
                    "date_updated",
                    "@dateUpdated",
                    DbType.DateTime2,
                    DateTime.UtcNow
                );
                AddOptionalAssignment(
                    assignments,
                    command,
                    schema.Legacy,
                    "modified_by_user_code",
                    "@modifiedBy",
                    DbType.Int32,
                    UserIdOrNull(currentUserId)
                );
                command.CommandText =
                    $"UPDATE [dbo].[scan_docs] SET {string.Join(", ", assignments)} WHERE [vmf_code] = @vmfCode AND [image] = @image AND {ActiveFilter(schema.Legacy, "")}";
            }
            else
            {
                command.CommandText =
                    "DELETE FROM [dbo].[scan_docs] WHERE [vmf_code] = @vmfCode AND [image] = @image";
            }

            AddParameter(command, "@vmfCode", DbType.Int32, vmfCode);
            AddParameter(command, "@image", DbType.String, documentKey);
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task<LicenseCertificateDocument> CreateModernAsync(
        TableSchema schema,
        LicenseCertificateDocument document,
        int currentUserId
    )
    {
        var values = new List<WriteValue>
        {
            RequiredValue(schema, "vmf_code", "@vmfCode", DbType.Int32, document.vmf_code),
            RequiredValue(schema, "document_category", "@category", DbType.String, "Licence"),
            RequiredValue(
                schema,
                "original_file_name",
                "@originalFileName",
                DbType.String,
                document.original_file_name ?? document.image
            ),
            RequiredValue(
                schema,
                "stored_file_path",
                "@storedFilePath",
                DbType.String,
                document.stored_file_path
            ),
            RequiredValue(schema, "mime_type", "@mimeType", DbType.String, document.mime_type),
            RequiredValue(
                schema,
                "file_size_bytes",
                "@fileSizeBytes",
                DbType.Int64,
                document.file_size_bytes
            ),
        };
        AddValue(
            values,
            schema,
            "document_description",
            "@description",
            DbType.String,
            document.document_description
        );
        AddValue(values, schema, "reference_type", "@referenceType", DbType.String, null);
        AddValue(values, schema, "reference_id", "@referenceId", DbType.Int32, null);
        AddAuditValues(values, schema, currentUserId);

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            $"INSERT INTO [dbo].[vehicle_documents] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) OUTPUT INSERTED.[document_id] VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        document.DocumentKey =
            Convert.ToString(await command.ExecuteScalarAsync())
            ?? throw new InvalidOperationException(
                "Created licence certificate did not return an identifier."
            );
        return document;
    }

    private async Task<LicenseCertificateDocument> CreateLegacyAsync(
        TableSchema schema,
        LicenseCertificateDocument document,
        int currentUserId
    )
    {
        var values = new List<WriteValue>
        {
            RequiredValue(schema, "vmf_code", "@vmfCode", DbType.Int32, document.vmf_code),
            RequiredValue(schema, "image", "@image", DbType.String, document.image),
            RequiredValue(
                schema,
                "period_begin",
                "@periodBegin",
                DbType.DateTime2,
                document.period_begin
            ),
            RequiredValue(
                schema,
                "period_end",
                "@periodEnd",
                DbType.DateTime2,
                document.period_end
            ),
        };
        AddAuditValues(values, schema, currentUserId);

        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            $"INSERT INTO [dbo].[scan_docs] ({string.Join(", ", values.Select(value => $"[{value.Column}]"))}) VALUES ({string.Join(", ", values.Select(value => value.Parameter))})";
        AddParameters(command, values);
        await command.ExecuteNonQueryAsync();
        return document;
    }

    private async Task<List<LicenseCertificateDocument>> QueryModernAsync(
        TableSchema schema,
        int? vmfCode = null,
        string? extraPredicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var conditions = new List<string>
        {
            ActiveFilter(schema, "documents"),
            "documents.[document_category] = @category",
        };
        if (vmfCode.HasValue)
            conditions.Add("documents.[vmf_code] = @vmfCode");
        if (!string.IsNullOrWhiteSpace(extraPredicate))
            conditions.Add(extraPredicate);
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            $"SELECT CONVERT(nvarchar(100), documents.[document_id]) AS [document_key], documents.[vmf_code], CAST(NULL AS nvarchar(max)) AS [image], {StringProjection(schema, "document_description")} AS [document_description], documents.[original_file_name], documents.[stored_file_path], documents.[mime_type], documents.[file_size_bytes], CAST(NULL AS datetime2) AS [period_begin], CAST(NULL AS datetime2) AS [period_end], {Projection(schema, "date_created")} AS [date_created], {Projection(schema, "date_updated")} AS [date_updated] FROM [dbo].[vehicle_documents] documents WHERE {string.Join(" AND ", conditions)} ORDER BY {Projection(schema, "date_created")} DESC, documents.[document_id] DESC";
        AddParameter(command, "@category", DbType.String, "Licence");
        if (vmfCode.HasValue)
            AddParameter(command, "@vmfCode", DbType.Int32, vmfCode.Value);
        configure?.Invoke(command);
        return await ReadAsync(command, "modern");
    }

    private async Task<List<LicenseCertificateDocument>> QueryLegacyAsync(
        TableSchema schema,
        int? vmfCode = null,
        string? extraPredicate = null,
        Action<DbCommand>? configure = null
    )
    {
        var conditions = new List<string> { ActiveFilter(schema, "documents") };
        if (vmfCode.HasValue)
            conditions.Add("documents.[vmf_code] = @vmfCode");
        if (!string.IsNullOrWhiteSpace(extraPredicate))
            conditions.Add(extraPredicate);
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            $"SELECT documents.[image] AS [document_key], documents.[vmf_code], documents.[image], CAST(NULL AS nvarchar(500)) AS [document_description], CAST(NULL AS nvarchar(255)) AS [original_file_name], CAST(NULL AS nvarchar(500)) AS [stored_file_path], CAST(NULL AS nvarchar(100)) AS [mime_type], CAST(NULL AS bigint) AS [file_size_bytes], documents.[period_begin], documents.[period_end], {Projection(schema, "date_created")} AS [date_created], {Projection(schema, "date_updated")} AS [date_updated] FROM [dbo].[scan_docs] documents WHERE {string.Join(" AND ", conditions)} ORDER BY documents.[period_begin], documents.[image]";
        if (vmfCode.HasValue)
            AddParameter(command, "@vmfCode", DbType.Int32, vmfCode.Value);
        configure?.Invoke(command);
        return await ReadAsync(command, "legacy");
    }

    private static string BuildCertificateUnionQuery(SchemaSet schema)
    {
        var queries = new List<string>();
        if (schema.Modern is not null)
        {
            queries.Add(
                $"""
                SELECT
                    CAST('modern' AS nvarchar(10)) AS [source],
                    CONVERT(nvarchar(max), documents.[document_id]) AS [document_key],
                    CONVERT(nvarchar(450), documents.[document_id]) AS [document_sort_key],
                    CONVERT(int, documents.[vmf_code]) AS [vmf_code],
                    CAST(NULL AS nvarchar(max)) AS [image],
                    CONVERT(nvarchar(max), {StringProjection(
                    schema.Modern,
                    "document_description"
                )}) AS [document_description],
                    CONVERT(nvarchar(max), documents.[original_file_name]) AS [original_file_name],
                    CONVERT(nvarchar(max), documents.[stored_file_path]) AS [stored_file_path],
                    CONVERT(nvarchar(100), documents.[mime_type]) AS [mime_type],
                    CONVERT(bigint, documents.[file_size_bytes]) AS [file_size_bytes],
                    CAST(NULL AS datetime2) AS [period_begin],
                    CAST(NULL AS datetime2) AS [period_end],
                    {Projection(schema.Modern, "date_created")} AS [date_created],
                    {Projection(schema.Modern, "date_updated")} AS [date_updated],
                    {Projection(schema.Modern, "date_created")} AS [sort_date]
                FROM [dbo].[vehicle_documents] AS documents
                WHERE {ActiveFilter(schema.Modern, "documents")}
                  AND documents.[document_category] = @category
                """
            );
        }

        if (schema.Legacy is not null)
        {
            queries.Add(
                $"""
                SELECT
                    CAST('legacy' AS nvarchar(10)) AS [source],
                    CONVERT(nvarchar(max), documents.[image]) AS [document_key],
                    CONVERT(nvarchar(450), documents.[image]) AS [document_sort_key],
                    CONVERT(int, documents.[vmf_code]) AS [vmf_code],
                    CONVERT(nvarchar(max), documents.[image]) AS [image],
                    CAST(NULL AS nvarchar(max)) AS [document_description],
                    CAST(NULL AS nvarchar(max)) AS [original_file_name],
                    CAST(NULL AS nvarchar(max)) AS [stored_file_path],
                    CAST(NULL AS nvarchar(100)) AS [mime_type],
                    CAST(NULL AS bigint) AS [file_size_bytes],
                    documents.[period_begin] AS [period_begin],
                    documents.[period_end] AS [period_end],
                    {Projection(schema.Legacy, "date_created")} AS [date_created],
                    {Projection(schema.Legacy, "date_updated")} AS [date_updated],
                    documents.[period_begin] AS [sort_date]
                FROM [dbo].[scan_docs] AS documents
                WHERE {ActiveFilter(schema.Legacy, "documents")}
                """
            );
        }

        return string.Join("\nUNION ALL\n", queries);
    }

    private static void AddCertificateCategoryParameter(DbCommand command, SchemaSet schema)
    {
        if (schema.Modern is not null)
            AddParameter(command, "@category", DbType.String, "Licence");
    }

    private static string BuildMissingVehicleWhereClause(
        SchemaSet schema,
        IReadOnlySet<string> vehicleColumns,
        short? locationCode
    )
    {
        var conditions = new List<string>
        {
            "[vehicles].[vehicle_status_code] > 0",
            vehicleColumns.Contains("is_deleted")
                ? "ISNULL([vehicles].[is_deleted], 0) = 0"
                : "1 = 1",
        };

        if (locationCode.HasValue)
            conditions.Add("[vehicles].[location_code] = @locationCode");

        if (schema.Modern is not null)
        {
            conditions.Add(
                $"""
                NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[vehicle_documents] AS [modern_documents]
                    WHERE {ActiveFilter(schema.Modern, "modern_documents")}
                      AND [modern_documents].[document_category] = @category
                      AND [modern_documents].[vmf_code] = [vehicles].[vmf_code]
                )
                """
            );
        }

        if (schema.Legacy is not null)
        {
            conditions.Add(
                $"""
                NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[scan_docs] AS [legacy_documents]
                    WHERE {ActiveFilter(schema.Legacy, "legacy_documents")}
                      AND [legacy_documents].[vmf_code] = [vehicles].[vmf_code]
                )
                """
            );
        }

        return string.Join(" AND ", conditions);
    }

    private static void AddMissingVehicleParameters(
        DbCommand command,
        SchemaSet schema,
        short? locationCode
    )
    {
        AddCertificateCategoryParameter(command, schema);
        if (locationCode.HasValue)
            AddParameter(command, "@locationCode", DbType.Int16, locationCode.Value);
    }

    private static async Task<List<LicenseCertificateDocument>> ReadAsync(
        DbCommand command,
        string source
    )
    {
        var result = new List<LicenseCertificateDocument>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            result.Add(ReadDocument(reader, source));
        return result;
    }

    private static LicenseCertificateDocument ReadDocument(DbDataReader reader, string source)
    {
        var description = ReadString(reader, "document_description");
        var period = ParseModernPeriod(description);
        return new LicenseCertificateDocument
        {
            Source = source,
            DocumentKey = ReadString(reader, "document_key") ?? string.Empty,
            vmf_code = ReadInt(reader, "vmf_code") ?? 0,
            image = ReadString(reader, "image"),
            document_description = description,
            original_file_name = ReadString(reader, "original_file_name"),
            stored_file_path = ReadString(reader, "stored_file_path"),
            mime_type = ReadString(reader, "mime_type") ?? "application/octet-stream",
            file_size_bytes = ReadLong(reader, "file_size_bytes") ?? 0,
            period_begin = ReadDate(reader, "period_begin") ?? period.Begin,
            period_end = ReadDate(reader, "period_end") ?? period.End,
            date_created = ReadDate(reader, "date_created"),
            date_updated = ReadDate(reader, "date_updated"),
        };
    }

    private async Task<List<LicenseCertificatePageItem>> ReadPageAsync(DbCommand command)
    {
        var result = new List<LicenseCertificatePageItem>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(
                new LicenseCertificatePageItem(
                    ReadDocument(reader, ReadString(reader, "source") ?? string.Empty),
                    ReadString(reader, "fleet_number"),
                    ReadString(reader, "registration_number")
                )
            );
        }

        return result;
    }

    private async Task<SchemaSet> GetSchemaAsync()
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
        command.CommandText =
            "SELECT [TABLE_NAME], [COLUMN_NAME] FROM [INFORMATION_SCHEMA].[COLUMNS] WHERE [TABLE_SCHEMA] = @schema AND [TABLE_NAME] IN (@modern, @legacy)";
        AddParameter(command, "@schema", DbType.String, "dbo");
        AddParameter(command, "@modern", DbType.String, "vehicle_documents");
        AddParameter(command, "@legacy", DbType.String, "scan_docs");
        var tables = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var table = reader.GetString(0);
            if (!tables.TryGetValue(table, out var columns))
            {
                columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                tables[table] = columns;
            }
            columns.Add(reader.GetString(1));
        }

        TableSchema? modern =
            tables.TryGetValue("vehicle_documents", out var modernColumns)
            && new[]
            {
                "document_id",
                "vmf_code",
                "document_category",
                "original_file_name",
                "stored_file_path",
                "mime_type",
                "file_size_bytes",
            }.All(modernColumns.Contains)
                ? new TableSchema(modernColumns)
                : null;
        TableSchema? legacy =
            tables.TryGetValue("scan_docs", out var legacyColumns)
            && new[] { "vmf_code", "image", "period_begin", "period_end" }.All(
                legacyColumns.Contains
            )
                ? new TableSchema(legacyColumns)
                : null;
        return new SchemaSet(modern, legacy);
    }

    private async Task<HashSet<string>> GetTableColumnsAsync(string tableName)
    {
        await using var scope = await OpenConnectionAsync();
        await using var command = scope.Connection.CreateCommand();
        command.Transaction = CurrentTransaction;
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
            columns.Add(reader.GetString(0));
        return columns;
    }

    private static void EnsureVehicleColumns(IReadOnlySet<string> vehicleColumns)
    {
        var missingColumns = MissingVehicleRequiredColumns
            .Where(column => !vehicleColumns.Contains(column))
            .ToArray();
        if (missingColumns.Length > 0)
        {
            throw new InvalidOperationException(
                $"The required vehicle_master compatibility columns are not available: {string.Join(", ", missingColumns)}"
            );
        }
    }

    private static string Projection(TableSchema schema, string column) =>
        schema.Columns.Contains(column) ? $"documents.[{column}]" : $"CAST(NULL AS datetime2)";

    private static string StringProjection(TableSchema schema, string column) =>
        schema.Columns.Contains(column) ? $"documents.[{column}]" : "CAST(NULL AS nvarchar(500))";

    private static (DateTime? Begin, DateTime? End) ParseModernPeriod(string? description)
    {
        var match = description is null ? null : ModernPeriodPattern.Match(description);
        if (match is null || !match.Success)
        {
            return (null, null);
        }

        DateTime? begin = DateTime.TryParseExact(
            match.Groups["begin"].Value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedBegin
        )
            ? parsedBegin
            : null;
        DateTime? end = DateTime.TryParseExact(
            match.Groups["end"].Value,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedEnd
        )
            ? parsedEnd
            : null;
        return (begin, end);
    }

    private static string ActiveFilter(TableSchema schema, string alias)
    {
        if (!schema.Columns.Contains("is_deleted"))
            return "1 = 1";
        var column = string.IsNullOrWhiteSpace(alias) ? "[is_deleted]" : $"{alias}.[is_deleted]";
        return $"ISNULL({column}, 0) = 0";
    }

    private static WriteValue RequiredValue(
        TableSchema schema,
        string column,
        string parameter,
        DbType type,
        object? value
    ) =>
        schema.Columns.Contains(column)
            ? new WriteValue(column, parameter, type, value)
            : throw new InvalidOperationException(
                $"Required licence certificate column {column} is unavailable."
            );

    private static void AddValue(
        ICollection<WriteValue> values,
        TableSchema schema,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (schema.Columns.Contains(column))
            values.Add(new WriteValue(column, parameter, type, value));
    }

    private static void AddAuditValues(
        ICollection<WriteValue> values,
        TableSchema schema,
        int currentUserId
    )
    {
        AddValue(values, schema, "date_created", "@dateCreated", DbType.DateTime2, DateTime.UtcNow);
        AddValue(
            values,
            schema,
            "created_by_user_code",
            "@createdBy",
            DbType.Int32,
            UserIdOrNull(currentUserId)
        );
        AddValue(values, schema, "is_deleted", "@isDeleted", DbType.Boolean, false);
    }

    private static void AddOptionalAssignment(
        ICollection<string> assignments,
        DbCommand command,
        TableSchema schema,
        string column,
        string parameter,
        DbType type,
        object? value
    )
    {
        if (!schema.Columns.Contains(column))
            return;
        assignments.Add($"[{column}] = {parameter}");
        AddParameter(command, parameter, type, value);
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

    private static int? UserIdOrNull(int value) => value > 0 ? value : null;

    private static string? ReadString(DbDataReader reader, string column) =>
        reader.IsDBNull(reader.GetOrdinal(column)) ? null : Convert.ToString(reader[column]);

    private static int? ReadInt(DbDataReader reader, string column) =>
        reader.IsDBNull(reader.GetOrdinal(column)) ? null : Convert.ToInt32(reader[column]);

    private static long? ReadLong(DbDataReader reader, string column) =>
        reader.IsDBNull(reader.GetOrdinal(column)) ? null : Convert.ToInt64(reader[column]);

    private static DateTime? ReadDate(DbDataReader reader, string column) =>
        reader.IsDBNull(reader.GetOrdinal(column)) ? null : Convert.ToDateTime(reader[column]);

    private sealed record TableSchema(IReadOnlySet<string> Columns);

    private sealed record SchemaSet(TableSchema? Modern, TableSchema? Legacy);

    private sealed record WriteValue(string Column, string Parameter, DbType Type, object? Value);

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
