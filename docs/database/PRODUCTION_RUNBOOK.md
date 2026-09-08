# Production database runbook

FIS keeps the client database as the source of truth. The production tooling is
split into three deliberate operations:

1. `db-audit` reads the database and creates a data-quality report.
2. `db-migrate` applies the single approved additive compatibility migration.
3. `db-backfix` applies only a reviewed correction plan supplied by the client.

The tools do not create a database, synchronize the EF model, seed demo data,
merge identities, or invent missing values. The canonical schema change is
`scripts/sql/migrations/001_modern_compatibility_additive.sql`; it is embedded
in the migration executable and tracked by `dbo.fis_schema_migrations`.

## Before touching production

- Take and verify a restorable SQL Server backup.
- Restore that backup into a staging database and run the complete sequence there.
- Confirm the connection string targets the intended named database, not a SQL
  Server system database. Never put credentials in this repository or a report.
- Stop if the client database has unexpected table or column shapes. The tools
  fail closed and require manual review in that situation.

Use a database account scoped for the operation. The audit account should be
read-only. The migration and back-fix accounts should have only the approved
DDL or data permissions needed for their operation; do not use `db_owner` for
routine application access.

## 1. Generate the audit

Set the real connection string outside the repository, then run the read-only
audit. The report contains stable issue codes, counts, normalized duplicate
keys, and record keys that can be used to prepare a correction plan.

```bash
export ConnectionStrings__Default='Server=<sql-server>;Database=<fis-database>;User Id=<audit-user>;Password=<secret>;Encrypt=True;TrustServerCertificate=False;'
docker compose -f docker/docker-compose.yml --profile tools run --rm \
  db-audit --format=json --output=/src/database-audit.json
```

Exit code `0` means no configured issues were found. Exit code `2` means the
audit ran and found data-quality issues. Exit code `1` means the audit could
not run. An audit is not proof that email delivery, external identity
configuration, or business approval is correct.

Review at least:

- duplicate or missing user email values in `TS_Users` and `user_access_old1`;
- duplicate or orphaned credential and Entra mappings;
- duplicate or missing vehicle fleet/registration identifiers;
- multiple current contracts for one vehicle and orphaned vehicle/site links;
- missing core site/department descriptions; and
- missing optional compatibility objects or incident columns.

## 2. Review the additive migration

The default migration command is read-only. Run it after the backup and before
the apply to see the script hash and whether the ledger is current:

```bash
docker compose -f docker/docker-compose.yml --profile tools run --rm db-migrate plan
```

Only the manifest-listed additive script can be applied. Its SQL is executed
inside one transaction while holding a SQL Server application lock. The ledger
records the migration ID and SHA-256 hash, so an already-applied script cannot
be silently changed or run under the same ID.

## 3. Apply the additive migration once

The production command requires both a command confirmation and a separate
environment gate. Keep the gate out of committed files and CI logs:

```bash
export FIS_ALLOW_PRODUCTION_MIGRATIONS=I_UNDERSTAND_ADDITIVE_ONLY
export FIS_MIGRATION_OPERATOR='<approved-operator>'
docker compose -f docker/docker-compose.yml --profile tools run --rm \
  db-migrate apply --confirm=FIS-ADDITIVE-ONLY
```

This migration adds the session-token table, its supporting indexes, the two
modern `Call_centre` incident columns, and the two control ledgers used by the
tooling. Existing objects are checked for the expected shape; they are not
altered to fit the EF model. A second run is a no-op after hash verification.

There is no production command for creating or dropping a database. The
development-only `docker-compose.dev.yml` may initialize its disposable local
SQL Server, but that path is not part of production deployment.

## 4. Prepare and validate a back-fix plan

The audit identifies records needing review; it does not choose a canonical
record or generate replacement information. A human with client approval must
create a JSON plan. Every item must include:

- a unique `fixId`;
- an allow-listed table/field and integer record key;
- either the exact `expectedOldValue` or `expectNull: true`;
- the verified `newValue`; and
- a non-empty `reason` explaining the approval.

Example shape using deliberately fictional values:

```json
{
  "planId": "client-2026-09-08-user-email-review-001",
  "runId": "00000000-0000-0000-0000-000000000001",
  "approvedBy": "<client-approver>",
  "items": [
    {
      "fixId": "user-123-email",
      "table": "TS_Users",
      "keyColumn": "user_access_code",
      "keyValue": "123",
      "column": "email",
      "expectNull": true,
      "newValue": "verified.person@example.invalid",
      "reason": "Client-approved address supplied in the signed data correction register."
    }
  ]
}
```

Validate the plan against the current database first:

```bash
docker compose -f docker/docker-compose.yml --profile tools run --rm \
  db-backfix plan --plan=/src/reviewed-fix.json
```

The back-fix tool refuses to change keys, delete rows, merge identities,
write passwords, or create a normalized duplicate. It also refuses to
overwrite a value that no longer matches the reviewed expected value.

## 5. Apply and re-audit

After the plan is approved and the validation output is reviewed, apply it with
the separate production gate:

```bash
export FIS_ALLOW_PRODUCTION_DATA_FIXES=I_UNDERSTAND_REVIEWED_DATA_FIX
docker compose -f docker/docker-compose.yml --profile tools run --rm \
  db-backfix apply --plan=/src/reviewed-fix.json \
  --confirm=FIS-REVIEWED-DATA-FIX
```

Each change and its before/after value is recorded in
`dbo.fis_data_fix_audit` in the same transaction as the data update. The plan
hash and plan ID make a successful re-run a no-op; a changed file under an
already-used plan ID is rejected.

Run the audit again after every back-fix batch and retain both reports with the
backup/change record. Do not add a uniqueness constraint to a legacy field
until the audit is clean and the client has approved that separate schema
decision.
