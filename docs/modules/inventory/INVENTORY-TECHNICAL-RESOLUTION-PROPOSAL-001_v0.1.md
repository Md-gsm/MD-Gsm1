# Inventory Technical Resolution Proposal — Seven Review Blockers

| Field | Value |
|---|---|
| Document ID | INVENTORY-TECHNICAL-RESOLUTION-PROPOSAL-001 |
| Version | 0.1 |
| Status | TECHNICAL DISPOSITION PROPOSED — CROSS-MODULE OWNER ACCEPTANCE REQUIRED |
| Authority | OWN-INV-018 v1.0; INVENTORY-TECHNICAL-OWNER-REVIEW-001 v0.1; MDG-ARCH-MASTER-001 v2.5 |
| Scope | Disposition of INV-REV-01..07; design only |
| PR | #66 — keep OPEN / DRAFT |

## 1. Purpose and authority boundary

This document supplies one internally consistent technical disposition for each of the seven blockers. It does not claim that the named module owners have accepted these values. Acceptance must be recorded by each accountable owner against this exact version and the exact PR head. Until then, the values are proposals, not runtime defaults.

This document does not amend OWN-INV-018 or MDG-ARCH-MASTER-001 v2.5. It authorizes no physical schema, SQL, migration, runtime, API, event publisher, database grant, Accounting Posting Rule, Posting Release or operational release. Migration 0009 remains reserved by PR #70.

## 2. Seven blocker dispositions

### INV-REV-01 — Decimal precision, UOM and conversion

**Proposed disposition**
- PostgreSQL quantity and base-quantity storage: `numeric(24,9)`.
- UOM conversion factor: `numeric(24,12)`; retain the exact conversion identifier and version used by each movement line.
- Valuation calculations: `numeric(38,12)` internal precision; no binary floating point.
- Catalog owns the permitted increment/precision for each UOM. Piece/count UOMs require integral quantities; fractional UOMs declare their minimum increment. A transaction must be an exact multiple of its declared increment.
- Convert transaction quantity to base quantity using the versioned factor. Do not silently round a quantity into stock. If the result cannot be represented at 9 decimal places or violates the base-UOM increment, reject it with a precision exception.
- Monetary rounding is separate from quantity conversion. Apply the currency's approved minor-unit rule only at the defined financial boundary; use deterministic round-half-even for intermediate monetary calculations and a documented residual-allocation rule for final totals.
- Overflow, underflow, non-finite input, zero/negative conversion factor and unsupported scale fail closed.

**Acceptance tests**: exact conversion; repeating decimal; max precision; overflow; base increment violation; conversion-version change; round-trip display without stock mutation.

**Owners**: Catalog, Inventory, Accounting, Platform/DB.

### INV-REV-02 — Sales reservation TTL

**Proposed disposition**
- Configuration scope: Organization business settings, with optional Warehouse override only if Sales and Inventory explicitly enable it; the effective value is snapshotted on each reservation.
- Proposed initial default: 30 minutes. This is a configurable business setting, not a code constant.
- TTL starts at server-recorded `ActivatedAtUtc`, not request creation, client time, invoice time or local wall-clock time. `ExpiresAtUtc = ActivatedAtUtc + EffectiveTtl`.
- All timestamps are UTC. The business UI may display local time but may not alter expiry calculations.
- Expiry is an idempotent server-side transition. It releases only the unfulfilled, unpicked remainder. Quantities already committed as issued are never released. Picked quantities remain protected until a valid unpick, issue or authorized exception transition returns them to the releasable pool.
- Partial allocation does not reset the TTL. A new reservation is required for additional demand. A race between expiry and issue is serialized at the reservation/stock scope; exactly one valid transition wins.
- Expiry never edits or cancels a finalized Sales document, never reverses an Inventory movement and never changes customer handover evidence.

**Owners**: Sales, Inventory. The 30-minute value requires explicit business-owner confirmation before it becomes the production default.

### INV-REV-03 — Serial/IMEI namespace and normalization

