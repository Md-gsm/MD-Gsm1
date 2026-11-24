# MDG Inventory PostgreSQL Physical Design — Logical-to-Physical Mapping

| Field | Value |
|---|---|
| Document ID | MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001 |
| Version | 0.1 |
| Status | TECHNICAL DESIGN PROPOSAL — REVIEW REQUIRED — NOT IMPLEMENTATION AUTHORITY |
| Sole architecture baseline | **MDG-ARCH-MASTER-001 v3.0.3** |
| Logical model | MDG-INVENTORY-LOGICAL-ERD-001 v0.2 |
| Governing semantics | MDG-INVENTORY-STAGE-05-EQUATIONS-CUSTODY-STOCKTAKE-001 v0.1 |
| Target DBMS | PostgreSQL 18 |
| Scope | Table mapping, key strategy, integrity constraints, indexes, transaction boundaries, concurrency, projection/replay and operational design |
| Exclusions | Executable SQL, DDL, EF Core mappings, migration, API implementation, runtime activation, Posting Rules, release approval |

## 1. Authority, purpose and limits

This proposal translates the logical ERD into a PostgreSQL-oriented physical design for review. It does not amend v3.0.3, Stage 05 or ERD v0.2. In any conflict, v3.0.3 prevails, followed by the accepted Stage 05 and ERD semantics. Older technical proposals are historical inputs only where consistent; they do not restore superseded TTL, ATP or accounting decisions.

**No SQL or migration is included.** Table and column names below are proposed identifiers, not committed schema. No migration number is reserved or allocated by this document. No physical schema, API, writer, database role grant or runtime feature is authorized.

## 2. Physical design principles

1. PostgreSQL is the authoritative transactional store for Inventory-owned facts. The application server is the only runtime database client; Windows clients do not connect directly to PostgreSQL.
2. Use normalized relational tables for authoritative facts. JSONB is permitted only for versioned immutable evidence/snapshots or outbox payloads where the contract explicitly requires extensibility; never use JSONB as a substitute for relational keys, constraints, queryable quantities or authorization.
3. Use UUID columns for opaque identifiers. This does not prescribe UUIDv7 or a generator until the platform-wide identifier policy is confirmed. Do not use database-generated identity integers as public aggregate identifiers.
4. All quantity/cost calculations use exact numeric types, never REAL/DOUBLE PRECISION. The final NUMERIC precision/scale for quantities is not fixed here: it must be derived from the accepted Catalog UOM precision/conversion contract and overflow analysis before DDL.
5. IRR monetary values are stored as integer Rial at the final approved accounting boundary. Intermediate allocation/valuation arithmetic uses exact NUMERIC with explicitly versioned precision and one-time final rounding; no floating point and no per-row rounding.
6. Technical instants use TIMESTAMPTZ and server UTC. Tehran business date is a separate DATE where required; do not convert date-only business values through client timezone.
7. Every committed ledger fact is append-only. Corrections, reversals, reassignments and late adjustments are new linked records.
8. All stock-changing operations have one explicit database transaction and one idempotency boundary.
9. Every projection has a declared source watermark/version and a deterministic rebuild path.
10. External module master data remains external. Cross-module references use typed opaque identifiers and version snapshots; Inventory cannot write another module's tables.
11. Constraints enforce invariants in PostgreSQL wherever expressible. Cross-row/cross-table invariants require serialized transactional commands and invariant/reconciliation checks; application-only check-then-write is insufficient.
12. Do not introduce partitioning, sharding, table inheritance or premature denormalization in R1 without measured evidence.

## 3. Proposed schemas and ownership

Use a dedicated PostgreSQL schema such as `inventory` for Inventory-owned tables. This is a namespace proposal, not a permission grant. Organization, Catalog, Accounting, AuditSecurity, Sales, Purchasing, RepairOperations, Persons and Treasury retain their own schemas/ownership.

