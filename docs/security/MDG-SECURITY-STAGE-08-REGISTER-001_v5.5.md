# Stage 08 Security Register — O20 / O21 Closure Status

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 5.5 / 2026-11-22 |
| Supersedes | v5.4 |
| Supersedes | v5.3 |
| Supersedes | v5.2 |
| Supersedes | v5.1 |
| Supersedes | v5.0 |
| Supersedes | v4.9 |
| Status | O20 / O21 OPEN — BOUNDED EXACT-HEAD CI PASS; OWNER / PLATFORM / PRODUCTION / INDEPENDENT GATES OPEN |
| Branch | security/o20-o21-final-closure |
| PR | #92 Draft / unmerged |

## 1. O21 outbox worker least-privilege proof

Added `tests/MDGsm.Domain.Tests/Security/O20OutboxWorkerLeastPrivilegePostgresProofTests.cs` and an explicit workflow step. The marked-disposable PostgreSQL fixture:
- separates immutable event envelope/payload from delivery state;
- creates a synthetic worker LOGIN and NOLOGIN ACL role with a single SET-only membership;
- gives SELECT to the event/delivery tables and column-scoped UPDATE only to delivery metadata;
- verifies payload/hash/event ID are not changeable;
- denies deletion, branch/warehouse reads, IdentityAccess credential reads, business mutation, DDL, role creation and assumption of owner/runtime/migration/backup/reporting/emergency roles.

The test contains no production credential, secret, real schema, grant, AuditSecurity API, WORM/KMS resource or worker process.

## 2. O20 N.6 identity model remains unchanged

The accepted five logical identities are retained. The dedicated outbox-worker role is a tested candidate but adds a logical service identity; it is not owner-approved. A separate owner/Platform/DB/Security decision is required before changing N.6 or provisioning a production role. The runtime LOGIN shim remains a physical connection-binding PoC only.

## 3. Overall gate state

- IdentityAccess public contract/request-time permission: source implemented; exact-head CI and module owner acceptance pending.
- Commit-time authorization: disposable FOR SHARE serialization proof; actual Organization shared transaction participant absent.
- Product Owner binding: unavailable/fail-closed; lifecycle absent.
- Organization maker-checker/history/idempotency/outbox/lock tests: disposable PostgreSQL sources present; exact-head validation mandatory.
- Runtime and outbox worker identity/grant deployment: unresolved owner decision; no credentials or roles provisioned.
- AuditSecurity canonical API/ACK, WORM, HSM/KMS, verifier/restore/alerts: production integrations absent.
- Legal schedule/Legal Hold and future RowScope consumers: open.
- Independent exact-SHA review/G1: not performed/not granted.

**Disposition:** O20-E OPEN; O-20 OPEN; O-21 OPEN; Organization G1 NOT READY / NOT GRANTED.

**END — Stage 08 register v5.0**


## 4. IdentityAccess transaction-scoped authorization guard — source implementation (2026-11-22; v5.1)

The public IdentityAccess Contracts interface now offers `EvaluateCurrentPermissionInTransactionAsync`, receiving the caller's already-open ADO.NET `DbConnection` and `DbTransaction`. IdentityAccess Infrastructure verifies they belong together, obtains current server-validated principal facts, checks the exact current permission/session/account/credential version, and obtains PostgreSQL `FOR SHARE` row locks over the account, session, account-role, role, role-permission and permission rows contributing to an Allow.

Added an explicit CI gate for `IdentityAccessTransactionScopedAuthorizationGuardPostgresProofTests`. The test invokes the actual adapter against real `identity.*` tables in the marked disposable PostgreSQL database, persists a protected-write probe in the same transaction, and verifies five independent IdentityAccess mutation transactions are blocked via `pg_blocking_pids()` until the guard transaction commits.

This is a meaningful source-level improvement but is not yet a closure claim:
- exact-head build/PostgreSQL proof result is pending;
- real Organization UoW/module has not been created and does not consume the method;
- no Platform-approved global lock order authorizes the IdentityAccess/Organization row order yet;
- all deadlock/timeout/retry/rollback/crash/unknown-commit cases remain open;
- no IdentityAccess module-owner or independent Security/Architecture approval is recorded.

No production grants, runtime roles, Organization migrations or cross-module private-table access are introduced. The method itself is implemented only inside IdentityAccess Infrastructure; a consumer receives a typed decision through the public Contracts assembly.

**Current disposition:** IdentityAccess transaction guard source implemented / PostgreSQL proof added pending exact-head CI; Organization transaction integration and global lock order OPEN; O20-E OPEN; O20/O21 OPEN; G1 NOT GRANTED.

**END — Stage 08 register v5.1**


## 5. IdentityAccess public contract v1.2 and transaction guard proof — 2026-11-22 (v5.2)

The public IdentityAccess Contracts version marker is now `1.2`. The public README records:
- request-scoped current principal resolution and exact live permission check;
- transaction-scoped `EvaluateCurrentPermissionInTransactionAsync`, using the supplied connection and its transaction;
- exact locked IdentityAccess row set: account, session, account-role, role, role-permission and permission;
- typed Allow/Deny/Unavailable and fail-closed behavior;
- no inference of Product Owner from role/client flag;
- no cross-module use until Platform and affected module owners accept the total lock order.

