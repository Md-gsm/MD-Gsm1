# OWN-INV-018 — Comprehensive Inventory Policy Decision

| Field | Value |
|---|---|
| Decision ID | OWN-INV-018 |
| Version | 1.0 |
| Status | APPROVED — BUSINESS OWNER POLICY |
| Decision date | 2026-11-18 |
| Scope | INV-EVT-001 through INV-EVT-018; D-213; D-215 |
| Architecture baseline | MDG-ARCH-MASTER-001 v2.5 |
| Related policy | OWN-FE-01-02-001 v1.0 |
| Execution status | NOT_VERIFIED |
| Test status | NOT_RUN |
| Operational release | NOT_GRANTED |

## 1. Decision

The business owner approves all 18 Inventory policy decisions as detailed in the controlled decision package dated 2026-11-18. This record is the concise authority index; the detailed accepted requirements are decomposed into the Inventory source-event contract v0.2 and the three design specifications registered alongside it.

| ID | Approved disposition |
|---|---|
| INV-EVT-001 | Inventory exclusively owns stock quantities, committed movements, reservations, allocation, transfers, physical returns/disposition, counts/adjustments, valuation evidence and stock reporting. Other modules must use Inventory contracts; direct table writes are prohibited. |
| INV-EVT-002 | Organization owns warehouse/location master data and hierarchy Organization → Warehouse → Section → Shelf → Bin/StorageLocation. Inventory owns stock balances/movements at applicable tracking granularity. Warehouse is the primary shortage-control scope; location is physical traceability. Internal location transfer does not change warehouse total. |
| INV-EVT-003 | Quantity, Serial, IMEI and Lot/Batch tracking are supported. Dual-SIM devices can retain two IMEIs for one physical StockUnit. Active IMEI uniqueness is enforced; serial uniqueness is scoped to a reliable manufacturer/type/model namespace. Serial correction requires authorized, audited correction. |
| INV-EVT-004 | Catalog defines base UOM. Inventory stores base quantity using Decimal, never float/double. Conversion factors are versioned and apply prospectively. UOM-specific fractional precision and overflow behavior must be explicit before implementation; display rounding cannot alter stored quantity. |
| INV-EVT-005 | Reservation lifecycle is type-specific: Sales reservations have configurable TTL; Repair reservations have no automatic TTL; Transfer reservations follow transfer state; temporary operational reservations have policy-specific TTL. Reservation, allocation, picking and issue remain distinct. |
| INV-EVT-006 | Goods receipt, sales issue, repair issue, internal/warehouse transfer, customer/supplier return, adjustment, status/disposition change, write-off/scrap and opening balance are distinct movement semantics with explicit commit boundaries. |
| INV-EVT-007 | Sales issue is linked at SalesLine and quantity level; partial/multiple fulfillment is supported. Sales owns customer handover acknowledgment; Inventory owns committed physical issue. Neither fact alone is a substitute for Accounting transfer-of-control evidence. |
| INV-EVT-008 | Repair issue requires a real, authorized RepairServiceLine/source reference and committed Inventory movement. FE-09 remains controlling. R1 repair intake stays disabled; no synthetic RepairCase or bypass is allowed. |
| INV-EVT-009 | Warehouse transfer is multi-stage. Dispatch reduces source stock and creates in-transit custody; destination increases only by accepted receipt. Unreceived/discrepant quantity remains in transit/discrepancy. A controlled in-transit inventory account is required for financial reconciliation; exact COA mapping and Posting Rule remain Accounting-owned and unapproved until separately evidenced. |
| INV-EVT-010 | Customer returns first enter RETURNED_PENDING_INSPECTION and are excluded from sellable ATP. Authorized disposition is SELLABLE, DEFECTIVE, QUARANTINED or UNUSABLE. Physical return, commercial entitlement, revenue/tax reversal and refund are independent. |
| INV-EVT-011 | Stocktake requires scoped snapshot/cut-off, physical count, variance, reason, authorized approval and a separate immutable adjustment movement. Count alone never changes stock. |
| INV-EVT-012 | Preserve prior owner policy: default non-serialized stock shortage threshold is -2 at item/warehouse control scope; serialized/IMEI stock cannot go negative. Only System Manager may change the non-serialized threshold with reason, scope, Actor and Audit. Open shortages older than 30 days warn; no automatic write-off/settlement; settlement requires authorized approval and traceable receipt/adjustment. |
| INV-EVT-013 | Non-serialized inventory uses Moving Weighted Average; serialized/IMEI inventory uses Specific Identification; FIFO is not the project's primary method. Landed-cost eligibility/allocation, original-issue return valuation, negative-stock provisional cost and controlled True-up are required. True-up splits remaining stock value from prior issued/COGS/expense impact and is idempotent. |
| INV-EVT-014 | Versioned event envelope includes stable EventId, EventType, ContractVersion, AggregateId/Version, immutable source document/version/line references, timestamps, server ActorReference, CorrelationId, CausationId, IdempotencyKey, item/variant, warehouse/location, tracking identity, quantity/UOM and movement/status fields as applicable. Same identity + different payload fails closed. |
| INV-EVT-015 | Movement, stock projection, audit and outbox persist atomically. Publish after commit; at-least-once delivery; idempotent consumers. Database uniqueness, locking/concurrency and recovery controls are mandatory. Projection is rebuildable from the immutable movement ledger. |
| INV-EVT-016 | Organization, Catalog, Purchasing, Sales, RepairOperations, Inventory and Accounting retain the ownership boundaries and contracts stated in the approved package. No direct cross-module table access. |
| INV-EVT-017 | Approve the 34 baseline acceptance tests and PostgreSQL requirements, including atomicity, isolation, row locks, unique constraints, deadlocks, concurrent updates, rollback, migration failure, outbox retry, recovery and reconciliation. |
| INV-EVT-018 | Acceptance requires all relevant module-owner reviews, implementation, QA, real PostgreSQL evidence, security/grant review, concurrency/recovery, independent review and exact source/build/test SHA. Release authority alone may grant operational release. |

