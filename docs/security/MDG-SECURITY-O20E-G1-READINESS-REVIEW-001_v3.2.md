# O20-E G1 Readiness Review — Ordered Technical Closure

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 |
| Version / Date | 3.2 / 2026-11-22 |
| Supersedes | v3.1 |
| Supersedes | v2.7 |
| Supersedes | v2.6 |
| Supersedes | v2.5 |
| Supersedes | v2.4 |
| Supersedes | v2.3 |
| Supersedes | v2.2 |
| Status | O20-E OPEN — BOUNDED EXACT-HEAD CI PASS; PLATFORM ORDER / OWNER DECISIONS / INDEPENDENT REVIEW REQUIRED |
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


## 5. IdentityAccess public transaction guard — bounded PostgreSQL PASS, current-head retest required (2026-11-22; v2.5)

The IdentityAccess authorization guard test has now passed on the bounded prior source/test head `c67d892197d02409827bbc127a296fd64374576b`:

- Linux test job completed with 52/52 tests passed.
- Explicitly selected `IdentityAccessTransactionScopedAuthorizationGuardPostgresProofTests` reported PASS.
- The test invoked the actual public-contract adapter against real IdentityAccess `identity.*` tables in the marked disposable database.
- It inserted a protected-write probe in the same caller transaction and verified five concurrent mutations were actually blocked via `pg_blocking_pids()`: session revocation, account disable/credential version update, account-role removal, role-permission removal and permission deactivation.
- After the guard transaction committed, the concurrent operations completed and a fresh authorization query denied.

The public module marker is now advanced from 1.1 to 1.2, the ArchitectureBoundary test expects 1.2, and the Contracts README has been updated to describe the new transaction-scoped API. Those changes create a later head, so the full CI suite must rerun before current-head PASS can be claimed.

### Updated order 1–2

| Order | Gate | Current source/test evidence | Still required |
|---:|---|---|---|
| 1 | IdentityAccess public contract | v1.2 source now documents request-time + transaction-scoped APIs; bounded Linux test PASS at prior source/test SHA | Exact latest-head full CI, module-owner acceptance and independent review |
| 2 | Commit-time authorization | Actual IdentityAccess adapter holds row locks through caller transaction; real-schema five-writer PostgreSQL proof PASS at prior source/test SHA | Platform global lock-order acceptance; real Organization UnitOfWork participant; complete rollback/deadlock/timeout/crash/unknown-commit tests |

This is a meaningful source/proof milestone, not closure of O20-E. The new API must remain unused for cross-module mutation until Platform and affected module owners approve total lock ordering. Product Owner binding, Organization permission namespace, production Org schema/consumer, AuditSecurity durable ACK/worker, legal schedule/hold, external O21 provider/custody/verifier and independent review remain open.

**Disposition:** IdentityAccess transaction guard bounded proof PASS at prior exact source/test SHA; latest head validation pending; O20-E OPEN; O-20/O-21 OPEN; G1 NOT GRANTED.

**END — O20-E readiness review v2.5**


## 6. Exact-head RowScope revalidation — 2026-11-22 (v2.6)

The RowScope source inventory is advanced to v0.5 and revalidated at commit `b80a040dea5ffb8cc7db98f3bb23cd6f8c5b48c1`, API Program.cs blob `98eb172eb2572f69095f2a54168f74bcd91305b6`. The executable modules remain Accounting, Finance, IdentityAccess, Persons and RepairOperations. No Organization/Inventory/Reporting/AuditSecurity runtime or Branch/Warehouse report/query/export route is present.

The current `reports.*` permission catalog and the new IdentityAccess transaction guard do not create RowScope. Consequently there is no current positive/negative Branch/Warehouse RowScope consumer test to pass. The absence finding is current only at the inspected source SHA. Any future report, dashboard, Inventory, export, print, background worker or data endpoint must trigger a new inventory and source-side SQL predicate test. ReportingReadOnly remains absent.

The full source topology review does not resolve the Product Owner binding, Platform lock-order/ID decisions or AuditSecurity production contract.

**Disposition:** current RowScope inventory revalidated; future consumer coverage OPEN. O20-E OPEN; G1 NOT GRANTED.

**END — O20-E readiness review v2.6**


## 7. Exact-head O20-E bounded verification and remaining owner/platform decisions — 2026-11-22 (v2.7)

### 7.1 Exact-head evidence at 448b6cc9809a934a85507f4213de1e4395a0b74b

