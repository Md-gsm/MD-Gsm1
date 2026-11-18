# Inventory Technical Owner Review — Findings and Acceptance Actions

| Field | Value |
|---|---|
| Document ID | INVENTORY-TECHNICAL-OWNER-REVIEW-001 |
| Version | 0.1 |
| Status | INTERNAL DESIGN REVIEW COMPLETE — CROSS-MODULE OWNER ACCEPTANCE OPEN |
| Review basis | OWN-INV-018 v1.0; Inventory contract v0.2; ERD/Data Dictionary v0.1; State/Movement v0.1; Test Specification v0.1; MDG-ARCH-MASTER-001 v2.5 |
| Review scope | Conceptual integrity, state semantics, cross-module ownership, accounting boundaries, PostgreSQL/testability |
| Runtime/schema review | NOT APPLICABLE — no Inventory runtime or physical schema is proposed here |

## 1. Executive result

The three design deliverables are directionally consistent with the approved business policy and correctly preserve the no-runtime/no-migration boundary. They are NOT YET READY to be frozen as implementation specifications. The review found seven blocking design decisions/clarifications and five required model/test refinements. These are documentation/design blockers, not evidence of implementation defects.

No module-owner approval is inferred from this review. Each named owner must explicitly accept their assigned items. OWN-INV-018 remains the business-policy authority; this review does not amend it or MDG-ARCH-MASTER-001 v2.5.

## 2. Blocking items before physical schema or implementation

| Review ID | Required resolution | Accountable owner(s) | Acceptance evidence |
|---|---|---|---|
| INV-REV-01 | Set Decimal precision/scale by quantity, currency/value and conversion domain; define UOM fractional increments, conversion precision, rounding stage/mode and overflow behavior. Define exact PostgreSQL numeric mapping. | Catalog + Inventory + Accounting + Platform/DB | Approved numeric policy table and boundary tests |
| INV-REV-02 | Set Sales reservation TTL default, configuration scope, start event, expiry timestamp semantics, timezone basis and behavior when a reservation is partially allocated/picked/fulfilled. Expiry must not alter committed issue or finalized Sales documents. | Sales + Inventory | Approved TTL policy and transition examples |
| INV-REV-03 | Define serial uniqueness namespace using actual Catalog manufacturer/model identifiers and normalization rules; distinguish active identity from historical identity; define IMEI normalization and correction/reassignment controls. | Catalog + Inventory + Security | Namespace specification, constraints and duplicate/history tests |
| INV-REV-04 | Define deterministic landed-cost allocation basis and eligibility; address mixed currencies, recoverable taxes, credit notes, freight after receipt, rounding residuals and reallocation after downstream issue. | Purchasing + Inventory + Accounting | Approved allocation algorithm with worked examples and journal/subledger lineage |
| INV-REV-05 | Select stocktake concurrency strategy: (A) freeze movements for the counted scope, or (B) immutable cut-off snapshot plus ordered delta replay. Define late/backdated events, partial scope, recount and cancellation. | Inventory + Platform/DB + QA | State transition, sequence diagram and race tests |
| INV-REV-06 | Define in-transit stock subledger classification and exact COA account/Posting Rule from canonical approved evidence. Distinguish same-legal-entity transfer from cross-entity/ownership transfer. No account code may be inferred. | Accounting + Inventory + Tax/Legal where relevant | Signed accounting disposition, source reference and transfer examples |
| INV-REV-07 | Define provisional valuation for permitted non-serialized negative stock, including cost source hierarchy, valuation date, later receipt matching, partial settlement, price variance and True-up lineage. Specify safeguards for zero/negative denominator in moving weighted average. | Accounting + Inventory + Purchasing | Approved algorithm, worked cases and deterministic tests |

Gate: INV-REV-01 through INV-REV-07 must be resolved before the conceptual model is promoted to a physical ERD/Data Dictionary or any migration/runtime implementation begins.

## 3. Required ERD/Data Dictionary refinements

| Review ID | Finding | Required change | Owner |
|---|---|---|---|
| INV-REV-08 | Diagram cardinality Variant 1-* StockPosition 1-* StockMovementLine can imply that immutable ledger rows depend on a mutable projection row. | Model StockMovementLine as an immutable ledger fact referencing Variant/warehouse/location/tracking dimensions. StockPosition is a rebuildable projection keyed by its dimensions; it is not the parent of historical movement lines. | Inventory + Platform/DB |
| INV-REV-09 | The stock-position key allows optional location, but reconciliation does not define unlocated stock. | Explicitly define whether untracked-location quantity lives in a warehouse-level UNLOCATED bucket or whether location is mandatory for that tracking policy. Prevent double counting warehouse aggregate plus location rows. | Inventory + Organization |
| INV-REV-10 | Lot and StockUnit coexistence is approved, but the ERD does not define the link for items tracked by both Lot and Serial/IMEI. | Define StockUnit-to-Lot membership and receipt lineage; enforce that serial/IMEI unit, Lot, Variant and receipt are mutually consistent. | Catalog + Inventory |
| INV-REV-11 | Status is a stock-position dimension while status changes are described as quantity-neutral. | Define a status transfer as paired decrement/increment across status buckets in one atomic movement; preserve physical on-hand total and prohibit negative status buckets unless explicitly permitted. | Inventory + Platform/DB |
| INV-REV-12 | Reservation, Allocation, Picking and Issued are separate, but the data model does not fully state their quantity conservation. | Define per-source-line requested/reserved/allocated/picked/issued/released quantities and invariants. Clarify whether ATP subtracts reservations once only; allocated/picked must not be double-subtracted. | Inventory + Sales + RepairOperations |

