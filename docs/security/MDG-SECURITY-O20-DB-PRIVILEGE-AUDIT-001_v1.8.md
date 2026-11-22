# O20 Database Privilege Audit — Worker Identity Reconciliation

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 |
| Version / Date | 1.8 / 2026-11-22 |
| Supersedes | v1.7 |
| Status | FIVE-IDENTITY LOGICAL BASELINE PRESERVED; OUTBOX WORKER IDENTITY DECISION OPEN |
| Related records | O20 role/object matrix v1.6; Stage 08 register v4.9 |

## 1. Accepted logical identity baseline remains unchanged

The owner-accepted N.6 baseline remains the five logical service identity categories:
1. runtime application ACL identity (`mdgsm_runtime`, NOLOGIN);
2. migration runner;
3. backup/recovery operator;
4. read-only observer;
5. emergency operator, ephemeral and unprovisioned until O20-F activation.

The separate `mdgsm_schema_owner` is a NOLOGIN owner, not an application/service identity.

The new test-only LOGIN shim PoC (LOGIN `runtime_app_<suffix>` -> SET-only membership -> NOLOGIN `mdgsm_runtime_<suffix>`) is a candidate *physical connection binding*. It does not add a sixth logical identity and it does not approve a production LOGIN credential or membership.

## 2. Background outbox worker is a separate execution concern

O20 outbox and O21 delivery require a worker which:
- claims and renews outbox delivery leases;
- reads only committed immutable outbox envelope data;
- updates delivery status/attempt metadata;
- sends the exact same EventId and payload hash on retry;
- persists durable ACK only after verifying the AuditSecurity contract;
- quarantines hash/schema/security conflicts;
- cannot mutate Organization aggregates or read IdentityAccess private data.

The older Platform DB role/grant proposal v0.1 names a background-worker class, but it was not reconciled into the later N.6 five-identity matrix. Neither record currently constitutes an explicit, current owner decision to add or omit a dedicated O20/O21 worker principal.

A worker must not be silently run with broad `mdgsm_runtime` access. A worker process sharing the application's login is not itself a privilege boundary.

## 3. Two deployment alternatives — decision required

**Alternative A — dedicated worker identity (recommended least privilege):**
- add one logical service identity `outbox_worker` (or approved name) as an explicit extension to the five-identity baseline;
- separate LOGIN credential/service principal from the application runtime;
- grant only the outbox schema/object operations necessary to claim rows and update delivery metadata;
- no ownership, DDL, general business-table read, IdentityAccess private data, backup, reporting or emergency role membership;
- isolate network egress and provider credentials for AuditSecurity/WORM;
- rotate/revoke independently; prove worker cannot mutate Organization aggregate tables.

**Alternative B — no additional logical identity:**
- keep exactly the existing five logical identity categories;
- run worker operations under the runtime application identity but require a distinct narrowly scoped database execution role/context and independently restricted process/network credentials;
- prove the worker cannot exercise normal runtime business privileges, and the runtime cannot exercise worker/outbox privileges;
- if PostgreSQL role separation cannot be demonstrated with the current five identities, this alternative fails closed and the outbox worker remains disabled.

Alternative B may prove technically impossible if the same LOGIN/session can assume both role contexts without enforceable actor/process separation. Do not claim a dedicated worker boundary by naming convention alone.

## 4. Runtime login shim status

The disposable test `O20RuntimeLoginMappingPostgresProofTests` proves one candidate SET-only LOGIN-to-NOLOGIN shape. It does not decide whether Platform/DB owner accepts this physical binding. Production credential source, rotation, connection-pool role initialization/reset, expected `session_user/current_user`, failover and catalog verification remain OPEN.

## 5. Required acceptance evidence

- explicit Product Owner + Platform/DB + Security decision on A or B;
- approved logical identity count and role membership/object matrix successor;
- test with independent runtime and worker connections proving non-overlap;
- worker can claim/ack only outbox data and cannot read/write business aggregates;
- runtime can perform only approved application grants and cannot mutate outbox delivery metadata;
- negative tests for attempts to SET ROLE to owner/migration/backup/reporting/emergency/other service roles;
- credential rotation/revocation and pool-reset tests;
- actual target DB catalog inventory and effective grants;
- exact-SHA independent review.

No role, LOGIN credential, secret, grant, worker or provider integration is created by this document.

**Disposition:** five logical identity baseline preserved; outbox worker identity count/boundary awaits explicit owner decision; runtime LOGIN shim remains disposable PoC; O20-C/G and O-20 remain OPEN.

**END — MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 v1.8**
