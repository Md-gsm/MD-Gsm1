# MDG Inventory PostgreSQL Physical Design — Warehouse Watermark and Cutover Guard Successor

| Field | Value |
|---|---|
| Document ID | MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001 |
| Version | 0.4 |
| Status | CONTROLLED SUCCESSOR DESIGN — OWNER AMENDMENTS INCORPORATED — INDEPENDENT REVIEW PENDING — IMPLEMENTATION NOT AUTHORIZED |
| Sole architecture baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Predecessor | v0.3; v0.1-v0.3 retained as historical predecessors |
| Logical model | MDG-INVENTORY-LOGICAL-ERD-001 v0.3 |
| Governing semantics | MDG-INVENTORY-STAGE-05-EQUATIONS-CUSTODY-STOCKTAKE-001 v0.1 |
| Target DBMS | PostgreSQL 18 |

## 1. Authority and effect

This v0.4 successor consolidates v0.1-v0.3 and applies the owner decisions in OWN-INV-019 v1.0. It supersedes the v0.3 Warehouse×Variant watermark allocator, open Sales TTL wording and retry-exhaustion quarantine behavior where they conflict with the owner-approved rules. PD-AUD-01..12 remain closed only at the internal design level; this v0.4 successor requires independent exact-SHA review. v3.0.3 remains the sole accepted architecture baseline pending controlled baseline crosswalk/reconciliation. Stage 05 and logical ERD v0.3 govern Inventory semantics.

Design closure is not implementation or evidence closure. No schema, SQL, DDL, migration, database grant, EF mapping, API, worker, PostgreSQL test, O-35 closure, Posting Release, production migration or Go-Live is authorized.

## 2. PD-AUD-07 — Projection generation, rebuild, catch-up and cutover

### 2.1 Authority model

StockMovement, committed lines and scope entries remain the only authoritative physical quantity facts. StockPosition and every other Inventory read projection are derived and rebuildable. A projection generation is identified by ProjectionName, ScopeKey, GenerationId, SourceContractVersion, BuildStartedAtUtc, StartWatermark, AppliedThroughWatermark, GenerationState and reconciliation digest. A generation is not visible to ordinary readers until its active pointer is switched.

For every WarehouseId, the authoritative source cursor is the contiguous committed Warehouse sequence WarehouseCommitSequence. The sequence is not allocated independently per Variant. A movement receives one movement-level sequence entry per affected Warehouse; a multi-line or multi-Variant movement within one Warehouse receives one entry for that Warehouse. A multi-Warehouse transaction creates one entry in each affected Warehouse atomically.

A projection may be Warehouse×Variant or finer-grained, but its source cursor is Warehouse-wide. It stores AppliedWarehouseSequence per Warehouse. A multi-Warehouse projection retains a WarehouseId→sequence vector; no global scalar watermark or globally simultaneous multi-Warehouse snapshot is claimed. A source sequence for another Variant is consumed as a verified no-op by a narrower projection and advances that Warehouse cursor.

### 2.2 Build and catch-up

1. Create a new generation in BUILDING state, uniquely identified by projection and scope. The existing active generation remains untouched.
2. For each Warehouse represented by the projection scope, capture the current committed Warehouse watermark under its counter-row lock in a fresh READ COMMITTED statement. For multiple Warehouses, acquire counters in ascending WarehouseId order, capture the vector, and release promptly. Never hold a stock-scope lock during bulk build.
3. Deterministically rebuild the shadow generation from opening state and committed movement facts through the captured per-Warehouse watermarks. Timestamps, UUID order, MAX(id) and non-transactional sequences are not watermarks.
4. Persist the captured AppliedWarehouseSequence for each Warehouse. Replaying a source sequence into the same generation is idempotent, enforced by a unique generation/Warehouse/sequence application identity.
5. Catch up only the next contiguous Warehouse sequence. A gap, conflicting duplicate digest, missing source fact, invalid contract version or conservation failure quarantines the generation; never skip ahead. A sequence for another Variant is a verified no-op for a narrower projection, not a gap.
6. Measure lag separately per Warehouse as current committed Warehouse watermark minus the generation's AppliedWarehouseSequence. For a multi-Warehouse projection, retain the full vector.



### 2.3 Atomic cutover