- Inventory tables may store external IDs and version references but should not create cross-schema foreign keys unless the owning module explicitly publishes a stable database-level contract and lifecycle guarantee.
- Prefer typed reference columns (e.g. source_module, source_document_id, source_line_id, source_version) over a polymorphic foreign key that falsely implies referential enforcement.
- Canonical shared AuditEvent remains AuditSecurity-owned. Inventory writes its own outbox/correlation reference and invokes the approved audit boundary; it must not create a second global AuditEvent table.
- Do not create Inventory-owned copies of Party, User, Warehouse, Section, Product, Variant, UOM, COA, fiscal period or source document masters.

## 4. Proposed table map

The names below are descriptive proposed names. Exact columns and keys must be finalized against the approved API and source-module contracts.

| Table | Primary key | Important references / unique scope | Mutability |
|---|---|---|---|
| inventory.stock_unit | stock_unit_id UUID | variant_id, catalog_policy_version; unique active identity via identifier table | Controlled lifecycle |
| inventory.unit_identifier | unit_identifier_id UUID | stock_unit_id; identifier_type, namespace, normalized_value, active interval | Append/retire; no destructive reassignment |
| inventory.lot | lot_id UUID | variant_id, source receipt reference/version | Controlled lifecycle |
| inventory.stock_unit_lot | stock_unit_lot_id UUID | stock_unit_id, lot_id, validity interval | Controlled link history |
| inventory.stock_movement | movement_id UUID | movement_type, state, source identity, event_id, idempotency scope | Draft workflow then immutable COMMITTED |
| inventory.stock_movement_line | movement_line_id UUID | movement_id, variant_id, warehouse/location, lot/unit, UOM conversion version | Immutable after commit |
| inventory.stock_position | stock_position_id UUID or composite natural key | canonical stock grain + projection watermark | Rebuildable projection |
| inventory.reservation | reservation_id UUID | source document/version, purpose, state | Workflow mutable |
| inventory.reservation_line | reservation_line_id UUID | reservation_id, source line, variant/scope | Workflow mutable under conservation |
| inventory.reservation_allocation | allocation_id UUID | reservation_line_id, stock grain | Workflow mutable subset |
| inventory.reservation_pick | pick_id UUID | allocation_id, quantity/status | Workflow mutable subset |
| inventory.custody | custody_id UUID | custodian refs, purpose, state | Workflow aggregate |
| inventory.custody_line | custody_line_id UUID | custody_id, stock grain/StockUnit | Workflow aggregate |
| inventory.custody_event | custody_event_id UUID | custody_line_id, movement_line_id, event identity | Append-only |
| inventory.transfer | transfer_id UUID | transfer kind, source/destination, state | Workflow aggregate |
| inventory.transfer_line | transfer_line_id UUID | transfer_id, variant/tracking grain | Workflow aggregate |
| inventory.stocktake | stocktake_id UUID | scope version, start watermark, cutoff | Workflow aggregate |
| inventory.stocktake_count_line | count_line_id UUID | stocktake_id, stock grain | Stable count scope |
| inventory.count_observation | observation_id UUID | count_line_id, observation watermark and timestamp | Append-only |
| inventory.inventory_adjustment | adjustment_id UUID | observation_id, approval reference, committed movement_id | Immutable authorization |
| inventory.valuation_layer | valuation_layer_id UUID | source movement line/receipt reference, method/version | Immutable |
| inventory.valuation_allocation | valuation_allocation_id UUID | layer_id, target movement/issue | Immutable |
| inventory.valuation_true_up | true_up_id UUID | source adjustment/version, idempotency identity | Immutable |
| inventory.valuation_true_up_allocation | true_up_allocation_id UUID | true_up_id, retained/issued lineage target | Immutable |
| inventory.outbox_envelope | outbox_id UUID | event_id, aggregate id/version, contract version | Payload immutable; delivery metadata mutable |
| inventory.projection_checkpoint | checkpoint_id UUID or scope key | projection name, scope key, committed watermark | Controlled projection metadata |
| inventory.scope_sequence | scope key | warehouse_id + variant_id + required tracking scope | Atomic sequence allocator/lock row |

