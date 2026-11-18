# Inventory Source Event and Stock Ownership Contract Proposal

| Field | Value |
|---|---|
| Document ID | INVENTORY-SOURCE-EVENT-CONTRACT-PROPOSAL-001 |
| Version | 0.1 |
| Status | PROPOSAL — INVENTORY OWNER REVIEW REQUIRED |
| Scope | Authoritative stock, committed movement, fulfillment, return inspection and valuation source facts |
| Architecture | MDG-ARCH-MASTER-001 v2.5 |
| Owner policy | OWN-FE-01-02-001 v1.0 — APPROVED BUSINESS POLICY |
| Related proposals | Sales v0.2; RepairOperations v0.2; FE-01/FE-02 crosswalk v0.2 |
| Data ownership | STK-01 — Inventory; Organization owns warehouse/location master data |

## 1. Authority, intent and non-claims

This proposal defines the candidate authoritative boundary and source evidence required from Inventory for FE-01, FE-02 and later stock-consuming workflows. It translates approved owner policy into a technical proposal; it is not an accepted runtime contract, executable schema, database migration, event publisher, Accounting Posting Rule or Posting Release.

The controlling architecture is MDG-ARCH-MASTER-001 v2.5. The approved business policy is OWN-FE-01-02-001 v1.0. Any choice explicitly marked OPEN (including costing method, negative-stock policy, reservation expiry and movement commit boundary) requires owner acceptance and must not be silently selected by implementation.

The reviewed PR #66 branch has no Inventory Contracts project, Inventory domain/runtime implementation, Inventory persistence or Inventory source-event contract. This proposal does not claim otherwise and does not create runtime components.

## 2. Exclusive ownership and module boundaries

Inventory is the sole authoritative owner of:
- stock quantities by product/variant, warehouse and applicable location;
- committed stock movements and immutable line-level history;
- fulfillment/issue evidence and allocation of actual issued quantity to source demand lines;
- transfer dispatch, in-transit custody (if accepted) and receipt facts;
- physical return receipt, inspection and stock disposition;
- stock counts, adjustments and derived balance projections;
- inventory valuation evidence/layers and cost allocation under an explicitly accepted valuation policy;
- serial/IMEI and lot/batch custody and movement lineage where tracking is required.

No consumer may maintain a parallel authoritative stock ledger, independently decrement stock, assert a committed movement, change stock disposition or overwrite Inventory valuation.

Ownership exclusions:
- Organization owns warehouse/location master identity, hierarchy and membership/scope. Inventory references and validates these records; it does not become the organizational master.
- Product/catalog owns product/variant identity, attributes and measurement definitions. Inventory references these and records operational stock facts; it must not fork the catalogue.
- Sales owns commercial documents, immutable SalesDocument/version and SalesLine identity, commercial handover and return entitlement. Sales retains Inventory references only.
- RepairOperations owns repair workflow and RepairServiceLine references when enabled. It retains Inventory part-movement references only and cannot consume stock itself. R1 repair intake remains disabled.
- Purchasing owns purchase orders and supplier commercial terms. Inventory owns physical receipt/acceptance and resulting stock facts; an order or expected receipt is not on-hand stock.
- Accounting owns RecognitionTime, account eligibility, Posting Rules, journals, COGS recognition and financial posting. Inventory supplies quantity and valuation evidence; it does not select GL accounts, debit/credit rules, revenue timing or Posting Release.
- Treasury/Payments owns receipts, allocations, settlements and refunds; none creates stock movements.

## 3. Stock identity and dimensions

A stock position is not identified by product alone. The published contract must explicitly define the dimensions that form stock identity. Candidate dimensions:
- ProductId and applicable ProductVariantId;
- Organization-owned WarehouseId and optional LocationId/BinId;
- stock disposition/status;
- applicable lot/batch identity;
- applicable serial/IMEI identity;
- base UOM and transaction UOM/conversion reference;
- ownership/custody dimension only if the approved business model supports stock not owned by the business.

Inventory and Product owners must define which dimensions are mandatory for each product tracking mode. Non-serialized goods must not be forced to carry serials; serial-tracked goods must not be issued as anonymous aggregate quantities. Lot-tracked goods retain lot lineage through receipt, transfer, issue and return. Serial/IMEI is an inventory tracking identifier, not a duplicate customer identity or a free-form Sales/RepairOperations field.

