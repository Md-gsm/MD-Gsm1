# MDG Inventory Stage 05 — Conservation Equations, Custody and Stocktake

| Field | Value |
|---|---|
| Document ID | MDG-INVENTORY-STAGE-05-EQUATIONS-CUSTODY-STOCKTAKE-001 |
| Version | 0.1 |
| Status | INTEGRATED DESIGN CONTRACT — implementation and runtime evidence pending |
| Sole architecture baseline | **MDG-ARCH-MASTER-001 v3.0.3** |
| Inventory business-policy authority | OWN-INV-018 v1.0, as incorporated and constrained by v3.0.3 |
| Related authorities | v3.0.3 §§8, L.2–L.7, D-258, O-35, O-36; Inventory source contract v0.2; Sales/Inventory/Finance Stage 04 contract |
| Scope | Inventory model, quantity conservation, ATP, reservations, custody, movement, section transfer, warehouse transfer, stocktake, valuation tie-out |
| Release boundary | Design only; no schema, migration, runtime, Posting Rule, Posting Release or operational release |

## 0. Authority and supersession

1. v3.0.3 is the only accepted architecture baseline. Earlier architecture versions and older proposal references are historical only.
2. This document completes the semantic equations and workflows required by O-35. It does not mark O-35 technically closed until the specified PostgreSQL concurrency, replay, tie-out and stocktake tests pass on an exact SHA.
3. OWN-INV-018 v1.0 remains the business-policy decision record only to the extent consistent with v3.0.3. Where an older workpaper or proposal differs from v3.0.3, v3.0.3 prevails.
4. No Sales reservation TTL is invented here. v3.0.3 keeps the effective Sales TTL configurable and its final value OPEN; Repair reservation has no automatic TTL. Any older proposal value such as 30 minutes is not a production default.
5. Negative stock rules are exactly those in v3.0.3/D-258: default -2 only for eligible non-serialized integer-UOM goods, zero for decimal-UOM non-serialized goods, and zero/absolute prohibition for serial/IMEI/tracked goods. Negative-stock feature remains OFF until its independent Gate/Feature Flag and contracts/tests are accepted. No monetary threshold exists.
6. COA and GL remain Accounting-owned. Inventory subledger facts do not invent account codes or Posting Rules. The accepted COA authority is v1.2 master plus v1.5 controlled supplement, not v1.4 or a nine-sheet workbook.
7. This design does not authorize R1 repair intake, inter-warehouse transfer FE-31 (R2), or any synthetic RepairCase/RepairServiceLine.

## 1. Ownership and stock identity

### 1.1 Data ownership

| Data / fact | Sole owner | Inventory boundary |
|---|---|---|
| Organization, Warehouse, Section, Shelf, Bin/StorageLocation hierarchy | Organization | Inventory references versioned active identifiers; it cannot create or mutate master hierarchy |
| Product, Variant/SKU, base UOM, conversion, tracking policy, ConditionGrade | Catalog | Inventory stores immutable reference/version snapshots |
| PurchaseDocument, PurchaseLine, commercial terms | Purchasing | Inventory accepts valid source references; it owns physical receipt/movement and custody |
| SalesDocument, SalesLine, customer handover acknowledgement, return entitlement | Sales | Inventory owns committed physical issue/return receipt/disposition |
| RepairCase, RepairServiceLine and service workflow | RepairOperations | Inventory accepts only a real authorized source; FE-09/R1 guard applies |
| StockUnit, identifier links, Lot membership, movements, reservations, custody, stocktake, valuation layers, stock projections | Inventory | Exclusive write ownership |
| RecognitionTime, COA, Journal/GL, Posting Rules, fiscal period, COGS policy | Accounting | Inventory publishes immutable evidence; no direct GL authority |
| Payments, refunds, settlements | Treasury | No cash/settlement mutation from a physical stock event |
| Read models and reports | Reporting | Derived only; never stock source of truth |

No other module writes Inventory tables or projections. Cross-module communication uses versioned contracts and immutable source references.

### 1.2 Canonical stock grain

The authoritative stock grain is:

- WarehouseId;
- VariantId (not ProductId alone);
- applicable Section/StorageLocationId;
- applicable StockStatus;
- applicable LotId;
- applicable StockUnitId / TrackingIdentity;
- BaseUOM and the UOM/tracking-policy version effective for the movement.

