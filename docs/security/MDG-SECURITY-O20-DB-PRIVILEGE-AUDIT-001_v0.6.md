# O-20 Database Privilege Audit — Current Schema, Role Mapping and Evidence Gaps

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 |
| Version / Date | 0.6 / 2026-11-21 |
| Baseline | MDG-ARCH-MASTER-001 v3.0.3 N.6; Stage 08; DB Role/Grant Matrix v0.1 |
| Status | OBJECT INVENTORY + FIVE-IDENTITY N.6 MATRIX DOCUMENTED; runtime_app DISPOSABLE DB/API PRIVILEGE TESTS PASS ON SHA 7ef35cca…; FULL FIVE-IDENTITY MATRIX OPEN |
| Execution | No role, credential, membership, grant, revoke, DDL or production DB operation performed |

## 1. Executive result

O-20 is not closed. The repository contains a partial IdentityAccess runtime grant script and one database-level LOGIN proof, but does not yet contain a complete, approved, executed and tested privilege matrix for all current schemas and module objects.

PR #86 now adds a disposable-only candidate proof:
- A PostgreSQL LOGIN named `runtime_app` (without a password and usable only through local trust in the isolated CI service) is granted membership in the existing NOLOGIN support role `mdgsm_runtime`.
- The exact checked-in IdentityAccess grant script is applied to the disposable database.
- Direct SQL tests use `current_user=runtime_app`, verify LOGIN/INHERIT and reject SUPERUSER/CREATEDB/CREATEROLE/REPLICATION/BYPASSRLS; prove only the `mdgsm_runtime` membership and no membership in disposable `migration_runner`, `backup_operator`, `reporting_ro`, `emergency_operator` or the test-only schema-owner role; prove no schema/relation/function ownership and no CREATE on `identity`/`public`; and evaluate a 15-assertion effective table/column privilege matrix for IdentityAccess.
- The real `WebApplicationFactory` API host is configured with the same `runtime_app` connection string and exercises the HTTP IdentityAccess login/session lifecycle. On SHA `7ef35cca3b3c86757ce1893656f9b6f617cdb4d9`, workflow `44912452300`, job `136871018414` completed the PostgreSQL 18 integration stage successfully: **37 passed, 0 failed**. The successful test run includes the runtime LOGIN membership/ownership/15-column-privilege assertions and cross-module denied-command probes. The current `persons-spec-lint` checksum verifier also passed on that SHA. This is disposable test evidence only; no production LOGIN, secret or grant changed.
- The current grant script covers only the `identity` schema. It does not grant or test the current Persons tables or Finance Party account table.
- The canonical N.6 logical identities and the implementation's supporting role names have not been reconciled into an exact PostgreSQL LOGIN/NOLOGIN/membership model.
- No production credentials, grants or role changes are authorized by the existing design documents.

## 2. Authoritative role names and unresolved mapping

Architecture v3.0.3 N.6 names five canonical logical identities:

| Canonical N.6 identity | Intended purpose | Current repository evidence | O-20 status |
|---|---|---|---|
| `runtime_app` | Approved application commands and scoped reads/writes | `database/operations/identityaccess-runtime-role-grants.sql` creates NOLOGIN `mdgsm_runtime`; PR #86 now provisions a disposable canonical `runtime_app` LOGIN, grants it membership, applies the checked-in grant script, executes allowed/denied SQL tests as that LOGIN and configures the HTTP API E2E host with the same connection identity. This remains a CI-only candidate until exact-head tests pass; production credential provisioning remains separate. | OPEN — map logical identity to exact LOGIN and group roles |
| `migration_runner` | Versioned migrations only in approved maintenance window | `tools/MDGsm.DbMigrator/Program.cs` reads `ConnectionStrings__MDGsmDatabase`; no dedicated migration principal/membership proof is recorded in the reviewed source. | OPEN — dedicated credential and isolation proof |
| `backup_operator` | Backup/WAL read and controlled restore | No executed role/credential proof found in the reviewed source/evidence. | OPEN |
| `reporting_ro` | Approved read-only views with RowScope | No executed role/credential/view/RowScope proof found in the reviewed source/evidence. | OPEN |
| `emergency_operator` | Allow-listed technical incident/recovery only | No executed role/credential/allow-list proof found in the reviewed source/evidence. | OPEN |