Warehouse/location references must resolve to Organization-owned active records and be checked against the authenticated actor's authorized scope. A client-supplied warehouse or actor identifier is not authorization evidence.

## 4. Quantity, UOM and stock invariants

Each movement line declares a positive quantity in a declared transaction UOM and retains its canonical base-quantity result plus the exact conversion reference/version used. Conversions are deterministic and validated. Floating-point arithmetic is not acceptable for persisted stock quantities. Numeric precision/scale is an Inventory owner decision and must be explicit in the published contract.

Invariants:
1. Movement magnitude is positive; direction is represented by a controlled movement type, never by a negative quantity.
2. Source/destination dimensions are valid for the movement type; a transfer is not a sale or consumption.
3. A movement cannot cross incompatible product, UOM, serial, lot or disposition identities.
4. Serialized quantity reconciles to the exact distinct serial/IMEI set. Conflicting duplicate active serial assignment fails closed.
5. Lot/serial lineage remains traceable from receipt through subsequent movement and disposition.
6. Balance projections are derivable from committed movement history; a mutable balance cache is not the ledger.
7. An operation violating the accepted availability/negative-stock policy fails closed. Backorder, negative-stock, reservation and override policies remain OPEN until accepted.
8. Every quantity change, including count variance and correction, has a source, actor/system, reason and immutable movement identity.

## 5. Reservation, allocation and committed movement

Reservation, allocation, picking, shipment preparation and committed movement are different concepts.

- A reservation is a non-ledger availability claim with its own identity, lifecycle, expiry and cancellation semantics. It does not change on-hand quantity and is not fulfillment evidence.
- Allocation/picking/packing may create operational preparation records. Unless a step physically changes custody/location under an accepted movement rule, it is not an issue from the authoritative ledger.
- A committed issue/fulfillment exists only after Inventory validates and atomically commits the actual stock movement and its source-line quantity allocation.
- A reservation may be partially fulfilled, released, expired or cancelled under accepted rules. Its unfulfilled remainder remains explicit.
- Competing fulfillment requests must be concurrency-safe; two commands cannot commit the same available quantity or serial.
- The commit boundary for dispatch versus customer handover, and whether goods in transit are a distinct Inventory location/status, require Inventory acceptance and alignment with Sales handover evidence.

Invoice finalization, payment, payment allocation, PSP settlement, reservation, pick list, shipment request or uncommitted stock command MUST NOT be represented as a committed Inventory issue.

## 6. Candidate movement categories

The following are semantic categories for owner review, not published enum values. The owner must approve exact names, allowed transitions, direction, commit boundary and required evidence.

| Category | Stock effect | Required source/evidence |
|---|---|---|
| Receipt acceptance | increases eligible stock at receiving location/disposition | supplier/purchase reference where applicable; actual received quantity and inspection evidence |
| Sales/customer issue | decreases issuing stock; creates committed fulfillment allocation | SalesDocumentId/version and SalesLineId; Inventory commit evidence |
| Repair part issue | decreases issuing stock | RepairServiceLineId or other approved real source line; R1 synthetic repair intake prohibited |
| Transfer dispatch | decreases source position and creates in-transit custody if accepted | TransferId/line; source and destination warehouse references |
| Transfer receipt | moves in-transit quantity into destination | matching dispatch line, received quantity and discrepancy evidence |
| Customer return receipt | records returned physical custody in pending inspection | Sales return/entitlement reference where one exists; physical receipt evidence |
| Supplier return dispatch | decreases stock held for supplier return | supplier-return document and authorization |
| Count/adjustment | creates traceable positive or negative correction | count session, counted quantity, variance, reason and authorization |
| Disposition transfer | moves quantity between sellable/defective/quarantined/unusable states | inspection/disposition decision and evidence |

No movement type may be introduced to force an Accounting event to pass. No movement category specifies an account or journal entry.

## 7. Sales fulfillment linkage and partial fulfillment

For FE-01, every committed customer issue/fulfillment allocation retains immutable references to:
- SalesDocumentId and immutable SalesDocumentVersion;
- SalesLineId and stable line revision;
- product/variant and UOM;
- requested quantity, committed quantity and base-quantity conversion evidence;
- FulfillmentId/MovementId and immutable movement-line identity/version;
- warehouse/location and applicable lot/serial references;
- committed timestamp and server-derived actor/system reference;
- separate dispatch, recipient handover or delivery evidence references where required by Sales policy.

