# Inventory PostgreSQL Lock-Order Harness Design

| Field | Value |
|---|---|
| Document ID | MDG-INVENTORY-POSTGRES-LOCK-ORDER-HARNESS-DESIGN-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | PRE-G1 TEST-HARNESS DESIGN — NOT IMPLEMENTED — NO TESTS RUN |
| Reviewed branch head | `c3e8e154c900faf71c0072b4a6239571b969dfdf` |
| Target | PostgreSQL 18; disposable isolated test database only |
| Related | Inventory Cross-Module Contract v1.2; Inventory Test Spec v1.1; Platform Lock-Order Registry v0.7; Platform–Inventory–IdentityAccess Lock-Order Reconciliation v0.1; Lock-Order Experiment Evidence Map v0.1 |
| Authority | Test-design artifact only; no runtime/schema/DDL/migration/permission/release authorization |

## 1. Purpose and boundaries

This design specifies how future PostgreSQL evidence for Inventory lock ordering, concurrency, rollback and replay is to be collected. It converts the Inventory local resource taxonomy and LOR-X01…X10 gaps into a repeatable, observable test harness.

This is a **design only**. No harness code, production Inventory module, schema, migration, permission, grant, seed, UnitOfWork, runtime consumer or database role is created by this document. No PostgreSQL test is executed and no Inventory Test Spec status changes from NOT_RUN.

The harness must use disposable test-owned schemas and fixture data. It must never connect to a production database or apply migrations to a non-disposable database. It must not inspect or reuse any RepairDesk source, schema, migration, script, binary or implementation artifact.

The harness proves only resources and transaction paths that actually participate in a given test. Synthetic fixtures must be labelled as such and cannot be reported as production-module integration proof.

## 2. Governing contracts and unresolved-order protection

### 2.1 Inventory-local sequence under test

The current Inventory contract v1.2 defines this local acquisition sequence. It is a local contract candidate and does not itself establish the relative rank of IdentityAccess, Organization or other module resources:

| Local rank | Resource | Canonical ordering key |
|---:|---|---|
| 0 | Idempotency receipt/result | Server-derived ActorId, operation code/version, canonical key digest |
| 1 | Source business aggregate, when enlisted | Owner module, aggregate type, AggregateId |
| 2 | Reservation aggregate | ReservationId |
| 3 | Parent shortage scope | WarehouseId, then VariantId |
| 4 | StockPosition | Complete canonical stock-grain key |
| 5 | StockUnit / UnitIdentifier / Lot | Fixed class order, then stable resource identifier |
| 6 | ValuationPool | BusinessEntityId, VariantId, PoolId |
| 7 | WarehouseCommitSequence counter | WarehouseId ascending |
| 8 | Projection generation/checkpoint/active pointer | ProjectionName, scope, generation/key |
| 9 | Outbox delivery metadata | Separate short Worker transaction by OutboxId |

For an Inventory movement, the applicable business resources, Warehouse counter, movement, Projection update, local AuditIntent/Outbox append and idempotency result must obey the accepted Inventory transaction contract. The Warehouse counter is transactional and held through local commit/rollback. Multiple Warehouse counters are acquired by ascending WarehouseId. Outbox **append** is in the local business transaction; Outbox **delivery/claim metadata** is a separate short Worker transaction and must not overlap domain locks or remote AuditSecurity calls.

The test harness must not silently reorder these Inventory-local ranks. It may omit unused ranks for a command, but may not acquire an earlier applicable rank after a later one.

### 2.2 Global cross-module order is deliberately not assumed

The Platform registry v0.7 remains non-normative. Reconciliation v0.1 explicitly leaves unresolved:
- IdentityAccess guard versus Inventory source aggregate / Reservation / stock resources;
- Organization Warehouse aggregate versus Inventory WarehouseCommitSequence counter;
- maker-checker approval versus Inventory resources;
- Inventory and Finance/Accounting transaction boundaries;
- local Outbox append versus Worker delivery/claim;
- IdentityAccess session lease, transaction deadline and commit reserve.

