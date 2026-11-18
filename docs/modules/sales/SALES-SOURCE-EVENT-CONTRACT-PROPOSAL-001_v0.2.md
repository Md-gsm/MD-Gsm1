# Sales Source Event Contract Proposal — FE-01 dependency

| Field | Value |
|---|---|
| Document ID | SALES-SOURCE-EVENT-CONTRACT-PROPOSAL-001 |
| Version | 0.2 |
| Status | PROPOSAL — OWNER REVIEW REQUIRED |
| Scope | Source facts required to evaluate FE-01 (sale of goods) |
| Architecture | MDG-ARCH-MASTER-001 v2.5 |
| Owner decision | OWN-FE-01-02-001 v1.0 — APPROVED POLICY |
| Data ownership | SELL-01 — Sales/Invoice; STK-01 — Inventory movement and fulfillment |

## 1. Authority and boundary

This proposal translates approved owner policy into a candidate source contract. It is not an accepted module contract, runtime schema, event publisher, Posting Rule or Posting Release.

The controlling business policy is OWN-FE-01-02-001 v1.0. Sales owns commercial documents, immutable line snapshots, commercial handover acknowledgment and references. Inventory exclusively owns committed stock movements, fulfillment records, stock status and valuation. Sales MUST NOT create, mutate, reverse or independently assert an Inventory movement or fulfillment record.

## 2. Distinct source facts

The eventual integration must preserve these as separate facts:

1. `SalesDocumentFinalized`: immutable commercial document and finalized line snapshots.
2. Sales commercial handover acknowledgment/reference: evidence that the commercial delivery/recipient step was acknowledged, where the Sales workflow owns that acknowledgment. It is not an Inventory movement.
3. Inventory-owned committed fulfillment/movement fact: published only by Inventory under its own contract and linked to originating Sales line(s).
4. Correction, cancellation, return entitlement and commercial reversal: new linked source facts/documents; never silent mutation.
5. Payment receipt, payment allocation, PSP settlement and refund: distinct payment/settlement facts; none is a substitute for delivery or revenue recognition.

Exact event names and schemas remain subject to source-owner approval. The Inventory event name and payload are not prescribed here.

## 3. Ownership and linkage

- Sales owns SalesDocumentId, SalesDocumentVersion, SalesLineId, commercial pricing/discount snapshots, commercial terms and commercial recipient acknowledgment.
- Inventory owns FulfillmentId/MovementId, committed movement state, stock disposition, valuation and movement correction/reversal.
- Each Inventory fulfillment line must reference the originating SalesDocumentId/version and SalesLineId, or an explicitly approved equivalent stable source-line reference.
- A Sales line may be fulfilled partially or in multiple stages. The relationship must support one SalesLine to many fulfillment lines and, where a committed Inventory fulfillment aggregates multiple source lines, many source lines to one fulfillment aggregate without losing line-level quantities.
- Sales stores immutable references to Inventory evidence. A missing, uncommitted, reversed or unresolved Inventory reference cannot be represented as completed fulfillment.

## 4. Candidate Sales document/line snapshot

The Sales owner must specify type, nullability, precision and lineage for:
- SalesDocumentId and immutable SalesDocumentVersion;
- SalesLineId and stable line version;
- PartyId (canonical identity reference only);
- product/catalog and variant references;
- quantity and UOM;
- finalized unit price, gross amount, line discount allocation, net amount and currency;
- document-level discount allocation result and deterministic line allocation evidence;
- applicable TaxSnapshotReference and source result only when Sales owns the legally determined tax snapshot; otherwise an explicit not-applicable/unresolved state;
- commercial terms and their effective snapshot;
- source finalization time and actor reference;
- links to Inventory-owned fulfillment/movement references, with source versions.

Historical snapshots are never recalculated downstream. No legal rate, account, tax result or valuation is fabricated.

## 5. Revenue and delivery evidence boundary

FE-01 source completeness requires all of:
- immutable Sales document and line snapshots;
- Inventory-owned committed fulfillment evidence linked to source line and fulfilled quantity;
- evidence that the relevant goods were delivered and control transferred under the approved commercial policy;
- approved Pricing/Tax snapshots where applicable;
- Accounting's separately approved RecognitionTime decision.

SalesDocumentFinalized, invoice issue, payment receipt, payment allocation, PSP settlement, reservation, shipment request, pick-list, uncommitted movement or mere stock decrement alone MUST NOT be substituted for the required delivery/transfer-of-control evidence.