Cardinality supports one SalesLine fulfilled by multiple partial/multi-stage Inventory issue lines. One Inventory movement batch may contain multiple SalesLines only if every line allocation preserves its own source-line identity and quantity. A movement header without line-level allocations is insufficient.

The sum of committed, non-reversed allocations must reconcile to the eligible SalesLine quantity. Over-fulfillment, duplicate allocation, source-line version mismatch, product/UOM mismatch or invalid source reference fails closed unless an explicit accepted exception exists.

Inventory confirms committed stock issue/fulfillment; Sales separately owns commercial handover acknowledgment and recipient evidence. Neither substitutes for the other. Accounting alone determines RecognitionTime under an accepted rule using the required source evidence.

## 8. RepairOperations linkage and R1 restriction

When RepairOperations is enabled in an approved future release, a part issue references stable RepairCaseId and RepairServiceLineId (or accepted equivalent) and exact source revision. RepairOperations stores the Inventory movement reference; it does not write stock quantity, status or valuation.

R1 restriction is absolute: repair intake/acceptance is disabled with the approved message «پذیرش تعمیر در نسخهٔ بعدی فعال می‌شود.». No synthetic RepairCase, substitute workflow, manual status or fabricated RepairServiceLine may be created to enable a part issue or FE-02 posting. A real R1 part issue is admissible only if a separately approved, real, immutable and independently addressable source already exists; otherwise repair-driven issue/posting is unavailable in R1.

Inventory part-issue evidence proves only stock consumption. It does not prove technical completion, required QC, readiness, customer handover or service revenue recognition.

## 9. Returns, inspection and disposition

Physical return receipt, commercial return entitlement, revenue/tax reversal, refund, inspection, restock and valuation effects are separate facts.

Returned goods first enter a non-sellable pending-inspection state. Physical receipt, Sales credit document or refund does not increase sellable/ATP quantity.

Candidate disposition states:
- RETURNED_PENDING_INSPECTION — physical receipt recorded; unavailable for ordinary sale/issue;
- SELLABLE — inspection/authorization confirms eligible return to sellable stock;
- DEFECTIVE — defective condition established; excluded from ordinary sellable availability;
- QUARANTINED — isolated pending investigation, supplier decision or other authorized disposition;
- UNUSABLE — authorized determination that item cannot return to usable stock; disposal/write-off needs its own evidence and approval.

Exact transitions, permitted actors, evidence, segregation and additional states require Inventory owner approval. Status changes are immutable disposition facts/movements, not silent edits. Inspection evidence identifies item/lot/serial, result, timestamp, server-derived actor, reason and controlled evidence references. Only Inventory may authorize SELLABLE disposition. Sales return entitlement/refund cannot do so.

## 10. Counts, adjustments, corrections and reversals

A stock count is an observation with scope, count-session identity, snapshot/cut-off, counter, timestamp and counted quantity. It is not itself a stock movement.

A variance becomes a committed adjustment only after authorization and evidence. It records counted quantity, expected quantity at the applicable cut-off, variance, reason, approver and created movement(s). Concurrent movements during a count require a defined cut-off/locking or delta policy; a stale count must never silently overwrite a newer balance.

Committed movements are append-only. A mistake is corrected by a new linked compensating/reversal movement with reason, actor and source reference. The original remains queryable and is never deleted or edited in place. Reversal dimensions and quantity are validated against the original; partial reversal retains remaining unreversed quantity and cumulative reversal cannot exceed the original committed amount.

A correction is not a new sale, return or receipt unless that underlying business fact genuinely occurred.

## 11. Valuation and cost evidence boundary

Inventory owns authoritative valuation evidence for stock movements and the cost layers/allocations needed to explain stock on hand and stock issued. Accounting owns GL/COGS policy and posting.

This proposal deliberately selects no costing method. FIFO, weighted average, specific identification or another method; landed-cost capitalization; freight/duty allocation; negative-stock revaluation; rounding; cost-period close; write-down; return costing and transfer costing all require explicit owner decisions and deterministic test vectors before runtime use.

