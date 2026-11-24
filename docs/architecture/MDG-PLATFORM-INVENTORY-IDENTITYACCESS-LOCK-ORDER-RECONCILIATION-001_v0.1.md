# Platform–Inventory–IdentityAccess Lock-Order Reconciliation

| Field | Value |
|---|---|
| Document ID | MDG-PLATFORM-INVENTORY-IDENTITYACCESS-LOCK-ORDER-RECONCILIATION-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | PRE-G1 RECONCILIATION PROPOSAL — NON-NORMATIVE — OWNER ACCEPTANCE / EXECUTION EVIDENCE OPEN |
| Branch | security/o20-o21-final-closure |
| Related | Platform Lock-Order Registry v0.7; Inventory Cross-Module Contract v1.2 §4; IdentityAccess/Organization Contract v2.1; Authorization Proposal v0.1 |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |

## 1. Purpose and authority

This document reconciles existing candidate lock resources and partial-order constraints in Platform, Inventory, IdentityAccess, and the future Organization command path. It identifies what is specified, what can be aligned, and what still has no accepted relative rank.

It does NOT establish a normative global lock order, accept a module lock contract, authorize an Organization or Inventory production implementation, create a UnitOfWork, schema, migration, permission grant, or production consumer. It is a pre-G1 design reconciliation only.

The Product Owner policy decisions already recorded remain controlling; no repeated Product Owner approval is requested. Platform, Inventory, IdentityAccess, Organization, and affected Accounting/Finance and AuditSecurity owners remain accountable for their technical boundaries.

## 2. Evidence baseline and limits

| Evidence | Established | Not established |
|---|---|---|
| IdentityAccess guard | Public Contracts API v1.2; actual Infrastructure adapter re-evaluates principal and exact permission in the caller transaction; FOR SHARE locks the account/session/role-membership/role/grant/permission rows contributing to Allow; complete result set is drained. A disposable PostgreSQL proof observes seven competing identity mutations blocked until transaction end. | No accepted cross-module rank for those locks; no global order; no production Organization consumer; no accepted transaction deadline/lease margin. |
| Platform registry v0.7 | Candidate order and local PostgreSQL fixture experiments, including synthetic Organization-shaped facts in a transaction using the actual IdentityAccess guard. | Candidate remains NON-NORMATIVE; actual Inventory, Finance and Accounting production units of work are not enlisted; not every cross-module race is proven. |
| Inventory contract v1.2 §4 | Inventory-local total order, WarehouseId counter semantics, atomic movement/projection/outbox behavior and Worker-lock separation. | Contract records NOT IMPLEMENTED / NOT VERIFIED and ALL TESTS NOT RUN. No Inventory production source/runtime module is present in the repository tree. |
| Inventory test spec v1.1 | Required PostgreSQL scenarios and evidence fields, including INV-04 for total order and multi-Warehouse/cutover cases. | All tests remain NOT_RUN. |
| Organization designs | Exact IdentityAccess permission remains conjunctive with the Product Owner direct path or non-owner approval; resource validity, history, audit intent and one local transaction remain required. | No production Organization module, UnitOfWork, schema, consumer or accepted Platform order. |

Passing disposable Platform/IdentityAccess fixtures are evidence only for resources actually tested. They must not be relabelled as Inventory integration or production Organization transaction proof.

## 3. Candidate orders already recorded

### 3.1 Platform / Organization candidate (registry v0.7)

The following remains an explicitly non-normative candidate:

| Candidate rank | Resource |
|---:|---|
| 00 | Command idempotency receipt/result |
| 10 | IdentityAccess account/session/role/membership/grant/permission facts, through the public transaction guard |
| 20 | Maker-checker approval request |
| 30 | Organization Branch aggregate |
| 40 | Organization Warehouse aggregate |
| 50 | Organization account-assignment aggregate |
| 60 | Append-only domain history |
| 70 | Local AuditIntent/Outbox write |

