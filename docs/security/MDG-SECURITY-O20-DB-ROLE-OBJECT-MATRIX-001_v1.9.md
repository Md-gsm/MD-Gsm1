# O-20 Database Role / Object Matrix — Accepted Bootstrap Capability Addendum

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 |
| Version / Date | 1.9 / 2026-11-24 |
| Supersedes | v1.8, only for the additive audit-delivery-worker identity binding |
| Status | **BOOTSTRAP PRINCIPAL PAIR OWNER-ACCEPTED; AUDIT DELIVERY WORKER TECHNICALLY SELECTED — OWNER RATIFICATION REQUIRED; NO PRODUCTION ROLE / GRANT PROVISIONED; O-20 OPEN** |
| Owner decision | MDG-SECURITY-O46-BOOTSTRAP-DATABASE-PRINCIPAL-DECISION-001 v0.1 |
| Related design | MDG-SECURITY-O46-BOOTSTRAP-DATABASE-PRINCIPAL-DESIGN-001 v0.1 |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |

## 1. Scope and precedence

This successor records the Product Owner's acceptance of two narrowly scoped, pre-activation PostgreSQL principals for the one-time initial Product Owner claim. It reconciles their classification with the O-20 five-identity model.

The original five N.6 operational categories remain unchanged. WG-0 technical selection on 2026-11-24 proposes a sixth logical operational identity, `audit-delivery-worker`, with PostgreSQL LOGIN `audit_delivery_worker` and NOLOGIN ACL role `mdgsm_audit_delivery_worker`. The accepted bootstrap executor remains a temporary technical capability outside the logical operational set; it is still a real PostgreSQL LOGIN and must be visible in the cluster/database role inventory. The function-owner is a NOLOGIN object/function owner, not an application identity.

This document does not replace the O-20 privilege audit, production deployment procedures, technical module-owner approval, independent review, WG-0 migration reservation or release gates. The v1.7 outbox-worker material remains a test-only candidate and is unaffected by this bootstrap decision.

## 2. Canonical N.6 identities — unchanged

| N.6 logical identity | Database role / state | Accepted purpose |
|---|---|---|
| runtime_app | `mdgsm_runtime` — NOLOGIN ACL role | Ordinary application runtime grants only; no Account INSERT, role/permission writes, DDL or ownership |
| migration_runner | `migration_runner` — LOGIN, NOINHERIT | Explicit migration execution; current exact SET-only membership to `mdgsm_schema_owner` remains unchanged |
| backup_operator | `backup_operator` — not provisioned except approved ephemeral O20-D fixture | Isolated backup/restore under its separate approval |
| reporting_ro | `reporting_ro` — not provisioned | Deny until RowScope/view contract and proof |
| emergency_operator | `emergency_operator` — not provisioned | Deny until O20-F lifecycle and exact capabilities are independently proven |
| audit_delivery_worker | `audit_delivery_worker` LOGIN → `mdgsm_audit_delivery_worker` NOLOGIN | Outbound source-Outbox delivery and receipt/quarantine/retry metadata only; WG-0 selection pending owner ratification; not provisioned |

`mdgsm_schema_owner` remains a separate NOLOGIN object-owner role, not a sixth N.6 application identity. Its current purpose and ownership graph are unchanged by this successor.

## 3. Owner-accepted pre-activation bootstrap principal pair

### 3.1 LOGIN capability — `mdgsm_bootstrap_executor`

| Property | Required state / boundary |
|---|---|
| Classification | One-purpose, pre-activation technical database capability; outside the five N.6 operational identities |
| LOGIN | Yes, only while initial claim setup is pending |
| Privilege attributes | NOINHERIT, NOSUPERUSER, NOCREATEDB, NOCREATEROLE, NOREPLICATION, NOBYPASSRLS |
| Connection limit | 1 concurrent connection |
| Database access | CONNECT only to the designated central application database |
| Role membership | Zero membership edges; no SET ROLE path |
| Object ownership | None |
| Schema access | USAGE only on isolated `identity_bootstrap` schema |
| Routine access | EXECUTE only on the exact versioned initial-owner claim routine |
| Direct table/sequence access | None |
| DDL / role administration / grants | None |
| Secret custody | Credential accessible only to the dedicated Windows Setup Service SID through OS-protected storage |
| Post-claim state | Service erases its local credential, terminates connection and disables setup; trusted deployment/database-owner sets LOGIN to NOLOGIN or drops the role |

The role must not be a member of `mdgsm_runtime`, `migration_runner`, `mdgsm_schema_owner`, `mdgsm_identity_bootstrap_fn_owner` or any other role. It cannot alter or disable itself. The database ACL must prevent CONNECT to every database other than the designated central database, including any effective PUBLIC CONNECT path.

A generated strong secret (SCRAM-SHA-256) or separately reviewed client-certificate authentication is required. The challenge's 30-minute lifetime is not a LOGIN credential TTL; credential lifetime, interrupted-setup resume and cleanup-failure handling remain open design/test items.

### 3.2 NOLOGIN function-owner — `mdgsm_identity_bootstrap_fn_owner`

| Property | Required state / boundary |
|---|---|
| Classification | Isolated SECURITY DEFINER function/schema owner; not an application or operational identity |
| LOGIN | NOLOGIN |
| Privilege attributes | NOSUPERUSER, NOCREATEDB, NOCREATEROLE, NOREPLICATION, NOBYPASSRLS |
| Ownership | Only isolated `identity_bootstrap` schema and exact versioned claim routine(s) |
| Identity/business table ownership | None |
| Direct table privileges | Only exact minimum privileges needed by the approved claim routine |
| Role/permission/session/recovery mutation | None |
| Cross-module data access | None |