For each valuation-bearing committed movement, the eventual contract exposes immutable evidence sufficient to audit/reproduce:
- source movement and movement-line identity/version;
- product/variant, warehouse and applicable lot/serial dimensions;
- quantity and base UOM;
- valuation method identifier/version effective at commit;
- unit and extended cost with declared currency/precision where monetary;
- source cost layer(s) and deterministic allocation evidence;
- accepted landed-cost treatment where applicable;
- valuation/recorded timestamps;
- correction/reversal linkage and original valuation reference.

Historical valuation is never recalculated using today's cost or overwritten. Transfers preserve/transform cost under the accepted method without artificial profit. Returns follow the accepted return-cost rule and retain original-issue lineage where available.

Inventory valuation is an input to Accounting. It does not authorize COGS posting, select a COA account, define RecognitionTime or prove revenue. Accounting independently accepts source crosswalk and Posting Rule. Missing, inconsistent or unverified required cost evidence blocks the Inventory-dependent Accounting event.

## 12. Event envelope, identity, versioning and idempotency

Every published Inventory event follows OWN-FE-01-02-001 v1.0. Candidate envelope:
- EventId — stable immutable event identity;
- EventType and ContractVersion;
- AggregateId and AggregateVersion;
- SourceDocumentReference, source version and source-line lineage;
- MovementId/FulfillmentId and movement-line identity where applicable;
- OccurredAtUtc and RecordedAtUtc;
- server-derived ActorReference where an authenticated human/system actor exists;
- CorrelationId and CausationId;
- scoped request IdempotencyKey, distinct from EventId;
- PartyId only when relevant; no copied personal data in stock events;
- warehouse/location and product/variant references;
- quantity/UOM/conversion and serial/lot references as applicable;
- valuation reference/result only for valuation-bearing event types;
- reason/evidence references for adjustment, inspection, disposition and reversal.

Each event schema declares required/optional/nullability, precision, allowed values, version compatibility and ownership. Non-monetary events do not invent currency or amount. Same identity/key with a different canonical payload fails closed. Retry reuses original identity without duplicate stock effect. Correction, reversal or new disposition is a new linked event. Consumers validate source version, event type, contract version and payload identity.

## 13. Transaction boundary, outbox and delivery

Within the Inventory owning database transaction, authoritative movement/stock-ledger fact, synchronously maintained balance projection, audit record and Outbox record commit atomically. If a required write fails, none of these effects commits.

Publish only after commit. Delivery is at-least-once; consumers are idempotent and deduplicate by stable EventId while validating payload identity. Retry reuses the same EventId. No exactly-once transport guarantee is claimed.

Concurrency controls protect available quantity, reservations, serial uniqueness, source-line fulfillment totals, transfer balances and reversal totals. The isolation/locking/optimistic-concurrency strategy must be documented and proven by concurrent PostgreSQL tests. Cross-module database atomicity follows ADR-0005 and accepted architecture; this proposal does not authorize bypassing module ownership or a second ledger.

No SQL, migration, EF entity, repository, endpoint, publisher or runtime adapter is authorized here. Migration 0009 remains reserved by PR #70; no Inventory or Accounting SQL may consume it before PR #70 is independently reviewed and merged to main and subsequent schema work is separately approved.

## 14. Consumer contract and fail-closed behavior

Consumers use versioned published Contracts and must not reference Inventory Domain/Infrastructure internals.

- Sales retains Inventory fulfillment/movement references and consumes committed fulfillment evidence; it cannot issue/reverse Inventory movements.
- RepairOperations retains part-issue references; it cannot decrement stock or infer repair completion.
- Accounting consumes validated quantity/valuation evidence only after its source mapping, RecognitionTime and Posting Rule gates are accepted; it cannot infer missing valuation or account data.
- Reports/read models may project stock information but disclose freshness/version and cannot become authoritative write sources.

Fail closed for unknown event type/version, malformed quantity, invalid UOM conversion, duplicate serial, stale aggregate version, source-line mismatch, unauthorized warehouse scope, insufficient eligible stock under accepted policy, conflicting idempotency payload, missing inspection/approval evidence, over-fulfillment, over-reversal or unverified required valuation. Rejected commands leave no partial movement, balance, audit or outbox effect.

## 15. Owner acceptance register