Cutover is performed per projection scope. Its source cursors and consistency checks are Warehouse-wide. A multi-Warehouse projection uses a WarehouseId→watermark vector; this protocol does not claim a single global instant across Warehouses.

Two independent, versioned cutover budgets are mandatory:

- MAX_CUTOVER_TAIL_COUNT: maximum unprocessed movement-level Warehouse sequence entries remaining at the final cutover stage. A multi-Warehouse attempt measures the total remaining entries across all affected Warehouses.
- MAX_CUTOVER_LOCK_DURATION: maximum elapsed duration from acquisition of the first required Warehouse counter lock until the cutover transaction commits or rolls back and releases its locks. It includes final Tail processing, reconciliation and Pointer transaction.

Both numeric values are NOT_SET until representative PostgreSQL 18 and target-hardware load/rollback tests establish them. The configuration is versioned and auditable; no guessed default is permitted. The benchmark envelope must match the target data volume, Warehouse count, movement distribution, concurrent writer profile, PostgreSQL configuration and server hardware.

Before attempting cutover, the builder performs background contiguous Catch-up outside the counter locks. A preflight estimate must show that the remaining Tail can satisfy both budgets. If the estimate exceeds either budget, defer cutover and continue Catch-up without taking the cutover locks.

1. Acquire the relevant WarehouseCommitSequence counter row(s), in ascending WarehouseId order. Movement writers allocate their sequence under these same counter locks before commit; retaining the locks creates a stable committed prefix for the cutover attempt.
2. Read each current committed Warehouse watermark Wc after acquiring its counter lock in a fresh READ COMMITTED statement. Calculate the remaining Tail count as the number of unprocessed movement-level Warehouse sequence entries from each generation's applied cursor through Wc.
3. If the total Tail count exceeds MAX_CUTOVER_TAIL_COUNT, roll back the attempt, preserve the previous active Pointer and resume background Catch-up.
4. Apply every committed movement sequence through Wc, in contiguous order. No pre-cutover movement may remain unapplied. A sequence for an unrelated Variant is a verified no-op for a narrower projection and advances its Warehouse cursor.
5. Reconcile the candidate against the authoritative Ledger at Wc. For every affected Warehouse, applied watermark difference must be exactly zero. At every canonical stock grain, quantity difference between candidate Projection and Ledger replay must be exactly zero. Missing source, invalid version, row/digest discrepancy, quantity discrepancy or watermark discrepancy blocks cutover.
6. Measure elapsed lock duration using a trusted server-side monotonic timer, not the client clock. If the measured duration reaches/exceeds MAX_CUTOVER_LOCK_DURATION before the Pointer transaction can commit, abort and roll back the whole attempt. A cutover attempt may proceed only while both count and duration limits remain satisfied.
7. Only after all preconditions pass, atomically mark the prior generation RETIRED/ROLLBACK_CANDIDATE, mark the new generation ACTIVE and switch the single active-generation Pointer for ProjectionName + ScopeKey. Commit and release all counter locks.
8. Readers resolve the active Pointer once per read transaction and never mix generations. A consumer requiring coherent multi-Warehouse data retains the complete WarehouseId→generation/watermark vector.

**Mandatory fail-closed rule:** Pointer Swap is prohibited if either budget is exceeded, any pre-cutover movement remains unprocessed, any quantity difference is non-zero, or any Warehouse watermark difference is non-zero. On every failed precondition, roll back the attempt, leave the prior Pointer unchanged, release locks and continue background Catch-up. The prior generation remains the only reader-visible generation.

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

Any additional Catalog tracking dimension must be added explicitly to the key and uniqueness rule before DDL. Section/location detail never changes the parent Warehouse × Variant shortage lock scope; commit sequence and projection/stocktake cursor scope is WarehouseId-wide.

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

Sales confirmed-reservation TTL defaults to 24 hours from server-confirmed creation, is configurable/versioned and is snapshotted per Reservation; configuration changes apply to new reservations only. Repair reservation has no automatic TTL and uses ExpiryMode=NONE. Equality at ExpiresAtUtc is expired.

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

