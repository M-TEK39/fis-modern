using FIS.Data.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace FIS.Api.Services;

/// <summary>
/// Ensures the fis_session_tokens table exists on API startup.
/// Safe to run against the legacy DB — uses IF OBJECT_ID checks, never touches other tables.
/// </summary>
public class SessionTokenSchemaInitializer : IHostedService
{
    private const string EnsureSchemaSql =
        @"
IF OBJECT_ID('dbo.fis_session_tokens', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.fis_session_tokens (
        token_id     NVARCHAR(64)  NOT NULL PRIMARY KEY,
        token_type   NVARCHAR(16)  NOT NULL,
        session_id   NVARCHAR(64)  NOT NULL,
        claims_json  NVARCHAR(MAX) NOT NULL,
        expires_at   DATETIME2     NOT NULL,
        created_at   DATETIME2     NOT NULL
    );
    CREATE INDEX IX_fis_session_tokens_session_id ON dbo.fis_session_tokens(session_id);
    CREATE INDEX IX_fis_session_tokens_expires_at ON dbo.fis_session_tokens(expires_at);
END";

    private readonly IServiceProvider _services;
    private readonly ILogger<SessionTokenSchemaInitializer> _logger;

    public SessionTokenSchemaInitializer(
        IServiceProvider services,
        ILogger<SessionTokenSchemaInitializer> logger
    )
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FisDbContext>();
            await db.Database.ExecuteSqlRawAsync(EnsureSchemaSql, cancellationToken);
            _logger.LogInformation("Session token table verified/created");
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to ensure fis_session_tokens table exists. Login will fail until the table is created manually via scripts/sql/001_fis_session_tokens.sql"
            );
            // Don't crash the app — let it start so other endpoints work; only login is affected
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
