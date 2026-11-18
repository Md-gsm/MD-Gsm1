# Sales Source Event Contract Proposal — FE-01 dependency

| Field | Value |
|---|---|
| Document ID | SALES-SOURCE-EVENT-CONTRACT-PROPOSAL-001 |
| Version | 0.1 |
| Status | PROPOSAL — OWNER REVIEW REQUIRED |
| Scope | Source facts required to evaluate FE-01 (sale of goods) |
| Architecture | MDG-ARCH-MASTER-001 v2.5 |
| Data ownership | SELL-01 — Sales/Invoice; STK-01 — Inventory movement |

## 1. Purpose and non-authority

This proposal defines the minimum source facts that Sales must make addressable to downstream consumers. It does not approve a revenue-recognition policy, a Posting Rule, a debit/credit pair, a tax treatment, or an Accounting integration.

The Sales module owns the sales document and its lines (SELL-01). Inventory owns committed stock movements and fulfillment evidence (STK-01). A Sales document by itself must not be treated as proof that goods were delivered or that inventory ownership/control transferred.

## 2. Proposed source facts

The following are proposed event concepts, not currently published runtime events:

1. `SalesDocumentFinalized` — a versioned, immutable sales document and its finalized line snapshots.
2. `SalesDocumentVoided` or a separately linked compensating document — only if the Sales owner confirms the supported cancellation lifecycle. A finalized document must not be silently overwritten.
3. Inventory fulfillment evidence — a reference to the Inventory-owned committed movement/fulfillment record. Sales must not create a parallel stock ledger or claim an Inventory event on Inventory's behalf.

The exact Inventory event name and contract are deliberately not prescribed here; Inventory is the owner and its contract must be reviewed separately.

## 3. Proposed event envelope

The owner should approve a common envelope before runtime publication:

| Field | Meaning | Constraint |
|---|---|---|
| EventId | Stable source-event identity | Unique and immutable |
| EventType | Exact event name | Versioned contract vocabulary |
| ContractVersion | Published schema version | Explicit; never inferred |
| AggregateId | Sales document identity | Stable opaque identifier |
| AggregateVersion | Version of the finalized source aggregate | Monotonic within the aggregate |
| OccurredAtUtc | Time the source fact occurred | UTC instant; not an Accounting RecognitionTime |
| RecordedAtUtc | Time the source system persisted the event | UTC instant |
| ActorReference | Authenticated server-side actor reference, where applicable | Never trusted from a client-supplied actor field |
| CorrelationId / CausationId | Workflow trace references | Opaque identifiers; no credentials |
| SourceDocumentReference | Stable document reference and version | Must resolve to the immutable source snapshot |

Transport, outbox implementation, delivery guarantees and retry protocol require separate platform approval; this proposal does not claim they exist.

## 4. Proposed finalized sales-line snapshot

The Sales owner should confirm whether the finalized document can publish these fields, with explicit nullability and source lineage:

- SalesDocumentId and SalesDocumentVersion.
- SalesLineId and stable line version.
- PartyId reference (Persons remains the identity owner); no copied customer identity.
- Product/Service catalog reference and variant reference where applicable.
- Quantity and UOM reference.
- Finalized unit price, line gross amount, line discount allocation and line net amount, each with explicit currency and precision.
- Tax snapshot reference/value only when the source Sales document actually owns a legally determined tax result; otherwise an explicit unresolved/no-tax state. No legal rate or tax result is fabricated by this contract.
- Document finalization status and source timestamp.
- Link to the Inventory-owned fulfillment/movement reference, once that source contract exists.

The contract must preserve the exact source snapshot. It must not recalculate historical prices, discounts, tax, or quantities downstream.

## 5. FE-01 evidence composition

FE-01 cannot be considered source-complete from `SalesDocumentFinalized` alone. The eventual crosswalk must identify, without inference:

- the immutable Sales document/line snapshot;
- the Inventory-owned committed fulfillment/stock-movement evidence and its ownership/control semantics;
- the relationship between each fulfilled quantity and the originating SalesLineId;
- partial, multiple, split, cancelled and reversed fulfillment behavior;
- source amount basis and any line-level discount/tax allocation;
- the approved RecognitionTime decision and its owner/source reference.

A document issue/finalization timestamp, payment receipt, shipment request, reservation, pick-list, or uncommitted stock operation must not be silently substituted for delivery/fulfillment evidence.

## 6. Identity, idempotency and corrections

- Party references use canonical PartyId; Sales does not create a parallel customer identity.
- EventId and source aggregate version must support deterministic duplicate detection.
- Replays must preserve source identity and must not create duplicate downstream effects.
- Corrections to finalized documents must be represented through an explicitly linked correction/void/replacement lifecycle approved by the Sales owner; silent mutation is prohibited.
- No payment instrument, PAN, authentication secret or unnecessary personal data belongs in the event payload.

## 7. Open owner decisions

| ID | Decision required | Owner | Status |
|---|---|---|---|
| SALES-EVT-001 | Confirm SalesDocumentFinalized semantics and immutable line snapshot | Sales | OPEN |
| SALES-EVT-002 | Confirm cancellation/correction lifecycle and event identity | Sales | OPEN |
| SALES-EVT-003 | Confirm line-level price/discount/amount precision and currency contract | Sales + Pricing | OPEN |
| SALES-EVT-004 | Confirm tax snapshot ownership and TaxMode interaction | Sales + Tax | OPEN |
| SALES-EVT-005 | Confirm Inventory fulfillment reference contract and partial/reversal behavior | Inventory + Sales | OPEN |
| SALES-EVT-006 | Approve envelope, event versioning, outbox and replay semantics | Sales + Platform Architecture | OPEN |
| SALES-EVT-007 | Approve whether any event/time is a source for FE-01 RecognitionTime | Sales + Accounting owner | OPEN |

## 8. Acceptance criteria before publication

1. Sales owner signs off event semantics, aggregate identity and immutable snapshot.
2. Inventory owner supplies its own committed-movement/fulfillment contract; Sales only references it.
3. All fields have type, nullability, precision, identity and versioning rules.
4. Partial fulfillment, cancellation, correction, replay and duplicate-delivery cases are specified.
5. Contract tests prove source payload stability and reference integrity.
6. Accounting separately approves the FE-01 source crosswalk and RecognitionTime. This proposal itself grants no posting permission.

## 9. Current implementation status

No Sales module runtime contract, event publisher, outbox, adapter or Accounting integration is created by this proposal. No positive FE-01 Posting Rule is accepted. FE-01 remains `BLOCKED`; Posting Release remains NOT GRANTED.

**End of proposal.**
