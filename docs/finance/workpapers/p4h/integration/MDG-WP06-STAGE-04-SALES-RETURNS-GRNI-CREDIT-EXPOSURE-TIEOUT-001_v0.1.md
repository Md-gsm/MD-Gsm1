# Stage 04 — Sales, Returns, GRNI, CreditExposure and Financial Tie-out Contract

| Field | Value |
|---|---|
| Document ID | MDG-WP06-STAGE-04-SALES-RETURNS-GRNI-CREDIT-EXPOSURE-TIEOUT-001 |
| Version | 0.1 |
| Baseline | **MDG-ARCH-MASTER-001 v3.0.3 — sole accepted architecture baseline** |
| COA authority | COA v1.2 master coding/GL authority + COA v1.5 controlled supplementary reconstruction/crosswalk |
| Status | CONTROLLED DESIGN WORKPAPER — integrated; technical owner acceptance, canonical crosswalk and runtime evidence remain separate |
| Scope | Sales FE-01/08, customer return FE-10/10b, purchase/GRNI FE-06/07/11/20, CreditExposure, AR/AP/Treasury/Inventory/GL tie-out |
| Supersedes | No accepted owner decision; consolidates and extends the relevant proposal/workpaper fragments without changing owner policy |

## 0. Authority and decision boundary

1. v3.0.3 controls every interpretation in this workpaper. Prior architecture versions are historical references only.
2. COA v1.2 remains the master coding and GL authority. COA v1.5 is the accepted controlled supplementary reconstruction/crosswalk. No other workbook, nine-sheet source file or separate Part 4H-6 document is a prerequisite or active authority for this work.
3. This workpaper does not invent missing COA attributes, parent relations, NormalBalance, Postable/DirectPostingAllowed flags, legal tax treatment, or debit/credit pairs.
4. Existing accepted owner decisions remain in force, including OWN-FE-01-02-001 v1.0, OWN-INV-018 v1.0, CHG-050, CHG-071, CHG-083, CHG-091, CHG-100/O-65 and O-09.
5. Policy approval, design completion, technical owner acceptance, implementation, tests, evidence, gate passage and release are distinct states.
6. No DDL, migration, seed, Posting writer, positive Posting Rule, production posting, Gate B-final/C/D pass or Go-Live is authorized by this document.

## 1. Shared financial and event invariants

- Each business fact has one authoritative owner. Cross-module consumers use versioned published contracts, never direct writes to another module's tables.
- Finalized commercial, receipt, return, payment, allocation, journal and movement documents are immutable. Correction/reversal is a new linked fact with reason, actor, source version and audit.
- Commercial document finalization, delivery/handover, Inventory committed movement, transfer of control/RecognitionTime, payment receipt, allocation, PSP settlement and refund are separate facts.
- Every source event carries stable EventId, EventType, ContractVersion, AggregateId/Version, immutable source document/version/line references, OccurredAtUtc, RecordedAtUtc, server ActorReference, CorrelationId, CausationId and scoped IdempotencyKey. Monetary events declare currency, integer IRR amount and source snapshot; non-monetary events do not fabricate amount/currency.
- Same identity + same canonical payload is idempotent. Same identity + different payload fails closed and is audited. Retry preserves original identity.
- Owning source fact, Audit and Outbox are committed atomically; publish only after commit; delivery is at-least-once; consumers deduplicate and validate payload.
- Cross-module posting orchestration is atomic at the business-operation boundary: either all required inventory/financial effects commit, or none do. No orphan journal, orphan movement or half-applied allocation.
- All business dates use Asia/Tehran and central-server date. Technical timestamps are UTC. Date-only values do not timezone-shift.
- All money is integer IRR, minimum unit 1 Rial. Document-total rounding follows the v3.0.3 rule: one-time AwayFromZero at the defined document boundary, maximum absolute difference 1 IRR per document. Do not re-round line rows independently or invent a monetary threshold.
- TaxMode=0 means zero Tax Lines and zero Tax Posting through UI, API, import, integration, retry and manual channels; it is not a legal exemption. Positive tax requires an effective accepted Tax Matrix and legal evidence.
- Accounting alone owns RecognitionTime, COA, Posting Rules, Journal/GL, FiscalPeriod and COGS policy. RecognitionTime must be sourced from the approved source-event contract, never inferred from invoice issue, payment, status, OccurredAtUtc or RecordedAtUtc.