The separate DB Role/Grant Matrix v0.1 describes six operational role classes: Schema Owner, Migration Executor, Runtime Application, Background Worker, Read-only Observer and Backup/Recovery. The mapping to N.6 is incomplete. In particular, N.6 does not authorize a separate Background Worker identity by name. No additional production identity or role membership should be created until the owner-approved mapping states whether the worker is a narrowly scoped sub-role of an existing canonical identity or requires a formal architecture change.

A NOLOGIN schema/object owner is a provisioning necessity, but its exact name, ownership scope and governance boundary must be registered; it must not be conflated with any application LOGIN identity.

## 3. Current object inventory visible in migrations

The following inventory is based on checked-in SQL migrations, not on a live production catalog:

| Schema | Objects identified in source | Owning boundary |
|---|---|---|
| `public` / default schema | `mdgsm_schema_version`, `mdgsm_migration_history` | Platform migration infrastructure |
| `identity` | accounts, roles, permissions, account_roles, role_permissions, sessions, login_attempts, recovery_codes, recovery_challenges, security_audit_events, system_settings | IdentityAccess |
| `public` / default schema | persons, persons_contact_points, persons_addresses, persons_operations, persons_access_audit, persons_temporary_workflows, persons_temporary_workflow_payloads, persons_party_merge_records | Persons |
| `public` / default schema | finance_party_accounts | Finance |

The current migration set does not yet prove a complete database catalog inventory for every future module. O-20's final matrix must be generated from the actual target schema catalog after all relevant migrations are applied, then reviewed against module ownership contracts.

## 4. Current grant script audit

File: `database/operations/identityaccess-runtime-role-grants.sql`

Observed:
- Creates `mdgsm_runtime` as NOLOGIN / NOSUPERUSER / NOCREATEDB / NOCREATEROLE / NOINHERIT if absent.
- Revokes PUBLIC access from the `identity` schema and its tables.
- Grants scoped SELECT/UPDATE/INSERT privileges to `mdgsm_runtime` on selected IdentityAccess tables.
- Does not create a LOGIN credential and does not grant membership to any actual application LOGIN; comments leave membership to DBA.
- Does not grant privileges on Persons or Finance objects.
- Does not define `runtime_app`, `migration_runner`, `backup_operator`, `reporting_ro` or `emergency_operator`.
- Does not prove ownership, default ACLs, role memberships, RowScope, SECURITY DEFINER function exposure, extension privileges, or all object-level ACLs.
- Its current account grants omit INSERT, while `PostgresIdentityAccessStores.SaveAsync` contains an insert path when an account row is absent. This is a deliberate or accidental boundary mismatch that must be resolved before any production bootstrap/account-provisioning path uses this role.
- Its `system_settings` update grant is limited to `updated_at_utc`, matching the current no-op row-lock statement; this does not itself authorize changing the active-account capacity or heartbeat/lease policy.
- It grants access to recovery tables although runtime recovery orchestration remains gated; those grants must be reconciled with the eventual recovery service boundary and least-privilege requirement.

The script must not be executed as-is against production and must not be treated as the complete N.6 privilege matrix.

## 5. Migration execution boundary

`tools/MDGsm.DbMigrator/Program.cs` reads `ConnectionStrings__MDGsmDatabase`. By default, the runner requires the disposable database marker; the explicit unmarked-database override requires a second confirmation environment variable. This is a useful safety guard, but it does not prove the process uses a dedicated `migration_runner` credential distinct from the application runtime credential.

Before O-20 can pass, deployment must provide separate secret references and demonstrate:
- the application process cannot use the migration credential;
- the migration process cannot be used for routine API traffic;
- the migration principal has only the approved migration-window privileges and is not a default superuser;
- the runtime principal cannot create/alter/drop objects, alter roles or bypass approved command boundaries;
- migration checksum and target-baseline checks are enforced independently of the credential.

## 6. Required canonical object-level matrix

The final O-20 matrix must enumerate each actual database/schema/object and privilege, not only broad object classes. At minimum it must include:

1. Database CONNECT and TEMP privileges.
2. Schema USAGE/CREATE and ownership.
3. Table SELECT/INSERT/UPDATE/DELETE/TRUNCATE/REFERENCES/TRIGGER per table and per column where applicable.
4. Sequence USAGE/SELECT/UPDATE.
5. View/materialized-view SELECT and RowScope enforcement.
6. Function/procedure EXECUTE, including SECURITY DEFINER owner, safe search_path, PUBLIC revocation and argument validation.
7. Type/domain/extension privileges.
8. Default ACLs and PUBLIC privileges.
9. Role attributes, LOGIN status, memberships, INHERIT/SET ROLE behavior and grant options.
10. Audit/ledger immutability and trigger ownership.
11. Backup/restore privileges, WAL access and key custody separation.
12. Emergency allow-list, time limit, independent authorization, audit and revocation.

The object inventory must explicitly cover the current `identity.*`, Persons tables, Finance Party table and migration metadata, plus every subsequently approved module object. Cross-module private-table access is denied unless a versioned owning-module contract expressly authorizes a narrow boundary.

## 7. Required independent-credential tests

The following tests are specifications; no result is claimed here:

| Test | Required result | Current evidence |
|---|---|---|
| O20-T01 Runtime identity and role inheritance | Exact disposable LOGIN -> canonical `runtime_app`; no owner/migration/backup/emergency membership | CANDIDATE ADDED IN PR #86; EXACT-SHA CI PENDING; production identity not proven |
| O20-T02 Runtime DDL and role changes | CREATE/ALTER/DROP/CREATE ROLE/GRANT/SET ROLE escalation denied | Partial database-level evidence only |
| O20-T03 IdentityAccess least privilege | Only approved operations on each IdentityAccess table/column succeed; account insert/bootstrap boundary explicit | Partial; account insert denied, application save path unresolved |
| O20-T04 Persons least privilege | Approved registration/read/update path works; unrelated/private/sensitive direct access denied | NOT PROVEN |
| O20-T05 Finance boundary | Runtime cannot directly mutate committed finance/ledger facts; approved command boundary works | NOT PROVEN |
| O20-T06 Worker boundary | No separate worker identity unless approved; if approved, queue-only privileges and no business ledger mutation | NOT PROVEN |
| O20-T07 Observer/RowScope | Approved views only; no base-table DML, side-effect function or out-of-scope rows | NOT PROVEN |
| O20-T08 Migration isolation | Dedicated migration LOGIN, no app reuse, no routine superuser | NOT PROVEN |
| O20-T09 Backup/Recovery isolation | Backup/restore/WAL permissions only; absent from app hosts; audited | NOT PROVEN |
| O20-T10 Emergency mode | Narrow allow-list, separate authorization, expiry/revocation and no commercial commands | NOT PROVEN |
| O20-T11 Ownership/default ACLs | NOLOGIN owner; no unintended PUBLIC grants, memberships or default privileges | Partial catalog fingerprint evidence only |
| O20-T12 Credential lifecycle | Rotation, revocation, restore and incident access proof | NOT PROVEN |
| O20-T13 End-to-end application identity | Running API connects as the exact runtime LOGIN and completes only approved operations | E2E candidate added in PR #86; API host and direct SQL probe share `runtime_app`; exact-SHA CI pending |

## 7.1 New disposable runtime-LOGIN/API evidence candidate in PR #86

The candidate is deliberately scoped to the existing IdentityAccess-only grant script. It does not claim the complete N.6 matrix, Persons/Finance runtime grants or production readiness.

- Identity: `runtime_app` is the canonical LOGIN; `mdgsm_runtime` is only a NOLOGIN support group. The CI test uses local trust authentication in an isolated, marked disposable PostgreSQL service and stores no reusable credential.
- Same-principal proof: direct SQL checks and the HTTP API host use the same runtime connection string.
- Positive path: approved IdentityAccess account/session reads and API login/session lifecycle must succeed.
- Negative path: schema CREATE, account INSERT, session DELETE, audit UPDATE, CREATE ROLE and direct Persons base-table SELECT must fail with SQLSTATE `42501`.
- Boundary: no positive Persons/Finance access is asserted. Those grants require the complete object-level N.6 matrix and owning-module command contracts.

Acceptance requires exact-head PostgreSQL 18 CI PASS with the source SHA and logs bound to that run. Until then, this is a test candidate and not evidence of PASS.

## 7.1 Runtime candidate test expansion — PR #86

The disposable-only candidate now checks the runtime login attributes and a broader set of privilege-escalation denials.

