# RepairOperations Source Event Contract Proposal — FE-02 dependency

| Field | Value |
|---|---|
| Document ID | REPAIROPERATIONS-SOURCE-EVENT-CONTRACT-PROPOSAL-001 |
| Version | 0.2 |
| Status | PROPOSAL — OWNER REVIEW REQUIRED |
| Scope | Source facts required to evaluate FE-02 (repair service) |
| Architecture | MDG-ARCH-MASTER-001 v2.5 |
| Owner decision | OWN-FE-01-02-001 v1.0 — APPROVED POLICY |
| Data ownership | REP-01 — Repair workflow; SELL-01 — Sales/Invoice; STK-01 — Inventory |

## 1. Authority and strict R1 boundary

This proposal translates approved owner policy into candidate source facts. It is not a runtime contract, event publisher, Posting Rule or Posting Release.

RepairOperations owns repair workflow data only when that workflow is enabled. Sales/Invoice owns invoices and invoice lines. Inventory owns committed part movements, stock balances and valuation. RepairOperations references Sales and Inventory records; it must not create parallel invoices, stock ledgers or valuations.

**R1 intake prohibition:** repair intake/acceptance is disabled in R1. The user-facing message is exactly: «پذیرش تعمیر در نسخهٔ بعدی فعال می‌شود.» No hidden, partial or alternate RepairCase intake path may bypass this prohibition.

R1 repair-related financial facts are admissible only if they originate from a real, authorized, independently addressable source already supported in R1 (for example, an existing external source with immutable references). If no such source exists, FE-02 financial posting is unavailable in R1. Do not synthesize RepairCase, lifecycle events, completion/handover states or manual status substitutions to manufacture a posting source.

Full RepairCase intake/workflow, WIP and repair profitability reporting remain R2. R1 WIP is disabled.

## 2. R2 operational lifecycle concepts

When RepairOperations intake is enabled in an approved later release, the candidate model is:
- RepairCase: operational case/aggregate;
- RepairServiceLine: separately identifiable service obligation and unit of measurement/recognition;
- RepairExecutionStep: traceable execution/work step.

Candidate event concepts, only where the actual enabled workflow supports them:
1. `RepairTechnicalCompletionRecorded`: authorized technician records technical completion for a service line.
2. `RepairQualityCheckPassed`: separate event only where QC is required by the applicable workflow; independent actor where policy requires.
3. `RepairReadyForHandover`: operational readiness state, not proof of customer handover.
4. `RepairOrderHandedOver`: evidence of handover to the customer or authorized recipient.
5. correction, reopen, rework, cancellation or warranty facts: new linked facts under an explicitly approved lifecycle; never erase prior events.

Technical completion, required QC, readiness, handover, invoice finalization and payment are distinct facts.

## 3. FE-02 service revenue evidence policy

For a standard physical repair, the approved policy requires both:
- technical completion of the relevant RepairServiceLine; and
- confirmed handover of the device/order to the customer or an authorized recipient.

Technical completion alone, invoice finalization, invoice issue or payment receipt is insufficient.

Alternative acceptance without physical handover is allowed only for a separately identified service-catalog item whose approved policy explicitly permits it and whose independent acceptance evidence is retained. This is not a general substitute for physical handover and requires service-catalog/Accounting owner approval.

Partial recognition is permitted only for independently identifiable, separately measurable service obligations/lines with their own completion and applicable acceptance evidence. A percentage estimate of an unfinished indivisible repair is not sufficient.

Accounting owns RecognitionTime. OccurredAtUtc and RecordedAtUtc remain source timestamps and must not be substituted for RecognitionTime without an explicit approved Accounting decision.

## 4. Source references and data ownership

Candidate source facts and references:
- RepairCaseId and immutable aggregate revision (R2 only);
- RepairServiceLineId and line revision;
- RepairExecutionStepId and execution evidence;
- canonical PartyId reference; no duplicated customer identity/PII;
- device/catalog reference with controlled access;
- service-catalog item, quantity and UOM where applicable;
- technical completion actor and server timestamp;
- QC result/evidence and independent actor only when QC is required;
- ReadyForHandover state separately from handover evidence;
- RecipientReference and authorization evidence for customer/representative handover;
- Sales/Invoice-owned document and line references (if an invoice exists);
- Inventory-owned committed part-movement references; no local authoritative quantity, stock status or valuation;
- subcontractor/service provider references and commission entitlement references only where their own approved source contracts exist.

ActorReference identifies the authenticated operator/system actor, derived server-side. RecipientReference identifies the customer/authorized receiver and is distinct; retain only minimum authorization evidence and avoid unnecessary PII.

## 5. Parts, outsourced services and technician commission