**Proposed disposition**
- IMEI: normalize to ASCII digits, require exactly 15 digits and a valid Luhn check digit. Store the normalized value; preserve the submitted value only in restricted audit evidence when needed.
- Serial: Unicode NFC, trim outer whitespace, uppercase invariant for comparison, and remove only presentation separators (ASCII spaces and hyphens). Do not remove arbitrary punctuation or non-ASCII characters without a manufacturer-specific rule.
- Active serial uniqueness key: `ManufacturerId + DeviceTypeId + NormalizedSerial`; when Catalog has no stable DeviceTypeId, use `ManufacturerId + ModelId + NormalizedSerial`. A manufacturer/model identity must be a Catalog-owned stable identifier, not a free-text label.
- Active IMEI uniqueness key: normalized IMEI globally unique across active StockUnits. One StockUnit may have two active IMEIs (dual-SIM); one IMEI may never identify two active StockUnits.
- Historical identifiers are retained with validity intervals and immutable reassignment/correction audit. A historical identifier may be reassigned only after an authorized retirement/release workflow establishes that no active StockUnit owns it. Corrections require Security-authorized permission, reason, actor and evidence.
- Database uniqueness must enforce active namespace rules under concurrency; application-only checks are insufficient.

**Owners**: Catalog, Inventory, Security, Platform/DB.

### INV-REV-04 — Landed-cost eligibility, allocation and rounding

**Proposed disposition**
- Capitalize only directly attributable costs necessary to bring the specific received inventory to its present location and condition, subject to Accounting policy. Examples may include directly attributable freight, transit insurance, non-refundable import duties and qualifying direct preparation. Recoverable tax, general administration, selling, financing/late-payment charges, penalties, abnormal waste and unrelated storage are excluded.
- Every landed-cost document line must declare: cost category, currency, source evidence, eligible receipt scope, allocation basis, exchange-rate evidence (if currencies differ), and policy/version. Missing or ambiguous evidence is rejected; no implicit default basis.
- Deterministic basis by category: freight by evidenced chargeable weight (fallback to volume only when the carrier/source document prices by volume); transit insurance and value-based duty by eligible net acquisition value excluding recoverable tax; unit-specific cost directly to the identified receipt line/StockUnit; other cost only with an Accounting-approved explicit basis. The selected basis and source quantities are snapshotted.
- Mixed-currency allocation requires an approved Accounting exchange-rate source and rate timestamp/version. If absent, keep the landed cost pending; do not use a current market rate or guessed rate.
- Allocate at high precision, then distribute the smallest-currency-unit residual by deterministic largest-remainder allocation, tie-broken by immutable receipt-line ID. Sum of allocations must equal the eligible source amount exactly.
- A late supplier credit/debit note creates a new append-only True-up. Recalculate only the variance allocation; never rewrite original receipt or issue valuation.
- Split each variance by original source-layer lineage: remaining quantity/value adjusts inventory; quantity already issued adjusts the linked COGS/expense lineage; returned quantities retain the original issue valuation. Write-off and scrap portions follow their original cost lineage. Duplicate source adjustment/version is idempotently rejected.

**Owners**: Purchasing, Inventory, Accounting, Platform/DB.

### INV-REV-05 — Stocktake concurrency and cut-off

**Selected strategy: immutable cut-off snapshot plus ordered delta replay (strategy B).**

- At stocktake start, persist a server-generated scope definition and immutable ledger high-watermark per affected Warehouse/Variant/tracking scope. The snapshot is the balance reconstructed through that watermark.
- Movements continue during counting. Each committed movement receives a monotonic per-scope ledger sequence in the same transaction as the movement.
- The physical count records its observation time and counted identity/quantity. At reconciliation, apply all committed post-watermark movements in sequence to the snapshot, then compare the resulting expected quantity with the physical count. Do not compare the count with a moving live projection.
- Backdated OccurredAtUtc does not reorder or rewrite the ledger. A late-arriving event whose business-effective time precedes the cut-off is an exception requiring investigation/recount or a separately authorized adjustment; it is never silently inserted before the watermark.
- Partial scope is explicit and versioned. A recount supersedes the prior observation but does not erase it. Cancellation closes the stocktake workflow without changing stock. Only an approved, separately committed InventoryAdjustment changes the ledger.
- Snapshot watermark, sequence range, count evidence, delta set and reconciliation result are retained for audit and replay.

**Owners**: Inventory, Platform/DB, QA, Organization for location scope.

