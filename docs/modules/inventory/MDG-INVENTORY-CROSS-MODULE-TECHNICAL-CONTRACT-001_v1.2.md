# MDG Inventory Cross-Module Technical Contract
## Version 1.1 — Owner-Approved Cutover and Scoped Authorization Successor

| Field | Value |
|---|---|
| Document ID | MDG-INVENTORY-CROSS-MODULE-TECHNICAL-CONTRACT-001 |
| Version / Date | 1.2 / 2026-11-20 |
| Status | OWNER DECISIONS APPROVED — CONTROLLED DESIGN SUCCESSOR — CROSS-OWNER ACCEPTANCE / BASELINE RECONCILIATION PENDING |
| Governing architecture baseline | MDG-ARCH-MASTER-001 v3.0.3 — owner-accepted and registered; v2.5 archived |
| Current architecture baseline | MDG-ARCH-MASTER-001 v3.0.3 — owner-accepted and registered; v2.5 archived |
| References | OWN-INV-019 v1.0; Inventory Logical ERD v0.3; PostgreSQL Physical Design v0.4; Stage 05 v0.1; owner answer package 2026-11-20 |
| Execution / tests | NOT IMPLEMENTED / NOT VERIFIED; ALL TESTS NOT RUN |

## 1. Authority and limits

This v1.1 successor consolidates the owner-directed cross-module decisions and the follow-up decisions recorded in OWN-INV-019 v1.0. It supersedes v1.0 only where the owner-approved WarehouseId watermark, D-15 cutover guard, Sales TTL and O-64 scoped-authority rules require it. It does not amend the accepted Master MDG-ARCH-MASTER-001 v3.0.3 or the Accounting Posting Matrix. Consumer-owner acceptance, independent review, implementation, test PASS, evidence acceptance, gate closure and release remain separate.

No SQL, DDL, EF mapping, schema freeze, Migration 0009, new migration, database role creation, GRANT/REVOKE, API/runtime/worker implementation, Inventory activation, negative-stock activation, Journal, Posting Rule, Posting Release, production migration or Go-Live is authorized. Migration 0009 remains RESERVED. Negative-stock Feature Flag remains OFF. O-35 remains OPEN. D-15 policy decision is CLOSED/APPROVED; numeric cutover budgets remain NOT_SET and Cutover remains prohibited until measured and accepted.

## 2. Owner decisions and controlled deltas

| ID | Owner decision | Status / reconciliation |
|---|---|---|
| VAL-POOL-01 | One Moving Weighted Average Pool per BusinessEntityId × VariantId. Warehouse/Section/Location/Lot do not create separate WAC pools. | OWNER APPROVED. Finance FE-08 currently says ProductVariant × Warehouse; Finance/Accounting acceptance and FE-08 revision required. FE-08 remains BLOCKED meanwhile. |
| SERIAL-NS-01 | Default Serial namespace: ManufacturerId + DeviceTypeId + NormalizedSerial. | OWNER APPROVED. ModelId is not in the default key. Catalog must guarantee stable Manufacturer/DeviceType references for Serial-tracked Variants. |
| NEG-STOCK-SCOPE-01 | Negative threshold scope: VariantId × WarehouseId across all Locations/Sections and Lots; not per Location/Lot or whole business entity. | OWNER APPROVED. Changes aggregation scope only; D-258 eligibility remains unchanged. |
| NEG-STOCK-AUTH-01 / O-64-SCOPE | GENERAL_MANAGER may change a negative-stock limit only for its explicitly assigned WarehouseId×VariantId pairs. Scope creation/change requires explicit owner approval and Audit. SYSTEM_MANAGER or Emergency Session alone gives no such authority. Serial/IMEI remains absolutely zero-negative. | OWNER APPROVED-CONSTRAINT in OWN-INV-019 v1.0. Server enforcement and scope/deny/allow tests remain open; negative-stock Feature Flag remains OFF. |
| RES-TTL-SALES-01 | Sales reservation default TTL 24h from server-confirmed creation; configurable/versioned and snapshotted per reservation. Repair reservation has no automatic TTL. | OWNER APPROVED. ERD v0.3/PDS v0.4 reflect the owner direction; controlled baseline crosswalk and source-owner reconciliation remain required before implementation. |
| WATERMARK-WH-01 | Commit sequence/watermark allocated per WarehouseId, not independently per WarehouseId × VariantId. | OWNER APPROVED. Replaces PDS v0.2/v0.3 per-Warehouse×Variant allocator; successor PDS and PostgreSQL proof required. |
| AUD-ATTENTION-01 | REQUIRES_ATTENTION is non-terminal; retry and controlled replay continue. | OWNER APPROVED. PDS v0.3 retry-exhaustion-to-QUARANTINED behavior requires controlled revision; quarantine reserved for integrity/security/non-retryable safety failures. |