Therefore this harness must not encode a guessed global rank or declare a cross-module test PASS merely because one candidate ordering completes. Cross-module experiments are gated on explicit acceptance of the participating resource contracts and one normative total order by Platform and affected data owners. Until then, cross-module cases are design-ready/BLOCKED, not executable acceptance.

The actual IdentityAccess public transaction guard may be used only through its published contract and real Infrastructure adapter when a separately authorized test includes it. Tests must not query IdentityAccess private tables or manufacture client-supplied ActorId/role/permission/owner flags.

## 3. Harness topology

### 3.1 Isolation model

Each test run provisions a uniquely named disposable database or isolated schema set, using test-only credentials with no production connection string. Provisioning and teardown are owned by the test fixture. The harness must fail before test execution if the connection target is not explicitly marked disposable.

Each concurrency scenario uses at least two independent PostgreSQL connections and independent transactions. A third observer connection captures lock state without participating in the transactions under test. Tests that require three competing writers use three or more independent connections.

The fixture records:
- PostgreSQL server_version_num, server_version, relevant configuration and extension availability;
- database/schema identity and disposable-fixture marker;
- repository SHA, test assembly/build SHA, fixture version and test case ID;
- connection/session identifiers (backend PID and application_name), transaction isolation and timeout settings;
- role identity and granted test-only capabilities;
- deterministic barrier events and timestamps in UTC.

No sleep-only synchronization is permitted. Use explicit application-side barriers (for example, TaskCompletionSource or equivalent) and database-observed conditions. A bounded timeout is a safety ceiling, not a synchronization mechanism.

### 3.2 Session naming and correlation

Every connection sets a unique application_name containing run ID, case ID and participant label (A, B, observer, Worker). The harness records pg_backend_pid() and txid_current() where available. Correlation IDs are generated by the harness and stored in its evidence envelope; they are not substituted for business idempotency keys.

All test SQL must be parameterized. Test fixture identifiers are generated per run; no shared static fixture state is permitted.

### 3.3 Lock observation

The observer samples PostgreSQL lock state while the competing transactions are deliberately held at known barriers. Capture at minimum:
- pg_stat_activity: pid, application_name, state, wait_event_type, wait_event, xact_start, query_start, state_change;
- pg_locks: locktype, relation, page, tuple, virtualxid, transactionid, mode, granted, pid;
- pg_blocking_pids(pid) for every participant;
- relevant relation/index identity and the test's canonical resource key mapping;
- server log deadlock details when available, including process/transaction wait graph.

The observer must correlate PostgreSQL relation/tuple/transaction locks back to the logical resource IDs using fixture metadata. A raw lock snapshot without a deterministic resource mapping is insufficient evidence.

Record snapshots at three points: before contention, while the intended wait is active, and after resolution/abort. Polling cadence is configurable and bounded; the test must assert that the expected blocker/waiter relationship was actually observed before releasing the barrier. Failure to observe it is INCONCLUSIVE/FAIL, not PASS.

## 4. Deterministic orchestration protocol

Each concurrency test follows this protocol unless its case explicitly requires a different sequence:

1. Create isolated fixture state and capture a before-snapshot.
2. Open independent connections A and B (and observer/Worker as required); verify distinct backend PIDs.
3. Start both transactions and stop each at explicit barrier points before the critical acquisition.
4. Release A to acquire its declared resource set; have the observer verify the granted lock and logical resource mapping.
5. Release B to attempt its competing acquisition in the deliberately selected input order.
6. Verify the expected wait graph through pg_blocking_pids and pg_locks. If the intended contention is not observed, do not infer serialization.
7. Release the next barrier or induce the specified cancellation/failure. Capture SQLSTATE and server diagnostics.
8. Await bounded completion of all participants; ensure no task/connection remains active.
9. Capture after-snapshot for business rows, receipt, movement, sequence, projection, history, AuditIntent and Outbox as applicable.
10. Verify invariant, atomicity, idempotent replay and absence of unintended side effects.
11. Teardown only after all sessions are closed and the fixture confirms no open transaction/lock remains.

