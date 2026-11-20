# MDG Inventory PostgreSQL Physical Design — Corrected Design Contracts

| Field | Value |
|---|---|
| Document ID | MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001 |
| Version | 0.2 |
| Status | CORRECTED TECHNICAL DESIGN — INTERNAL RE-AUDIT REQUIRED — NOT IMPLEMENTATION AUTHORITY |
| Sole architecture baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Logical model | MDG-INVENTORY-LOGICAL-ERD-001 v0.2 |
| Governing semantics | MDG-INVENTORY-STAGE-05-EQUATIONS-CUSTODY-STOCKTAKE-001 v0.1 |
| Supersedes | Physical Design v0.1 for the contracts explicitly replaced below |
| Target DBMS | PostgreSQL 18 |
| Scope | Durable idempotency, audit-intent atomicity, immutable committed ledger enforcement, global lock hierarchy and committed-prefix sequence/watermark |
| Exclusions | Executable SQL, DDL, EF mapping, migration, grants, API/runtime implementation, PostgreSQL execution, Posting Rules, release approval |

## 1. Authority and replacement effect

This is the corrected successor to physical-design v0.1. It is read together with v0.1, Stage 05 and ERD v0.2. For the four blocker areas below, this document replaces conflicting or incomplete v0.1 wording. All other v0.1 design content remains in force unless inconsistent with this document. v3.0.3 remains the sole accepted architecture baseline; this document does not amend it.

The correction closes design ambiguity only. It does not establish implementation, PostgreSQL behavior, passing tests, O-35 closure, Posting Release, production migration or Go-Live.

## 2. Corrected table map additions

| Entity | Key / uniqueness | Purpose | Mutability |
|---|---|---|---|
| inventory.command_receipt | UUID; UNIQUE(actor_id, operation_code, idempotency_key) | Durable command boundary for every mutating Inventory command, including commands with no movement | Insert once; completion set once in same transaction; immutable thereafter |
| inventory.stock_scope_lock | PRIMARY KEY(warehouse_id, variant_id) | One canonical parent lock and transactional committed-sequence allocator per Warehouse × Variant across all Sections/tracking/status buckets | Counter increments only under row lock in transaction committing affected movement |
| inventory.movement_scope_entry | UNIQUE(warehouse_id, variant_id, ledger_sequence); UNIQUE(movement_id, warehouse_id, variant_id) | One committed sequence per movement and each affected parent scope | Append-only |
| inventory.audit_intent_outbox | UUID; UNIQUE(event_id) | Durable Inventory-owned audit intent, committed atomically with Inventory effects and consumed by AuditSecurity | Payload immutable; delivery/ack metadata controlled |
| inventory.active_projection_generation | projection name + scope identity | Atomic pointer to active projection generation and declared watermark | Controlled cutover only |

Existing StockMovement and StockMovementLine remain authoritative physical movement facts. A movement with several lines in the same Warehouse × Variant scope receives one scope sequence for that scope, not one sequence per line. Inventory does not add a canonical AuditEvent table; AuditSecurity remains its sole owner.

## 3. BLOCKER PD-AUD-01 — Durable idempotency contract

### 3.1 Coverage

Every mutating Inventory command MUST enter one idempotency boundary whether or not it creates StockMovement. This includes movement/receipt/issue/status transitions; reservation create/allocate/release/expire/pick; custody create/handover/return/consume/resolve/handoff; transfer create/dispatch/receive/resolve; stocktake create/observation/recount/review/approval/adjustment; valuation allocation/TrueUp; and exposed administrative projection rebuild/cutover commands. Read-only queries are excluded.

EventId, MovementId, OutboxId and source-document identity are distinct from IdempotencyKey and must never substitute for it.

### 3.2 Scope and canonical digest

The key is scoped by authenticated ActorId (derived by trusted server, never accepted from request body), stable OperationCode, OperationContractVersion and client-generated IdempotencyKey. No tenant/company/branch dimension is added because current product scope is single-company/single-location.

The server computes a canonical request digest over all semantically material, validated command inputs. Canonicalization and digest algorithms are versioned and stored. Server-derived values (ActorId, server time, generated aggregate IDs) are execution/result data, not client-controlled digest inputs. Any excluded request field must be explicitly listed in the API contract; no implicit exclusions.

### 3.3 Atomic receipt model