A table is not required merely because it appears in this proposal: optional entities must be justified by the source contract and test needs. Conversely, a logical entity may be implemented as a constrained subtype only if identity, audit and lifecycle remain explicit.

## 5. Key and column type policy

| Data | Proposed PostgreSQL type | Rule / unresolved prerequisite |
|---|---|---|
| Aggregate/entity IDs | UUID | Opaque IDs; generator/version follows platform policy |
| External module IDs | UUID or exact owning-module ID type | Match published contract; never cast text opportunistically |
| Version numbers | BIGINT or INTEGER | Monotonic positive version; exact range follows platform convention |
| State/type/reason | SMALLINT or constrained TEXT | Prefer stable code table/enum contract; never rely on display label |
| Quantity | NUMERIC(p,s) | p/s remain OPEN until Catalog precision and overflow proof |
| Unit conversion factor | NUMERIC(p,s) | Exact rational/decimal policy; conversion version is snapshotted |
| IRR final amount | BIGINT | Integer Rial; overflow bounds and checked arithmetic required |
| Intermediate monetary allocation | NUMERIC(p,s) | Exact arithmetic; scale/rounding at approved boundary only |
| Technical timestamp | TIMESTAMPTZ | UTC server generated |
| Tehran business date | DATE | Stored independently from timestamp |
| Hash/digest | BYTEA or fixed-length hexadecimal TEXT | Canonical encoding and algorithm version specified |
| Payload/evidence snapshot | JSONB | Immutable versioned snapshot only; canonical digest stored separately |
| Concurrency token | BIGINT | Aggregate version / ledger sequence; incremented atomically |
| Boolean flags | BOOLEAN | Only true binary facts; not lifecycle state |

Do not use floating point for quantity, currency, exchange rate, unit cost, average cost or allocation. Do not hardcode a precision such as NUMERIC(18,4) or NUMERIC(20,6) until the Catalog UOM and valuation precision policy is explicitly confirmed. The proposal deliberately leaves that decision open.

### 5.1 Nullability and sentinel values

- Required WarehouseId and VariantId are NOT NULL on all stock-bearing facts.
- LocationId may be NULL only where the explicit UNLOCATED bucket semantics apply; do not invent a Location row to represent unknown source location.
- LotId and StockUnitId are nullable only when Catalog tracking policy says they are not applicable.
- A nullable field must not mean multiple different states. Use explicit state/reason codes for unknown, not-applicable, pending or unresolved.
- Avoid empty UUIDs, zero UUID sentinels, empty strings and magic numeric values.
- For date ranges, use half-open intervals [valid_from, valid_to); only one active ownership interval may exist for a serial identifier.

## 6. Primary, alternate and idempotency keys

1. Every row has a stable UUID primary key.
2. Source-document uniqueness is enforced on the full scoped identity: source module + source document ID + source version + source line ID + movement purpose/type where contract requires one effect per source line.
3. EventId is globally unique within the Inventory event contract. IdempotencyKey is unique within its declared actor/tenant/module/operation scope (the exact scope must match the API command contract; no company/tenant dimension may be invented if the product is single-company).
4. Store a canonical payload hash with the idempotency record. Reuse with the same key and same canonical payload returns the prior result; same key with different payload is rejected and audited.
5. StockUnit identifier uniqueness is enforced by normalized identifier and accepted namespace rules. Active IMEI uniqueness is global across active StockUnits. One StockUnit may have two active IMEIs where the Catalog policy permits dual-SIM.
6. Historical identifier records remain. Reassignment requires retirement of the former active claim and an authorized, evidenced transition; a partial unique index or equivalent database constraint enforces one active owner under concurrency.
7. The exact namespace and normalization rules are owned/approved by Catalog and Security; this proposal does not invent them.
8. No unique constraint is defined from account code, name prefix, title or other inferred COA attribute.

