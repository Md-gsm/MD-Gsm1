# Lock-Order Experiment Evidence Map

| Field | Value |
|---|---|
| Document ID | MDG-LOCK-ORDER-EXPERIMENT-EVIDENCE-MAP-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | STATIC SOURCE REVIEW — NO INVENTORY INTEGRATION TEST EXECUTED — PRE-G1 |
| Reviewed source SHA | 72376d6a4e1f982d404b1803e3a6ad280847444b |
| Related | Platform Lock-Order Registry v0.7; Inventory Cross-Module Contract v1.2; Inventory Cross-Module Test Spec v1.1; Lock-Order Reconciliation v0.1 |
| Target | PostgreSQL 18 |
| Authority | Evidence classification only; no implementation/release authorization |

## 1. Method and limits

This is a static mapping of source and test files at the exact reviewed SHA. No test was run while producing this document. No Inventory test status is changed. Synthetic Organization fixtures are not represented as Inventory implementation evidence.

Inventory Cross-Module Contract v1.2 states execution NOT IMPLEMENTED / NOT VERIFIED and ALL TESTS NOT RUN. Inventory Cross-Module Test Spec v1.1 states all cases NOT_RUN and requires independent PostgreSQL sessions for concurrency cases. Those statuses remain authoritative. The reviewed tree contains no Inventory production source/runtime module and no Inventory-owned PostgreSQL integration test file.

## 2. LOR-X01 through X10

| Experiment | Existing test evidence at reviewed SHA | Classification | Missing proof / disposition |
|---|---|---|---|
| X01 Idempotency + IdentityAccess guard + source aggregate | `IdentityAccessTransactionScopedAuthorizationGuardPostgresProofTests.Actual_identityaccess_guard_holds_account_session_membership_role_grant_and_permission_locks_until_commit`; `O20OrganizationIdempotencyConcurrencyPostgresProofTests.Concurrent_same_key_replay_and_distinct_key_same_assignment_resolve_to_one_fact`; `O20OrganizationDeterministicLockOrderPostgresProofTests.Candidate_transaction_order_persists_receipt_approval_aggregates_history_and_outbox_atomically` | PARTIAL; separate and synthetic fixtures | No actual Inventory source aggregate/command enlisted with actual IdentityAccess guard. Inventory acceptance remains NOT_RUN. |
| X02 IdentityAccess vs Reservation / parent scope | `IdentityAccessAuthorizationLinearizationPostgresProofTests.Revocation_and_permission_removal_serialize_behind_protected_write_lock_set`; transaction-scoped guard proof | PARTIAL; IdentityAccess-only races | No Reservation or Warehouse×Variant scope participant and no reversed cross-resource acquisition race. |
| X03 IdentityAccess vs StockPosition / StockUnit / Identifier / Lot / ValuationPool | Transaction-scoped guard proves identity-row locking and multi-role union only. | NOT COVERED for Inventory resources | No stock/valuation resources in the proof; requires actual Inventory participant and two-session races. |
| X04 Organization Warehouse vs Inventory WarehouseCommitSequence | Organization deterministic-lock tests use disposable Branch/Warehouse/Assignment tables. | NOT COVERED for cross-context pair | No Inventory counter participant or joint locking test; distinct-resource ordering unproven. |
| X05 Multi-Warehouse counters | Inventory Test Spec INV-15 defines ascending WarehouseId locks and atomicity. | NOT COVERED | No counter implementation or test. INV-15 remains NOT_RUN; FE-31 remains R2/disabled. |
| X06 Approval vs Inventory/Organization state | `O20OrganizationAssignmentApprovalPostgresProofTests.Non_owner_requires_independent_approval_but_bound_product_owner_executes_directly`; candidate Organization transaction-envelope proof | PARTIAL; Organization fixture only | No Inventory stock/valuation enlistment or stale fingerprint/version race against Inventory mutation. |
| X07 Projection cutover vs movement writer | Inventory contract §4.4 and Test Spec CUT-01…CUT-12 define required behavior. | NOT COVERED | No active Projection pointer/counter/cutover/movement-writer integration proof. CUT cases remain NOT_RUN; numeric budgets NOT_SET. |
| X08 Local Outbox and Worker | `O20OrganizationAuditOutboxPostgresProofTests.Local_outbox_is_atomic_remote_ack_replay_is_idempotent_and_payload_conflict_quarantines`; `Competing_outbox_workers_do_not_claim_the_same_pending_row`; `O20OutboxWorkerLeastPrivilegePostgresProofTests.Worker_can_read_immutable_event_and_update_only_delivery_metadata` | PARTIAL; disposable fixtures | No Inventory movement/sequence/projection/receipt atomicity with Outbox and no real canonical AuditSecurity ACK in an Inventory command path. |
| X09 Finance/Accounting boundary | Inventory contract §6 defines ownership split; separate Accounting PostgreSQL tests exist. | NOT COVERED cross-module | No Inventory valuation/event implementation, Finance acceptance, or Inventory→Finance event-boundary proof. Do not infer shared transaction or GL locks. |
| X10 Deadline, lease and failure handling | IdentityAccess revocation linearization proof; Organization idempotency proof covers selected rollback/unknown-commit replay; candidate fixtures use lock/statement timeouts. | PARTIAL | No server-enforced guarded-transaction maximum, measured commit reserve, lease-boundary/heartbeat test, or integrated Inventory deadline proof. |

## 3. Exact proof boundaries