inventory.command_receipt stores at minimum: CommandReceiptId; ActorId; OperationCode/Version; IdempotencyKey; CanonicalizationVersion; DigestAlgorithm; RequestDigest; COMPLETED state; ResultKind; stable ResultReference(s); versioned replay-response snapshot; StartedAtUtc; CompletedAtUtc; correlation/evidence references.

The receipt insert, all domain changes, movement/scope entries where applicable, projections, domain outbox, audit-intent outbox and receipt completion occur in the SAME PostgreSQL transaction. No committed PROCESSING receipt exists. If any part fails, receipt and effects roll back together.

Concurrent requests with the same unique key serialize at the unique index. The loser waits for the first transaction:
- first commits: loser reads the completed receipt;
- first rolls back: loser can claim the key and execute;
- same digest: return the stored stable response without reapplying effects;
- different digest: reject with IDEMPOTENCY_KEY_REUSE_CONFLICT, emit a separate security signal with fresh server correlation identity, and perform no domain effect.

The replay response is the original committed response, not a newly calculated view of mutable current state.

### 3.4 Retention and restore

No arbitrary short expiry is introduced. Retain receipt and uniqueness claim at least as long as the source command can be retried, replayed, audited or restored from backup. Exact legal/business retention is a platform/security records-policy dependency to resolve before DDL. Purging is prohibited while linked movements, audit intents, source documents, replay windows or backup obligations remain. If archival is later required, the unique-key tombstone remains effective.

Backup/restore includes receipts, unique keys, result references and linked outbox state. A restored database must not accept a previously completed key as a new command.

### 3.5 Required proof

Concurrent identical requests produce one effect and the same result; same key/different digest fails closed; lost response after commit returns stored result; rollback leaves neither receipt nor effect; exercise both movement and non-movement commands; restore preserves deduplication.

## 4. BLOCKER PD-AUD-02 — Audit durability and ownership

### 4.1 Selected integration pattern

AuditSecurity exclusively owns canonical AuditEvent schema, storage, retention, access policy and query surface. Inventory MUST NOT create a duplicate canonical audit table or write to AuditSecurity private tables.

Use a transactional audit-intent outbox:
1. Inventory constructs a versioned, minimized audit-intent envelope.
2. The envelope is inserted into inventory.audit_intent_outbox in the same transaction as command receipt, domain changes, movement, scope entries and synchronous projections.
3. Commit guarantees both the Inventory effect and durable local audit intent.
4. A worker delivers the envelope after commit through AuditSecurity's published ingestion contract.
5. AuditSecurity validates and deduplicates by stable EventId, persists canonical AuditEvent and returns durable acknowledgement.
6. Inventory records delivery acknowledgement; retries use the same EventId.

There is no distributed transaction claim between Inventory and AuditSecurity. The atomic guarantee is durable local intent; canonical AuditEvent delivery is eventual, observable and retryable.

### 4.2 Envelope and delivery state

Envelope fields: stable EventId; contract version; event type; aggregate identity/version; command receipt reference; trusted Actor reference; server timestamps; correlation/source references; minimal evidence fields; canonical payload digest. No password, recovery secret or unnecessary personal data.

States: PENDING, LEASED, RETRYABLE_FAILURE, DELIVERED_ACKNOWLEDGED, QUARANTINED. Leases expire and are reclaimable. Delivery is at-least-once; AuditSecurity EventId uniqueness is the final deduplication boundary. Timeout after remote commit is ambiguous and is retried with the same EventId. Quarantine preserves the original immutable envelope, raises a durable alert and requires explicit disposition.

### 4.3 Command behavior and remaining dependency

Inventory command success requires successful commit of its local required audit intent, not synchronous remote availability. A post-commit AuditSecurity outage does not undo or rewrite committed movement; intent remains pending/retryable and observable.

Before DDL, AuditSecurity/platform security must publish ingestion schema/version and authentication; EventId deduplication and acknowledgement semantics; retry/backoff/quarantine contract; event classes that require downstream delivery before a later sensitive operation; alert thresholds/escalation; retention/privacy classification. The local atomicity and ownership design is fixed; downstream contract details remain a dependency.

### 4.4 Required proof

Pre-commit failure leaves no movement/projection/receipt/outbox partial state; AuditSecurity outage after commit preserves intent; lost acknowledgement after remote commit retries same EventId and yields one canonical event; invalid envelope quarantines without deletion; backup/restore resumes pending intents and deduplicates.