### INV-REV-06 — In-transit custody and accounting classification

**Proposed disposition**
- At dispatch, quantity and its existing Inventory valuation move from the source Warehouse into a distinct Inventory-owned `IN_TRANSIT` custody bucket keyed by TransferId/TransferLineId. Destination on-hand increases only for accepted physical receipt.
- In-transit is an Inventory subledger custody classification, not a sale, purchase, revenue or expense event. Same-legal-entity transfer is financially neutral at the Inventory source-contract level.
- Preserve carrying value and source valuation-layer lineage while in transit. Partial receipt transfers only the accepted quantity/value to destination; remaining quantity/value stays in transit. A discrepancy remains in transit until a separately authorized resolution movement documents loss, recovery, return or other disposition.
- **No GL account code or Posting Rule is selected by this document.** The transfer source event must not post to GL until Accounting identifies the exact account from canonical COA v1.2 master authority and v1.4 supplementary evidence, documents the account attributes/effective eligibility, and approves a separate Posting Rule. No prefix, title or account-name inference is allowed.
- Cross-legal-entity transfer or change of ownership is not covered by the same-entity neutral rule; it is blocked pending separate Tax/Legal and Accounting disposition.
- If canonical account evidence is absent, the in-transit subledger remains operationally traceable but financial posting is blocked and reported as an explicit reconciliation exception. This is a fail-closed disposition, not an implicit account mapping.

**Owners**: Accounting (COA and Posting authority), Inventory, Tax/Legal where ownership crosses entities, QA. The exact GL mapping remains a separately gated Accounting deliverable.

### INV-REV-07 — Provisional valuation for permitted negative stock

**Proposed disposition**
- Negative quantity remains limited to the approved non-serialized default threshold of -2 at Variant/Warehouse scope. Serial/IMEI negative quantity remains prohibited. The threshold is shared across all locations in the Warehouse and protected by one concurrency/locking scope.
- A negative issue must carry a provisional valuation reference. Cost source hierarchy:
  1. latest valid positive Moving Weighted Average for the same Variant and Warehouse immediately before the issue;
  2. if none, latest valid positive Moving Weighted Average for the same Variant in another Warehouse of the same legal entity, with explicit valuation-source reference;
  3. if none, a finalized, accepted GoodsReceipt valuation for the same Variant with auditable receipt lineage;
  4. if none, an approved supplier purchase-line price only where Purchasing confirms the price is final/accepted and Accounting supplies an approved FX rate where required.
- No source means the negative issue is rejected, even if quantity threshold permits shortage. Zero, guessed, stale untraceable or unrelated product cost is never substituted.
- Store provisional source ID/version, source warehouse, source date, currency/rate evidence, unit cost and algorithm version. The provisional value is an operational valuation estimate, not permission to post a final GL amount.
- When a later eligible receipt arrives, match the shortage to receipt quantity deterministically by Variant/Warehouse and oldest unresolved shortage first, then source event sequence. Partial receipt settles only the matched quantity. Remaining shortage stays open.
- True-up splits the variance between still-on-hand quantity and quantity already issued, linked to the original negative movement and any downstream issue/return/write-off. Use high precision; apply currency rounding/residual allocation once at the final accounting boundary. Repeated receipt/price updates are idempotent by source adjustment/version.
- Moving Weighted Average must never divide by zero or by a non-positive denominator. If a receipt/adjustment would produce an invalid denominator or unsupported negative-value state, quarantine valuation and fail closed pending Accounting resolution; do not silently reset average cost.

**Owners**: Inventory, Accounting, Purchasing, Platform/DB, QA.

## 3. Required model and state refinements (apply with these dispositions)

