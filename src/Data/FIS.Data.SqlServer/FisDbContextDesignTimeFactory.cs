using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FIS.Data.SqlServer;

/// <summary>
/// Prevents EF CLI from applying the historical clean-install migration set.
/// Production and client-schema changes must use DatabaseMigrationTool instead.
/// </summary>
public sealed class FisDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FisDbContext>
{
    public FisDbContext CreateDbContext(string[] args)
    {
        throw new InvalidOperationException(
            "EF Core design-time database operations are disabled. Use the guarded DatabaseMigrationTool additive runner."
        );
    }
}
