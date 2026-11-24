# Platform Global Lock-Order Registry — O20-E Candidate

| Field | Value |
|---|---|
| Document ID | MDG-PLATFORM-LOCK-ORDER-REGISTRY-001 |
| Version / Date | 0.6 / 2026-11-22 |
| Supersedes | v0.5 |
| Status | PLATFORM REVIEW PROPOSAL — NON-NORMATIVE; NO LOCK ORDER ACCEPTED |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Related design | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v0.6 |

## 1. Governing constraint

Master v3.0.3 requires a global Lock-Order Registry and real competing-writer evidence for deadlock, timeout, crash, retry and replay. A suggested order is not a fact until tested. Global OCC and unbounded retry are prohibited. This document therefore proposes an experiment and resource inventory only; it does not set a production lock order.

The current IdentityAccess `IIdentityAccessUnitOfWork` is an internal serializable IdentityAccess boundary, not a cross-module lock contract. Organization must not query or lock IdentityAccess private tables.

## 2. Candidate Organization resources

| Resource | Purpose | Candidate deterministic ordering |
|---|---|---|
| Idempotency receipt/result | Duplicate and unknown-commit recovery | ActorId, operation code/version, key digest |
| Branch aggregate | Branch state and code reservation | BranchId ascending |
| Warehouse aggregate | Current Branch link, lifecycle, row version | WarehouseId ascending |
| Account assignment | Branch/Warehouse assignment lifecycle | AssignmentId ascending; otherwise resource type, resource ID, AccountId |
| Approval request | Maker-checker workflow | ApprovalRequestId ascending |
| Domain history | Immutable event history | Insert-only append |
| Local AuditIntent/Outbox | Durable delivery intent | Insert atomically; worker delivery metadata in a separate short transaction |

These are candidate sort keys only, not globally accepted lock ranks.

## 3. Candidate local transaction envelope

A mutating Organization command is expected to atomically persist: command idempotency receipt/fingerprint, authorized aggregate state transition and version, immutable domain history, local AuditIntent/Outbox, and durable command result. Failure before commit rolls the whole local unit back. External AuditSecurity delivery occurs only after commit and never while Organization aggregate locks are held.

A worker claims an outbox row in a short transaction, commits the lease, then calls AuditSecurity. It must not hold an outbox claim lock while acquiring domain locks. Final isolation level, lock ranks and authorization linearization remain Platform/Application/Security decisions.

## 4. IdentityAccess boundary — unresolved linearization

The API currently resolves a live session and installs a server-authenticated AccountId. IdentityAccess permission storage is internal. No published cross-module principal/permission runtime contract exists. Organization must not read IdentityAccess private tables, accept client ActorId/role/permission/owner flags, or rely on stale cached Allow.

IdentityAccess and Platform must define how session/account/permission revocation races with a protected Organization write. A successful pre-transaction check alone is not proof that revocation wins at commit. Candidate designs require a versioned server-side decision and a documented revalidation/linearization protocol; no distributed transaction is assumed.

## 5. Required two-writer PostgreSQL experiments

| ID | Race/failure | Required invariant |
|---|---|---|
| LO-01 | Same idempotency key + same payload concurrently | One effect; both callers resolve same durable result |
| LO-02 | Same key + different payload | Second request denied; first result unchanged |
| LO-03 | Concurrent grants for same Account/resource | No duplicate active assignment/lost update |
| LO-04 | Assignment revoke vs protected source write | No stale assignment use after accepted revocation linearization |
| LO-05 | Warehouse reassignment vs assignment create/change/revoke | Exact WarehouseId binding retained; no implicit grant/revoke |
| LO-06 | Warehouse reassignment vs Branch deactivate/archive | Relationship remains valid or entire command fails |
| LO-07 | Maker command vs approval and concurrent command edit | Approver distinct; approved fingerprint/version equals executed command |
| LO-08 | Two transactions touching multiple Branch/Warehouse/Assignment rows in reversed input order | Stable acquisition plan; no unhandled deadlock |
| LO-09 | Backend killed before commit | No partial state/history/outbox |
| LO-10 | Unknown commit then same-key replay | Original response/IDs recovered; no duplicate event |
| LO-11 | Lock timeout/deadlock followed by bounded retry | No duplicate side effect; retry measurable and bounded |
| LO-12 | AuditSecurity outage or worker crash | Local intent durable; delivery gaps visible and recoverable |

