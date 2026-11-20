# MDG Inventory PostgreSQL Physical Design — Residual Blocker Closure

| Field | Value |
|---|---|
| Document ID | MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001 |
| Version | 0.3 |
| Status | CORRECTED DESIGN — PD-AUD-01..12 CLOSED IN DESIGN — IMPLEMENTATION NOT AUTHORIZED |
| Sole architecture baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Predecessor | v0.2, read together with v0.1 |
| Logical model | MDG-INVENTORY-LOGICAL-ERD-001 v0.2 |
| Governing semantics | MDG-INVENTORY-STAGE-05-EQUATIONS-CUSTODY-STOCKTAKE-001 v0.1 |
| Target DBMS | PostgreSQL 18 |

## 1. Authority and effect

This v0.3 supplement closes the six residual findings recorded by re-audit 002. It is read with v0.1 and v0.2; for PD-AUD-07..12 it supersedes conflicting or incomplete earlier wording. v3.0.3 remains the sole accepted architecture baseline. Stage 05 and logical ERD v0.2 remain the governing Inventory semantics.

Design closure is not implementation or evidence closure. No schema, SQL, DDL, migration, database grant, EF mapping, API, worker, PostgreSQL test, O-35 closure, Posting Release, production migration or Go-Live is authorized.

## 2. PD-AUD-07 — Projection generation, rebuild, catch-up and cutover

### 2.1 Authority model

StockMovement, committed lines and scope entries remain the only authoritative physical quantity facts. StockPosition and every other Inventory read projection are derived and rebuildable. A projection generation is identified by ProjectionName, ScopeKey, GenerationId, SourceContractVersion, BuildStartedAtUtc, StartWatermark, AppliedThroughWatermark, GenerationState and reconciliation digest. A generation is not visible to ordinary readers until its active pointer is switched.

For one Warehouse × Variant scope, watermark is the contiguous committed ledger sequence from the canonical scope allocator in v0.2. A projection spanning multiple parent scopes uses a vector keyed by (WarehouseId, VariantId); no global scalar watermark or globally simultaneous snapshot is claimed.

### 2.2 Build and catch-up

1. Create a new generation in BUILDING state, uniquely identified by projection and scope. The existing active generation remains untouched.
2. Under the canonical Warehouse × Variant parent lock, read and persist committed watermark W0 in a fresh READ COMMITTED statement. Release the lock immediately; never hold a stock lock during bulk build.
3. Deterministically rebuild the shadow generation from opening state and committed movement facts through W0. The source must be an internally consistent committed prefix; timestamps, UUID order, MAX(id) and non-transactional sequences are not watermarks.
4. Persist AppliedThroughWatermark=W0. Replaying a source sequence into the same generation is idempotent, enforced by a unique generation/scope/sequence application identity.
5. Catch up only the next contiguous entries AppliedThroughWatermark+1 onward. A gap, conflicting duplicate digest, missing source fact, invalid contract version or conservation failure quarantines the generation; never skip ahead.
6. Measure lag as current committed scope watermark minus AppliedThroughWatermark.

### 2.3 Atomic cutover

Cutover is per projection scope, not a global cross-warehouse event.

Before attempting cutover, the builder continues contiguous background catch-up outside the stock-scope lock until the candidate generation is close to the latest committed prefix. The remaining locked tail must fit a separately configured and load-tested cutover budget; no numeric budget is invented in this document. If the candidate is not within that budget, cutover is deferred and catch-up continues without blocking stock writers.

1. Acquire the same Warehouse × Variant lock used by movement writers.
2. Read current committed watermark Wc after lock acquisition in a fresh READ COMMITTED statement.
3. While retaining the lock, apply only the remaining bounded contiguous tail through Wc and verify the generation is exactly caught up. If the remaining tail exceeds the approved cutover budget or cannot be applied within the bounded cutover transaction, roll back the cutover attempt, leave the active pointer unchanged, release the lock, resume background catch-up and retry later; never hold the stock lock while processing an unbounded backlog.
4. Verify contract version, source completeness, row/digest reconciliation, quantity equations and conservation at Wc. Any unresolved exception blocks cutover.
5. In one short transaction, atomically set the prior generation to RETIRED/ROLLBACK_CANDIDATE, set the new generation ACTIVE and update the single active-generation pointer for ProjectionName + ScopeKey.
6. Release the lock. Readers resolve the active pointer once per read transaction and never mix generations.
7. Multi-scope projections switch each scope independently and retain a vector of generation/watermark values. A consumer requiring coherent multi-scope data must retain that vector.

