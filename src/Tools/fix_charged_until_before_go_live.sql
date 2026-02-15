-- =============================================================================
-- FIX: Initialise Charged_Until for existing active contracts
-- =============================================================================
-- Purpose:
--   The monthly billing job (MonthlyBillingJob) uses Charged_Until to track
--   where billing last left off. Contracts created before this field was
--   initialised have Charged_Until = NULL, which causes the job to fall back
--   to start_date — generating backdated journal entries for years of history.
--
-- This script sets Charged_Until = GETDATE() for all active contracts that
--   have never been touched by the new billing job, telling it:
--   "treat these as billed up to today — only bill going forward."
--
-- IDEMPOTENT: safe to run multiple times. Only touches rows where
--   still_current = 'Y' AND Charged_Until IS NULL.
--   Running it again after the first time is a no-op.
--
-- RUN THIS ONCE before the first monthly billing job fires (ideally the day
--   before going live on the new system).
-- =============================================================================

BEGIN TRANSACTION;

DECLARE @updated INT;

UPDATE hire_contracts
SET    Charged_Until = CAST(GETDATE() AS DATE)
WHERE  still_current  = 'Y'
  AND  Charged_Until  IS NULL
  AND  is_deleted     = 0;

SET @updated = @@ROWCOUNT;

PRINT CONCAT('Charged_Until initialised for ', @updated, ' active contract(s). Billing will run forward from today.');

-- Verify: should return 0 rows after the update
SELECT COUNT(*) AS remaining_null_contracts
FROM   hire_contracts
WHERE  still_current = 'Y'
  AND  Charged_Until IS NULL
  AND  is_deleted    = 0;

COMMIT;
