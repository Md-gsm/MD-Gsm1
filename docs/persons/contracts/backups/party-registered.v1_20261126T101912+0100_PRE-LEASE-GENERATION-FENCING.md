# Persons PartyRegistered Event Contract v1.0

Status: implementation contract for AC-37; source Outbox schema is in migration 0013.

## Purpose and emission rule

PartyRegistered is the first and only Persons source event enabled by the approved AC-37 scope.

- Emit exactly once in the same transaction whenever a new Party is created by a Persons-owned creation operation, including successful temporary-workflow promotion.
- Do not emit when duplicate resolution attaches an existing Party, when a selected existing Party is reused, or when an idempotent operation is replayed.
- A Party merge does not create a new Party and emits no PartyRegistered event. PartyMerged remains out of scope and must not be emitted by this contract.

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
| idempotencyKey | UUID | Source OperationId that created the Party; for temporary-workflow promotion this is its Promotion OperationId |

The source envelope contains no arbitrary JSON payload. In particular, it contains no name, national identifier, mobile number, email address, address, internal note, raw workflow payload, or encryption material. The PartyId and opaque actor reference are identifiers, not descriptive personal fields.

## Transaction and delivery semantics

Party creation, its source operation record, the immutable PartyRegistered event, and the initial pending delivery row are inserted by the Persons repository in one PostgreSQL transaction. For temporary-workflow promotion, the same caller-owned transaction also includes workflow promotion state, ciphertext deletion, and the immutable promotion tombstone. The promotion service passes correlation/causation metadata to the repository and does not insert a second event. PostgreSQL rollback removes the Party, source operation, event, delivery row, workflow changes, tombstone, and ciphertext deletion together if any step fails. Disposable PostgreSQL integration tests verify this rollback behavior.

Delivery is at-least-once. A database-backed delivery store atomically leases due or expired rows with `FOR UPDATE SKIP LOCKED`; each attempt increments the attempt count and retains the original eventId. Acknowledgement and retry scheduling are fenced by both lease owner and an unexpired lease. A stale worker cannot acknowledge or reschedule a lease claimed by another worker. A retry or process restart reuses the same EventId; consumers must deduplicate by EventId. Event envelopes are immutable. Delivery state is mutable and supports pending, leased, delivered, and quarantined states. Retry delay and lease duration are supplied by the explicitly invoked dispatcher; this contract adds no worker schedule, broker, retry ceiling, or automatic quarantine policy.

The infrastructure registers the database delivery store only. A host may opt into the one-batch dispatcher through `AddPersonsOutboxDispatcher` only after it has explicitly registered an approved `IPersonsOutboxPublisher`. No publisher, hosted/background worker, scheduler, broker, worker principal, or database grants are registered here. The dispatcher is invoked explicitly by a host operation; no automatic API activation or production delivery is authorized.
