using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.SqlClient;

namespace FIS.Tools.DatabaseAuditTool;

public static class Program
{
    private const string DefaultFormat = "text";

    private static readonly TableSpec[] RequiredTables =
    [
        new("TS_Users", true),
        new("vehicle_master", true),
        new("contract", true),
        new("site", true),
        new("department", true),
        new("journal_detail", true),
        new("Call_centre", false),
        new("user_access_old1", false),
        new("Legacy_User_Credentials", false),
        new("EntraId_User_Mapping", false),
        new("fis_session_tokens", false),
        new("fis_data_fix_audit", false),
    ];

    // These names are derived from the read-only legacy DDL under
    // backup/sources/GGMT.Database/SQLScripts/v2.0.0. They are deliberately
    // kept as an allow-listed audit contract instead of being loaded from a
    // database or an arbitrary file path at runtime.
    private static readonly string[] LegacySiteCompatibilityColumns =
    [
        "financial_system_activate_date",
        "export_is_active",
        "date_last_exported",
        "Service_Kilometres",
        "Service_Years",
        "Overhead_Percentage",
        "province_code",
        "notes",
        "user_access_code",
    ];

    private static readonly string[] LegacyJobCardRequiredColumns =
    [
        "jc_code",
        "vmf_code",
        "extra_code",
        "status_code",
        "captured_by",
    ];

    private static readonly string[] ModernJobCardRequiredColumns =
    [
        "job_card_id",
        "vmf_code",
        "extra_code",
        "status_code",
        "priority",
        "jcs_comment",
        "authorizer",
        "reviewed",
        "date_created",
        "is_deleted",
    ];

