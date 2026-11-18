# Inventory ERD and Data Dictionary — Design Proposal
Version: 0.1 | Status: DESIGN PROPOSAL — OWNER REVIEW REQUIRED
Authority: OWN-INV-018 v1.0; Inventory contract v0.2; MDG-ARCH-MASTER-001 v2.5.

## 1. Conceptual ERD (not physical schema)
```text
Organization 1-* Warehouse 1-* StorageLocation
Catalog Product 1-* Variant 1-1 BaseUOM
Variant 1-* StockPosition 1-* StockMovementLine
Variant 1-* StockUnit 1-* UnitIdentifier(Serial/IMEI)
Variant 1-* Lot
StockMovement 1-* StockMovementLine
Reservation 1-* Allocation
Transfer 1-* TransferLine 1-* Dispatch/Receipt MovementLine
ReturnCase 1-* ReturnLine 1-* Inspection/Disposition
Stocktake 1-* CountLine 0..1 Adjustment Movement
PurchaseReceiptLine 1-* ValuationLayer 1-* ValuationAllocation
TrueUp 1-* TrueUpAllocation(retained stock / previously issued)
Movement transaction: Ledger + Projection + Audit + Outbox (atomic)
```
Conceptual relationships only. Organization and Catalog masters are referenced, not copied or owned by Inventory.

## 2. Candidate entities / dictionary
| Entity | Purpose and principal fields |
|---|---|
| WarehouseRef | Organization-owned WarehouseId, OrganizationId, status/version; active and actor-authorized for new movements |
| StorageLocationRef | Organization-owned StorageLocationId, WarehouseId, parent/type/status/version; Warehouse→Section→Shelf→Bin |
| VariantRef | Catalog-owned VariantId/SKU, ProductId, base UOM, tracking mode/version; stock identity for non-serialized items |
| UomConversionRef | Catalog conversion ID/version, source/base UOM, exact factor, effective scope, permitted precision |
| StockPosition | Inventory projection: Warehouse, optional Location, Variant, Status, optional Lot/StockUnit; ledger-derived |
| StockUnit | One physical serialized unit, Variant, unit state and independent cost identity |
| UnitIdentifier | Serial/IMEI type, normalized value, approved namespace and active interval; dual IMEI allowed per StockUnit |
| Lot | LotId, VariantId, lot code, source receipt and lineage |
| StockMovement | Immutable MovementId/type/source/version/state/Actor/timestamps/reason/idempotency |
| StockMovementLine | LineId, movement, Variant, source/destination warehouse/location/status, positive quantity, base qty, UOM conversion, Lot/StockUnit |
| Reservation | ReservationId/type/source line/quantity/expiry policy/state; no on-hand effect |
| Allocation | AllocationId, ReservationId, source line, quantity and state; distinct from issue |
| Transfer / TransferLine | Source/destination, requested/dispatched/received/in-transit/discrepant quantities |
| ReturnCase / ReturnLine | Original issue/SalesLine, returned quantity and pending-inspection state |
| Inspection | Item/Lot/serial, result, Actor, timestamp, reason/evidence |
| Stocktake / CountLine | Scope, snapshot/cut-off, system qty, counted qty, variance, reason and approval |
| ValuationLayer | Receipt/movement ref, method/version, quantity/value/currency and cost basis |
| ValuationAllocation | Source layer, target movement line, quantity/value and algorithm version |
| TrueUp / TrueUpAllocation | Unique source adjustment; retained inventory vs issued COGS/expense split |
| InventoryAudit | Immutable action, aggregate/movement refs, server Actor, timestamp, reason/evidence |
| InventoryOutbox | EventId, contract version, payload digest and retry/publication state |

## 3. Relationship and integrity rules
1. Organization owns Warehouses and StorageLocations; Inventory validates references and actor scope.
2. Product has Variants; Variant/SKU is stock identity for non-serialized goods (D-215).
3. StockPosition = Warehouse + applicable Location + Variant + applicable TrackingIdentity + StockStatus (D-213).
4. A movement has one or more lines; quantity magnitude is positive and direction is controlled by MovementType.
5. One SalesLine can map to many partial fulfillment lines with exact line-level quantities.
6. Repair issue references a real authorized RepairServiceLine/source; no R1 synthetic record.
7. Transfer conservation: dispatched = received + in-transit + formally resolved discrepancy.
8. Return receipt is pending inspection until authorized disposition.
9. CountLine is an observation; only approved Adjustment Movement changes stock.
10. Valuation allocations reconcile source layer quantity/value under accepted precision/rounding.
11. TrueUp has unique source/version identity and retained/issued allocations.
12. Projection is rebuildable from committed movement ledger.

## 4. Mandatory logical constraints
- One active normalized IMEI cannot identify two StockUnits; dual IMEI on one StockUnit is allowed.
- Active serial uniqueness is scoped to accepted manufacturer/type/model namespace.
- Variant—not Product alone—is the authoritative non-serialized stock key.
- Serialized quantity reconciles to distinct eligible StockUnits; Lot-tracked issue requires LotId.
- Unique EventId and scoped IdempotencyKey; conflicting payload reuse fails closed.
- Cumulative SalesLine fulfillment cannot exceed eligible quantity.
- Cumulative transfer receipt cannot exceed dispatch; cumulative reversal cannot exceed original.
- Reservation never increments physical on-hand; count never changes ledger by itself.
- Warehouse/location and valuation reconciliations are enforceable.
- Non-Inventory module principals have no direct write grants.

## 5. Data conventions and open parameters
Identifiers are opaque stable IDs; exact ID strategy is not selected. Times are UTC; OccurredAtUtc and RecordedAtUtc differ. Quantity is Decimal, never float/double. ActorReference is server-derived. Source references include module/document/version/line. Status and MovementType are controlled versioned vocabularies.

Before physical schema: approve Decimal precision/scale and UOM fractions; serial namespace; location granularity; count freeze/cut-off/delta replay; TrueUp allocation/rounding; indexes/partitioning; exact in-transit COA mapping. No SQL, migration or EF mapping is authorized.
