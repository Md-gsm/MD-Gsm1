# O20 Database Role / Object Privilege Matrix — Runtime Binding Clarification

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 |
| Version / Date | 1.5 / 2026-11-22 |
| Supersedes | v1.4 |
| Status | LOGICAL FIVE-IDENTITY MATRIX PRESERVED; PHYSICAL APP LOGIN BINDING OPEN |
| Related | MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 v1.7 |

## 1. Logical identity matrix (unchanged)

| Logical identity | Intended state | Boundary |
|---|---|---|
| Runtime application | `mdgsm_runtime` NOLOGIN ACL identity | Only approved runtime object privileges; no DDL/ownership/role administration |
| Migration runner | Dedicated LOGIN, narrowly approved SET-only relationship to schema owner where required | Migration window only; no routine runtime usage |
| Backup/recovery operator | Dedicated separate identity | Backup/restore capability only; no runtime application privilege |
| Read-only observer | Dedicated separate identity | Approved read-only views/objects only; no broad base-table access |
| Emergency operator | No standing human login; ephemeral per O20-F | Two distinct approvers, exact scope, max 30 minutes, one use, synchronous revoke |

The `mdgsm_schema_owner` remains a separate NOLOGIN object owner, not a sixth application identity.

## 2. Physical connection binding is a separate concern

A NOLOGIN ACL role cannot itself authenticate a network connection. A deployment must therefore authenticate a physical LOGIN principal and establish the intended ACL execution role. The current N.6 audit describes the logical runtime identity but does not prove how the deployed application connection is authenticated/mapped.

A disposable PostgreSQL experiment has been added for a candidate:
- LOGIN role named `runtime_app_<suffix>`;
- NOLOGIN ACL role named `mdgsm_runtime_<suffix>`;
- one SET-only membership edge, with INHERIT and ADMIN disabled;
- explicit `SET ROLE` is required to reach the runtime ACL;
- default LOGIN context has no schema/object grants;
- SET ROLE to owner/migration/backup/reporting/emergency roles is denied.

This is deliberately **not** promoted to the production matrix. It is one candidate binding shape for Platform/DB owner decision.

## 3. Mandatory connection pool controls

Any future production binding, regardless of selected mechanism, must prove:
- each new physical connection authenticates using an individually attributable LOGIN/service identity;
- the selected ACL context is initialized before application commands execute;
- `session_user` and `current_user` have the expected values;
- connection return resets the role and session settings before the connection is reused;
- initialization failure or role mismatch closes/rejects the connection;
- pooling, reconnect, failover, secret rotation and backup restore preserve the same boundary;
- app LOGIN cannot inherit, SET or administer any support identity beyond the explicitly accepted runtime ACL mapping.

## 4. Open decision and evidence

Platform/DB owner must select the physical binding mechanism (direct LOGIN with SET-only membership, managed connection proxy, or other verifiable approach) and accept exact deployment configuration. No additional persistent logical service identity is authorized by this successor. No production grants, password, secret, LOGIN role or membership were created.

**Disposition:** logical N.6 identity matrix unchanged; runtime LOGIN binding method OPEN; disposable PoC pending exact-head CI; effective production privilege evidence OPEN; O-20 OPEN.

**END — MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 v1.5**