## 2. Sales contract — FE-01 and FE-08

### 2.1 Ownership

| Fact / data | Sole owner | Consumer boundary |
|---|---|---|
| SalesDocument, SalesLine, price/discount/terms/tax snapshots, commercial finalization | Sales | immutable versioned Sales source contract |
| Delivery/handover acknowledgment and recipient evidence | Sales | linked to document and source line; not an Inventory movement |
| Physical issue, committed fulfillment, stock status, valuation and movement reversal | Inventory | Inventory-owned committed movement/fulfillment contract |
| Transfer of control, RecognitionTime, revenue/COGS rule, journal | Accounting | accepted FE Posting Rule |
| Receipt, allocation, refund, PSP/bank settlement | Treasury | Treasury payment/settlement contracts |
| Customer identity | Persons (Party) | PartyId only; no duplicate Party master |

### 2.2 Required immutable Sales snapshots

Each finalized SalesDocument and line must preserve:
- SalesDocumentId, immutable version, SalesLineId and stable line version;
- PartyId and applicable recipient/authorized-representative reference;
- product/variant and source catalog version;
- quantity, UOM and conversion version;
- unit price, gross amount, line discount, document discount allocation, net amount, currency and deterministic allocation evidence;
- commercial terms/credit terms and effective version;
- TaxMode and applicable TaxSnapshotReference/result where tax is applicable;
- finalization time/actor and source references;
- line-level links to one or more Inventory fulfillment references, preserving fulfilled quantity and version.

Downstream modules consume snapshots; they do not recalculate historical price, discount, tax or UOM conversion using current master data.

### 2.3 Sales state and fulfillment

Minimum business states: DRAFT, CONFIRMED/READY, PARTIALLY_DELIVERED, DELIVERED/FINALIZED, CANCELLED/VOIDED, with correction/reversal and return represented by linked documents/events. Exact internal enum names are contract-versioned.

- Finalization creates the immutable commercial obligation/document, not by itself revenue recognition or stock movement.
- A delivery may be partial. Each accepted delivery has its own immutable delivery/fulfillment reference; outstanding quantities remain open.
- Sales owns commercial handover acknowledgment. Inventory independently owns committed physical issue/fulfillment. The two references must be linked at line and quantity level.
- A reservation, pick-list, shipment request, invoice issue, stock decrement without committed evidence, payment or PSP settlement is not proof of transfer of control.
- FE-01 revenue RecognitionTime is determined only from the approved transfer-of-control evidence and Accounting's accepted rule.
- FE-08 cost is sourced from Inventory valuation: Moving Weighted Average by ProductVariant × Warehouse for non-serialized items; Specific Identification for Serial/IMEI items. No current-price or guessed-cost fallback.
- Inventory issue, its valuation snapshot, and the corresponding Accounting posting must be linked by stable source movement and posting identities. A failed posting must not leave an untraceable stock/GL mismatch.

### 2.4 FE-01/FE-08 Posting readiness fields

For each accepted positive rule, 7B must contain: SourceModule, SourceDocument, SourceEvent, exact Trigger/commit boundary, RecognitionTime source and rule, debit and credit accounts with verified COA attributes, AmountBasis, tax treatment, revenue/COGS separation, Party/Inventory subledger dimensions, PostingIdentity, idempotency, reversal, period behavior, atomicity/retry, permission/audit and executable tests/evidence.

Until every field is verified, the event remains POSTING_BLOCKED. This workpaper does not assign revenue or COGS account codes beyond verified COA crosswalk evidence.

## 3. Customer returns, credits and refunds — FE-10 / FE-10b

### 3.1 Separate lifecycles and ownership

| Effect | Owner | Trigger/evidence |
|---|---|---|
| Commercial return entitlement and SalesReturn/CreditDocument | Sales | original finalized Sales document/line, quantity and entitlement evidence |
| Physical returned-goods receipt, inspection, quarantine and disposition | Inventory | actual physical receipt and inspection; new linked movement |
| Revenue/discount/tax reversal | Accounting | accepted commercial return/credit entitlement and original source allocation |
| COGS/inventory valuation reversal or adjustment | Accounting + Inventory | Inventory disposition and original issue valuation lineage |
| Customer refund/payment execution | Treasury | approved refund instruction and actual payment outcome |
| AR/customer credit balance | Finance-AR/AP | immutable source credit and allocation state |