    private static readonly string[] JournalDetailRequiredColumns =
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
        "journal_detail_date",
    ];

    public static async Task Main(string[] args)
    {
        try
        {
            var options = ParseOptions(args);
            if (options.ShowHelp)
            {
                PrintUsage();
                return;
            }

            var connectionString = ResolveRequiredConnectionString();
            ValidateConnectionString(connectionString);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            Console.Error.WriteLine($"Auditing database '{connection.Database}' (read-only).");

            var report = await BuildReportAsync(connection, options.LegacyDdlRoot);
            var output = options.Format switch
            {
                "json" => JsonSerializer.Serialize(report, JsonOptions),
                "csv" => ToCsv(report.Issues),
                _ => ToText(report),
            };

            if (options.OutputPath is null)
                Console.WriteLine(output);
            else
                await File.WriteAllTextAsync(options.OutputPath, output, Encoding.UTF8);

            Console.Error.WriteLine(
                $"Audit complete: {report.Issues.Count} issue(s), {report.ErrorCount} error(s), {report.WarningCount} warning(s)."
            );
            Environment.ExitCode = report.ErrorCount > 0 ? 2 : 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Database audit failed: {exception.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static AuditOptions ParseOptions(string[] args)
    {
        var format = DefaultFormat;
        string? outputPath = null;
        string? legacyDdlRoot = null;
        var showHelp = false;

        foreach (var argument in args)
        {
            if (argument is "--help" or "-h")
            {
                showHelp = true;
                continue;
            }

            if (argument.StartsWith("--format=", StringComparison.Ordinal))
            {
                format = argument["--format=".Length..].ToLowerInvariant();
                if (format is not ("text" or "json" or "csv"))
                    throw new InvalidOperationException("--format must be text, json, or csv.");
                continue;
            }

            if (argument.StartsWith("--output=", StringComparison.Ordinal))
            {
                outputPath = argument["--output=".Length..];
                if (string.IsNullOrWhiteSpace(outputPath))
                    throw new InvalidOperationException("--output requires a file path.");
                continue;
            }

            if (argument.StartsWith("--ddl-root=", StringComparison.Ordinal))
            {
                legacyDdlRoot = argument["--ddl-root=".Length..];
                if (string.IsNullOrWhiteSpace(legacyDdlRoot))
                    throw new InvalidOperationException("--ddl-root requires a directory path.");
                continue;
            }

            throw new InvalidOperationException($"Unknown option '{argument}'.");
        }

        return new AuditOptions(format, outputPath, legacyDdlRoot, showHelp);
    }

    private static string ResolveRequiredConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__Default must be set explicitly. The audit tool never supplies a fallback connection string."
            );
        }

        return connectionString;
    }

    private static void ValidateConnectionString(string connectionString)
    {
        if (
            connectionString.Contains("YOUR_DB_", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("192.0.2.10", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new InvalidOperationException(
                "The audit tool rejected a documentation or placeholder database connection."
            );
        }

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidOperationException(
                "ConnectionStrings__Default is not valid.",
                exception
            );
        }

        if (string.IsNullOrWhiteSpace(builder.DataSource))
            throw new InvalidOperationException(
                "ConnectionStrings__Default must specify a SQL Server data source."
            );

        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            throw new InvalidOperationException(
                "ConnectionStrings__Default must specify a target database."
            );

        if (
            new[] { "master", "model", "msdb", "tempdb" }.Contains(
                builder.InitialCatalog,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            throw new InvalidOperationException(
                "The audit tool refuses to target a SQL Server system database."
            );
        }
    }

    private static async Task<AuditReport> BuildReportAsync(
        SqlConnection connection,
        string? legacyDdlRoot
    )
    {
        var issues = new List<AuditIssue>();
        var tableAvailability = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var legacySchemaAudit = await LegacySchemaAudit.BuildAsync(connection, legacyDdlRoot);
        issues.AddRange(
            legacySchemaAudit.Issues.Select(
                issue =>
                    new AuditIssue(
                        issue.IssueCode,
                        issue.Severity,
                        issue.TableName,
                        issue.FieldName,
                        null,
                        issue.Occurrences,
                        issue.Description,
                        issue.RecommendedAction
                    )
            )
        );

        foreach (var table in RequiredTables)
        {
            var exists = await TableExistsAsync(connection, table.Name);
            tableAvailability[table.Name] = exists;

            if (!exists)
            {
                issues.Add(
                    new AuditIssue(
                        table.Required
                            ? "SCHEMA_REQUIRED_TABLE_MISSING"
                            : "SCHEMA_OPTIONAL_TABLE_MISSING",
                        table.Required ? "error" : "info",
                        table.Name,
                        null,
                        null,
                        1,
                        table.Required
                            ? "Required legacy table is not present. Core screens or compatibility paths cannot be trusted."
                            : "Optional expanded table is not present. The runtime must use its documented legacy fallback.",
                        table.Required
                            ? "Restore the table from the client schema or stop the cutover for manual investigation."
                            : "Run the approved additive migration only if this capability is required."
                    )
                );
            }
        }

        if (tableAvailability.GetValueOrDefault("TS_Users"))
            await AuditUsersAsync(connection, issues);

        if (tableAvailability.GetValueOrDefault("user_access_old1"))
            await AuditLegacyUsersAsync(
                connection,
                issues,
                tableAvailability.GetValueOrDefault("TS_Users")
            );

        if (tableAvailability.GetValueOrDefault("Legacy_User_Credentials"))
            await AuditCredentialsAsync(
                connection,
                issues,
                tableAvailability.GetValueOrDefault("TS_Users")
            );

        if (tableAvailability.GetValueOrDefault("EntraId_User_Mapping"))
            await AuditEntraMappingsAsync(
                connection,
                issues,
                tableAvailability.GetValueOrDefault("TS_Users")
            );

        if (tableAvailability.GetValueOrDefault("vehicle_master"))
            await AuditVehiclesAsync(connection, issues);

        if (tableAvailability.GetValueOrDefault("contract"))
            await AuditContractsAsync(
                connection,
                issues,
                tableAvailability.GetValueOrDefault("vehicle_master"),
                tableAvailability.GetValueOrDefault("site")
            );

        if (tableAvailability.GetValueOrDefault("site"))
            await AuditSitesAsync(connection, issues);

        if (tableAvailability.GetValueOrDefault("department"))
            await AuditDepartmentsAsync(connection, issues);

        if (tableAvailability.GetValueOrDefault("Call_centre"))
            await AuditCallCentreSchemaAsync(connection, issues);

        await AuditCompatibilitySchemaAsync(connection, tableAvailability, issues);

        var errorCount = issues.Count(issue => issue.Severity == "error");
        var warningCount = issues.Count(issue => issue.Severity == "warning");
        return new AuditReport(
            DateTimeOffset.UtcNow,
            connection.Database,
            legacySchemaAudit.ReferenceRoot,
            issues,
            errorCount,
            warningCount
        );
    }

    private static async Task AuditCompatibilitySchemaAsync(
        SqlConnection connection,
        IReadOnlyDictionary<string, bool> tableAvailability,
        List<AuditIssue> issues
    )
    {
        if (tableAvailability.GetValueOrDefault("site"))
            await AuditLegacySiteSchemaAsync(connection, issues);

        await AuditJobCardSchemaAsync(connection, issues);

        if (tableAvailability.GetValueOrDefault("journal_detail"))
            await AuditRequiredColumnsAsync(
                connection,
                issues,
                "journal_detail",
                JournalDetailRequiredColumns,
                "The legacy journal_detail table is missing columns required by the finance execution path.",
                "Restore the exact client journal_detail shape or stop the finance cutover for manual review."
            );
    }

    private static async Task AuditLegacySiteSchemaAsync(
        SqlConnection connection,
        List<AuditIssue> issues
    )
    {
        foreach (var column in LegacySiteCompatibilityColumns)
        {
            if (await ColumnsExistAsync(connection, "site", column))
                continue;

            issues.Add(
                new AuditIssue(
                    "SCHEMA_LEGACY_COLUMN_MISSING",
                    "warning",
                    "site",
                    column,
                    null,
                    1,
                    "A column documented by the legacy site schema is absent from this database.",
                    "Keep the runtime site projection guarded and use the documented legacy fallback; do not issue a static EF query for this column."
                )
            );
        }
    }

    private static async Task AuditJobCardSchemaAsync(
        SqlConnection connection,
        List<AuditIssue> issues
    )
    {
        var modernAvailable = await TableExistsAsync(connection, "job_cards");
        var legacyAvailable = await TableExistsAsync(connection, "Jobcards");

        if (!modernAvailable && !legacyAvailable)
        {
            issues.Add(
                new AuditIssue(
                    "SCHEMA_REQUIRED_COMPATIBILITY_TABLE_MISSING",
                    "error",
                    "job_cards/Jobcards",
                    null,
                    null,
                    1,
                    "Neither the modern job_cards table nor the documented legacy Jobcards table is present.",
                    "Restore one approved job-card schema before enabling job-card routes; do not create a guessed replacement table."
                )
            );
            return;
        }

        if (modernAvailable)
            await AuditRequiredColumnsAsync(
                connection,
                issues,
                "job_cards",
                ModernJobCardRequiredColumns,
                "The modern job_cards table is missing columns required by the modern job-card route.",
                "Compare the table with the approved modern schema and stop the job-card cutover for manual review."
            );

        if (legacyAvailable)
            await AuditRequiredColumnsAsync(
                connection,
                issues,
                "Jobcards",
                LegacyJobCardRequiredColumns,
                "The legacy Jobcards table is missing columns required by the compatibility repository.",
                "Compare the table with dbo.Jobcards.Table.sql and stop the job-card cutover for manual review."
            );
    }

    private static async Task AuditRequiredColumnsAsync(
        SqlConnection connection,
        List<AuditIssue> issues,
        string table,
        IReadOnlyCollection<string> requiredColumns,
        string description,
        string recommendation
    )
    {
        var missingColumns = new List<string>();
        foreach (var column in requiredColumns)
        {
            if (!await ColumnsExistAsync(connection, table, column))
                missingColumns.Add(column);
        }

        if (missingColumns.Count > 0)
            AddUnexpectedShapeIssue(issues, table, string.Join(',', missingColumns), description, recommendation);
    }

    private static async Task AuditUsersAsync(SqlConnection connection, List<AuditIssue> issues)
    {
        if (!await ColumnsExistAsync(connection, "TS_Users", "user_access_code", "email"))
        {
            AddUnexpectedShapeIssue(issues, "TS_Users", "user_access_code,email");
            return;
        }

        await AddGroupedIssuesAsync(
            connection,
            issues,
            "DUPLICATE_USER_EMAIL",
            "warning",
            "TS_Users",
            "email",
            """
            SELECT LOWER(LTRIM(RTRIM(email))) AS business_key,
                   COUNT_BIG(*) AS occurrences,
                   STRING_AGG(CONVERT(nvarchar(max), CONVERT(nvarchar(50), user_access_code)), N',') AS record_keys
            FROM dbo.TS_Users
            WHERE NULLIF(LTRIM(RTRIM(email)), N'') IS NOT NULL
            GROUP BY LOWER(LTRIM(RTRIM(email)))
            HAVING COUNT_BIG(*) > 1;
            """
        );

        await AddCountIssueAsync(
            connection,
            issues,
            "MISSING_USER_EMAIL",
            "warning",
            "TS_Users",
            "email",
            """
            SELECT COUNT_BIG(*)
            FROM dbo.TS_Users
            WHERE NULLIF(LTRIM(RTRIM(email)), N'') IS NULL;
            """,
            "User records have no email address for notification and Microsoft sign-in compatibility.",
            "Supply the correct email from an approved client data source; do not fabricate an address."
        );

        await AddCountIssueAsync(
            connection,
            issues,
            "INVALID_USER_EMAIL",
            "warning",
            "TS_Users",
            "email",
            """
            SELECT COUNT_BIG(*)
            FROM dbo.TS_Users
            WHERE NULLIF(LTRIM(RTRIM(email)), N'') IS NOT NULL
              AND email NOT LIKE N'%_@_%._%';
            """,
            "User records contain a value that does not pass the audit email shape check.",
            "Confirm the address with the client; the audit pattern is a triage signal, not a proof of deliverability."
        );
    }

    private static async Task AuditLegacyUsersAsync(
        SqlConnection connection,
        List<AuditIssue> issues,
        bool currentUsersAvailable
    )
    {
        if (!await ColumnsExistAsync(connection, "user_access_old1", "user_access_code", "E_Mail"))
        {
            AddUnexpectedShapeIssue(issues, "user_access_old1", "user_access_code,E_Mail");
            return;
        }

        await AddGroupedIssuesAsync(
            connection,
            issues,
            "DUPLICATE_LEGACY_USER_EMAIL",
            "warning",
            "user_access_old1",
            "E_Mail",
            """
            SELECT LOWER(LTRIM(RTRIM(E_Mail)) COLLATE DATABASE_DEFAULT) AS business_key,
                   COUNT_BIG(*) AS occurrences,
                   STRING_AGG(CONVERT(nvarchar(max), CONVERT(nvarchar(50), user_access_code)), N',') AS record_keys
            FROM dbo.user_access_old1
            WHERE NULLIF(LTRIM(RTRIM(E_Mail)), N'') IS NOT NULL
            GROUP BY LOWER(LTRIM(RTRIM(E_Mail)) COLLATE DATABASE_DEFAULT)
            HAVING COUNT_BIG(*) > 1;
            """
        );

        await AddCountIssueAsync(
            connection,
            issues,
            "MISSING_LEGACY_USER_EMAIL",
            "warning",
            "user_access_old1",
            "E_Mail",
            """
            SELECT COUNT_BIG(*)
            FROM dbo.user_access_old1
            WHERE NULLIF(LTRIM(RTRIM(E_Mail)), N'') IS NULL;
            """,
            "Legacy user records have no email address.",
            "Supply the correct email from an approved client data source; do not fabricate an address."
        );

        if (currentUsersAvailable)
        {
            await AddCountIssueAsync(
                connection,
                issues,
                "LEGACY_USER_WITHOUT_CURRENT_USER",
                "error",
                "user_access_old1",
                "user_access_code",
                """
                SELECT COUNT_BIG(*)
                FROM dbo.user_access_old1 legacy_user
                LEFT JOIN dbo.TS_Users current_users
                  ON current_users.user_access_code = legacy_user.user_access_code
                WHERE current_users.user_access_code IS NULL;
                """,
                "A legacy authentication record has no matching TS_Users record.",
                "Resolve the identity mapping manually before enabling Microsoft sign-in or password back-fix."
            );
        }
    }

    private static async Task AuditCredentialsAsync(
        SqlConnection connection,
        List<AuditIssue> issues,
        bool usersAvailable
    )
    {
        var requiredColumns = new[]
        {
            "credential_id",
            "user_access_code",
            "password_hash",
            "password_salt",
        };
        if (!await ColumnsExistAsync(connection, "Legacy_User_Credentials", requiredColumns))
        {
            AddUnexpectedShapeIssue(
                issues,
                "Legacy_User_Credentials",
                string.Join(',', requiredColumns)
            );
            return;
        }

        await AddGroupedIssuesAsync(
            connection,
            issues,
            "DUPLICATE_LEGACY_CREDENTIAL_USER",
            "error",
            "Legacy_User_Credentials",
            "user_access_code",
            """
            SELECT CONVERT(nvarchar(50), user_access_code) AS business_key,
                   COUNT_BIG(*) AS occurrences,
                   STRING_AGG(CONVERT(nvarchar(max), CONVERT(nvarchar(50), credential_id)), N',') AS record_keys
            FROM dbo.Legacy_User_Credentials
            GROUP BY user_access_code
            HAVING COUNT_BIG(*) > 1;
            """
        );

        await AddCountIssueAsync(
            connection,
            issues,
            "MISSING_LEGACY_CREDENTIAL_MATERIAL",
            "error",
            "Legacy_User_Credentials",
            "password_hash,password_salt",
            """
            SELECT COUNT_BIG(*)
            FROM dbo.Legacy_User_Credentials
            WHERE NULLIF(LTRIM(RTRIM(password_hash)), N'') IS NULL
               OR (
                    NULLIF(LTRIM(RTRIM(password_salt)), N'') IS NULL
                    AND LEFT(LTRIM(RTRIM(password_hash)), 4) NOT IN (N'$2a$', N'$2b$', N'$2y$')
               );
            """,
            "Credential records are missing a password hash or the external salt required by their hash format. BCrypt hashes embed their salt and do not require password_salt.",
            "Repair from an approved credential migration; never generate or print passwords in an audit."
        );

        if (usersAvailable)
        {
            await AddCountIssueAsync(
                connection,
                issues,
                "CREDENTIAL_WITHOUT_USER",
                "error",
                "Legacy_User_Credentials",
                "user_access_code",
                """
                SELECT COUNT_BIG(*)
                FROM dbo.Legacy_User_Credentials credentials
                LEFT JOIN dbo.TS_Users users
                  ON users.user_access_code = credentials.user_access_code
                WHERE users.user_access_code IS NULL;
                """,
                "A credential record points to no TS_Users identity.",
                "Resolve the identity manually before applying any password or account back-fix."
            );
        }
    }

    private static async Task AuditEntraMappingsAsync(
        SqlConnection connection,
        List<AuditIssue> issues,
        bool usersAvailable
    )
    {
        var requiredColumns = new[] { "mapping_id", "entra_object_id", "user_access_code" };
        if (!await ColumnsExistAsync(connection, "EntraId_User_Mapping", requiredColumns))
        {
            AddUnexpectedShapeIssue(
                issues,
                "EntraId_User_Mapping",
                string.Join(',', requiredColumns)
            );
            return;
        }

        await AddGroupedIssuesAsync(
            connection,
            issues,
            "DUPLICATE_ENTRA_OBJECT_ID",
            "error",
            "EntraId_User_Mapping",
            "entra_object_id",
            """
            SELECT LOWER(LTRIM(RTRIM(entra_object_id))) AS business_key,
                   COUNT_BIG(*) AS occurrences,
                   STRING_AGG(CONVERT(nvarchar(max), CONVERT(nvarchar(50), mapping_id)), N',') AS record_keys
            FROM dbo.EntraId_User_Mapping
            WHERE NULLIF(LTRIM(RTRIM(entra_object_id)), N'') IS NOT NULL
            GROUP BY LOWER(LTRIM(RTRIM(entra_object_id)))
            HAVING COUNT_BIG(*) > 1;
            """
        );

        await AddGroupedIssuesAsync(
            connection,
            issues,
            "DUPLICATE_ENTRA_USER_MAPPING",
            "error",
            "EntraId_User_Mapping",
            "user_access_code",
            """
            SELECT CONVERT(nvarchar(50), user_access_code) AS business_key,
                   COUNT_BIG(*) AS occurrences,
                   STRING_AGG(CONVERT(nvarchar(max), CONVERT(nvarchar(50), mapping_id)), N',') AS record_keys
            FROM dbo.EntraId_User_Mapping
            GROUP BY user_access_code
            HAVING COUNT_BIG(*) > 1;
            """
        );

        if (usersAvailable)
        {
            await AddCountIssueAsync(
                connection,
                issues,
                "ENTRA_MAPPING_WITHOUT_USER",
                "error",
                "EntraId_User_Mapping",
                "user_access_code",
                """
                SELECT COUNT_BIG(*)
                FROM dbo.EntraId_User_Mapping mappings
                LEFT JOIN dbo.TS_Users users
                  ON users.user_access_code = mappings.user_access_code
                WHERE users.user_access_code IS NULL;
                """,
                "An Entra mapping points to no TS_Users identity.",
                "Resolve the mapping to a canonical user before enabling Microsoft sign-in."
            );
        }
    }

    private static async Task AuditVehiclesAsync(SqlConnection connection, List<AuditIssue> issues)
    {
        var requiredColumns = new[] { "vmf_code", "fleet_number", "registration_number" };
        if (!await ColumnsExistAsync(connection, "vehicle_master", requiredColumns))
        {
            AddUnexpectedShapeIssue(issues, "vehicle_master", string.Join(',', requiredColumns));
            return;
        }

        await AddGroupedIssuesAsync(
            connection,
            issues,
            "DUPLICATE_VEHICLE_FLEET_NUMBER",
            "error",
            "vehicle_master",
            "fleet_number",
            """
            SELECT UPPER(LTRIM(RTRIM(fleet_number))) AS business_key,
                   COUNT_BIG(*) AS occurrences,
                   STRING_AGG(CONVERT(nvarchar(max), CONVERT(nvarchar(50), vmf_code)), N',') AS record_keys
            FROM dbo.vehicle_master
            WHERE NULLIF(LTRIM(RTRIM(fleet_number)), N'') IS NOT NULL
            GROUP BY UPPER(LTRIM(RTRIM(fleet_number)))
            HAVING COUNT_BIG(*) > 1;
            """
        );

        await AddGroupedIssuesAsync(
            connection,
            issues,
            "DUPLICATE_VEHICLE_REGISTRATION",
            "error",
            "vehicle_master",
            "registration_number",
            """
            SELECT UPPER(LTRIM(RTRIM(registration_number))) AS business_key,
                   COUNT_BIG(*) AS occurrences,
                   STRING_AGG(CONVERT(nvarchar(max), CONVERT(nvarchar(50), vmf_code)), N',') AS record_keys
            FROM dbo.vehicle_master
            WHERE NULLIF(LTRIM(RTRIM(registration_number)), N'') IS NOT NULL
            GROUP BY UPPER(LTRIM(RTRIM(registration_number)))
            HAVING COUNT_BIG(*) > 1;
            """
        );

        await AddCountIssueAsync(
            connection,
            issues,
            "MISSING_VEHICLE_FLEET_NUMBER",
            "warning",
            "vehicle_master",
            "fleet_number",
            """
            SELECT COUNT_BIG(*)
            FROM dbo.vehicle_master
            WHERE NULLIF(LTRIM(RTRIM(fleet_number)), N'') IS NULL;
            """,
            "Vehicle records are missing fleet numbers used for operational identification.",
            "Supply the verified fleet number; never generate a replacement identifier automatically."
        );

        await AddCountIssueAsync(
            connection,
            issues,
            "MISSING_VEHICLE_REGISTRATION",
            "warning",
            "vehicle_master",
            "registration_number",
            """
            SELECT COUNT_BIG(*)
            FROM dbo.vehicle_master
            WHERE NULLIF(LTRIM(RTRIM(registration_number)), N'') IS NULL;
            """,
            "Vehicle records are missing registration numbers.",
            "Supply the verified registration number; never generate a replacement identifier automatically."
        );
    }

    private static async Task AuditContractsAsync(
        SqlConnection connection,
        List<AuditIssue> issues,
        bool vehiclesAvailable,
        bool sitesAvailable
    )
    {
        var requiredColumns = new[] { "contract_code", "vmf_code", "site_code", "still_current" };
        if (!await ColumnsExistAsync(connection, "contract", requiredColumns))
        {
            AddUnexpectedShapeIssue(issues, "contract", string.Join(',', requiredColumns));
            return;
        }

        await AddGroupedIssuesAsync(
            connection,
            issues,
            "DUPLICATE_CURRENT_CONTRACT",
            "error",
            "contract",
            "vmf_code",
            """
            SELECT CONVERT(nvarchar(50), vmf_code) AS business_key,
                   COUNT_BIG(*) AS occurrences,
                   STRING_AGG(CONVERT(nvarchar(max), CONVERT(nvarchar(50), contract_code)), N',') AS record_keys
            FROM dbo.contract
            WHERE still_current = N'Y'
            GROUP BY vmf_code
            HAVING COUNT_BIG(*) > 1;
            """
        );

        await AddCountIssueAsync(
            connection,
            issues,
            "INVALID_CONTRACT_CURRENT_FLAG",
            "warning",
            "contract",
            "still_current",
            """
            SELECT COUNT_BIG(*)
            FROM dbo.contract
            WHERE NULLIF(LTRIM(RTRIM(still_current)), N'') IS NOT NULL
              AND still_current NOT IN (N'Y', N'N');
            """,
            "Contract records contain a current-state value outside the known legacy Y/N values.",
            "Confirm the intended state with the client before changing any contract history."
        );

        if (vehiclesAvailable)
        {
            await AddCountIssueAsync(
                connection,
                issues,
                "CONTRACT_WITHOUT_VEHICLE",
                "error",
                "contract",
                "vmf_code",
                """
                SELECT COUNT_BIG(*)
                FROM dbo.contract contracts
                LEFT JOIN dbo.vehicle_master vehicles
                  ON vehicles.vmf_code = contracts.vmf_code
                WHERE vehicles.vmf_code IS NULL;
                """,
                "A contract points to no vehicle_master record.",
                "Resolve the vehicle identity before repairing current-contract data."
            );
        }

        if (sitesAvailable)
        {
            await AddCountIssueAsync(
                connection,
                issues,
                "CONTRACT_WITHOUT_SITE",
                "warning",
                "contract",
                "site_code",
                """
                SELECT COUNT_BIG(*)
                FROM dbo.contract contracts
                LEFT JOIN dbo.site sites
                  ON sites.Site_code = contracts.site_code
                WHERE sites.Site_code IS NULL;
                """,
                "A contract points to no site record.",
                "Resolve the site identity before applying a contract back-fix."
            );
        }
    }

    private static async Task AuditSitesAsync(SqlConnection connection, List<AuditIssue> issues)
    {
        if (!await ColumnsExistAsync(connection, "site", "Site_code", "description"))
        {
            AddUnexpectedShapeIssue(issues, "site", "Site_code,description");
            return;
        }

        await AddCountIssueAsync(
            connection,
            issues,
            "MISSING_SITE_DESCRIPTION",
            "warning",
            "site",
            "description",
            """
            SELECT COUNT_BIG(*)
            FROM dbo.site
            WHERE NULLIF(LTRIM(RTRIM(description)), N'') IS NULL;
            """,
            "Site records are missing the description used in navigation and operational context.",
            "Supply the verified site description from an approved client source."
        );
    }

    private static async Task AuditDepartmentsAsync(
        SqlConnection connection,
        List<AuditIssue> issues
    )
    {
        if (!await ColumnsExistAsync(connection, "department", "department_code", "description"))
        {
            AddUnexpectedShapeIssue(issues, "department", "department_code,description");
            return;
        }

        await AddCountIssueAsync(
            connection,
            issues,
            "MISSING_DEPARTMENT_DESCRIPTION",
            "warning",
            "department",
            "description",
            """
            SELECT COUNT_BIG(*)
            FROM dbo.department
            WHERE NULLIF(LTRIM(RTRIM(description)), N'') IS NULL;
            """,
            "Department records are missing the description used in operational context.",
            "Supply the verified department description from an approved client source."
        );
    }

    private static async Task AuditCallCentreSchemaAsync(
        SqlConnection connection,
        List<AuditIssue> issues
    )
    {
        var incidentColumns = new[] { "Incident_type", "Incident_Desc" };
        foreach (var column in incidentColumns)
        {
            if (!await ColumnsExistAsync(connection, "Call_centre", column))
            {
                issues.Add(
                    new AuditIssue(
                        "SCHEMA_OPTIONAL_COLUMN_MISSING",
                        "warning",
                        "Call_centre",
                        column,
                        null,
                        1,
                        "The modern incident field is not present on the legacy Call_centre table.",
                        "Run the guarded additive compatibility migration before enabling incident editing."
                    )
                );
            }
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Audit SQL is fixed in this executable and never accepts SQL from the database or command line."
    )]
    private static async Task AddGroupedIssuesAsync(
        SqlConnection connection,
        List<AuditIssue> issues,
        string code,
        string severity,
        string table,
        string field,
        string sql
    )
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            issues.Add(
                new AuditIssue(
                    code,
                    severity,
                    table,
                    field,
                    reader.IsDBNull(0) ? null : reader.GetString(0),
                    reader.GetInt64(1),
                    $"The normalized {field} value occurs in multiple records.",
                    "Choose and record a canonical record in an approved back-fix plan; do not merge or delete automatically.",
                    reader.IsDBNull(2) ? null : reader.GetString(2)
                )
            );
        }
    }

    private static async Task AddCountIssueAsync(
        SqlConnection connection,
        List<AuditIssue> issues,
        string code,
        string severity,
        string table,
        string field,
        string sql,
        string description,
        string recommendation
    )
    {
        var count = await ExecuteCountAsync(connection, sql);
        if (count == 0)
            return;

        issues.Add(
            new AuditIssue(code, severity, table, field, null, count, description, recommendation)
        );
    }

    private static void AddUnexpectedShapeIssue(
        List<AuditIssue> issues,
        string table,
        string columns,
        string description = "A required audit column is missing, so this data rule was not evaluated.",
        string recommendation = "Compare the database to the client schema and resolve manually; the tool will not reshape it."
    )
    {
        issues.Add(
            new AuditIssue(
                "SCHEMA_UNEXPECTED_SHAPE",
                "error",
                table,
                columns,
                null,
                1,
                description,
                recommendation
            )
        );
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Audit SQL is fixed in this executable and never accepts SQL from the database or command line."
    )]
    private static async Task<long> ExecuteCountAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    private static async Task<bool> TableExistsAsync(SqlConnection connection, string tableName)
    {
        const string sql = """
            SELECT CASE WHEN OBJECT_ID(@table_name, N'U') IS NULL THEN 0 ELSE 1 END;
            """;
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@table_name", System.Data.SqlDbType.NVarChar, 258).Value =
            $"dbo.{tableName}";
        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture)
            == 1;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The query shape is fixed; only generated parameter names and parameter values are inserted."
    )]
    private static async Task<bool> ColumnsExistAsync(
        SqlConnection connection,
        string tableName,
        params string[] columnNames
    )
    {
        const string sql = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = N'dbo'
              AND TABLE_NAME = @table_name
              AND COLUMN_NAME IN ({0});
            """;

        var parameterNames = columnNames.Select((_, index) => $"@column_{index}").ToArray();
        var commandText = string.Format(
            CultureInfo.InvariantCulture,
            sql,
            string.Join(',', parameterNames)
        );
        await using var command = new SqlCommand(commandText, connection);
        command.Parameters.Add("@table_name", System.Data.SqlDbType.NVarChar, 128).Value =
            tableName;
        for (var index = 0; index < columnNames.Length; index++)
            command
                .Parameters.Add(parameterNames[index], System.Data.SqlDbType.NVarChar, 128)
                .Value = columnNames[index];

        return Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture)
            == columnNames.Length;
    }

    private static string ToText(AuditReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"FIS database audit: {report.DatabaseName}");
        builder.AppendLine($"Generated UTC: {report.GeneratedAtUtc:O}");
        builder.AppendLine($"Legacy DDL reference: {report.LegacyReferenceRoot ?? "unavailable"}");
        builder.AppendLine($"Errors: {report.ErrorCount}; warnings: {report.WarningCount}");
        builder.AppendLine();

        if (report.Issues.Count == 0)
        {
            builder.AppendLine("No issues found by the configured audit rules.");
            return builder.ToString();
        }

        foreach (var issue in report.Issues)
        {
            builder.AppendLine(
                $"[{issue.Severity.ToUpperInvariant()}] {issue.IssueCode} {issue.TableName}.{issue.FieldName} "
                    + $"count={issue.Occurrences} key={issue.BusinessKey ?? "-"} records={issue.RecordKeys ?? "-"}"
            );
            builder.AppendLine($"  {issue.Description}");
            builder.AppendLine($"  Action: {issue.RecommendedAction}");
        }

        return builder.ToString();
    }

    private static string ToCsv(IEnumerable<AuditIssue> issues)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "issue_code,severity,table_name,field_name,business_key,occurrences,record_keys,description,recommended_action"
        );
        foreach (var issue in issues)
        {
            builder.AppendLine(
                string.Join(
                    ',',
                    Csv(issue.IssueCode),
                    Csv(issue.Severity),
                    Csv(issue.TableName),
                    Csv(issue.FieldName),
                    Csv(issue.BusinessKey),
                    issue.Occurrences.ToString(CultureInfo.InvariantCulture),
                    Csv(issue.RecordKeys),
                    Csv(issue.Description),
                    Csv(issue.RecommendedAction)
                )
            );
        }

        return builder.ToString();
    }

    private static string Csv(string? value)
    {
        if (value is null)
            return string.Empty;
        return
            value.Contains(',', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            ? $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\""
            : value;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("FIS read-only database audit tool");
        Console.WriteLine();
        Console.WriteLine(
            "  dotnet run --project src/Tools/DatabaseAuditTool -- --format=json --output=audit.json"
        );
        Console.WriteLine();
        Console.WriteLine("Connection: set ConnectionStrings__Default to a real target database.");
        Console.WriteLine(
            "Legacy DDL: set FIS_LEGACY_DDL_ROOT or pass --ddl-root=backup/sources/GGMT.Database/SQLScripts/v2.0.0."
        );
        Console.WriteLine(
            "Exit codes: 0 clean, 2 data-quality issues found, 1 audit could not run."
        );
        Console.WriteLine(
            "The audit tool only runs SELECT/metadata queries and never changes the database."
        );
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed record AuditOptions(
        string Format,
        string? OutputPath,
        string? LegacyDdlRoot,
        bool ShowHelp
    );

    private sealed record TableSpec(string Name, bool Required);

    private sealed record AuditReport(
        DateTimeOffset GeneratedAtUtc,
        string DatabaseName,
        string? LegacyReferenceRoot,
        IReadOnlyList<AuditIssue> Issues,
        int ErrorCount,
        int WarningCount
    );

    private sealed record AuditIssue(
        string IssueCode,
        string Severity,
        string TableName,
        string? FieldName,
        string? BusinessKey,
        long Occurrences,
        string Description,
        string RecommendedAction,
        string? RecordKeys = null
    );
}