Normal writers synchronously update only the active generation. They do not dual-write every shadow generation. The builder/catch-up worker alone writes BUILDING generations.

### 2.4 Failure and rollback

Failed build/catch-up/cutover leaves the prior active pointer unchanged. Candidate generation is quarantined with immutable failure evidence, source watermark, error category and correlation reference. Retry resumes only from its last verified contiguous watermark or starts a new generation; source ledger facts are never edited.

Rollback is permitted only after the prior generation has been caught up and reconciled to the current watermark under the same locked cutover protocol. Pointer reversal is atomic. If equivalence cannot be proven, rollback is prohibited and a fresh rebuild is required. Retention duration is governed by platform records policy, not invented here.

### 2.5 Proof required

PD-T-016..020: deterministic rebuild; concurrent writer catch-up; contiguous replay; atomic pointer cutover; reader generation consistency; failure isolation; rollback after catch-up; multi-scope vector behavior. Projection work must never mutate StockMovement, Reservation, Custody, valuation layers or Accounting facts.

## 3. PD-AUD-08 — Canonical StockPosition identity and NULL normalization

### 3.1 Full stock grain

StockPosition logical identity is the full Stage 05 grain:

(WarehouseId, VariantId, Section/Location scope, StockStatus, Lot applicability/identity, StockUnit applicability/identity, BaseUomId, UomConversionVersion).

Any additional Catalog tracking dimension must be added explicitly to the key and uniqueness rule before DDL. Section/location detail never changes the parent Warehouse × Variant lock/sequence scope.

### 3.2 Explicit applicability states

- WarehouseId, VariantId, StockStatus, BaseUomId, UomConversionVersion and optional-dimension state codes are NOT NULL.
- A real Section/Location uses its owning ID. NULL SectionId is permitted only for the explicit UNLOCATED/unknown-source bucket; it is not a fabricated Section row or a generic missing value. If warehouse-level unsectioned stock differs from unknown source location, use distinct non-null scope-kind codes.
- LotTrackingState is NOT_TRACKED or TRACKED. NOT_TRACKED requires LotId=NULL; TRACKED requires LotId non-NULL and Variant-compatible.
- UnitTrackingState is NOT_SERIALIZED or SERIALIZED. NOT_SERIALIZED requires StockUnitId=NULL; SERIALIZED requires StockUnitId non-NULL and valid active identity. Serialized position is one StockUnit identity; aggregate serialized quantity is derived.
- Applicability codes are domain states, not sentinel UUIDs. Empty/zero UUIDs, empty strings and magic numbers are prohibited.

### 3.3 Database uniqueness

Use PostgreSQL 18 UNIQUE NULLS NOT DISTINCT over the complete canonical grain, including applicability/scope-kind codes, so equal NULL dimensions cannot create duplicate logical rows. If a single constraint is not used, an equivalent collision-proof, versioned normalized key with database-enforced uniqueness is mandatory. Application check-then-insert is insufficient.

Position identity is immutable. Scope, Lot, StockUnit, BaseUom or conversion-version changes produce a different derived grain through movement; they do not rewrite historical keys. Zero-balance compaction, if ever used, must be deterministic and replay-safe.

### 3.4 Proof required

PD-T-021..023: concurrent duplicate NULL-grain insert; all applicability combinations; serialized identity uniqueness; Section/UNLOCATED separation; UOM-version separation; rebuild key stability.

## 4. PD-AUD-09 — Draft/Commit header-line boundary

Draft header/lines are workflow state only. They may be edited or deleted only while DRAFT and before commit. Draft rows never contribute to OnHand, Sellable, ATP, custody, valuation or GL/subledger.

The draft header carries AggregateVersion BIGINT NOT NULL. Every mutation supplies expected version and increments it atomically. Stale versions fail with conflict and no child mutation. Child mutations lock the parent first and follow v0.2 global lock order.

Commit validates current version, authorization, source identity, movement type/state, complete header, all lines, positive exact quantity magnitude, explicit direction, base-UOM conversion snapshot, tracking applicability, scope locks/sequences, conservation, reservation/custody/transfer bounds and reversal links. Receipt, committed header, lines, scope entries, projections and outboxes commit in one transaction through the restricted v0.2 database commit boundary.

A physical StockMovement that changes quantity MUST contain at least one valid movement line. Zero-line or incomplete physical headers cannot commit. A legitimate business event with no physical quantity effect must not create a synthetic empty StockMovement; it remains a source/workflow event in its owning module.

