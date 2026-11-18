# Inventory State and Movement Specification — Design Proposal
Version: 0.1 | Status: DESIGN PROPOSAL — OWNER REVIEW REQUIRED
Authority: OWN-INV-018 v1.0; Inventory contract v0.2; MDG-ARCH-MASTER-001 v2.5.

## 1. Principles
Movement, reservation, transfer, return, stocktake, disposition and valuation use separate state machines. A transition is an authorized command producing an immutable fact. Projections are rebuildable. Reservation, allocation, picking, custody and committed issue are distinct. Only COMMITTED movement changes authoritative stock.

## 2. Movement lifecycle and commit boundaries
Common candidate: DRAFT → REQUESTED → VALIDATED → APPROVED (where required) → COMMITTED → PARTIALLY_REVERSED/REVERSED. A committed movement is never edited/deleted; correction is a linked new movement.

| Type | Commit condition | Effect |
|---|---|---|
| OpeningBalance | Controlled opening document and evidence approved | Establish documented baseline |
| GoodsReceipt | Actual receipt counted; accepted/rejected qty recorded | Accepted quantity increases receiving stock |
| SalesIssue | Authorized SalesLine, quantity, tracking and warehouse validated; physical issue committed | Source stock decreases; fulfillment line created |
| RepairMaterialIssue | Real authorized RepairServiceLine/source; FE-09/R1 guard passes | Stock decreases; movement reference returned |
| InternalLocationTransfer | Valid source/destination locations in same warehouse | Location balances change; warehouse total unchanged |
| WarehouseTransferDispatch | Approved transfer and source dispatch committed | Source decreases; in-transit increases |
| WarehouseTransferReceipt | Actual accepted destination qty confirmed | In-transit decreases; destination increases only accepted qty |
| CustomerReturnReceipt | Physical return received | Adds RETURNED_PENDING_INSPECTION only |
| SupplierReturnDispatch | Authorized actual supplier dispatch | Stock decreases |
| InventoryAdjustment | Count variance, reason and approval complete | Separate correction movement |
| Status/DispositionChange | Inspection/authorized decision | Changes eligibility/status, not physical qty unless custody changes |
| WriteOff/Scrap | Authorized disposal evidence/removal | Reduces relevant stock |

Purchase order, invoice, reservation, pick-list and count alone do not commit stock.

## 3. Reservation state machines
Candidate states: REQUESTED → ACTIVE → PARTIALLY_ALLOCATED → ALLOCATED → PARTIALLY_FULFILLED → FULFILLED; alternatives RELEASED, CANCELLED, EXPIRED. Transitions are type-specific.
- Sales: configurable TTL from business settings; expiry releases remaining reservation only.
- Repair: no automatic TTL; release only on valid workflow/cancellation/authorized decision.
- Transfer: lifecycle-bound; release on approved cancel/dispatch/receipt/discrepancy resolution.
- Temporary operational: purpose-specific configurable TTL.
Expiry is idempotent/race-safe with issue/release and cannot alter finalized Sales docs or committed movements. Exact Sales TTL default remains unresolved.

## 4. Quantity buckets and ATP
Track PhysicalOnHand, EligibleSellableOnHand, Reserved by type, Allocated, Picked, Issued, InTransit, PendingInspection, Defective, Quarantined, Unusable and Shortage separately. ATP = EligibleOnHand + QualifiedInbound − ActiveReservations. QualifiedInbound includes approved future states only. Pending-inspection/defective/quarantined/unusable are excluded. Warehouse is shortage-control scope; location is traceability; location totals reconcile to warehouse.

## 5. Negative stock
Non-serialized default shortage threshold is -2 at Variant/Warehouse scope. System Manager alone may change it with permission, reason, scope, Actor and Audit. Serial/IMEI cannot go negative and has no override. Shortage requires real source movement; no synthetic receipt/unit. Backorder is not stock. More than 30 days open triggers warning only; no auto-expense/closure. Settlement requires authorized approval and traceable receipt/adjustment.

## 6. Transfers and in-transit
States: DRAFT → REQUESTED → APPROVED → DISPATCHED → IN_TRANSIT → PARTIALLY_RECEIVED → RECEIVED → CLOSED, with controlled CANCELLED/DISCREPANCY_REVIEW alternatives. Partial receipts allowed. Dispatched = accepted received + in-transit + formally resolved discrepancy. 10 dispatched/9 received means 1 remains in transit/discrepancy; no destination credit for missing unit. Same-entity transfer is not revenue/expense. Accounting must approve exact in-transit COA and Posting Rule; no code inferred.

## 7. Returns
RECEIVED → RETURNED_PENDING_INSPECTION → SELLABLE | DEFECTIVE | QUARANTINED | UNUSABLE. Pending inspection is physical but not sellable/ATP. SELLABLE requires authorized Inventory inspection. UNUSABLE disposal/write-off requires separate evidence. Physical return, commercial entitlement, revenue/tax reversal and refund remain independent. Missing original cost lineage blocks auto-valuation.

## 8. Stocktake
PLANNED → SNAPSHOTTED → COUNTING → COUNT_SUBMITTED → VARIANCE_REVIEW → APPROVED → ADJUSTED → CLOSED; controlled CANCELLED path before adjustment. Snapshot has scope and exact cut-off. Concurrent movement requires safe freeze/lock or cut-off plus delta replay. Count is observation only; approved adjustment is a separate movement with variance, reason, approver and audit.

## 9. Valuation and True-up
Moving Weighted Average applies to non-serialized; Specific Identification to Serial/IMEI; FIFO is not primary. Transfers carry cost without artificial profit; returns use original issue valuation when traceable. True-up: PENDING_FINAL_COST → CALCULATED → REVIEWED (if required) → COMMITTED → RECONCILED. It links purchase/receipt/Variant or StockUnit and original valuation version; splits difference between retained inventory and previously issued COGS/expense; deterministic, append-only and idempotent. Negative stock uses evidence-based provisional cost, never zero/guessed cost. Landed-cost eligibility, precision and allocation/rounding require owner approval.

## 10. Atomicity and module boundaries
Movement ledger + projection + Audit + Outbox persist in one DB transaction; failure rolls back all. Publish after commit; at-least-once delivery; idempotent consumers. Controls cover warehouse/Variant availability, reservation race, IMEI/serial uniqueness, SalesLine fulfillment, transfer receipt, count cut-off, reversal and TrueUp. Stable lock order, DB constraints and PostgreSQL evidence are mandatory. Organization/Catalog/Purchasing/Sales/RepairOperations/Accounting retain approved ownership; no direct table access. R1 repair intake disabled; FE-09 controls repair issue.

Open before implementation: Decimal precision/UOM rules, Sales TTL default, serial namespace, landed-cost/TrueUp allocation and rounding, count cut-off strategy, exact in-transit COA mapping and cross-entity transfer policy.
