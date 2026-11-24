# Stage 08 Security Register — O20 / O21 Closure Status

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 5.4 / 2026-11-22 |
| Supersedes | v5.3 |
| Supersedes | v5.2 |
| Supersedes | v5.1 |
| Supersedes | v5.0 |
| Supersedes | v4.9 |
| Status | O20 / O21 OPEN — EXACT-HEAD CI / OWNER DECISIONS / INDEPENDENT REVIEW REQUIRED |
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