A transaction-end deferred constraint or equivalent database-enforced validation rejects committed header without required lines/scope entries. Runtime cannot bypass via direct table DML. Draft and committed facts have separate privilege paths.

Proof: PD-T-024..027 for stale-version races, line-vs-commit race, empty movement rejection, atomic rollback, duplicate commit retry and exclusion of draft rows from projections.

## 5. PD-AUD-10 — Reservation expiry persistence and equality boundary

Every reservation persists ExpiryMode, ExpiryPolicyCode, ExpiryPolicyVersion and CreatedAtUtc. FINITE requires ExpiresAtUtc NOT NULL, computed by trusted server from the snapshotted policy. NONE requires ExpiresAtUtc NULL. Client timestamps cannot extend or shorten expiry without authorized versioned amendment.

Sales TTL remains OPEN/configurable under v3.0.3; no duration/default is chosen here. Repair reservation has no automatic TTL and uses ExpiryMode=NONE unless a future accepted decision changes it. No 30-minute or 24-hour value is adopted.

All comparisons use trusted server/database UTC captured after Reservation aggregate lock acquisition:
- Active iff now_utc < ExpiresAtUtc.
- Expired iff now_utc >= ExpiresAtUtc.
- Equality is EXPIRED.

A request waiting for the aggregate lock must evaluate time after acquiring it. Expiry is an explicit idempotent state transition, not a read-time side effect or silent subtraction. The transition records ExpiredAtUtc, policy/version, released remaining quantity, principal, reason and stable expiry-instance identity. Repeated delivery cannot release twice.

If renewal/amendment is permitted by source contract, it creates a new version and expiry-instance identity with authorization/audit; it never overwrites history. Issue and expiry serialize on Reservation aggregate: if now >= expiry at the winning decision point, expiry wins and issue is rejected; otherwise a valid pre-expiry issue commits and only remaining quantity may later expire.

Proof: PD-T-028..031 for before/equal/after boundary, lock wait crossing expiry, issue/expiry race, worker retry, policy version and no Repair TTL.

## 6. PD-AUD-11 — Outbox operations, ownership and observability

Inventory owns its local domain/audit-intent outboxes. AuditSecurity exclusively owns canonical AuditEvent ingestion, storage, retention and access. Inventory Operations owns local queue and worker health; AuditSecurity owns ingestion availability, canonical deduplication, acknowledgement and downstream retention. Incident ownership follows the failing boundary. No event is silently discarded or manually edited.

v0.2 states remain authoritative: PENDING, LEASED, RETRYABLE_FAILURE, DELIVERED_ACKNOWLEDGED, QUARANTINED. Each claim has LeaseOwner, LeaseStartedAtUtc and LeaseExpiresAtUtc; expired leases are reclaimable. Durable metadata includes attempts, next-attempt time, last attempt, error category and acknowledgement reference. Envelope and digest are immutable.

Retry uses exponential backoff with jitter and bounded per-attempt timeout. No terminal discard state exists. Exhausted automatic attempts enter QUARANTINED, preserving envelope/history and raising alert. Re-drive requires authorized operator, incident/correlation reference and audit event; it reuses same EventId and cannot create another business effect.

Initial R1 operational defaults, configuration-driven and not embedded in code:
- Worker poll target: 30 seconds.
- Oldest PENDING/RETRYABLE age: warning 5 minutes; critical 15 minutes.
- Queue depth: warning 1,000; critical 5,000.
- Any QUARANTINED item: immediate critical alert.
- Lease expiry not reclaimed by next poll: warning; repeated reclaim failure: critical.
- Daily reconciliation of local EventIds against AuditSecurity durable acknowledgements; immediate reconciliation after restore, prolonged outage or incident.

These are operational defaults, not legal retention periods or remote-availability guarantees. Changes require versioned configuration/change control. Raising thresholds cannot suppress quarantine alert or delete evidence.

Metrics: pending count, oldest age, retryable/leased/quarantined counts, expired leases, attempts, acknowledgement latency, failure category, last successful delivery/reconciliation, mismatch/duplicate count and restore backlog. Daily reconciliation classifies each EventId as acknowledged, pending, quarantined, missing remotely or remote duplicate. Mismatch is an incident, not an automatic delete. Backup/restore resumes pending delivery with same EventIds.

Proof: PD-T-032..035 for worker crash/lease recovery, ambiguous remote commit, quarantine/re-drive, alert thresholds, reconciliation, restore and payload immutability.

