# O-20 Database Privilege Audit — Controlled O20-D Evidence Update

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 |
| Version / Date | 1.4 / 2026-11-21 |
| Supersedes | v1.3 for current O20-D evidence disposition; history retained |
| Baseline | MDG-ARCH-MASTER-001 v3.0.3 N.6; Stage 08; owner-approved role/object policy |
| Status | O-20 PARTIAL; O20-D disposable logical-backup/isolated-restore PASS on exact SHA 6befb0cd9144d717086852ee43773ae48ff42279; overall O-20 OPEN |
| Execution boundary | CI-only marked disposable PostgreSQL 18 database; no production operation |

## 1. O20-D exact-head execution record

Repository Md-gsm/MD-Gsm1; PR #86; branch security/stage08-n3-adr0013-reconciliation.
Tested source SHA: 6befb0cd9144d717086852ee43773ae48ff42279.
Workflow: MD-Gsm Persons Module Gates, run 44937631216 (SUCCESS).
Job: persons-postgres-migrations, job ID 136947736646 (SUCCESS).
Step: "Prove O-20-D logical backup and isolated restore under backup_operator" (completed).

Direct script output:
```
O20-D role_state=true|0|0
O20-D privilege_state=true|false|false|true|false|false|false|false|false
PASS: backup_operator performed logical pg_dump with read-only source privileges; separate DBA restored into a marked isolated database; migration and IdentityAccess invariants matched; backup identity cannot write, create databases, or access restore target.
```

## 2. What the executed proof establishes

Within the marked disposable PostgreSQL database, the proof:
1. Creates a per-run backup_operator LOGIN with NOINHERIT and without SUPERUSER, CREATEDB, CREATEROLE, REPLICATION or BYPASSRLS; role-state output confirms LOGIN=true and zero membership edges.
2. Removes PostgreSQL's default inherited PUBLIC TEMPORARY database privilege from this disposable test database only. The effective privilege vector confirms source CONNECT=true, TEMP=false, database CREATE=false, schema CREATE=false, and no write privileges on the tested IdentityAccess account relation.
3. Runs logical pg_dump using the backup identity itself, not the DBA identity.
4. Uses a separate CI DBA identity to create a uniquely named restore database marked "MDGSM_DISPOSABLE_TEST_DB; O20-D isolated restore" and runs pg_restore into that target.
5. Compares migration history (8 entries, latest version 8), IdentityAccess base-table count (11), and the append-only security-audit trigger invariant between source and restored databases.
6. Confirms the backup identity cannot connect to the restore target, cannot create databases, and cannot INSERT/UPDATE/DELETE/TRUNCATE the tested source table.
7. Cleans up only the disposable restore database, ephemeral role and temporary archive.

**Disposition: O20-D PASS — bounded disposable CI proof only.** This is not production backup acceptance.

## 3. Scope limitations — explicitly not proven

This proof does not establish WAL archiving or point-in-time recovery (PITR), off-host backup retention or immutable/WORM storage, backup encryption/key custody/rotation, production scheduling/monitoring/retention, restore RTO/RPO, disaster-recovery exercise or production restore authorization, complete database-wide object/ACL/default-ACL/effective-privilege catalog inventory, reporting RowScope, emergency allow-list/expiry/revocation/audit, or independent review.

The restore check is a logical custom-format dump/restore and compares selected migration and IdentityAccess invariants; it is not a full business-data completeness attestation.

## 4. Overall O-20 disposition

O20-D is complete for this narrowly defined disposable CI test. O-20 remains OPEN pending O20-E reporting views and positive/negative RowScope evidence; O20-F emergency allow-list, independent authorization, expiry, revocation and audit proof; O20-G complete database-wide catalog, role/membership, ownership, ACL/default-ACL and evidence digest; deployment credential lifecycle and operational backup controls outside CI; and fresh independent security review.

No production LOGIN, credential, grant, membership, ownership change, migration, backup, restore, key or operational schedule was created or executed.


## Current Architecture Master authority — 2026-11-24

The Product Owner has designated **MDG-ARCH-MASTER-001 v3.0.4** as the sole current, accepted, official and governing Architecture/Accounting Master. The v3.0.3 references in this dated review/evidence artifact identify the baseline actually reviewed or used when the artifact was produced; they are retained as historical provenance and are not silently rewritten. For current design decisions, v3.0.4 governs. Any unresolved section-level conflict or design dependency must be re-audited against v3.0.4 before implementation. This authority notice does not upgrade this artifact's review type, evidence strength, gate status or acceptance.
