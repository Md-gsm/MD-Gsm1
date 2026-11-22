# O20-E G1 Readiness Review — Ordered Technical Closure

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 |
| Version / Date | 2.0 / 2026-11-22 |
| Supersedes | v1.9 |
| Status | O20-E OPEN — LATEST EXACT-SHA CI AND OWNER ACCEPTANCE REQUIRED |
| Branch | security/o20-o21-final-closure |
| PR | #92 Draft / not merged |

## 1. Additional authorization linearization proof

A disposable PostgreSQL test was added:
`tests/MDGsm.Domain.Tests/Security/IdentityAccessAuthorizationLinearizationPostgresProofTests.cs`.

The test locks the matching account/session/account-role/role-permission/permission rows with `FOR SHARE` in a deterministic order, stages a protected write in the same transaction and starts independent session-revocation and permission-removal transactions. Both revocations must wait while the protected write transaction retains its locks. After the write commits, both revocations complete, and a fresh authorization query denies.

This provides evidence for one candidate PostgreSQL row-lock mechanism. It does not wire the lock set into the live IdentityAccess store or an Organization UnitOfWork, and does not prove the complete global order. The existing request-time contract remains a decision snapshot, not a mutation lease.

## 2. Ordered blocker ledger

| Order | Blocker | Current evidence | Remaining closure condition |
|---:|---|---|---|
| 1 | IdentityAccess principal/permission contract | Public v1.1 interface and live-session permission query source implemented | Latest CI, IdentityAccess owner acceptance, transaction-scoped public guard/participant |
| 2 | Commit-time authorization linearization | New disposable two-writer lock proof added | Wire guard into real IdentityAccess persistence and actual Organization transaction; prove all account/session/grant mutation races |
| 3 | Product Owner authority | Typed fail-closed contract; current resolver Unavailable | Protected binding record, bootstrap/enrollment identity ceremony, change/rotation/recovery/disable/restore and independent review |
| 4 | Organization maker-checker | Disposable policy PoC exists | Production caller/approver principal, owner binding, permission, idempotency and exact-version checks |
| 5 | Permission catalog | Ten-role exact matrix and sensitive permission test source exists | Latest CI and independent matrix review |
| 6 | Organization history | Immutable WarehouseId relationship proof exists | Production DDL/privileges and legal retention/hold implementation |
| 7 | Command idempotency and concurrency | Same-key/same-fingerprint, changed-fingerprint and unique assignment PoC source exists | Latest CI, complete deadlock/timeout/unknown-commit/approval/reassignment matrix |
| 8 | Outbox and AuditSecurity | Local atomic intent, synthetic ACK replay/hash conflict and worker claim PoCs exist | Latest CI; real AuditSecurity API/ACK, event registry, worker, retry/quarantine/reconciliation and owner acceptance |
| 9 | Database identities/grants | O20 support identity proof and candidate runtime LOGIN-shim PoC source exist | Exact CI; owner/Platform approval of runtime login binding; connection pool reset; actual deployment credentials and effective grants |
| 10 | Platform ID/global lock order | Candidate ID request and local lock experiments exist | Platform selects generator and publishes total cross-module order; complete two-writer evidence |
| 11 | RowScope | No current Organization/Inventory/Reporting Branch/Warehouse consumer in source inventory | Re-inventory every future path and SQL positive/negative tests; reporting role remains absent |
| 12 | Retention / Legal Hold | OD-05 baseline accepted, no automatic disposal | Approved legal schedule, authority/hold lifecycle, backups/replicas/exports and fail-closed disposal test |
| 13 | O-21 Hash Chain | Disposable cryptographic PoC source and workflow step exist | Latest CI and production AuditSecurity writer, external WORM, HSM/KMS custody, verifier, restore continuity, alerts/runbook |
| 14 | Independent review / G1 | No independent exact-SHA review accepted | Independent Architecture/Security review, finding closure/risk acceptance and explicit owner G1 authorization |

## 3. Runtime database LOGIN binding clarification

A new disposable PostgreSQL proof explores one candidate way to authenticate a physical runtime connection while retaining a NOLOGIN runtime ACL role:
- synthetic LOGIN `runtime_app` has exactly one SET-only membership to synthetic `mdgsm_runtime` (NOLOGIN);
- no rights are inherited before SET ROLE;
- after SET ROLE, session_user remains the login and current_user is the ACL role;
- only explicit test SELECT/INSERT object grants work; DDL/UPDATE/DELETE and SET ROLE to support/owner identities are denied.

This is not an approved production role-membership edge. The owner-approved N.6 five logical identity policy remains unchanged. Platform/DB owner must choose a deployable authenticated-login binding and approve pool initialization/reset, rotation, restore and telemetry.

## 4. Non-authorization and CI state

No production Organization runtime, DDL, migration, LOGIN credential, membership, grant, Product Owner binding, external WORM or KMS/HSM was created. The branch and PR #92 remain unmerged. Exact latest-head CI must be retrieved after the latest new proof/checksum changes; earlier successful tests at older SHAs are not evidence for the latest source.

**Disposition:** a bounded candidate transaction-scoped authorization proof and runtime LOGIN-shim proof have been added; O20-E remains OPEN, O-20 OPEN, O-21 OPEN, Organization G1 NOT READY / NOT GRANTED.

**END — O20-E readiness review v2.0**
