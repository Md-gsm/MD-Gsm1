# Organization Assignment Audit Event and Outbox Contract — Proposal

| Field | Value |
|---|---|
| Document ID | MDG-ORGANIZATION-AUDIT-EVENT-OUTBOX-CONTRACT-001 |
| Version / Date | 0.2 / 2026-11-22 |
| Status | PROPOSAL WITH BOUNDED DISPOSABLE POSTGRESQL EVIDENCE; AUDITSECURITY / PLATFORM ACCEPTANCE AND PRODUCTION ADAPTER OPEN |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Related policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.5 |

## 1. Ownership

Organization owns Branch/Warehouse/assignment aggregates and domain lifecycle history. AuditSecurity exclusively owns canonical shared AuditEvent/SecurityEvent, deduplication, durable storage, retention, access/export and durable ACK. Organization must not create a competing canonical audit ledger.

Inventory cross-module contract v1.2 describes local transactional AuditIntent/Outbox, stable EventId, payload hash, at-least-once delivery and durable ACK. It is a design precedent only; it is not an accepted AuditSecurity contract for Organization.

## 2. Atomic local intent

One Organization PostgreSQL transaction should atomically persist:
- idempotency receipt and canonical request fingerprint;
- aggregate state/version transition;
- append-only domain lifecycle history;
- local audit-intent record and outbox event;
- durable idempotent command result.

Failure before commit rolls all local effects back. External AuditSecurity delivery occurs only after local commit. Whether high-risk commands may commit while canonical delivery is pending requires explicit Security/AuditSecurity/Product Owner acceptance; this proposal does not presume it.

## 3. Candidate envelope

| Field | Requirement |
|---|---|
| EventId | Stable opaque identifier created with local AuditIntent; ID version deferred to Platform |
| EventType | Fixed registered event name, never client-supplied |
| ContractName/Version, PayloadSchemaVersion | Explicit, versioned |
| PayloadHashAlgorithm/PayloadHash | SHA-256 over accepted canonical serialization |
| ActorReference/ActorType | Account reference from validated server principal; not client supplied |
| ExecutionMode | STANDARD_MAKER_CHECKER or PRODUCT_OWNER_DIRECT, the latter only after trusted owner-binding decision |
| SubjectAccountReference | Opaque AccountId; may equal actor only for the accepted Product Owner exception |
| ExecutorServiceIdentity | Authenticated service/host identity, distinct from human actor |
| SourceModule/Operation | Fixed Organization command identifier |
| AggregateId/AggregateVersion | Organization aggregate and resulting version |
| ResourceReferences | BranchId, WarehouseId, AssignmentId, ApprovalRequestId as applicable |
| Decision / ReasonCode | Fixed outcome and controlled reason code; detailed reason remains in protected domain history |
| CorrelationId/CausationId | Trace references, not authentication evidence |
| OccurredAtUtc/RecordedAtUtc | Server RecordedAtUtc is authoritative |
| EvidenceReference | Opaque reference/digest to retained Organization domain history |

Secrets, password/credential hashes, bearer/session tokens, recovery material and unnecessary personal data are prohibited.

## 4. Event taxonomy proposal

Candidate events:
- `organization.branch.created/activated/deactivated/archived`
- `organization.warehouse.created/activated/deactivated/archived`
- `organization.warehouse.branch-reassigned`
- `organization.assignment.branch.created/changed/revoked`
- `organization.assignment.warehouse.created/changed/revoked`
- `organization.assignment.approval.approved/rejected`

Exact event names and treatment of denied attempts require AuditSecurity/Security owner acceptance.

## 5. Candidate delivery semantics

Delivery is at-least-once; exactly-once transport is not claimed.

Candidate local states: PENDING → IN_FLIGHT (expiring lease) → ACKNOWLEDGED, or QUARANTINED for integrity/version/security/non-retryable failures. Same EventId + same PayloadHash must return the same durable ACK. Same EventId + different PayloadHash is an integrity violation: preserve the original and quarantine the conflicting payload. Timeout/unknown response is retried with identical EventId and bytes/hash. Local state is marked ACKNOWLEDGED only after a verifiable durable ACK is persisted.

