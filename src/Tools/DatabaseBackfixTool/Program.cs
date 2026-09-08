using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace FIS.Tools.DatabaseBackfixTool;

public static class Program
{
    private const string ApplyConfirmation = "FIS-REVIEWED-DATA-FIX";
    private const string ProductionGate = "I_UNDERSTAND_REVIEWED_DATA_FIX";
    private const string LockResource = "FIS.Database.ReviewedDataFix";

    private static readonly IReadOnlyDictionary<string, FixTarget> AllowedTargets = new Dictionary<
        string,
        FixTarget
    >(StringComparer.OrdinalIgnoreCase)
    {
        ["TS_Users.email"] = new("TS_Users", "user_access_code", "email", true),
        ["TS_Users.tel_no"] = new("TS_Users", "user_access_code", "tel_no", false),
        ["user_access_old1.E_Mail"] = new("user_access_old1", "user_access_code", "E_Mail", true),
        ["user_access_old1.telephone"] = new(
            "user_access_old1",
            "user_access_code",
            "telephone",
            false
        ),
        ["vehicle_master.fleet_number"] = new("vehicle_master", "vmf_code", "fleet_number", true),
        ["vehicle_master.registration_number"] = new(
            "vehicle_master",
            "vmf_code",
            "registration_number",
            true
        ),
        ["site.description"] = new("site", "Site_code", "description", false),
        ["department.description"] = new("department", "department_code", "description", false),
        ["EntraId_User_Mapping.entra_object_id"] = new(
            "EntraId_User_Mapping",
            "mapping_id",
            "entra_object_id",
            true
        ),
    };

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

            var plan = await LoadPlanAsync(options.PlanPath);
            ValidatePlan(plan);

            var connectionString = ResolveRequiredConnectionString();
            var environmentName = ResolveEnvironmentName();
            ValidateConnectionString(connectionString);

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            Console.Error.WriteLine(
                $"Connected to database '{connection.Database}' on '{connection.DataSource}'."
            );

            var planHash = Convert.ToHexString(
                SHA256.HashData(await File.ReadAllBytesAsync(options.PlanPath))
            );
            if (!options.Apply)
            {
                await ValidatePlanAgainstDatabaseAsync(connection, plan);
                Console.WriteLine(
                    $"Plan '{plan.PlanId}' is valid for review ({plan.Items.Count} item(s), hash {planHash})."
                );
                Console.WriteLine(
                    "No database changes were made. Use the guarded apply command after approval."
                );
                return;
            }

