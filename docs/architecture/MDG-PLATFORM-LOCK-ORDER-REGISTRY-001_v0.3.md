# Platform Global Lock-Order Registry — O20-E Candidate

| Field | Value |
|---|---|
| Document ID | MDG-PLATFORM-LOCK-ORDER-REGISTRY-001 |
| Version / Date | 0.3 / 2026-11-22 |
| Supersedes | v0.2 |
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