### 2.1 D-258 eligibility is not expanded

NEG-STOCK-SCOPE-01 changes aggregation scope only. For this proposal, the candidate's v3.0.3/D-258 eligibility and defaults are preserved as owner-directed design inputs; no negative-stock implementation or activation is authorized:

| Item class | Default limit | Rule |
|---|---:|---|
| Non-serialized, integer/counting Base UOM | -2 | Only after independent Gate/Feature Flag is accepted |
| Non-serialized, decimal Base UOM | 0 | Negative issue prohibited by default |
| Serial / IMEI / mandatory tracked | 0 | Negative absolutely prohibited |

No monetary threshold. Limit is in Variant Base UOM. Check aggregates eligible OnHand across Locations/Sections and Lots for the same Variant/Warehouse. Defective, quarantined, unusable, custody-held and in-transit stock cannot offset an issue or become sellable through aggregation. A common parent scope lock serializes the check. Feature Flag remains OFF. A lower limit than an existing deficit blocks new issue until recovery or separately authorized/audited temporary override. No silent balance adjustment or automatic write-off.

### 2.2 Baseline conflict register

| Topic | Existing controlled wording | New owner direction | Required closure |
|---|---|---|---|
| Sales TTL | Candidate v3.0.3/Logical ERD v0.2/PDS v0.3: configurable; exact value OPEN | 24h initial configurable default from confirmed reservation | Amend master crosswalk/CHG, ERD/PDS and O-36; preserve existing reservation snapshots |
| Watermark | PDS v0.2/v0.3: per Warehouse×Variant | Per WarehouseId, contiguous committed prefix; movement-level entries; per-Warehouse projection/stocktake cursor vector | PDS v0.4 prepared; independent review and PostgreSQL proof required |
| WAC Pool | FE-08: ProductVariant×Warehouse | BusinessEntity×Variant | Finance/Accounting acceptance and FE-08 update |
| Threshold authority | Older INV-EVT-012 / OWN-INV-018 wording: System Manager | GENERAL_MANAGER only within owner-approved assigned WarehouseId×VariantId scope; scope change requires owner approval + Audit; SYSTEM_MANAGER/Emergency denied; Serial/IMEI never negative | O-64-SCOPE owner decision recorded in OWN-INV-019; source-event successor and server enforcement/tests required |
| Retry attention | PDS v0.3: exhausted retries become QUARANTINED | Attention non-terminal; retry continues | Successor separates attention flag from delivery state |

The v3.0.3 Master is the sole current owner-accepted and registered architecture baseline; v2.5 is archived. This v1.2 successor changes only baseline authority references. Consumer-owner acceptance, independent review, implementation, test PASS, evidence acceptance, gate closure and release remain separate. No executable change follows from this table.

## 3. Catalog contract

