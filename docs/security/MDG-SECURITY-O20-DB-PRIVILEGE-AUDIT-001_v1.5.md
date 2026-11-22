# O-20 Database Privilege Audit — O20-G Catalog Evidence Implementation

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 |
| Version / Date | 1.5 / 2026-11-21 |
| Supersedes | v1.4 |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #86 |
| Scope | O20-G complete catalog inventory and effective-privilege digest |
| Execution disposition | Implementation and fail-closed assertions added; exact-head CI execution pending |
| Overall O-20 | OPEN |

## 1. O20-G execution boundary

O20-G runs only against the PostgreSQL database named by MDGSM_TEST_DB after verifying the run-specific name pattern and the database comment prefix MDGSM_DISPOSABLE_TEST_DB. The script refuses a non-superuser audit connection and makes no mutation to production databases. Its outputs are metadata/ACL evidence, a SHA-256 digest and a CI artifact. It does not select business rows, role passwords, password hashes, tokens, recovery material or connection secrets.

## 2. Inventory coverage

The evidence is a deterministic JSON inventory of:
- PostgreSQL server version and marked target database owner/configuration/ACL;
- every cluster role attribute (excluding password verifiers), all role membership edges and ADMIN/INHERIT/SET options;
- all schemas, relation objects, columns with explicit ACL entries, routines (including SECURITY DEFINER metadata), types and extensions;
- database/schema/relation/routine/type ACL entries expanded with PostgreSQL default ACL semantics where supported by the core catalogs;
- default ACLs, languages, foreign-data wrappers, foreign servers, large-object metadata and tablespace ACLs;
- object ownership dependencies for the target database plus shared database/tablespace ownership references;
- effective privileges actually held by the five canonical logical identities where mapped database roles exist. runtime_app maps to the NOLOGIN privilege role mdgsm_runtime in the current CI grant script; deploy-time LOGIN binding remains a separate deployment-control item.

The evidence omits OIDs, hostnames, database run IDs and data rows so its digest is deterministic across equivalent disposable CI runs. Role password material is never queried.

## 3. Enforced assertions

The executable O20-G gate requires:
- migration_runner is a non-elevated LOGIN, NOINHERIT and has exactly one membership to the NOLOGIN mdgsm_schema_owner with ADMIN FALSE, INHERIT FALSE, SET TRUE;
- migration_runner has no effective database/schema CREATE authority before SET ROLE;
- mdgsm_runtime is a non-login, non-elevated, non-inheriting role with no memberships and no database/schema CREATE authority;
- neither runtime nor migration LOGIN owns an object in the audited database;
- runtime cannot provision accounts, mutate/delete/truncate append-only security audit events, use dormant recovery_codes or write role/permission/grant catalog tables; only reviewed IdentityAccess grants remain;
- runtime does not own SECURITY DEFINER routines and neither runtime nor migration LOGIN owns default ACLs;
- backup_operator is absent after O20-D cleanup; reporting_ro and emergency_operator remain intentionally unprovisioned. Their absence is recorded, not treated as a positive authorization proof.

## 4. Exact execution record

Pending exact-head GitHub Actions execution. The workflow step must pass and emit inventory counts and a SHA-256 digest of the generated JSON artifact. The artifact is named o20-g-catalog-inventory.json and is uploaded by the PostgreSQL CI job. The final record must be updated with the exact tested SHA, workflow/run/job IDs and digest after logs and artifact metadata are inspected.

## 5. Limitations and remaining O-20 work

This is a complete catalog inventory for the marked target database and a cluster-wide role/membership inventory within the disposable CI PostgreSQL cluster. It is not a production-cluster scan, deployment credential attestation or live grant audit. It does not prove Reporting RowScope (O20-E), emergency allow-list/expiry/revocation (O20-F), production backup lifecycle/PITR/off-host retention, or independent security acceptance. Object-level business RowScope remains fail-closed until canonical data owners and authoritative resolver sources are established.

O20-G may be marked PASS only after exact-head CI succeeds, its digest artifact is present, output coverage is reviewed, and final digest/run evidence is recorded. Overall O-20 remains OPEN until O20-E, O20-F and independent review are satisfied.