## 7. Declarative constraints and indexes

The following are design requirements, not SQL syntax. PostgreSQL partial indexes, unique constraints, CHECK constraints, exclusion constraints and transactional validation may be selected after exact DDL review.

| Area | Required constraint/index behavior |
|---|---|
| Stock movement | Unique movement_id; unique event_id; unique scoped idempotency key |
| Movement lines | FK to movement; positive quantity magnitude; explicit direction/type; committed parent prevents line mutation |
| Movement source | Unique source identity per operation where one-time consumption is required |
| Stock position | Unique canonical stock-grain key; projection watermark cannot move backwards |
| Stock unit | Unique active normalized IMEI globally; unique active serial within approved namespace |
| Lot membership | At most one valid active Lot membership per StockUnit where policy requires; Variant compatibility |
| Reservation | Unique source reservation identity where contract requires; nonnegative remaining quantities |
| Allocation/pick | Allocated and picked quantities cannot exceed active reservation remaining; enforce under parent lock |
| Custody | Unique event identity; cumulative returned/consumed/resolved quantity cannot exceed handover |
| Transfer | Section transfer paired lines commit all-or-none; Warehouse total conserved |
| Stocktake | Unique observation ID; each observation has timestamp + watermark; recount adds a row |
| Adjustment | Unique authorization-to-movement link; adjustment bounded by accepted variance |
| Valuation | Unique source layer identity; allocations conserve source quantity/value; true-up source/version unique |
| Outbox | Unique event_id; aggregate version ordering; lease/claim state is retry-safe |
| Checkpoint | Monotonic checkpoint watermark for a projection and scope |
| Audit | Correlation/event reference unique as required by AuditSecurity contract; no duplicate canonical audit ownership |

### 7.1 Index strategy

Create only indexes justified by query plans and concurrency contracts. Candidate indexes:
- stock_movement_line (warehouse_id, variant_id, ledger_sequence) for ordered replay at the scope grain;
- stock_movement_line (movement_id) for aggregate retrieval;
- stock_movement_line (source_module, source_document_id, source_version, source_line_id) for source traceability;
- stock_position unique index on canonical grain and a read index beginning (warehouse_id, variant_id);
- unit_identifier unique partial index on normalized_value for active IMEI and a separate namespace-aware active serial index;
- reservation_line (variant_id, warehouse_id, state) for active reservation reads;
- custody_line (stock_unit_id, state) and (warehouse_id, variant_id, state) for outstanding custody;
- count_observation (count_line_id, observed_at_utc DESC) and (scope_key, ledger_watermark);
- valuation_layer (variant_id, warehouse_id, source_sequence) and valuation_allocation (target_movement_line_id);
- outbox_envelope (delivery_state, created_at_utc, outbox_id) for worker claims, plus unique event_id;
- foreign-key supporting indexes on all high-volume relationship columns.

Index ordering must be validated against actual query predicates and EXPLAIN (ANALYZE, BUFFERS) on representative datasets. Do not add redundant indexes or index every column. Avoid indexing mutable JSONB payloads.

## 8. Ledger sequencing and stock grain

The StockMovement ledger is append-only and ordered by a committed per-scope sequence for each Warehouse × Variant × applicable tracking grain required by Stage 05. The exact sequence partition key must be consistent with negative-stock locking and stocktake observation scope.

- Sequence assignment and movement commit occur in the same transaction.
- A sequence value must not become visible to a count watermark unless its corresponding movement committed.
- A rolled-back transaction must not create a committed logical sequence gap that can be mistaken for a movement.
- PostgreSQL sequences are non-transactional; therefore a plain nextval() is not sufficient as the authoritative gapless committed watermark. Use a locked scope-sequence row or another demonstrably transactional allocator.
- The allocator row is locked FOR UPDATE within the movement transaction; increment and movement insert commit atomically.
- Do not serialize unrelated Warehouses/Variants on one global counter.
- A projection checkpoint is a contiguous committed watermark, not the highest arbitrary ID observed.