- Catalog exclusively owns Product, ProductVariant, SKU, product specifications, Base UOM, permitted UOMs/conversions, tracking policy, Manufacturer, DeviceType, Model and Serial/IMEI rules.
- Inventory consumes a versioned Variant contract. VariantId is the stock/valuation identity; ProductId is the reporting/classification parent.
- Every Variant has exactly one BaseUomId. Permitted UOM conversions are versioned and effective prospectively. Every movement retains input quantity/UOM, Base-UOM quantity, conversion factor/version and Variant snapshot/hash used.
- Quantity storage capacity NUMERIC(24,9) is not universal 9-decimal permission. Initial precision: count/device/part/package/box=0; meter/kg/liter=3; gram/mm/ml when Base UOM=0; custom UOM requires explicit approval. Fractional quantity only when FractionalQuantityAllowed=true.
- No rounding before allowed UOM precision. Inexact, unrepresentable or out-of-capacity conversion is rejected; no silent truncation/rounding.
- Variant contract includes ContractName/Version/Hash, VariantId, ProductId, VariantStatus, IsStockTracked, IsPurchasable, IsSellable, BaseUomId, permitted UOM/conversion references, QuantityPrecision, FractionalQuantityAllowed, TrackingPolicyVersion, SerialNormalizationPolicyVersion, ImeiPolicyVersion, EffectiveFrom/To and VariantSnapshotVersion. QuantityRoundingPolicyVersion only exists when a separate non-silent policy is approved.
- Default Serial key: ManufacturerId + DeviceTypeId + NormalizedSerial; ModelId is not part of the default key. Normalize Unicode NFC; trim boundary whitespace; reject controls; preserve internal separators; case-fold only when Manufacturer policy explicitly allows it; retain normalization version. Do not fall back to a weaker key when Manufacturer/DeviceType is missing.
- IMEI: 15 ASCII digits, Luhn, globally unique active, max two active IMEIs per StockUnit, never a person identifier. Correction requires authorization/reason/Actor/required independent approval; old ID remains inactive history.

## 4. Global lock order and transactional Warehouse watermark

### 4.1 Total order

All quantity/reservation/valuation/stocktake/projection mutations follow one total lock order. Sort all rows at a rank before advancing. Omit unused ranks; never acquire a lower rank after a higher rank.

| Rank | Resource | Deterministic order |
|---:|---|---|
| 0 | Idempotency receipt/result | ActorId, OperationCode, OperationContractVersion, IdempotencyKey |
| 1 | Source business aggregate, if in same transaction | Owner module, aggregate type, AggregateId; official application/transaction contract only, never foreign private-table access |
| 2 | Reservation aggregates | ReservationId ascending |
| 3 | Parent shortage scope | WarehouseId, then VariantId ascending |
| 4 | StockPosition | Canonical complete key ascending |
| 5 | StockUnit, UnitIdentifier, Lot | Fixed order StockUnit → UnitIdentifier → Lot; stable namespace/normalized identifier/UUID ascending |
| 6 | ValuationPool | BusinessEntityId, VariantId, PoolId ascending |
| 7 | Warehouse commit counter | WarehouseId ascending |
| 8 | Projection generation/checkpoint/active pointer | ProjectionName, scope, generation/key ascending |
| 9 | Outbox delivery metadata | Isolated short Worker transaction by OutboxId; never held while acquiring domain locks |

Rank 3 is the shared parent lock for NEG-STOCK-SCOPE-01. Rank 4 locks individual stock grains. Pool locks precede warehouse counters. Outbox Worker never holds a delivery lock while acquiring business/domain locks.

### 4.2 Idempotency boundary

Every mutating command, including commands with no StockMovement, uses AuthenticatedActor + OperationCode + OperationContractVersion + IdempotencyKey + CanonicalRequestFingerprint. Actor is server-derived; canonicalization version/digest algorithm are stored. Same key/same payload returns original durable response; same key/different payload fails closed; concurrent same requests produce one effect. Timeout/lost response is resolved using the same key. Receipt/result, business effects, movement, projection, local AuditIntent, Outbox and counters commit atomically. EventId, MovementId, OutboxId, source identity and IdempotencyKey are distinct.

### 4.3 Per-WarehouseId commit sequence

Logical transactional counter: WarehouseCommitSequence(WarehouseId, LastCommittedSequence).

