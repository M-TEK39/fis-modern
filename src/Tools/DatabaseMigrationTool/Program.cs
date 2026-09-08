using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace FIS.Tools.DatabaseMigrationTool;

public static class Program
{
    private const string ApplyConfirmation = "FIS-ADDITIVE-ONLY";
    private const string ProductionGate = "I_UNDERSTAND_ADDITIVE_ONLY";
    private const string LedgerTable = "dbo.fis_schema_migrations";

    private static readonly MigrationDefinition[] Manifest =
    [
        new("001_modern_compatibility_additive", "001_modern_compatibility_additive.sql"),
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
            var environmentName = ResolveEnvironmentName();
            ValidateConnectionString(connectionString, environmentName);
            var migrations = LoadManifest();

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            Console.WriteLine($"Connected to database '{connection.Database}'.");

            if (!options.Apply)
            {
                await PrintPlanAsync(connection, migrations);
                return;
            }

            ValidateApplyAuthorization(options, environmentName);
            await ApplyMigrationsAsync(connection, migrations, environmentName);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Migration command failed: {exception.Message}");
            Environment.ExitCode = 1;
        }
    }

    private static MigrationOptions ParseOptions(string[] args)
    {
        var apply = false;
        var showHelp = false;
        string? confirmation = null;

        foreach (var argument in args)
        {
            switch (argument)
            {
                case "plan":
                case "status":
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
                    if (argument.StartsWith("--confirm=", StringComparison.Ordinal))
                    {
                        confirmation = argument["--confirm=".Length..];
                        break;
                    }

                    throw new InvalidOperationException(
                        $"Unknown option '{argument}'. Only plan/status or the guarded apply command is supported."
                    );
            }
        }

        return new MigrationOptions(apply, confirmation, showHelp);
    }

    private static string ResolveRequiredConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings__Default must be set explicitly. The migration tool never supplies a fallback connection string."
            );
        }

        return connectionString;
    }

    private static string ResolveEnvironmentName() =>
        Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? "Production";

    private static void ValidateConnectionString(string connectionString, string environmentName)
    {
        if (
            connectionString.Contains("YOUR_DB_", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("192.0.2.10", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new InvalidOperationException(
                "The migration tool rejected a documentation or placeholder database connection."
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

        var systemDatabaseNames = new[] { "master", "model", "msdb", "tempdb" };
        if (systemDatabaseNames.Contains(builder.InitialCatalog, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "The migration tool refuses to target a SQL Server system database."
            );

        if (!builder.IntegratedSecurity && string.IsNullOrWhiteSpace(builder.UserID))
        {
            throw new InvalidOperationException(
                $"The {environmentName} connection must specify a database user or use integrated security."
            );
        }
    }

    private static void ValidateApplyAuthorization(MigrationOptions options, string environmentName)
    {
        if (!string.Equals(options.Confirmation, ApplyConfirmation, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Apply is guarded. Re-run with --confirm={ApplyConfirmation}."
            );
        }

        if (string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
            return;

        var productionGate = Environment.GetEnvironmentVariable("FIS_ALLOW_PRODUCTION_MIGRATIONS");
        if (!string.Equals(productionGate, ProductionGate, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Non-development migration requires FIS_ALLOW_PRODUCTION_MIGRATIONS=I_UNDERSTAND_ADDITIVE_ONLY."
            );
        }

        Console.WriteLine("Production gate accepted: additive migrations only.");
    }

    private static MigrationDefinition[] LoadManifest()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames();
        var definitions = new List<MigrationDefinition>(Manifest.Length);

        foreach (var expected in Manifest)
        {
            var resourceName = resources.SingleOrDefault(name =>
                name.EndsWith(
                    $".Migrations.{expected.FileName}",
                    StringComparison.OrdinalIgnoreCase
                )
            );

            if (resourceName is null)
                throw new InvalidOperationException(
                    $"Approved migration resource '{expected.FileName}' is missing."
                );

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
                throw new InvalidOperationException(
                    $"Approved migration resource '{expected.FileName}' cannot be read."
                );

            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: true
            );
            var sql = reader.ReadToEnd();
            ValidateAdditiveScript(expected.FileName, sql);
            definitions.Add(expected with { ResourceName = resourceName, Sql = sql });
        }

        return definitions.ToArray();
    }

    private static void ValidateAdditiveScript(string fileName, string sql)
    {
        var forbiddenFragments = new[]
        {
            "CREATE DATABASE",
            "ALTER DATABASE",
            "DROP ",
            "TRUNCATE ",
            "DELETE ",
            "UPDATE ",
            "MERGE ",
            "SP_RENAME",
            "DBCC ",
        };

        foreach (var fragment in forbiddenFragments)
        {
            if (sql.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Approved migration '{fileName}' contains non-additive SQL fragment '{fragment.Trim()}'."
                );
            }
        }

        if (sql.Contains("GO", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Approved migration '{fileName}' must not contain batch separators."
            );
    }

    private static async Task PrintPlanAsync(
        SqlConnection connection,
        IReadOnlyCollection<MigrationDefinition> migrations
    )
    {
        var ledgerExists = await TableExistsAsync(connection, LedgerTable);
        var applied = ledgerExists
            ? await ReadAppliedMigrationsAsync(connection)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        Console.WriteLine("Migration plan (read-only):");
        if (!ledgerExists)
            Console.WriteLine(
                $"  {LedgerTable} is absent; it will be created only by a guarded apply."
            );

        var pending = 0;
        foreach (var migration in migrations)
        {
            var hash = ComputeHash(migration.Sql!);
            if (applied.TryGetValue(migration.Id, out var appliedHash))
            {
                if (!string.Equals(appliedHash, hash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Applied migration '{migration.Id}' has a different script hash; refusing to continue."
                    );
                }

                Console.WriteLine($"  applied  {migration.Id}  {hash}");
            }
            else
            {
                pending++;
                Console.WriteLine($"  pending  {migration.Id}  {hash}");
            }
        }

        Console.WriteLine($"Pending migrations: {pending}.");
        Console.WriteLine(
            "No database changes were made. Use the guarded apply command after backup and review."
        );
    }

    private static async Task ApplyMigrationsAsync(
        SqlConnection connection,
        IReadOnlyCollection<MigrationDefinition> migrations,
        string environmentName
    )
    {
        await AcquireMigrationLockAsync(connection);
        try
        {
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            try
            {
                await EnsureLedgerAsync(connection, transaction);
                await SetMigrationContextAsync(connection, transaction);
                var applied = await ReadAppliedMigrationsAsync(connection, transaction);
                var pending = 0;

                foreach (var migration in migrations)
                {
                    var hash = ComputeHash(migration.Sql!);
                    if (applied.TryGetValue(migration.Id, out var appliedHash))
                    {
                        if (!string.Equals(appliedHash, hash, StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException(
                                $"Applied migration '{migration.Id}' has a different script hash; refusing to continue."
                            );
                        }

                        Console.WriteLine($"  applied  {migration.Id}");
                        continue;
                    }

                    pending++;
                    Console.WriteLine($"  applying {migration.Id}...");
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    await ExecuteScriptAsync(connection, transaction, migration.Sql!);
                    stopwatch.Stop();
                    await RecordMigrationAsync(
                        connection,
                        transaction,
                        migration.Id,
                        hash,
                        stopwatch.ElapsedMilliseconds
                    );
                    Console.WriteLine(
                        $"  applied  {migration.Id} ({stopwatch.ElapsedMilliseconds} ms)"
                    );
                }

                await transaction.CommitAsync();
                Console.WriteLine(
                    pending == 0
                        ? $"Database is already current; no changes were required in {environmentName}."
                        : $"Applied {pending} additive migration(s) successfully."
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
            await ReleaseMigrationLockAsync(connection);
        }
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "Only embedded, manifest-listed, additive SQL is accepted after forbidden-fragment validation."
    )]
    private static async Task ExecuteScriptAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql
    )
    {
        await using var command = new SqlCommand(sql, connection, transaction)
        {
            CommandTimeout = 120,
        };
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetMigrationContextAsync(
        SqlConnection connection,
        SqlTransaction transaction
    )
    {
        const string sql =
            "EXEC sys.sp_set_session_context @key = N'FIS_MIGRATION_RUNNER', @value = N'FIS-ADDITIVE-ONLY';";
        await using var command = new SqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task EnsureLedgerAsync(
        SqlConnection connection,
        SqlTransaction transaction
    )
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.fis_schema_migrations', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.fis_schema_migrations
                (
                    migration_id NVARCHAR(200) NOT NULL,
                    script_sha256 CHAR(64) NOT NULL,
                    applied_at_utc DATETIME2(7) NOT NULL CONSTRAINT DF_fis_schema_migrations_applied_at_utc DEFAULT SYSUTCDATETIME(),
                    applied_by NVARCHAR(256) NOT NULL,
                    duration_ms BIGINT NOT NULL,
                    CONSTRAINT PK_fis_schema_migrations PRIMARY KEY (migration_id)
                );
            END
            ELSE IF (
                SELECT COUNT(*)
                FROM sys.columns
                WHERE object_id = OBJECT_ID(N'dbo.fis_schema_migrations')
                  AND name IN (N'migration_id', N'script_sha256', N'applied_at_utc', N'applied_by', N'duration_ms')
            ) <> 5
            BEGIN
                THROW 51001, 'dbo.fis_schema_migrations exists with an unexpected shape; manual review is required.', 1;
            END
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RecordMigrationAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string migrationId,
        string hash,
        long durationMilliseconds
    )
    {
        const string sql = """
            INSERT INTO dbo.fis_schema_migrations
                (migration_id, script_sha256, applied_by, duration_ms)
            VALUES
                (@migration_id, @script_sha256, @applied_by, @duration_ms);
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@migration_id", System.Data.SqlDbType.NVarChar, 200).Value =
            migrationId;
        command.Parameters.Add("@script_sha256", System.Data.SqlDbType.Char, 64).Value = hash;
        command.Parameters.Add("@applied_by", System.Data.SqlDbType.NVarChar, 256).Value =
            ResolveOperatorName();
        command.Parameters.Add("@duration_ms", System.Data.SqlDbType.BigInt).Value =
            durationMilliseconds;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<Dictionary<string, string>> ReadAppliedMigrationsAsync(
        SqlConnection connection,
        SqlTransaction? transaction = null
    )
    {
        const string sql = """
            SELECT migration_id, script_sha256
            FROM dbo.fis_schema_migrations;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        var applied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            applied[reader.GetString(0)] = reader.GetString(1);

        return applied;
    }

    private static async Task<bool> TableExistsAsync(
        SqlConnection connection,
        string qualifiedTableName
    )
    {
        const string sql = """
            SELECT CASE WHEN OBJECT_ID(@table_name, N'U') IS NULL THEN 0 ELSE 1 END;
            """;

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@table_name", System.Data.SqlDbType.NVarChar, 258).Value =
            qualifiedTableName;
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    private static async Task AcquireMigrationLockAsync(SqlConnection connection)
    {
        const string sql = """
            DECLARE @result INT;
            EXEC @result = sp_getapplock
                @Resource = N'FIS.Database.AdditiveMigrations',
                @LockMode = N'Exclusive',
                @LockOwner = N'Session',
                @LockTimeout = 0;
            SELECT @result;
            """;

        await using var command = new SqlCommand(sql, connection);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (result < 0)
            throw new InvalidOperationException(
                "Another additive migration run is active; try again later."
            );
    }

    private static async Task ReleaseMigrationLockAsync(SqlConnection connection)
    {
        try
        {
            const string sql =
                "EXEC sp_releaseapplock @Resource = N'FIS.Database.AdditiveMigrations', @LockOwner = N'Session';";
            await using var command = new SqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
        catch (SqlException)
        {
            // Closing the connection also releases a session-owned application lock.
        }
    }

    private static string ResolveOperatorName()
    {
        var operatorName = Environment.GetEnvironmentVariable("FIS_MIGRATION_OPERATOR");
        if (string.IsNullOrWhiteSpace(operatorName))
            operatorName = Environment.UserName;

        return operatorName.Length <= 256 ? operatorName : operatorName[..256];
    }

    private static string ComputeHash(string sql) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sql)));

    private static void PrintUsage()
    {
        Console.WriteLine("FIS additive database migration tool");
        Console.WriteLine();
        Console.WriteLine(
            "  plan                                      Read-only migration plan (default)"
        );
        Console.WriteLine(
            "  apply --confirm=FIS-ADDITIVE-ONLY        Apply approved additive migrations"
        );
        Console.WriteLine();
        Console.WriteLine("For non-development environments also set:");
        Console.WriteLine("  FIS_ALLOW_PRODUCTION_MIGRATIONS=I_UNDERSTAND_ADDITIVE_ONLY");
        Console.WriteLine();
        Console.WriteLine("The tool never creates a database or synchronizes the EF model.");
    }

    private sealed record MigrationOptions(bool Apply, string? Confirmation, bool ShowHelp);

    private sealed record MigrationDefinition(
        string Id,
        string FileName,
        string? ResourceName = null,
        string? Sql = null
    );
}