The exact scope granularity must satisfy both: (a) one consistent order for count/replay and (b) one shared lock for Warehouse × Variant negative-stock thresholds across Sections. If finer tracking dimensions are needed for stocktake, their sequence/watermark protocol must still provide a consistent parent-scope snapshot.

## 9. Transaction boundaries

Each command must have one server-side transaction. The following are indivisible boundaries:

| Command | Atomic writes / locks | Required result |
|---|---|---|
| Receipt/issue/status movement | lock affected stock scope(s); allocate sequence(s); insert movement + lines; update position projection; persist outbox + audit correlation | All effects commit or none |
| Section transfer | lock source and destination stock scopes in deterministic key order; paired decrement/increment lines; update both positions; outbox/audit | Source and destination commit together; Warehouse total invariant |
| Custody handover/return/consumption/resolution | lock CustodyLine and stock scope; validate remaining custody; create event + linked movement; update stock/custody projection; outbox/audit | No custody/warehouse double count |
| Reservation create/allocate/release/expire | lock ReservationLine and relevant Warehouse × Variant stock scope; validate ATP and TTL against server time; update reservation/allocation and projection | Exactly-once quantity effect; expiry/issue race serialized |
| Pick | lock allocation/reservation line; validate remaining allocated quantity and stock state | Pick is subset; no physical issue until committed issue movement |
| Stocktake observation | consistent snapshot + scope watermark capture; persist immutable observation | Observation binds to exact ledger position |
| Stocktake adjustment | lock stock scope; verify approval and observation/variance; create adjustment + committed movement + projection/outbox/audit | Count itself never changes stock |
| Valuation allocation/TrueUp | lock valuation layer and target allocations; validate conservation and source version; persist immutable allocation batch | No over-allocation or duplicate TrueUp |
| Outbox delivery | claim rows using lease/skip-locked; external delivery outside long DB transaction; mark result idempotently | Retry does not duplicate consumer effect |

All transactions must use a documented isolation/locking strategy. Prefer short READ COMMITTED transactions with explicit row/advisory locks and version checks when sufficient. Use SERIALIZABLE only where invariant proof requires it and handle serialization failures with bounded retries. Never hold database locks while waiting for user input or remote network calls.

## 10. Concurrency and lock ordering

1. Define one canonical lock key order: (WarehouseId, VariantId, tracking-scope key, entity ID). Acquire multiple locks in sorted order to avoid deadlocks.
2. Negative-stock threshold validation is Warehouse × Variant across Sections; it must use the same parent lock for all affected Section operations.
3. For a serialized StockUnit, additionally lock the StockUnit row and active UnitIdentifier claim before changing physical location/custody.
4. For Custody, lock CustodyLine before checking cumulative handover/return/consumption/resolution.
5. For reservations, lock ReservationLine then the relevant stock scope; use one globally documented order if a command touches both.
6. For stocktake, capture observation watermark and corresponding replay-consistent balance in one database snapshot/lock protocol; never read a moving projection and a separately sampled watermark.
7. For transfers touching multiple scopes, sort all scope locks before acquiring any.
8. Every optimistic version update includes expected version in the WHERE predicate and verifies exactly one row changed. Zero rows means concurrency conflict, not success.
9. Deadlock and serialization errors are retried only for commands with stable idempotency identity and bounded retry policy. Validation/authorization errors are not retried.
10. Test races with multiple independent PostgreSQL sessions, not mocked repository calls.

## 11. StockPosition projection and replay

StockPosition is a cache/projection, never a ledger.

