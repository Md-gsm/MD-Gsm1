# MDG Platform Database Role and Grant Matrix
## Version 0.1 — Design-Only Privilege Specification

| Field | Value |
|---|---|
| Document ID | MDG-PLATFORM-DB-ROLE-GRANT-MATRIX-001 |
| Version / Date | 0.1 / 2026-11-20 |
| Status | OWNER POLICY APPROVED — PRIVILEGE DESIGN SPECIFICATION — NOT EXECUTED |
| Baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Tests / execution | NOT RUN; no roles or grants created |

## 1. Scope and limits

This matrix converts the owner's six-role decision into PostgreSQL privilege intent. It is not SQL, migration, grant script, credentials, authorization implementation or evidence of actual privilege separation. No CREATE ROLE, GRANT, REVOKE, ownership transfer or privilege change is authorized. Migration 0009 remains RESERVED.

## 2. Canonical roles

| Role | Login model | Sole purpose | Must not do |
|---|---|---|---|
| Schema Owner | NOLOGIN object/schema owner | Own schemas/objects after controlled provisioning | App login, business commands, Runtime/Worker membership |
| Migration Executor | Separate controlled short-lived credential | Authorized versioned migrations after checksum, dependency, backup and recovery checks | Superuser by default, app traffic, Runtime/Worker membership, Migration 0009 |
| Runtime Application | Least-privilege service login | Approved domain command boundary and approved views | DDL, role changes, direct committed-ledger/audit edits, foreign private-table access |
| Background Worker | Separate service login/identity | Claim/lease/deliver Outbox/Inbox and narrow worker procedures | Owner/Migration credentials, direct business ledger/Journal writes, bypass Idempotency/Audit |
| Read-only Observer | Named time-bounded read-only identity | Approved views, metrics and reconciliation | DML/DDL, side-effect functions, sensitive base-table access by default |
| Backup/Recovery | Isolated credential, absent from app hosts | Approved backup/restore/verification runbook | Runtime use, unrelated business writes, unrelated role/grant changes |

Membership is explicit. Runtime/Worker cannot SET ROLE to Schema Owner or Migration Executor. Migration Executor is activated only by controlled migration procedure with independent authorization and recovery readiness. Backup/Recovery is isolated. Observer defaults to privacy-filtered views. Database superuser is outside these roles and never used by app processes. Secrets are in protected Secret Store, rotated and absent from logs/source.

## 3. Object-class privilege matrix

Legend: OWN=ownership through controlled provisioning; EXEC=narrow reviewed function only; SELECT-V=approved views only; SELECT-S=explicitly approved same-module support tables only; BACKUP=runbook backup/restore; —=none; DENY=explicitly prohibited.

| Object class | Schema Owner | Migration Executor | Runtime Application | Background Worker | Read-only Observer | Backup/Recovery |
|---|---|---|---|---|---|---|
| Database/schema | OWN | Controlled migration access only; no ownership | DENY CREATE/ALTER/DROP | DENY CREATE/ALTER/DROP | — | BACKUP/RESTORE runbook |
| Mutable workflow/draft tables | OWN | DDL only during approved migration | Narrow same-module command boundary; no cross-module DML | Narrow task procedures | SELECT-V or — | BACKUP |
| Committed Movement/Ledger/Journal/AuditEvent | OWN | DDL only; no ordinary row edit | DENY direct UPDATE/DELETE/TRUNCATE; commit only via approved boundary | DENY business mutation | SELECT-V only | BACKUP; no content editing |
| Local Outbox/Inbox | OWN | DDL only | Insert intent through atomic command; no delivery-state edit | Claim/lease/ACK/retry via narrow functions; no payload rewrite | SELECT-V metrics | BACKUP |
| Other module private tables | OWN only for owning schema | DDL only | DENY direct access | DENY unless Worker belongs to owner module and official contract permits | SELECT-V only | BACKUP |
| Views/read models | OWN | DDL only | SELECT approved app views | SELECT task/ops views | SELECT-V | BACKUP |
| Sequences/transactional counters | OWN | DDL only | No direct nextval for commit watermark; use transactional counter boundary | No direct allocation | — | BACKUP |
| Functions/procedures | OWN | Create/replace only in authorized migration | EXEC approved command/query boundaries | EXEC approved claim/delivery/recovery boundaries | No side-effect execution | Recovery tools only |
| Extensions | OWN after provisioning | Install/upgrade only by explicit approval | DENY create/alter | DENY create/alter | — | Backup metadata |
| Roles/memberships/default ACLs | Controlled governance ownership only | No CREATEROLE/superuser by default | DENY | DENY | DENY | No change except approved recovery runbook |

Final object ACLs must be derived from actual schemas/tables/views/functions/sequences/extensions. This matrix does not claim those objects already exist.

## 4. Non-negotiable restrictions

Runtime cannot run DDL/migrations; change roles; update/delete/truncate committed Movement, Journal, canonical AuditEvent or immutable history; read/write another module's private data; disable protection triggers; bypass reviewed search_path/function resolution; allocate watermark from non-transactional sequences; or invoke unreviewed SECURITY DEFINER routines.

Worker may claim queue items, record lease/attempt/retry metadata, send immutable envelopes, persist verified ACK references and raise attention/quarantine through narrow functions. Worker cannot alter EventId/hash/payload; mark delivered without matching durable ACK; mutate stock/Journal merely because it is a worker; write another module's AuditEvent; or hold Outbox locks while acquiring domain locks.

Observer reads approved views only. Sensitive base data is denied by default; exceptional access is explicit, scoped, time-limited and audited.

Migration Executor validates target SHA/checksum, schema/dependency versions, backup/restore readiness, lock/availability impact, rollback/forward-fix plan, authorization identity and post-run evidence. Migration 0009 remains reserved.

## 5. SECURITY DEFINER constraints

Any later approved restricted function must have a NOLOGIN narrow owner; fixed safe search_path; schema-qualified references; no unsafe dynamic SQL; PUBLIC EXECUTE revoked; only intended Runtime/Worker EXECUTE; no access to unrelated module data; positive/negative privilege tests; and exact definition hash/version evidence. No function is created here.

## 6. Required PostgreSQL proof — NOT RUN

Independent credentials must prove:
1. Runtime DDL/ALTER/DROP/CREATE ROLE denied.
2. Runtime direct UPDATE/DELETE/TRUNCATE of committed ledger, Journal and canonical AuditEvent denied.
3. Runtime cross-module private-table access denied; approved command/query boundary works.
4. Worker cannot mutate Movement/Journal/AuditEvent and only uses approved queue functions.
5. Worker cannot mark delivered without matching durable ACK.
6. Observer can read only approved views and cannot DML/execute side-effect functions.
7. Migration Executor has no default superuser, cannot serve Runtime and executes only authorized migration.
8. Schema Owner is NOLOGIN and not an app credential.
9. Backup/Recovery is runbook-only and audited.
10. No unintended role inheritance/default ACL.
11. Credential rotation/revocation/restore works.

No database roles, credentials or grants have been created. No GRANT/REVOKE SQL is present or executed.

**END — MDG-PLATFORM-DB-ROLE-GRANT-MATRIX-001 v0.1**