The Linux PostgreSQL 18 persons-postgres-migrations job completed successfully in workflow 45046411908:
- application-owned migration runner applied 8 migrations, then a second run applied 0 new migrations (idempotence);
- O20-D logical backup/isolated restore and O20-G catalog inventory/effective privilege digest passed;
- complete .NET integration suite passed 52/52;
- O20 support identity and runtime login mapping proofs passed;
- O20-F emergency lifecycle PoC passed;
- O20-E OD-01/OD-02/OD-03 policy, immutable Warehouse-Branch history, maker-checker/owner direct policy, local outbox/ACK, idempotency concurrency and deterministic candidate lock-order PoCs passed;
- O21 AuditSecurity hash-chain disposable PostgreSQL PoC passed;
- IdentityAccess live-session permission, revocation/permission linearization and transaction-scoped authorization guard PoCs passed;
- isolated Accounting AP-03/AP-04 proof passed.

The explicit transaction-guard test invoked the actual IdentityAccess Infrastructure adapter and actual identity.* tables in the marked disposable DB, observed five competing IdentityAccess mutations blocked behind the guard via pg_blocking_pids(), and then observed the fresh permission check deny after those mutations completed.

On the same SHA, CodeQL run 45046411892 succeeded and Windows Persons Preview Installer run 45046411889 succeeded. The Windows PostgreSQL migration job persons-postgres-windows-migrations remained in progress at last inspection. Therefore the entire primary workflow is NOT YET a completed all-platform PASS. No overall CI-green claim is made until that Windows job and the primary workflow complete successfully.

These are disposable CI proofs, not production Organization/AuditSecurity implementations, production identity/secret bindings or external-provider proofs.

### 7.2 Remaining ordered owner/platform dependencies

The consolidated decision dependencies are recorded in MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.1:
1. IdentityAccess owner acceptance of public Contracts v1.2 and transaction guard.
2. Platform and affected module-owner acceptance of total lock-order and full cross-module race matrix.
3. Product Owner/IdentityAccess/Platform/Security acceptance of Product Owner binding and trusted bootstrap.
4. Explicit decision on a dedicated sixth outbox-worker logical identity or keeping production delivery disabled; N.6 five-identity baseline remains unchanged.
5. Platform acceptance of UUIDv7 (or alternate) generator/version policy.
6. Actual KMS/HSM key custody and independent Object Lock Compliance provider selection/provisioning, verifier, restore and operations.
7. Jurisdiction-specific retention schedule and Legal Hold authority/enforcement.
8. WG-0 migration reservation and explicit Organization G1 implementation authorization.
9. Independent exact-SHA Security/Architecture review and explicit Product Owner G1 decision.

No production Organization module/UoW has been created because module-topology policy prohibits introduction before G1 authorization. The actual Organization consumer integration therefore follows—not precedes—the accepted G1/implementation release gate. Disposable PoCs and the public IdentityAccess contract are not represented as production consumers.

**Disposition:** bounded Linux O20/O21/IdentityAccess disposable proof suite PASS on exact SHA 448b6cc...; CodeQL and Windows Preview PASS on the same SHA; Windows PostgreSQL migration/overall workflow still in progress at last inspection. O20-E, O-20 and O-21 remain OPEN; Organization G1 NOT GRANTED.

**END — O20-E readiness review v2.7**


## 8. Bounded exact-head CI confirmation — 2026-11-22 (v2.8)

At source/test head `5096fe9693a65213321f1b123341a220a9826a7d`, primary workflow `45047243289` completed SUCCESS with all 13 jobs passing. Build/test evidence: 242/242 .NET unit tests, 52/52 marked disposable PostgreSQL integration tests, zero build warnings/errors, eight migrations applied on the first run and zero new migrations on the second run on Linux and Windows PostgreSQL 18. CodeQL `45047243377`, Windows Preview Installer `45047243268`, and Project Charter Owner Gate `45047290161` also succeeded on the same SHA.

The Linux integration logs explicitly show PASS for OD-01/OD-02/OD-03, immutable Warehouse-Branch history, maker-checker/owner direct policy, local outbox/replay/quarantine, assignment idempotency concurrency, O21 hash-chain, deterministic local UUID lock acquisition, IdentityAccess live-session permission, revocation linearization, and the transaction-scoped authorization guard. The latter uses the actual IdentityAccess adapter/schema and PostgreSQL lock inspection. O20-D backup/restore and O20-G catalog inventory/effective privilege digest also passed.

The worker least-privilege test is in the `Category=Integration` suite, and a separate explicit class-filter step was added after the tested SHA. Its first workflow run was cancelled because the checksum manifest still referenced the preceding workflow file; this successor updates the manifest and the next exact-head run must independently confirm the added step.

### Remaining release blockers