Warehouse is the primary availability and shortage-control scope. Section/Location is physical traceability, not an independent shortage scope. A Product with multiple Variants never shares stock between Variants.

For stock not tracked by location, one explicit Warehouse-level UNLOCATED bucket is used. For stock whose policy requires location tracking, LocationId is mandatory. The Warehouse aggregate is the sum of child location buckets OR the UNLOCATED bucket as applicable; aggregate rows are never added again to their own children.

For Lot+Serial tracking, each StockUnit has a consistent Variant, Lot and receipt lineage. A dual-SIM physical unit may have two IMEIs linked to one StockUnit; one active IMEI cannot identify two StockUnits. Serial namespace/normalization follows the approved Catalog identity contract.

## 2. Source of truth and movement semantics

1. The append-only committed StockMovement ledger is the sole source of truth for quantity changes.
2. StockBalance/StockPosition is a rebuildable projection. It may be materialized for performance but is never an independent authority.
3. A movement line stores positive quantity magnitude; MovementType and source/destination dimensions determine direction. Negative quantity is not used to hide direction.
4. Only COMMITTED movement changes authoritative on-hand/custody/in-transit quantities. Draft, requested, approved-but-not-committed, reservation, allocation, pick, count observation and Backorder do not.
5. A committed movement is immutable. Correction, reversal or disposition is a new linked movement with source MovementId/LineId, reason, actor, permission, timestamp and idempotency identity.
6. Movement, affected projection update, Audit and Outbox persist atomically in one Inventory DB transaction. Failure rolls back all. Events publish only after commit; delivery is at-least-once; consumers are idempotent.
7. Same EventId/IdempotencyKey and same canonical payload produces one effect. Reuse with a different payload fails closed and is audited.
8. All calculations use the base UOM and exact versioned conversion used by the source movement. Quantity storage is Decimal, never float/double. Exact PostgreSQL precision/scale and UOM increments must follow the accepted technical numeric policy; no undocumented rounding is allowed.
9. Each movement records UTC technical timestamps and server-derived ActorReference. Business date uses Asia/Tehran central-server semantics. Client clock and client-supplied Actor are not trusted.

### 2.1 Movement types and commit boundary

| Movement type | Commit evidence | Quantity effect |
|---|---|---|
| OpeningBalance | approved opening source, quantity and valuation evidence | establishes a documented initial position; no unsupported opening stock |
| GoodsReceipt | actual physical receipt and accepted/rejected/pending quantities | accepted quantity increases the applicable receiving bucket |
| SalesIssue | authorized SalesLine, physical issue and tracking validation | decreases warehouse on-hand; links exact fulfillment quantity |
| RepairMaterialIssue | real authorized RepairServiceLine and FE-09 guard | decreases stock; no synthetic repair source |
| Internal Section transfer | valid source/destination Sections in same Warehouse | paired source decrement/destination increment; Warehouse total unchanged |
| Warehouse transfer dispatch | approved transfer and actual dispatch | decreases source on-hand and increases in-transit custody |
| Warehouse transfer receipt | actual accepted destination receipt | decreases in-transit and increases destination only by accepted quantity |
| CustomerReturnReceipt | actual physical return received | increases RETURNED_PENDING_INSPECTION, not Sellable |
| SupplierReturnDispatch | authorized actual supplier dispatch | decreases applicable on-hand/status quantity |
| InventoryAdjustment | approved stocktake/authorized correction | separate immutable variance movement |
| Status/DispositionChange | inspection and authorized decision | paired transfer between status buckets; physical total conserved |
| WriteOff/Scrap | authorized disposal evidence and approval | removes quantity from company-held stock through a distinct movement |

A Purchase Order, invoice, reservation, allocation, pick-list, delivery promise, stocktake count or Backorder alone is not a committed stock movement.

## 3. Canonical quantity equations

All equations are evaluated for a declared scope and AsOf cutoff. The scope must state Warehouse, Variant, UOM, applicable Section/Location, Lot/StockUnit, Status and the source-ledger watermark. Values from different grains, UOMs or cutoffs must not be combined.

### 3.1 OnHand

For a given Warehouse × Variant × tracking grain × status × location bucket:

`OnHand(t) = OpeningBalance + Σ CommittedInboundMovements(≤t) − Σ CommittedOutboundMovements(≤t)`

