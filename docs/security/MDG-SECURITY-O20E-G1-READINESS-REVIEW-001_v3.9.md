# O20-E G1 Readiness Review — Ordered Technical Closure

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 |
| Version / Date | 3.9 / 2026-11-23 |
| Supersedes | v3.8 |
| Supersedes | v3.6 |
| Supersedes | v3.5 |
| Supersedes | v3.4 |
| Supersedes | v3.3 |
| Supersedes | v2.7 |
| Supersedes | v2.6 |
| Supersedes | v2.5 |
| Supersedes | v2.4 |
| Supersedes | v2.3 |
| Supersedes | v2.2 |
| Status | O20-E OPEN — 014B559 UUIDv7/DI/PG TESTS PASS; WINDOWS MIGRATION CANCELLED; RETENTION CROSSWALK v0.6 ADDED; NEW HEAD CI PENDING |
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


## 13. Multi-role authorization guard hardening — 2026-11-22 (v3.3)

A code-level review identified a subtle gap in the transaction-scoped guard: the SQL returned every role/grant row contributing to a permission union, but the adapter only read the first row before disposing the reader. That could leave additional grant paths unlocked. The Infrastructure adapter is corrected to drain the entire result set, validate every row's AccountId/SessionId/permission key and return Allow only after all matching rows are consumed.

The expanded PostgreSQL test uses two role membership/grant paths for one permission and seven competing writers covering session revoke, account disable/credential rotation, deletion of both memberships, deletion of both grants and permission deactivation. All must wait on the guard's row-share locks until commit.

The test now also performs Branch, Warehouse and assignment version changes plus domain-history and outbox appends in temporary disposable tables on the same connection/transaction as the actual IdentityAccess guard. This closes a test-coverage gap between "guard locks IdentityAccess rows" and "same transaction commits protected local writes". It remains a synthetic consumer fixture; it is not a production Organization module and does not grant the Platform global order acceptance.

Exact-head build/PostgreSQL/CodeQL/Windows CI for the updated adapter/test is pending. The previous 2afb... run passed its 61/61 base PostgreSQL tests and all explicit O20-E policy/history/retention/concurrency/approval/outbox/idempotency/O21/lock-order/worker/IdentityAccess proof steps, but it predates the multi-role guard correction.

**Disposition:** multi-role grant-lock completeness corrected in source; same-transaction synthetic Organization test added; latest exact-head validation pending. IdentityAccess module-owner acceptance, Platform total lock order, protected Product Owner binding, AuditSecurity production ACK/worker, legal schedule, G1 and independent review remain OPEN. O20-E OPEN; G1 NOT GRANTED.

**END — O20-E readiness review v3.3**


## 14. Exact Linux outcome and bounded Windows migration runner — 2026-11-22 (v3.4)

The exact Linux PostgreSQL job on `f5a38a2004fb5958c4d6bc17210b0965c60931cf` completed all explicit PostgreSQL proof commands, including the multi-role transaction guard and integrated local write/history/outbox transaction. The same head had 243/243 unit tests, 61/61 base PostgreSQL tests, Linux migration idempotence, CodeQL, Windows Preview and Project Charter Owner Gate PASS. Build was 0 warnings / 0 errors.

The Windows PostgreSQL 18.6-5 installer/migration step remained in progress when a later branch push superseded that workflow. It is neither a PASS nor a reported migration failure. To prevent a stuck installer/network request from monopolizing the 25-minute job, the script now enforces a 240-second download deadline and a 900-second installer-process deadline, terminating the installer process tree on timeout. The gate validator and its unit test enforce the timeout contract.

The code/script/validator changes and their refreshed checksum are now on the current PR branch. A new full exact-head CI run is required; its Windows step, checksum/spec gate and all other jobs must be inspected before updating this readiness status to final PASS.

**Disposition:** Linux disposable technical proof set PASS at `f5a38a...`; bounded Windows runner correction source added; latest branch CI PENDING. Owner/platform/legal/production/independent gates unchanged. O20-E OPEN; Organization G1 NOT READY / NOT GRANTED.