1. IdentityAccess module owner has not accepted the public Contracts v1.2 API/transaction-guard.
2. Platform and affected module owners have not accepted a total cross-module lock-order/linearization protocol. The local deterministic Organization test covers only one candidate resource class.
3. No protected Product Owner binding store or enrollment/change/recovery/disable/restore ceremony exists; the public contract deliberately returns Unavailable.
4. Organization assignment permissions/roles and a production Organization UoW/runtime remain blocked; no module consumer exists to execute the transaction guard or source RowScope.
5. No production canonical AuditSecurity API/durable ACK or real outbox worker has been deployed. The five-category N.6 identity model remains unchanged and the proposed sixth worker identity is not approved/provisioned.
6. Platform UUID generator/version policy remains pending.
7. Actual KMS/HSM signing custody, external Object Lock Compliance anchor, independently administered custody, verifier deployment, restore continuity, alerting and runbooks are absent.
8. Jurisdictional retention schedule and Legal Hold authority/enforcement remain open; OD-05 default retention/no-auto-delete policy is accepted.
9. Independent exact-SHA Architecture/Security review and explicit G1 authorization have not been received.

No production grant, identity, secret, provider resource, Organization schema/runtime, migration or release authorization is inferred from CI success.

**Disposition:** bounded source/test suite PASS at the specified exact SHA; release/security owner decisions and production evidence remain OPEN. O20-E OPEN; Organization G1 NOT READY / NOT GRANTED; overall O-20 and O-21 OPEN.

**END — O20-E readiness review v2.8**


## 9. Legal Hold and retention eligibility proof — 2026-11-22 (v2.9)

The accepted OD-05 policy now has a focused disposable PostgreSQL eligibility test at O20OrganizationLegalHoldRetentionPostgresProofTests. It fails closed for an unset/unapproved schedule, future retention deadline, unavailable hold resolver, active/pending/unknown hold, missing/expired/version-mismatched authorization, self-approval or missing records/legal authority. The second test proves a concurrent hold insertion serializes before the disposal eligibility decision and causes DENY.

The test does not delete records and does not set any statutory duration. It uses a synthetic schedule/authority only as a positive test control. Production Hold lifecycle, legal/records owner identity, schedule/jurisdiction, backup/replica/export scope, disposal operation, alerts/runbooks and independent review remain open.

An explicit CI step has been added. Current PR branch head is b05d8e9933f7a1c4d9713c851b7984bb1e2c0765 and the full run 45049762142 is in progress; exact compile/test output is not yet confirmed. This proof remains CI-PENDING.

**Disposition:** OD-05 policy remains OWNER-ACCEPTED; fail-closed retention/Hold disposable proof added but pending exact-head validation; O20-E OPEN; Organization G1 NOT READY / NOT GRANTED.

**END — O20-E readiness review v2.9**


## 10. Exact-head CI and O20-E blocker disposition — 2026-11-22 (v3.0)

### 10.1 Exact-head validation

Current source/test head `99f28b63a179a4a54b377bca2a9174a819883c28` completed:
- Primary workflow `45051449948`: SUCCESS, all 13/13 jobs;
- CodeQL `45051449934`: SUCCESS;
- Windows Persons Preview Installer `45051449938`: SUCCESS;
- Project Charter Owner Gate `45051449965`: SUCCESS;
- Release build 0 warnings / 0 errors; unit tests 242/242 passed;
- marked disposable PostgreSQL 18 integration suite 58/58 passed;
- Linux and Windows application-owned migrator: 8 migrations applied first run, 0 new migrations on repeat;
- SHA256SUMS verifier: 643 tracked files match; PII-free commit-bound evidence artifact ID `14765768333` uploaded with digest `061167f054c6676c5335a4190ab10081f78ff5d5e76e37f8f4af117552ba982b`.

Explicit PostgreSQL proof classes on the same head passed for OD-01/02/03, immutable Warehouse-Branch history, fail-closed Legal Hold eligibility and concurrent hold placement, maker-checker/Product Owner synthetic policy, cross-aggregate races/bounded retry, local AuditIntent/Outbox/replay/quarantine, idempotency, local deterministic lock ordering, outbox-worker object/column least privilege, O20-D/O20-G/O20-F and IdentityAccess live permission/revocation/transaction-scoped authorization.

### 10.2 Implementation status

IdentityAccess Contracts v1.2 and the transaction-scoped Infrastructure guard are source-implemented. The exact-head PostgreSQL proof calls the actual adapter against real disposable `identity.*` tables and demonstrates five concurrent revocation/identity/grant mutations block behind the guard's `FOR SHARE` lock set until the protected transaction commits. IdentityAccess module-owner acceptance is still outstanding.

The owner-approved permission matrix is source-aligned and golden-tested. No `organization.*` keys have been added because exact assignment-administration role mapping has not been accepted. The Product Owner authority adapter deliberately returns Unavailable until a protected installation-scoped binding and bootstrap/recovery lifecycle exist.

