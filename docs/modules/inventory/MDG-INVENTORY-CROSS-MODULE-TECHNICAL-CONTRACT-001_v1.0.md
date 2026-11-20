# MDG Inventory Cross-Module Technical Contract
## Version 1.0 — Owner-Directed Controlled Design Supplement

| Field | Value |
|---|---|
| Document ID | MDG-INVENTORY-CROSS-MODULE-TECHNICAL-CONTRACT-001 |
| Version / Date | 1.0 / 2026-11-20 |
| Status | OWNER DECISIONS RECORDED — DESIGN CANDIDATE — CROSS-OWNER ACCEPTANCE / BASELINE CHANGE CONTROL PENDING |
| Sole accepted architecture baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| References | Inventory Logical ERD v0.2; PostgreSQL Physical Design v0.3; Stage 05 v0.1; owner answer package 2026-11-20 |
| Execution / tests | NOT IMPLEMENTED / NOT VERIFIED; ALL TESTS NOT RUN |

## 1. Authority and limits

This document consolidates owner answers as a cross-module design contract. It does not silently amend v3.0.3, Inventory PDS v0.3, the Accounting Posting Matrix or another accepted baseline. Owner approval is distinct from consumer-owner acceptance, implementation, test PASS, evidence acceptance, gate closure and release.

No SQL, DDL, EF mapping, schema freeze, Migration 0009, new migration, database role creation, GRANT/REVOKE, API/runtime/worker implementation, Inventory activation, negative-stock activation, Journal, Posting Rule, Posting Release, production migration or Go-Live is authorized. Migration 0009 remains RESERVED. Negative-stock Feature Flag remains OFF. O-35 remains OPEN. D-15 remains NOT_SET.

## 2. Owner decisions and controlled deltas

| ID | Owner decision | Status / reconciliation |
|---|---|---|
| VAL-POOL-01 | One Moving Weighted Average Pool per BusinessEntityId × VariantId. Warehouse/Section/Location/Lot do not create separate WAC pools. | OWNER APPROVED. Finance FE-08 currently says ProductVariant × Warehouse; Finance/Accounting acceptance and FE-08 revision required. FE-08 remains BLOCKED meanwhile. |
| SERIAL-NS-01 | Default Serial namespace: ManufacturerId + DeviceTypeId + NormalizedSerial. | OWNER APPROVED. ModelId is not in the default key. Catalog must guarantee stable Manufacturer/DeviceType references for Serial-tracked Variants. |
| NEG-STOCK-SCOPE-01 | Negative threshold scope: VariantId × WarehouseId across all Locations/Sections and Lots; not per Location/Lot or whole business entity. | OWNER APPROVED. Changes aggregation scope only; D-258 eligibility remains unchanged. |
| NEG-STOCK-AUTH-01 | GENERAL_MANAGER alone may change negative-stock limit. SYSTEM_MANAGER or Emergency Session alone gives no such authority. | OWNER APPROVED; resolves O-64 role policy. Supersedes old System Manager wording in OWN-INV-018 v1.0 and Inventory source-event proposal v0.2. Server enforcement/tests remain open. |
| RES-TTL-SALES-01 | Sales reservation default TTL 24h from server-confirmed creation; configurable/versioned. Repair reservation has no automatic TTL. | OWNER APPROVED. v3.0.3/PDS/ERD currently leave exact Sales TTL OPEN; controlled baseline amendment required. |
| WATERMARK-WH-01 | Commit sequence/watermark allocated per WarehouseId, not independently per WarehouseId × VariantId. | OWNER APPROVED. Replaces PDS v0.2/v0.3 per-Warehouse×Variant allocator; successor PDS and PostgreSQL proof required. |
| AUD-ATTENTION-01 | REQUIRES_ATTENTION is non-terminal; retry and controlled replay continue. | OWNER APPROVED. PDS v0.3 retry-exhaustion-to-QUARANTINED behavior requires controlled revision; quarantine reserved for integrity/security/non-retryable safety failures. |

### 2.1 D-258 eligibility is not expanded

NEG-STOCK-SCOPE-01 changes aggregation scope only. Until a separate owner amendment to D-258 exists, v3.0.3 remains controlling:

| Item class | Default limit | Rule |
|---|---:|---|
| Non-serialized, integer/counting Base UOM | -2 | Only after independent Gate/Feature Flag is accepted |
| Non-serialized, decimal Base UOM | 0 | Negative issue prohibited by default |
| Serial / IMEI / mandatory tracked | 0 | Negative absolutely prohibited |

