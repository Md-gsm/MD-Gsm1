# RepairOperations Source Event Contract Proposal — FE-02 dependency

| Field | Value |
|---|---|
| Document ID | REPAIROPERATIONS-SOURCE-EVENT-CONTRACT-PROPOSAL-001 |
| Version | 0.1 |
| Status | PROPOSAL — OWNER REVIEW REQUIRED |
| Scope | Source facts required to evaluate FE-02 (repair service) |
| Architecture | MDG-ARCH-MASTER-001 v2.5 |
| Data ownership | REP-01 — Repair workflow; SELL-01 — Sales/Invoice; STK-01 — Inventory |

## 1. Purpose and non-authority

This proposal separates operational repair facts that must not be conflated. It does not decide whether technical completion, quality acceptance, customer handover, invoice finalization, or another approved fact is the Accounting RecognitionTime for FE-02.

RepairOperations owns the repair case and its operational lifecycle (REP-01). Sales/Invoice owns the invoice and its lines (SELL-01). Inventory owns committed part movements and stock balances (STK-01). RepairOperations must reference those records through versioned contracts; it must not create parallel invoices or inventory ledgers.

## 2. Proposed distinct operational facts

The following event concepts are proposed for owner review. Their existence and exact semantics must be confirmed against the actual workflow before publication:

1. `RepairTechnicalCompletionRecorded` — the responsible workflow records that the technical work on a repair order/service line has reached its defined completion state.
2. `RepairQualityCheckPassed` — a distinct fact only if the approved repair workflow actually includes a quality-control step. It must not be synthesized where no such step exists.
3. `RepairOrderHandedOver` — evidence that the repaired device/order was handed over to the customer or an authorized recipient, if this lifecycle is supported and recorded.
4. A correction/reopen/rework fact — only after the owner defines how a previously completed or handed-over repair is lawfully corrected or reopened. Historical events must not be erased.

These are intentionally separate. Technical completion is not automatically equivalent to quality acceptance or customer handover.

## 3. Proposed event envelope

The owner should approve a common envelope before runtime publication:

| Field | Meaning | Constraint |
|---|---|---|
| EventId | Stable source-event identity | Unique and immutable |
| EventType | Exact event name | Versioned vocabulary |
| ContractVersion | Published schema version | Explicit |
| AggregateId | RepairOrderId | Stable opaque identifier |
| AggregateVersion | Version of repair aggregate | Monotonic within aggregate |
| OccurredAtUtc | Time the operational fact occurred | UTC instant; not automatically Accounting RecognitionTime |
| RecordedAtUtc | Time persisted by RepairOperations | UTC instant |
| ActorReference | Authenticated server-side actor reference | Never trusted from a client-supplied actor field |
| CorrelationId / CausationId | Workflow trace references | Opaque identifiers |
| SourceDocumentReference | Repair order and immutable revision reference | Must resolve to source evidence |

Transport, outbox, delivery guarantees and retry protocol remain subject to separate platform approval.

## 4. Proposed repair-service snapshot and references

The owner should confirm the supported fields and their precise semantics:

- RepairOrderId and immutable aggregate revision.
- RepairServiceLineId for each separately identifiable service obligation/work item.
- PartyId reference for the customer; Persons remains the identity owner.
- Device/catalog reference and relevant repair-case reference; do not copy a second device or Party identity.
- Service catalog reference, quantity/UOM where the approved model supports them.
- Technical completion timestamp and responsible actor reference, if recorded by the workflow.
- Quality-check result and evidence reference only if quality control is an actual workflow step.
- Handover timestamp and authorized recipient reference/evidence only if handover is recorded.
- Link to the Sales/Invoice-owned invoice/document and line, when one exists.
- References to Inventory-owned committed part movements; no local inventory quantity or valuation is authoritative here.

No customer name, phone, national identifier, payment instrument or other unnecessary personal data should be copied into the event payload.

## 5. FE-02 evidence composition

FE-02 must not be populated by assuming that one operational status is equivalent to revenue recognition. The eventual crosswalk must resolve:

- which service line(s) are billable and how a multi-line repair is represented;
- whether technical completion, quality acceptance, handover, invoice finalization or another owner-approved fact controls recognition;
- treatment of partial completion, multiple services, rework, reopened repairs, cancellations and disputed handover;
- source invoice/document and line linkage without creating a duplicate invoice;
- service amount, discount and tax snapshots from their owning source;
- separation of service revenue from parts inventory/COGS movements;
- any subcontracted service and technician commission as separate events/rules, not embedded into FE-02 by assumption.

`OccurredAtUtc` is an operational timestamp only. Accounting must not infer RecognitionTime from it without an explicit approved decision.

## 6. Identity, assignment and idempotency

- Customer identity is referenced by canonical PartyId.
- Technician/work-role identity follows Persons ownership; RepairOperations owns skills, capabilities, scheduling and assignment. A technician identifier must not be treated as an application authorization role.
- Event identity must remain stable across retries and replay.
- A correction/reopen must be a traceable new fact linked to the prior event/revision; silent mutation is prohibited.
- A duplicate event must not cause duplicate downstream effects.

## 7. Open owner decisions

| ID | Decision required | Owner | Status |
|---|---|---|---|
| REPAIR-EVT-001 | Confirm repair aggregate and service-line identities/revisions | RepairOperations | OPEN |
| REPAIR-EVT-002 | Confirm technical completion definition and permitted transitions | RepairOperations | OPEN |
| REPAIR-EVT-003 | Confirm whether quality control exists and its evidence model | RepairOperations | OPEN |
| REPAIR-EVT-004 | Confirm handover/recipient evidence and exceptional cases | RepairOperations | OPEN |
| REPAIR-EVT-005 | Confirm rework, reopen, cancellation and correction lifecycle | RepairOperations | OPEN |
| REPAIR-EVT-006 | Confirm invoice and line references against Sales/Invoice contract | RepairOperations + Sales | OPEN |
| REPAIR-EVT-007 | Confirm service amount/discount/tax source snapshots | Sales + Pricing + Tax | OPEN |
| REPAIR-EVT-008 | Confirm part-consumption references to Inventory-owned movements | RepairOperations + Inventory | OPEN |
| REPAIR-EVT-009 | Approve envelope, event versioning, outbox and replay semantics | RepairOperations + Platform Architecture | OPEN |
| REPAIR-EVT-010 | Approve the FE-02 RecognitionTime source/decision | RepairOperations + Accounting owner | OPEN |

## 8. Acceptance criteria before publication

1. RepairOperations owner signs off aggregate, service-line identity and lifecycle semantics.
2. Technical completion, quality check and handover remain distinct unless the owner documents why a state is unsupported.
3. Sales/Invoice and Inventory references resolve to their owning source contracts.
4. Partial completion, rework, reopen, cancellation, replay and duplicate-delivery cases are specified.
5. Contract tests validate immutable source snapshots and reference integrity.
6. Accounting separately approves the FE-02 source crosswalk and RecognitionTime. This proposal itself grants no posting permission.

## 9. Current implementation status

The repository currently contains only the RepairOperations Contracts boundary marker; no runtime repair event contract or publisher is published. This proposal does not add one. No positive FE-02 Posting Rule is accepted. FE-02 remains `BLOCKED`; Posting Release remains NOT GRANTED.

**End of proposal.**