## 5. BLOCKER PD-AUD-03 — Database-enforced committed-ledger immutability

### 5.1 Fact boundary

Draft command/workflow state is separate from committed ledger facts. Drafts may change only before commit with optimistic version checks. Once committed, StockMovement, StockMovementLine and MovementScopeEntry are immutable forever. Source identity, EventId, command receipt identity, scope sequence and committed timestamps are immutable after commit.

Correction/reversal is a new linked movement with reason/evidence. UPDATE/DELETE is never a correction path.

### 5.2 Required database enforcement pattern

The implementation MUST enforce the commit boundary in PostgreSQL, not by application convention alone:

- Runtime application role has no direct UPDATE or DELETE privilege on committed movement/header/line/scope-entry storage.
- Runtime role cannot directly set COMMITTED state or insert committed ledger facts using unrestricted table DML.
- A narrowly scoped SECURITY DEFINER command function (or equivalent reviewed database-enforced boundary) validates draft/version/authorization/source/idempotency and atomically inserts immutable committed header, lines and scope entries.
- Function owner is a non-login migration/schema-owner role; search_path is fixed to trusted schemas; object names are schema-qualified; PUBLIC EXECUTE is revoked.
- Actor identity is bound from trusted server execution context, not arbitrary client input.
- Append-only committed tables permit INSERT only through the controlled function; runtime receives no UPDATE/DELETE/TRUNCATE/ALTER/trigger-disable privilege.
- BEFORE UPDATE OR DELETE protection triggers reject mutation on committed tables as defense in depth, including child lines and scope entries.
- Direct INSERT by runtime principal is denied; only the validated command function performs committed inserts.
- Draft storage, if retained, is separate and never queried as committed stock truth.

Any alternative must provide equal or stronger database-enforced guarantees and receive review before DDL. Application-only checks, repository discipline or broad table DML are unacceptable.

### 5.3 Commit invariants

Before inserting committed facts, the command boundary validates receipt/digest; source uniqueness; authorization/state transition; complete non-empty lines where movement type requires; positive magnitude and explicit direction; UOM/tracking/warehouse/variant/status; all scope locks and sequence allocations; conservation and reversal bounds; projection updates and both outbox envelopes. Header, lines and one scope entry per affected Warehouse × Variant are committed in one transaction. A deferred constraint trigger or equivalent transaction-end validation prevents a committed header without required lines/scope entries.

### 5.4 Privileged maintenance

Schema-owner/migration credentials are never runtime credentials. Emergency repair is not a routine correction method. If legally required and separately authorized, break-glass requires dual authorization, immutable external evidence, incident reference, before/after digest and post-action reconciliation. Not an R1 application capability.

### 5.5 Required proof

Using actual runtime DB principal, direct INSERT/UPDATE/DELETE/TRUNCATE and trigger-disable attempts on committed header, lines, source identity, EventId and scope entries are rejected. Approved commit function succeeds; reversal is a new linked row; original remains unchanged. Child-line mutation cannot bypass committed parent.

## 6. BLOCKER PD-AUD-04 — One global lock hierarchy

### 6.1 Total lock order

All Inventory commands acquire conflicting locks in strictly ascending order and never acquire a lower-ranked resource after a higher-ranked one:

| Rank | Resource | Deterministic order |
|---:|---|---|
| 0 | Command idempotency claim | ActorId, OperationCode, IdempotencyKey |
| 1 | Workflow aggregate roots/lines: Reservation, Custody, Transfer, Stocktake, Valuation | fixed entity-type code, then UUID ascending; acquire all required rows in order |
| 2 | Warehouse × Variant parent stock-scope locks | WarehouseId UUID ascending, then VariantId UUID ascending |
| 3 | Serialized StockUnit rows | StockUnitId UUID ascending |
| 4 | UnitIdentifier active-claim rows | namespace code, normalized identifier bytes, UnitIdentifierId |
| 5 | Other child/workflow rows not covered above | fixed entity-type code, then UUID ascending |
| 6 | Projection rows and active-generation/checkpoint rows | projection name, scope key, grain key |
| 7 | Outbox delivery metadata rows | OutboxId UUID; only in short worker transactions, never held while acquiring domain locks |