The ArchitectureBoundary test has been updated to enforce IdentityAccess contract marker 1.2.

### Bounded validation evidence

At source/test head `c67d892197d02409827bbc127a296fd64374576b`, Linux PostgreSQL CI reported 52/52 tests passed and the explicit transaction guard test passed. The test used the actual Infrastructure adapter and actual IdentityAccess tables in the marked disposable DB. Five concurrent IdentityAccess mutation transactions were observed blocked behind the authorization guard using `pg_blocking_pids()`, then completed after the protected transaction ended.

The marker/README/architecture test changes in the current successor came after that SHA. Full Linux/Windows migrations, CodeQL and Windows preview must be revalidated on the new resulting head; no latest-head green claim is made here.

**Current disposition:** transaction guard source and bounded real-IdentityAccess PostgreSQL test exist; previous exact test SHA PASS. Latest public version marker/test changes await full CI; Platform lock-order, real Organization UoW, Product Owner binding and independent review remain OPEN. O20-E/O-20/O-21 OPEN; G1 NOT GRANTED.

**END — Stage 08 register v5.2**


## 6. Current source-topology / RowScope revalidation (2026-11-22; v5.3)

RowScope inventory v0.5 inspects exact source commit `b80a040dea5ffb8cc7db98f3bb23cd6f8c5b48c1` and API Program.cs blob `98eb172eb2572f69095f2a54168f74bcd91305b6`. No executable Organization, Inventory, Reporting or AuditSecurity module and no Branch/Warehouse report/query/export route exists at that SHA. The Persons Party detail path has no BranchId/WarehouseId source context.

Current `reports.*` PermissionCatalog entries and transaction-scoped IdentityAccess guard do not supply RowScope. No positive/negative RowScope consumer test is currently applicable; ReportingReadOnly remains absent and unprovisioned. Re-inventory is mandatory at the introduction of every future endpoint/worker/consumer.

**Disposition:** current RowScope absence revalidated; future consumers OPEN. No new routes, schema, roles, grants or data access authorized. O20-E/O-20/O-21 remain OPEN; G1 NOT GRANTED.

**END — Stage 08 register v5.3**


## 7. Bounded exact-head O20/O21 evidence and remaining owner/platform decisions (2026-11-22; v5.4)

### 7.1 Exact CI evidence on source/test head 448b6cc9809a934a85507f4213de1e4395a0b74b

The Linux PostgreSQL 18 job persons-postgres-migrations in workflow 45046411908 completed successfully:
- application-owned migrator applied eight migrations; repeat run applied zero new migrations;
- O20-D backup/isolated restore and O20-G catalog inventory/effective privilege digest passed;
- full .NET integration suite passed 52/52;
- O20 support identity, runtime login mapping and O20-F emergency lifecycle tests passed;
- O20-E canonicalization and OD-02/OD-03 lifecycle, immutable Warehouse-Branch history, maker-checker/owner direct path, local outbox/ACK, idempotency concurrency and deterministic candidate lock-order tests passed;
- O21 hash-chain PostgreSQL PoC passed;
- IdentityAccess live permission, revocation/linearization and transaction-scoped authorization guard tests passed;
- Accounting AP-03/AP-04 disposable tests passed.

The transaction-scoped guard test exercised the actual IdentityAccess Infrastructure adapter against the real identity.* schema in the marked disposable DB and observed five concurrent authorization-row mutations blocked behind the caller's transaction using pg_blocking_pids().

On the same SHA, CodeQL run 45046411892 succeeded and Windows Preview Installer run 45046411889 succeeded. Windows PostgreSQL migration job persons-postgres-windows-migrations was still in progress at last inspection. Therefore the overall primary workflow does not yet have an all-platform completed PASS. Do not claim full CI green until the Windows job and overall workflow complete.

### 7.2 Remaining decision packet

MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.1 consolidates recommendations and explicit decisions still required:
- Product Owner binding cardinality, bootstrap designation and independent owner-binding lifecycle;
- trusted local one-time bootstrap channel, installation binding and listener fail-closed ceremony;
- Recovery endpoint boundary, verified-email enrollment, SMTP and HMAC key service;
- whether to approve a dedicated sixth outbox-worker logical identity, while preserving the accepted N.6 five-identity baseline until a decision is recorded;
- Platform acceptance of UUIDv7 (or alternate) generator/version policy;
- Platform and affected module-owner acceptance of global lock ranks and full cross-module concurrency/failure matrix;
- actual KMS/HSM and independent Object Lock Compliance provider/custodians/cadence/retention provisioning;
- jurisdictional records schedule and Legal Hold authority/enforcement.

These entries are recommendations or pending decisions, not owner-accepted implementation. No production binding, credential, sixth identity, UUID policy, global lock order, provider resource, retention period, DDL, grant or runtime is created by the packet.

### 7.3 Independent review and G1

