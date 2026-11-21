# MDG Security Stage 08 — Controlled Register Addendum

| Field | Value |
|---|---|
| Register ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 2.0 / 2026-11-21 |
| Supersedes | v1.9 |
| Package | PR #86 — open, non-Draft, unmerged |
| O20-G disposition | Implementation and fail-closed catalog assertions committed; exact-head CI execution pending |
| Overall O-20 | OPEN |

## 1. O20-D carried forward

O20-D remains PASS for the bounded disposable logical-backup/isolated-restore test on source SHA 6befb0cd9144d717086852ee43773ae48ff42279, as recorded in audit v1.4. This is not production backup/PITR acceptance.

## 2. O20-G implementation record

The CI PostgreSQL job now runs a catalog/effective-privilege inventory on its marked disposable migration database. It records cluster role attributes and memberships, database/schema/relation/column/routine/type/extension/default ACLs, special object ACL catalogs, object ownership dependencies and effective privileges for the mapped canonical support identities. The JSON artifact excludes business rows, role password material, credential secrets, OIDs and run-specific database names. Its SHA-256 and inventory counts are emitted to the job log; the artifact is uploaded for review.

The gate asserts the exact migration LOGIN → NOLOGIN owner SET-only membership, runtime deny boundaries, absence of runtime/migration object ownership, absence of runtime-owned SECURITY DEFINER routines, and no unintended persistent backup/reporting/emergency role provisioning.

Exact tested SHA, workflow run/job IDs, artifact presence, inventory counts and digest remain pending until the exact-head GitHub Actions run completes and its logs/artifact are reviewed.

## 3. O-20 open boundaries

O20-G execution is not yet marked PASS. O-20 remains OPEN pending:
- O20-E: reporting views/read models and positive/negative RowScope evidence after canonical data ownership and authoritative resolver contracts exist;
- O20-F: emergency allow-list, independent authorization, expiry, revocation and audit proof;
- O20-G: exact-head CI PASS, full evidence artifact inspection and digest record;
- production credential lifecycle/deployment identity attestation and operational backup controls;
- fresh independent security review.

No production role, LOGIN, credential, grant, membership, ownership, migration, backup or restore operation was created or executed. PR #86 remains open and unmerged.