Equivalent signed-ledger form:

`OnHand(t) = Σ SignedCommittedMovementQuantity(≤t)`

- Includes physical company-held stock in sellable and non-sellable status buckets.
- Does not include unreceived inbound purchase, warehouse in-transit custody, technician custody or customer-owned devices.
- It may be negative only where v3.0.3/D-258 permits and only while the separate negative-stock feature gate is enabled.
- Warehouse OnHand equals the sum of its mutually exclusive child location buckets or its UNLOCATED bucket; never both.

### 3.2 Sellable

`Sellable(t) = Σ OnHand(t) for buckets that are simultaneously: company-owned, physically held in the Warehouse, inspected/eligible, good condition, active and permitted for ordinary sale`

Exclude, at minimum:
- RETURNED_PENDING_INSPECTION;
- DEFECTIVE, QUARANTINED and UNUSABLE;
- customer-owned devices;
- stock in technician/external Custody;
- warehouse/inter-warehouse IN_TRANSIT;
- any blocked, inactive or otherwise non-saleable status.

Sellable is a subset of OnHand, not an additional stock quantity. A status change must move quantity between status buckets atomically; it must not increase or decrease total OnHand.

### 3.3 Reserved and EffectiveReserved

`Reserved(t) = Σ remaining quantity of valid, active, unconsumed reservations at the same stock grain`

Reservation remaining quantity is conserved per source line:

`Requested = Issued + ActiveReservedRemaining + ReleasedOrCancelled + UnreservedRemainder`

Allocated and Picked are workflow subsets of ActiveReservedRemaining, not extra reservation quantities and not extra ATP deductions.

`EffectiveReserved(t) = Σ distinct active reserved physical quantity that is included in Sellable(t)`

A unit/quantity is subtracted once only. Expired, released, consumed, cancelled or invalid reservations are excluded. Reservation expiry and issue/release races serialize at the applicable Warehouse × Variant scope and are idempotent.

### 3.4 ATP

`ATP(t) = Sellable(t) − EffectiveReserved(t)`

- ATP is a derived availability quantity, not an independent ledger.
- ATP does not include unreceived purchase orders, unreceived warehouse transfers, technician Custody, customer-owned goods, Backorders or non-saleable stock.
- A Backorder records demand only; it creates no stock, Reservation, Movement, Posting or revenue.
- ATP is computed at the server for the authoritative Warehouse × Variant scope; location is a traceability dimension and cannot create independent negative-stock capacity.
- The result is not silently clamped to zero where an authorized non-serialized integer-UOM shortage exists. The exact negative-stock rules and feature gate in §7 apply.

### 3.5 InTransit

`InTransit(t) = Σ dispatched quantity not yet accepted at destination and not yet closed by a formally authorized resolution movement`

For each TransferLine:

`Dispatched = AcceptedReceived + OutstandingInTransit + FormallyResolvedDiscrepancy`

A discrepancy label alone does not remove quantity from InTransit. Partial receipt transfers only accepted quantity/value. Missing quantity remains in transit/discrepancy until a distinct authorized resolution movement records recovery, return, loss or other accepted disposition.

Unreceived purchase quantity is separately represented as QualifiedInbound only when the v3.0.3 purchasing conditions are satisfied; it is not physical OnHand or ATP. 110805/in-transit asset, physical stock, QualifiedInbound and GRNI 2103 remain distinct concepts and must not be collapsed.

### 3.6 Custody

`Custody(t) = OpeningCustody + Σ CommittedHandoversToCustodian − Σ ValidCustodyReturns − Σ AcceptedConsumption − Σ AuthorizedCustodyResolutions`

Custody is quantity definitively handed to a technician or other named custodian and not yet validly returned or otherwise resolved. Company ownership may remain, but the stock is outside warehouse Sellable and ATP.

- Custody is not Reservation.
- A handover to technician does not create another Reservation.
- Custody cannot be counted as Warehouse OnHand or Sellable while held outside the Warehouse.
- A part is returned to ATP only after a committed physical return movement, valid destination/inspection/disposition and any required workflow evidence.
- Customer-owned devices are not company StockUnit/ATP, even if physically held for service.
- Consumed parts require a real authorized source event (for repair, FE-09/RepairServiceLine where enabled); they cannot be cleared by merely changing Custody status.
- Loss/damage requires its own evidence, reason, authorization and resolution movement; it is not silently deleted from Custody.