No one effect implies another. A refund does not create physical stock; physical receipt does not automatically issue a refund; commercial credit does not make an item sellable.

### 3.2 SalesReturn identity and limits

- SalesReturn is a new immutable document linked to the original SalesDocument/version and original SalesLine/version.
- Returned quantity is cumulative and cannot exceed the original delivered/eligible quantity net of prior accepted returns and reversals.
- Every return line carries original source line, original allocation identity, quantity, reason, entitlement evidence, actor, timestamps and correction lineage.
- Missing original source, ambiguous quantity, missing allocation identity or over-return fails closed.
- Multiple partial returns are supported without rewriting earlier return documents or redistributing earlier rounding results.

### 3.3 v3.0.3 return-allocation rule

For each original source line, the cumulative allocated monetary return amount is computed from the immutable original integer-IRR allocation and cumulative returned quantity using the v3.0.3 rule:
- cumulative target = AwayFromZero(abs(original source allocated IRR) × cumulative returned quantity / original quantity);
- current return delta = cumulative target − previously accepted cumulative returned allocation;
- preserve the source allocation identity and destination/account/scope;
- no repeated independent rounding of each partial return;
- cumulative returned quantity and amount may not exceed the original eligible quantity and source allocation;
- full return of a line reverses exactly the original integer allocation for that line;
- missing/ambiguous source allocation or inconsistent cumulative state fails closed.

The rule applies to the appropriate immutable source allocation snapshot. It does not authorize a Posting Rule or substitute for tax/legal review.

### 3.4 Inventory disposition and COGS

Physical return enters RETURNED_PENDING_INSPECTION and is excluded from sellable ATP. Only Inventory may disposition it as SELLABLE, DEFECTIVE, QUARANTINED or UNUSABLE. Defective/quarantined/unusable stock is not sellable. Where traceable, valuation follows original issue cost lineage and the v3.0.3 lower-of-cost-and-NRV boundary; current selling price is never a cost substitute. If original issue/cost lineage is absent, create an exception and block valuation posting rather than guess.

### 3.5 Refund and credit balance

- Refund request, approval, execution attempt, success/failure, reversal and settlement are separate Treasury facts.
- Only a successful, evidenced payment settles the refund payable/credit obligation; a failed or pending payment does not.
- A SalesReturn/CreditDocument may reduce AR or create a customer credit according to the accepted AR contract; it is not itself a cash payment.
- 2107 is not Refund Payable. No refund liability account or journal is invented. Refund posting remains blocked until a canonical account and full FE-12 rule are established.
- Customer credit can be allocated only to a specific eligible exposure/document by an immutable AllocationId; unallocated credit does not reduce CreditExposure.

## 4. Purchasing receipt and GRNI — FE-06 / FE-07 / FE-11 / FE-20

### 4.1 Ownership and distinct objects

Purchasing owns PurchaseDocument, PurchaseLine, supplier terms and supplier invoice references. Inventory owns GoodsReceipt business facts, accepted/rejected quantities, committed receipt movements and valuation. Accounting owns RecognitionTime, AP/GRNI accounts and Posting Rules. Treasury owns supplier payments, allocations and settlements. Supplier identity is PartyId.

A Purchase Order alone creates neither a liability, expense, inventory balance nor GRNI. An invoice alone does not prove physical receipt, service delivery or inventory ownership.

### 4.2 GoodsReceipt contract

Every receipt must identify:
- GoodsReceiptId/version and immutable PurchaseDocumentId/version + PurchaseLineId/version;
- supplier PartyId, warehouse/location, Variant, tracking identity/lot/serial as applicable;
- received, accepted, rejected and pending-inspection quantities in base UOM with conversion version;
- actual receipt time, acceptance/inspection time, actor, evidence and reason;
- unit/extended source cost, currency, landed-cost references and valuation version as applicable;
- Inventory committed movement id/version and source-line quantities;
- correction/reversal link and idempotency identity.