Local Organization policy PoCs are all bounded disposable fixtures, not a production Organization module. Master/Stage topology still gates creation of Organization module/UoW/schema/migrations until G1. No real Organization command yet consumes the IdentityAccess transaction guard; no RowScope consumer exists. The current source inventory is v0.6.

### 10.3 Remaining ordered blockers

The controlling matrix is `MDG-SECURITY-O20E-BLOCKER-CLOSURE-MATRIX-001 v1.0`. Remaining external gates are:
1. IdentityAccess module-owner acceptance of Contracts v1.2 and transaction guard;
2. Platform and affected module-owner acceptance of total global lock order and cross-module revocation linearization;
3. Product Owner/IdentityAccess/Platform/Security decisions and implementation for binding cardinality, attended bootstrap, independent binding change/recovery, emergency disable and restoration;
4. Product Owner/Platform/DB/Security decision on the additional outbox-worker identity, or continued production-delivery disablement; the existing N.6 five-identity baseline remains unchanged;
5. Platform decision on UUIDv7 or alternate generator and clock/multi-instance policy;
6. AuditSecurity durable ACK/API, production worker, external Object Lock Compliance and KMS/HSM key custody/provider;
7. Records/legal jurisdictional retention schedule and Legal Hold authority/scope/release, including backup/replica/export handling;
8. WG-0 Organization migration reservation and explicit G1 implementation authorization;
9. independent exact-SHA Security/Architecture review and findings disposition;
10. explicit Product Owner G1 decision after all required evidence.

PR #92 remains Draft/unmerged. Reviewer `mrdeljoe4` is requested; no submitted review exists. PR #91 is open, non-Draft and unmerged as the full revert of PR #86. PR #92 is based on current main; if PR #91 merges first, reconcile/rebase #92 onto the resulting main and rerun all checks.

**Disposition:** bounded exact-head CI PASS and technical disposable proofs completed within stated scope; cross-owner, production, legal and independent gates remain OPEN. O20-E OPEN; O-20/O-21 OPEN; Organization G1 NOT READY / NOT GRANTED.

**END — O20-E Readiness Review v3.0**


## 11. Permission view immutability and expanded lock-order candidate — 2026-11-22 (v3.1)

The public IdentityAccess permission/role collections were hardened against in-process mutation: Catalog, role grants and RoleCatalog.All now use .NET frozen collections, and the full golden test attempts to mutate the public IReadOnlySet views and requires read-only/NotSupportedException behavior. The exact owner-approved role matrix and supplementary super_admin-only `persons.sensitive.read` control are preserved. This is API surface hardening, not a new authorization grant.

An additional disposable PostgreSQL lock-order method was also added. It records a candidate single-local-transaction order of receipt, approval, Branch, Warehouse, Assignment, domain history and local outbox, with reverse caller-order input. It does not yet include the actual IdentityAccess guard in the same synthetic transaction and is not Platform-approved. Exact latest-head CI remains required.

At intermediate source head `58dd0d052fd2b004d9b36913008c38ce1bd0f8a4`, Release build passed 243/243 with zero warnings/errors and the checksum/spec gate passed 650 tracked files. The full PostgreSQL and Windows workflows for the following expanded source are not yet complete; no final-head PASS is claimed.

**Updated disposition:** PermissionCatalog immutability change SOURCE-IMPLEMENTED and unit-tested at intermediate head; expanded multi-resource lock order PoC added and exact-head CI pending. IdentityAccess owner approval, global Platform order, owner binding, AuditSecurity/worker production decision, Platform ID policy, legal operations, WG-0/G1 and independent review remain OPEN. O20-E OPEN; G1 NOT GRANTED.

**END — O20-E readiness review v3.1**


## 12. OD-01 Unicode conformance expansion — 2026-11-22 (v3.2)

A new PostgreSQL integration test is added to validate every scalar in the pinned OD-01 White_Space, Cc and Cf sets against the corresponding .NET canonicalizer. It verifies embedded forbidden scalar denial, exterior whitespace trimming, valid boundary scalars, identical UTF-8 output, and NFC vectors in Latin/Greek/Arabic/Hangul.

PostgreSQL text cannot carry U+0000; the test therefore asserts .NET rejects it before the database boundary and intentionally excludes it from SQL parameter conformance. No claim is made that PostgreSQL can represent a NUL text scalar.

The new method is a disposable PoC only. It does not introduce production Organization code, schema or runtime. It needs an exact-head PostgreSQL CI pass. The current branch has also just corrected a prior disposable lock-order test's expected history count from four to the actual two inserted events; the rerun is pending.

**Disposition:** OD-01 test breadth strengthened; exact-head CI for both new test changes pending. All external IdentityAccess/Platform/Product Owner/Legal/AuditSecurity/independent-review gates remain open. O20-E OPEN; G1 NOT GRANTED.

**END — O20-E readiness review v3.2**