Retry schedule, max attempts/age, alert thresholds, worker authentication, quarantine authority, operator replay and durable ACK format must be accepted by AuditSecurity/Platform. No unbounded retry or silent discard.

The worker receives only least-privilege access to outbox delivery metadata; it cannot mutate Organization domain state or access IdentityAccess private data. It commits its claim before network delivery and does not hold an outbox lock while acquiring domain locks.

## 6. Reconciliation, retention and tests

Monitor oldest pending age, pending/in-flight counts, lease expiry, ACK latency, retry counts, quarantine count/age, EventId/hash conflicts, and committed local intents lacking canonical durable ACK. A 2xx response alone is not proof of durable acknowledgement.

Required tests include crash before local commit, crash after local commit before send, crash after remote durable store before local ack update, same-ID/same-hash replay, same-ID/different-hash quarantine, stale lease reclaim, unsupported schema, worker least privilege, legal-hold conflict, and alert/recovery/replay evidence.

OD-05 requires Organization history retained by default, no automatic deletion and lawful disposal only after approved retention expiry, no Legal Hold and documented authority. AuditSecurity must separately define canonical event retention/hold; the local domain record does not become the canonical security ledger.

## 7. Required acceptance

AuditSecurity must accept envelope/canonicalization/hash, EventId deduplication, durable ACK, schema evolution, worker identity/privileges, retry/quarantine/replay and retention/hold. Platform/Application must accept local UoW/outbox/service identity. Organization must accept domain event semantics; Security must accept the threat model.

**Disposition:** candidate contract prepared; AuditSecurity/Platform acceptance, runtime adapter and PostgreSQL evidence OPEN. No table, worker, grant, canonical ledger or production integration is created.

**END — MDG-ORGANIZATION-AUDIT-EVENT-OUTBOX-CONTRACT-001 v0.1**


## 8. Bounded PostgreSQL proof and exact-head validation — 2026-11-22 (v0.2)

The current PR branch adds:
- `O20OrganizationAuditOutboxPostgresProofTests`: local audit intent/outbox commit atomicity, same EventId/same payload-hash replay, changed-payload rejection/quarantine and competing worker claim behavior;
- `O20OutboxWorkerLeastPrivilegePostgresProofTests`: synthetic dedicated worker LOGIN with SET-only membership to a narrow NOLOGIN ACL role, SELECT of immutable event/delivery facts and UPDATE limited to delivery metadata columns; event payload/hash/ID, delete, branch/warehouse data and IdentityAccess credential data remain inaccessible.

Both tests passed in the marked disposable PostgreSQL job on exact source/test SHA `99f28b63a179a4a54b377bca2a9174a819883c28`, primary workflow `45051449948`. The worker proof also confirms its synthetic LOGIN cannot assume owner, runtime, migration, backup, reporting or emergency roles.

### 8.1 Explicit proof boundary

The fixture's `canonical_audit_event` endpoint is synthetic and hosted in the disposable test database. It does not prove a networked production AuditSecurity service, external canonical storage, production durable-ACK cryptographic/semantic verification, external WORM Object Lock Compliance, deployed worker lifecycle, retry scheduler, alerting, on-call runbook, restore/reconciliation or real credential/key custody.

The separate N.6 five-category identity baseline remains unchanged. A sixth worker identity is only a recommendation in the owner/platform decision packet; no production LOGIN, password, secret, grant, connection string or worker is provisioned. Production delivery remains disabled until the sixth identity and all service boundaries are explicitly accepted.

### 8.2 Acceptance required

AuditSecurity must publish/accept the canonical event and durable ACK contract, deduplication conflict behavior, schema evolution, worker authentication, retry/quarantine/replay, retention and Legal Hold. Platform/DB/Security must approve any additional identity and object ACL. Organization domain event semantics and local transaction boundary require their accountable owner acceptance.

**Disposition:** local outbox and least-privilege object/column PoCs PASS at the exact bounded test SHA; production AuditSecurity/API/ACK/worker/WORM and owner/platform decisions OPEN. No canonical ledger, service identity, secret, grant or runtime is created.

**END — Organization Audit Event/Outbox contract v0.2**