1. Acquire all applicable Warehouse×Variant locks, stock grains and ValuationPool locks before Warehouse counter rows.
2. Use a transactional row counter, not nextval(), timestamp or UUID ordering.
3. Hold each Warehouse counter row lock through commit/rollback.
4. Each committed physical movement receives one movement-level sequence entry per affected Warehouse. Multiple movements in a command receive consecutive values while the counter is held.
5. One multi-line/multi-Variant movement in one Warehouse has one Warehouse sequence entry with line-level Variant/source references.
6. Future multi-Warehouse transactions lock all Warehouse counters ascending by WarehouseId and commit both sides atomically with shared transfer/cause identity. FE-31 remains R2 / NOT ENABLED.
7. Counter update, entry, movement, projection, AuditIntent, Outbox and Idempotency result are one transaction. Rollback leaves no committed counter/gap.
8. Each Warehouse sequence is a contiguous committed prefix; gaps/duplicates are integrity failures.
9. A movement writer resolves the active Projection generation only after it acquires the Warehouse counter lock. It must not cache the active Pointer before that lock; a writer unblocked after cutover must update the newly active generation. Ledger, sequence and that Projection update commit atomically.

### 4.4 Projection, stocktake and cutover guard

A projection may be Warehouse×Variant or finer-grained, but its source cursor is Warehouse-wide:

- Store AppliedWarehouseSequence per Warehouse.
- Consume each next contiguous sequence; if the event concerns another Variant, apply a verified no-op and advance. No false gap.
- Multi-Warehouse views retain a WarehouseId→sequence vector; no false global instant.
- CountObservation captures Warehouse watermark under counter lock; multiple Warehouses use a vector. Reconcile post-cutoff movements through declared cutoff.
- Hold counter lock only for short watermark capture/write, never physical counting.
- Shadow build/bulk Catch-up occurs outside writer/counter locks. Final Tail processing is gated by two independent versioned budgets: MAX_CUTOVER_TAIL_COUNT and MAX_CUTOVER_LOCK_DURATION.
- MAX_CUTOVER_TAIL_COUNT counts unprocessed movement-level Warehouse sequence entries across all affected Warehouses in the cutover attempt. MAX_CUTOVER_LOCK_DURATION measures elapsed time from acquisition of the first relevant Warehouse counter lock through transaction commit/rollback and lock release.
- Both numeric values are NOT_SET pending representative PostgreSQL 18 and target-hardware load/rollback tests. No guessed default is permitted.
- Before Pointer Swap, every movement through the cutover watermark must be applied, every affected Warehouse watermark difference must equal zero, and quantity difference at every canonical stock grain between Projection and Ledger replay must equal zero.
- If either budget is exceeded or any completeness/reconciliation check fails, abort and roll back the attempt, preserve the prior active Pointer, release locks and continue background Catch-up. Pointer Swap is fail-closed.
- Abort must start early enough for rollback and final lock release to finish within MAX_CUTOVER_LOCK_DURATION. The abort reserve is obtained from target-hardware rollback measurements, not guessed. Record elapsed lock-held time through final lock release. If rollback/release exceeds the limit, preserve the prior Pointer and fail the Cutover Gate; do not record a successful Cutover.
- Pointer switch is atomic only after both budgets and all reconciliation checks pass. Rollback is allowed only to a generation independently reconciled to the current watermark.



### 4.5 Negative-stock evaluation

Acquire parent Warehouse×Variant lock, then affected StockPositions in canonical order. Re-read eligible warehouse OnHand after locks. Aggregate eligible same-Variant/same-Warehouse positions across Locations/Sections and Lots. Do not net defective/quarantined/unusable/custody-held/in-transit amounts. Recheck D-258 class and Feature Flag. Before a limit change is accepted, the server must authenticate GENERAL_MANAGER and verify an active owner-approved assignment for that exact WarehouseId×VariantId pair. Scope creation, expansion, reassignment or amendment requires explicit owner approval and an Audit record; a manager cannot self-assign or expand scope. SYSTEM_MANAGER alone and Emergency Session alone are denied. Serial/IMEI class remains zero-negative without exception. Atomically record before/after limit, reason, Actor, assignment version, owner-approval reference, AuditIntent and Outbox. Client/UI balance or claimed role/scope is never authoritative.