- Store its canonical grain key, quantity buckets, last applied committed scope sequence, projection schema/version and updated timestamp.
- Update the projection in the same transaction as the committed movement when synchronous consistency is required by the command.
- A projection worker may rebuild or catch up from ledger, but must be idempotent and checkpoint only contiguous committed sequences.
- Projection update must not silently skip an event. A missing sequence or invariant mismatch quarantines the affected scope and blocks operations whose correctness depends on it.
- Rebuild writes to a shadow projection/version, validates replay totals and atomically switches the active version; do not delete the only usable projection before validation.
- The ledger remains queryable/replayable independently of the projection.
- Warehouse totals are computed from mutually exclusive location buckets or UNLOCATED, never by summing both location detail and a duplicated warehouse aggregate.

## 12. Stocktake watermark implementation

For every CountObservation:
- persist observed_at_utc, scope key, committed ledger watermark, counted quantity, UOM/conversion version, counter/actor reference, evidence reference and observation version;
- capture watermark and the corresponding consistent database snapshot under the same protocol;
- expected-at-observation = replay of committed movement through that watermark;
- variance = observed quantity − expected-at-observation;
- at reconciliation cutoff C, replay only committed movements with sequence > observation watermark and committed by C;
- late/backdated business-effective events are exception records; do not reorder the ledger;
- recount inserts a new observation; prior observation remains immutable and auditable;
- adjustment is a separate authorized command and committed movement.

A global MAX(sequence) or PostgreSQL sequence value is not a valid watermark unless the transaction protocol proves committed-prefix semantics.

## 13. Idempotency, outbox and audit

- Persist StockMovement, projection change, outbox envelope and required audit correlation within one database transaction.
- Outbox payload contains event ID, contract version, aggregate identity/version, source identity, occurred/committed timestamps and digest; avoid unnecessary personal or sensitive data.
- Outbox delivery is at-least-once. Consumers deduplicate by EventId and enforce aggregate version ordering.
- Claim pending rows with a short lease/claim transaction using row locking (e.g. SKIP LOCKED); network delivery occurs after commit; completion/failure is recorded separately.
- A retry of a command with the same idempotency key and same payload returns the original result. A different payload fails closed.
- Shared AuditEvent belongs to AuditSecurity. Inventory records the required correlation and does not write directly into another module's private tables unless the approved audit integration contract explicitly permits it.
- Audit/outbox failure before transaction commit causes the stock movement transaction to roll back. External delivery failure after commit does not undo a committed movement; it triggers retry/alert and reconciliation.

## 14. Security and database privileges

No grants are issued by this design. Before implementation, define separate roles for:
- schema owner/migration principal (not used by runtime);
- Inventory application runtime (only required EXECUTE/CRUD through approved command boundary);
- read-only reporting principal with restricted views;
- outbox worker principal with narrow claim/update rights;
- backup/monitoring principal.

Runtime clients must not receive DDL, schema-owner, superuser, direct cross-module write, audit deletion or unrestricted table mutation privileges. Prefer stored command procedures only if consistent with the platform architecture; otherwise enforce writes through the server application and restrict direct DB access to the server. No database credential may be shipped to Windows clients.

Row-level security is not assumed as a substitute for application authorization. If used, session identity must be set by trusted server-side connection handling and protected from client spoofing/connection pooling leakage.

## 15. Numeric integrity, rounding and valuation

- Quantity arithmetic uses exact NUMERIC. Conversion uses the versioned Catalog conversion rule; precision and overflow are validated before commit.
- Unit conversion snapshot records source UOM, base UOM, conversion ID/version and exact factor. Reinterpretation under a later conversion version is prohibited.
- IRR final document amounts remain integer Rial. The accepted one-time rounding rule is applied at the final signed document net boundary, AwayFromZero, with maximum absolute difference 1 IRR/document. Do not round each row or impose a 100,000 IRR threshold.
- Intermediate valuation and allocation calculations retain sufficient exact precision. The final accounting boundary owns the rounding operation; the Inventory database must not independently round rows to force a tie-out.
- WAC for non-serialized stock and Specific Identification for serialized/IMEI stock remain as Stage 05/v3.0.3. FIFO is restricted to the permitted negative-stock settlement case.
- WAC denominator <= 0, invalid value/quantity states or untraceable cost source quarantine valuation and fail closed.
- Landed-cost eligibility and allocation basis follow the applicable accepted Accounting/Purchasing contract. This design does not create a cost category, FX source or GL account.
- Inventory in-transit has operational subledger evidence only. No GL account or Posting Rule is selected here.