Per custodian/source line:

`HandedOver = OutstandingCustody + ValidReturned + AcceptedConsumed + AuthorizedResolved`

For partial returns/consumption, the equation is maintained at line and tracking-identity level. Serial/IMEI custody must identify the exact StockUnit; fractional or pooled identity cannot substitute.

### 3.7 Deficit

`Deficit(t) = max(0, −OnHand(t))` for the applicable permitted negative-stock grain, with additional separately identified demand/count exceptions where the contract explicitly defines them.

Deficit is derived, not a new stock source. Every deficit quantity must link to its originating committed movement, Warehouse × Variant, age, ReasonCode, provisional cost source and settlement state. An unclassified reason is interim only and cannot be finally settled.

### 3.8 BookBalance and valuation

`BookBalance(t) = Accounting GL/Subledger monetary balance for the same source scope, currency, cutoff and accepted posting set`

BookBalance is not a physical quantity and must never be added to OnHand, Sellable, ATP, Custody or InTransit. Quantity and monetary valuation reconcile through the accepted valuation layers and Accounting control-account map.

- Non-serialized goods: Moving Weighted Average at ProductVariant × Warehouse.
- Serial/IMEI goods: Specific Identification by StockUnit.
- FIFO is not the general inventory valuation method; FIFO is used only where v3.0.3 explicitly requires deterministic allocation of negative-stock settlement receipts.
- WAC must not divide by zero or a non-positive denominator. Unsupported negative-value states are quarantined and fail closed.
- Negative issue requires an evidence-backed provisional cost source; if no valid source exists, reject with COST_UNDETERMINED.
- True-up is append-only, idempotent and lineage-preserving. It allocates variance between remaining stock and previously issued COGS/related expense without rewriting historical movements.
- Stocktake quantity adjustment and accounting valuation/GL adjustment are related but distinct authorized effects. No journal pair/account is inferred here.

## 4. Custody lifecycle and controls

### 4.1 Custody state machine

`REQUESTED → AUTHORIZED → HANDED_OVER → IN_CUSTODY → PARTIALLY_RETURNED / RETURNED`

Controlled terminal/exception states: `CONSUMED`, `DAMAGED`, `LOST_PENDING_REVIEW`, `RESOLVED`, `CANCELLED_BEFORE_HANDOVER`.

- REQUESTED/AUTHORIZED do not change stock.
- HANDED_OVER is the physical handover commit: warehouse stock decreases and Custody increases in one transaction.
- IN_CUSTODY records accountable possession; it is not available to ATP.
- PARTIALLY_RETURNED reduces outstanding Custody only by the exact quantity physically returned and committed.
- RETURNED requires zero outstanding quantity.
- CONSUMED requires the real authorized source event and corresponding committed issue/consumption movement; it is not a free-form custody closure.
- DAMAGED/LOST_PENDING_REVIEW remains outstanding custody until an authorized resolution movement and evidence close it.
- CANCELLED_BEFORE_HANDOVER is permitted only before physical handover and has no quantity effect.

### 4.2 Required custody record

Every Custody aggregate/line records:
- CustodyId/version, source module/document/version/line and business purpose;
- StockUnit/Variant, Lot, serial/IMEI and exact quantity/UOM/conversion version as applicable;
- source Warehouse/Section/Location and custodian PartyId/UserId;
- handover timestamp, server ActorReference, recipient acknowledgement/evidence;
- expected return date if applicable (a monitoring attribute, not an automatic stock return);
- partial return/consumption/resolution quantities and linked MovementIds;
- condition/status at handover and return, reason/evidence for damage/loss;
- permissions, approver, audit and scoped idempotency key.

The custodian identity must be a valid Persons/Identity reference; Inventory does not duplicate the Person master. The client cannot self-assert the Actor or recipient acknowledgement.

### 4.3 Custody invariants