| ID | Required acceptance | Owner | Status |
|---|---|---|---|
| INV-EVT-001 | Module boundary, aggregate identities and stock ledger/balance ownership | Inventory | OPEN |
| INV-EVT-002 | Product/variant, warehouse/location dimensions and master references | Inventory + Organization + Product | OPEN |
| INV-EVT-003 | Aggregate, lot/batch and serial/IMEI tracking modes | Inventory + Product | OPEN |
| INV-EVT-004 | Quantity precision, base UOM, conversion governance and arithmetic | Inventory + Product | OPEN |
| INV-EVT-005 | Reservation/allocation/picking lifecycle, expiry and concurrency | Inventory | OPEN |
| INV-EVT-006 | Movement types, commit boundaries and state transitions | Inventory | OPEN |
| INV-EVT-007 | SalesLine linkage, partial fulfillment cardinality and over-fulfillment | Inventory + Sales | OPEN |
| INV-EVT-008 | RepairServiceLine/approved R1 source linkage and R1 prohibition | Inventory + RepairOperations | OPEN |
| INV-EVT-009 | Transfer dispatch, in-transit custody, receipt and discrepancies | Inventory + Organization | OPEN |
| INV-EVT-010 | Return receipt, inspection evidence and disposition transitions | Inventory + Sales | OPEN |
| INV-EVT-011 | Count sessions, cut-off, variance approval and adjustments | Inventory | OPEN |
| INV-EVT-012 | Negative-stock/backorder and exceptional override policy | Inventory + Product Owner | OPEN |
| INV-EVT-013 | Costing method, cost layers, landed cost, returns, transfers and reversals | Inventory + Accounting | OPEN |
| INV-EVT-014 | Event schema, lineage, versioning, actor/evidence and idempotency | Inventory + Platform Architecture | OPEN |
| INV-EVT-015 | Transactional outbox, concurrency and cross-module transaction boundary | Inventory + Platform Architecture | OPEN |
| INV-EVT-016 | Sales/RepairOperations/Accounting consumer contracts and source crosswalk | Inventory + Sales + RepairOperations + Accounting | OPEN |
| INV-EVT-017 | PostgreSQL atomicity, concurrency, replay, correction and reconciliation tests | Inventory + QA | OPEN |
| INV-EVT-018 | Independent review of accepted contract and exact implementation/test SHA | Independent Reviewer | OPEN |

## 16. Acceptance criteria before publication

1. Inventory owner accepts exclusive ownership and aggregate/line identities.
2. Organization and Product owners confirm master-data references and stock dimensions.
3. Every movement type has explicit direction, allowed source/destination, commit boundary, evidence and reversal policy.
4. Reservation is demonstrably distinct from on-hand stock and committed fulfillment.
5. Sales partial fulfillment and multi-line cardinality are defined and reconciliation-tested.
6. Repair issue linkage preserves R1 prohibition and creates no synthetic workflow.
7. Returned stock cannot enter sellable/ATP before authorized inspection/disposition.
8. Counts, adjustments, transfers and reversals preserve immutable traceable history.
9. Valuation method and cost/return/transfer policies are explicitly accepted; no method is inferred.
10. Event identity, versioning, idempotency and outbox semantics are accepted.
11. PostgreSQL tests prove atomicity, concurrency safety, replay, conflicting-payload rejection, source-line reconciliation and no partial side effects.
12. Accounting separately accepts valuation/quantity source mapping, RecognitionTime and any Posting Rule. Inventory acceptance alone never grants Posting Release.
13. Independent review accepts exact final implementation and evidence SHA before release gates close.

## 17. Status and explicit non-authorization

- OWN-FE-01-02-001 v1.0 business policy: APPROVED.
- Inventory source-event/stock ownership contract: PROPOSAL — INVENTORY OWNER REVIEW REQUIRED.
- Inventory Contracts/runtime/persistence: NOT IMPLEMENTED / NOT VERIFIED on reviewed PR #66 branch.
- Inventory owner technical acceptance: NOT RECEIVED.
- Sales and RepairOperations technical acceptance: OPEN.
- FE-01: BLOCKED pending Inventory/Sales evidence and Accounting gates.
- FE-02: BLOCKED; R1 repair intake remains DISABLED.
- Accepted positive Posting Rules: 0.
- Runtime PostgreSQL Inventory/Accounting integration tests: NOT RUN / NOT VERIFIED.
- Independent review: PENDING.
- Posting Release: NOT GRANTED.
- No migration, SQL, API, event publisher, Posting Rule, COA mapping or runtime behavior is authorized by this proposal.

**End of proposal.**
