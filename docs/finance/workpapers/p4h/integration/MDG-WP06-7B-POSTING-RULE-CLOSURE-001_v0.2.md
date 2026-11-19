# MDG-WP06-7B-POSTING-RULE-CLOSURE-001
## 7B Posting Rule Closure Ledger — Version 0.2

| Field | Value |
|---|---|
| Document ID | MDG-WP06-7B-POSTING-RULE-CLOSURE-001 |
| Version | 0.2 |
| Classification | CONTROLLED RECONSTRUCTION — DISPOSITION / CLOSURE LEDGER |
| Architecture baseline | MDG-ARCH-MASTER-001 v2.5 |
| Current inputs | 7B v0.2; 7C v0.4; 7D v0.2; Integrated v0.6 |
| Owner change control | CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 v0.1 — OWNER APPROVED |
| Decision overlays | OWN-7C-043; OWN-7C-004-R1; ADR-0017 |
| Design disposition coverage | 39/39 event rows recorded |
| Positive Posting Rules | 0 ACCEPTED |
| Implementation / runtime tests | NOT VERIFIED / NOT RUN where stated |
| Evidence | NOT ACCEPTED |
| Posting Release | NOT GRANTED |

## 1. Purpose and interpretation

This version replaces the stale closure snapshot v0.1 as the current 7B closure ledger. It reconciles the closure work with the current controlled workpapers; it does not rewrite or supersede Architecture v2.5, owner decisions, ADRs, or the primary 7B Posting Matrix.

Two independent questions are tracked:

1. **Policy/disposition closure:** is the known policy boundary, prohibition, or scope decision explicitly recorded?
2. **Posting-rule acceptance:** is the complete event contract evidenced, including SourceEvent, Trigger, Preconditions, RecognitionTime, SourceDocument, both journal sides and conditions, AmountBasis, CostBasis, TaxMode, subledger/dimensions, inventory/party effects, PostingIdentity, idempotency, reversal, period, atomicity, retry, authorization, audit, tests, evidence, and source references?

A policy disposition may be closed while the event remains `POSTING_BLOCKED`. No item below grants a positive posting rule.

## 2. Current authoritative workpaper set

- 7B: `WP06-P4H-7B-POSTING-MATRIX-001 v0.2` — 39 event rows (FE-01..FE-38 plus separate FE-10b).
- 7C: `WP06-P4H-7C-COA-RECONCILIATION-001 v0.4` — 78 identifiers in scope; unresolved canonical attributes remain unassigned/blocked.
- 7D: `WP06-P4H-7D-TEST-REGISTRY-001 v0.2` — 67 Test IDs; ID coverage is not test execution.
- Integrated: `MDG-WP06-7B-7C-7D-INTEGRATED-001 v0.6`.
- O-01 replacement acceptance criterion: `CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 v0.1`, owner-approved. The prior request to obtain a new CR is obsolete; O-01 itself remains open until deliverable/evidence acceptance.

Authority order remains Architecture v2.5, accepted owner decisions/ADRs, COA v1.2 as master coding/GL authority, COA v1.4 as current supplementary account-level register based on v1.2, and v1.3 as intermediate only. No account attribute may be inferred from a code prefix or title.

## 3. Event-level closure disposition — 39/39 rows

The status in this table concerns policy/disposition only. Every row remains blocked from positive posting until its complete Posting Rule and evidence are accepted.

