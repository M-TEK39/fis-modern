using System.Data;
using FIS.Data.SqlServer;
using FIS.Tools.DatabaseTooling;
using Microsoft.EntityFrameworkCore;

namespace FIS.Tools.DatabaseDevelopmentTool;

public static class Program
{
    private const string Confirmation = "FIS-DEVELOPMENT-DATABASE";
    private const string AuthorizationVariable = "FIS_ALLOW_DEVELOPMENT_DATABASE_BOOTSTRAP";
    private const string AuthorizationValue = "I_UNDERSTAND_LOCAL_DATABASE_BOOTSTRAP";

    public static async Task<int> Main(string[] args)
    {
        try
        {
            var connectionString = ValidateAuthorization(args);
            var options = new DbContextOptionsBuilder<FisDbContext>()
                .UseSqlServer(
                    connectionString,
                    sql => sql.MigrationsAssembly(typeof(FisDbContext).Assembly.FullName)
                )
                .Options;

            await using var dbContext = new FisDbContext(options);
            await EnsureSafeDatabaseStateAsync(dbContext);

            var pendingMigrations = (await dbContext.Database.GetPendingMigrationsAsync()).ToArray();
            if (pendingMigrations.Length == 0)
            {
                Console.WriteLine("Local development database is already current.");
            }

            if (pendingMigrations.Length > 0)
            {
                Console.WriteLine(
                    $"Applying {pendingMigrations.Length} clean-install migration(s) to the local development database..."
                );
                await dbContext.Database.MigrateAsync();
            }

            await dbContext.Database.ExecuteSqlRawAsync(DevelopmentCompatibilitySchema.Sql);
            Console.WriteLine("Local development database bootstrap completed successfully.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Development database bootstrap refused or failed: {exception.Message}");
            return 1;
        }
    }

    private static string ValidateAuthorization(string[] args)
    {
        var environmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        if (!string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The development database bootstrap only runs in the Development environment."
            );
        }

        if (!args.Contains($"--confirm={Confirmation}", StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"The development database bootstrap is guarded. Re-run with --confirm={Confirmation}."
            );
        }

        if (
            !string.Equals(
                Environment.GetEnvironmentVariable(AuthorizationVariable),
                AuthorizationValue,
                StringComparison.Ordinal
            )
        )
        {
            throw new InvalidOperationException(
                $"The development database bootstrap requires {AuthorizationVariable}={AuthorizationValue}."
            );
        }

        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        DevelopmentDatabaseTargetGuard.ValidateConnectionString(connectionString);
        return connectionString!;
    }

    private static async Task EnsureSafeDatabaseStateAsync(FisDbContext dbContext)
    {
        await dbContext.Database.OpenConnectionAsync();
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT
                CASE WHEN OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL THEN 0 ELSE 1 END,
                (
                    SELECT COUNT(*)
                    FROM sys.tables
                    WHERE is_ms_shipped = 0
                      AND name <> N'__EFMigrationsHistory'
                );
            """;

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            throw new InvalidOperationException("Could not inspect the local development database state.");

        var hasMigrationHistory = reader.GetInt32(0) == 1;
        var userTableCount = reader.GetInt32(1);
        if (!hasMigrationHistory && userTableCount > 0)
        {
            throw new InvalidOperationException(
                "The target database already contains tables but no EF migration history. Bootstrap refuses to alter an existing or restored database. Use a new fis_dev or fis_test database."
            );
        }

        if (dbContext.Database.GetDbConnection().State != ConnectionState.Open)
            throw new InvalidOperationException("The local development database connection closed unexpectedly.");
    }
}
