# Inventory Source Event and Stock Ownership Contract

| Field | Value |
|---|---|
| Document ID | INVENTORY-SOURCE-EVENT-CONTRACT-PROPOSAL-001 |
| Version | 0.3 |
| Status | OWNER-DECISION SUCCESSOR — TECHNICAL OWNER ACCEPTANCE / BASELINE RECONCILIATION REQUIRED |
| Authority | OWN-INV-018 v1.0 (remaining decisions); OWN-INV-019 v1.0 (follow-up decisions); OWN-FE-01-02-001 v1.0; MDG-ARCH-MASTER-001 v3.0.3 |
| Supersedes | v0.2 only for the explicit owner-decision amendments in OWN-INV-019; v0.1/v0.2 retained as historical snapshots |

## 1. Authority and scope

Data ownership classification: STK-01 — Inventory. Cross-module source-line keys include SalesLineId and RepairServiceLineId; these are immutable references, not Inventory-owned source records.
This proposal incorporates the owner's approval of INV-EVT-001..018 dated 2026-11-18. It resolves D-213 and D-215. Business policy is approved; technical contracts remain subject to acceptance by all named module owners. This is not an executable schema, migration, runtime contract, Posting Rule or release.

## 2. Ownership
Inventory exclusively owns on-hand quantities, committed movement ledger, reservations/releases, allocations, picking, transfers and in-transit custody, physical returns/inspection/disposition, stocktakes/adjustments, valuation/cost layers/True-up, tracking identity and stock reporting. All writes pass through Inventory contracts. No other module may write Inventory tables, balances, movements, status or valuation.

Organization owns warehouse/location master data and hierarchy Organization→Warehouse→Section→Shelf→Bin/StorageLocation. Catalog owns Product, Variant/SKU, tracking policy and UOM. Purchasing owns purchase orders/terms; Sales owns commercial documents/lines, return entitlement and customer handover acknowledgment; RepairOperations owns repair workflow and RepairServiceLine; Accounting owns RecognitionTime, COA, Posting Rules, Journal/GL and COGS; Treasury owns payments/settlements/refunds.

## 3. Warehouse, location and stock identity
D-213: Warehouse is the primary stock/shortage control scope; StorageLocation is physical traceability. Warehouse balance must reconcile to tracked location balances. Internal location transfer changes location balances only. Organization creates/activates/deactivates warehouse/location records; Inventory validates active references and actor scope and preserves historical references.

D-215: Variant/SKU is the primary stock identity for non-serialized stock; Product is a classification parent. Serialized/IMEI stock additionally identifies a physical StockUnit. Stock position dimensions are Warehouse + applicable Location + Variant + applicable TrackingIdentity + StockStatus. Shelf/bin tracking is policy-dependent, not mandatory for every item.

## 4. Tracking, quantity and UOM
Catalog declares Quantity, Serial, IMEI and Lot/Batch tracking, including combined Lot+Serial where required. A dual-SIM device may retain two IMEIs for one StockUnit; one active IMEI cannot identify two units. Serial uniqueness uses an approved manufacturer/type/model namespace where global uniqueness is not guaranteed. Corrections require authorization, reason and immutable audit. Lot-tracked issue must reference a valid Lot.

Catalog defines one base UOM per Variant. Inventory stores base quantity as Decimal; float/double are prohibited. Conversion factors are versioned and apply prospectively only. Base UOM cannot be directly changed after movements exist. UOM fractional precision, Decimal precision/scale, conversion rounding and overflow are mandatory technical parameters requiring owner approval before implementation. Display rounding never changes stored quantity.

## 5. Reservation and availability
Reservation, allocation, picking and committed issue are distinct. Confirmed Sales reservation defaults to a 24-hour TTL from server-confirmed creation; policy is configurable/versioned and snapshotted per reservation, and changes affect new reservations only. Repair reservation has no automatic TTL (ExpiryMode=NONE); Transfer reservation follows transfer lifecycle; temporary operational reservation has policy-specific TTL. Expiry releases only remaining reservation and cannot mutate finalized Sales documents or committed movements.

Maintain PhysicalOnHand, Reserved, Allocated, Picked, Issued, InTransit and stock-status buckets separately. ATP is Sellable − EffectiveReserved. QualifiedInbound and unreceived/transfer-in-transit quantities are not included in ATP. Pending-inspection, defective, quarantined and unusable stock is excluded.

## 6. Negative stock
Preserve the approved D-258 policy: non-serialized shortage default is -2 at Variant/Warehouse scope only for the permitted non-serialized integer-UOM class; Serial/IMEI stock cannot go negative under any circumstance. GENERAL_MANAGER may change the limit only for explicitly assigned WarehouseId×VariantId pairs. Scope creation, expansion, reassignment or amendment requires explicit Product/Business Owner approval and an immutable Audit record. GENERAL_MANAGER alone does not create global authority and cannot self-assign or expand scope. SYSTEM_MANAGER alone and Emergency Session alone are denied. Other D-258 item classes remain at zero eligibility; the negative-stock Feature Flag remains OFF. Shortage requires a real source movement; no synthetic receipt/StockUnit. Backorder is not stock. Open shortage older than 30 days warns but is not automatically expensed/closed; settlement requires authorized approval and traceable receipt/adjustment. -2 is a default threshold, not a general bypass.

## 7. Movement semantics and links
Movement categories: OpeningBalance, GoodsReceipt, SalesIssue, RepairMaterialIssue, InternalLocationTransfer, WarehouseTransferDispatch/Receipt, CustomerReturnReceipt, SupplierReturnDispatch, InventoryAdjustment, StockStatus/DispositionChange and WriteOff/Scrap. Each defines direction, source/destination, evidence, commit boundary, allowed transitions and reversal. Purchase order, invoice, reservation, allocation, pick or count alone does not create committed stock movement.