Only accepted, committed quantities affect authoritative stock. Pending inspection, rejected quantity and mere arrival without accepted control do not enter sellable ATP. The ownership/control trigger is evidenced and approved, not inferred from PO or invoice date.

### 4.3 GRNI definition and separation

- COA 2103 is reserved for **physical goods received and accepted under the applicable ownership/control condition but not yet invoiced**. It is not a generic supplier payable, service accrual, invoice suspense or refund account.
- COA 2101 is the invoiced supplier AP classification and remains distinct from 2103.
- COA 2114 is only the owner-approved design candidate for a service actually received, accepted and supported by a demonstrable payment obligation but not yet invoiced. It is not goods GRNI, not 2101, and not activated for Posting until canonical attributes and FE-20 are complete.
- A service Purchase Order or unaccepted service invoice alone does not create 2114 liability.
- Supplier prepayment/advance is a separate Treasury/Finance fact and is not silently netted against GRNI/AP.
- No direct GL posting to inventory subledger classifications 110802–110806. 110801 is the sole GL inventory control account; inventory category balances are subledger dimensions/classifications.

### 4.4 Invoice/receipt matching and lifecycle

The contract supports:
- invoice before receipt;
- receipt before invoice (GRNI);
- partial receipts and partial invoices;
- one invoice matched to multiple receipts and one receipt matched across permitted invoice lines;
- price/quantity discrepancy, rejected quantity, supplier credit note and purchase return;
- tax snapshot eligibility only through accepted TaxMode/Tax Matrix;
- landed-cost additions and supplier price adjustments with lineage to affected receipt/stock/cost layers.

Each match/allocation has a stable MatchId/AllocationId, source versions, quantity/value basis, remaining unmatched amount/quantity, actor/time, and idempotency key. Overmatching, duplicate match, invoice over receipt beyond accepted tolerance, or reuse of one supplier credit twice fails closed. No tolerance amount or price variance threshold is invented; unresolved tolerance is an explicit OPEN configuration/policy.

### 4.5 Accounting event boundaries (no inferred journal pairs)

- Accepted physical goods receipt with ownership/control and no invoice: candidate GRNI recognition event; amount/cost basis must come from accepted receipt cost snapshot and approved valuation.
- Supplier invoice matched to previously recognized GRNI: candidate reclassification/clearing event from GRNI to invoiced AP, with variance and tax treatment only under approved rules.
- Invoice without accepted goods receipt: must not create inventory or claim GRNI clearing. Any separate AP recognition depends on the legally/contractually accepted invoice obligation and a complete FE-06 rule.
- Goods receipt without evidence of ownership/control: operational receipt/custody only; no automatic inventory asset/GRNI posting.
- Service accrual: FE-20/2114 only under the strict service-received/accepted/obligation evidence; currently DESIGN ONLY / NO POSTING.
- Supplier return: linked to original purchase and receipt; physical dispatch/stock reduction, supplier credit, AP/GRNI reduction and tax correction are separate linked facts. No unsupported account mapping.
- Purchase returns and supplier credits never mutate the original purchase/receipt; they create linked immutable documents and movements.

### 4.6 Return-to-supplier (FE-11)

SupplierReturn must reference original PurchaseDocument/Line and GoodsReceipt/Movement line, exact accepted quantity, tracking identity and original valuation/cost version. Inventory owns the committed dispatch and cost effect; Purchasing owns supplier commercial return/credit document; Accounting owns AP/GRNI/tax posting; Treasury owns cash refund/credit allocation. No supplier credit note, AP reduction or cash refund is assumed from a physical dispatch alone.

## 5. Credit Limit and CreditExposure

### 5.1 Canonical key and zero defaults

- Credit limit key is PartyId × Product/Item scope. Party-only limits and risk-factor substitutions are not allowed.
- New/Misc Party default credit limit is zero. Credit is unavailable until an authorized, effective-dated limit is explicitly configured.
- Limit changes require permission, reason, actor, effective time/version and immutable audit history. Existing finalized exposures retain their historical source terms; a limit change is prospective unless an explicitly authorized correction rule says otherwise.
- Credit sale is allowed only within the effective configured scope/limit, subject to overdue/returned-cheque controls and O-09. Cash sale without credit must not be blocked by CreditExposure.

