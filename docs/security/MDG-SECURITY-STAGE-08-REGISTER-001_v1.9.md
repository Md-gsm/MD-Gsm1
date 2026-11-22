# MDG Security Stage 08 — Controlled Register Addendum

| Field | Value |
|---|---|
| Register ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 1.9 / 2026-11-21 |
| Supersedes | v1.8 as current Stage 08 register |
| Package | PR #86 — open, non-Draft, unmerged |
| Exact evidence SHA | 6befb0cd9144d717086852ee43773ae48ff42279 |

## 1. O20-D recorded disposition

O20-D is PASS for the bounded disposable logical-backup/isolated-restore test on the exact SHA above.

- Workflow 44937631216 — MD-Gsm Persons Module Gates — SUCCESS.
- Job 136947736646 — persons-postgres-migrations — SUCCESS.
- O20-D step "Prove O-20-D logical backup and isolated restore under backup_operator" completed successfully.
- Direct evidence:
  ```
  O20-D role_state=true|0|0
  O20-D privilege_state=true|false|false|true|false|false|false|false|false
  PASS: backup_operator performed logical pg_dump with read-only source privileges; separate DBA restored into a marked isolated database; migration and IdentityAccess invariants matched; backup identity cannot write, create databases, or access restore target.
  ```
- Compared invariants: 8 migration-history rows/latest version 8; 11 IdentityAccess base tables; append-only security audit trigger present.
- backup_operator ran logical pg_dump; a separate DBA restored to a run-marked isolated database. Backup identity was denied write, database creation and restore-target access.
- The test fixture revokes default PUBLIC TEMPORARY only on the marked disposable source database so the backup LOGIN's effective TEMP privilege is false.

## 2. Exact-head workflow disposition

All workflows associated with this exact SHA completed SUCCESS:
- Persons Module Gates: 44937631216 (13 jobs, including PostgreSQL 18, Windows PostgreSQL 18 migration, CI evidence and checksum verification).
- Windows Persons Preview Installer: 44937631163.
- CodeQL: 44937631161.
- Project Charter Owner Gate: 44937631173.

The PostgreSQL job also reports 38/38 integration tests passed and separately runs the O-20 support identity/catalog proof. This record is bound to the exact SHA above, not to a future commit.

## 3. Explicit limitation / open status

O20-D does not prove WAL/PITR, off-host retention, encryption/key custody, production scheduling, RTO/RPO, full database-wide catalog/effective ACL digest, reporting RowScope, emergency allow-list/expiry/revocation, or independent review. It is not a production backup/restore exercise.

O-20 remains OPEN. O20-E, O20-F, O20-G and independent review remain outstanding. O-21, O-45 and O-46 remain governed by their current open/partial dispositions; this O20-D evidence does not close them.

## 4. Safety and merge boundary

No production role, credential, grant, membership, ownership, migration, key, WORM storage, recovery secret, backup schedule or production restore was created or executed. PR #86 remains open and unmerged. This documentation update does not constitute independent reviewer approval or owner acceptance.
