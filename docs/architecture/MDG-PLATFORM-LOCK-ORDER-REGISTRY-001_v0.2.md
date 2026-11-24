# Platform Global Lock-Order Registry — O20-E Candidate

| Field | Value |
|---|---|
| Document ID | MDG-PLATFORM-LOCK-ORDER-REGISTRY-001 |
| Version / Date | 0.2 / 2026-11-22 |
| Supersedes | v0.1 |
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
