# MDG Inventory PostgreSQL Physical Design — Internal Adversarial Technical Audit

| Field | Value |
|---|---|
| Audit ID | MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-AUDIT-001 |
| Version | 0.1 |
| Date | 2026-11-20 |
| Audited artifact | MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001 v0.1 |
| Artifact SHA-256 | 75dd5e6f0978a535f416e163a3f22254280a0fb2241dc8bbea30c761c11f06a3 |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 — sole accepted architecture baseline |
| Logical reference | MDG-INVENTORY-LOGICAL-ERD-001 v0.2 |
| Semantic reference | MDG-INVENTORY-STAGE-05-EQUATIONS-CUSTODY-STOCKTAKE-001 v0.1 |
| Review type | Internal adversarial design audit; not an independent external reviewer attestation |
| Overall disposition | **NOT ACCEPTED FOR DDL/MIGRATION DESIGN FREEZE — BLOCKING CLARIFICATIONS REQUIRED** |

## 1. Executive disposition

The physical design is directionally aligned with v3.0.3, Stage 05 and ERD v0.2 in its central choices: append-only committed movement facts, exact numeric arithmetic, explicit stock ownership, a transactional committed-prefix allocator rather than plain `nextval()`, per-observation stocktake watermarks, rebuildable projections, custody separation, and no premature Posting/GL activation.

However, it is not yet sufficiently deterministic to serve as a safe DDL/migration implementation specification. Four blocking design gaps remain:

1. command idempotency is specified but its durable persistence model and atomic scope are not;
2. shared AuditSecurity ownership is respected conceptually, but the required audit durability/atomicity boundary is not implementable as written across module ownership;
3. committed-ledger immutability is asserted, but the database enforcement mechanism and privilege model are not specified sufficiently to prevent direct mutation;
4. the global lock-order contract is internally incomplete and can permit deadlocks between reservation and stock-scope operations.

Three further high-priority clarifications are required before DDL freeze: one canonical sequence/watermark scope, a precise stocktake snapshot protocol, and explicit projection update/rebuild concurrency rules. No implementation, SQL, migration, PostgreSQL test, runtime behavior or release status was inferred from this document review.

## 2. Scope and method

The review inspected the complete physical-design proposal v0.1 and compared its stated contracts with the current Stage 05 semantic workpaper and ERD v0.2. The review focused on:

- committed-prefix sequencing and rollback/visibility;
- Warehouse × Variant concurrency and negative-stock limits;
- reservation/issue/expiry and multi-aggregate lock ordering;
- stocktake observation consistency and replay;
- atomicity across movement, projection, outbox and audit;
- idempotency and retry behavior;
- committed-fact immutability and database privilege enforcement;
- projection checkpoints, rebuild and quarantine behavior;
- preservation of ownership, accounting and release boundaries.

This is an adversarial internal review by the same assistant workflow that prepared the proposal. It is not represented as an independent external review, code review, database execution or PostgreSQL proof.

## 3. Findings

### BLOCKER — PD-AUD-01: Idempotency has no complete durable command model

**Evidence in proposal:** Sections 6, 9, 13 and 20 require scoped IdempotencyKey uniqueness, canonical payload hashing, same-key/same-payload replay, same-key/different-payload rejection and retry-safe command effects. The proposed table map does not define a command-idempotency record/table or identify where the key, canonical request digest, in-progress/completed state, response/result reference and retention boundary live. The movement table's source identity and the outbox EventId are not substitutes: commands such as reservation allocation/release, pick, custody workflow transitions and stocktake observation may be idempotent without creating a StockMovement.

**Risk:** a retry after timeout or ambiguous commit can duplicate workflow effects, or the service may be unable to return the original result. An idempotency key stored only on movement cannot protect non-movement commands or distinguish command identity from event identity.