These ranks are not an accepted global order. No consumer may query IdentityAccess private tables.

### 3.2 Inventory-local order (contract v1.2 §4.1)

| Local rank | Resource | Deterministic acquisition |
|---:|---|---|
| 0 | Idempotency receipt/result | ActorId, operation, contract version, key |
| 1 | Source business aggregate, if in the same transaction | Owner module, aggregate type, AggregateId |
| 2 | Reservation aggregate | ReservationId ascending |
| 3 | Parent shortage scope | WarehouseId, then VariantId ascending |
| 4 | StockPosition | Complete canonical key ascending |
| 5 | StockUnit / UnitIdentifier / Lot | Fixed class order, then stable identifiers |
| 6 | ValuationPool | BusinessEntityId, VariantId, PoolId ascending |
| 7 | WarehouseCommitSequence counter | WarehouseId ascending |
| 8 | Projection generation/checkpoint/active pointer | ProjectionName, scope, generation/key |
| 9 | Outbox delivery metadata | Separate short Worker transaction by OutboxId; never held while acquiring domain locks |

Inventory requires applicable Warehouse×Variant, stock-grain and ValuationPool locks before Warehouse counter rows; counter rows are acquired in ascending WarehouseId order and held through commit/rollback. Movement, sequence, projection, AuditIntent, Outbox and idempotency result persist atomically. The counter is transactional; PostgreSQL sequence, timestamp or UUID order is not a commit watermark.

### 3.3 IdentityAccess guard semantics

The current transaction guard acquires FOR SHARE locks across every row contributing to the exact permission Allow. It drains the complete result so a multi-role permission union does not leave later membership/grant paths unlocked. Locks persist until the caller-owned transaction commits or rolls back.

The SQL checks the session lease at guard-evaluation time. A successful guard is not a global rank decision or proof that a long-running transaction remains within a lease-safe duration. Authorization proposal v0.1 recommends a linearization point and requires a measured, server-enforced transaction bound and lease margin; these remain unaccepted.

## 4. Reconciliation findings

### 4.1 Idempotency

Both candidate designs place the idempotency receipt/result at the entry of a mutating command. This can be aligned as a shared precondition when the receipt identity is server-derived ActorId plus operation contract and canonical request fingerprint, and the receipt participates in the same local transaction as protected effects.

The exact uniqueness/locking semantics and whether every cross-module command uses the same receipt service remain Platform and participating-module decisions.

### 4.2 IdentityAccess versus Inventory source aggregate

The Platform candidate places IdentityAccess authorization after idempotency and before approval and Organization aggregates. Inventory places a generic source business aggregate immediately after idempotency. Inventory §4.1 does not say whether that aggregate is acquired before or after the IdentityAccess guard.

This is an unresolved global ordering edge, not an accepted resolution. A command path acquiring an Inventory/source aggregate before IdentityAccess while another acquires IdentityAccess before that aggregate could violate the eventual total order. No ordering is selected here.

Before acceptance, owners must inventory every transaction path combining an IdentityAccess guard with a source aggregate, Reservation, stock-scope, StockPosition, StockUnit/identifier/Lot, ValuationPool, Warehouse counter, Projection, approval or Organization resource. The same global order must apply regardless of which module starts the command.

### 4.3 Maker-checker approval

The Platform candidate puts the approval-request resource before Organization aggregates. Inventory's local order assigns no rank to maker-checker approval. That absence is not permission to acquire approval after Inventory locks have already been taken.

Owners must decide whether approval is locked in the same transaction, is a previously committed immutable decision checked by fingerprint/version, or follows another explicit model. Concurrent approval edits/revocations must not turn stale approval into execution.

### 4.4 Organization Warehouse versus Inventory counter

The Organization Warehouse aggregate and Inventory WarehouseCommitSequence counter are different resources owned by different bounded contexts, despite sharing WarehouseId. They must not be collapsed into one lock row or assigned one rank by name alone.