Retry uses exponential backoff with jitter and bounded per-attempt timeout. REQUIRES_ATTENTION is a non-terminal alert flag: it does not stop retry, delete the envelope or change the delivery state. Exhausting an automatic retry schedule raises/retains REQUIRES_ATTENTION and retry continues under controlled policy. QUARANTINED is reserved for integrity failure, invalid/unsupported contract, security failure or explicitly non-retryable safety conditions. Re-drive requires an authorized operator, incident/correlation reference and Audit event; it reuses the same EventId/Hash and cannot create another business effect.

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

ATP = Sellable − EffectiveReserved. Custody is not Reservation and is excluded from ATP. FE-31 remains R2 / NOT ENABLED. Sales confirmed-reservation TTL defaults to 24 hours, configurable/versioned and snapshotted per reservation; Repair reservation has no automatic TTL. D-258 negative-stock limits and feature OFF remain unchanged. WAC/Specific Identification and permitted shortage treatment remain unchanged. Exact NUMERIC/no floats. IRR base is 1 Rial; integer final amount; one-time document-total AwayFromZero; maximum absolute difference 1 Rial/document; no 100,000 IRR threshold and no row-level rounding. 110801 remains sole Inventory GL control; no COA/Posting inference. The abandoned nine-sheet COA workbook is not an authority or dependency.

## 12. Final status and non-authorization

**PD-AUD-01..12: CLOSED IN DESIGN.** The physical design is suitable for independent technical review, but not implementation freeze until dependencies and exact-SHA PostgreSQL evidence are satisfied.

No SQL, DDL, EF mapping, migration, grants, runtime code, PostgreSQL execution, Posting Rule, Posting Release, production migration or Go-Live is authorized. O-35 remains OPEN pending independent review and exact-SHA PostgreSQL evidence. PD-T-001..046 are NOT RUN.

## 12. Owner-directed negative-stock authorization scope — O-64

Only a GENERAL_MANAGER principal may request a permitted non-serialized negative-stock limit change, and only for an explicitly assigned WarehouseId × VariantId pair. The role does not imply global scope. Scope creation, expansion, reassignment or amendment requires explicit Product/Business Owner approval and an immutable Audit record. The request must prove the active assignment and matching owner-approval reference at the server boundary; UI state or client-supplied role/scope is not authority.

SYSTEM_MANAGER alone and Emergency Session alone are denied. Serial/IMEI-tracked stock remains absolutely ineligible for negative stock, regardless of assignment or role. The v3.0.3/D-258 class eligibility and -2 default for its permitted non-serialized integer-UOM class are unchanged; the Feature Flag remains OFF.

The logical scope assignment is a versioned policy concept. This document does not create its physical table, roles, permissions, database grants or authorization code.

## 13. Owner-directed cutover decision and reservation policy

OWN-INV-019 v1.0 is the source record for D-15-CUTOVER-GUARD-01, O-64-SCOPE, WATERMARK-WH-01 and RES-TTL-SALES-01.

- D-15 policy decision is CLOSED/APPROVED. MAX_CUTOVER_TAIL_COUNT and MAX_CUTOVER_LOCK_DURATION remain NOT_SET until load/rollback measurement on target PostgreSQL/hardware. Therefore operational Cutover Gate remains unsatisfied.
- Both budgets and all three consistency checks are mandatory; failure preserves the previous Pointer and resumes Catch-up.
- Sales TTL is 24 hours by default, configurable/versioned and snapshotted; Repair has no automatic TTL.
- REQUIRES_ATTENTION is non-terminal and does not stop retry.
- O-35 remains OPEN until independent review and exact-SHA PostgreSQL watermark/race/replay/tie-out evidence is accepted. D-15 policy closure does not close O-35 or authorize Cutover.

## 14. Final status and non-authorization

PD-AUD-01..12 remain closed only at the internal design level inherited from v0.3; the successor amendments above require independent review. All PostgreSQL execution tests remain NOT_RUN. O-35 and O-36 remain OPEN pending their applicable independent review, implementation and execution evidence. O-64 owner decision is approved, but server enforcement and authorization tests remain OPEN. D-15 policy decision is closed; numeric budget values remain NOT_SET and Cutover remains prohibited until measurement and acceptance.

No SQL, DDL, EF mapping, migration, grants, runtime code, PostgreSQL execution, Posting Rule, Posting Release, production migration or Go-Live is authorized.

**END — MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001 v0.4**