## 7. PD-AUD-12 — Canonical naming and nullability data dictionary

### 7.1 Naming

Physical identifiers use lowercase snake_case. Primary keys use singular entity name + _id; foreign references use exact owning entity _id; timestamps use _at_utc; business dates use _date; versions use _version; quantities use _qty; IRR values identify _irr; booleans use is_/has_; state/type/reason values use stable _code names, never localized labels.

Avoid unqualified date, time, status, type, value, amount, quantity, source_id or version. Do not use SQL reserved words. Display labels remain localized and separate from storage identifiers.

### 7.2 Common requiredness

- Primary keys: UUID NOT NULL.
- Required owner-contract foreign keys: NOT NULL and type-matched.
- AggregateVersion and contract/schema versions: NOT NULL, positive.
- CreatedAtUtc and committed timestamps: NOT NULL, server-generated.
- State/type/reason codes: NOT NULL once lifecycle state exists.
- ActorId: NOT NULL for user/system mutations, using typed system principal for workers; never magic UUID.
- IdempotencyKey and canonical request digest: NOT NULL for mutating commands.
- EventId, contract version and payload digest: NOT NULL for outbox.
- ExpiresAtUtc: NULL iff ExpiryMode=NONE; otherwise NOT NULL.
- Optional external reference may be NULL only if the source event genuinely has none and applicability is explicit.
- Optional evidence reference may be NULL before evidence is required; transition validates evidence at the point it becomes mandatory.
- Empty string, zero UUID, magic number and sentinel timestamp are prohibited.

### 7.3 Entity applicability matrix

| Entity/field | Required rule |
|---|---|
| StockMovement.warehouse_id, variant_id | NOT NULL for physical movement |
| StockMovementLine.quantity_magnitude, base_uom_id, direction_code | NOT NULL; quantity > 0 |
| StockPosition.section_id | NULL only with explicit UNLOCATED scope kind |
| StockPosition.lot_id | NULL iff LotTrackingState=NOT_TRACKED |
| StockPosition.stock_unit_id | NULL iff UnitTrackingState=NOT_SERIALIZED |
| StockPosition.base_uom_id, uom_conversion_version | NOT NULL; part of grain |
| Reservation.expires_at_utc | NOT NULL iff ExpiryMode=FINITE; otherwise NULL |
| Reservation.expiry_policy_code/version | Required for finite expiry; explicit NONE policy otherwise |
| CountObservation.observed_at_utc and per-scope watermark | NOT NULL |
| ValuationLayer source identity and method/version | NOT NULL |
| Outbox EventId, contract version, digest, created_at_utc | NOT NULL and immutable |
| Outbox lease fields | all NULL outside LEASED; all required while LEASED |
| Outbox delivered_at_utc and acknowledgement | required iff DELIVERED_ACKNOWLEDGED |
| Outbox quarantine metadata | required iff QUARANTINED |
| Projection generation start/applied watermark | NOT NULL; applied watermark is verified contiguous prefix |
| Active-generation pointer | exactly one active generation per projection scope |

Every nullable column must document reason, lifecycle and legal state combinations. CHECK/unique/FK constraints and transactional commands make invalid combinations unrepresentable or fail closed. NULL must not simultaneously mean unknown, not-applicable, pending and deleted. ID types, UUID generation, quantity precision, identifier normalization and cross-module FK guarantees remain owned by their source contracts.

Proof: PD-T-036..039 for schema naming lint, nullability/state matrix, sentinel scan and owner-contract compatibility.

## 8. Integrated finding disposition

| Finding | v0.3 disposition | Remaining proof |
|---|---|---|
| PD-AUD-07 projection rebuild/cutover | CLOSED IN DESIGN | PostgreSQL build/catch-up/cutover/rollback tests |
| PD-AUD-08 StockPosition identity/nulls | CLOSED IN DESIGN | Unique NULL-grain and applicability tests |
| PD-AUD-09 Draft/Commit boundary | CLOSED IN DESIGN | Version, deferred completeness and privilege tests |
| PD-AUD-10 Reservation expiry | CLOSED IN DESIGN | UTC boundary and lock-wait race tests |
| PD-AUD-11 Outbox observability | CLOSED IN DESIGN | Worker, alert, reconciliation and restore tests |
| PD-AUD-12 Naming/nullability | CLOSED IN DESIGN | Schema lint and constraint tests |

Together with PD-AUD-01..06 closed by v0.2, all PD-AUD-01..12 are CLOSED IN DESIGN. This is not implementation or runtime evidence.