Inventory acquires its counter only after its preceding stock/valuation locks and takes multiple counters in ascending WarehouseId order. No current production Organization/Inventory participant contract establishes when or whether an Organization Warehouse aggregate lock must be held in that same transaction. Owners must establish whether both resources are needed, what facts must remain stable, and their relative ordering if combined.

### 4.5 History, Outbox and Worker metadata

The documents describe different operations:

- Local AuditIntent/Outbox append is part of the protected business transaction and commits atomically with business state and idempotency result.
- Inventory rank-9 Outbox delivery metadata is accessed in a separate short Worker transaction. The Worker must not hold a claim/lease lock while acquiring business/domain locks or calling remote AuditSecurity.
- Canonical AuditSecurity Event persistence and durable ACK are remote/module-owned operations; network delivery follows local commit and occurs outside domain locks.

The normative registry must define local append and Worker claim/reconciliation separately. Atomic outbox insertion is not the same operation as retaining a delivery lock during a domain transaction.

### 4.6 Accounting / Finance

Inventory owns quantity/valuation source evidence. Accounting/Finance owns Journal, JournalLine, GL, fiscal periods, COA eligibility and Posting Rules; Inventory cannot select accounts or Posting Rules. Finance acceptance of the valuation/True-up contract remains pending.

Current evidence does not show that ordinary Inventory movement must lock Finance Journal/Posting resources in the same transaction. Do not assume cross-module enlistment. Owners must confirm the event/Outbox hand-off and transaction boundary. If a future command needs both contexts in one transaction, their relative order must be specified and proven before implementation.

## 5. Proposed reconciliation rules for owner review

These are proposals, not normative until accepted by accountable owners:

1. Exactly one global total order must cover every resource that can be locked together in one PostgreSQL transaction. Module-local sequences are subordinate; their cross-module placement must be explicit.
2. Mutating commands obtain the canonical idempotency receipt under server-derived identity and request fingerprint, where required by command contract.
3. IdentityAccess authorization uses the public transaction-scoped guard on the same connection/transaction as the protected mutation. Direct IdentityAccess table access remains prohibited.
4. The complete resource set is planned before the first aggregate lock. Same-rank identifiers are sorted deterministically; no command later acquires a resource at an earlier accepted rank.
5. Inventory preserves its internal sequence: Reservation → parent Warehouse×Variant shortage scope → StockPosition → StockUnit/UnitIdentifier/Lot → ValuationPool → WarehouseCommitSequence → Projection. Unused ranks may be omitted; used Inventory resources are not reordered.
6. Multiple Warehouse counters are acquired in ascending WarehouseId order and held through local commit/rollback. Counter, movement, active Projection, AuditIntent, Outbox and idempotency result commit atomically.
7. Organization Warehouse aggregate and Inventory WarehouseCommitSequence remain distinct until both owners accept any joint ordering.
8. Approval, source business, Inventory and Organization aggregate locks follow the accepted total registry, not call-stack order or initiating module.
9. Local history and AuditIntent/Outbox insertion commit atomically. Worker delivery metadata is claimed/reconciled in a separate short transaction; remote AuditSecurity calls occur outside domain locks.
10. Transaction duration, PostgreSQL lock timeout, deadlock classification, cancellation, bounded retry, rollback, crash-before-commit, unknown-commit/replay and durable audit-intent behavior belong to the same accepted contract.
11. The IdentityAccess session lease must cover the accepted maximum duration plus commit/finalization reserve and safety margin, or another accepted protocol proves validity through its linearization point. Numeric values must come from measured target PostgreSQL 18 evidence.
12. Proposal, CI success, owner policy approval or synthetic fixture does not replace module-owner acceptance and exact-SHA PostgreSQL evidence.

## 6. Required evidence