All rows at a rank are sorted and locked before moving to the next rank. Shared aggregate roots precede their children. Administrative, expiry, repair and reconciliation commands obey the same order.

### 6.2 Unified scope lock and sequence

The single authoritative stock serialization scope is Warehouse × Variant across all Sections, locations, status buckets, Lots and serial/IMEI units. inventory.stock_scope_lock is both the negative-stock parent lock across Sections and the transactional committed-sequence allocator. There is no finer-grain authoritative sequence allocator. Fine-grain balances are projections derived from parent-scope ordered movements and line dimensions.

Each movement receives one monotonically increasing ledger_sequence per affected Warehouse × Variant. Multiple lines in the same scope share one sequence. A movement touching multiple scopes acquires all scope rows sorted by (WarehouseId, VariantId), increments each counter and writes one MovementScopeEntry per scope in the same transaction. All scopes commit or roll back together.

### 6.3 Committed-prefix argument

Every stock-changing writer: (1) locks relevant stock_scope_lock rows FOR UPDATE; (2) reads current sequence only after acquiring the lock; (3) increments transactionally; (4) inserts movement facts and scope entries; (5) updates synchronous projections/outboxes; (6) holds locks through commit/rollback.

A second writer cannot allocate until the first commits or rolls back. On rollback, counter increment and entry roll back. On commit, next writer sees committed counter. Thus values form a contiguous committed prefix per Warehouse × Variant if every writer follows this boundary. PostgreSQL nextval(), UUID order, timestamps and MAX(id) are not authoritative ledger order.

### 6.4 Executable single-observation watermark algorithm

Each CountObservation belongs to one Warehouse × Variant parent scope. A stocktake across multiple Warehouses/Variants stores a vector of per-scope observations/watermarks; it does not claim one globally simultaneous instant across independent scopes.

For each observation, execute one short READ COMMITTED transaction:
1. Validate and claim observation command idempotency receipt.
2. Lock required Stocktake aggregate rows (rank 1) in deterministic order.
3. Acquire its Warehouse × Variant stock_scope_lock (rank 2).
4. After lock grant, issue a fresh SQL statement to read committed ledger_sequence from that locked row. Do not use a value sampled before lock acquisition or a transaction-start snapshot.
5. While holding the lock, replay committed scope entries through W (or read a projection proven exactly at W), filtering lines to the count's Section/location/Lot/StockUnit/status grain.
6. Insert immutable CountObservation with server observed_at_utc, W, count, UOM/conversion version, actor/evidence and expected-at-observation result/reference.
7. Commit and release lock.

Every movement writer takes the same parent lock before sequence allocation and holds it through commit. A movement committed before observation lock acquisition is included in W and replay. A writer waiting behind observation gets W+1 or later after observation commits. A rolled-back writer contributes neither sequence nor movement. This establishes the single-scope observation boundary.

For multiple parent scopes, capture each observation independently and retain vector (WarehouseId, VariantId, W). A globally synchronized multi-scope physical snapshot would require a separate coordinated freeze protocol and approval.

### 6.5 Locking examples and retry

Reservation issue/allocation/expiry locks Reservation aggregate first, then stock scope. Custody return/consume locks Custody aggregate first, then stock scope, then StockUnit/identifier. Serialized transfer/custody follows same order. Opposite-direction Section transfers sort all scope locks. No command may lock stock scope first if it also needs a workflow aggregate.

Deadlock/serialization errors restart the entire transaction from rank 0 with the same idempotency identity, bounded attempts and jitter. Validation/authorization errors are not retried. Record deadlock/retry metrics; successful retry does not erase incident evidence.

### 6.6 Required race proof

Multiple independent PostgreSQL sessions must exercise reserve-vs-issue, expiry-vs-issue at ExpiresAt, custody return-vs-consume, serialized transfer-vs-handover, opposite Section transfers, overlapping multi-scope movements, observation/writer races, and rollback after sequence increment. Assert conservation, no duplicate allocation, no watermark ambiguity and no committed sequence gap.

## 7. Reconciliation with governing design

