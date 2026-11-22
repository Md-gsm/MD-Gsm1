# O20 Database Privilege Audit — O21 Worker Boundary Proof

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 |
| Version / Date | 1.9 / 2026-11-22 |
| Supersedes | v1.8 |
| Status | DEDICATED WORKER LEAST-PRIVILEGE PoC ADDED; OWNER DECISION / PRODUCTION IDENTITY OPEN |
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

**END — MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 v1.9**