Each experiment must use at least two independent PostgreSQL connections, deterministic barriers, SQLSTATE/lock-wait capture, bounded retry and final-state/event assertions.

## 6. Candidate guardrails pending approval

Sort all same-rank identifiers deterministically before locking; never acquire a lower accepted rank after a higher rank; keep network calls outside domain transactions; do not lock immutable history rows for append; do not use timestamps or UUID order as commit sequencing; use OCC only where a specific aggregate requires it; retry only classified transient SQLSTATEs and never policy/integrity failures.

## 7. Acceptance

Platform must publish the global resource taxonomy/order across IdentityAccess, Finance, Accounting, Inventory and Organization. Each data owner must accept its boundary. PostgreSQL concurrency/failure experiments must pass and produce machine-readable evidence. The final registry must be versioned, checksummed and independently reviewed against one exact SHA. Separate explicit G1/implementation authorization remains required.

**Disposition:** experiment matrix prepared; Platform acceptance, final order, implementation and PostgreSQL concurrency evidence OPEN. No order in this proposal is approved.

**END — MDG-PLATFORM-LOCK-ORDER-REGISTRY-001 v0.1**


## 8. Additional Organization-local PostgreSQL ordering experiment — 2026-11-22 (v0.2)

Added `tests/MDGsm.Domain.Tests/Security/O20OrganizationDeterministicLockOrderPostgresProofTests.cs` and an explicit PR workflow step. The test uses two independent connections and reversed caller input arrays. Each transaction sorts the same set of aggregate UUIDs by canonical lowercase UUID text and acquires row locks in that deterministic order, then increments all rows. Both writers must complete, all rows must reach the expected version, and lock/statement timeout or deadlock causes the test to fail.

This demonstrates one candidate Organization-local ordering experiment for a single aggregate resource class. It does **not** establish the platform-wide total order, cross-module lock ranks, AccountId/Permission/owner-binding revocation linearization, Finance/Accounting/Inventory participation, outbox-vs-domain ordering, or maker-checker concurrency. The canonical UUID text ordering itself remains a proposal pending Platform approval. Exact-head CI is pending.

**Disposition:** local Organization row-order experiment added; global registry, other module owners' ranks, and all remaining race cases remain OPEN. No normative lock order is approved.

**END — Platform Lock-Order Registry proposal v0.2**


## 9. Candidate multi-aggregate rank experiment — 2026-11-22 (v0.3)

A second disposable PostgreSQL test was added to `O20OrganizationDeterministicLockOrderPostgresProofTests`:

`Candidate_branch_warehouse_assignment_rank_order_avoids_reversed_multi_table_cycle`

The experiment introduces three distinct fixture resource classes and a candidate local ordering:
1. Branch aggregate rows, UUIDs sorted ordinally by canonical lowercase `Guid.ToString("N")`;
2. Warehouse aggregate rows, sorted by the same UUID ordering;
3. Account assignment rows, sorted by the same UUID ordering.

Two independent PostgreSQL transactions receive the same Branch/Warehouse/Assignment sets in opposite caller order. Each transaction sorts within the prescribed rank, acquires `FOR UPDATE` locks rank-first and ID-second, then updates all rows. Both must commit, and all six rows must reach the expected version. The test uses lock/statement timeouts and a synchronization delay to make the competing writer overlap at the first resource.

This expands local disposable evidence from one homogeneous aggregate table to a candidate three-class order. The rank sequence is deliberately a test proposal, **not** a published Platform lock order. It does not include the IdentityAccess guard, idempotency receipt, maker-checker approval, Finance/Accounting/Inventory resources, or AuditSecurity/outbox worker. The IdentityAccess transaction guard is separately proven against its actual disposable schema; the outbox worker is separately tested not to acquire domain locks.

### 9.1 Candidate resource rank for Platform review