### 5.2 v3.0.3 semantic formula

For a given Party, Item scope and AsOf time:

CreditExposure(Party, Item, AsOf) =
OpenAR attributable to that Party/Item
+ AcceptedUncollectedCheques that have replaced the same AR (count once)
+ ApprovedCreditCommitments within explicitly enabled scope
+ R2 installment exposure within explicitly enabled scope
− AllocatedAdvancesOrPayments explicitly applied to the same exposure.

R1 does not include R2 installment exposure unless that scope is explicitly enabled in a later accepted release. A cheque that replaces a receivable replaces that AR in the exposure calculation; it is never counted in addition to the same AR. A returned cheque restores the related AR once and changes the cheque instrument state to Returned. Unallocated receipt/advance/payment never reduces exposure. Allocations must carry PartyId, exposure/document identity, amount, currency, allocation time, source version and immutable AllocationId.

### 5.3 Available credit and decision result

AvailableCredit = max(0, EffectiveCreditLimit(Party, Item, AsOf) − CreditExposure(Party, Item, AsOf)).

The decision service returns at least:
- ALLOW_WITHIN_LIMIT;
- BLOCK_ZERO_OR_MISSING_LIMIT;
- BLOCK_LIMIT_EXCEEDED;
- REQUIRE_MANAGER_APPROVAL_OVERDUE;
- REQUIRE_REVIEW_RETURNED_CHEQUE;
- BLOCK_INVALID_OR_UNALLOCATED_SOURCE;
- BLOCK_STALE_OR_UNAVAILABLE_EXPOSURE.

No stale cache or client-side calculation can authorize credit. The server recalculates or verifies exposure within the same transaction that accepts the credit commitment. Concurrent credit sales must lock/version the Party×Item exposure scope to prevent limit oversubscription.

### 5.4 Aging and overdue

Aging uses immutable agreed DueDate, not invoice date:
- 0–30 days;
- 31–60 days;
- 61–90 days;
- over 90 days.

More than 30 days overdue generates a warning; a new credit sale then requires manager approval and recorded reason. Returned customer cheque moves the case to NEEDS_APPROVAL/review and restores the related AR once. No uniform automatic block for every customer is inferred. No generic 7-day grace period or 30-day payment term is invented.

### 5.5 O-09 separation

O-09 high-value sale approval is independent of CreditExposure and applies only when FinalSalesAmount is greater than 1,000,000,000 IRR, measured after discounts and including taxes/charges, without subtracting prepayments, receipts, credits, offsets or amount due. It applies to cash and credit sales. Approval occurs before Finalize; material changes invalidate prior approval. This is not a credit limit and does not alter CreditExposure.

## 6. Financial Tie-out and reconciliation contracts

### 6.1 Reconciliation ownership

Accounting owns GL and journal control totals. Finance-AR/AP owns customer/supplier subledgers and exposure balances. Treasury owns cash/bank/PSP/cheque/payment/receipt/allocation/settlement records. Inventory owns movement, quantity and valuation subledgers. Sales/Purchasing own commercial source documents and line allocations. Reporting may project these facts but is never the source of truth.

### 6.2 Required tie-out layers

1. Source-document to source-event: every finalized document/line and correction has exactly one stable source identity/version; event count and amount/quantity totals reconcile to immutable source snapshots.
2. Source-event to domain subledger: Sales to fulfillment/return; Purchasing to accepted GoodsReceipt/GRNI; Treasury to receipts/allocations/refunds/settlements; Inventory to movement/valuation; Finance-AR/AP to AR/AP/credit allocations.
3. Subledger to GL: each GL control account reconciles to its authoritative subledger at the same cutoff and currency; only verified control accounts are used. Inventory GL control is 110801; 110802–110806 are subledger classifications, not extra GL controls.
4. Treasury external settlement: PSP gross principal, fees, refunds/chargebacks, settlement batches, bank credits and cash transfers reconcile using unique TransferId/SettlementId/StatementLineId. Physical cash-in-transit is not conflated with non-cash PSP/bank clearing; 1110 is not a generic PSP transit account.
5. Period close: all open exceptions, pending journals, unmatched receipts/invoices, unapplied cash/credits, unresolved cheque states and GL/subledger differences are listed and governed by O-51 close states.

