# FE-01 / FE-02 Source Crosswalk Proposal

| Field | Value |
|---|---|
| Document ID | MDG-WP06-FE01-FE02-SOURCE-CROSSWALK-PROPOSAL-001 |
| Version | 0.2 |
| Status | PROPOSAL — SOURCE OWNER REVIEW REQUIRED |
| Parent | 7B Posting Matrix v0.2; 7C Reconciliation v0.4; integrated baseline v0.6 |
| Architecture | MDG-ARCH-MASTER-001 v2.5 |
| Owner policy | OWN-FE-01-02-001 v1.0 — APPROVED |
| Posting Release | NOT GRANTED |

## 1. Purpose and authority

This crosswalk translates the approved business policy into source-evidence dependencies for Accounting. OWN-FE-01-02-001 v1.0 is approved policy authority. This document remains a technical proposal until Sales, RepairOperations, Inventory, Pricing/Tax and Accounting owners accept their respective source responsibilities.

It is not a runtime contract, accepted Posting Rule, RecognitionTime approval, journal instruction or Posting Release.

## 2. FE-01 — goods sale

| Dimension | Required source/evidence | Owner |
|---|---|---|
| Commercial obligation | Immutable SalesDocument/line snapshot, finalized terms, quantity, price, discount, currency and applicable tax snapshot | Sales; Pricing/Tax for owned snapshots |
| Fulfillment | Committed Inventory fulfillment/movement record, linked to originating SalesLineId and fulfilled quantity | Inventory |
| Delivery/control | Evidence of commercial handover/transfer of control under approved policy, linked to Inventory fulfillment and recipient acknowledgment where applicable | Sales + Inventory |
| Corrections/returns | Commercial return entitlement/credit/reversal reference | Sales |
| Physical return/disposition | Inspection, quarantine, sellable/defective/unusable disposition and any stock/valuation reversal | Inventory |
| Receipt/settlement | Payment, allocation, PSP settlement and refund facts kept separate from revenue recognition | Payment/Finance owner |
| RecognitionTime | Explicit Accounting decision based on accepted source evidence; no timestamp substitution | Accounting |

FE-01 source completeness requires immutable Sales line evidence plus Inventory-owned committed fulfillment and delivery/control evidence. SalesDocumentFinalized, payment receipt, PSP settlement, reservation, pick-list, shipment request or uncommitted stock operation alone is insufficient. Revenue/tax reversal, refund and Inventory restock/COGS reversal are separate effects with separate evidence.

## 3. FE-02 — repair service

| Dimension | Required source/evidence | Owner |
|---|---|---|
| Repair obligation/lifecycle | RepairCase, RepairServiceLine and RepairExecutionStep with immutable revisions (R2 enabled workflow only) | RepairOperations |
| Technical completion | Authorized technician completion fact with server-derived actor/time | RepairOperations |
| Quality control | Separate QC result/evidence only where required; independent actor where policy requires | RepairOperations |
| Readiness | Ready-for-handover fact; not itself customer handover | RepairOperations |
| Handover | Confirmed handover to customer/authorized recipient with RecipientReference and authorization evidence | RepairOperations |
| Commercial amount/tax | Sales/Invoice-owned document/line and approved Pricing/Tax snapshots where applicable | Sales + Pricing/Tax |
| Parts | Inventory-owned committed part movement and valuation reference | Inventory |
| Outsourced service/commission | Separate evidence and rules; never embedded by assumption into service revenue | Relevant source owner + Accounting |
| RecognitionTime | Explicit Accounting decision; standard physical repair requires technical completion plus confirmed handover | Accounting |

For standard physical repair, technical completion and confirmed handover are both required. Alternative customer acceptance without physical handover is permitted only for a separately identified service-catalog item with approved policy and independent evidence. Partial recognition applies only to independent, separately measurable service obligations.