| Candidate rank | Resource | Ordering discipline | Evidence status |
|---:|---|---|---|
| 00 | Command idempotency receipt / request fingerprint | Stable actor + operation + key digest; unique conflict serializes duplicate command | Separate O20 idempotency PoC |
| 10 | IdentityAccess account/session/role/membership/grant/permission facts | Public transaction guard; retain row-share locks through protected local commit | Actual adapter + disposable IdentityAccess PostgreSQL proof |
| 20 | Maker-checker approval request | RequestId ascending; bind decision to exact immutable request fingerprint/version | Separate approval/cross-aggregate PoC |
| 30 | Branch aggregates | Binary UUID order (canonical UUID text is current .NET/PostgreSQL candidate comparator) | New multi-class rank PoC |
| 40 | Warehouse aggregates | Binary UUID order | New multi-class rank PoC |
| 50 | Account Branch/Warehouse assignment aggregates | Resource type, resource ID, AssignmentId/AccountId tie-breaker as approved by owner | New multi-class rank PoC |
| 60 | Immutable domain history | Append only after aggregate locks; no prior history-row lock for ordinary append | Relationship-history PoC |
| 70 | Local AuditIntent/Outbox | Insert atomically after domain facts; network delivery after commit; worker claims in separate short transaction | Outbox atomicity/worker PoCs |

This is a candidate total order for review only. Platform must validate whether rank 00 and rank 10 should be swapped or split, reconcile existing Finance/Accounting/IdentityAccess/Inventory transactions, and publish one authoritative cross-module total order. No module may infer the rank order from this proposal.

### 9.2 Retained release blockers

The candidate experiment still requires exact-head CI. Platform and affected module owners must accept the total order, including cancellation, lock timeout/deadlock classification, bounded retry, rollback, unknown commit, replay, outbox reconciliation and cross-module races. No Organization write consumer is authorized before that acceptance.

**Disposition:** candidate three-class local resource order and supporting fixture test added; exact-head CI pending; Platform-wide total order remains OPEN and NON-NORMATIVE. O20-E OPEN; G1 NOT GRANTED.

**END — Platform Lock-Order Registry proposal v0.3**


## 10. Candidate transaction envelope across receipt, approval, aggregates, history and outbox — 2026-11-22 (v0.4)

A third disposable PostgreSQL method has been added to `O20OrganizationDeterministicLockOrderPostgresProofTests`:

`Candidate_transaction_order_persists_receipt_approval_aggregates_history_and_outbox_atomically`

The fixture creates candidate local resources and runs two independent writers with reversed caller arrays. Each writer acquires:
1. its command idempotency receipt row;
2. its approval-request row;
3. all Branch rows in deterministic UUID order;
4. all Warehouse rows in deterministic UUID order;
5. all Assignment rows in deterministic UUID order;
6. append-only domain history;
7. local AuditIntent/Outbox event.

Both transactions use one local PostgreSQL transaction; aggregate version updates, history insert and outbox insert either commit together or roll back together. The test checks the actual acquisition sequence, lock and statement timeouts, final versions and durable history/outbox row counts.

This is still a synthetic local candidate. The fixture uses separate receipt and approval rows per writer, and it does not include the actual IdentityAccess transaction guard in the same lock sequence. IdentityAccess' actual transaction-scoped guard is tested separately against the real disposable `identity.*` schema. The test does not combine both into one integrated Organization command because no production Organization module/UnitOfWork exists and Platform has not accepted the global rank ordering.

The exact total order remains NON-NORMATIVE. Platform must still reconcile IdentityAccess locks, idempotency, approval, Branch, Warehouse, Assignment, Finance/Accounting/Inventory participation, history and outbox. The candidate does not demonstrate that all affected modules already obey the same order. The network worker remains outside the local transaction and must never retain domain locks during remote delivery.

**Exact-head status:** the new method and current test checksum have been committed; CI for the resulting latest head is pending. No PASS is claimed until the PostgreSQL job logs show this exact method passed.

**Disposition:** candidate local transaction ordering now has a three-table and full local-envelope disposable proof source; exact-head CI pending; Platform-wide order and module-owner acceptance OPEN. O20-E OPEN; G1 NOT GRANTED.