## 5. Reservation state and TTL

| Current | Event | Next | Rule |
|---|---|---|---|
| None | Confirm eligible source | ACTIVE | Server creates and snapshots TTL policy/version |
| ACTIVE | Partial consume | ACTIVE | Reduce remainder; preserve consumed history |
| ACTIVE | Full consume | CONSUMED | Remaining zero; linked committed movement |
| ACTIVE | Authorized release | RELEASED | Unconsumed remainder only |
| ACTIVE | At/after ExpiresAtUtc | EXPIRED | Release remainder once; immutable event |
| ACTIVE | Authorized cancellation | CANCELLED | Unconsumed remainder only |
| CONSUMED | Correction/reversal | New linked fact | Never rewrite/reopen original reservation |

Draft and Backorder create no reservation. Allocation/Pick are subsets of active remaining reservation, not extra ATP deductions. Transitions are idempotent; no physical deletion. Partial consumption remains ACTIVE while quantity remains.

Sales default TTL is 24 hours from server-confirmed reservation creation; configurable policy and version are snapshotted on each Reservation. Policy change affects new reservations only. Repair uses ExpiryMode=NONE/no automatic TTL. ACTIVE only while now_utc < ExpiresAtUtc; equality is expired. Use trusted UTC after acquiring Reservation lock and recalculate after lock wait. Expired reservation is excluded from EffectiveReserved/ATP even if cleanup Worker is down. Issue/expiry serializes on Reservation; after-boundary issue is rejected. ATP=Sellable−EffectiveReserved; Custody is separate and excluded. Two competing reservations lock parent scope and revalidate capacity. The 24-hour default is the owner-approved O-36 policy. Reconcile v3.0.3 crosswalk and source contracts before implementation; no reservation created before the effective policy change is silently recalculated.

## 6. Valuation and True-up

- Pool key: BusinessEntityId × VariantId (VAL-POOL-01). R1 still has one business entity; this does not activate multi-company. Warehouse/Section/Location/Lot do not create separate pools.
- Non-serialized: Moving Weighted Average. Serial/IMEI: Specific Identification per StockUnit. FIFO is not generalized as primary valuation.
- Pool lock is Rank 6. Only eligible, approved directly attributable landed costs enter pool.
- Exact numeric arithmetic; no floating point. Monetary results integer IRR under existing one-time document-total policy; no new per-unit/per-row rounding is invented.
- Inventory owns pool/layers, cost lineage, WAC/Specific Identification calculations, True-up evidence, append-only valuation adjustment and outgoing valuation event.
- Accounting/Finance owns RecognitionTime, COA, account eligibility, Journal/JournalLine, GL, COGS recognition, fiscal periods and Posting Rules. Inventory cannot choose account, debit/credit, Journal or PostingRule.
- 110801 remains sole Inventory GL control; 110802–110806 are subledger classifications, not extra GL controls. No account mapping is inferred.

True-up is append-only, versioned, linked and idempotent. It references TrueUpId, source Purchase/Receipt ID+version, original Movement/Line, Variant/StockUnit/Lot, ValuationPoolId, source ValuationLayer/version, provisional/actual cost evidence, quantity lineage, remaining and consumed quantities, total integer-IRR delta, split allocation, calculation version, Actor, approver, reason, EventId, IdempotencyKey, timestamps and Audit references.

Owner's example: two units were valued provisionally at 100 each; actual verified cost is 110; one unit remains and one was already sold/consumed. Difference is 10 per unit: 10 IRR adjusts remaining inventory and 10 IRR adjusts prior issued/consumed cost lineage; total 20 IRR. No prior Movement/source/finalized Journal is rewritten. A non-integral split must conserve total integer IRR via a deterministic allocation accepted by Finance and tested; no new rounding/Posting Rule is inferred. Inventory emits valuation evidence; Finance accepts it under its official contract.

Finance FE-08 currently states WAC scope ProductVariant×Warehouse; VAL-POOL-01 states BusinessEntity×Variant. They coincide operationally in current one-entity/one-warehouse topology but differ for future multi-Warehouse. Finance acceptance and FE-08 update are mandatory; FE-08 stays BLOCKED meanwhile.

