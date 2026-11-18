# FE-01 / FE-02 Source Crosswalk Proposal

| Field | Value |
|---|---|
| Document ID | MDG-WP06-FE01-FE02-SOURCE-CROSSWALK-PROPOSAL-001 |
| Version | 0.1 |
| Status | PROPOSAL — SOURCE OWNER REVIEW REQUIRED |
| Parent | 7B Posting Matrix v0.2; 7C Reconciliation v0.4; integrated baseline v0.6 |
| Architecture | MDG-ARCH-MASTER-001 v2.5 |
| Posting Release | NOT GRANTED |

## 1. Purpose

This document connects the Accounting questions for FE-01 and FE-02 to the proposed source facts of their owning modules. It is a proposal for review, not an accepted crosswalk or Posting Rule.

## 2. Crosswalk

| FE | Operational owner | Proposed source facts | Required external references | RecognitionTime | Current result |
|---|---|---|---|---|---|
| FE-01 — goods sale | Sales owns SELL-01 document/lines; Inventory owns STK-01 committed movement | SalesDocumentFinalized plus Inventory-owned fulfillment/movement evidence | Immutable Sales line snapshot; committed Inventory movement linked to SalesLineId; approved Pricing/Tax snapshots where applicable | OPEN — must be explicitly decided by Accounting and source owners; no timestamp substitution | BLOCKED |
| FE-02 — repair service | RepairOperations owns REP-01 repair lifecycle; Sales/Invoice owns invoice; Inventory owns part movements | RepairTechnicalCompletionRecorded; optional RepairQualityCheckPassed; optional RepairOrderHandedOver | Repair service-line snapshot; invoice/document line reference; Inventory part-movement references; approved Pricing/Tax snapshots where applicable | OPEN — technical completion, quality, handover and invoice finalization are distinct candidate facts; no trigger selected | BLOCKED |

## 3. Non-conflation rules

1. Sales document finalization is not proof of goods delivery or Inventory ownership/control transfer.
2. A Sales payment/receipt is not a substitute for a goods fulfillment event.
3. Repair technical completion is not automatically quality acceptance or customer handover.
4. A repair order is not a second invoice; invoice identity and line ownership remain with Sales/Invoice.
5. Repair part consumption is an Inventory-owned movement and is not the same event as repair service revenue.
6. Revenue recognition must not be inferred from `OccurredAtUtc`, document issue time, payment time, assignment time or status labels.
7. Service revenue, parts COGS, outsourced-service liability and technician commission remain distinct event/rule concerns.
8. No tax rate, account attribute, debit/credit side, amount basis or normal balance is invented in this proposal.

## 4. Required common contract controls

Before either source contract is published, its owner must approve:
- stable EventId, aggregate identity and aggregate version;
- explicit contract version and schema compatibility rules;
- immutable source-document/revision references;
- occurred and recorded timestamps with UTC semantics;
- server-derived actor reference where applicable;
- correlation/causation references;
- replay, duplicate, retry and correction semantics;
- no unnecessary personal or payment-instrument data;
- contract tests for identity, immutability, versioning and reference integrity.

Outbox/transport implementation is not assumed by this proposal and requires platform design/approval.

## 5. Acceptance sequence

1. Sales owner reviews and accepts the Sales proposal and confirms its dependency on Inventory fulfillment evidence.
2. Inventory owner publishes/reviews its own committed movement/fulfillment contract.
3. RepairOperations owner reviews and accepts the repair lifecycle proposal.
4. Sales/Invoice owner confirms repair invoice references and line snapshots.
5. Pricing and Tax owners confirm their source snapshots and effective rules, if relevant.
6. Accounting owner approves the FE-01/FE-02 source mapping and RecognitionTime decisions.
7. 7C account eligibility and 7D expected results are cross-audited against the accepted source/decision evidence.
8. Only after all required evidence exists may a positive Posting Rule be considered; implementation, tests, independent review and Posting Release remain separate gates.

## 6. Open decisions

- SALES-EVT-001..007: all OPEN in the Sales proposal.
- REPAIR-EVT-001..010: all OPEN in the RepairOperations proposal.
- Inventory fulfillment contract: not present in this repository slice; OPEN.
- FE-01 RecognitionTime: OPEN.
- FE-02 RecognitionTime: OPEN.
- FE-01/FE-02 debit/credit, amount, tax, subledger and dimensions: OPEN pending source and canonical 7C evidence.

## 7. Status

This proposal makes the missing source dependencies explicit and reviewable. It does not change 7B v0.2, 7C v0.4, 7D v0.2, the integrated v0.6 baseline or Architecture v2.5. It does not add runtime source events, adapters, migrations, journals or posting behavior.

- FE-01: BLOCKED.
- FE-02: BLOCKED.
- Accepted positive Posting Rules: 0.
- Runtime tests: NOT RUN / NOT VERIFIED.
- Independent review: PENDING.
- Posting Release: NOT GRANTED.

**End of proposal.**