### 3.1 IdentityAccess

The existing guard test uses the actual Infrastructure adapter and a disposable PostgreSQL identity schema. It drains the complete permission result, retains FOR SHARE locks on contributing identity rows through the caller transaction, and observes seven competing identity mutations blocked. This proves the tested IdentityAccess behavior only; it does not establish a global rank or Inventory participation.

### 3.2 Organization

The deterministic-order proof exercises reversed caller inputs and a candidate Branch → Warehouse → Assignment order with disposable tables. The candidate envelope adds synthetic receipt, approval, aggregate updates, history and local Outbox. These fixtures do not prove Inventory ranks 0–9, WarehouseCommitSequence, stock/valuation locks, Projection cutover, or production Organization UoW.

### 3.3 Idempotency / Audit / Worker

The disposable Organization idempotency test covers same-key replay, changed-fingerprint rejection, unknown-commit replay and rollback/retry. The Outbox tests cover local fact+intent atomicity, simulated remote-ACK replay, competing claims and a disposable least-privilege Worker role. They do not prove atomicity with Inventory movement, counter, valuation or Projection, nor actual canonical AuditSecurity durable ACK.

## 4. Inventory test-spec crosswalk

| Inventory test IDs | Relation | Status |
|---|---|---|
| INV-01…INV-04 | Idempotency and total order; closest to X01–X03 | NOT_RUN |
| INV-11…INV-17 | Warehouse counter, rollback, multi-Warehouse, deadlock; closest to X04/X05/X10 | NOT_RUN |
| INV-18 | Stocktake cutoff/replay | NOT_RUN |
| AUD-01…AUD-14 | AuditIntent/Outbox, remote ACK, Worker and restore; closest to X08 | NOT_RUN |
| VAL-01…VAL-13 | Valuation and Finance boundary; closest to X03/X09 | NOT_RUN |
| CUT-01…CUT-12 | Projection/cutover and writer race; closest to X07/X10 | NOT_RUN |
| RSV-01…RSV-12 | Reservation TTL/lock-wait boundary; closest to X02 | NOT_RUN |
| SCOPE-01…SCOPE-07 | Scoped authority; not a lock-order substitute | NOT_RUN |

The exact test IDs/assertions remain governed by Inventory Test Spec v1.1. Static inspection does not change any NOT_RUN status.

## 5. Prioritized next work and prerequisites

| Order | Work | Current disposition |
|---:|---|---|
| 1 | Preserve bounded IdentityAccess/Organization fixture results with exact test names, SHA and logs. | Documentation/evidence reconciliation can proceed now. |
| 2 | Prepare an Inventory ranks 0–9 harness design: canonical resource identity, sorted acquisition plan, deterministic two-session barriers, lock graph and SQLSTATE capture. | Test design only; no production schema/runtime. |
| 3 | Execute X01–X03 with actual Inventory participants. | Blocked on separate G1/implementation authorization and Inventory source/schema/owner contract. |
| 4 | Execute X04–X05 for distinct Warehouse aggregate/counter and multi-Warehouse atomicity. | Blocked on accepted Organization/Inventory participant contracts and counter implementation authorization. |
| 5 | Complete X06 stale approval fingerprint/version race. | Existing Organization-only proof is partial; requires accepted approval rank and business-state race. |
| 6 | Execute X07 and CUT-01…CUT-12. | Blocked on Projection/counter implementation and measured, approved D-15 budgets. |
| 7 | Complete X08 with actual Inventory transaction and canonical AuditSecurity durable ACK. | Existing Outbox/Worker fixture is partial. |
| 8 | Complete X09 Finance/Accounting event boundary. | Blocked on Finance/Accounting owner acceptance and versioned event contract. |
| 9 | Complete X10 lease/deadline/heartbeat integration. | Blocked on IdentityAccess/Platform acceptance, measured PostgreSQL timing and integrated rollback/replay proof. |

Pre-G1 permitted work: source/test inventory, static mapping, disposable-only harness design, evidence reconciliation and non-mutating review. This document does not authorize production Inventory/Organization schema, migrations, permissions/grants/seeds/binding, UnitOfWork, runtime consumers, Inventory activation, negative stock, cutover, Posting or Go-Live.

## 6. Evidence record for future runs

Each executed experiment must record exact repo/build/schema SHA; PostgreSQL 18 version/settings; disposable fixture and role; independent connection count and deterministic barriers; planned/observed lock order; SQLSTATE, timeout, pg_blocking_pids and deadlock data; before/after business/counter/projection/receipt/history/Outbox snapshots; rollback and unknown-commit replay; PASS/FAIL/BLOCKED; reviewer, UTC timestamp and evidence SHA256. Sleep-only assertions are insufficient.

## 7. Disposition

Static review establishes bounded fixture coverage for IdentityAccess guard/revocation, Organization-local candidate ordering, assignment approval, idempotency and disposable Outbox/Worker behavior.

X03, X04, X05, X07 and X09 have no matching cross-module proof. X01, X02, X06, X08 and X10 are partial only. All Inventory Test Spec v1.1 statuses remain NOT_RUN. Executable Inventory integration evidence cannot honestly be generated until the required implementation/authorization and owner contracts exist.

O20-E remains OPEN; O-20 and O-21 remain OPEN; Organization G1 is NOT GRANTED; Inventory activation remains unauthorized.

**END — MDG-LOCK-ORDER-EXPERIMENT-EVIDENCE-MAP-001 v0.1**
