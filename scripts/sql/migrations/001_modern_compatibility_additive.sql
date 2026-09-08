/*
   FIS approved additive compatibility migration.

   This file is embedded in DatabaseMigrationTool and must not be executed by
   hand. The runner supplies one transaction, an application lock, and the
   once-only hash ledger. It intentionally contains no batch separators.

   The script adds only objects required by the modern application. Existing
   objects are validated rather than altered so a legacy database is never
   silently reshaped.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.fis_session_tokens', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.fis_session_tokens
    (
        token_id NVARCHAR(64) NOT NULL,
        token_type NVARCHAR(16) NOT NULL,
        session_id NVARCHAR(64) NOT NULL,
        claims_json NVARCHAR(MAX) NOT NULL,
        expires_at DATETIME2 NOT NULL,
        created_at DATETIME2 NOT NULL,
        CONSTRAINT PK_fis_session_tokens PRIMARY KEY (token_id)
    );
END
ELSE IF EXISTS
(
    SELECT 1
    FROM (VALUES
        (N'token_id', N'nvarchar', 128, 0),
        (N'token_type', N'nvarchar', 32, 0),
        (N'session_id', N'nvarchar', 128, 0),
        (N'claims_json', N'nvarchar', -1, 0),
        (N'expires_at', N'datetime2', 8, 0),
        (N'created_at', N'datetime2', 8, 0)
    ) AS expected(name, type_name, max_length, is_nullable)
    LEFT JOIN sys.columns actual
        ON actual.object_id = OBJECT_ID(N'dbo.fis_session_tokens')
       AND actual.name = expected.name
    LEFT JOIN sys.types data_type
        ON data_type.user_type_id = actual.user_type_id
    WHERE actual.column_id IS NULL
       OR data_type.name <> expected.type_name
       OR actual.max_length <> expected.max_length
       OR CONVERT(INT, actual.is_nullable) <> expected.is_nullable
)
BEGIN
    ;THROW 51002, 'dbo.fis_session_tokens exists with an unexpected shape; manual review is required.', 1;
END

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.fis_session_tokens')
      AND name = N'IX_fis_session_tokens_session_id'
)
BEGIN
    CREATE INDEX IX_fis_session_tokens_session_id
        ON dbo.fis_session_tokens(session_id);
END

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.fis_session_tokens')
      AND name = N'IX_fis_session_tokens_expires_at'
)
BEGIN
    CREATE INDEX IX_fis_session_tokens_expires_at
        ON dbo.fis_session_tokens(expires_at);
END

IF OBJECT_ID(N'dbo.Call_centre', N'U') IS NULL
BEGIN
    ;THROW 51003, 'Required legacy table dbo.Call_centre is missing; no compatibility column was added.', 1;
END

IF NOT EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Call_centre')
      AND name = N'Incident_type'
)
BEGIN
    ALTER TABLE dbo.Call_centre ADD Incident_type NVARCHAR(100) NULL;
END
ELSE IF EXISTS
(
    SELECT 1
    FROM sys.columns column_info
    JOIN sys.types data_type ON data_type.user_type_id = column_info.user_type_id
    WHERE column_info.object_id = OBJECT_ID(N'dbo.Call_centre')
      AND column_info.name = N'Incident_type'
      AND (data_type.name <> N'nvarchar' OR column_info.max_length <> 200 OR column_info.is_nullable <> 1)
)
BEGIN
    ;THROW 51004, 'dbo.Call_centre.Incident_type exists with an unexpected shape; manual review is required.', 1;
END

IF NOT EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Call_centre')
      AND name = N'Incident_Desc'
)
BEGIN
    ALTER TABLE dbo.Call_centre ADD Incident_Desc NVARCHAR(500) NULL;
END
ELSE IF EXISTS
(
    SELECT 1
    FROM sys.columns column_info
    JOIN sys.types data_type ON data_type.user_type_id = column_info.user_type_id
    WHERE column_info.object_id = OBJECT_ID(N'dbo.Call_centre')
      AND column_info.name = N'Incident_Desc'
      AND (data_type.name <> N'nvarchar' OR column_info.max_length <> 1000 OR column_info.is_nullable <> 1)
)
BEGIN
    ;THROW 51005, 'dbo.Call_centre.Incident_Desc exists with an unexpected shape; manual review is required.', 1;
END
