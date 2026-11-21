# O-20 Database Role/Object Matrix — O20-D Evidence Update

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 |
| Version / Date | 1.2 / 2026-11-21 |
| Supersedes | v1.1 for O20-D evidence disposition |
| Policy baseline | Owner-approved N.6 five-identity model |
| Status | O20-D disposable backup/isolated-restore envelope EXECUTED PASS; reporting/emergency and complete catalog proof OPEN |

## 1. Canonical support identity: backup_operator

| Dimension | Required/observed result | Evidence |
|---|---|---|
| Login | Dedicated ephemeral LOGIN | O20-D role_state=true|0|0 |
| Role attributes | NOINHERIT; no SUPERUSER/CREATEDB/CREATEROLE/REPLICATION/BYPASSRLS | Exact-head O20-D job |
| Memberships | Zero membership edges | role_state second field = 0 |
| Source database | CONNECT allowed; TEMP denied; CREATE denied | Effective privilege vector |
| Schema access | USAGE on eligible non-system schemas only; no CREATE | Script assertions |
| Data access | SELECT for logical dump; no INSERT/UPDATE/DELETE/TRUNCATE | Effective privilege vector and denial probes |
| Dump execution | pg_dump --format=custom executed by backup_operator | O20-D script/job |
| Restore target | No CONNECT or CREATE authority | Separate DBA creates marked target; backup LOGIN denial verified |
| Restore execution | Separate DBA performs pg_restore into isolated marked target | O20-D script/job |
| Data verification | Migration history 8/8; IdentityAccess tables 11; audit immutability trigger present | Source/restore comparison |
| Environment | Per-run disposable database, ephemeral role and temporary archive | Job cleanup |

The script explicitly revokes PostgreSQL's default PUBLIC TEMPORARY privilege from the marked disposable test database before checking the backup LOGIN's effective TEMP privilege. This is a test-fixture ACL correction only; it does not change production database grants.

## 2. Exact execution evidence

- SHA: 6befb0cd9144d717086852ee43773ae48ff42279
- Workflow run: 44937631216 — SUCCESS
- Job: 136947736646 (persons-postgres-migrations) — SUCCESS
- O20-D step — SUCCESS; direct output records role state, effective privilege vector and PASS message.
- The same job subsequently reports 38/38 PostgreSQL Integration tests passed and separately invokes the support-identity/catalog proof.

## 3. Boundaries and non-claims

This matrix entry is limited to logical backup and isolated logical restore in disposable PostgreSQL CI. It does not confer or prove production role provisioning. It does not prove WAL/PITR, off-host retention, encryption/key custody, RTO/RPO, production scheduling, complete catalog/default ACL inventory, reporting RowScope, emergency allow-list lifecycle or independent review.

## 4. Remaining canonical identities

| Identity | Current disposition |
|---|---|
| runtime_app | Existing disposable IdentityAccess grant and API execution evidence; broader module command boundary remains incomplete |
| migration_runner | Actual DbMigrator LOGIN/NOLOGIN owner SET ROLE boundary proven in disposable Linux and Windows PostgreSQL 18 CI |
| backup_operator | O20-D bounded logical backup/isolated restore PASS; production controls and broader recovery remain open |
| reporting_ro | No approved positive RowScope/view execution proof; OPEN |
| emergency_operator | No allow-list/expiry/revocation/independent authorization proof; OPEN |

Overall O-20 remains OPEN. No production credentials, grants, memberships, object ownership or role lifecycle operations were performed.