**END — O20-E readiness review v3.4**


## 15. Final bounded validation on exact source/test SHA — 2026-11-22


## Exact-head bounded CI evidence — 2026-11-22

All results below refer to the same source/test commit `5eb769cda992d05a784bc9bebc4fe9e30084f81a` on `security/o20-o21-final-closure`:

| Validation | Run | Result |
|---|---:|---|
| MD-Gsm Persons Module Gates | 45063596273 | SUCCESS — 13/13 jobs |
| CodeQL | 45063596261 | SUCCESS |
| Windows Persons Preview Installer | 45063596250 | SUCCESS |
| Project Charter Owner Gate | 45063631382 | SUCCESS |

Primary workflow evidence:
- Release build succeeded with 0 warnings and 0 errors.
- .NET test suite: 243/243 passed.
- Marked disposable PostgreSQL 18 integration suite: 61/61 passed.
- SHA256 verifier: 669 tracked files matched `SHA256SUMS.txt`.
- Linux and Windows PostgreSQL 18.6-5 migration tests: 8 migrations applied on first run and 0 new migrations on repeat; the database session used `migration_runner` with effective owner `mdgsm_schema_owner`.
- O20-G catalog inventory artifact: ID `14770677123`, SHA-256 `09fd2abcfad01a58aaeb7ad0cf1a622260d5eb4639d9a23a3cc24d45e5aab5a3`.
- PII-free CI evidence artifact: ID `14769978817`, uploaded artifact SHA-256 `165f08b510e5b1ef34f9d7350e8f9dcc4cf147ce1b52d68a4dc804b55061dabd`.

The explicit PostgreSQL proof steps in the same primary run all completed successfully:
- OD-01 exhaustive pinned White_Space/Cc/Cf scalar conformance; OD-02/OD-03 Warehouse identity, reassignment and terminal archive;
- immutable Warehouse-to-Branch relationship history;
- fail-closed retention/Legal Hold eligibility and concurrent hold placement;
- cross-aggregate Branch/Warehouse/assignment races, bounded retry and stale approval rejection;
- non-owner maker-checker, self-assignment denial and synthetic Product Owner direct-policy path;
- local AuditIntent/Outbox atomicity, same-EventId/hash replay, changed-payload quarantine and competing worker claims;
- assignment idempotency and deterministic local lock-order envelope;
- outbox-worker object/column least-privilege boundary;
- O20-D backup/isolated restore, O20-G catalog inventory, O20-F emergency lifecycle, O21 hash-chain;
- IdentityAccess live-session permission, revocation linearization and the transaction-scoped authorization guard.

The IdentityAccess transaction guard proof used the actual adapter against disposable `identity.*` tables, drained the complete multi-role permission result set, held the relevant row-share locks through the caller transaction, and verified seven competing revocation/disable/grant mutations remained blocked until commit. The same disposable transaction also wrote synthetic receipt, approval, Branch, Warehouse, assignment, history and pending outbox facts.

**Evidence boundary:** all Organization-shaped tables and the canonical-audit endpoint in these proofs are disposable fixtures. These results do not create or prove a production Organization module/UnitOfWork, production AuditSecurity API/durable ACK, trusted Product Owner binding, Platform-wide accepted lock order, legal disposal service, WORM/KMS custody or G1. The later documentation-only successor commits are not covered by this run and must receive their own PR checks.


## 16. Ordered remaining release gates

