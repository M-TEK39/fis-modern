-- ============================================================================
-- FIS Modernized App: Add Incident_type / Incident_Desc to legacy Call_centre
-- Reason: modern app stores call centre incident type + cross-references
--         (booking IDs, etc.) in these columns; legacy schema lacks them.
-- Additive only, NULL-able, no data migration needed. Idempotent.
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Call_centre') AND name = 'Incident_type'
)
BEGIN
    ALTER TABLE dbo.Call_centre ADD Incident_type NVARCHAR(100) NULL;
    PRINT 'Added Call_centre.Incident_type';
END
ELSE
BEGIN
    PRINT 'Call_centre.Incident_type already exists — skipped';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Call_centre') AND name = 'Incident_Desc'
)
BEGIN
    ALTER TABLE dbo.Call_centre ADD Incident_Desc NVARCHAR(500) NULL;
    PRINT 'Added Call_centre.Incident_Desc';
END
ELSE
BEGIN
    PRINT 'Call_centre.Incident_Desc already exists — skipped';
END
GO
