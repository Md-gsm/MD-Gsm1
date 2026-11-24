# O20 Database Privilege Audit — O21 Worker Boundary Proof

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 |
| Version / Date | 2.0 / 2026-11-24 |
| Supersedes | v1.9, for the worker identity decision and adapter status |
| Status | AUDIT DELIVERY WORKER IDENTITY SELECTED; POSTGRESQL ADAPTERS ADDED; PRODUCTION ROLE / GRANT / SERVICE STILL NOT PROVISIONED |
| Related evidence | O20OutboxWorkerLeastPrivilegePostgresProofTests |

## 1. Accepted baseline

The current five logical O20 identity categories and separate NOLOGIN schema owner remain unchanged. The candidate runtime LOGIN shim remains test-only. Neither this audit nor the new test grants production access.

## 2. Dedicated outbox worker PoC

Added `tests/MDGsm.Domain.Tests/Security/O20OutboxWorkerLeastPrivilegePostgresProofTests.cs`. It creates a disposable PostgreSQL schema with separate immutable event and mutable delivery tables and synthetic identities:
- a LOGIN `outbox_worker` principal;
- a NOLOGIN `mdgsm_outbox_worker` ACL role;
- one SET-only membership, with ADMIN and INHERIT disabled;
- separate synthetic runtime, migration, backup, reporting, emergency and schema-owner roles.

The worker ACL is granted only:
- USAGE on the isolated test schema;
- SELECT on the immutable event envelope table;
- SELECT on the delivery state table;
- column-scoped UPDATE on delivery metadata (state, attempts, lease, claim identity, ACK reference, quarantine reason and update timestamp).

The test proves:
- before SET ROLE, the LOGIN has no schema/object rights;
- after SET ROLE, session_user remains the LOGIN and current_user is the worker ACL role;
- the worker can read the immutable event and update delivery metadata;
- it cannot alter event payload/hash, change delivery event_id, delete either outbox table, read Branch/Warehouse or IdentityAccess credential tables, mutate Branch data, perform DDL, create roles, or SET ROLE to owner/runtime/migration/backup/reporting/emergency identities.

This is a test-only candidate matrix and does not use a live AuditSecurity endpoint, actual canonical durable ACK, HSM/KMS/WORM, production service credential or deployed database. The test's LOGIN and membership are created only in a marked disposable PostgreSQL database and dropped in finally.

## 3. Logical identity decision remains open

This worker design adds a separate logical service identity beyond the current N.6 five-category baseline. It is intentionally not treated as accepted. The Product Owner, Platform/DB owner and Security must approve one of:
- a dedicated background/outbox worker identity with the narrow rights proven in this test (recommended); or
- a formally demonstrated alternative in which runtime and worker capabilities remain non-overlapping without an additional logical identity.

Do not broaden `mdgsm_runtime` or silently treat the application's interactive/runtime credential as the outbox worker.

## 4. Deployment prerequisites

Before provisioning:
- approve logical identity count/name and owner;
- choose independent credential source and rotation/revocation process;
- define connection-pool initialization/reset and prove session_user/current_user;
- inventory actual grants/default privileges/ownership on the target database;
- bind worker to an authenticated AuditSecurity API with durable ACK, retry/quarantine/reconciliation;
- prove the worker cannot read credentials, sessions or business aggregates;
- independent exact-SHA security review.

**Disposition:** dedicated worker least-privilege PoC source added; exact-head CI required; production role, credential, grant, provider and worker remain UNPROVISIONED. O20-C/G and O21 production closure remain OPEN.

**END — MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 v2.0**


## 5. Product Owner identity resolution and adapter implementation update — 2026-11-24

The previously open identity-name decision is resolved by selecting the previously approved conceptual worker identity \`audit-delivery-worker\`, with PostgreSQL LOGIN \`audit_delivery_worker\` and NOLOGIN ACL role \`mdgsm_audit_delivery_worker\`. This is a sixth logical operational identity in addition to the original five N.6 categories; it does not replace or broaden \`mdgsm_runtime\`. The required sole membership edge is SET-only (\`ADMIN FALSE\`, \`INHERIT FALSE\`, \`SET TRUE\`).

### 5.1 Exact capability boundary

The ACL is intended to grant only source-local Outbox access: SELECT on immutable \`audit_outbox_events\`, SELECT on \`audit_outbox_delivery\`, and column-scoped UPDATE on delivery metadata (state, attempts, next-attempt time, lease fields, worker claim, acknowledgement reference, failure/quarantine reason and timestamps). No immutable payload/EventId/digest update, INSERT, DELETE, TRUNCATE, DDL, business aggregate access, IdentityAccess credential/session access, canonical AuditSecurity ledger access, backup/reporting/migration access or role escalation is permitted.

The worker posts to the authenticated AuditSecurity API and records the matching durable receipt. It is not a direct canonical-ledger PostgreSQL writer. The API's database identity remains a separate unresolved Platform design item.

### 5.2 Adapter code now present on PR #92 branch

- \`PostgresAuditEventStore\`: Npgsql append/read adapter. It initializes a missing stream head with the canonical genesis digest, locks the stream-head row FOR UPDATE, obtains PostgreSQL \`clock_timestamp()\` in the transaction, checks global EventId idempotency, inserts the immutable event record, advances the head with a sequence compare-and-swap and commits both writes atomically. Read snapshots use Repeatable Read.
- \`PostgresAuditEventOutboxStore\`: source-schema-scoped adapter. Enqueue uses the caller's open Npgsql connection and transaction; it never begins or commits a second transaction. Immutable event envelope and mutable delivery metadata are stored separately. Worker claims use \`FOR UPDATE SKIP LOCKED\`, short committed leases and lease tokens; network delivery occurs after the claim transaction commits. ACK/retry/quarantine updates are conditional on the active lease.
- \`AuditEventSubmissionCanonicalizer\`: Contracts-level v1 canonicalization/digest, tested against the existing Persian Unicode golden vector.
- \`AuditEventOutboxDispatcher\`: at-least-once delivery, digest verification, bounded exponential retry, permanent rejection quarantine and durable-receipt matching.
- \`HttpAuditEventIngestionClient\`: HTTP transport adapter; host must supply the authenticated handler/credential (for example platform-managed mTLS). No key or credential is embedded.
- PostgreSQL disposable integration test and dispatcher/contract unit tests have been added. Their exact-head status is pending CI until a successful run reports them.

### 5.3 Deployment boundary

The adapters do not create schema, SQL, roles, membership, grants, credential, API route or a Worker host. Migration 0011 is reserved for the central canonical AuditSecurity ledger only; its SQL is not authorized or present. Each source module owns its own local Outbox tables and must reserve/implement its own migration after its module-specific G1 and owner gates. No production credential or PostgreSQL principal is provisioned.

The pre-existing disposable worker ACL proof remains test-only evidence. It does not prove that the real deployed worker's grants, pool identity, certificate, API endpoint or operational lifecycle have passed acceptance.

**Disposition:** identity naming and adapter source implementation are resolved/added; CI evidence is pending for the new adapter tests; production role/credential/grant, source-module Outbox adoption, authenticated deployed API, AuditSecurity service DB identity, Platform key custody, WORM anchor and independent exact-SHA review remain OPEN. O20/O21 are not closed.