| ID | Experiment | Minimum proof |
|---|---|---|
| LOR-X01 | Idempotency + IdentityAccess guard + source aggregate | Two independent sessions; competing acquisition paths; deterministic barriers; no cycle; exact guard Allow/Deny; one durable idempotent result. |
| LOR-X02 | IdentityAccess versus Inventory Reservation/parent scope | Reversed entry-path attempts; pg_blocking_pids() and SQLSTATE capture; bounded outcome; no stale permission after a winning revocation. |
| LOR-X03 | IdentityAccess versus StockPosition / StockUnit / Identifier / Lot / ValuationPool | Full resource acquisition map; multi-role permission union; revocation and stock mutations; no lower-rank acquisition after a higher rank. |
| LOR-X04 | Organization Warehouse versus Inventory counter | Distinct resource identities; races with Warehouse deactivate/reparent and Inventory issue/transfer; no cycle and valid resource facts. |
| LOR-X05 | Multi-Warehouse counters | Two or more counter rows acquired ascending WarehouseId across reversed inputs; atomic commit/rollback; no committed sequence gap or partial transfer. |
| LOR-X06 | Approval versus Inventory/Organization state | Actor/approver/subject separation; exact fingerprint/version binding; concurrent edit/revoke cannot execute stale approval. |
| LOR-X07 | Projection cutover versus movement writer | Writer blocked on counter observes new active pointer after cutover, or abort preserves old pointer; no lost movement and exact watermark/tie-out. |
| LOR-X08 | Local Outbox and Worker | Business facts plus AuditIntent/Outbox/idempotency commit or roll back together; Worker claim is short and does not overlap domain locks; remote ACK durable and replay-safe. |
| LOR-X09 | Finance/Accounting boundary | Confirm valuation/True-up delivery is asynchronous and outside Inventory transaction; no inferred GL account/Journal/Posting Rule; version/hash/lineage acknowledged. |
| LOR-X10 | Deadline, lease and failure handling | Database-enforced max transaction duration; lease boundary; heartbeat contention; timeout/deadlock/cancel; crash, unknown commit and replay; no duplicate effect. |

Each experiment records repository/build/schema SHA, PostgreSQL 18 configuration, fixture and role identity, independent sessions, barriers, SQLSTATE, blocking PIDs, before/after snapshots, durable idempotency/history/outbox facts, timing, PASS/FAIL/BLOCKED, reviewer and evidence SHA256. Sleep-only assertions are insufficient.

## 7. Acceptance and implementation boundary

| Owner | Required technical acceptance |
|---|---|
| IdentityAccess | Public v1.2 guard locking, exact permission decision, lease linearization, deadline and fail-closed outcomes |
| Inventory | Local ranks 0–9, Warehouse counter semantics, multi-Warehouse order, projection/cutover and Worker boundary |
| Organization | Branch/Warehouse/assignment ownership, joint Warehouse resource use, approval transaction behavior and RowScope |
| Platform | One normative total order, participant contract, timeout/deadlock/cancel/retry/recovery and lease/deadline semantics |
| Accounting / Finance | Any shared transaction with Inventory; valuation/True-up event boundary and Journal/Posting ownership |
| AuditSecurity | Canonical event/ACK and Worker claim/delivery/replay boundary |
| Independent Security/Architecture | Exact-SHA review of final accepted order and actual PostgreSQL evidence |

The repository currently has no Inventory production source/runtime module, no production Organization consumer and no integrated Inventory+IdentityAccess+Organization unit of work. Inventory's technical contract and test specification mark implementation/verification/tests NOT RUN. Existing disposable guard/lock experiments remain fixture evidence only.

**Disposition:** Candidate sequences are reconciled and unresolved ordering edges are explicit. No global rank, module sign-off, production code, schema, migration, permission grant or release is authorized. O20-E remains OPEN; O-20 and O-21 remain OPEN; Organization G1 is NOT GRANTED.

**END — MDG-PLATFORM-INVENTORY-IDENTITYACCESS-LOCK-ORDER-RECONCILIATION-001 v0.1**
