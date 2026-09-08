using Microsoft.Data.SqlClient;

namespace FIS.Data.SqlServer;

public static class SqlServerConnectionStringHelper
{
    public const string SafeFallbackConnectionString =
        "Server=192.0.2.10,1433;Database=legacy;User Id=YOUR_DB_USER;Password=YOUR_DB_PASSWORD;Encrypt=False;TrustServerCertificate=True;";

    public static string Resolve(string? connectionString, bool isDevelopment)
    {
        var effectiveConnectionString = string.IsNullOrWhiteSpace(connectionString)
            ? SafeFallbackConnectionString
            : connectionString;

        if (!isDevelopment)
        {
            return effectiveConnectionString;
        }

        var builder = new SqlConnectionStringBuilder(effectiveConnectionString)
        {
            TrustServerCertificate = true,
        };

        builder["Encrypt"] = false;

        return builder.ConnectionString;
    }

    public static bool IsDevelopmentEnvironment()
    {
        var environmentName =
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }
}