- `runtime_app` is provisioned as LOGIN; `mdgsm_runtime` remains a NOLOGIN support group. The test confirms LOGIN/INHERIT and rejects SUPERUSER, CREATEDB, CREATEROLE, REPLICATION and BYPASSRLS attributes.
- The HTTP `WebApplicationFactory` host and direct SQL probes use the same runtime connection identity.
- Negative checks include schema CREATE, table ALTER/DROP, INSERT where not granted, DELETE/TRUNCATE, unauthorized column UPDATE, audit UPDATE, CREATE/ALTER ROLE, GRANT, SET ROLE escalation and direct Persons base-table SELECT.
- Every denied command is expected to fail with PostgreSQL SQLSTATE `42501`; positive IdentityAccess SQL reads and HTTP login/session lifecycle are exercised.
- PostgreSQL LOGIN roles and memberships exist only in the marked disposable service, use local trust authentication, and contain no reusable password. This does not create production credentials or memberships.

The candidate is **not** the full N.6 proof. See `MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001_v0.2.md` for the five-identity/object matrix and outstanding catalog evidence. It does not prove production LOGIN provisioning, all five logical identities, every ACL/default ACL, owner isolation, extension privileges, or RowScope. The current grant script still has broad table-level SELECT on IdentityAccess accounts/sessions and recovery-table grants; these require data-scope/ownership review and must not be mistaken for row-level least privilege. The account INSERT/bootstrap mismatch remains intentionally unresolved.
## 7.2 Object-level role matrix and materialized DB inventory

The controlled object-by-object inventory and expected role behavior are recorded in `docs/security/MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001_v0.1.md`. It enumerates the materialized IdentityAccess, Persons, Finance Party-account, temporary workflow, merge, and migration-metadata tables, and specifies the intended boundary for each of the five canonical N.6 identities.

The runtime_app disposable candidate now includes denied direct SELECT probes for all currently materialized Persons tables, temporary-workflow tables, merge records, Finance Party-account identity and migration metadata, in addition to the IdentityAccess privilege/escalation tests. This is a negative direct-table test only; it does not establish that API-level module operations are positively authorized or prove RowScope.

Current role-matrix disposition:
- `runtime_app`: partial IdentityAccess grant-template execution and HTTP API candidate; effective ACL/catalog and positive module-owner command paths still incomplete.
- `migration_runner`, `backup_operator`, `reporting_ro`, `emergency_operator`: no distinct real/test login and no effective privilege proof yet; their expected envelopes and required denied operations are specified in the new matrix.
- All schemas/object owners, default privileges, PUBLIC/inherited ACLs, functions, sequences, extensions, trigger/SECURITY DEFINER ownership and RowScope still require exact PostgreSQL catalog evidence. No production identity or grant was changed.

## 7.2 Effective membership, ownership and per-column matrix candidate

The latest PR #86 candidate adds the following checks inside the marked disposable IdentityAccess database:
- Disposable support LOGIN classes are represented by NOLOGIN test roles only. `runtime_app` must be a member of `mdgsm_runtime` and must not be a member of `migration_runner`, `backup_operator`, `reporting_ro`, `emergency_operator` or the test-only schema-owner role. This verifies separation for the disposable runtime principal; it does not provision or approve production support identities.
- Catalog queries require zero schemas, relations (including tables/views/sequences) and functions/procedures owned by `runtime_app`, plus no CREATE privilege on `identity` or `public`.
- The test executes a 15-assertion effective privilege query for selected IdentityAccess table and column privileges: approved account/password and session lifecycle operations, insert-only login attempts/audit, recovery challenge consumption, and restricted system-settings updates; unapproved email/max-active-users/session-created-time changes and account/audit updates must remain denied.
- Direct denial probes now include public-schema table/sequence creation and Persons/Finance inserts in addition to reads, DDL and identity/role escalation.
- GRANT non-escalation is verified by querying the target role's effective privilege after the attempted GRANT. This avoids a false assumption that PostgreSQL must raise `42501`; it may instead emit a warning and grant nothing.

Acceptance still requires the exact final SHA to pass the complete PostgreSQL 18 integration job and checksum gate. These checks remain candidates until that run is complete.
## 7.3 Recorded runtime_app PostgreSQL 18 execution evidence