- Part consumption is recorded only by Inventory's committed movement contract. RepairOperations stores the movement reference and cannot decrement stock itself.
- Part valuation and COGS are Inventory/Accounting concerns, separate from repair service revenue.
- Outsourced service expense/liability and principal-versus-agent treatment require independent source evidence and an approved rule. Account 2114 is not inferred from this proposal; eligibility must be established from canonical COA and evidence.
- Technician commission entitlement is distinct from service revenue. R1 may record only supported entitlement/accrual if a real source and accepted rule exist. Full payroll calculations remain R2. This proposal does not authorize commission payout; payout requires a separately approved payment/settlement flow.

## 6. Common event envelope and idempotency

Each financial-capable event uses, as applicable:
- stable EventId, EventType, ContractVersion;
- AggregateId/AggregateVersion;
- immutable SourceDocumentReference and source revision/line;
- OccurredAtUtc and RecordedAtUtc;
- server-derived ActorReference where applicable;
- CorrelationId/CausationId;
- PartyId where a party reference is relevant;
- scoped request IdempotencyKey, distinct from EventId.

RecipientReference and recipient authorization evidence remain separate from ActorReference. Currency, SourceAmount and TaxSnapshotReference are required only for event types carrying monetary facts; per-event schemas define required/optional/nullability/precision. Non-monetary operational events do not invent amounts or currency.

Same identity/key with a different canonical payload fails closed. Retries reuse original identities; corrections/reopens are new linked facts and cannot mutate history.

## 7. Transactional outbox and delivery

The approved shared policy requires the source fact/document, audit record and Outbox record to persist atomically in the owning database transaction. Publish only after commit. Delivery is at-least-once; consumers are idempotent. Retries preserve the same EventId. No exactly-once transport guarantee is claimed.

This is a required design constraint, not evidence of existing RepairOperations or platform implementation.

## 8. Owner acceptance register

| ID | Required acceptance | Owner | Status |
|---|---|---|---|
| REPAIR-EVT-001 | R2 aggregate, service-line and execution-step identity/cardinality | RepairOperations | OPEN |
| REPAIR-EVT-002 | Technical completion definition and authorized transitions | RepairOperations | OPEN |
| REPAIR-EVT-003 | QC applicability, independence and evidence model | RepairOperations | OPEN |
| REPAIR-EVT-004 | Readiness, handover, recipient and representative authorization evidence | RepairOperations + Security | OPEN |
| REPAIR-EVT-005 | Rework, reopen, warranty, cancellation and correction lifecycle | RepairOperations | OPEN |
| REPAIR-EVT-006 | Sales/Invoice document and line reference contract | RepairOperations + Sales | OPEN |
| REPAIR-EVT-007 | Service amount, discount, currency, rounding and tax snapshot ownership | Sales + Pricing + Tax | OPEN |
| REPAIR-EVT-008 | Inventory committed part-movement reference contract | RepairOperations + Inventory | OPEN |
| REPAIR-EVT-009 | Event schema, versioning, idempotency and transactional outbox design | RepairOperations + Platform Architecture | OPEN |
| REPAIR-EVT-010 | FE-02 source mapping and RecognitionTime decision | RepairOperations + Accounting | OPEN |
| REPAIR-EVT-011 | Exact R1 external source eligibility, if any; otherwise confirm FE-02 posting unavailable in R1 | Accounting + relevant source owner | OPEN |

Business policy approval OWN-FE-01-02-001 is recorded; it does not substitute for module-owner technical acceptance.

## 9. Acceptance criteria before publication

1. R1 intake prohibition is enforced and tested; no synthetic repair workflow exists.
2. Any proposed R1 financial source is identified, real, authorized, immutable and independently addressable. Without it, no R1 FE-02 posting.
3. RepairOperations owner accepts R2 aggregate and lifecycle definitions.
4. Technical completion, required QC, readiness and handover are separate states/facts.
5. Standard physical repair requires completion plus confirmed handover; alternative acceptance has separate approved catalog policy and evidence.
6. Sales and Inventory references resolve to their authoritative contracts.
7. Partial completion, rework, reopen, cancellation, warranty, replay and duplicate-delivery cases are tested.
8. Accounting separately approves source crosswalk and RecognitionTime.
9. PostgreSQL atomicity/idempotency tests and independent review pass on exact commit.

## 10. Status

- Owner policy: APPROVED — OWN-FE-01-02-001 v1.0.
- RepairOperations technical contract: PROPOSAL — OWNER REVIEW REQUIRED.
- R1 intake: DISABLED.
- R1 FE-02 posting: BLOCKED unless a real approved source is identified; otherwise unavailable in R1.
- R2 full workflow/WIP/profitability: NOT IMPLEMENTED.
- Runtime repair event/outbox/adapter: NOT IMPLEMENTED / NOT VERIFIED.
- FE-02: BLOCKED.
- Accepted positive Posting Rules: 0.
- Posting Release: NOT GRANTED.

**End of proposal.**
