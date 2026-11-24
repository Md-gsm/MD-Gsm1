# MD-Gsm Shared AuditSecurity Source Outbox Contract

| Field | Value |
|---|---|
| Document ID | MDG-AUDITSECURITY-SOURCE-OUTBOX-CONTRACT-001 |
| Version / Date | 1.0 / 2026-11-24 |
| Status | IMPLEMENTATION CONTRACT; SOURCE MODULE ADOPTION AND PRODUCTION API/WORKER ACCEPTANCE OPEN |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.4 |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Code locations | \`src/Modules/AuditSecurity/Contracts\`, \`src/Modules/AuditSecurity/Application\`, \`src/BuildingBlocks/MDGsm.Infrastructure/AuditSecurity\` |

## 1. Ownership and transaction boundaries

Each source module exclusively owns its business facts and a local transactional Outbox in the same PostgreSQL database/schema boundary as those facts. The source module must atomically commit its domain fact, immutable audit intent and initial delivery metadata in one local database transaction.

AuditSecurity exclusively owns the canonical cross-module ledger, stream order, hash chain, checkpoint and durable ingestion receipt. Source modules must not create a parallel canonical ledger. AuditSecurity must not read or mutate source-module business tables.

There is no distributed transaction and no exactly-once transport claim. Delivery is at-least-once. Unknown response or connection loss after the remote commit is recovered by retrying the exact same EventId and canonical intent; the canonical service must return the original durable receipt for a replay with the same digest.

## 2. Published contract and canonical intent

The public module contract is \`IAuditEventIngestionContract\` with ContractVersion \`1.0\`, using \`AuditEventSubmission\`. Required identity and trace fields include EventId, AuditStreamId, source EventId/module, typed Actor, optional Subject, CorrelationId, optional CausationId, event type, occurrence time, outcome, schema/canonicalization versions and hash algorithm.

The version-1 canonical intent serialization is deterministic, strict UTF-8, NFC-normalized, field-ordered, length-prefixed, ordinally sorted for metadata and normalized to UTC microseconds. SHA-256 is calculated over those exact canonical bytes. The Contracts-level \`AuditEventSubmissionCanonicalizer\` must remain byte-identical to the AuditSecurity Domain canonicalizer. The Persian Unicode golden vector is:

\`E186D866975D3549541AFF549D00433BC18299E2D229A934DCD1667A65B06056\`

The event type and actor/subject references must be populated from fixed, server-validated module command data, never trusted from client-supplied event metadata. Contract/schema/canonicalization version mismatches fail closed. Secrets, passwords/password hashes, authentication/session tokens, OTPs, recovery material, connection strings, private keys and unnecessary raw personal data are prohibited.

Until an event-type-specific allow-list is reviewed and registered, metadata must be empty. The current source Outbox writer rejects non-empty metadata. A future metadata allow-list must be versioned and approved before any value can be persisted or delivered.

## 3. Source-local Outbox persistence shape

Each adopting source module owns two distinct tables in its own schema:

### 3.1 Immutable \`audit_outbox_events\`

Contains EventId (primary key), AuditStreamId, schema and canonicalization versions, hash algorithm, event type, OccurredAtUtc, typed Actor/Subject references, source module/EventId, CorrelationId/CausationId, outcome, metadata JSON, contract version, canonical IntentDigest and server-created timestamp.

After insertion, event intent, EventId, digest and immutable envelope cannot be updated or deleted by the worker. The source module's own authorized migration/owner owns DDL and retention.

### 3.2 Mutable \`audit_outbox_delivery\`

One row per EventId, with state, attempt count, next-attempt time, lease expiry/token, claimed-by worker, acknowledgement reference, failure code, quarantine reason/time and updated timestamp.

Allowed states: \`PENDING\`, \`IN_FLIGHT\`, \`ACKNOWLEDGED\`, \`QUARANTINED\`. Illegal state transitions must fail closed. Event payload and delivery state are physically separated to make column-scoped worker permissions possible.

These tables are source-owned. They are not part of AuditSecurity Migration 0011. Each source module must obtain its own migration reservation and schema-owner acceptance before creating them. Organization remains blocked by its separate G1/implementation gates.

## 4. Writer contract — same local transaction

\`IAuditEventOutboxWriter.EnqueueAsync(DbConnection, DbTransaction, AuditEventSubmission, CancellationToken)\` must:
1. require an open connection and an active transaction belonging to that connection;
2. canonicalize and validate the submission before writing;
3. insert the immutable intent and its digest plus the initial delivery row in the caller's transaction;
4. never open, commit or dispose the caller's transaction;
5. on same EventId + same canonical digest, act idempotently only if a matching delivery row also exists;
6. on same EventId + different digest or missing delivery-state row, raise an integrity/conflict error; never overwrite the prior event.

Transaction rollback removes the business fact, audit intent and delivery row together. The source module must not send an event before its local transaction commits.

## 5. Worker identity and database capability

The selected logical identity is \`audit-delivery-worker\`. Proposed PostgreSQL principal names are LOGIN \`audit_delivery_worker\` and NOLOGIN ACL role \`mdgsm_audit_delivery_worker\`. The LOGIN is non-interactive, NOINHERIT and non-elevated; it has exactly one SET-only membership edge (\`ADMIN FALSE, INHERIT FALSE, SET TRUE\`). No production role, credential, grant or service is provisioned by this contract.

After \`SET ROLE\`, the worker may have only:
- SELECT on registered \`audit_outbox_events\` immutable rows;
- SELECT on \`audit_outbox_delivery\`;
- UPDATE on delivery metadata columns only: state, attempts, next-attempt, lease fields, claimed-by, ACK reference, failure/quarantine fields and timestamps.

The worker must not insert or mutate immutable event rows, change EventId/digest/payload, delete, truncate, perform DDL or role management, access business facts, IdentityAccess credentials/sessions, canonical AuditSecurity ledger tables, migration/backup/reporting/emergency objects or use another identity. Its pool must verify \`session_user\` and \`current_user\`, and reset role state between leases.

The worker is an outbound API client; it has no direct canonical-ledger database access. The API's service/database identity and authentication/certificate deployment are separate Platform decisions. The host configures the authenticated HTTP handler (for example platform-managed mTLS); credentials must not be embedded in code, installer, ordinary app configuration, logs or source database.

## 6. Delivery protocol

The worker claims a bounded batch using row locks with \`FOR UPDATE SKIP LOCKED\`, assigns a random lease token and expiry, increments attempts, commits the claim transaction, and only then performs network delivery. It must never hold an Outbox row lock while waiting for the AuditSecurity API or taking source-domain locks.

On successful response, the worker validates EventId, AuditStreamId, ContractVersion, positive canonical sequence, matching IntentDigest and 32-byte EventHash. A 2xx response alone is not sufficient. ACKNOWLEDGED is committed only with an exact active lease token and a matching event digest. The stored ACK reference is an opaque receipt reference/digest record; transport authenticity is provided by the configured authenticated channel.

Transient transport/service outcomes use bounded exponential retry; permanent schema/auth/conflict/rejection outcomes are quarantined. Retry count, maximum event age, batch size, lease duration, alert thresholds and manual replay policy are deployment configuration subject to Platform/Security approval; no unbounded retry or silent discard is permitted. Worker crash or uncertain response leaves a finite lease which may be reclaimed after expiry. Lease token mismatch prevents stale workers from acknowledging or reclassifying an event.

## 7. Operational metrics and required tests

Monitor oldest pending age, PENDING/IN_FLIGHT/ACKNOWLEDGED/QUARANTINED counts, lease expiry/reclaim, retry count and age, ACK latency, conflict count and source intents without durable canonical ACK. Alert and runbook ownership must be assigned before production enablement.

Required evidence includes:
- source fact + intent + delivery initial state atomically commit or roll back;
- canonicalizer golden-vector parity across Contracts and Domain;
- duplicate EventId/same digest idempotency and changed digest rejection;
- concurrent independent worker claims with no double active lease;
- crash before/after source commit and after remote commit before local ACK;
- lease expiry/reclaim, stale token denial and no stale acknowledgement;
- authenticated API acceptance and receipt mismatch/quarantine;
- transient retry/backoff bounds and permanent rejection quarantine;
- worker least-privilege positive/negative catalog proofs;
- PostgreSQL Linux/Windows parity, backup/restore and reconciliation;
- alert, operational recovery and evidence preservation.

## 8. Current implementation status and non-authorization

PR #92 currently contains the provider-neutral canonicalizer/contracts, appender/dispatcher, Npgsql canonical ledger store adapter, source-local Npgsql Outbox adapter, HTTP ingestion client and disposable integration/unit proof sources. These are source implementation artifacts under CI review, not a deployed service.

Migration 0011 is reserved for the central canonical AuditSecurity ledger only. No SQL file has been created. No source module's Outbox schema has been created through a production migration. No LOGIN, credential, grant, API listener, scheduler, worker service, KMS/HSM key or Object Lock anchor has been provisioned. O20/O21 production closure, Organization G1, IdentityAccess G1, release and Go-Live remain separate gates.

**END — MDG-AUDITSECURITY-SOURCE-OUTBOX-CONTRACT-001 v1.0**