| Evidence field | Result |
|---|---|
| Exact tested source SHA | `7ef35cca3b3c86757ce1893656f9b6f617cdb4d9` |
| Workflow | `44912452300` — MD-Gsm Persons Module Gates |
| PostgreSQL job | `136871018414` — `persons-postgres-migrations` |
| Database | Marked disposable PostgreSQL `18.6` service |
| Integration test result | 37 passed / 0 failed / 0 skipped |
| Checksum gate | `persons-spec-lint` job `136871018380` — PASS |
| Runtime identity | `current_user=runtime_app`, disposable LOGIN via local trust; membership in `mdgsm_runtime` only |
| API identity | HTTP `WebApplicationFactory` and Npgsql data source used the same runtime connection identity |

The successful 37/37 integration suite is an **executed PASS for the current runtime_app disposable candidate**. The exact SHA is the last code/test package SHA before the N.3 documentation-only follow-up; the `IdentityAccessHttpEndToEndTests.cs` and other tested code/test source blobs were unchanged by the later N.3/O-21 register-only documentation commit. A fresh exact-HEAD workflow is still required to bind all latest documentation/checksum changes.

### Precisely what this PASS proves
- `runtime_app` login attributes and inheritance are asserted; it has only the `mdgsm_runtime` support membership, no membership in migration/backup/reporting/emergency/test-owner roles, no schema/table/function ownership and no CREATE on identity/public schemas.
- The test reads effective ACLs for the selected 15 IdentityAccess table/column privileges and verifies positive/negative results under the real disposable LOGIN.
- It denies direct runtime DDL, sequence creation, protected IdentityAccess DML, role escalation and direct Persons/Finance/temporary/merge/infrastructure access; GRANT non-escalation is verified by querying the target role's actual privilege after the command.
- The API HTTP login/session lifecycle test runs with the same disposable runtime LOGIN connection used for direct SQL probes.

### What remains OPEN
- No live production catalog was inspected and no production LOGIN, credential, membership, grant, revoke or owner privilege was changed.
- `migration_runner`, `backup_operator`, `reporting_ro` and `emergency_operator` still have no independent positive/negative LOGIN workflow proof. Their exact allowlists and credential isolation are not implemented.
- The candidate does not prove every schema/table/column/view/materialized view/sequence/function/procedure/extension ACL or default ACL; does not prove complete database CONNECT/TEMP/RowScope; and does not prove all SECURITY DEFINER exposure/trigger ownership and owner separation.
- The `mdgsm_runtime` template still has account INSERT/bootstrap and recovery-table boundary mismatches described earlier. O-46 remains disabled and gated.
- O-20 remains OPEN until the full five-identity N.6 catalog and RowScope matrix, independent review and fresh exact-head package run are accepted.

## 8. Safe implementation sequence

1. Approve the canonical mapping of the five N.6 logical identities to actual PostgreSQL LOGIN/NOLOGIN roles and memberships; resolve the Background Worker role question without inventing a sixth canonical identity.
2. Obtain a fresh disposable PostgreSQL 18 catalog from the exact main SHA and enumerate all owners, ACLs, default ACLs, role attributes, memberships, schemas, tables, sequences, functions and extensions.
3. Generate the object-by-object privilege manifest and compare it with the current SQL grant script. Resolve the account-insert/bootstrap mismatch and recovery-table access.
4. Implement changes only in a new, versioned, disposable-test migration/operation package after the role mapping and object matrix are approved. Do not consume reserved migration 0009 without its existing gate.
5. Run positive and negative tests with independent real LOGIN credentials for every canonical identity, including API end-to-end use of the runtime identity.
6. Record exact source SHA, PostgreSQL version, role/catalog fingerprints, test logs, artifact digests and denied-command results.
7. Obtain independent security review and explicit owner approval before any non-disposable database grant or credential provisioning.

## 9. Status and non-authorization

O-20 remains **OPEN**. The checked-in IdentityAccess grant script and PR #86 disposable `runtime_app` LOGIN/API candidate are partial evidence only; no exact-head PASS is recorded and the full canonical N.6 matrix remains OPEN. No production role, LOGIN credential, membership, grant, revoke, migration, secret, listener, recovery activation, G1 activation, Posting Release or Go-Live is authorized by this audit.

**END — MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 v0.6**