Barriers must be named by semantic point (e.g. after-idempotency, before-reservation-lock, after-counter-lock, before-commit), not arbitrary elapsed sleeps. The test report includes a barrier timeline and the observed wait graph.

## 5. Resource identity and acquisition-plan instrumentation

The harness requires a test-only lock-plan recorder at the boundary where each logical resource is requested. Each record contains:
- run/case/transaction ID;
- resource owner/module, resource class and local rank (where accepted);
- canonical resource key and normalized sort key;
- requested/acquired/released timestamp;
- transaction connection/backend PID;
- command path and source entry point;
- whether the resource is a row lock, advisory lock, optimistic version check or non-locking read;
- whether the resource is held to commit or belongs to a separate short transaction.

The recorder is observational and test-only. It must not become a production authorization bypass or alter lock behavior. It should emit a structured sequence so the test can assert:
- no duplicate acquisition with inconsistent identity;
- no descending acquisition within an accepted local rank/order;
- same-rank keys are sorted deterministically;
- all required locks are acquired before protected state is re-read;
- counter rows are ascending by WarehouseId;
- Outbox delivery metadata is not retained in the business transaction;
- locks are released on commit/rollback/cancellation.

Where a logical lock is implemented through SQL row locks, the recorder must be cross-checked against pg_locks. An application trace alone is not proof that PostgreSQL granted the intended lock.

## 6. Required failure and retry instrumentation

Capture the original and final SQLSTATE, exception type, cancellation source, transaction outcome and retry decision. At minimum classify:
- 40P01 — deadlock_detected;
- 40001 — serialization_failure;
- 55P03 — lock_not_available / lock timeout where configured;
- 57014 — query_canceled / statement timeout or cancellation;
- connection loss / transport failure where commit outcome may be unknown.

The retry policy under test must be explicit, bounded and limited to classified transient outcomes. Policy denial, validation failure, stale approval, integrity conflict and changed idempotency fingerprint are not retried as transient database failures.

For each retry, record attempt number, delay, elapsed time, SQLSTATE, same idempotency key/fingerprint, and whether the prior attempt is known rolled back or has an indeterminate commit outcome. Unknown commit must be resolved by querying/replaying the same durable idempotency identity; never mint a new key to conceal uncertainty.

A test passes only if final durable state proves one intended business effect and a stable replay result. An exception by itself does not prove rollback.

## 7. Test suites and acceptance mapping

The following suite design maps to the existing LOR experiment matrix and Inventory Test Spec v1.1. It does not change the latter's IDs or status values.

| Suite | Required scenarios | Evidence focus | Related cases |
|---|---|---|---|
| H0 Fixture safety | Disposable-target refusal, unique schema/run isolation, teardown, no leaked sessions | Target identity, role, PIDs, cleanup | Harness prerequisite |
| H1 Local acquisition plan | Each applicable Inventory command, rank omission, same-rank sorting, reverse caller input | Logical acquisition trace cross-checked with pg_locks | INV-04; X01, X03 |
| H2 Idempotency and source | Same key/same fingerprint, same key/changed fingerprint, concurrent replay, unknown commit, source aggregate race | One durable receipt/result and one effect | INV-01…INV-03; X01 |
| H3 Reservation and shortage | Reservation issue/expiry boundary, parent Warehouse×Variant serialization, two competing reservations | Post-lock revalidation and no stale authorization/scope | RSV cases; INV-05…INV-10; X02 |
| H4 Stock and valuation | StockPosition/StockUnit/Identifier/Lot/ValuationPool competing mutations and reversed entry paths | No descending rank/cycle; quantity and valuation invariants | X03; VAL-01…VAL-13 |
| H5 Warehouse counter | Same-Warehouse multi-Variant movements, rollback, first-counter provisioning, multi-Warehouse reversed inputs | Contiguous committed prefix, no gaps/duplicates/partial transfer | INV-11…INV-18; X04, X05 |
| H6 Approval and Organization boundary | Stale approval fingerprint/version, Organization Warehouse state versus distinct Inventory counter | No stale approval; distinct resource identities | X04, X06 |
| H7 Projection/cutover | Writer blocked behind counter while Pointer Swap commits or aborts; tail-budget and rollback reserve boundaries | New pointer observed after lock; old pointer preserved on abort; exact tie-out | CUT-01…CUT-12; X07 |
| H8 AuditIntent/Outbox | Local atomic append/rollback, post-commit delivery, Worker claim and remote ACK replay | Atomic local facts; no domain-lock overlap; matching durable ACK | AUD-01…AUD-14; X08 |
| H9 Finance/Accounting boundary | Valuation/True-up event handoff, version/hash/lineage and no shared-transaction assumption | Inventory commit independent from downstream Journal/Posting; no inferred GL behavior | X09 |
| H10 Deadline/lease/failure | Transaction max duration, lock wait, cancellation, lease boundary, commit reserve, crash and replay | Bounded locks; lease-safe authorization; exact rollback/unknown-commit resolution | X10 |