### 6.3 Mandatory reconciliation identities

- Sales line: finalized eligible net amount = cumulative accepted return/credit amount + remaining commercial amount, with original allocation identity preserved. Separate tax totals reconcile to their own accepted tax snapshots.
- Sales fulfillment: ordered/confirmed quantity = fulfilled committed quantity + remaining open/backorder quantity + valid cancellation quantity; no quantity is fulfilled twice.
- Inventory: opening quantity + committed receipts + accepted customer returns + positive adjustments − committed issues − supplier returns − negative adjustments = closing quantity, by Variant × Warehouse × applicable tracking identity/status. Location totals reconcile to warehouse totals.
- Inventory valuation: opening value + accepted receipt cost + eligible landed-cost/True-up adjustments + return value − issue/COGS value − supplier return value ± authorized valuation corrections = closing inventory value. WMA/Specific Identification method and cost version must reconcile.
- GRNI: opening GRNI + accepted uninvoiced goods receipt obligations − matched invoice-clearing amounts − accepted supplier returns/credits attributable to GRNI ± authorized corrections = closing GRNI, by supplier, receipt and source line. No service accrual in 2103.
- AP: opening supplier AP + accepted invoiced obligations − supplier credit notes/returns − allocated payments/settlements ± authorized corrections = closing AP, by PartyId and invoice.
- AR: opening customer AR + recognized receivable events − accepted sales credits/returns − allocated receipts/payments/cheques replacing AR ± authorized corrections = closing AR. Cheques replacing AR are not double counted.
- CreditExposure: exposure formula components reconcile to the exact AR/cheque/commitment/installment/payment-allocation source rows for the same Party×Item×AsOf.
- Treasury: opening instrument/clearing balance + receipts/settlement credits − payments/refunds/fees/settlements out ± documented timing differences = closing balance, reconciled to bank/PSP statement lines. Pending/unsettled instruments remain separately identified.
- GL: sum of posted journal debits equals credits per journal and period; control account GL balances equal corresponding subledger totals after approved reconciling items. Unposted, reversed, rejected or pending journals are never silently included.

### 6.4 Cutoff and exception management

Each reconciliation run is versioned and records:
- ReconciliationRunId, period/cutoff and Asia/Tehran business date;
- source watermarks and source contract versions;
- GL period/version, subledger snapshots and statement/import versions;
- scope dimensions (PartyId, supplier, Variant, Warehouse, account, currency);
- expected/actual totals, difference, itemized unmatched rows and aging;
- timing difference vs error classification, owner, severity and due date;
- resolution document/event references, reviewer/approver and audit;
- rerun lineage and prior-run supersession without deletion.

No unexplained difference is auto-written off, expensed, netted or concealed. A materiality threshold is not introduced. Unresolved critical differences block affected posting/close path. O-51 applies: OPEN, SOFT_CLOSED, HARD_CLOSED and YEAR_CLOSED; hard/year reopen requires independent second approval. Soft close only permits documented closing adjustments through the approved route.

### 6.5 Tie-out schedule and outputs

- Transaction-level: synchronous pre-commit invariants for each atomic business operation.
- Daily: operational reconciliation of Sales/Inventory, receipts/GRNI, AR/AP allocations and Treasury settlements.
- Period-end: full subledger-to-GL control-account reconciliation, aging, outstanding GRNI/AP/AR, cheque and settlement exceptions, valuation/COGS and return balances.
- Outputs: signed/versioned reconciliation report, exception register, source lineage, zero-duplicate proof, control-total diff and closure evidence. A report alone does not mutate balances.

## 7. 7B / COA / subledger crosswalk constraints