1. **IdentityAccess owner acceptance:** formally accept Contracts v1.2, the current request-time principal/permission semantics and the transaction-scoped row-lock guard.
2. **Platform and affected module owners:** accept one total lock-order registry and revocation/authorization linearization contract. The current deterministic local/participant PoCs are not a cross-module order approval.
3. **Product Owner binding lifecycle:** decide one active principal per installation (or other cardinality), installation scope, attended bootstrap designation, transfer/change, independent recovery, emergency disable and restore. The current adapter intentionally returns `Unavailable` until a protected binding exists.
4. **Organization permission namespace:** approve exact assignment-management permission keys and role mapping. Until then no `organization.*` key or grant is added.
5. **AuditSecurity / worker:** publish the canonical event and durable ACK contract; decide the additional worker identity or retain production delivery disabled; approve retry/quarantine/alert/runbook and real network/service identity.
6. **Platform identifier policy:** select UUID version/generator and define clock/multi-instance compatibility. UUIDv7 remains a recommendation, not a platform standard.
7. **Records/legal authority:** approve record-class/jurisdiction retention schedule, hold placement/change/release authority, backup/replica/export scope, privacy handling and lawful disposal.
8. **Production O21 trust infrastructure:** select and provision actual KMS/HSM custody, external Object Lock Compliance (or proven equivalent), verifier deployment, restore, alerts and operating ownership.
9. **WG-0 and G1:** reserve the Organization migration slot, authorize implementation explicitly, then implement the actual Organization module/UnitOfWork/schema/permissions only within that authorization.
10. **Independent exact-SHA review:** obtain reviewer findings against a single final source SHA, fix Critical/High findings and record dispositions. Only after these gates may Product Owner grant G1.

### 16.1 What is technically complete in this bounded phase

OD-01 through OD-03 and OD-05's fail-closed eligibility have executable PostgreSQL proofs. The IdentityAccess request/transaction contract and adapter are implemented and tested. The local assignment policy, relationship history, concurrency, idempotency, local outbox/replay/quarantine, worker least-privilege and deterministic candidate order proofs all passed the exact bounded CI above. This closes the technical PoC and CI portion only.

### 16.2 What remains deliberately unavailable

There is no production Organization module or UoW; no current RowScope consumer; no trusted Product Owner binding; no accepted Organization management permission namespace; no Platform-approved global order; no production AuditSecurity API/durable ACK/worker/WORM/KMS; and no legal disposal schedule/production Hold service. These are not CI defects and cannot be made true by changing a readiness status.

**Disposition:** exact-head bounded technical validation PASS; independent review and external owner/platform/legal/production gates OPEN. O20-E OPEN; Organization G1 NOT READY / NOT GRANTED.

**END — O20-E Readiness Review v3.5**


## 17. Iran records-retention legal source crosswalk — 2026-11-22 (v3.6)

The retention policy v0.4 and owner/platform decision packet v0.2 now record a current source crosswalk:
- Commercial Code Articles 1, 2(1), 6, 13 and 19: possible ten-year minimum for specified commercial books from year-end, but small-merchant exemption/classification applies.
- AML Executive Regulation Article 142 (amended 1404/07/27): ten-year retention for specified financial-institution records; Article 149 extends specified AML duties to listed non-financial professions and activities. Generic mobile retail/repair is not expressly in the listed category set reviewed; a residual high-risk designation requires an effective Council decision.
- VAT Executive Regulation Article 103: five years for ordinary taxable records, distinct capital-asset and real-estate rules, and possible extension, according to the retrieved unofficial translation; current applicability and controlling Persian text require tax-counsel verification.

The legal review therefore does **not** approve a ten-year, five-year or fifteen-year retention duration for Organization assignment history. It does not conclude that mobile repair/retail is an AML-covered profession or that every operator is a merchant required to maintain the same books. Those are record/entity-specific legal classifications.

**Disposition:** legal research crosswalk COMPLETE; record-class/jurisdiction schedule, Legal Hold authority/scope/release, privacy, backup/replica/export treatment and lawful disposal workflow remain OPEN. OD-05's accepted retain-by-default / no-auto-delete baseline remains controlling. O20-E OPEN; G1 NOT GRANTED.

**END — O20-E readiness review v3.6**


## 18. Exact-head CI confirmation and public-contract DI registration test — 2026-11-23 (v3.7)

