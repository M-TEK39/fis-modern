using Microsoft.Data.SqlClient;

namespace FIS.Data.SqlServer;

public static class SqlServerConnectionStringHelper
{
    public const string LegacyLocalFallbackConnectionString =
        "Server=10.245.1.36,1433;Database=GG;User Id=23525100;Password=Moretegi@2001;Encrypt=True;TrustServerCertificate=True;";

    public static string Resolve(string? connectionString, bool isDevelopment)
    {
        var effectiveConnectionString = string.IsNullOrWhiteSpace(connectionString)
            ? LegacyLocalFallbackConnectionString
            : connectionString;

        if (!isDevelopment)
        {
            return effectiveConnectionString;
        }

        var builder = new SqlConnectionStringBuilder(effectiveConnectionString)
        {
            TrustServerCertificate = true
        };

        builder["Encrypt"] = false;

        return builder.ConnectionString;
    }

    public static bool IsDevelopmentEnvironment()
    {
        var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }
}