The final table/column grants must be derived from the reviewed function source and proven through effective-privilege catalog tests. The proposed minimum operation requires the ability to create the initial Account, immutable Product Owner binding and claim receipt, validate/consume the challenge, and write the required IdentityAccess audit intent. No broad grant is pre-approved.

### 3.3 SECURITY DEFINER ACL requirements

- Function owner is exactly `mdgsm_identity_bootstrap_fn_owner`; neither database owner nor `mdgsm_schema_owner` nor any LOGIN may own the routine.
- Safe pinned search_path contains only trusted schemas and places `pg_temp` last; non-catalog objects are fully qualified.
- No dynamic SQL, generic dispatcher, caller-selected object names, arbitrary SQL, or unbounded JSON input.
- Revoke PUBLIC EXECUTE and verify the effective ACL; set controlled default privileges so future setup routines are not accidentally public.
- Bootstrap executor receives only USAGE on the isolated schema and EXECUTE on the single allowed routine.
- Verify RLS policy behavior, function owner effective privileges and object ownership on PostgreSQL 18.
- The routine's function-definition hash, owner, ACL, default ACL and dependent table/column privileges are captured in the O-20 inventory.

## 4. Deployment and migration boundary — deliberately unresolved

This owner acceptance does **not** approve a membership edge from `migration_runner` to `mdgsm_identity_bootstrap_fn_owner`, or any other change to the existing migration-owner membership graph. It also does not approve a new deployer LOGIN, nor does it authorize routine installation by the Setup Wizard.

A separately authenticated trusted deployment/database-owner boundary must install or replace the routine under the declared function owner, with reviewed DDL provenance, exact owner assignment, immutable function hash, controlled upgrade/rollback and no runtime access. The exact principal and operational sequence are a separate design gate. The existing migration_runner → mdgsm_schema_owner SET-only edge remains the only accepted migration membership edge.

Migration reservation is also separate:
- 0009 remains RESERVED for Accounting and must not be used.
- 0010 remains RESERVED for IdentityAccess; 0011 is RESERVED in PR #92 for the AuditSecurity canonical ledger only; 0012+ remains UNALLOCATED.
- Source-local Outbox tables are not part of Migration 0011 and require separate source-module schema ownership/migration gates.
- No SQL, DDL or schema object is authorized by this matrix.

## 5. Required exact-head PostgreSQL proof

All proof rows below are **NOT RUN / NOT VERIFIED** for the accepted bootstrap pair:

| ID | Required proof |
|---|---|
| O46-DBP-01 | Five N.6 identities and their accepted purposes/membership edges unchanged |
| O46-DBP-02 | Executor attributes exact: LOGIN, NOINHERIT, non-elevated, connection limit 1 |
| O46-DBP-03 | Executor has zero membership edges and no SET ROLE path |
| O46-DBP-04 | Executor has no direct table/sequence privileges |
| O46-DBP-05 | Executor has only the designated database CONNECT, schema USAGE and exact routine EXECUTE |
| O46-DBP-06 | Executor DDL, GRANT, role-management and ownership changes denied |
| O46-DBP-07 | Function-owner attributes, exact schema/function ownership and minimal effective privileges |
| O46-DBP-08 | SECURITY DEFINER owner, search_path, fully qualified references, RLS behavior and PUBLIC/default ACL controls |
| O46-DBP-09 | Account, binding, challenge consumption, idempotency result and audit intent atomicity |
| O46-DBP-10 | Concurrent claim serializes to exactly one Account/binding |
| O46-DBP-11 | Challenge purpose, SID, InstallationId, SetupOperationId, expiry and replay protections |
| O46-DBP-12 | Same-operation unknown-commit reconciliation; conflicting operation denied |
| O46-DBP-13 | No second owner, replacement, transfer or re-enrollment |
| O46-DBP-14 | Runtime Account INSERT denial unchanged |
| O46-DBP-15 | Migration runner cannot invoke or alter bootstrap routine through an unapproved membership path |
| O46-DBP-16 | Secret readable only by Setup Service SID; absent from API, client, process arguments/environment and logs |
| O46-DBP-17 | Credential erasure, connection termination and independent post-claim NOLOGIN/drop cleanup |
| O46-DBP-18 | Reinstall, Repair, update, restore and interrupted-setup continuity |
| O46-DBP-19 | Supported PostgreSQL 18 Linux/Windows behavior parity and Windows central-server acceptance |
| O46-DBP-20 | Independent review of exact SHA, SQL, roles, effective privileges, tests and cleanup evidence |

Existing O20-G disposable evidence does not cover either new role and cannot be reused as their acceptance evidence. All positive and negative checks must run against a fresh disposable PostgreSQL 18 catalog fixture and be tied to the exact final commit.

## 6. Current disposition

- Original N.6 five operational identities: unchanged.
- Additive sixth logical identity `audit-delivery-worker` and its proposed LOGIN/ACL names: **WG-0 TECHNICAL SELECTION; OWNER RATIFICATION REQUIRED; NOT PROVISIONED**.
- Bootstrap executor + function-owner classification: **OWNER-ACCEPTED CONSTRAINT**.
- Role creation, secrets, grants, memberships, schema, function and migration: **NOT CREATED / NOT AUTHORIZED**.
- DDL owner/deployment pathway and WG-0 migration reservation: **OPEN**.
- O46-G04: **OPEN**.
- O20: **OPEN**; no previous O20-G evidence is extended by this acceptance.
- IdentityAccess G1 / production activation: **NOT GRANTED**.

**END — MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 v1.9**