| Event | Policy/disposition established by current sources | Remaining posting-rule blocker | Posting status |
|---|---|---|---|
| FE-01 | 4101 is candidate only; sale recognition requires a valid delivery/recognition condition. | SourceEvent, delivery trigger, RecognitionTime, source document, canonical counterpart, amount/tax and subledger. | BLOCKED |
| FE-02 | 4102 is candidate only; an open/incomplete service is not recognized revenue. | Repair completion/delivery source event, RecognitionTime, source document, both accounts, amount/tax and subledger. | BLOCKED |
| FE-03 | Receipt alone is not revenue; cash/bank/PSP paths must not be conflated. | Payment-method-specific destination/clearing, fee, source identity, allocation and both journal sides. | BLOCKED |
| FE-04 | 2105 is a customer-advance liability candidate, not income. | Obligation lifecycle, receipt-side mapping, allocation/consumption, refund, reversal and subledger. | BLOCKED |
| FE-05 | Allocation is separate from receipt; duplicate revenue is prohibited. | Partial/multiple allocation, over-allocation, unapplied balance, reversal and idempotency contract. | BLOCKED |
| FE-06 | 2101 is invoiced AP; 2103 is goods GRNI; 110701 is recoverable purchase tax only under valid TaxMode/Tax Matrix. | Invoice/receipt sequence, inventory/cost recognition, tax eligibility and both sides. | BLOCKED |
| FE-07 | 110801 is the sole GL inventory control; 110802–110806 are inventory subledger classifications, not separate GL controls. | Ownership/control transfer, receipt trigger, valuation, quantity/cost basis, GRNI relationship and atomicity. | BLOCKED |
| FE-08 | 5101 is candidate; WAC is scoped to ProductVariant × Warehouse and Specific Identification applies to serial items. | Valuation service/source movement, cost layers, inventory credit, timing and concurrency. | BLOCKED |
| FE-09 | 110807 is inactive in R1; it is not a generic WIP account; pre-recognition COGS is prohibited. | Any future repair-part consumption rule requires a separately approved scope/ownership contract; no R1 activation. | BLOCKED_R1 — policy guard established |
| FE-10 | Financial return is distinct from physical inventory return. | Credit/refund linkage, price/tax/COGS treatment, source link and reversal rule. | BLOCKED |
| FE-10b | Physical return movement is separate from the financial refund. | Inspection/disposition, warehouse, valuation and link to original movement/document. | BLOCKED |
| FE-11 | Supplier return must link to the original purchase/receipt. | Physical return vs credit note, inventory, tax, AP/GRNI and reversal. | BLOCKED |
| FE-12 | 2107 is not Refund Payable; it represents incoming unapplied funds. | Canonical refund liability/settlement account and full refund lifecycle are not established. | BLOCKED — prohibition established |
| FE-13 | Discount has Contra Revenue nature; 4201–4203 are not canonicalized by this statement. | Account mapping, reason codes, NormalBalance, limits, authorization and tests. | BLOCKED |
| FE-14 | OWN-7C-004-R1 fixes roles: 110401 cheque held; 110402 endorsed to bank; 110403 in collection; 110404 returned pending disposition. | Source-title/attribute reconciliation, lifecycle transitions, dates, subledger, no-double-count and lifecycle tests. | BLOCKED — policy roles established |
| FE-15 | 6104 is not a generic Landed Cost account. | Economic nature, capitalization vs expense, allocation basis and approved policy. | BLOCKED |
| FE-16 | 110808 is shortage suspense; 2113 is excess-count suspense; excess is not automatic income. | Count evidence, recount, independent approval, suspense clearing and source movement. | BLOCKED |
| FE-17 | 2112 is cash-variance suspense; 610801 is limited to confirmed and approved cash shortage. | Cash count, cause, independent approval, aging/clearing and evidence. | BLOCKED |
| FE-18 | 1110 is physical cash in transit only; 110302 is PSP-related. | Transfer/clearing lifecycle and bank/PSP reconciliation without double counting. | BLOCKED |
| FE-19 | Damage, waste, theft, failure and technician responsibility are distinct economic causes. | Cause taxonomy, evidence, approval, recovery vs expense, account and tax treatment. | BLOCKED |
| FE-20 | 2114 is only for received/accepted service with provable payment obligation and no invoice; 2102 is inactive; 5103 remains blocked. | Service evidence/acceptance, canonical expense account, RecognitionTime, invoice settlement, reversal and subledger. | BLOCKED — liability definition only |
| FE-21 | Trade-In requires two independent documents (purchase from customer and sale) linked by TradeInId; netting is prohibited; IMEI is not transaction identity; 110803 follows accepted ownership/recognized inventory. | Ownership evidence, valuation, inventory recognition, duplicate-IMEI handling, tax and linked-document lifecycle. | BLOCKED |
| FE-22 | 6106/6107 are candidates; 110302 PSP balance must not be double-counted. | Statement source, fee recognition, gross/net settlement, reconciliation and fee identity. | BLOCKED |
| FE-23 | 5104/2108 are candidates only after valid technical completion; payroll is separate. | Commission basis/eligibility, approval, reversal/clawback, authorization and tests. | BLOCKED |
| FE-24 | Fixed-asset lifecycle is R2; 1201 cost, 1202 accumulated depreciation, 6105 depreciation expense. | R2 approval, fixed-asset subledger, lifecycle and tests. No R1 activation. | DEFERRED_R2 |
| FE-25 | Installments are outside R1. | R2 approval and schedule/interest/receivable/settlement contract. | DEFERRED_R2 |
| FE-26 | FX/revaluation is outside R1. | R2 approval, rate source/effective date and realized/unrealized rules. | DEFERRED_R2 |
| FE-27 | Tax posting requires valid TaxMode and Tax Matrix; TaxMode=0 blocks all tax lines across channels. | Authoritative tax matrix, effective dates, exemptions, rounding and UI/API/import/integration/manual negative tests. | BLOCKED |
| FE-28 | 3199 is temporary opening-balance control, not equity or income. | Opening source balances, measurement basis, approval, reconciliation and clearing. | BLOCKED |
| FE-29 | Period close requires reconciliation and no disallowed pending journals; reopen needs independent approval. | Complete close checklist, hard/soft close, lock, reopen authorization and repeat-safe tests. | BLOCKED |
| FE-30 | Manual journal requires server-side permission, segregation of duties and auditability. | Role/permission matrix, account eligibility, approval, attachments/reason, reversal and security tests. | BLOCKED |
| FE-31 | Inter-warehouse transfer is outside R1. | R2 approval, inventory ownership/transfer contract and atomic movement tests. | DEFERRED_R2 |
| FE-32 | Generic WIP is outside R1; 110807 is not a WIP substitute and remains inactive. | R2 approval, WIP ownership/valuation and settlement contract. | DEFERRED_R2 |
| FE-33 | 2107 is incoming unapplied receipt; 110703 is outgoing unapplied payment; they are distinct. | Direction-specific allocation, reversal, aging, reconciliation and no-netting tests. | BLOCKED |
| FE-34 | Year close requires unique identity, idempotency, atomicity and approved equity mapping. | Canonical equity map, carry-forward workflow, approvals and repeat-safe close tests. | BLOCKED |
| FE-35 | Expense account follows actual economic nature; 110702 is only a genuine prepayment. | Expense taxonomy, source evidence, service period, tax and allocation schedule. | BLOCKED |
| FE-36 | Unknown event/account/mapping must fail closed; no fallback posting is allowed. | Runtime negative test, alert/review trail and evidence. | BLOCKED — policy established |
| FE-37 | Payroll is R2; operational commission remains FE-23 and must not be merged into payroll. | R2 payroll owner contract and tests; no R1 payroll activation. | DEFERRED_R2 |
| FE-38 | 3101–3105 equity mapping is unresolved; 3199 remains temporary opening control only. | Approved equity agreement, source, NormalBalance, mapping and year-close tests. | BLOCKED |