1. OutstandingCustody cannot be negative or exceed cumulative committed handover quantity.
2. Cumulative return + accepted consumption + authorized resolution cannot exceed handed-over quantity.
3. One StockUnit cannot be simultaneously in warehouse Sellable, another custodian's outstanding Custody, or another physical location.
4. Custody handover and return movements are atomic with ledger, projection, Audit and Outbox.
5. A retry with the same identity has one effect; conflicting payload fails closed.
6. Custody is excluded from Sales ATP and ordinary Warehouse stock availability.
7. Custody transfer between custodians requires a linked handover/acceptance event; it cannot disappear between responsible parties.
8. Expected return date passing raises an overdue exception only; it does not auto-return, write off or expense the item.
9. Custody cannot be used to bypass R1 repair intake prohibition or FE-09 source validation.

## 5. Section and Warehouse transfers

### 5.1 Internal Section transfer — v3.0.3 Option B

Within one Warehouse, when a valid source Section has eligible stock and another Section has demand, the approved Option B is an automatic paired movement:
- source Section decrement;
- destination Section increment;
- common correlated transfer identity, source/destination, quantity, Variant and tracking identity;
- one transaction, one authorization context, one Audit chain;
- Warehouse OnHand, Sellable and ATP total remain unchanged except for any separately validated status/eligibility change.

If no attributable source Section exists, source SectionId remains NULL/UNLOCATED; a fabricated source Section is prohibited. This internal Section transfer is not FE-31 inter-warehouse transfer.

### 5.2 Warehouse transfer / in-transit custody

Inter-Warehouse transfer FE-31 is R2 and is not enabled by this design. Its future semantics remain:
- dispatch: source on-hand decreases; in-transit custody increases;
- destination increases only on actual accepted receipt;
- partial receipts are supported;
- discrepancy remains in-transit until formally resolved;
- same-legal-entity movement does not create revenue/expense;
- exact COA/Posting Rule requires separate Accounting authority and canonical evidence;
- cross-entity/ownership transfer is blocked pending separate Legal/Tax/Accounting disposition.

## 6. Stocktake and physical count contract

### 6.1 State machine

`DRAFT → SNAPSHOT_LOCKED → COUNTING → COUNT_SUBMITTED → [RECOUNT_REQUIRED → COUNTING] → REVIEW → APPROVED → ADJUSTMENT_POSTED → CLOSED`

Controlled alternative states: `CANCELLED` before adjustment; `REJECTED` or `RECOUNT_REQUIRED` during review. A recount is a new immutable observation; it never overwrites the earlier count.

### 6.2 Snapshot and per-observation watermark

Selected strategy: immutable cut-off snapshot plus ordered delta replay. Movements continue during counting; no silent overwrite of a live StockBalance is allowed.

- On SNAPSHOT_LOCKED, persist exact scope and a server-generated per-scope committed ledger high-watermark.
- Each physical count observation, not merely the overall stocktake, captures its own server timestamp and per-scope committed ledger watermark under a consistent PostgreSQL snapshot/locking protocol.
- Count scope identifies Warehouse, Section/Location, Variant, Lot, StockUnit/serial/IMEI and status as applicable.
- The observed quantity is bound to the exact ledger sequence at the time that specific item/location was counted.
- Movement sequence assignment and movement commit are serialized at the required scope. An uncommitted movement/sequence cannot appear in a count watermark; a rolled-back transaction cannot leave a sequence interpreted as a committed movement.
- Backdated OccurredAtUtc does not reorder the append-only ledger. A late-arriving event whose business-effective claim precedes the observation watermark becomes an explicit exception requiring investigation/recount or a separately authorized adjustment; it is not silently inserted before the watermark.

### 6.3 Count and variance equations

For each count observation at watermark W:

`ExpectedAtObservation(W) = Replay(OpeningBalance, all committed movements with sequence ≤ W)`

`ObservedVariance(W) = PhysicalCount(W) − ExpectedAtObservation(W)`

At reconciliation cutoff C, replay movements committed after W through C:

`BookAtCutoff(C) = Replay(OpeningBalance, all committed movements with sequence ≤ C)`

`ExpectedAfterCountAtCutoff(C) = BookAtCutoff(C) + ObservedVariance(W)`

Equivalently:

`ReconciledVarianceAtCutoff = PhysicalCount(W) + Σ PostObservationNetMovements(W,C) − BookAtCutoff(C)`

The result must equal the original observation variance, unless a separately documented late/backdated event or scope change creates an exception. Do not compare an old physical count directly to a moving live projection without replaying the intervening committed movement delta.

