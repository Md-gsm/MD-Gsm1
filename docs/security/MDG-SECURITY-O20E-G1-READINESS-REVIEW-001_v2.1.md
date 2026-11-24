# O20-E G1 Readiness Review — Ordered Technical Closure

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 |
| Version / Date | 2.1 / 2026-11-22 |
| Supersedes | v2.0 |
| Status | O20-E OPEN — EXACT-HEAD CI / OWNER ACCEPTANCE / INDEPENDENT REVIEW REQUIRED |
| Branch | security/o20-o21-final-closure |
| PR | #92 Draft / not merged |

## 1. Stronger transaction-lock proof

The disposable PostgreSQL authorization linearization test now:
- locks account, session, account-role, role, role-permission and permission rows in one candidate guard query;
- stages a protected write in that same transaction;
- starts independent session-revocation and permission-grant-removal transactions;
- waits until both are visibly blocked in `pg_stat_activity` with `wait_event_type='Lock'`;
- commits the protected transaction and observes both revocations proceed;
- confirms a fresh authorization query then denies.

This is an explicit proof of one row-lock serialization candidate, not an implemented production authorization lease or accepted global lock order. It remains test-only in a marked disposable PostgreSQL database.

## 2. Remaining ordered blockers

| Order | Blocker | Current evidence | Remaining closure condition |
|---:|---|---|---|
| 1 | IdentityAccess public contract | v1.1 interface and live-session request-time permission query source implemented | Latest exact-head CI and IdentityAccess owner acceptance |
| 2 | Commit-time authorization linearization | Stronger two-writer disposable PostgreSQL lock proof added | Implement transaction participant in real IdentityAccess/Organization shared UoW; prove all session/account/grant changes and all race/failure paths |
| 3 | Product Owner authority | Typed fail-closed contract; current resolver Unavailable | Protected binding store, bootstrap/enrollment ceremony, change/rotation/recovery/disable/restore and independent review |
| 4 | Organization maker-checker | Disposable policy PoC exists | Real authenticated identities, owner binding, policy/permission enforcement, idempotency and exact-version check |
| 5 | Permission catalog | Ten-role exact catalog and sensitive-person boundary tests source-present | Exact latest CI and independent matrix review |
| 6 | Relationship history | WarehouseId-bound append-only history PostgreSQL PoC source-present | Production schema/privileges; approved legal schedule and Legal Hold enforcement |
| 7 | Assignment idempotency/concurrency | Two-writer disposable PostgreSQL source-present | Exact CI; broader lock, revoke, reassignment, approval, deadlock and unknown-commit suite |
| 8 | Audit event/outbox | Local atomic intent and synthetic ACK/quarantine/worker tests source-present | Real AuditSecurity event/ACK API, production adapter/worker, retry/quarantine/reconciliation and module-owner acceptance |
| 9 | DB identities/grants | Support-role proof and candidate runtime LOGIN shim PoC source-present | Exact CI; Platform/DB owner accepts connection binding; connection-pool reset, credential rotation and deployed effective grant evidence |
| 10 | Platform ID/global lock order | Candidate ID request and local lock experiments source-present | Platform decision and total cross-module order with complete race/failure evidence |
| 11 | RowScope | No current Organization/Inventory/Reporting Branch/Warehouse source consumer | Inventory/test every future route/export/worker; no ReportingReadOnly grant |
| 12 | Retention / Legal Hold | OD-05 baseline owner-accepted | Applicable legal schedule, hold lifecycle, media treatment, authority and fail-closed disposal proof |
| 13 | O-21 Hash Chain | Disposable cryptographic PostgreSQL PoC | Production AuditSecurity writer, external WORM, HSM/KMS custody, verifier, recovery/restore, alerts/runbook |
| 14 | Independent review / G1 | Not performed | Independent exact-SHA Architecture/Security review; resolve or explicitly accept findings; owner G1 authorization |

## 3. Runtime LOGIN binding status

The new runtime-login mapping test models a synthetic LOGIN shim that has exactly one SET-only membership to a NOLOGIN runtime ACL. It checks no inherited rights, expected session/current user identities, only bounded test-object privileges and denial of owner/support-role escalation. This remains a candidate mapping experiment; no production role/credential/membership is created and N.6's five logical identity baseline is not amended.

## 4. Non-authorization

The new tests and documents do not authorize a production Organization module, schema, migration, LOGIN credential, membership, permission grant, owner binding, external WORM account or KMS/HSM resource. PR #92 stays Draft/unmerged; PR #91 remains the pending full-revert proposal.

**Disposition:** transaction-scoped lock candidate now has observable lock-wait evidence; integration and all other blockers remain OPEN. O20-E OPEN; O-20 OPEN; O-21 OPEN; G1 NOT READY / NOT GRANTED.

**END — O20-E readiness review v2.1**