            ValidateApplyAuthorization(options, environmentName);
            await ApplyPlanAsync(connection, plan, planHash);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Database back-fix failed: {exception.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static BackfixOptions ParseOptions(string[] args)
    {
        var apply = false;
        var showHelp = false;
        string? confirmation = null;
        string? planPath = null;

        foreach (var argument in args)
        {
            switch (argument)
            {
                case "plan":
                case "validate":
                    break;
                case "apply":
                case "--apply":
                    apply = true;
                    break;
                case "--help":
                case "-h":
                    showHelp = true;
                    break;
                default:
                    if (argument.StartsWith("--plan=", StringComparison.Ordinal))
                    {
                        planPath = argument["--plan=".Length..];
                        break;
                    }

                    if (argument.StartsWith("--confirm=", StringComparison.Ordinal))
                    {
                        confirmation = argument["--confirm=".Length..];
                        break;
                    }

                    throw new InvalidOperationException($"Unknown option '{argument}'.");
            }
        }

        if (!showHelp && string.IsNullOrWhiteSpace(planPath))
            throw new InvalidOperationException(
                "A reviewed JSON plan is required with --plan=PATH."
            );

        return new BackfixOptions(apply, confirmation, planPath ?? string.Empty, showHelp);
    }

    private static async Task<BackfixPlan> LoadPlanAsync(string planPath)
    {
        if (!File.Exists(planPath))
            throw new InvalidOperationException($"Back-fix plan '{planPath}' does not exist.");

        await using var stream = File.OpenRead(planPath);
        var plan = await JsonSerializer.DeserializeAsync<BackfixPlan>(stream, JsonOptions);
        return plan ?? throw new InvalidOperationException("The back-fix plan is empty.");
    }

    private static void ValidatePlan(BackfixPlan plan)
    {
        if (string.IsNullOrWhiteSpace(plan.PlanId) || plan.PlanId.Length > 200)
            throw new InvalidOperationException(
                "PlanId is required and must be at most 200 characters."
            );

        if (string.IsNullOrWhiteSpace(plan.ApprovedBy) || plan.ApprovedBy.Length > 256)
            throw new InvalidOperationException(
                "ApprovedBy is required and must be at most 256 characters."
            );

        if (plan.Items.Count == 0)
            throw new InvalidOperationException(
                "The back-fix plan must contain at least one reviewed item."
            );

        var fixIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in plan.Items)
        {
            if (string.IsNullOrWhiteSpace(item.FixId) || !fixIds.Add(item.FixId))
                throw new InvalidOperationException(
                    "Every back-fix item must have a unique FixId."
                );

            var targetKey = $"{item.Table}.{item.Column}";
            if (
                !AllowedTargets.TryGetValue(targetKey, out var target)
                || !string.Equals(
                    item.KeyColumn,
                    target.KeyColumn,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                throw new InvalidOperationException(
                    $"Back-fix target '{targetKey}' or key '{item.KeyColumn}' is not allow-listed."
                );
            }

            if (string.IsNullOrWhiteSpace(item.KeyValue))
                throw new InvalidOperationException(
                    $"Back-fix item '{item.FixId}' requires a key value."
                );

            if (item.ExpectNull && item.ExpectedOldValue is not null)
                throw new InvalidOperationException(
                    $"Back-fix item '{item.FixId}' cannot specify both ExpectNull and ExpectedOldValue."
                );

            if (!item.ExpectNull && item.ExpectedOldValue is null)
                throw new InvalidOperationException(
                    $"Back-fix item '{item.FixId}' must specify ExpectedOldValue or ExpectNull=true."
                );

            if (string.IsNullOrWhiteSpace(item.NewValue))
                throw new InvalidOperationException(
                    $"Back-fix item '{item.FixId}' requires a non-empty NewValue."
                );

            if (item.NewValue.Length > 4000)
                throw new InvalidOperationException(
                    $"Back-fix item '{item.FixId}' NewValue is too long."
                );

            if (target.UniqueNormalized && !LooksLikeValidUniqueValue(target.Column, item.NewValue))
                throw new InvalidOperationException(
                    $"Back-fix item '{item.FixId}' has an invalid value for the unique {target.Column} field."
                );
        }
    }

    private static bool LooksLikeValidUniqueValue(string column, string value)
    {
        if (column.Contains("email", StringComparison.OrdinalIgnoreCase))
            return value.Contains('@', StringComparison.Ordinal)
                && value.Contains('.', StringComparison.Ordinal);

        return true;
    }

    private static async Task ValidatePlanAgainstDatabaseAsync(
        SqlConnection connection,
        BackfixPlan plan
    )
    {
        foreach (var item in plan.Items)
        {
            var target = ResolveTarget(item);
            var current = await ReadCurrentValueAsync(connection, target, item.KeyValue);
            ValidateExpectedValue(item, current);
            await EnsureNoNormalizedConflictAsync(connection, target, item.KeyValue, item.NewValue);
        }
    }

    private static async Task ApplyPlanAsync(
        SqlConnection connection,
        BackfixPlan plan,
        string planHash
    )
    {
        await AcquireLockAsync(connection);
        try
        {
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            try
            {
                await EnsureAuditTableAsync(connection, transaction);
                var existingHash = await ReadAppliedPlanHashAsync(
                    connection,
                    transaction,
                    plan.PlanId
                );
                if (existingHash is not null)
                {
                    if (!string.Equals(existingHash, planHash, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"PlanId '{plan.PlanId}' was already recorded with a different file hash."
                        );
                    }

                    await transaction.CommitAsync();
                    Console.WriteLine(
                        $"Plan '{plan.PlanId}' was already applied; no changes were made."
                    );
                    return;
                }

                foreach (var item in plan.Items)
                {
                    var target = ResolveTarget(item);
                    var current = await ReadCurrentValueAsync(
                        connection,
                        target,
                        item.KeyValue,
                        transaction
                    );
                    ValidateExpectedValue(item, current);
                    await EnsureNoNormalizedConflictAsync(
                        connection,
                        target,
                        item.KeyValue,
                        item.NewValue,
                        transaction
                    );
                    await UpdateValueAsync(connection, transaction, target, item);
                    await RecordAuditAsync(
                        connection,
                        transaction,
                        plan,
                        planHash,
                        target,
                        item,
                        current
                    );
                }

                await transaction.CommitAsync();
                Console.WriteLine(
                    $"Applied reviewed plan '{plan.PlanId}' ({plan.Items.Count} item(s))."
                );
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        finally
        {
            await ReleaseLockAsync(connection);
        }
    }

    private static FixTarget ResolveTarget(BackfixItem item) =>
        AllowedTargets[$"{item.Table}.{item.Column}"];

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The table and column identifiers are selected only from the fixed allow-list. Values are parameterized."
    )]
    private static async Task<string?> ReadCurrentValueAsync(
        SqlConnection connection,
        FixTarget target,
        string keyValue,
        SqlTransaction? transaction = null
    )
    {
        var sql = $"""
            SELECT CONVERT(nvarchar(max), [{target.Column}])
            FROM dbo.[{target.Table}]
            WHERE [{target.KeyColumn}] = @key_value;
            """;
        await using var command = new SqlCommand(sql, connection, transaction)
        {
            CommandTimeout = 120,
        };
        command.Parameters.Add("@key_value", System.Data.SqlDbType.Int).Value = ParseKeyValue(
            keyValue
        );
        await using var reader = await command.ExecuteReaderAsync();
        string? value = null;
        var rows = 0;
        while (await reader.ReadAsync())
        {
            rows++;
            if (rows > 1)
                throw new InvalidOperationException(
                    $"Key '{keyValue}' returned more than one target record."
                );
            value = reader.IsDBNull(0) ? null : reader.GetString(0);
        }

        if (rows == 0)
            throw new InvalidOperationException(
                $"Key '{keyValue}' was not found in dbo.{target.Table}."
            );

        return value;
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The table and column identifiers are selected only from the fixed allow-list. Values are parameterized."
    )]
    private static async Task EnsureNoNormalizedConflictAsync(
        SqlConnection connection,
        FixTarget target,
        string keyValue,
        string newValue,
        SqlTransaction? transaction = null
    )
    {
        if (!target.UniqueNormalized)
            return;

        var sql = $"""
            SELECT COUNT_BIG(*)
            FROM dbo.[{target.Table}]
            WHERE LOWER(LTRIM(RTRIM([{target.Column}]))) = LOWER(LTRIM(RTRIM(@new_value)))
              AND [{target.KeyColumn}] <> @key_value;
            """;
        await using var command = new SqlCommand(sql, connection, transaction)
        {
            CommandTimeout = 120,
        };
        command.Parameters.Add("@new_value", System.Data.SqlDbType.NVarChar, 4000).Value = newValue;
        command.Parameters.Add("@key_value", System.Data.SqlDbType.Int).Value = ParseKeyValue(
            keyValue
        );
        var count = Convert.ToInt64(
            await command.ExecuteScalarAsync(),
            CultureInfo.InvariantCulture
        );
        if (count > 0)
        {
            throw new InvalidOperationException(
                $"The reviewed value for dbo.{target.Table}.{target.Column} would create another normalized duplicate."
            );
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The table and column identifiers are selected only from the fixed allow-list. Values are parameterized."
    )]
    private static async Task UpdateValueAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        FixTarget target,
        BackfixItem item
    )
    {
        var sql = $"""
            UPDATE dbo.[{target.Table}]
            SET [{target.Column}] = @new_value
            WHERE [{target.KeyColumn}] = @key_value
              AND ((@expect_null = 1 AND [{target.Column}] IS NULL)
                   OR (@expect_null = 0 AND [{target.Column}] = @expected_old_value));
            """;
        await using var command = new SqlCommand(sql, connection, transaction)
        {
            CommandTimeout = 120,
        };
        command.Parameters.Add("@new_value", System.Data.SqlDbType.NVarChar, 4000).Value =
            item.NewValue;
        command.Parameters.Add("@key_value", System.Data.SqlDbType.Int).Value = ParseKeyValue(
            item.KeyValue
        );
        command.Parameters.Add("@expect_null", System.Data.SqlDbType.Bit).Value = item.ExpectNull;
        command.Parameters.Add("@expected_old_value", System.Data.SqlDbType.NVarChar, 4000).Value =
            (object?)item.ExpectedOldValue ?? DBNull.Value;
        if (await command.ExecuteNonQueryAsync() != 1)
        {
            throw new InvalidOperationException(
                $"Back-fix item '{item.FixId}' changed since the plan was reviewed; no value was overwritten."
            );
        }
    }

    private static void ValidateExpectedValue(BackfixItem item, string? current)
    {
        if (item.ExpectNull)
        {
            if (current is not null)
                throw new InvalidOperationException(
                    $"Back-fix item '{item.FixId}' expected NULL but found a value."
                );
            return;
        }

        if (!string.Equals(current, item.ExpectedOldValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Back-fix item '{item.FixId}' expected a different current value; no value was changed."
            );
        }
    }

    private static async Task EnsureAuditTableAsync(
        SqlConnection connection,
        SqlTransaction transaction
    )
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.fis_data_fix_audit', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.fis_data_fix_audit
                (
                    audit_id BIGINT IDENTITY(1, 1) NOT NULL,
                    run_id UNIQUEIDENTIFIER NOT NULL,
                    plan_id NVARCHAR(200) NOT NULL,
                    plan_sha256 CHAR(64) NOT NULL,
                    fix_id NVARCHAR(200) NOT NULL,
                    table_name NVARCHAR(128) NOT NULL,
                    key_column NVARCHAR(128) NOT NULL,
                    key_value NVARCHAR(256) NOT NULL,
                    column_name NVARCHAR(128) NOT NULL,
                    old_value NVARCHAR(MAX) NULL,
                    new_value NVARCHAR(MAX) NOT NULL,
                    approved_by NVARCHAR(256) NOT NULL,
                    applied_at_utc DATETIME2(7) NOT NULL CONSTRAINT DF_fis_data_fix_audit_applied_at_utc DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT PK_fis_data_fix_audit PRIMARY KEY (audit_id)
                );
                CREATE INDEX IX_fis_data_fix_audit_plan ON dbo.fis_data_fix_audit(plan_id, plan_sha256);
            END
            ELSE IF (
                SELECT COUNT(*)
                FROM sys.columns
                WHERE object_id = OBJECT_ID(N'dbo.fis_data_fix_audit')
                  AND name IN
                  (
                      N'audit_id', N'run_id', N'plan_id', N'plan_sha256', N'fix_id',
                      N'table_name', N'key_column', N'key_value', N'column_name',
                      N'old_value', N'new_value', N'approved_by', N'applied_at_utc'
                  )
            ) <> 13
            BEGIN
                THROW 51020, 'dbo.fis_data_fix_audit exists with an unexpected shape; manual review is required.', 1;
            END
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ReadAppliedPlanHashAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string planId
    )
    {
        const string sql = """
            SELECT TOP (1) plan_sha256
            FROM dbo.fis_data_fix_audit
            WHERE plan_id = @plan_id;
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@plan_id", System.Data.SqlDbType.NVarChar, 200).Value = planId;
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull
            ? null
            : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The audit table and columns are fixed; all data values are parameterized."
    )]
    private static async Task RecordAuditAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        BackfixPlan plan,
        string planHash,
        FixTarget target,
        BackfixItem item,
        string? oldValue
    )
    {
        const string sql = """
            INSERT INTO dbo.fis_data_fix_audit
                (run_id, plan_id, plan_sha256, fix_id, table_name, key_column, key_value,
                 column_name, old_value, new_value, approved_by)
            VALUES
                (@run_id, @plan_id, @plan_sha256, @fix_id, @table_name, @key_column, @key_value,
                 @column_name, @old_value, @new_value, @approved_by);
            """;
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@run_id", System.Data.SqlDbType.UniqueIdentifier).Value =
            plan.RunId;
        command.Parameters.Add("@plan_id", System.Data.SqlDbType.NVarChar, 200).Value = plan.PlanId;
        command.Parameters.Add("@plan_sha256", System.Data.SqlDbType.Char, 64).Value = planHash;
        command.Parameters.Add("@fix_id", System.Data.SqlDbType.NVarChar, 200).Value = item.FixId;
        command.Parameters.Add("@table_name", System.Data.SqlDbType.NVarChar, 128).Value =
            target.Table;
        command.Parameters.Add("@key_column", System.Data.SqlDbType.NVarChar, 128).Value =
            target.KeyColumn;
        command.Parameters.Add("@key_value", System.Data.SqlDbType.NVarChar, 256).Value =
            item.KeyValue;
        command.Parameters.Add("@column_name", System.Data.SqlDbType.NVarChar, 128).Value =
            target.Column;
        command.Parameters.Add("@old_value", System.Data.SqlDbType.NVarChar, -1).Value =
            (object?)oldValue ?? DBNull.Value;
        command.Parameters.Add("@new_value", System.Data.SqlDbType.NVarChar, -1).Value =
            item.NewValue;
        command.Parameters.Add("@approved_by", System.Data.SqlDbType.NVarChar, 256).Value =
            plan.ApprovedBy;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AcquireLockAsync(SqlConnection connection)
    {
        const string sql = """
            DECLARE @result INT;
            EXEC @result = sp_getapplock
                @Resource = N'FIS.Database.ReviewedDataFix',
                @LockMode = N'Exclusive',
                @LockOwner = N'Session',
                @LockTimeout = 0;
            SELECT @result;
            """;
        await using var command = new SqlCommand(sql, connection);
        if (Convert.ToInt32(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture) < 0)
            throw new InvalidOperationException(
                "Another reviewed data-fix run is active; try again later."
            );
    }

    private static async Task ReleaseLockAsync(SqlConnection connection)
    {
        try
        {
            const string sql =
                "EXEC sp_releaseapplock @Resource = N'FIS.Database.ReviewedDataFix', @LockOwner = N'Session';";
            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
            // Closing the connection releases a session-owned application lock.
        }
    }

    private static int ParseKeyValue(string keyValue)
    {
        if (
            !int.TryParse(
                keyValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var parsed
            )
        )
            throw new InvalidOperationException(
                $"Key value '{keyValue}' is not a valid integer key."
            );
        return parsed;
    }

    private static string ResolveRequiredConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings__Default must be set explicitly."
            );
        return connectionString;
    }

    private static string ResolveEnvironmentName() =>
        Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? "Production";

    private static void ValidateConnectionString(string connectionString)
    {
        if (
            connectionString.Contains("YOUR_DB_", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("192.0.2.10", StringComparison.OrdinalIgnoreCase)
        )
            throw new InvalidOperationException(
                "The back-fix tool rejected a placeholder database connection."
            );

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

        if (
            string.IsNullOrWhiteSpace(builder.DataSource)
            || string.IsNullOrWhiteSpace(builder.InitialCatalog)
        )
            throw new InvalidOperationException(
                "ConnectionStrings__Default must specify a server and database."
            );

        if (
            new[] { "master", "model", "msdb", "tempdb" }.Contains(
                builder.InitialCatalog,
                StringComparer.OrdinalIgnoreCase
            )
        )
            throw new InvalidOperationException(
                "The back-fix tool refuses to target a SQL Server system database."
            );
    }

    private static void ValidateApplyAuthorization(BackfixOptions options, string environmentName)
    {
        if (!string.Equals(options.Confirmation, ApplyConfirmation, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Apply is guarded. Re-run with --confirm={ApplyConfirmation}."
            );

        if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
            return;

        if (
            !string.Equals(
                Environment.GetEnvironmentVariable("FIS_ALLOW_PRODUCTION_DATA_FIXES"),
                ProductionGate,
                StringComparison.Ordinal
            )
        )
            throw new InvalidOperationException(
                "Non-development data fixes require FIS_ALLOW_PRODUCTION_DATA_FIXES=I_UNDERSTAND_REVIEWED_DATA_FIX."
            );
    }

    private static void PrintUsage()
    {
        Console.WriteLine("FIS reviewed database back-fix tool");
        Console.WriteLine();
        Console.WriteLine("  plan --plan=reviewed-fix.json");
        Console.WriteLine("  apply --plan=reviewed-fix.json --confirm=FIS-REVIEWED-DATA-FIX");
        Console.WriteLine();
        Console.WriteLine("For non-development environments also set:");
        Console.WriteLine("  FIS_ALLOW_PRODUCTION_DATA_FIXES=I_UNDERSTAND_REVIEWED_DATA_FIX");
        Console.WriteLine();
        Console.WriteLine(
            "Only allow-listed non-key fields can be changed. Deletes, merges, fabricated values, and key changes are unsupported."
        );
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed record BackfixOptions(
        bool Apply,
        string? Confirmation,
        string PlanPath,
        bool ShowHelp
    );

    private sealed record FixTarget(
        string Table,
        string KeyColumn,
        string Column,
        bool UniqueNormalized
    );

    private sealed class BackfixPlan
    {
        public string PlanId { get; init; } = string.Empty;
        public Guid RunId { get; init; } = Guid.NewGuid();
        public string ApprovedBy { get; init; } = string.Empty;
        public List<BackfixItem> Items { get; init; } = [];
    }

    private sealed class BackfixItem
    {
        public string FixId { get; init; } = string.Empty;
        public string Table { get; init; } = string.Empty;
        public string KeyColumn { get; init; } = string.Empty;
        public string KeyValue { get; init; } = string.Empty;
        public string Column { get; init; } = string.Empty;
        public string? ExpectedOldValue { get; init; }
        public bool ExpectNull { get; init; }
        public string NewValue { get; init; } = string.Empty;
        public string Reason { get; init; } = string.Empty;
    }
}