**END — Platform Lock-Order Registry proposal v0.4**


## 11. Integrated IdentityAccess-guard plus synthetic Organization local transaction proof — 2026-11-22 (v0.5)

The exact transaction-guard integration test is expanded to combine:
- the actual IdentityAccess Infrastructure adapter against real disposable `identity.*` tables;
- two separate role memberships and two grants that both establish the same permission;
- the same open PostgreSQL connection and transaction for the authorization guard;
- synthetic Organization Branch, Warehouse, assignment, domain-history and local-outbox temp tables;
- the synthetic Organization row-version changes, immutable history append and pending-outbox append in that very same transaction;
- seven competing identity mutations, including revoke/removal of each distinct role membership and each distinct permission grant.

The adapter source review exposed a multi-role locking issue: reading only the first row of a query with `FOR SHARE` does not prove all rows contributing to a union-of-role Allow are locked. The adapter now consumes the full matching result set before returning Allow and validates the identity/permission values on every returned row. The expanded proof requires every competing mutation to remain blocked until the local transaction commits. After commit it verifies that all Organization test facts are durable and that a fresh permission check is denied after the queued revocations complete.

This is a stronger disposable PostgreSQL participant/linearization proof, not a Platform-approved global lock-order registry. It still does not combine every actual Finance/Accounting/Inventory participant or create a production Organization UnitOfWork. The global rank sequence remains NON-NORMATIVE until Platform and each affected owner accepts it and exact cross-module races are proven.

**Exact-head status:** new adapter and integrated guard proof are awaiting exact-head CI, checksums and independent review.

**Disposition:** transaction-guard coverage now includes a multi-role union and same-transaction synthetic Organization write/history/outbox; Platform-wide order and owner acceptance OPEN. O20-E OPEN; G1 NOT GRANTED.

**END — Platform Lock-Order Registry proposal v0.5**


## 12. Integrated proof outcome and current candidate order boundary — 2026-11-22 (v0.6)

The exact Linux PostgreSQL job `45062599789` passed the updated IdentityAccess transaction guard proof on source/test SHA `f5a38a2004fb5958c4d6bc17210b0965c60931cf`. The test combines in one disposable local transaction:
1. a synthetic idempotency receipt lock/update;
2. the actual IdentityAccess infrastructure adapter, which now drains and locks every row in the two-role permission union;
3. a synthetic approval request lock/update;
4. temporary Branch, Warehouse and Assignment row-version changes;
5. a domain-history append;
6. a local pending-outbox append.

Seven competing writers attempting session/account changes, both role memberships, both permission grants and permission deactivation remain blocked until the protected transaction completes. After commit, the test verifies receipt/approval/aggregate version changes, history/outbox facts and fresh denial after the competing revocations complete.

The evidence is strong for the tested fixture and transaction participant order. It does **not** make the candidate rank sequence normative. The fixture uses temporary Organization-shaped tables; it does not call an Organization production UnitOfWork, does not exercise actual Finance/Accounting/Inventory data owners in the same transaction, and does not establish every cross-module race/deadlock/retry scenario. Platform must publish a total order and all affected data owners must accept their participant contract before production use.

### 12.1 Windows CI runner timeout hardening

The Windows PostgreSQL install script now bounds the installer download to 240 seconds and the unattended EDB process to 900 seconds, killing the process tree on expiry. The Windows job's overall timeout remains 25 minutes. The gate validator and its policy unit test enforce the presence of these bounds so the integration step cannot remain indefinitely stuck due to a stalled installer/network operation.

The first run on `f5a38a...` had all Linux PostgreSQL checks pass, but Windows installation was still in progress when a later push superseded that run. The new timeout controls require fresh Windows CI; no Windows migration PASS is claimed yet.

**Disposition:** tested local receipt→IdentityAccess→approval→aggregate→history→outbox candidate path PASS at `f5a38a...`; global Platform lock order remains PROPOSED / NOT ACCEPTED. Windows timeout hardening added; current-head CI pending. O20-E OPEN; G1 NOT GRANTED.

**END — Platform Lock-Order Registry proposal v0.6**