## 4. Required State/Movement refinements

1. ATP and reservation accounting: define mutually exclusive or explicitly overlapping quantity buckets. State whether ActiveReservations includes allocated/picked quantities; publish conservation equations so the same units cannot be deducted twice.
2. Negative stock scope: enforce the -2 threshold at Variant/Warehouse across all locations, with a lock/serialization scope that prevents two locations from independently spending the same threshold. Define whether an authorized threshold change is prospective only.
3. Transfer conservation: define discrepancy as unresolved custody quantity until a formally authorized resolution movement closes it. A discrepancy label alone must not silently remove stock from the in-transit subledger.
4. True-up after partial issue/return: define allocation ordering and immutable cost-layer lineage when one receipt lot feeds several issues, partial returns, write-offs and subsequent True-ups. Repeated or late supplier adjustments must be deterministic.
5. Movement reversal: define whether reversal quantity is bounded per original line and how reversal of a reversal is represented. Do not allow a reversal chain to exceed original net quantity/value.

## 5. Cross-module technical acceptance matrix

| Owner | Must explicitly accept |
|---|---|
| Organization | Warehouse/location hierarchy, identifier/version contract, active/inactive behavior, deactivation with historical balances, and location-level policy |
| Catalog | Variant/SKU stock identity, tracking modes and combinations, base UOM ownership, conversion versioning, serial/IMEI namespace and normalization |
| Purchasing | GoodsReceipt accepted/rejected/pending quantity contract, source document/version references, landed-cost evidence, provisional/final cost and supplier adjustments |
| Sales | SalesLine immutable linkage, partial fulfillment, handover acknowledgment separation, return entitlement references and Sales TTL semantics |
| RepairOperations | RepairServiceLine reference and lifecycle validity, movement-reference ownership, FE-09 compliance and R1 no-synthetic-source prohibition |
| Accounting | WMA/Specific Identification, negative-stock provisional cost, landed-cost capitalization, True-up allocation and COGS lineage, return valuation, in-transit COA/Posting Rule and cross-entity boundary |
| Security | System Manager threshold override, inspection/disposition authorization, actor derivation, audit immutability and module principal grant matrix |
| Platform/DB | PostgreSQL numeric types, unique constraints, isolation/locking, idempotency constraints, transactional outbox, projection rebuild, migration ordering and recovery |
| QA | 65 listed tests plus additional invariants in this review, real PostgreSQL execution, evidence schema and exact-SHA acceptance |

An owner acceptance must name the reviewed document version and exact commit SHA, list accepted/rejected items, and record conditions. Silence, PR participation or a green structural validator is not acceptance.

## 6. Test specification review

The current Test Specification lists 65 tests (34 owner-approved baseline + 31 additional). This is a useful baseline, but the following explicit tests should be added before test design is frozen:

| Proposed TestId | Scenario | Required assertion |
|---|---|---|
| INV-T-066 | Unlocated vs location-tracked stock | No double count; warehouse total reconciles exactly |
| INV-T-067 | Lot + Serial combined tracking | StockUnit, Lot, Variant and receipt lineage remain consistent |
| INV-T-068 | ATP bucket conservation | Reservation/allocation/picking do not double-subtract ATP |
| INV-T-069 | Negative threshold across locations | Concurrent issues in separate locations cannot exceed the single warehouse-level -2 threshold |
| INV-T-070 | WMA at zero/negative quantity | No divide-by-zero, guessed cost or silent zero valuation |
| INV-T-071 | Landed-cost residual rounding | Allocation is deterministic and total allocated amount equals approved source amount |
| INV-T-072 | Late landed-cost True-up after partial issues | Retained and issued shares reconcile to the full variance exactly |
| INV-T-073 | Stocktake cut-off and late event | Cut-off/delta replay produces no lost or double-applied movement |
| INV-T-074 | Transfer discrepancy resolution | Only authorized resolution movement clears in-transit discrepancy |
| INV-T-075 | Reservation partial pick/expiry | Expiry releases only unconsumed remainder exactly once |
| INV-T-076 | Historical serial reassignment/correction | Active uniqueness and historical audit are both preserved |
| INV-T-077 | Reversal chain bounds | Net reversed quantity/value cannot exceed original committed movement |

The 12 cases above are proposed additions; they are not yet recorded as owner-approved policy or executed tests.

## 7. Recommended acceptance sequence

1. Organization, Catalog, Purchasing, Sales, RepairOperations and Accounting each review assigned rows in Section 5 and respond against the seven blocking items.
2. Inventory owner consolidates accepted technical choices without changing OWN-INV-018 policy.
3. Security and Platform/DB review authorization, constraints, concurrency and transaction design.
4. QA maps all 77 test IDs to requirements and confirms fixtures, PostgreSQL behavior and evidence capture.
5. Revise the three design documents to v0.2 (or a controlled next version), add approved parameter decisions, and update the crosswalk/register/checksums.
6. Conduct independent review on the exact resulting SHA.
7. Only after design acceptance may a separate implementation authorization be considered. This review grants no SQL, migration, runtime, Posting Release or operational release.

## 8. Current disposition

- Business policy OWN-INV-018 v1.0: APPROVED.
- Internal technical design review: COMPLETE WITH BLOCKERS.
- Cross-module technical owner acceptance: OPEN.
- Physical ERD / Data Dictionary: NOT READY.
- Inventory runtime / SQL / migrations: NOT AUTHORIZED.
- Tests: SPECIFIED; NOT RUN.
- Posting Release: NOT GRANTED.
- Operational Release: NOT GRANTED.