## 4. Closure ledger for 7B-CL-001..020

Status vocabulary:
- **CLOSED — POLICY/DISPOSITION ONLY:** the specified policy or scope boundary is settled; this does not close the Posting Rule.
- **PARTIAL:** some controlling policy is settled, but the closure criterion still has material open fields.
- **OPEN:** the required policy/source/contract/evidence has not been established.

| Closure ID | Current disposition | Status | Remaining work |
|---|---|---|---|
| 7B-CL-001 — FE-01/02 source and recognition | Candidate revenue codes and delivery/completion principle recorded; exact source contracts and recognition times absent. | OPEN | Sales and RepairOperations source event contracts; both journal sides; 7C canonical eligibility; tests. |
| 7B-CL-002 — FE-03/04/05 funds | Receipt is not revenue; 2105 is advance liability; allocation is independent. | PARTIAL | Cash/bank/PSP clearing, obligation lifecycle, allocation/reversal and subledger. |
| 7B-CL-003 — FE-06/07 purchase and receipt | 2101/2103 distinction, 110701 tax constraint and 110801/subledger boundary recorded. | PARTIAL | Invoice/receipt sequencing, ownership, valuation, tax matrix and atomicity. |
| 7B-CL-004 — FE-08/09 sale COGS and repair parts | WAC/Specific Identification scope recorded; 110807 R1 prohibition recorded. | PARTIAL | FE-08 cost layers/source movement/concurrency; FE-09 remains R1 blocked pending separate scope contract. |
| 7B-CL-005 — FE-10/10b/11/12 returns | Financial vs physical return separation; 2107 not Refund Payable. | PARTIAL | Refund liability, credit note, physical disposition, tax, settlement and reversals. |
| 7B-CL-006 — FE-13 discount | Contra Revenue nature recorded. | OPEN | Canonical 420x mapping, reason codes, NormalBalance and authorization. |
| 7B-CL-007 — FE-14 cheque | OWN-7C-004-R1 account roles are established. | PARTIAL | Source title/attributes, state transition, dates, subledger and lifecycle tests. |
| 7B-CL-008 — FE-15 landed cost | 6104 generic use prohibited. | OPEN | Economic classification and approved capitalization/expense allocation policy. |
| 7B-CL-009 — FE-16/17/19 variances | Suspense boundaries and limited 610801 use recorded. | PARTIAL | Cause, counting/cash evidence, approvals, clearing and damage taxonomy. |
| 7B-CL-010 — FE-18/22 clearing and fees | 1110 physical cash-in-transit and 110302 PSP are distinct; fee double-counting prohibited. | PARTIAL | Full clearing/statement/fee reconciliation and posting contract. |
| 7B-CL-011 — FE-20 outsourced service | 2114 liability definition and 2102/5103 restrictions recorded. | PARTIAL | Canonical account attributes, expense mapping, service acceptance, invoice settlement and reversal. |
| 7B-CL-012 — FE-21 Trade-In | Two documents, shared TradeInId, no netting, IMEI not identity and ownership gate recorded. | PARTIAL | Valuation, tax, subledger and linked-document tests. |
| 7B-CL-013 — FE-23 commission / FE-37 payroll | Technical-completion condition and commission/payroll separation recorded. | PARTIAL | Commission calculation/eligibility; payroll remains R2 and separately gated. |
| 7B-CL-014 — R2 scope guards | FE-24/25/26/31/32/37 are explicitly R2/deferred; FE-23 commission remains distinct from FE-37 payroll. | CLOSED — POLICY/DISPOSITION ONLY | No R1 posting. Any R2 work requires its own approved phase/scope and contracts; no R2 implementation/test is claimed. |
| 7B-CL-015 — FE-27 tax | TaxMode=0 no-tax invariant and need for Tax Matrix recorded. | OPEN | Approved Tax Matrix, effective dates, exemptions, all-channel negative/positive tests. |
| 7B-CL-016 — FE-28/29/34/38 opening/period/year/equity | 3199 temporary-control boundary and close/reopen invariants recorded. | OPEN | Opening evidence, canonical equity mapping, complete close workflow and tests. |
| 7B-CL-017 — FE-30 manual journal | Permission, SoD and audit principles recorded. | OPEN | Actual role matrix, approval workflow, account eligibility and security tests. |
| 7B-CL-018 — FE-35 expense | Economic-nature rule and genuine-prepayment constraint recorded. | OPEN | Expense taxonomy, evidence, tax and allocation policy. |
| 7B-CL-019 — FE-36 unknown event | Fail-closed/no-fallback policy is explicit. | CLOSED — POLICY/DISPOSITION ONLY | Runtime negative test, alerting/review evidence and exact Test execution remain NOT RUN. |
| 7B-CL-020 — FE↔COA↔Test | Structural event/Test linkage is 39/39; 7C scope has 78 identifiers. | PARTIAL | Canonical account eligibility and exact row-level FE↔COA↔Test expected-result crosswalk; execution evidence. |