Preserved unchanged: append-only committed movement ledger and rebuildable StockPosition; ATP = Sellable − EffectiveReserved; Custody distinct from Reservation and excluded from ATP; atomic paired same-Warehouse Section transfer; FE-31 remains R2/NOT ENABLED; Sales TTL remains OPEN/configurable; Repair reservation has no automatic TTL; negative-stock rules exactly v3.0.3 D-258 and feature OFF pending separate gate; exact NUMERIC/no floats; final IRR integer Rial with one-time document-total AwayFromZero rounding and maximum absolute difference 1 Rial/document; no 100,000 IRR threshold or row-level rounding; 110801 sole Inventory GL control; no COA/Posting inference; no duplicate module masters or AuditEvent.

## 8. Re-audit disposition matrix

| Finding | Design disposition in v0.2 | Remaining evidence |
|---|---|---|
| PD-AUD-01 idempotency | CLOSED IN DESIGN | PostgreSQL uniqueness/concurrency/rollback/restore tests |
| PD-AUD-02 audit durability/ownership | CLOSED IN DESIGN for local atomic intent and canonical ownership | AuditSecurity ingestion/ack contract + retry/dedup/quarantine tests |
| PD-AUD-03 immutable committed ledger | CLOSED IN DESIGN with DB-enforced command boundary | DDL and runtime-principal privilege tests |
| PD-AUD-04 global lock order | CLOSED IN DESIGN with total hierarchy | PostgreSQL multi-session race/deadlock tests |
| PD-AUD-05 sequence scope | CLOSED IN DESIGN as Warehouse × Variant parent sequence | PostgreSQL committed-prefix proof |
| PD-AUD-06 stocktake observation | CLOSED IN DESIGN for one scope; multi-scope uses vector watermarks | Concurrent observation/writer tests |
| PD-AUD-07 projection rebuild/cutover | OPEN from v0.1 | Generation/catch-up/cutover algorithm |
| PD-AUD-08..12 | OPEN from v0.1 | Final data dictionary and edge-case resolution |

"Closed in design" means sufficiently specified for review, not implemented, tested, externally approved or released.

## 9. Remaining prerequisites before DDL freeze

AuditSecurity ingestion/acknowledgement/retention contract; Catalog quantity precision/base UOM/conversion; platform UUID generator; serial/IMEI namespace/normalization; exact database function deployment and privilege review; PD-AUD-07 projection rebuild/cutover; PD-AUD-08..12 data dictionary/expiry/nullability; measured index/query plan; backup/restore, security and privacy review.

## 10. New test catalogue

| Test ID | Scenario | Required assertion |
|---|---|---|
| PD-T-001 | Concurrent same key/same digest | One effect; same stable result |
| PD-T-002 | Same key/different digest | Conflict; no second effect |
| PD-T-003 | Lost response after commit | Retry returns stored response |
| PD-T-004 | Command rollback | No receipt or domain/outbox partial state |
| PD-T-005 | Non-movement command retry | Workflow effect occurs once |
| PD-T-006 | Audit intent atomicity | Intent commits/rolls back with domain transaction |
| PD-T-007 | Audit delivery retry | Ambiguous remote commit retries same EventId; one canonical event |
| PD-T-008 | Audit quarantine | Intent retained and alerted |
| PD-T-009 | Runtime committed-row mutation denial | UPDATE/DELETE/TRUNCATE/direct INSERT/trigger disable denied |
| PD-T-010 | Valid commit and reversal | Approved boundary commits; reversal is new immutable fact |
| PD-T-011 | Global lock-order races | Conservation maintained; deadlock/retry counts recorded |
| PD-T-012 | Committed-prefix rollback | Counter rollback leaves no gap/visible entry |
| PD-T-013 | Multi-scope movement | Sorted locks; all-or-none sequence entries |
| PD-T-014 | Stocktake observation race | W deterministic for writer-before/after-lock interleavings |
| PD-T-015 | Restore idempotency and audit | Restored system deduplicates old command and resumes pending delivery |

All PD-T tests remain NOT RUN until separately authorized implementation and exact-SHA PostgreSQL evidence.

## 11. Final disposition and non-authorization

The four blocking design ambiguities and associated sequence/observation ambiguity are resolved in this v0.2 design. PD-AUD-07 and PD-AUD-08..12 remain open, so the overall physical design is NOT READY for DDL/migration freeze.

No SQL, DDL, EF mapping, migration, grant, API/runtime implementation, PostgreSQL test, Posting Rule, Posting Release, production migration or Go-Live is authorized by this document.

**END — MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001 v0.2**