The exact branch source/test head `83b8bf35345b14faf6ce549d80640180aca1c28e` passed:
- Primary Persons Module Gates run `45066929461`: all 13/13 jobs SUCCESS;
- CodeQL `45066929413`: SUCCESS;
- Windows Preview Installer `45066929420`: SUCCESS;
- Project Charter Owner Gate `45067172747`: SUCCESS.

The primary run showed Release build 0 warnings/0 errors; .NET 243/243; PostgreSQL 18 integration 61/61; Linux and Windows application-owned migrations 8 first run / 0 repeat; source checksum validation 683/683. The explicit O20-E/IdentityAccess/O20-D/O20-F/O20-G/O21 tests and database proofs listed in blocker matrix v1.7 passed in their disposable fixtures.

A new regression test was then added to `PersistenceRegistrationTests.cs` to prove the public IdentityAccess authorization contract is registered and remains fail-closed when no database is configured. The modified test and updated SHA256 manifest are at head `6a07f13fb8cfe00ddf317acf7e86157055eebeed`. This test has not yet received exact-head CI, so it is PENDING and must not be counted as PASS.

### 18.1 Review and PR state

PR #92 remains Draft/unmerged and is based on main that still contains the prematurely merged PR #86. Independent reviewer `mrdeljoe4` remains requested; no review has been submitted. PR #91 remains open, non-Draft and unmerged as a full revert of PR #86; its exact pre-PR-86 tree restoration and own CI checks have been recorded, but its independent review is also outstanding. No merge has occurred.

### 18.2 Release blockers remain external or G1-gated

The exact-head CI does not close the following:
- IdentityAccess module-owner acceptance of public Contracts v1.2;
- Platform and affected module-owner acceptance of a total lock-order/commit-time revocation protocol;
- protected Product Owner binding/cardinality/bootstrap/change/recovery/disable/restore;
- approved Organization assignment-management permission keys and role mapping;
- AuditSecurity canonical API/durable ACK and worker identity decision;
- Platform ID generator/version decision;
- production O21 HSM/KMS, Object Lock Compliance, verifier, restore and operations;
- legal record-class schedule and Legal Hold operational authority;
- WG-0 schema/migration reservation and explicit G1 implementation authorization;
- independent exact-SHA Security/Architecture review.

No Organization production module/UoW/schema, migration, role/grant, Product Owner binding, additional worker identity/credential, ReportingReadOnly, canonical audit network adapter or external key/storage provider was created.

**Disposition:** bounded exact-head technical CI PASS at 83b8bf…; new fail-closed DI registration test pending at 6a07f13…. O20-E OPEN; G1 NOT READY / NOT GRANTED; O-20 and O-21 OPEN.

**END — O20-E readiness review v3.7**


## 19. UUIDv7 and records-retention successor review — 2026-11-23 (v3.8)

### 19.1 Identifier source/test scope

Platform Identifier Policy Request v0.2 records a recommendation to use native PostgreSQL `uuid` with .NET `Guid.CreateVersion7()` for new opaque IDs in this deployment, while explicitly withholding a Platform-wide decision. Two tests now exist:
- `UuidV7IdentifierConformanceTests`: AccountId/PartyId UUIDv7 version and RFC variant, explicit timestamp encoding, same-millisecond uniqueness sample, pre-epoch rejection and 20,000 parallel in-process AccountId generation checks.
- `O20OrganizationUuidV7PostgresRoundTripProofTests`: 512 generated UUIDv7 values stored/read using PostgreSQL native `uuid`, proving bit-preserving Guid round-trip in a marked disposable database.

This does not prove collision behavior across hosts/processes under deployment peak load, clock rollback/skew coordination, restore/reinstall, cross-language compatibility or index performance. UUID order is not authoritative chronology or commit sequencing. Platform acceptance remains OPEN.

### 19.2 Retention legal research