| Domain fact | Known account/control boundary | Status / restriction |
|---|---|---|
| Physical goods GRNI | 2103 | Goods only; exact canonical attributes and positive Posting Rule still require verification |
| Invoiced supplier AP | 2101 | Supplier subledger and FE-06 complete rule required |
| Accepted service accrual | 2114 | DESIGN ONLY / NO POSTING pending canonical attributes and FE-20 |
| Inventory GL control | 110801 | Sole GL inventory control |
| Inventory categories | 110802–110806 | Subledger classifications; no independent GL control postings |
| Inventory shortage technical clearing | 2104 | Not external supplier liability, asset or income; do not use as generic GRNI |
| Customer advance | 2105 | Liability candidate; only exact source-linked advance/allocation lifecycle |
| Unallocated customer receipt | 2107 | Not Refund Payable; no refund posting |
| Unallocated outgoing payment | 110703 | Distinct from 2107; no netting |
| Cheque/AR instruments | 1105 and related cheque subledger per v3.0.3 | Replacement and return rules must prevent double count; exact mapping per canonical COA |
| Cash-in-transit | 1110 | Physical cash only; not generic PSP/bank transit |
| 4107 / 5106 | rounding gain/loss | DESIGN ONLY until canonical attributes, namespace collision, eligibility, FE mapping, Posting Rule and tests are complete |

No unlisted revenue, tax, refund, supplier-return or clearing account is assigned by this workpaper. Account codes and debit/credit pairs must be taken from the v1.2/v1.5 verified row-level crosswalk and accepted source/decision evidence.

## 8. Test and evidence registry for Stage 04

These are required test definitions, not executed PASS results.

| Test ID | Scenario | Required positive assertion | Required negative assertion |
|---|---|---|---|
| S4-SAL-001 | cash sale, finalized but not delivered | commercial doc exists; revenue waits for approved transfer-of-control evidence | invoice/payment alone creates no revenue |
| S4-SAL-002 | partial and multi-stage fulfillment | line quantity and movement references reconcile exactly | duplicate/over-fulfillment rejected |
| S4-SAL-003 | WMA and serial sale | cost sourced from correct Inventory valuation method | guessed/current selling price not used as COGS |
| S4-RET-001 | partial customer return repeated over time | cumulative allocation delta follows v3.0.3 rule | independent re-rounding/reallocation of earlier returns rejected |
| S4-RET-002 | refund without physical return and physical return without refund | independent lifecycles remain linked | refund cannot create stock; receipt cannot create cash refund |
| S4-RET-003 | defective/quarantined return | excluded from sellable ATP | defective item cannot become sellable |
| S4-RET-004 | full return after multiple partial returns | exact original source allocation reversed | returned qty/value cannot exceed original |
| S4-GRN-001 | accepted receipt before invoice | goods-only GRNI source obligation tracked once | PO alone and unaccepted receipt create no GRNI/inventory |
| S4-GRN-002 | invoice before receipt | invoice/AP handling follows its own accepted source rule | invoice alone creates no physical stock or GRNI clearing |
| S4-GRN-003 | partial receipt/partial invoice/multiple matching | match balances and remaining quantity/value reconcile | duplicate/overmatching rejected |
| S4-GRN-004 | service accrual | 2114 only with accepted service and demonstrable obligation | goods/service 2103/2114 crossover rejected; no 2114 posting |
| S4-GRN-005 | supplier return/credit note | links original purchase, receipt and valuation | AP/GRNI/tax/cash not changed from physical dispatch alone |
| S4-CRE-001 | AR replaced by accepted cheque | exposure counts replacement once | AR + same cheque double count rejected |
| S4-CRE-002 | cheque returned | related AR restored once; case requires review | duplicate AR restoration rejected |
| S4-CRE-003 | allocated vs unallocated advance/payment | only same-scope allocation reduces exposure | unallocated receipt reduces no exposure |
| S4-CRE-004 | concurrent credit sales | server-side lock/version prevents limit oversubscription | stale/client-calculated exposure cannot authorize |
| S4-CRE-005 | >30 days overdue | warning and manager approval/reason for new credit | no invented blanket block/grace period |
| S4-CRE-006 | O-09 high-value cash and credit sale | separate approval before finalize above threshold | credit limit and O-09 not conflated |
| S4-TIE-001 | inventory GL vs subledger | 110801 equals inventory valuation control total | category subledgers are not separately posted as GL control |
| S4-TIE-002 | GRNI roll-forward | receipt/invoice/return/credit roll-forward reconciles by source | service accrual enters goods GRNI |
| S4-TIE-003 | AR/AP roll-forward | source events, allocations and balances reconcile | duplicate receipt/cheque/credit application rejected |
| S4-TIE-004 | PSP/bank/cash settlement | principal, fee, refund and transfer IDs reconcile | PSP and physical cash-in-transit double count rejected |
| S4-TIE-005 | close with unresolved difference | exception blocks affected close/posting path | unexplained difference auto-written off/netted |
| S4-ATOMIC-001 | crash between source, movement and journal writes | full rollback or deterministic idempotent recovery | orphan movement/journal/duplicate posting |
| S4-SEC-001 | unauthorized/self-approved correction or credit override | server permission, SoD and audit enforced | client-side bypass or silent override rejected |