No monetary threshold. Limit is in Variant Base UOM. Check aggregates eligible OnHand across Locations/Sections and Lots for the same Variant/Warehouse. Defective, quarantined, unusable, custody-held and in-transit stock cannot offset an issue or become sellable through aggregation. A common parent scope lock serializes the check. Feature Flag remains OFF. A lower limit than an existing deficit blocks new issue until recovery or separately authorized/audited temporary override. No silent balance adjustment or automatic write-off.

### 2.2 Baseline conflict register

| Topic | Existing controlled wording | New owner direction | Required closure |
|---|---|---|---|
| Sales TTL | v3.0.3/Logical ERD v0.2/PDS v0.3: configurable; exact value OPEN | 24h initial configurable default from confirmed reservation | Amend master crosswalk/CHG, ERD/PDS and O-36; preserve existing reservation snapshots |
| Watermark | PDS v0.2/v0.3: per Warehouse×Variant | Per WarehouseId | Successor PDS; projection/stocktake cursor revision; independent review and PostgreSQL proof |
| WAC Pool | FE-08: ProductVariant×Warehouse | BusinessEntity×Variant | Finance/Accounting acceptance and FE-08 update |
| Threshold authority | Older INV-EVT-012 / OWN-INV-018 wording: System Manager | GENERAL_MANAGER only | O-64 owner decision recorded; supersede old wording; server enforcement/tests |
| Retry attention | PDS v0.3: exhausted retries become QUARANTINED | Attention non-terminal; retry continues | Successor separates attention flag from delivery state |

v3.0.3 remains the accepted baseline until its controlled amendment is reviewed and accepted. No executable change follows from this table.

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

### 4.4 Projection and stocktake cursor

A projection may be Warehouse×Variant or finer-grained, but its source cursor is Warehouse-wide:
- Store AppliedWarehouseSequence per Warehouse.
- Consume each next contiguous sequence; if the event concerns another Variant, apply a verified no-op and advance. No false gap.
- Multi-Warehouse views retain a WarehouseId→sequence vector; no false global instant.
- CountObservation captures Warehouse watermark under counter lock; multiple Warehouses use a vector. Reconcile post-cutoff movements through declared cutoff.
- Hold counter lock only for short watermark capture/write, never physical counting.
- Shadow build/bulk catch-up outside writer lock; only bounded tail under lock. If tail exceeds approved budget, abort pointer switch, preserve current generation and continue catch-up.
- Pointer switch atomic after contiguous watermark, quantities, counts, digest and source completeness pass. Rollback only to a generation reconciled to current watermark.

This replaces PDS v0.2/v0.3 per-Warehouse×Variant allocator and requires a successor PDS and PostgreSQL re-audit. D-15 remains NOT_SET until measured.

### 4.5 Negative-stock evaluation

Acquire parent Warehouse×Variant lock, then affected StockPositions in canonical order. Re-read eligible warehouse OnHand after locks. Aggregate eligible same-Variant/same-Warehouse positions across Locations/Sections and Lots. Do not net defective/quarantined/unusable/custody-held/in-transit amounts. Recheck D-258 class and Feature Flag; atomically commit quantity, reason, Actor, AuditIntent and Outbox. Client/UI balance is never authoritative.

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

Sales default TTL is 24 hours from server-confirmed reservation creation; configurable policy and version are snapshotted on each Reservation. Policy change affects new reservations only. Repair uses ExpiryMode=NONE/no automatic TTL. ACTIVE only while now_utc < ExpiresAtUtc; equality is expired. Use trusted UTC after acquiring Reservation lock and recalculate after lock wait. Expired reservation is excluded from EffectiveReserved/ATP even if cleanup Worker is down. Issue/expiry serializes on Reservation; after-boundary issue is rejected. ATP=Sellable−EffectiveReserved; Custody is separate and excluded. Two competing reservations lock parent scope and revalidate capacity. The 24-hour default is a controlled amendment because current v3.0.3/PDS/ERD leave exact Sales TTL OPEN; reconcile sources/O-36 before implementation.

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
| Sales 24h and GENERAL_MANAGER role | Owner decisions recorded; controlled baseline/source reconciliation pending |
| D-15 | NOT_SET until representative performance and rollback measurement |
| O-35 | OPEN; watermark/race/replay/conservation/tie-out runtime evidence NOT RUN |

Required controlled successors: architecture crosswalk/CHG; Inventory ERD; Inventory PDS; OWN-INV-018/source event; Finance FE-08/True-up crosswalk; O-register and Document Register. No existing version is silently overwritten. Exact-SHA independent review required.

**END — MDG-INVENTORY-CROSS-MODULE-TECHNICAL-CONTRACT-001 v1.0**