Sales issue links to immutable SalesDocument/version and SalesLine/version with quantity-level partial/multiple fulfillment. Sales owns handover acknowledgment; Inventory owns committed physical issue. Neither alone proves transfer of control/RecognitionTime.

Repair issue requires a real authorized RepairServiceLine/source. RepairOperations retains movement reference only. FE-09 remains controlling. R1 repair intake is disabled; no synthetic RepairCase/ServiceLine or bypass; absent a valid source, repair issue/posting is unavailable in R1.

## 8. Transfers and in-transit stock
Transfer lifecycle supports DRAFT, REQUESTED, APPROVED, DISPATCHED, IN_TRANSIT, PARTIALLY_RECEIVED, RECEIVED, CLOSED and controlled CANCELLED/DISCREPANCY states. Dispatch decreases source and creates in-transit custody; destination increases only by actual accepted receipt. Dispatched = received + in-transit + formally resolved discrepancy. Internal same-entity transfer creates no revenue/expense. Accounting must approve exact in-transit COA account and Posting Rule from canonical evidence; no account code is inferred. Cross-entity transfers need separate approval.

## 9. Returns, stocktake and corrections
Customer return first enters RETURNED_PENDING_INSPECTION, excluded from sellable ATP. Authorized Inventory disposition: SELLABLE, DEFECTIVE, QUARANTINED or UNUSABLE. Physical return, commercial entitlement, revenue/tax reversal and refund are separate. Missing original issue/cost lineage creates an exception; current price is not a substitute.

Stocktake requires scope, immutable snapshot/cut-off, physical count, variance, reason, review, authorized approval and separate adjustment movement. Count alone never changes stock. Concurrent movement requires a safe freeze/cut-off/delta policy. Movements are append-only; corrections/reversals are linked new movements.

## 10. Valuation and True-up
Approved methods: Moving Weighted Average for non-serialized goods; Specific Identification for Serial/IMEI goods; FIFO is not the primary method. Eligible committed receipt updates moving average. Serialized StockUnits retain independent cost. Only approved directly attributable landed costs are capitalized; excluded examples include general administration, selling, penalties and abnormal waste. Exact eligibility/allocation, precision and rounding require Inventory+Accounting acceptance.

Returns use original issue valuation when traceable; no guessed/current-price fallback. True-up links purchase, receipt, Variant/StockUnit/Lot and original valuation version; splits difference between remaining stock and previously issued quantities; retained share adjusts inventory value and issued share adjusts COGS/related expense lineage. True-up is deterministic, high-precision, idempotent and append-only. Negative non-serialized stock uses evidence-based provisional valuation, never zero/guessed cost; later valid receipt/cost resolves it by linked True-up. Accounting retains GL authority.

## 11. Event, transaction and delivery
Versioned event fields include EventId, EventType, ContractVersion, AggregateId/Version, immutable source document/version/line, timestamps, server ActorReference, CorrelationId, CausationId, IdempotencyKey, Warehouse/Location, Variant, TrackingIdentity, quantity/UOM/conversion, movement/status and valuation/evidence references as applicable. Same identity+same canonical payload is idempotent; same identity+different payload fails closed and is audited.

Movement ledger, projection, Audit and Outbox commit in one Inventory database transaction. Failure rolls back all. Publish after commit; at-least-once delivery; idempotent consumers; no exactly-once transport claim. Projection is rebuildable from immutable movement history. Database constraints, concurrency locks/versioning and stable lock order are mandatory.

The original INV-EVT-001..018 dispositions remain recorded in OWN-INV-018 v1.0 except the threshold-authority wording in INV-EVT-012, which is superseded by OWN-INV-019 v1.0 (O-64-SCOPE). OWN-INV-019 also records the D-15 cutover guard, WarehouseId watermark and Sales TTL owner decisions.

## 12. Technical acceptance and status
All 18 owner decisions are business-approved. Technical acceptance remains open for Inventory and the relevant owners: Organization, Catalog, Purchasing, Sales, RepairOperations, Accounting, Security, Platform/DB and QA. The three design outputs are separately registered: ERD/Data Dictionary v0.1, State/Movement v0.1 and Test Specification v0.1. Sales TTL default and negative-stock authority scope are owner-decided in OWN-INV-019. Other technical parameters, Catalog namespace acceptance, landed-cost/True-up allocation, count cut-off implementation and exact in-transit COA mapping remain subject to their separate owner/technical acceptance.

Inventory runtime/persistence/SQL/migrations: NOT IMPLEMENTED / NOT VERIFIED. Tests: NOT RUN. FE-01/FE-02 remain BLOCKED under their independent gates; FE-09 remains controlling. Accepted positive Posting Rules: 0. Migration 0009 remains reserved by PR #70. Posting Release: NOT GRANTED. Operational release: NOT GRANTED.

## 13. Owner follow-up decision reconciliation — OWN-INV-019 v1.0

This v0.3 proposal updates the source-event proposal's Sales TTL and INV-EVT-012 threshold-authority wording to the explicit owner decisions. It preserves the separation of business policy, module technical acceptance, runtime implementation and release authorization.

The WarehouseId committed watermark and D-15 dual-budget cutover guard are governed by OWN-INV-019 v1.0 and the successor Inventory PDS v0.4; they do not create a physical schema or authorize database implementation.

No SQL, DDL, EF mapping, migration, GRANT/REVOKE, API/runtime/worker code, Inventory activation, negative-stock Feature Flag activation, Posting Rule/Release, production migration or Go-Live is authorized by this proposal. All technical acceptance and runtime tests remain open.

**END — INVENTORY-SOURCE-EVENT-CONTRACT-PROPOSAL-001 v0.3**