An independent reviewer request was sent to the currently designated WG-QA reviewer for PR #92. No review approval has been received in this snapshot. PR #92 remains Draft/unmerged; PR #91 remains Draft/unmerged. The main branch still contains the prematurely merged PR #86; PR #91 is the full-revert proposal and must be reconciled with this branch if accepted/merged.

No Organization runtime has been introduced because the repository module-topology policy requires G1 authorization before a module is introduced. O20-E/O-20/O-21 remain OPEN. Organization G1 remains NOT READY / NOT GRANTED.

**END — Stage 08 register v5.4**


## 8. Exact-head validation update — 2026-11-22 (v5.5)

### 8.1 Completed validation on code/test head 5096fe9693a65213321f1b123341a220a9826a7d

The latest completed primary workflow for this code/test head was run `45047243289` and completed SUCCESS with all 13 jobs successful. Related workflows on the same SHA:
- CodeQL `45047243377`: SUCCESS;
- Windows Persons Preview Installer `45047243268`: SUCCESS;
- Project Charter Owner Gate `45047290161`: SUCCESS.

The primary workflow evidence includes:
- .NET Release build: 0 warnings, 0 errors;
- .NET unit tests: 242/242 passed;
- marked disposable PostgreSQL 18 integration suite: 52/52 passed;
- application-owned migrator: 8 migrations applied on first run and 0 new migrations on repeat, on both Linux and Windows PostgreSQL 18 jobs;
- O20-D backup / isolated restore proof and O20-G catalog/effective-privilege digest proof passed. O20-G digest: `278a2fb1e7e5fbd55cd594f1afdd97c37a2f3e00fc71a788e8bc73c28b17b50f`;
- O20 support identity, runtime login mapping and O20-F emergency lifecycle PoCs passed;
- O20-E OD-01/OD-02/OD-03, immutable Warehouse-Branch history, maker-checker / Product Owner direct policy, local outbox atomicity / replay / quarantine, idempotency concurrency and deterministic candidate lock-order proofs passed;
- O21 hash-chain PostgreSQL PoC passed;
- IdentityAccess live-session permission, revocation/permission linearization and transaction-scoped guard tests passed. The guard test exercised the actual IdentityAccess Infrastructure adapter against the actual disposable `identity.*` tables and observed five competing mutation transactions blocked by the held `FOR SHARE` lock set through `pg_blocking_pids()`;
- Accounting AP-03/AP-04 disposable PostgreSQL proofs passed;
- SHA256SUMS validation, spec lint and PII-free CI evidence enforcement passed.

The worker least-privilege test `O20OutboxWorkerLeastPrivilegePostgresProofTests` is tagged `Category=Integration`, so it was included in the 52/52 disposable integration suite. A separate explicit class-filter workflow step has now been added to make this proof independently visible in the next run. The first run containing that new workflow step (`45048592314`, commit `30d99841ec8839573db03adeade3e30b45535097`) was cancelled after the checksum verifier correctly detected that the workflow checksum had not yet been updated. The manifest correction is committed in the current successor; the explicit-step rerun is pending.

### 8.2 Current branch and review state

PR #92 remains Draft / unmerged. The current code/test source baseline is the SHA above; this v5.5 document and the corrected checksum manifest are a documentation/evidence successor. PR #91 remains Draft / unmerged as the full revert proposal for premature PR #86. No direct main change or merge is performed.

The designated reviewer `mrdeljoe4` is requested on PR #92, but the review list remains empty. No independent approval or findings disposition is claimed.

### 8.3 Closure boundary remains unchanged

The green bounded CI does not close production O20/O21:
- IdentityAccess module-owner acceptance of Contracts v1.2 and transaction guard remains pending.
- The IdentityAccess transaction guard has no real Organization UnitOfWork consumer; Platform-wide lock order and cross-module races remain unaccepted.
- Product Owner binding/enrollment/change/recovery/disable/restore remains unavailable and fail-closed.
- Organization management permission namespace/role mapping remains unapproved.
- The current five-category N.6 identity baseline is unchanged. A sixth outbox-worker identity is not approved or provisioned; production O21 delivery remains disabled until an approved non-overlapping worker identity exists.
- Production AuditSecurity canonical writer/API/durable ACK, production source-module outbox, external Object Lock Compliance anchor, KMS/HSM custody, verifier, restore continuity, alerts and runbooks remain absent.
- Legal retention schedule and Legal Hold implementation remain open; OD-05 retain/no-auto-delete baseline is owner-accepted.
- No current Organization/Inventory/Reporting Branch/Warehouse source consumer exists; future RowScope implementation and tests remain gated on a real consumer.
- No Organization schema, DDL, migration, route, runtime module, database grant, credential, reporting role or production access is created.
- Independent exact-SHA Security/Architecture review and explicit G1 decision remain required.

**Disposition:** the tested source/test head has a completed all-job CI PASS on Linux and Windows plus CodeQL, preview installer and owner-gate success. The explicit worker-only rerun is pending due the newly added workflow filter. O20-E, O-20 and O-21 remain OPEN; Organization G1 NOT READY / NOT GRANTED.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v5.5**
