# O20-E G1 Readiness Review — Ordered Technical Closure

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 |
| Version / Date | 2.4 / 2026-11-22 |
| Supersedes | v2.3 |
| Supersedes | v2.2 |
| Status | O20-E OPEN — TRANSACTION-GUARD EXACT-HEAD CI / PLATFORM ORDER / OWNER DECISIONS / INDEPENDENT REVIEW REQUIRED |
| Branch | security/o20-o21-final-closure |
| PR | #92 Draft / unmerged |

## 1. Additional O21 worker database boundary

Added `O20OutboxWorkerLeastPrivilegePostgresProofTests` and an explicit workflow step. The disposable PostgreSQL fixture splits immutable outbox event content from mutable delivery metadata. A synthetic dedicated worker LOGIN can assume only one NOLOGIN worker ACL role using SET-only membership. It can read event/delivery data and update only designated delivery metadata columns. It cannot mutate payload/hash/event ID, delete outbox rows, access Branch/Warehouse/IdentityAccess credential data, execute DDL, create roles or assume owner/runtime/support identities.

This demonstrates a least-privilege object matrix candidate. It does not authorize a sixth logical identity, production credential, outbox schema, worker service, AuditSecurity network client or WORM/KMS integration. The N.6 five-identity baseline remains unchanged until explicit decision.

## 2. Current ordered blockers

| Order | Blocker | Current evidence | Remaining closure condition |
|---:|---|---|---|
| 1 | IdentityAccess contract | Public v1.1 source adapter and request-time live permission query | Exact CI; module-owner acceptance |
| 2 | Commit-time authorization | Disposable two-writer FOR SHARE proof with observable PostgreSQL lock waits | Actual transaction participant in real Organization UoW and all IdentityAccess mutation races |
| 3 | Product Owner authority | Typed Unavailable decision and binding design | Trusted protected binding store/enrollment/change/recovery/disable/restore |
| 4 | Assignment approval | Disposable maker-checker/owner policy proof | Real identity, permission, version/idempotency and transaction integration |
| 5 | Permission catalog | Exact ten-role matrix and sensitive-data boundary test sources | Exact CI and independent review |
| 6 | History / retention | Append-only Warehouse history proof; OD-05 baseline accepted | Production schema/grants plus legal schedule/hold implementation |
| 7 | Assignment concurrency | Idempotency and local deterministic lock proofs | Full global order and deadlock/timeout/crash/replay/approval/reassignment matrix |
| 8 | AuditSecurity/outbox | Synthetic local outbox/ACK proofs and worker least-privilege test | Owner decision on worker identity; real canonical AuditSecurity API/durable ACK and production worker |
| 9 | DB identity matrix | Five-identity baseline, support role proof, runtime login shim and worker privilege PoCs | Explicit approval of runtime physical login binding and dedicated worker identity or proved alternative; actual secret/pool/target DB evidence |
| 10 | Platform ID/order | Candidate proposals and local PoCs | Platform standard and global cross-module acceptance |
| 11 | RowScope | No current consumer found | New source inventory and SQL tests for every future consumer |
| 12 | Legal retention | OD-05 baseline accepted | Jurisdictional schedule, Legal Hold controls, media scope and enforcement |
| 13 | O21 production | Disposable cryptographic/hashchain tests | Production AuditSecurity writer, external WORM, key custody, verifier, restore, alerting/runbook |
| 14 | Independent review/G1 | Not performed | Independent exact-SHA review and explicit G1 authorization |

## 3. Identity model decision gate

The five logical N.6 categories remain accepted and unmodified. The candidate outbox worker proof adds a separately isolated logical identity as a proposal only. The recommended design is a dedicated worker identity with the narrowly scoped object permissions tested in the disposable fixture; this requires explicit owner/Platform/DB/Security approval before the baseline changes or any role is provisioned.

**Disposition:** worker least-privilege test added; exact current CI still required; all production/owner and independent gates remain open. O20-E OPEN, O-20 OPEN, O-21 OPEN, G1 NOT GRANTED.

**END — O20-E readiness review v2.3**


## 4. IdentityAccess transaction-scoped permission guard source — 2026-11-22 (v2.4)

A public transaction-participant method is now present in `IIdentityAccessAuthorizationContract`: `EvaluateCurrentPermissionInTransactionAsync`. The Infrastructure adapter validates the host-supplied current principal, requires a supplied open connection and its own transaction, evaluates the exact permission against current IdentityAccess records, and retains `FOR SHARE` locks over account, session, account-role, role, role-permission and permission rows until that transaction ends.

The explicit CI gate `IdentityAccessTransactionScopedAuthorizationGuardPostgresProofTests` uses the real IdentityAccess schema in the marked disposable database and verifies five concurrent mutations actually wait behind the guard by checking `pg_blocking_pids()`. The test was added in the current closure branch; exact-head CI has not yet been inspected for this latest addition, so it remains PENDING.

**What this closes at source level:** the former absence of a public same-transaction IdentityAccess permission-lock participant. This improves the contract from a request-time Allow snapshot to an API capable of holding the current authorization facts stable through one caller-owned local transaction.

**What remains open:** the actual Organization module and UoW do not exist; no Organization mutation currently invokes the guard. Platform-wide IdentityAccess→Organization lock ranks/order must be accepted before use; IdentityAccess role/permission mutation code and all race cases require owner-level integration acceptance; exact-head test result and independent review remain pending. This method does not grant caller access to IdentityAccess private tables.

### Updated ordered blocker 1–2

| Order | Blocker | Current evidence | Remaining closure condition |
|---:|---|---|---|
| 1 | IdentityAccess contract | Request-time public interface plus source-implemented transaction-scoped guard v1.4 | Exact-head CI; IdentityAccess module-owner acceptance; independent review |
| 2 | Commit-time authorization | Actual IdentityAccess adapter now exposes row-lock guard; real-schema five-writer PostgreSQL proof added | Exact-head test PASS; Platform global order acceptance; real Organization UoW participant and complete rollback/deadlock/timeout/unknown-commit tests |

All other blockers and all non-authorization gates remain as listed in v2.3 unless a later controlled successor closes them.

**Disposition:** source implementation advanced; O20-E remains OPEN; O-20/O-21 remain OPEN; G1 NOT GRANTED.

**END — O20-E readiness review v2.4**