### 7.1 Cross-module suite activation gate

H2/H3/H4/H6/H9/H10 scenarios that enlist another module remain BLOCKED until:
1. each affected data owner accepts the resource identity and transaction participation;
2. Platform publishes one normative total lock order covering all resources that can coexist in one transaction;
3. IdentityAccess accepts the exact transaction-guard and lease/deadline linearization contract where applicable;
4. the command's source/approval/Organization/Finance/AuditSecurity boundaries are explicit;
5. a separate implementation/G1 authorization permits the required actual module participants.

A synthetic fixture can validate orchestration mechanics but must be reported as fixture-only and cannot satisfy this gate.

## 8. Specific PostgreSQL assertions

### 8.1 Lock graph and deadlock

For every intentional wait, the observer must show the expected waiter PID and at least one blocker PID. For a no-cycle case, the final graph must drain without an unhandled deadlock. For a deliberately injected deadlock-class failure test, the server must report SQLSTATE 40P01 for the victim; the harness then verifies full rollback and bounded same-key retry.

Do not rely solely on client exception text. Retain server diagnostics and the relevant pg_locks/pg_stat_activity snapshots.

### 8.2 Warehouse counter

For one Warehouse, committed movement entries must form a contiguous committed prefix with no duplicate sequence. Rollback must leave no committed counter advancement or sequence gap. A multi-line/multi-Variant movement allocates one Warehouse sequence entry for the movement, with line-level references.

For multi-Warehouse operations, each distinct WarehouseCommitSequence row is a separate resource from any Organization Warehouse aggregate. Acquire counter rows ascending by WarehouseId and prove both sides commit or roll back atomically under the accepted Inventory contract. Do not combine these tests with Organization Warehouse locks until their relative global rank is accepted.

### 8.3 Projection and cutover

The writer must resolve the active Projection generation only after acquiring the Warehouse counter lock. The cutover test must force the writer to wait behind the counter, complete a successful pointer swap, then prove that the unblocked writer reads and updates the newly active generation. A separate abort case proves the prior pointer remains active and unchanged when either cutover budget or reconciliation condition fails.

Both MAX_CUTOVER_TAIL_COUNT and MAX_CUTOVER_LOCK_DURATION remain NOT_SET. The harness may test parameterized boundary behavior in a disposable fixture, but must not choose or recommend production numeric values. Target-hardware measurement and independent acceptance are prerequisites for setting values.

### 8.4 Outbox and Worker separation

Local AuditIntent/Outbox append must share the business transaction with movement, counter, Projection and idempotency result. Injected local append failure must roll back all local facts. Once committed, remote AuditSecurity delivery happens outside domain locks.

Worker claim/lease metadata is exercised in a separate short transaction. The test must show that the Worker releases the claim transaction before remote I/O and does not acquire Inventory domain locks while holding delivery metadata locks. A delivery is marked acknowledged only after a durable canonical AuditSecurity ACK matching EventId and PayloadHash. A simulated ACK fixture is not canonical AuditSecurity proof.

