# Persons PartyRegistered Event Contract v1.0

Status: implementation contract for AC-37; source Outbox schema is in migration 0013.

## Purpose and emission rule

PartyRegistered is the first and only Persons source event enabled by the approved AC-37 scope.

- Emit exactly once when a new Party is created by successful temporary-workflow promotion.
- Do not emit when promotion attaches an explicitly selected existing Party or resolves to an existing Party.
- Replays return the stored promotion result and do not emit another event.
- PartyMerged is out of scope and must not be emitted by this contract.

## Versioned envelope

| Field | Type | Meaning |
|---|---|---|
| eventId | UUID | Stable event identity; every delivery retry reuses this exact value |
| eventType | string | Constant PartyRegistered |
| contractVersion | string | 1.0 |
| aggregateId | UUID | Persons-owned PartyId |
| actorReference | opaque string | Server-authenticated ActorId reference |
| occurredAtUtc | timestamp | Server UTC event time |
| correlationId | UUID or null | Request correlation |
| causationId | UUID or null | Source WorkflowId |
| idempotencyKey | UUID | Source Promotion OperationId |

The source envelope contains no arbitrary JSON payload. In particular, it contains no name, national identifier, mobile number, email address, address, internal note, raw workflow payload, or encryption material. The PartyId and opaque actor reference are identifiers, not descriptive personal fields.

## Transaction and delivery semantics

Party creation, workflow promotion state, ciphertext deletion, the immutable promotion tombstone, the PartyRegistered event, and its initial pending delivery row commit in one PostgreSQL transaction. A deferred database constraint rejects an event-only commit without the initial delivery record.

Delivery is at-least-once. Retries update the delivery state and retain the original eventId; consumers must deduplicate by eventId. Event envelopes are immutable. Delivery state is mutable and supports pending, leased, delivered, and quarantined states.

This contract does not authorize creation of a worker principal, database grants, a runtime worker, API activation, or production delivery.