## 9. Expanded test catalogue

All are specifications only and NOT RUN.

| Test ID | Scenario | Required assertion |
|---|---|---|
| PD-T-016 | Shadow generation build at W0 | Deterministic committed-prefix replay |
| PD-T-017 | Writers during build/catch-up/cutover | Contiguous catch-up; no missing/duplicate sequence; oversized cutover tail aborts without pointer switch and retries after background catch-up |
| PD-T-018 | Atomic generation cutover | Pointer/state change together; reader sees one generation |
| PD-T-019 | Failed build/cutover | Active generation unchanged; candidate quarantined |
| PD-T-020 | Rollback/multi-scope projection | Old generation caught up; vector watermarks preserved |
| PD-T-021 | Concurrent duplicate NULL-grain insert | Exactly one canonical position |
| PD-T-022 | Applicability matrix | Invalid Lot/StockUnit/Section combinations rejected |
| PD-T-023 | Grain key immutability | Rebuild produces same canonical key |
| PD-T-024 | Draft optimistic concurrency | Stale AggregateVersion cannot mutate/commit |
| PD-T-025 | Draft line vs commit race | Parent lock; all-or-none result |
| PD-T-026 | Empty/incomplete movement | Commit rejected; no ledger/projection effect |
| PD-T-027 | Draft visibility | Draft never affects quantity/valuation projections |
| PD-T-028 | Expiry boundary | Before active; equality/after expired |
| PD-T-029 | Lock wait crosses expiry | Decision uses server UTC after lock |
| PD-T-030 | Issue vs expiry | One serialized valid outcome; no over-issue |
| PD-T-031 | Expiry retry/policy version | One release; no automatic Repair expiry |
| PD-T-032 | Worker crash/lease expiry | Lease reclaim; same EventId |
| PD-T-033 | Remote commit/lost acknowledgement | Retry deduplicates canonical event |
| PD-T-034 | Alert thresholds/quarantine | Alerts fire and remain observable |
| PD-T-035 | Reconciliation/restore | Mismatch classified; queue resumes idempotently |
| PD-T-036 | Naming lint | Identifiers comply |
| PD-T-037 | Nullability/state matrix | Illegal combinations rejected |
| PD-T-038 | Sentinel scan | No magic sentinel identifiers |
| PD-T-039 | Owner-contract compatibility | IDs/types/requiredness match source contracts |

Together with PD-T-001..015 in v0.2, design catalogue is PD-T-001..039. None is runtime PASS evidence.

## 10. Remaining dependencies and gates

- AuditSecurity must publish/accept ingestion authentication, EventId deduplication/acknowledgement, retention/privacy and incident contract.
- Catalog must publish quantity precision/base UOM/conversion, tracking and identifier normalization contracts.
- Platform must publish UUID generation, DB role/migration ownership, worker identity, configuration/secrets and alert routing.
- PostgreSQL implementation must prove constraints, function privileges, watermark, projection concurrency, reservation expiry, outbox delivery, backup/restore and performance on exact SHA.
- Independent reviewer must review exact document SHA and implementation evidence.
- O-35 remains OPEN until exact-SHA PostgreSQL watermark/race/replay/tie-out evidence is accepted.
- DDL/migration freeze requires these prerequisites and separate authorization.

## 11. Governing semantics preserved

ATP = Sellable − EffectiveReserved. Custody is not Reservation and is excluded from ATP. FE-31 remains R2 / NOT ENABLED. Sales TTL remains OPEN/configurable; no duration is selected. Repair reservation has no automatic TTL. D-258 negative-stock limits and feature OFF remain unchanged. WAC/Specific Identification and permitted shortage treatment remain unchanged. Exact NUMERIC/no floats. IRR base is 1 Rial; integer final amount; one-time document-total AwayFromZero; maximum absolute difference 1 Rial/document; no 100,000 IRR threshold and no row-level rounding. 110801 remains sole Inventory GL control; no COA/Posting inference. The abandoned nine-sheet COA workbook is not an authority or dependency.

## 12. Final status and non-authorization

**PD-AUD-01..12: CLOSED IN DESIGN.** The physical design is suitable for independent technical review, but not implementation freeze until dependencies and exact-SHA PostgreSQL evidence are satisfied.

No SQL, DDL, EF mapping, migration, grants, runtime code, PostgreSQL execution, Posting Rule, Posting Release, production migration or Go-Live is authorized. O-35 remains OPEN. PD-T-001..039 are NOT RUN.

**END — MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001 v0.3**