## 2. Resolved prior decisions

- D-213: warehouse is the primary stock-control/shortage scope; storage location is physical traceability. Warehouse totals must reconcile to the sum of applicable locations. Internal location transfers do not change warehouse total.
- D-215: Variant/SKU is the primary stock identity for non-serialized goods. Product remains a catalog classification parent. Serialized/IMEI goods additionally require a StockUnit/TrackingIdentity.
- Sales reservation TTL and Repair reservation non-expiry are distinct policies.
- Negative-stock default -2 for non-serialized goods and prohibition for serialized/IMEI goods are retained.
- Moving Weighted Average and Specific Identification are the accepted valuation methods by tracking class.

## 3. Constraints and unresolved implementation parameters

This approval fixes business policy; it does not invent missing numeric or technical parameters. Before implementation, the technical design must explicitly propose and obtain the required owner approval for:
1. Decimal precision/scale and allowed fractional precision by UOM, including conversion rounding and overflow.
2. Default Sales reservation TTL and configuration scope; no value is hard-coded.
3. Exact manufacturer/model namespace rules for serial uniqueness where global manufacturer uniqueness is not guaranteed.
4. Deterministic landed-cost allocation, monetary precision, rounding and True-up posting lineage.
5. The exact COA account identifier and Posting Rule for in-transit stock. No account is inferred from a name/prefix.
6. Treatment of transfers across legal entities/ownership boundaries; this decision covers internal transfers within one legal entity unless a separately approved policy says otherwise.
7. Whether a count session uses a hard movement freeze, a timestamped cut-off with delta replay, or another proven concurrency-safe method.

## 4. Authority and non-authorization

This decision is binding for architecture decomposition, data ownership, contracts, design, implementation and tests. It does not:
- amend MDG-ARCH-MASTER-001 v2.5;
- grant technical acceptance by Organization, Catalog, Purchasing, Sales, RepairOperations, Accounting, Security or Platform Architecture;
- authorize SQL, migrations, API/runtime code, event publishers or database grants;
- authorize any Accounting Posting Rule or COA mapping;
- grant Posting Release or operational activation.

**Execution:** NOT_VERIFIED. **Tests:** NOT_RUN. **Operational Release:** NOT_GRANTED.