- For serialized goods, count exact StockUnit/IMEI identities; aggregate quantity alone cannot conceal missing, duplicated or substituted units.
- For Lot-tracked goods, count and reconcile by Lot.
- For location-tracked goods, count each required location and reconcile to Warehouse; do not add Warehouse aggregate to location rows.
- Counted quantity is expressed in base UOM using the snapshot conversion version. Invalid precision/increment is rejected; display rounding has no effect.
- A recount produces a new CountObservationId, watermark and evidence. The selected accepted observation and rejected/superseded observation lineage remain visible.

### 6.4 Review and adjustment

- Count alone is observation only; it never changes OnHand, ATP, Custody or BookBalance.
- Variance requires reason, source evidence, counter, reviewer and authorized approver. Separation of Duties applies; a creator/counter cannot approve their own adjustment where SoD requires independent approval.
- Only after approval may Inventory commit a separate immutable InventoryAdjustment movement for the accepted variance.
- The adjustment records source StocktakeId/CountLineId/ObservationId, original expected quantity, observed quantity, approved variance, cutoff/watermark, reason, actor, approver and idempotency identity.
- The adjustment cannot exceed the approved variance or cumulatively reverse more than its source movement.
- Rejected/cancelled count has no quantity effect. An adjustment reversal is a new linked movement and cannot exceed the original net adjustment.
- Any required accounting valuation/GL effect is a separate Accounting-owned contract. No automatic expense, income, suspense or COA mapping is created by this document.

## 7. Negative stock and settlement boundary

The v3.0.3 D-258 rules are reproduced without broadening:

| Tracking/UOM class | Default permitted shortage |
|---|---:|
| Non-serialized, integer/counting UOM | -2 |
| Non-serialized, decimal UOM | 0 |
| Serial/IMEI/mandatory tracking | 0; negative absolutely prohibited |

- The negative-stock Feature Flag remains OFF until its required Inventory/Finance Gate and tests are accepted.
- Any permitted shortage is checked and locked at Warehouse × Variant across all Sections, not separately per Section.
- If the configured limit is reduced below current deficit, new issue/sale is blocked until the balance returns inside the limit or a permissioned, audited temporary override is applied. No silent balance adjustment.
- A valid negative issue requires a traceable provisional cost source; otherwise COST_UNDETERMINED blocks the issue.
- Only the five v3.0.3 ReasonCodes are valid for final settlement: RECEIPT_NOT_POSTED, TRANSFER_NOT_POSTED, CONSUMPTION_NOT_POSTED, COUNT_ERROR, DAMAGE_OR_LOSS. UNCLASSIFIED is interim only. SUPPLIER_RETURN_NOT_POSTED is not an authorized sixth reason.
- NegativeStockSettlementBatch is unique, source-linked, auditable and may be partially settled. Receipt allocation uses FIFO only for shortage settlement as explicitly directed by v3.0.3; this does not change the general WAC valuation policy.
- Age over 30 days creates a warning; it does not auto-write-off, auto-close or auto-settle a shortage.

## 8. Valuation and inventory tie-out

### 8.1 Quantity tie-out

For each Warehouse × Variant × tracking/status grain:

`OpeningOnHand + Σ CommittedInbound − Σ CommittedOutbound = ClosingOnHand`

For each Warehouse:

`WarehouseOnHand = Σ mutually exclusive location buckets (or UNLOCATED bucket)`

For custody:

`OpeningCustody + Handovers − Returns − AcceptedConsumption − AuthorizedResolutions = ClosingCustody`

For warehouse transfers:

`Dispatched = AcceptedReceived + OutstandingInTransit + FormallyResolvedDiscrepancy`

For reservations:

`Requested = Issued + ActiveReservedRemaining + ReleasedOrCancelled + UnreservedRemainder`

For ATP:

`ATP = Sellable − EffectiveReserved`

For stocktake:

`CountedAtObservation + PostObservationNetMovements = ExpectedBookAtCutoff + AcceptedUnpostedVariance`

The accepted adjustment movement moves the accepted variance into the movement ledger; after posting, the adjusted projection must reconcile to replay.

### 8.2 Valuation tie-out

`OpeningInventoryValue + AcceptedReceiptCost + EligibleLandedCost/TrueUp + ReturnValue − Issue/COGSValue − SupplierReturnValue ± AuthorizedValuationCorrections = ClosingInventoryValue`