## 16. Negative stock, reservations and TTL

- Negative-stock thresholds and eligible classes follow v3.0.3 D-258 exactly. Feature flag remains OFF until its separate gate.
- Warehouse × Variant is the threshold and concurrency scope, not each Section.
- Serial/IMEI/tracked items remain prohibited from negative stock.
- The final Sales reservation TTL remains OPEN/configurable under v3.0.3; no 30-minute, 24-hour or other default is adopted here. Expiry is evaluated using server time and must serialize against issue. Repair reservation has no automatic TTL.
- Reservation expiry is a workflow event and releases only unissued remaining quantity. A delayed cleanup job must not make an expired reservation count toward ATP after its exact ExpiresAt.
- Draft/Backorder states do not create a reservation unless the approved source contract explicitly says otherwise.

## 17. Failure, recovery and observability

- Any failure before commit rolls back movement, lines, projection, outbox and required audit correlation together.
- After commit, outbox delivery failure is retried; the stock movement remains committed.
- Projection mismatch, sequence discontinuity, duplicate active identifier, custody imbalance, stocktake watermark inconsistency, valuation non-conservation or failed replay creates a durable operational exception and blocks dependent commands.
- Log correlation IDs, EventId, MovementId, aggregate version, scope key, retry count and exception category. Do not log passwords, full recovery secrets, or unnecessary personal data.
- Backup/restore must preserve ledger, idempotency, outbox, sequence allocator, projection checkpoint and audit correlation consistency. Restore tests must verify replay and deduplication.
- Monitoring includes oldest pending outbox age, projection lag, scope quarantine count, deadlock/retry rate, failed idempotency conflicts, valuation reconciliation exceptions and stocktake replay exceptions.

## 18. Performance and scale posture

- R1 target is one on-prem PostgreSQL server and at most five Windows clients through the API. Start with ordinary B-tree indexes and normalized tables.
- Avoid speculative partitioning. Consider partitioning only after measured table growth and query/write profiles, and only if it preserves global uniqueness, replay order, FK strategy and retention/audit requirements.
- Do not use unlogged tables for authoritative inventory facts, idempotency records, outbox, audit evidence or sequence state.
- Bulk operations must chunk work while preserving source-level idempotency and atomicity requirements; do not partially commit a document whose business contract is atomic.
- Benchmark with representative high-volume movement, serial scans, concurrent reservation expiry/issue, stocktake observation and projection rebuild workloads.

## 19. Required physical-design review checklist

| ID | Review / proof required | Status |
|---|---|---|
| PD-01 | All columns mapped to logical entity and owner | PROPOSED |
| PD-02 | Exact Catalog UOM precision/scale and overflow bounds | OPEN |
| PD-03 | Exact valuation precision and integer IRR overflow proof | OPEN |
| PD-04 | Platform UUID generator/version policy | OPEN |
| PD-05 | Source-module ID/version contracts | OPEN |
| PD-06 | IMEI/serial namespace and normalization contract | OPEN |
| PD-07 | Per-scope committed-prefix ledger allocator design | PROPOSED; PostgreSQL proof required |
| PD-08 | Lock scope and global lock ordering tested | PROPOSED; race tests required |
| PD-09 | Index/query plans measured on representative data | OPEN |
| PD-10 | Stocktake consistent-snapshot/watermark implementation proof | OPEN |
| PD-11 | Projection rebuild/cutover and replay proof | OPEN |
| PD-12 | Outbox/audit atomicity and consumer idempotency proof | OPEN |
| PD-13 | DB role/grant matrix and least privilege review | OPEN |
| PD-14 | Backup/restore including ledger, outbox and checkpoints | OPEN |
| PD-15 | Exact-SHA PostgreSQL integration and concurrency suite | NOT RUN |
| PD-16 | Security, privacy and operational review | OPEN |