Required runtime evidence includes PostgreSQL transaction/isolation/locking tests, replay/idempotency, outbox retry, exact-source lineage, subledger/GL snapshots, external statement fixtures, negative zero-mutation proofs and independent review on exact commit SHA. Until actually run, TestStatus remains NOT_RUN.

## 9. Open items and closure conditions

| ID | Open item | Required closure evidence |
|---|---|---|
| S4-OPEN-01 | Sales source owner acceptance and exact event schema | accepted versioned Sales contract + schema/nullability/precision registry |
| S4-OPEN-02 | Inventory fulfillment/return/valuation technical acceptance | accepted Inventory event contract and source-line/valuation fixtures |
| S4-OPEN-03 | Accounting RecognitionTime and positive FE-01/08 rules | full verified 7B rules and canonical account crosswalk |
| S4-OPEN-04 | Treasury refund/settlement lifecycle and refund payable | Treasury source contract, canonical account decision and FE-12 rule; 2107 prohibited as refund payable |
| S4-OPEN-05 | Purchasing GoodsReceipt/Invoice/Match contract | accepted Purchasing contract with partial/multiple matching and cutoff evidence |
| S4-OPEN-06 | 2103/2101 canonical row attributes and FE-06/07/11 mapping | v1.2/v1.5 row-level evidence, verified Nature/NormalBalance/eligibility/subledger and both journal sides |
| S4-OPEN-07 | 2114 / FE-20 service accrual | canonical account attributes, service acceptance contract, demonstrable obligation, positive rule and tests |
| S4-OPEN-08 | CreditExposure source projection and concurrency | accepted AR/cheque/commitment/allocation contracts and PostgreSQL concurrency tests |
| S4-OPEN-09 | Financial tie-out control-account map and cutoffs | canonical control-account map, source watermarks, statement contracts and zero-difference/exception fixtures |
| S4-OPEN-10 | Tax and legal source evidence | effective accepted Tax Matrix and legal evidence; TaxMode=0 negative tests |
| S4-OPEN-11 | 7D runtime tests and evidence | exact-SHA PostgreSQL/runtime tests, independently reviewed evidence |
| S4-OPEN-12 | B-final Gate closure | 7B positive rules, 7C canonical attributes, 7D runtime evidence and independent review all accepted |

## 10. Acceptance matrix and current result

| Layer | Stage 04 result |
|---|---|
| Owner-policy alignment to v3.0.3 | COMPLETED IN THIS WORKPAPER |
| Sales/returns/GRNI/CreditExposure semantic contract | INTEGRATED DESIGN SPECIFIED |
| Financial tie-out identities and exception model | SPECIFIED |
| COA mapping | ONLY VERIFIED EXISTING BOUNDARIES; remaining mappings OPEN |
| Named module technical owner acceptance | PENDING |
| Canonical schema/API/ERD | NOT IMPLEMENTED / NOT VERIFIED |
| Positive 7B Posting Rules | NOT ACCEPTED BY THIS DOCUMENT |
| PostgreSQL/runtime tests | NOT RUN / NOT VERIFIED |
| Independent exact-SHA review | PENDING |
| B-final / Gate C / Gate D | OPEN |
| Posting Release / production migration / Go-Live | NOT GRANTED |

### Final disposition

Stage 04 is complete as an integrated, v3.0.3-aligned **design contract and reconciliation specification**. It is not represented as implemented, technically accepted by every module owner, runtime-tested or release-ready. All unknown source/account/legal details remain explicit OPEN/BLOCKED; no fabricated account, journal pair, threshold or event trigger is introduced.

**END — MDG-WP06-STAGE-04-SALES-RETURNS-GRNI-CREDIT-EXPOSURE-TIEOUT-001 v0.1**