For cash-at-handover, payment and recognition may occur in the same business operation, but remain distinct source facts and accounting effects. The revenue recognition rule must not assume receipt or PSP settlement.

## 6. Returns, refunds and corrections

- Physical receipt of returned goods, commercial acceptance/return entitlement, revenue/tax reversal, refund, Inventory inspection and stock disposition are separate facts.
- Sales owns commercial return entitlement and commercial credit/correction documents.
- Inventory owns returned-item inspection, quarantine/disposition, restock and inventory/valuation effects.
- A returned item remains unavailable for sellable/ATP stock until Inventory explicitly sets an eligible disposition.
- Revenue/tax reversal follows the approved commercial return/contractual entitlement decision; inventory restock/COGS effects follow Inventory inspection and disposition. Neither is inferred from the other.
- Corrections to finalized records are linked new documents/events with source references and reason evidence.

## 7. Common event envelope requirements

The approved shared policy requires a common envelope with:
- EventId (stable immutable event identity);
- EventType and ContractVersion;
- AggregateId and AggregateVersion;
- SourceDocumentReference and immutable source version/lineage;
- OccurredAtUtc and RecordedAtUtc;
- server-derived ActorReference where an authenticated operator/system actor exists;
- CorrelationId and CausationId;
- PartyId where a party reference is relevant;
- scoped request IdempotencyKey distinct from EventId.

RecipientReference and recipient authorization evidence are separate from ActorReference. Monetary fields (Currency, SourceAmount, tax snapshot references) are required only for event types carrying monetary facts; each event schema must declare required/optional/nullability and precision. Non-monetary events do not invent currency or amount.

Same event/request identity with a different canonical payload fails closed. Corrections use new linked identities. Retries preserve original identity and do not duplicate effects.

## 8. Outbox and delivery semantics

The approved shared policy requires source document/fact, audit record and Outbox record to be committed atomically within the owning database transaction. Publication occurs only after commit. Delivery is at-least-once; consumers are idempotent and deduplicate using stable event identity and validated payload. Retry reuses the original EventId and does not create a new business fact. No exactly-once transport guarantee is claimed.

This section is a normative requirement for the eventual design, not evidence that Sales or platform runtime currently implements it.

## 9. Owner acceptance register

| ID | Required acceptance | Owner | Status |
|---|---|---|---|
| SALES-EVT-001 | Finalization semantics, aggregate identity and immutable line snapshot | Sales | OPEN |
| SALES-EVT-002 | Cancellation, correction, return-entitlement and credit-document lifecycle | Sales | OPEN |
| SALES-EVT-003 | Price, discount, currency, precision and deterministic rounding contract | Sales + Pricing | OPEN |
| SALES-EVT-004 | Tax snapshot ownership, reference and effective TaxMode interaction | Sales + Tax | OPEN |
| SALES-EVT-005 | Sales-to-Inventory source-line linkage and partial fulfillment behavior | Sales + Inventory | OPEN |
| SALES-EVT-006 | Envelope, versioning, idempotency and outbox implementation design | Sales + Platform Architecture | OPEN |
| SALES-EVT-007 | Source evidence and RecognitionTime mapping for FE-01 | Sales + Accounting | OPEN |
| SALES-EVT-008 | Recipient acknowledgment and authorized representative evidence | Sales + Persons/Security | OPEN |

Business policy approval OWN-FE-01-02-001 is recorded; it does not substitute for these technical/data-owner acceptances.

## 10. Acceptance criteria before publication

1. Sales owner accepts aggregate, finalization, commercial handover, immutable line snapshot and correction semantics.
2. Inventory owner independently publishes/accepts committed fulfillment/movement contract and source-line linkage.
3. Pricing and Tax owners approve snapshot ownership, precision, rounding and effective-rule references.
4. Per-event schema specifies types, nullability, precision, identity and version compatibility.
5. Partial fulfillment, returns, corrections, duplicate delivery, retry and replay are contract-tested.
6. Accounting approves FE-01 source crosswalk and RecognitionTime separately.
7. Runtime implementation, PostgreSQL atomicity/idempotency tests and independent review pass on exact commit.

## 11. Status

- Owner policy: APPROVED — OWN-FE-01-02-001 v1.0.
- Sales technical contract: PROPOSAL — OWNER REVIEW REQUIRED.
- Inventory contract: NOT STARTED.
- Runtime Sales event/outbox/adapter: NOT IMPLEMENTED / NOT VERIFIED.
- FE-01: BLOCKED.
- Accepted positive Posting Rules: 0.
- Posting Release: NOT GRANTED.

**End of proposal.**
