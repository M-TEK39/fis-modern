using Microsoft.Data.SqlClient;

namespace FIS.Tools.DatabaseTooling;

/// <summary>
/// Restricts development-only database operations to explicitly named local databases.
/// This is a defense-in-depth check and must remain independent of the ASP.NET environment name.
/// </summary>
public static class DevelopmentDatabaseTargetGuard
{
    private static readonly HashSet<string> AllowedServerNames = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ".",
        "(local)",
        "127.0.0.1",
        "::1",
        "localhost",
        "mssql",
    };

    public static string ValidateConnectionString(
        string? connectionString,
        bool requireDockerSqlServerHost = false
    )
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "A development database connection string is required."
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
                "The development database connection string is not valid.",
                exception
            );
        }

        var serverName = ExtractServerName(builder.DataSource);
        if (!AllowedServerNames.Contains(serverName))
        {
            throw new InvalidOperationException(
                "Development database operations are restricted to the local SQL Server hosts: mssql, localhost, 127.0.0.1, ::1, or ."
            );
        }

        if (
            requireDockerSqlServerHost
            && !string.Equals(serverName, "mssql", StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new InvalidOperationException(
                "Development seeding is restricted to the Docker SQL Server service named mssql."
            );
        }

        var databaseName = builder.InitialCatalog.Trim();
        if (!IsAllowedDatabaseName(databaseName))
        {
            throw new InvalidOperationException(
                "Development database operations require a database named fis_dev, fis_test, or a name prefixed with fis_dev_ or fis_test_."
            );
        }

        if (!string.IsNullOrWhiteSpace(builder.AttachDBFilename))
        {
            throw new InvalidOperationException(
                "Development database operations do not accept attached database files."
            );
        }

        return databaseName;
    }

    private static bool IsAllowedDatabaseName(string databaseName) =>
        databaseName.Length is > 0 and <= 128
        && databaseName.All(static character =>
            char.IsAsciiLetterOrDigit(character) || character is '_' or '-'
        )
        && (
            string.Equals(databaseName, "fis_dev", StringComparison.OrdinalIgnoreCase)
            || string.Equals(databaseName, "fis_test", StringComparison.OrdinalIgnoreCase)
            || databaseName.StartsWith("fis_dev_", StringComparison.OrdinalIgnoreCase)
            || databaseName.StartsWith("fis_test_", StringComparison.OrdinalIgnoreCase)
        );

    private static string ExtractServerName(string dataSource)
    {
        var serverName = dataSource.Trim();
        if (serverName.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
            serverName = serverName[4..];

        var portSeparator = serverName.IndexOf(',');
        if (portSeparator >= 0)
            serverName = serverName[..portSeparator];

        var instanceSeparator = serverName.IndexOf('\\');
        if (instanceSeparator >= 0)
            serverName = serverName[..instanceSeparator];

        return serverName.Trim().Trim('[', ']');
    }
}