Retention policy v0.5 expands the Iran legal-source and operator/record-class decision checklist. It requires legal classification of the specific entity and activities, statutory bookkeeping scope, VAT taxpayer/record categories, AML obliged-person/activity/designation, exact record classes, trigger dates, extensions/holds and disposal authority. It intentionally selects no numeric duration for Organization history. OD-05 retain-by-default/no-auto-delete remains controlling.

### 19.3 Exact-head validation

The completed 83b8bf… evidence remains valid for its exact sources only. UUIDv7 tests, policy v0.2/v0.5 and DI-registration test are additional changes. The exact current successor `0c2543708b7b92e8848a6d58b007ccb58036a11d` has Primary, CodeQL and Windows Preview workflows pending; Project Charter Owner Gate has succeeded. Do not mark new tests PASS until the exact-head PostgreSQL logs are inspected.

**Disposition:** UUIDv7 PoC source added; Platform standard OPEN. Iran record-class/legal schedule OPEN. O20-E OPEN; G1 NOT READY / NOT GRANTED.

**END — O20-E readiness review v3.8**


## 20. Current VAT/AML retention crosswalk correction and CI gate — 2026-11-23 (v3.9)

### 20.1 Legal-source correction

`MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.6` is now controlling. It corrects Article 103 detail: five years for ordinary taxable books/records, five years measured after the adjustment-period Tax Period for capital-asset records, fifteen years for real-estate records, and possible notified extension up to five years. The English PDF is explicitly an unofficial translation; the Persian text and precise record applicability remain for Iran counsel/tax adviser.

The former ten-year VAT Act 1387 Article 34 is not a current blanket rule after VAT Act 1400 Article 55 repeal. Commercial Code Article 13's ten years apply to the defined books under Article 6 subject to small-trader classification. AML Law Article 7 five-year minimum and amended executive regulation Articles 142/149/158 must be assessed against the actual operator, category, activities and latest amendments. No period is applied to Organization history by analogy.

No numeric Organization-history schedule is selected. OD-05 retain-by-default/no-auto-delete remains controlling.

### 20.2 Exact CI results at 014b559…

On exact source/test SHA `014b5594b0da020e9e6fee9b7a0c3eb03264eed6`:
- Release build: 0 warnings / 0 errors;
- .NET tests: 249/249 PASS, including the fail-closed DI registration test and UUIDv7 conformance unit tests;
- PostgreSQL 18 Linux integration base suite: 62/62 PASS;
- explicit UUIDv7 PostgreSQL native UUID round-trip test: PASS;
- Linux migration idempotence: 8 first run / 0 second run;
- SHA256 verification: 696 tracked files matched;
- CodeQL, Windows Preview and Project Charter Owner Gate: SUCCESS;
- Windows PostgreSQL migration job: CANCELLED; CI evidence-enforcement step failed because this mandatory gate was cancelled.

A rerun was requested for the cancelled Windows job. Further document commits have since moved the branch, so a fresh all-gates validation on the latest exact head is required. Do not represent the Windows job or the entire Primary workflow as green.

### 20.3 Updated gate state

- IdentityAccess public contract and transaction-scoped permission guard: source-published and bounded live-session/transaction tests pass; module-owner acceptance remains OPEN.
- Product Owner authority: deliberately returns Unavailable until trusted binding enrollment/change/recovery/restore is implemented and accepted.
- UUIDv7: current bounded test passes; multi-process/host, cross-host clock rollback/skew, compatibility and production performance remain OPEN for Platform.
- Platform total lock order: OPEN; local candidate tests are not a global rank approval.
- AuditSecurity canonical API/durable ACK and production worker remain OPEN.
- Legal retention operator/record-class classification, Legal Hold authority/scope/release and lawful disposal remain OPEN.
- no current Organization/Inventory/Reporting RowScope consumer exists; future consumers must be inventoried/tested.
- Independent review, WG-0 reservation and G1 are not complete.

**Disposition:** exact bounded tests PASS at 014b559…; Windows migration gate is not complete; final head validation pending. O20-E OPEN; G1 NOT READY / NOT GRANTED.

**END — O20-E readiness review v3.9**