**Required correction before DDL:** define one durable command-idempotency contract covering every mutating Inventory command. Specify key scope (actor/operation and the product's single-company boundary without inventing tenant dimensions), canonicalization/hash version, transaction boundary, in-progress concurrency behavior, completed result reference or replayable response, same-key/different-payload rejection, failure/rollback semantics, expiry/retention and uniqueness. State exactly which commands create a movement and which do not. Keep EventId distinct from IdempotencyKey.

**Acceptance test:** two concurrent identical requests produce one effect and the same stable result; a retry after simulated lost response returns that result; same key with a different payload fails closed; test both movement and non-movement commands.

### BLOCKER — PD-AUD-02: Audit ownership and atomic durability boundary are unresolved

**Evidence in proposal:** Sections 3, 9 and 13 state that canonical AuditEvent is owned by AuditSecurity and that Inventory must not create a duplicate canonical audit table. The same sections require movement, projection, outbox and required audit correlation to be atomic, and state that audit/outbox failure before commit rolls back the stock transaction. The table map contains InventoryOutboxEnvelope but no explicit Inventory audit-intent/envelope entity or exact AuditSecurity transaction contract.

**Risk:** if AuditSecurity persistence is a separate service/transaction, Inventory cannot atomically commit its movement and a remote AuditEvent. If it is a shared PostgreSQL transaction, the proposal must explicitly define the allowed cross-module transaction boundary and ownership contract. An unspecified “invoke approved audit boundary” can either lose audit evidence after stock commit or cause a distributed transaction assumption.

**Required correction before DDL:** choose and document exactly one permitted pattern:
- a shared PostgreSQL transaction contract in which Inventory writes a narrowly defined AuditSecurity-owned audit record through an explicitly authorized boundary; or
- a durable Inventory transactional audit-intent/outbox envelope committed with movement, with AuditSecurity consuming it idempotently and maintaining the canonical AuditEvent. In this pattern distinguish “audit intent durably recorded” from “canonical AuditEvent delivered”; define retry, alert, reconciliation and commands blocked by undelivered audit evidence.

Do not create a second canonical AuditEvent table. Define the exact atomic guarantee that is achievable and observable.

**Acceptance test:** force failure before commit and prove no movement/outbox/audit-intent partial state; then commit movement while AuditSecurity delivery is unavailable and prove the durable audit intent survives, is retried, deduplicated and reconciled without rewriting the committed movement.

### BLOCKER — PD-AUD-03: Committed-ledger immutability is not tied to enforceable database controls

**Evidence in proposal:** Sections 2, 4, 7, 13, 14 and 20 state that committed movement facts are immutable, while the proposed movement lifecycle permits draft workflow followed by COMMITTED. The privilege section says runtime should not receive unrestricted table mutation rights, but does not define how the runtime can perform valid inserts/transitions while preventing UPDATE/DELETE of committed rows, including line rows, source links and referenced evidence.

**Risk:** application-layer discipline alone does not protect financial/inventory evidence from an accidental or compromised runtime path, maintenance script or future repository method. A committed parent could be changed indirectly by editing/deleting its lines unless the database write boundary prevents it.

**Required correction before DDL:** specify a concrete enforceable pattern: e.g. immutable committed fact tables plus separate draft/workflow tables; or tightly scoped stored command procedures/triggers with runtime privileges that cannot UPDATE/DELETE committed rows. Define allowed transition from draft to committed, how line rows become immutable atomically, how correction/reversal links are appended, and how privileged maintenance is audited. Do not grant broad table UPDATE/DELETE and rely only on application checks.

**Acceptance test:** using the actual runtime DB principal, attempt UPDATE/DELETE on committed movement and lines, source identity, event identity and audit evidence; every forbidden mutation must be rejected by PostgreSQL. Valid commit and linked reversal must still succeed through the approved boundary.

### BLOCKER — PD-AUD-04: Lock-order specification is not globally consistent

**Evidence in proposal:** Section 9 describes Reservation commands as locking ReservationLine and relevant stock scope; Section 10 explicitly orders ReservationLine before stock scope. Other commands describe stock-scope-first operations, and the proposal does not define one total order for mixed commands that touch reservation, custody, StockUnit/identifier, movement, sequence allocator and projection rows. It gives a canonical scope-key order for multiple stock scopes, but that is not a complete order across entity classes.

**Risk:** two transactions can acquire the same resources in opposite order (for example, a reservation workflow locks a ReservationLine then waits for the Warehouse × Variant scope while another operation holds that scope and needs the same ReservationLine). PostgreSQL will detect a deadlock, but repeated deadlocks can starve user operations and bounded retry alone does not prove acceptable behavior.

**Required correction before DDL:** publish one global lock hierarchy for all mutable rows and scope locks, including ReservationLine, CustodyLine, StockUnit, UnitIdentifier, Warehouse × Variant parent scope, fine-grain sequence rows, Transfer and StockPosition/projection rows. Every command's acquisition order must conform. For commands requiring multiple scope locks, sort all scope keys before locking; never acquire a lower-ranked resource after a higher-ranked resource. If a command must deviate, redesign the transaction or document a narrowly proven exception.

**Acceptance test:** create adversarial concurrent tests for reserve-vs-issue, expiry-vs-issue, custody-return-vs-consume, serialized-unit transfer-vs-custody, and two opposite-direction Section transfers; run repeated multi-session races and record deadlock/serialization retry counts and final conservation invariants.

### HIGH — PD-AUD-05: Sequence partition key and stocktake watermark scope are not one canonical definition

**Evidence in proposal:** Sections 4, 8, 10 and 12 describe a per-scope allocator, Warehouse × Variant negative-stock parent locking, and additional tracking/location grain for stocktake. The scope_sequence row is described using WarehouseId + VariantId + required tracking scope, while the text also requires a single parent lock across Sections for negative stock and a consistent watermark for count/replay.

**Risk:** if different commands allocate sequence numbers at different grains (for example, one per Section/status and another at Warehouse × Variant), a stocktake watermark may not represent a contiguous committed prefix for the exact count scope. A fine-grain counter cannot by itself prove a parent-scope snapshot across all child buckets.

**Required correction before DDL:** define the canonical ledger ordering scope and the relationship between parent and child scopes. Prefer one Warehouse × Variant parent committed sequence for all stock-affecting movements across Sections/status/tracking buckets where that is the required stocktake and negative-stock scope; use separate derived projections for finer grains. If more than one sequence is necessary, define a vector watermark and prove atomic capture/replay across every component. Specify how a movement touching multiple Warehouse × Variant scopes obtains all locks and sequence values in deterministic order.

**Acceptance test:** concurrently commit movements in multiple Sections/status buckets, roll back selected transactions, capture an observation, and prove that its watermark includes every committed movement before the observation and excludes every later movement, with no invisible lower-sequence commit.

### HIGH — PD-AUD-06: Stocktake “same snapshot + watermark” protocol is still a requirement, not an executable algorithm

**Evidence in proposal:** Sections 9, 10 and 12 require a consistent snapshot/lock protocol and recommend READ COMMITTED with explicit locks when sufficient, but do not specify the exact sequence of lock acquisition, snapshot creation, reads of ledger/projection, watermark capture and observation insert.

**Risk:** at READ COMMITTED, separate statements can see different snapshots. A lock-based protocol can make this correct, but only if every movement writer takes the same parent scope lock before allocating sequence/committing and the observation reads its watermark and expected balance under a proven ordering. Without that exact algorithm, the requirement can be implemented incorrectly while passing simple tests.

**Required correction before DDL:** write a numbered transaction algorithm for observation capture, including isolation level, lock target, lock order, watermark read, replay/projection read, observation insert and commit. Explain why a waiting transaction sees the last committed sequence row and why no writer can commit into the observed prefix after capture. Define the behavior when a count spans multiple Warehouse × Variant scopes.

**Acceptance test:** pause a movement transaction before and after scope-lock acquisition while a count observation races; prove the observation and replay result are deterministic for each interleaving.

### HIGH — PD-AUD-07: Projection writer/rebuilder serialization and cutover are under-specified

**Evidence in proposal:** Sections 9 and 11 say the projection is updated in the movement transaction, can also be rebuilt/caught up by a worker, and uses shadow rebuild plus atomic switch. They do not define how synchronous writers coordinate with a shadow rebuild/cutover or how a projection version/checkpoint is prevented from regressing or dropping a committed movement.

**Risk:** a rebuild can race with new movement commits and switch to a shadow projection that omits events committed during the rebuild, or a synchronous writer can update the old projection while the cutover activates the new one.

**Required correction before DDL:** define a generation/version model: capture start watermark, build shadow through a known watermark, catch up the contiguous delta while coordinating with writers, acquire a short cutover lock, verify no gap and atomically switch active generation/checkpoint. Define whether writers dual-write, block briefly, or write only to the active generation during cutover. Specify quarantine/recovery if a checkpoint is missing or projection totals differ.

**Acceptance test:** run continuous movement writes while rebuilding and cutting over; prove the active projection equals deterministic ledger replay at its declared watermark and never moves backward.

## 4. Additional findings

### MEDIUM — PD-AUD-08: StockPosition identity is ambiguous

The table map permits either UUID primary key or a composite natural key, while the canonical grain is multi-dimensional and nullable for explicitly inapplicable tracking/location dimensions. Before DDL, choose one representation and define canonical null/UNLOCATED normalization so uniqueness is stable. Do not allow two rows to represent the same stock grain.

### MEDIUM — PD-AUD-09: Draft-to-commit state and line ownership need a sharper boundary

The proposal allows draft workflow and immutable committed movement in one table, but does not define whether draft lines are mutable, whether a committed parent can be committed with zero lines, how line count/quantity validation is enforced, or how concurrent draft edits are version-checked. Align this with the immutability enforcement finding PD-AUD-03.

### MEDIUM — PD-AUD-10: Reservation expiry needs explicit persisted time semantics

The proposal requires expiry using server time and says delayed cleanup must not retain expired reservations in ATP, but the physical table map does not explicitly identify ExpiresAt, expiry policy/version, expiration event identity or the distinction between effective expiry and asynchronous state cleanup. Define exact timestamp and boundary semantics (including equality at ExpiresAt) and test expiry-vs-issue under the same lock contract. Do not invent a final Sales TTL; it remains OPEN/configurable under v3.0.3.

### MEDIUM — PD-AUD-11: Audit/outbox retry observability needs separate states

The proposal should distinguish local durable intent, queued, leased, delivered, retryable failure, terminal/quarantined failure and canonical AuditSecurity acknowledgement. A single generic delivery state is insufficient if audit durability is a release invariant. Define alert thresholds and reconciliation ownership without making delivery failure silently disappear.

### LOW — PD-AUD-12: Physical naming and exact key/null rules are not yet consistently normalized

The table map alternates logical names (LocationRef, VariantRef) and proposed storage names (stock_position, stock_unit_lot), and some relationship fields are described as nullable without a full applicability matrix. This is acceptable at proposal stage but must be normalized in the final data dictionary before DDL.

## 5. What passed this design-level review

The following choices are sound at the proposal level and should be preserved:

1. PostgreSQL sequences are correctly rejected as the authoritative committed-prefix watermark because sequence allocation is not transactional.
2. A transactionally locked per-scope sequence row can provide committed-prefix ordering if all writers and observation paths use the same lock protocol and the lock is held through commit.
3. The append-only movement ledger is correctly separated from rebuildable StockPosition projections.
4. ATP is correctly stated as Sellable − EffectiveReserved; custody and unreceived inbound quantities are not added.
5. Custody is distinct from reservation and excluded from warehouse ATP; physical handover/return/consumption requires linked committed movement evidence.
6. Same-Warehouse Section transfer preserves Warehouse totals through atomic paired source/destination lines; FE-31 remains R2/not enabled.
7. Count observations are immutable, each observation owns its watermark, and adjustment is a separate authorized committed movement.
8. Exact NUMERIC is used for quantities/intermediate valuation; floating point is prohibited; final IRR rounding remains the accepted one-time document-total AwayFromZero rule with maximum absolute difference 1 Rial/document.
9. Sales TTL remains OPEN/configurable; Repair reservation has no automatic TTL; negative-stock feature remains OFF pending its separate gate.
10. No COA inference, Posting Rule, DDL, migration, grant, runtime activation or release is introduced.

## 6. Required closure sequence

Do not start DDL or migration authoring from v0.1 yet. Close findings in this order:

| Order | Findings | Required deliverable |
|---|---|---|
| 1 | PD-AUD-01, PD-AUD-02, PD-AUD-03 | Idempotency, audit durability and immutable-write enforcement contracts |
| 2 | PD-AUD-04, PD-AUD-05, PD-AUD-06 | One global lock hierarchy, canonical sequence scope and executable stocktake algorithm |
| 3 | PD-AUD-07 | Projection rebuild/cutover concurrency algorithm |
| 4 | PD-AUD-08..12 | Final physical data dictionary and edge-case contracts |
| 5 | All blockers/high findings | Updated physical design version, traceability matrix and external reviewer handoff |
| 6 | After owner acceptance and separately authorized implementation scope | DDL/migration plan; still no execution until its own gate |

## 7. Test acceptance matrix

| Test family | Minimum adversarial cases | Required evidence |
|---|---|---|
| Idempotency | concurrent same-key/same-payload; same-key/different-payload; lost response; non-movement command retry | exact result identity, one effect, database constraints |
| Audit durability | pre-commit failure; AuditSecurity unavailable after commit; duplicate delivery; delayed retry | atomic local intent, no duplicate canonical event, reconciliation |
| Immutability | runtime UPDATE/DELETE on committed header/lines; valid reversal; privileged maintenance | PostgreSQL principal/privilege evidence and rejection logs |
| Lock order | reserve/issue; expiry/issue; custody return/consume; transfer/custody; opposite transfers | multi-session PostgreSQL traces, deadlock/retry metrics |
| Committed prefix | rollback, parallel scopes, multi-section movement, multi-scope transfer | no visible uncommitted sequence; contiguous replay |
| Stocktake | observation racing before/after writer lock; multi-scope count; late/backdated source | exact watermark, expected/observed/replay equality |
| Projection | continuous writes during shadow build/catch-up/cutover | active generation equals ledger replay at checkpoint |
| Recovery | backup/restore with idempotency/outbox/checkpoints | post-restore deduplication and replay reconciliation |

Every test result must be tied to exact source SHA, schema/migration SHA, PostgreSQL version, test ID, command, environment, logs and reviewer disposition. A structural validator or mocked test is not PostgreSQL concurrency evidence.

## 8. Architecture and release boundary

This audit is subordinate to MDG-ARCH-MASTER-001 v3.0.3. It does not amend the master, Stage 05 semantics or ERD ownership. The nine-sheet COA workbook remains abandoned and is not a source or prerequisite. No monetary threshold of 100,000 IRR is introduced; the accepted 1 Rial base and one-time document-total rounding rule remain unchanged.

**No DDL, SQL, EF mapping, migration, database grant, API, runtime implementation, Posting Rule, Posting Release, production migration or Go-Live is authorized by this audit.** O-35 and all relevant PostgreSQL runtime evidence remain open until exact-SHA execution and review.

**END — MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-AUDIT-001 v0.1**


## Current Architecture Master authority — 2026-11-24

The Product Owner has designated **MDG-ARCH-MASTER-001 v3.0.4** as the sole current, accepted, official and governing Architecture/Accounting Master. The v3.0.3 references in this dated review/evidence artifact identify the baseline actually reviewed or used when the artifact was produced; they are retained as historical provenance and are not silently rewritten. For current design decisions, v3.0.4 governs. Any unresolved section-level conflict or design dependency must be re-audited against v3.0.4 before implementation. This authority notice does not upgrade this artifact's review type, evidence strength, gate status or acceptance.
