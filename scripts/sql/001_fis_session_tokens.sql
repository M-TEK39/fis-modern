-- ============================================================================
-- FIS Modernized App: Session token persistence table
-- Run this ONCE on the restored legacy database before starting the API.
-- Idempotent — safe to re-run.
-- ============================================================================

IF OBJECT_ID('dbo.fis_session_tokens', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.fis_session_tokens (
        token_id        NVARCHAR(64)  NOT NULL PRIMARY KEY,
        token_type      NVARCHAR(16)  NOT NULL,
        session_id      NVARCHAR(64)  NOT NULL,
        claims_json     NVARCHAR(MAX) NOT NULL,
        expires_at      DATETIME2     NOT NULL,
        created_at      DATETIME2     NOT NULL
    );

    CREATE INDEX IX_fis_session_tokens_session_id
        ON dbo.fis_session_tokens(session_id);

    CREATE INDEX IX_fis_session_tokens_expires_at
        ON dbo.fis_session_tokens(expires_at);

    PRINT 'Created dbo.fis_session_tokens';
END
ELSE
BEGIN
    PRINT 'dbo.fis_session_tokens already exists — skipped';
END
GO

-- ============================================================================
-- Optional: scheduled cleanup of expired tokens.
-- Run periodically (e.g. SQL Agent Job nightly) to prevent table bloat.
-- ============================================================================
-- DELETE FROM dbo.fis_session_tokens WHERE expires_at < DATEADD(hour, -1, SYSUTCDATETIME());