### 8.5 Finance boundary

Inventory valuation evidence is committed and delivered through the approved versioned event boundary. The test must not imply that Inventory owns Journal, JournalLine, COA, Posting Rule or GL. No cross-module shared transaction is assumed. If Finance later requires same-transaction resource participation, that is a new ordering edge requiring owner acceptance and a revised harness case.

## 9. Evidence envelope and verdict rules

Every executed case produces a machine-readable evidence envelope and human-readable summary with:
- repository commit SHA and test/build artifact SHA256;
- exact test name, case ID and suite version;
- PostgreSQL 18 server version/configuration and disposable database identity;
- fixture schema/version and role identity;
- independent session count, backend PIDs and transaction identifiers;
- barrier timeline, logical lock-plan trace and observed pg_locks/pg_blocking_pids snapshots;
- SQLSTATE, server deadlock details, timeout/cancellation and retry timeline;
- before/after snapshots of all relevant business, counter, projection, idempotency, history and Outbox facts;
- transaction outcome (committed / rolled back / indeterminate) and same-key replay result;
- PASS / FAIL / INCONCLUSIVE / BLOCKED with reason;
- UTC execution time, operator/CI identity, reviewer and evidence SHA256.

Verdict definitions:
- **PASS:** all required preconditions and barriers were observed; actual participants and independent sessions were used; lock graph, SQLSTATE and final durable invariants match the assertion.
- **FAIL:** a required invariant is violated, unexpected deadlock/partial commit occurs, or a bounded safety limit is exceeded.
- **INCONCLUSIVE:** the intended contention/barrier was not observed, evidence is incomplete, or cleanup cannot establish a final state.
- **BLOCKED:** required implementation, owner acceptance, normative order, G1 or authorized environment is absent.
- **NOT_RUN:** no execution has been performed.

No aggregate “suite passed” may conceal an individual FAIL/INCONCLUSIVE/BLOCKED case. CI success is structural evidence only unless the actual PostgreSQL integration job executes these exact tests and publishes its evidence envelope.

## 10. Implementation sequence and prerequisites

1. **Harness skeleton (test-only):** disposable-target guard, unique fixture namespace, independent connection factory, observer connection, deterministic barriers, structured evidence writer and cleanup assertions.
2. **Observer validation:** prove that the observer can correlate a known test row lock to its logical resource key and detect a blocked waiter.
3. **Inventory-local unit scenarios:** after authorized Inventory participants exist, implement and run local ranks 0–9, same-rank sorting, idempotency and counter tests.
4. **Cross-module scenarios:** only after owner acceptances and normative global lock order; no guessed rank or synthetic-to-production evidence promotion.
5. **Failure/replay scenarios:** inject lock timeout, deadlock, serialization failure, cancellation, process loss before commit and unknown commit; verify durable same-key recovery.
6. **Cutover/load measurements:** only after Projection implementation and representative target PostgreSQL 18/hardware environment; set no numeric budget before measured evidence and acceptance.
7. **Independent review:** review exact final SHA, test implementation, fixture isolation, captured evidence and all failures before any gate closure.

## 11. Current disposition

- This document is a pre-G1 test-harness design only.
- No harness source or test code has been added.
- No PostgreSQL test was run for this design.
- Inventory Test Spec v1.1 remains ALL TESTS NOT RUN; every existing case remains NOT_RUN.
- LOR-X01…X10 classifications in Evidence Map v0.1 remain unchanged.
- The global lock order remains non-normative; cross-module execution is blocked on owner acceptance and a normative Platform registry.
- O20-E remains OPEN; O-20 and O-21 remain OPEN; Organization G1 is NOT GRANTED; Inventory activation is unauthorized.

**END — MDG-INVENTORY-POSTGRES-LOCK-ORDER-HARNESS-DESIGN-001 v0.1**