## 7. Audit Intent, Outbox and AuditSecurity

Inventory owns local AuditIntent and Outbox; AuditSecurity exclusively owns canonical AuditEvent, deduplication, durable store, retention, access/export and durable ACK. The same local Inventory transaction commits Idempotency result, Movement/lines, active Projection, AuditIntent, Outbox and required sequence/counters. Failure to persist any required local part rolls back all. AuditSecurity outage does not alone block local commit if local intent/outbox are durable. Publish only after commit; delivery is at-least-once, not exactly-once transport.

Required envelope: EventId, EventType, ContractName/Version, PayloadSchemaVersion, PayloadHashAlgorithm/Hash, ActorReference/Type, ActorRoleReference where allowed, ExecutorServiceIdentity, SourceModule/Operation, AggregateId/Version, source document/version/line, CorrelationId, CausationId, OccurredAtUtc, server RecordedAtUtc, Variant/Warehouse/Location/Tracking/Quantity/UOM/Valuation references as applicable. EventId is created with AuditIntent and remains stable. Same EventId+Hash returns same durable ACK; same EventId+different Hash is INTEGRITY_VIOLATION, reject and preserve original. Actor comes from authenticated server context; Worker identity remains distinct. Credentials/tokens/recovery secrets/card data/unnecessary PII prohibited.

ACK is issued only after canonical AuditEvent and dedup identity durably commit; HTTP/in-memory/queued response is insufficient. ACK includes EventId, PayloadHash, canonical AuditEvent reference and durable RecordedAtUtc. Inventory marks DELIVERED_ACKNOWLEDGED only after matching EventId+Hash. Lost ACK is retried with same identity.

Local delivery states: PENDING, LEASED, RETRYABLE_FAILURE, DELIVERED_ACKNOWLEDGED, QUARANTINED. REQUIRES_ATTENTION is a non-terminal alert flag and never stops retry/deletes event. Transient failures remain retryable. QUARANTINED only for integrity, invalid contract, security or explicitly non-retryable safety. Lease expiry permits reclaim. Retry is exponential+jitter with configurable cap. Owner initial values: 15m cap, warning >15m, critical >60m; all configurable and subject to operational/load review. Manual re-drive requires authorization/reason/incident reference and reuses same EventId/Hash. This supersedes PDS v0.3 retry-exhaustion quarantine wording only after successor review.

Retention owner policy: commercial/financial audit at least 10 years from end of applicable year; technical/security-only logs default 2 years; Legal Hold/order or longer applicable rule prevails; use longest applicable period. This is product policy, not final legal opinion; legal applicability/current Iranian law must be reviewed before release. Sensitive reads/search/export require separate permission and audit.

## 8. Shared contracts and ownership

All contracts use MAJOR.MINOR.PATCH; compatible optional fields MINOR, documentation/clarification PATCH, breaking semantics MAJOR. Each message includes ContractName/Version; unsupported version fails closed.

OccurredAtUtc=trusted occurrence; RecordedAtUtc=durable server record; EffectiveAtUtc=business/financial effect when permitted. Store technical times UTC; Persian calendar/timezone affect business display/date, not commit order.

Common errors: VALIDATION_FAILURE, AUTHORIZATION_FAILURE, CONFLICT, DUPLICATE, STALE_VERSION, DEPENDENCY_UNAVAILABLE, TIMEOUT, RETRYABLE_FAILURE, PERMANENT_FAILURE, INTEGRITY_VIOLATION. Every response states commit/rollback/indeterminate, prior effect, retry/key, human attention and prior-result query. Timeout alone never proves rollback.

No direct private-table/DbContext/entity access across modules. Inventory owns movement/stock/reservation/custody/stocktake/valuation/projection/local Outbox; Catalog owns masters/UOM/tracking; Organization owns Warehouse/Location; AuditSecurity owns canonical AuditEvent; Platform owns shared technical infrastructure; Accounting owns Journal/COA/Posting/RecognitionTime/GL; Treasury owns payment/settlement/refund. Formal internal Application Service contracts are permitted.