The inventory value projection must reconcile to source ValuationLayers/Allocations and the accepted Accounting control-account mapping at the same cutoff. 110801 remains the sole Inventory GL control account; 110802–110806 are inventory subledger classifications, not independent GL controls. No in-transit account is selected here.

### 8.3 Rebuild and exception behavior

- Rebuild StockBalance and all derived buckets from the immutable committed movement ledger plus versioned reservation/custody/valuation facts.
- Replay must be deterministic and produce the same projection for the same source set, versions and cutoff.
- Any missing source, duplicate identity, invalid UOM conversion, inconsistent StockUnit/IMEI, negative status bucket, over-reversal, unbalanced transfer/custody or unexplained ledger/projection difference creates a visible exception and blocks the affected operation/close path.
- Never auto-net physical and accounting differences, never mutate a committed source movement, and never conceal a difference with an unreferenced adjustment.

## 9. Required logical entities and constraints

| Entity | Required purpose / key references |
|---|---|
| StockMovement / StockMovementLine | immutable committed ledger, source/version/line, direction, quantity, UOM conversion, tracking, location/status, actor, reason, idempotency |
| StockPosition / StockBalance | rebuildable projection by canonical stock grain; never parent/source of ledger history |
| StockUnit / UnitIdentifier | physical serialized unit and active/historical Serial/IMEI identity; dual IMEI on one unit allowed |
| Lot / StockUnitLot | Lot and StockUnit membership with Variant/receipt lineage |
| Reservation / ReservationLine | source line, type, effective TTL, requested/consumed/released remainder, state |
| Allocation / Pick | workflow subsets of reservation; do not double-subtract ATP |
| Custody / CustodyLine / CustodyEvent | source, custodian, exact unit/quantity, handover, partial return, consumption, resolution, acknowledgement and audit |
| Transfer / TransferLine | Section paired transfer or separately gated Warehouse transfer; dispatch/receipt/in-transit/discrepancy |
| Stocktake / CountObservation / CountLine | immutable scope, snapshot and per-observation watermark, physical count, recount lineage, variance/reason/review/approval |
| InventoryAdjustment | separate immutable movement linked to approved CountLine/Observation |
| ValuationLayer / ValuationAllocation / TrueUp | cost source, WAC/Specific ID, movement lineage, retained vs issued split, deterministic version |
| InventoryAudit / InventoryOutbox | immutable audit and transactional event publication |

Physical schema, exact column types, index design and migrations remain subject to the separately authorized implementation phase.

## 10. Acceptance tests and evidence

Existing INV-T-001..077 remain applicable as registered. This stage adds the following explicit tests; the full test set is therefore 78 IDs, not 78 passing tests.