R1 repair intake is disabled. R1 FE-02 financial posting is unavailable unless a real, authorized, immutable and independently addressable source already supported in R1 is identified and accepted. No synthetic RepairCase or lifecycle event may be created to bypass intake prohibition. Full workflow/WIP/profitability remain R2; R1 WIP is disabled.

## 4. Shared contract controls

For every relevant event, the source owner must specify:
- EventId (stable immutable event identity), EventType and ContractVersion;
- AggregateId/AggregateVersion;
- immutable SourceDocumentReference and source revision/lineage;
- OccurredAtUtc and RecordedAtUtc;
- server-derived ActorReference where applicable;
- separate RecipientReference/authorization evidence where relevant;
- CorrelationId/CausationId;
- PartyId only where party reference is needed;
- scoped request IdempotencyKey, distinct from EventId;
- per-event required/optional/nullability/type/precision rules for Currency, SourceAmount and TaxSnapshotReference; non-monetary events do not invent monetary fields.

Same identity/key with a different canonical payload fails closed. Corrections/reversals are new linked facts. Retry preserves original identity and must not duplicate effects.

The approved shared policy requires source fact/document, audit record and Outbox record to persist atomically in the owning DB transaction. Publish only after commit. Delivery is at-least-once with idempotent consumers; exactly-once transport is not claimed. This policy is not proof that runtime outbox exists.

## 5. Accounting non-conflation and posting boundary

1. RecognitionTime is owned by Accounting and is not inferred from OccurredAtUtc, RecordedAtUtc, invoice issue, payment, settlement or status.
2. Revenue recognition, receipt, allocation, settlement, refund, tax reversal, Inventory movement, COGS, outsourced-service liability and commission entitlement are distinct events/rules.
3. No debit/credit account, account attribute, amount basis, tax rate, normal balance or subledger is inferred here.
4. Exact account mappings require canonical COA authority and accepted Posting Matrix evidence; no prefix/title inference.
5. Accepted positive Posting Rules remain 0. No Posting Release is granted.

## 6. Owner acceptance sequence

1. Sales accepts commercial document/line snapshot, delivery acknowledgment and correction/return semantics.
2. Inventory defines and accepts its own committed movement/fulfillment, partial fulfillment, return inspection/disposition and valuation contract.
3. RepairOperations accepts R2 repair lifecycle and enforces R1 intake prohibition.
4. Sales/Invoice accepts repair invoice references and service-line commercial snapshots.
5. Pricing/Tax accepts money, discount, rounding and tax snapshot ownership/effective references.
6. Accounting accepts FE-01/FE-02 source mapping and separately determines RecognitionTime.
7. Platform Architecture confirms common envelope, scoped idempotency and transactional outbox implementation design.
8. Contract tests, PostgreSQL atomicity/idempotency tests and independent review pass on exact commit before any Posting Rule can be considered.

## 7. Open dependencies

- Sales technical acceptance: OPEN.
- Inventory committed movement/fulfillment contract: NOT STARTED.
- RepairOperations technical acceptance: OPEN.
- Pricing/Tax snapshot acceptance: OPEN.
- Accounting RecognitionTime for FE-01: OPEN.
- Accounting RecognitionTime for FE-02: OPEN.
- R1 eligible external repair source: NOT IDENTIFIED; FE-02 R1 posting remains blocked.
- Exact account mappings and positive Posting Rules: OPEN / 0 accepted.

## 8. Status

- Owner policy OWN-FE-01-02-001: APPROVED.
- Sales proposal v0.2: PROPOSAL — OWNER REVIEW REQUIRED.
- RepairOperations proposal v0.2: PROPOSAL — OWNER REVIEW REQUIRED.
- Inventory contract: NOT STARTED.
- FE-01: BLOCKED.
- FE-02: BLOCKED.
- Runtime contracts/tests: NOT IMPLEMENTED / NOT RUN / NOT VERIFIED.
- Independent review: PENDING.
- Accepted positive Posting Rules: 0.
- Posting Release: NOT GRANTED.

**End of proposal.**