1. StockMovementLine is an immutable ledger fact referencing Variant, Warehouse, optional Location and tracking dimensions. StockPosition is a rebuildable projection; movement history must not depend on a mutable StockPosition parent.
2. For non-location-tracked stock, use one explicit Warehouse-level `UNLOCATED` bucket. For location-tracked stock, LocationId is mandatory. Warehouse totals are aggregates over either the UNLOCATED bucket or child locations, never both; aggregate rows are not separately counted as stock.
3. Lot+Serial tracking links each StockUnit to exactly one applicable Lot membership at a time, with Variant and receipt lineage consistency. A unit cannot move between incompatible Lots without a controlled correction workflow.
4. Stock-status change is one atomic paired quantity transfer between status buckets. Physical on-hand total is invariant; source bucket cannot become negative.
5. Demand quantity conservation is explicit per source line: requested = unreserved remainder + active reserved remainder + released/cancelled remainder + committed issued quantity, with allocated/picked represented as subsets of active reserved quantity, not additional deductions. ATP subtracts each physical unit once: EligibleOnHand minus the union of active reserved units/quantities, plus QualifiedInbound. Allocated/picked do not subtract again.
6. Negative threshold validation and stock locking occur at Warehouse+Variant, not per location.
7. Transfer discrepancy remains part of in-transit custody until a formal resolution movement.
8. Reversal quantities/values are cumulative and bounded by the original committed line net of prior reversals; a reversal-of-reversal is a new linked movement and cannot exceed the original amount.

## 4. Additional required tests

Retain INV-T-001..065 and add the following twelve tests from the internal review: INV-T-066..077. Add explicit assertions for:
- quantity conversion and precision boundary/overflow (INV-REV-01);
- Sales reservation activation/expiry, partial pick and expiry/issue race (INV-REV-02);
- active serial namespace, global IMEI uniqueness and historical reassignment (INV-REV-03);
- landed-cost category/basis, mixed-currency fail-closed, residual conservation and late True-up after partial issue (INV-REV-04);
- stocktake watermark, concurrent movement, late/backdated event and delta replay (INV-REV-05);
- in-transit quantity/value conservation and fail-closed absence of approved COA/Posting Rule (INV-REV-06);
- provisional valuation source hierarchy, partial receipt matching, WMA zero/negative denominator and True-up idempotency (INV-REV-07).

All tests remain specifications until executed against the actual PostgreSQL target and exact build/schema SHA. Current execution status remains NOT_RUN.

## 5. Disposition and remaining acceptance gates

| Blocker | Technical disposition | Design disposition | External acceptance still required |
|---|---|---|---|
| INV-REV-01 | numeric/UOM policy above | RESOLVED IN PROPOSAL | Catalog, Inventory, Accounting, Platform/DB |
| INV-REV-02 | 30-minute configurable proposal and precise expiry semantics | RESOLVED IN PROPOSAL | Sales, Inventory; confirm default |
| INV-REV-03 | normalized active namespace and historical controls | RESOLVED IN PROPOSAL | Catalog, Inventory, Security, Platform/DB |
| INV-REV-04 | evidence-driven category basis and deterministic allocation | RESOLVED IN PROPOSAL | Purchasing, Inventory, Accounting |
| INV-REV-05 | cut-off snapshot plus ordered delta replay | RESOLVED IN PROPOSAL | Inventory, Platform/DB, QA, Organization |
| INV-REV-06 | Inventory in-transit subledger; no GL posting without canonical COA/approved Rule | RESOLVED AS FAIL-CLOSED DESIGN; GL MAPPING BLOCKED | Accounting, Inventory, Tax/Legal as applicable |
| INV-REV-07 | evidence hierarchy and deterministic shortage settlement/True-up | RESOLVED IN PROPOSAL | Inventory, Accounting, Purchasing, Platform/DB, QA |

No silence, reviewer approval, code-owner fallback or CI pass is treated as module-owner acceptance. The independent reviewer approval on PR #66 is recorded separately and does not replace these owner acceptances.

## 6. Release boundary

- Technical design dispositions: proposed in this document; not yet owner-accepted.
- Cross-module technical acceptance: OPEN until all named owners record acceptance of this version and exact head SHA.
- Physical ERD/Data Dictionary promotion: NOT AUTHORIZED until owner acceptance and integration of this proposal.
- Inventory runtime/SQL/migrations/API/publisher/grants: NOT AUTHORIZED.
- Migration 0009: remains reserved by PR #70.
- Accounting Posting Rules: none created; exact in-transit GL mapping remains blocked pending canonical evidence.
- PostgreSQL tests: NOT RUN.
- Posting Release: NOT GRANTED.
- Operational Release: NOT GRANTED.

**End of proposal.**