## 9. Cross-owner acceptance and remaining gates

| Dependency | Status |
|---|---|
| Catalog Variant/UOM/Serial/IMEI | Owner decisions specified; Catalog technical acceptance pending |
| AuditSecurity ingestion/EventId/ACK/retention/privacy | Owner policy specified; AuditSecurity technical acceptance pending |
| Platform six-role privilege matrix | Companion document prepared; actual roles/grants/tests absent |
| Finance WAC Pool/True-up event | Finance acceptance pending; FE-08 BLOCKED |
| WarehouseId watermark/PDS successor | Owner design specified; independent review/PostgreSQL proof pending |
| Sales 24h and O-64-SCOPE | Owner decisions approved; source/baseline reconciliation and server permission tests pending |
| D-15 | Policy decision CLOSED/APPROVED in OWN-INV-019; MAX_CUTOVER_TAIL_COUNT and MAX_CUTOVER_LOCK_DURATION remain NOT_SET pending representative PostgreSQL/hardware measurement; Cutover Gate unsatisfied |
| O-35 | OPEN; watermark/race/replay/conservation/tie-out runtime evidence NOT RUN |

Required controlled successors: architecture crosswalk/CHG; Inventory ERD v0.3; Inventory PDS v0.4; OWN-INV-019; Inventory source-event proposal v0.3; Finance FE-08/True-up crosswalk; O-register v1.2 and Document Register v1.6. No existing version is silently overwritten. Exact-SHA independent review required.

## 10. Owner follow-up decisions — OWN-INV-019 v1.0

### 10.1 D-15-CUTOVER-GUARD-01 — policy closed, numeric budgets NOT_SET

Two independent budgets are mandatory: MAX_CUTOVER_TAIL_COUNT and MAX_CUTOVER_LOCK_DURATION. Both are configured/versioned separately and both must be satisfied. Their values are derived only from representative load and rollback tests on the target PostgreSQL 18 configuration and target server hardware.

Pointer Swap additionally requires all committed movements before the cutover point to be applied, zero quantity difference between Projection and Ledger replay, and zero applied-watermark difference for every affected Warehouse. Failure of either budget or any one of these consistency conditions prohibits Pointer Swap. The attempt rolls back, the prior active Pointer remains unchanged, locks are released and background Catch-up continues.

D-15 policy decision is CLOSED/APPROVED. Numeric values remain NOT_SET and Cutover Gate remains unsatisfied.

### 10.2 O-64-SCOPE — assigned Warehouse × Variant authority

GENERAL_MANAGER may change the negative-stock limit only within the principal's explicit owner-approved WarehouseId×VariantId assignments. The role alone creates no global authority. Assignment creation/change requires explicit owner approval and an Audit record. SYSTEM_MANAGER and Emergency Session alone are denied. Serial/IMEI negative stock is absolutely prohibited. D-258 eligibility and the negative-stock Feature Flag OFF are unchanged.

### 10.3 O-36 reservation policy

Sales default TTL is 24 hours from server-confirmed creation; it is configurable/versioned and snapshotted on each Reservation. Repair has no automatic TTL. Policy changes affect only new reservations.

### 10.4 Remaining acceptance

PDS v0.4, ERD v0.3, source-event proposal v0.3 and the test specification v1.1 are design artifacts. Cross-owner acceptance, independent exact-SHA review and all required implementation/PostgreSQL evidence remain open. No implementation or release authorization is implied.

**END — MDG-INVENTORY-CROSS-MODULE-TECHNICAL-CONTRACT-001 v1.1**


## Baseline authority alignment — 1.2 successor, 2026-11-21

This controlled successor aligns the document's governing architecture reference with MDG-ARCH-MASTER-001 v3.0.3, the sole current owner-accepted and repository-registered Master. MDG-ARCH-MASTER-001 v2.5 is archived historical material only. This alignment does not imply independent technical review, module-owner acceptance, implementation, test execution, evidence acceptance, migration authorization, Posting Release or Go-Live.