| Test ID | Scenario | Required assertion |
|---|---|---|
| INV-T-078 | Per-observation stocktake watermark | Every count observation captures its own committed ledger sequence under a consistent DB snapshot; reconciliation replays only later committed movements; pre-observation movements are excluded; concurrent count/movement race is serialized; rollback cannot expose an uncommitted sequence |
| INV-T-079 | OnHand/Sellable bucket conservation | Status transfers preserve physical total; Sellable is a subset of OnHand |
| INV-T-080 | Custody handover | Warehouse quantity decreases and Custody increases atomically; ATP excludes handed-over quantity |
| INV-T-081 | Partial custody return | Only returned committed quantity leaves Custody and returns to the proper inspection/status bucket |
| INV-T-082 | Custody consumption | Custody closes only against valid source/committed consumption; no free-form close |
| INV-T-083 | Custody loss/damage | Remains outstanding until authorized evidence and resolution movement |
| INV-T-084 | Custodian-to-custodian handoff | Paired acceptance preserves outstanding Custody and full audit lineage |
| INV-T-085 | Custody/ATP non-duplication | Custody is neither reservation nor warehouse Sellable; no double count/deduction |
| INV-T-086 | Location/UNLOCATED reconciliation | Warehouse equals mutually exclusive location buckets or UNLOCATED; aggregate is not double-counted |
| INV-T-087 | Section transfer atomic pair | Source/destination paired movement commits all-or-none; Warehouse total unchanged |
| INV-T-088 | Warehouse transfer discrepancy | Discrepancy remains in-transit until formal resolution |
| INV-T-089 | Stocktake delta replay | Count variance at observation watermark plus later movements reconciles to cutoff without lost/double-applied movement |
| INV-T-090 | Stocktake recount lineage | Recount creates new observation/watermark; prior count remains immutable and traceable |
| INV-T-091 | Serialized stocktake | Exact StockUnit/IMEI identities reconcile; duplicate/substituted unit is detected |
| INV-T-092 | Stocktake adjustment authorization | Count alone has no effect; only approved separate adjustment movement changes ledger |
| INV-T-093 | Late/backdated event after observation | No silent ledger reorder; explicit exception/recount or authorized adjustment |
| INV-T-094 | Quantity projection replay | Rebuilt projection exactly equals deterministic committed ledger replay |
| INV-T-095 | Concurrent custody/issue/return | Locking prevents same unit/quantity from being simultaneously issued, returned or in custody |
| INV-T-096 | Negative threshold cross-Section | Warehouse × Variant lock enforces one shared threshold across all Sections |
| INV-T-097 | Deficit settlement reason | Only five v3.0.3 reason codes can finally settle; UNCLASSIFIED remains interim |
| INV-T-098 | Status-bucket non-negativity | Status transfer cannot create negative source bucket or change physical total |
| INV-T-099 | Movement reversal bound | Cumulative reversals cannot exceed original committed quantity/value |
| INV-T-100 | WAC and valuation tie-out | WAC/Specific ID, True-up and integer-IRR value reconcile to source layers and GL control evidence |
| INV-T-101 | Atomic ledger/projection/audit/outbox | Failure at each write boundary leaves no partial movement, projection, audit or outbox |
| INV-T-102 | Idempotent replay | Same event replay has one effect; conflicting payload is rejected and audited |

Required runtime evidence:
- actual target PostgreSQL, not only mocks/InMemory;
- exact source/build/test SHA and schema/migration fingerprint;
- multiple independent DB connections for races and locks;
- query evidence for movement ledger, projections, custody, reservation, stocktake watermark, valuation, audit and outbox;
- rollback and crash-injection at each transaction boundary;
- replay/rebuild equality and warehouse/location/custody/transfer conservation;
- permissions/grants and Actor spoofing denial;
- backup/restore and deterministic replay;
- independent review on the exact tested SHA.

All tests above remain NOT_RUN until actual evidence is produced. A structural documentation validator is not a runtime PASS.

## 11. Open gates and explicit non-authorization

| Item | Current disposition |
|---|---|
| Semantic inventory equations | COMPLETED IN THIS DESIGN |
| Custody lifecycle and conservation | COMPLETED IN THIS DESIGN |
| Stocktake snapshot + per-observation watermark + delta replay | COMPLETED IN THIS DESIGN |
| Logical model refinements and test mapping | COMPLETED IN THIS DESIGN |
| Exact PostgreSQL numeric precision/scale and UOM increments | Follow approved technical numeric decision; implementation evidence pending |
| Physical ERD/schema/API/EF model | NOT IMPLEMENTED / NOT VERIFIED |
| Runtime Inventory movement/custody/stocktake | NOT IMPLEMENTED / NOT VERIFIED |
| INV-T-001..102 runtime execution | NOT RUN |
| Independent exact-SHA review | PENDING |
| O-35 | DESIGN SPECIFIED; technical closure requires exact-SHA PostgreSQL/race/replay/tie-out evidence |
| O-36 Sales TTL | Configurable semantics retained; final value remains OPEN; Repair no automatic TTL |
| Negative-stock feature | OFF; separate gate remains required |
| Warehouse transfer FE-31 | R2 / NOT ENABLED |
| Accounting Posting Rules / in-transit COA | Not selected or authorized here |
| B-final / Gate C / Gate D | OPEN |
| Migration / Posting Release / production / Go-Live | NOT GRANTED |

### Final disposition

Stage 05 is complete as an integrated v3.0.3-aligned inventory design contract for quantity conservation, custody and stocktake. It closes the semantic/design gaps in the workpaper, but it does not claim physical implementation, runtime correctness, PostgreSQL test PASS, O-35 Gate closure or operational release.

**END — MDG-INVENTORY-STAGE-05-EQUATIONS-CUSTODY-STOCKTAKE-001 v0.1**