## 20. Test plan and acceptance evidence

This design requires actual PostgreSQL 18 tests, not SQLite substitutes or mocked repositories. Tests must run on a disposable isolated database with a recorded migration/schema SHA when implementation is later authorized.

Required suites:
- constraint tests: positive quantity, exact numeric boundaries, active serial/IMEI uniqueness, source/idempotency uniqueness, FK and ownership boundaries;
- ledger tests: immutable committed facts, deterministic replay, transactional scope sequence, rollback behavior and reversal bounds;
- concurrency tests: same Warehouse×Variant negative-stock lock across Sections, two simultaneous reservations/issues, expiry-vs-issue, dual-SIM identifier conflict, Custody return-vs-consume, Section transfer and stocktake watermark race;
- atomicity tests: forced failure at movement, projection, outbox and audit persistence points proves all-or-nothing commit;
- projection tests: checkpoint contiguous prefix, replay, shadow rebuild, cutover and quarantine;
- stocktake tests: each observation watermark, movement before/after observation, late/backdated event exception, recount lineage and authorized adjustment;
- valuation tests: WAC denominator, source lineage, partial issue TrueUp, residual conservation, duplicate source version and no unauthorized GL posting;
- recovery tests: backup/restore and post-restore replay/idempotency/outbox consistency;
- security tests: runtime principal cannot DDL, mutate committed ledger, write other modules, delete audit evidence or bypass API authorization.

No tests are marked PASS by this document. Exact-SHA evidence must include PostgreSQL version, build SHA, schema/migration SHA, test IDs, command/environment, results, logs and reviewer disposition.

## 21. Open dependencies and release boundary

| Dependency | Status |
|---|---|
| Architecture baseline v3.0.3 | OWNER-ACCEPTED; sole authority |
| Stage 05 inventory semantics | Design contract |
| Logical ERD v0.2 | Current logical design proposal; review status as recorded in its header |
| Exact quantity precision and conversion | OPEN pending Catalog/platform numeric contract |
| Exact intermediate valuation precision | OPEN pending Accounting/Platform decision |
| UUID generation/version | OPEN pending platform policy |
| Serial namespace/normalization | OPEN pending Catalog/Security contract |
| Ledger committed-prefix sequencing proof | OPEN pending PostgreSQL prototype/test |
| Lock ordering and concurrency proof | OPEN pending PostgreSQL test |
| O-35 | OPEN pending exact-SHA PostgreSQL watermark/race/replay/tie-out evidence |
| FE-31 inter-Warehouse transfer | R2 / NOT ENABLED |
| Negative-stock feature | OFF pending separate gate |
| Sales TTL final value | OPEN/configurable; no default invented |
| Repair reservation TTL | No automatic TTL |
| PostgreSQL DDL / migration / API / runtime | NOT AUTHORIZED by this document |
| Posting Rules / Posting Release / production migration / Go-Live | NOT AUTHORIZED |

Acceptance of this proposal can authorize a separately scoped DDL/migration implementation plan only after all applicable OPEN items are resolved or explicitly accepted as bounded implementation prerequisites. It does not itself authorize SQL execution, migration creation, runtime feature activation or release.

**END — MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001 v0.1**


## Current Architecture Master authority — 2026-11-24

The Product Owner has designated **MDG-ARCH-MASTER-001 v3.0.4** as the sole current, accepted, official and governing Architecture/Accounting Master. The v3.0.3 references in this dated review/evidence artifact identify the baseline actually reviewed or used when the artifact was produced; they are retained as historical provenance and are not silently rewritten. For current design decisions, v3.0.4 governs. Any unresolved section-level conflict or design dependency must be re-audited against v3.0.4 before implementation. This authority notice does not upgrade this artifact's review type, evidence strength, gate status or acceptance.
