# O20-E G1 Readiness Review — Ordered Closure Update

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 |
| Version / Date | 1.9 / 2026-11-22 |
| Supersedes | v1.8 |
| Status | O20-E OPEN — NO G1 — LATEST EXACT-SHA CI REQUIRED |
| Branch | security/o20-o21-final-closure |
| PR | #92 Draft / not merged |

## 1. Progress since v1.8

A disposable PostgreSQL proof was added for a candidate physical connection binding of the NOLOGIN runtime ACL role:
- a synthetic LOGIN principal has exactly one SET-only membership to a synthetic NOLOGIN runtime ACL;
- no privileges are inherited before SET ROLE;
- the active SQL context preserves LOGIN as `session_user` and runtime ACL as `current_user`;
- only the synthetic test table SELECT/INSERT grants are available after SET ROLE;
- UPDATE/DELETE/TRUNCATE/DDL and SET ROLE to owner or support identities are denied.

This is a test-only mapping experiment. It does not change or approve the owner-accepted five logical DB identity model and does not prove actual deployment login credentials, connection-pool reset, external secret rotation or production effective grants.

## 2. Current ordered blocker ledger

| Order | Blocker | Current state | Remaining closure evidence |
|---:|---|---|---|
| 1 | IdentityAccess public principal/permission contract | v1.1 interface/source adapter exists; live-session permission query added | Exact latest CI; IdentityAccess owner acceptance; full 41-case runtime acceptance; Organization consumer |
| 2 | Product Owner authority | Fail-closed typed decision exists; adapter returns Unavailable | Protected binding aggregate, trusted enrollment/bootstrap, change/rotation/recovery, disable and restore evidence |
| 3 | Organization assignment policy | Disposable maker-checker/owner PoC source exists | Exact-head CI; production actor/approver/owner trust and commit-time authorization linearization |
| 4 | Permission catalog | Ten-role matrix and sensitive-person separation source/test exists | Exact-head CI and independent full matrix review |
| 5 | Relationship history | WarehouseId-bound append-only history PoC exists | Exact-head CI; production schema/privileges; retention/Legal Hold implementation |
| 6 | Idempotency/concurrency | Same-key and same-resource two-writer PoC source exists | Exact-head CI; Platform global lock ranks; revocation/reassignment/approval multi-aggregate races |
| 7 | Audit intent/outbox | Atomic local outbox / synthetic ACK replay / hash conflict proof source exists | Exact-head CI; AuditSecurity-owned canonical API and durable ACK; production adapter/worker/retry/quarantine/reconciliation |
| 8 | DB identities and grants | O20 catalog/support-role PoCs exist; runtime login-shim proof added | Exact-head CI; Platform/DB owner decision on LOGIN-to-NOLOGIN binding; actual deployed credentials, pool reset and effective-grant evidence |
| 9 | Platform ID and lock order | Candidate requests and local ordering experiment exist | Platform chooses ID generator; global cross-module lock-order registry and complete race/failure matrix |
| 10 | RowScope | Exact source inventory shows no current Branch/Warehouse consumer | Re-inventory every future query/report/export route; SQL predicate tests; ReportingReadOnly remains absent |
| 11 | Retention/Legal Hold | OD-05 baseline owner-accepted; no automatic deletion | Legal retention schedule, authority, hold lifecycle, backup/replica/export treatment and fail-closed enforcement |
| 12 | O-21 Hash Chain | Disposable PostgreSQL PoC exists | Exact-head CI; production AuditSecurity writer, WORM/Object Lock, HSM/KMS key custody, verifier, restore continuity, monitoring/runbook |
| 13 | Independent review / G1 | No independent exact-SHA review accepted | Independent Architecture/Security review of one final exact SHA; findings resolved/formally accepted; explicit G1 authorization |

## 3. Unchanged non-authorizations

- No production Organization module, endpoint, schema, migration or database grant has been created.
- No owner binding, runtime LOGIN, service secret, external key provider or WORM account has been provisioned.
- No Organization permission key or ReportingReadOnly role has been granted.
- The LOGIN-shim PoC does not authorize a production membership edge.
- O20-E, O-20 and O-21 remain OPEN; Organization G1 is NOT READY / NOT GRANTED.

**Disposition:** O20 database runtime-binding evidence expanded with a disposable PoC; production binding remains a Platform/DB owner decision. Latest exact-head validation and all external owner/independent gates remain mandatory.

**END — O20-E readiness review v1.9**