## 5. R1/R2 and policy-only closure boundaries

The following are now settled as policy/disposition boundaries, not as accepted positive posting rules:

1. FE-09: 110807 remains inactive in R1; it is not generic WIP.
2. FE-12: 2107 is not Refund Payable.
3. FE-14: OWN-7C-004-R1 defines the four cheque account roles; source attribute reconciliation remains open.
4. FE-20: 2114 is restricted to received/accepted service with a provable obligation and no invoice; expense mapping remains open.
5. FE-24/25/26/31/32/37: R2/deferred boundary; FE-37 payroll is distinct from FE-23 operational commission.
6. FE-36: unknown event/account/mapping fails closed with no fallback.

These are not Debit/Credit approvals. Any runtime path that creates a Journal, inventory mutation or subledger mutation from an unresolved rule remains prohibited.

## 6. Test and evidence status

- Structural validation of the 7B/7C/7D package passed on the exact PR head `c1d38847a0e7f5c4ed3fc3f588cfe6eae771480d` (Finance Workpaper Structural Validation run #96).
- The successful workflow verifies document structure/crosswalk counts only: 39 7B event rows, 39 fail-closed gate references, 16 7C Open Items mapped to 16 COA gate tests, and 67 unique 7D Test IDs.
- It does not verify positive journal behavior, runtime posting, PostgreSQL persistence, accounting/tax correctness, or end-to-end atomicity.
- 7D execution status remains NOT RUN / NOT VERIFIED except for separately identified domain fail-closed tests; no unexecuted test is reported as PASS.
- Independent review on the final SHA and professional accounting/tax review remain pending.

## 7. Required next work

1. Complete 7C canonicalization from COA v1.2/v1.4 and accepted owner decisions, without prefix inference.
2. For each R1 event, obtain the owning module's versioned SourceEvent/Trigger/SourceDocument contract and explicit RecognitionTime.
3. Only then determine whether a complete debit/credit pair and amount/cost/tax/subledger rule is source-supported.
4. Complete PostingIdentity, idempotency, reversal, period, atomicity, retry, authorization and audit contracts.
5. Complete exact FE↔COA↔Test expected-result mapping in 7D and execute tests when the required runtime exists.
6. Obtain independent review on the exact final SHA and required professional review before any acceptance/release decision.

The prior v0.1 instruction to seek a new O-01 CR is superseded: the replacement criterion has already been owner-approved. Do not edit Architecture v2.5 in this work.

## 8. Final status

- 7B event disposition coverage: 39/39 — COMPLETE AT POLICY/DISPOSITION LEVEL.
- Policy-only closure items: 7B-CL-014 and 7B-CL-019 — CLOSED at policy/disposition level only.
- Partial policy decisions: recorded per §4.
- Fully accepted positive Posting Rules: 0.
- Posting Matrix overall: PARTIAL / NOT ACCEPTED.
- Implementation: NOT VERIFIED.
- Tests: structural validation PASS; accounting runtime tests NOT RUN / NOT VERIFIED.
- Evidence: NOT ACCEPTED.
- Gate 3 / Gate 4: OPEN / OPEN.
- O-01: OWNER-APPROVED REPLACEMENT CRITERION / DELIVERABLE ACCEPTANCE PENDING.
- Posting Release: NOT GRANTED.

**End of document.**

## Current v3.0.3 authority and B-final status — 2026-11-20

This section supersedes conflicting historical authority statements. Accepted architecture is MDG-ARCH-MASTER-001 v3.0.3. COA v1.2 is master; COA v1.5 is the controlled supplementary reconstruction/crosswalk recognized by v3.0.3; v1.4 is historical/superseded for current supplementary authority; v1.3 is intermediate. Original nine-sheet COA and Part 4H-6 remain unrecovered.

Integer monetary base is 1 IRR. Exact document net is rounded once at document total using AwayFromZero; maximum absolute rounding difference is 1 IRR per document. No per-row monetary threshold exists. O-65 / CHG-100 controls mixed-sign D/P/N allocation as stated in v3.0.3 P.4. O-09 is a separate high-value sales approval rule.

The 39/39 event dispositions remain complete only at policy/disposition level. Accepted positive Posting Rules remain 0. Every FE remains POSTING_BLOCKED until the complete source event, trigger, RecognitionTime, both debit/credit sides, amount/cost basis, tax, canonical account eligibility, subledger/dimensions, PostingIdentity/idempotency, reversal, period, atomicity/retry, authorization/audit, test and evidence are complete. No fallback account or inferred recognition time is allowed. B-final remains OPEN; Posting Release NOT GRANTED.
