# MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001

| Field | Value |
|---|---|
| Version / Date | 1.0 / 2026-11-21 |
| Scope | O-20, master architecture N.6 database identity/privilege contract |
| Status | OWNER-APPROVED N.6 POLICY BASELINE — runtime_app and actual CLI migrator identity proofs PASS on SHA c29d22f in Linux/Windows PostgreSQL 18; backup/reporting/emergency remain no-grant; full catalog proof OPEN |
| Authority | Derived from accepted architecture v3.0.3 N.6, current checked-in migrations and runtime grant script |
| Safety boundary | No production LOGIN, credential, grant, ownership, schema, migration or feature activation was changed |

## 1. Purpose and acceptance rule

This matrix distinguishes (a) policy expected by N.6, (b) grants currently defined in repository source, and (c) effective PostgreSQL proof actually executed under a distinct LOGIN. Presence of a grant template or a successful migration test is not proof of effective production privileges.

The five logical identities are canonical and distinct:

- `runtime_app`: normal API execution.
- `migration_runner`: approved versioned migration only, after Gate B-final.
- `backup_operator`: controlled backup/WAL/restore operations only.
- `reporting_ro`: approved reporting views/read models subject to RowScope.
- `emergency_operator`: allowlisted technical incident actions only.

Production provisioning of any of these LOGINs, passwords, secrets, membership, object ownership or grants remains DBA/platform work outside source control. CI creates only ephemeral test principals inside a marked disposable PostgreSQL database using its isolated local trust configuration.

## 2. Materialized object inventory

The following objects are taken from current repository migration sources, not inferred from future designs.

| Namespace | Materialized objects | Owning domain | Migration/source |
|---|---|---|---|
| `identity` | `accounts`, `roles`, `permissions`, `account_roles`, `role_permissions`, `sessions`, `login_attempts`, `recovery_codes`, `recovery_challenges`, `security_audit_events`, `system_settings` | IdentityAccess | `database/migrations/0003_identityaccess_core.sql` |
| `public` | `persons`, `persons_contact_points`, `persons_addresses`, `persons_operations`, `persons_access_audit` | Persons | `database/migrations/persons/0002_persons_core.sql` |
| `public` | `persons_temporary_workflows`, `persons_temporary_workflow_payloads` | Persons | `database/migrations/0007_persons_temporary_workflows.sql` |
| `public` | `persons_party_merge_records` | Persons | `database/migrations/0008_persons_party_merge.sql` |
| `public` | `finance_party_accounts` | Finance | `database/migrations/0006_finance_party_accounts.sql` |
| `public` | `mdgsm_schema_version` | Platform / Infrastructure | `database/migrations/0001_infrastructure_schema_version.sql` |

No Accounting Journal, StockMovement, Inventory, tax, or reporting-view objects are asserted to exist by this inventory. The reserved Accounting migration 0009 remains unavailable for use in this work package. The catalog audit must additionally inspect every schema, table, view/materialized view, sequence, function/procedure, trigger, extension, owner and default ACL actually present in the disposable/target catalog; the table above is the checked-in migration inventory only.

## 3. Object-level matrix: runtime_app

Status vocabulary: **DEFINED** = checked-in grant script expresses the privilege; **DENY-TEST-CANDIDATE** = a test attempts the operation and expects SQLSTATE 42501; **NOT-GRANTED-IN-SOURCE** = no grant exists in the reviewed template; **OPEN** = effective privilege or object-specific command contract still needs proof.

| Schema / object | Required runtime behavior | Current source grant | Candidate evidence / remaining gap |
|---|---|---|---|
| `identity.roles`, `identity.permissions`, `identity.account_roles`, `identity.role_permissions` | SELECT only; no seed/assignment/permission mutation | SELECT defined | Verify exact LOGIN effective ACL; no writes |
| `identity.accounts` | SELECT; update only declared login/password/lockout/profile state columns; no INSERT/DELETE/TRUNCATE; no role/owner writes | SELECT and column-limited UPDATE defined; INSERT explicitly withheld | Candidate verifies read path and denies INSERT, unauthorized email UPDATE, DELETE/TRUNCATE/DDL. Actual allowed column-level UPDATE command paths need positive + negative column matrix |
| `identity.sessions` | SELECT/INSERT; update only last-seen, lease, revoke fields; no DELETE/TRUNCATE | SELECT/INSERT and column-limited UPDATE defined | E2E candidate uses runtime LOGIN and tests HTTP session lifecycle; direct DELETE/TRUNCATE denied |
| `identity.login_attempts` | SELECT/INSERT only; no UPDATE/DELETE/TRUNCATE | SELECT/INSERT defined | Exact LOGIN end-to-end and append-only denial tests pending |
| `identity.recovery_challenges` | SELECT/INSERT; only consumed/revoked columns may update; no issuance activation by grant alone | SELECT/INSERT and column-limited UPDATE defined | Recovery remains disabled; positive workflow test not present |
| `identity.recovery_codes` | SELECT; only used/revoked fields may update; provisioning withheld | SELECT and column-limited UPDATE defined | No offline code issuance; runtime path not activated |
| `identity.security_audit_events` | INSERT only; no read/update/delete/truncate/trigger mutation | INSERT defined; migration revokes PUBLIC UPDATE/DELETE/TRUNCATE and installs mutation trigger | Candidate denies UPDATE; complete catalog inspection of PUBLIC/inherited/effective rights and trigger ownership pending |
| `identity.system_settings` | SELECT; update only `updated_at_utc` for singleton row lock | SELECT and column-limited UPDATE defined | Need exact function/command proof, no broad settings mutation |
| `public.persons`, contacts, addresses, operations, access audit | No direct cross-module table access; access only through an approved Persons API/command/function contract and authorized row scope | No runtime grant template exists for these objects | Candidate expanded to SELECT-deny probes for each listed Persons table |
| `public.persons_temporary_workflows`, payloads | No direct access outside approved temporary-workflow owner contract | No runtime grant template exists | Candidate SELECT-deny probes; payload ciphertext handling and command boundary still need owning-module tests |
| `public.persons_party_merge_records` | No direct cross-module mutation/read; merge owner contract only | No runtime grant template exists | Candidate SELECT-deny probe; owner-controlled append-only integrity and merge service tests remain separate |
| `public.finance_party_accounts` | No direct IdentityAccess/Persons runtime access; Finance contract only | No runtime grant template exists | Candidate SELECT-deny probe; Finance-owned API/transaction contract remains separate |
| `public.mdgsm_schema_version` | No routine API access | No runtime grant template exists | Candidate SELECT-deny probe |
| Any future Journal, StockMovement, AuditSecurity ledger, tax or inventory object | Direct mutation forbidden; use approved owner command/function; immutable facts append/reverse only | No such runtime grants are part of the current IdentityAccess template | Future owner schema contracts and N.6 catalog tests required; not asserted as current materialized tables |

The candidate direct SQL denial probes cover: identity/public schema table and sequence CREATE, table ALTER/DROP, account INSERT and disallowed column UPDATE, session DELETE/TRUNCATE, audit UPDATE, CREATE/ALTER ROLE, SET ROLE escalation, direct Persons/Finance/temporary/merge/infra object SELECT and direct Persons/Finance INSERT. Other denied SQL commands require SQLSTATE `42501`. PostgreSQL may issue a warning/no-op rather than an error for a GRANT with no grantable privilege; the test therefore attempts the grant to a disposable target role and verifies that `has_table_privilege` remains false.

## 3.1 Effective runtime membership and ownership checks — PR #86 candidate

The same disposable `runtime_app` LOGIN used by the HTTP API candidate is additionally tested for:
- `mdgsm_runtime` is the only approved support-group membership in the disposable role model.
- No membership in `migration_runner`, `backup_operator`, `reporting_ro`, `emergency_operator` or the test-only schema-owner role.
- No ownership of schema, relation (table/view/sequence) or function/procedure objects in `identity` or `public`.
- No CREATE on `identity` or `public` schemas.
- A 15-assertion effective privilege query covers selected table/column grants across accounts, sessions, login attempts, recovery challenges/codes, security audit events and system settings. It includes positive approved operations and negative unapproved columns/table actions.

The support roles and owner placeholder above exist only as ephemeral NOLOGIN test roles in the marked disposable database. No production principal names, credentials, membership or owner role are created. This test is not proof of the support identities' own allowlists, backup/restore, migration, RowScope or emergency workflows.

Exact-head PostgreSQL 18 CI and checksum verification are pending; source addition is not a PASS.
## 3.2 Executed runtime_app candidate result — PASS once

The test in `tests/MDGsm.Domain.Tests/IdentityAccess/IdentityAccessHttpEndToEndTests.cs` passed as part of the PostgreSQL 18 integration stage on SHA `7ef35cca3b3c86757ce1893656f9b6f617cdb4d9` (workflow `44912452300`, job `136871018414`). The job reported **37 passed, 0 failed, 0 skipped**. The SHA256 manifest check also passed in job `136871018380`.

The PASS includes the same effective-membership, ownership, no-schema-CREATE, 15-column-privilege and cross-module direct-access assertions listed in §3.1, plus the HTTP IdentityAccess login/session lifecycle using the same disposable `runtime_app` LOGIN connection.

Scope of the PASS is intentionally limited: this is a test-only identity in the marked disposable database with trust authentication, no reusable password, and no production credential or membership. It proves the current source grant template plus the tested API paths behave as asserted under this principal. It does not prove the production credential lifecycle, all five logical identity allowlists, full object/RowScope catalog, or grant model for future modules.

After the tested SHA, PR #86 received only controlled N.3/O-20/O-21 documentation and manifest changes; the IdentityAccess candidate test source blob was not changed. A new full package run on the latest PR head is required before package-level acceptance.

## 3.3 Dormant recovery-code table — runtime access explicitly denied

The email-only owner policy excludes offline recovery-code issuance in this phase. PR #86 therefore removes `ConsumeRecoveryCodeAsync` from the IdentityAccess application port and its PostgreSQL implementation, removes SELECT/UPDATE from the `mdgsm_runtime` grant template, and explicitly revokes all privileges on `identity.recovery_codes` from the runtime support role when the idempotent grant script is applied.

The disposable runtime_app test now expects both table SELECT and UPDATE to be false and executes direct SELECT/UPDATE denial probes. The effective privilege matrix increases to 16 assertions. Migration 0003 still contains the table for schema-history compatibility; it is dormant and inaccessible to runtime_app. No production grant script or database has been changed.

The exact-source PostgreSQL 18 suite passed on SHA `284ac9dbb94fbbd05d19de18146ccc27d35d8cbf`, job `136876940898`, 37 passed / 0 failed / 0 skipped. The checksum gate `136876940885` passed with 459 tracked files. This is a disposable runtime_app proof, not closure of the four other identities or the full object/RowScope matrix.
## 3.4 Email-only Recovery — no runtime rights on offline codes

The email-only policy excludes offline recovery-code provisioning and consumption. PR #86 removes `ConsumeRecoveryCodeAsync` from `IRecoveryStore` and the corresponding PostgreSQL implementation. The runtime grants on `identity.recovery_codes` are removed and `REVOKE ALL PRIVILEGES ... FROM mdgsm_runtime` is included to clear legacy grants when the updated grant script is reapplied.

The disposable runtime privilege test now checks 16 effective ACL assertions and explicitly requires `has_table_privilege(runtime_app, identity.recovery_codes, SELECT/UPDATE)` to be false, together with direct denied SQL probes. The `identity.recovery_codes` table remains dormant in migration 0003; no new migration or DDL is authorized. These new source assertions remain CI PENDING until the latest exact-head PostgreSQL 18 run completes.

## 4. Support-identity matrix

| Identity | Allowlist to be defined and tested | Mandatory denials | Repository/runtime proof |
|---|---|---|---|
| `migration_runner` | Versioned migration objects/scripts only after Gate B-final, under approved maintenance window and migration manifest/checksum | Routine API/business use, posting, business DML outside migration, role creation/membership, superuser privileges, unmanifested DDL | No canonical LOGIN or exact migration execution principal proven; OPEN |
| `backup_operator` | Backup/WAL read and controlled restore only; separate restore target and explicit restore runbook | Daily business DML, posting, identity mutation, arbitrary schema DDL, changing audit evidence, runtime application use | No role/membership/backup proof or restore drill under distinct identity; OPEN |
| `reporting_ro` | SELECT only on approved views/read models with server-enforced RowScope | DML, base Journal/Movement/Audit table access, PII beyond scope, bypass of row filters, function side effects | No approved reporting views/read-model grants or RowScope DB proof inventoried; OPEN |
| `emergency_operator` | Only individually named, time-bounded, audited technical incident functions/commands | Commercial document creation, posting, stock movement, account/role creation, arbitrary DDL, unrestricted business writes | No allowlist functions, time-bound principal workflow or audit proof; OPEN |

These are policy envelopes, not executable grants. Do not create LOGINs or assign any memberships until the exact role/object privilege rows, object owners, grantors, expiry, approval and audit behaviour are explicitly accepted and tested.

## 5. Mandatory catalog proof

For each actual test LOGIN (and for every role reachable through membership or `SET ROLE`), evidence must include:

1. `pg_roles`: LOGIN, SUPERUSER, CREATEDB, CREATEROLE, REPLICATION, BYPASSRLS, INHERIT, connection limit and validity; exact memberships from `pg_auth_members`; no owner/migration/backup/emergency escalation path.
2. Schema/object owner and grants: `pg_namespace`, `pg_class`, `pg_attribute`, `pg_proc`, `pg_trigger`, `pg_extension`, `pg_default_acl`, ACL expansion via `aclexplode`, plus PUBLIC and inherited grants. Include tables, partitions, views, materialized views, sequences, functions/procedures and extension-owned objects.
3. Effective privilege probes using the real LOGIN: `has_schema_privilege`, `has_table_privilege`, `has_column_privilege`, `has_sequence_privilege`, `has_function_privilege`, `current_user`, `session_user`, and controlled `SET ROLE` tests.
4. Positive and negative commands per row: approved operation succeeds; DDL/owner/grant escalation and forbidden direct table operations fail with expected SQLSTATE; failed attempts leave no persistent state.
5. RowScope: positive/negative row-access pairs for each scoped read, including cross-tenant or unassigned-resource identifiers; role grants alone cannot be treated as RowScope proof.
6. Capture the exact source SHA, database version, migration list/checksums, test LOGIN role attributes/membership, catalog snapshot hash, test result and run/job ID. Never capture a password, token, OTP, connection secret or raw PII.

The current workflow candidate is only an IdentityAccess runtime proof against an ephemeral PostgreSQL service and the existing `mdgsm_runtime` grant template. Its cross-module denials are valuable regression evidence, not a complete production N.6 matrix. Exact-head PostgreSQL CI, full role catalog checks, all positive module command paths, the four support identities, default ACLs, object-owner separation, extension/function privileges and RowScope remain required.

## 6. Explicit non-authorization

This matrix does not authorize production LOGIN creation, credentials, grants, migrations, schema/ownership changes, Recovery/bootstrap, IdentityAccess or Persons activation, Finance Posting, or Go-Live. O-20 remains OPEN until all five identities and all discovered database objects are reconciled against the complete N.6 matrix and independently reviewed on an exact accepted SHA. The runtime_app disposable proof is PASS once on SHA `7ef35cca…`, not closure of the other four identities or the full catalog.

**END — MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 v0.8**


## 7. Owner-approved policy decision — 2026-11-21

The owner approved the least-privilege five-identity model proposed for O-20. This section is the accepted policy baseline for implementation and disposable-database tests. It does not authorize production provisioning or claim that the remaining proofs have passed.

### 7.1 Canonical identity mapping

| Canonical identity | Login / execution boundary | Allowed scope | Explicit prohibitions |
|---|---|---|---|
| `runtime_app` | Dedicated application LOGIN; inherits only the `mdgsm_runtime` NOLOGIN support group | Approved module command paths and scoped reads/writes | No migration, backup, reporting, emergency, owner, role-management or schema-CREATE membership |
| `migration_runner` | Dedicated deployment-only LOGIN, unavailable to API/runtime hosts | Versioned, checksum-verified migrations during an approved maintenance/deployment window; may SET ROLE only to the registered NOLOGIN schema-owner role required by the migration manifest | No runtime API use, business posting, superuser, role creation, unmanifested DDL or permanent owner membership |
| `backup_operator` | Dedicated backup/restore LOGIN isolated from application hosts | First implementation tranche: logical backup through read-only access to approved backup scope and required metadata; restore only into an explicitly designated isolated target under a separately audited restore procedure | No business DML, posting, identity mutation, audit alteration, arbitrary DDL or runtime API use. Physical backup/WAL replication privileges are NOT granted until a separate capability proof demonstrates they are necessary and least-privileged |
| `reporting_ro` | Dedicated read-only LOGIN | SELECT on approved reporting views/read models only, after server-enforced RowScope is implemented and tested | No base-table access, DML, side-effecting functions, scope bypass or access to unapproved PII |
| `emergency_operator` | Dedicated, time-bounded LOGIN activated only for an approved incident | EXECUTE on individually allow-listed, audited technical functions; fixed safe `search_path`; explicit expiry and revocation | No general table DML, commercial/financial document creation, posting, stock movement, account/role creation, arbitrary DDL or superuser |

### 7.2 Shared controls and ownership

1. Each canonical identity is distinct. No LOGIN inherits another canonical identity's group or credentials.
2. Schema and object ownership belongs to registered NOLOGIN owner roles, never to application or support LOGINs.
3. The migration principal can assume a schema-owner role only for the approved migration operation; the owner role is not granted to `runtime_app`, `backup_operator`, `reporting_ro` or `emergency_operator`.
4. No identity is SUPERUSER, CREATEDB, CREATEROLE, REPLICATION or BYPASSRLS by default. Any future exception requires a separate architecture decision and proof; the present policy grants none.
5. PUBLIC privileges and unintended default ACLs are revoked/absent. Role membership, INHERIT and SET ROLE behavior are explicitly tested.
6. Every object is inventoried from the live disposable PostgreSQL catalog, including tables, partitions, views/materialized views, sequences, functions/procedures, triggers, types, extensions, owners, PUBLIC grants and default ACLs.
7. Positive and negative operation pairs are required for every granted privilege. Denial tests must assert the effective privilege result and command outcome; PostgreSQL GRANT warnings/no-ops are not treated as denial evidence without checking the target ACL.
8. RowScope is a mandatory authorization boundary for reporting. Until approved views and positive/negative scope tests exist, `reporting_ro` receives no reporting data grants.
9. Until allow-listed emergency functions and expiry/revocation tests exist, `emergency_operator` receives no operational data grants.
10. Production provisioning, credential delivery, role memberships, grants and ownership changes remain DBA/platform operations and are not authorized by this decision record.

### 7.3 Execution order and acceptance evidence

- O20-A: materialize the five disposable LOGINs and their NOLOGIN support/owner roles; assert role attributes, membership closure, owner separation, PUBLIC/default ACLs and absence of privilege escalation.
- O20-B: prove `runtime_app` positive/negative command matrix and preserve its existing email-only recovery-code denial.
- O20-C: prove migration-only SET ROLE and manifest/checksum enforcement; prove the API/runtime identity cannot use the migration LOGIN.
- O20-D: prove backup logical-read/isolated-restore boundary without granting business writes; do not enable physical backup/WAL privileges in this tranche.
- O20-E: prove reporting views and RowScope after those views exist; otherwise record NOT IMPLEMENTED and verify no grants.
- O20-F: prove emergency allow-list, independent authorization, expiry, revocation and audit; otherwise record NOT IMPLEMENTED and verify no grants.
- O20-G: capture catalog snapshot, effective ACL matrix, source SHA, PostgreSQL version, migration/checksum list, run/job identifiers and evidence digest; obtain independent review.

O-20 remains OPEN until all applicable rows have executable positive/negative evidence and independent review. The approved policy baseline is not a PASS for the four support identities.

**END ADDENDUM — OWNER-APPROVED O-20 POLICY BASELINE v0.7**

## 8. Disposable support-identity baseline proof — added 2026-11-21

A new integration test, `tests/MDGsm.Domain.Tests/Security/O20SupportIdentityPostgresProofTests.cs`, materializes four unique disposable LOGINs for `migration_runner`, `backup_operator`, `reporting_ro` and `emergency_operator` inside the marked local disposable PostgreSQL database. It checks:

- LOGIN is enabled; SUPERUSER, CREATEDB, CREATEROLE, REPLICATION and BYPASSRLS are disabled; INHERIT is disabled.
- No membership edges exist to or from any of the four identities.
- No identity owns a database object.
- No schema USAGE/CREATE, table SELECT/INSERT/UPDATE/DELETE/TRUNCATE, function EXECUTE or database CREATE privilege is effective on the disposable probe objects.
- Direct SELECT, INSERT, UPDATE, DELETE, TRUNCATE, schema DDL and function execution are denied with SQLSTATE 42501.
- The test creates and removes only uniquely named ephemeral roles/schema in the marked disposable database; it does not provision production principals or credentials.

**Evidence status: source added; exact-head PostgreSQL 18 CI PENDING.** This is a deny-all baseline proof only. It does not yet prove migration manifest execution/temporary SET ROLE, logical backup and isolated restore, reporting views/RowScope, emergency allow-list/expiry/revocation, complete live-catalog/default-ACL inventory, or any positive support-identity operation. Those capabilities remain ungranted and O-20 remains OPEN. The existing `runtime_app` proof is separate and does not convert this partial support-role test into full N.6 acceptance.

**END ADDENDUM — O-20 SUPPORT-IDENTITY BASELINE PROOF v0.1**


## 9. Implementation candidate update — exact SHA dffa5c1fbb6b79a7b0304048ce4d98816e29fd9b

The four disposable support LOGINs are no longer modeled as uniformly deny-all:

- `migration_runner`: one explicit membership to the disposable NOLOGIN schema owner; `INHERIT FALSE`, `SET TRUE`, `ADMIN FALSE`. The test asserts `current_user` becomes the owner only after `SET ROLE`, while `session_user` remains the migration LOGIN; a probe object is created under the owner identity and the session is reset. Outside that explicit role transition, the migration LOGIN has no effective privilege on the probe schema/table/function.
- `backup_operator`, `reporting_ro`, `emergency_operator`: no memberships, no object ownership, no schema/table/function/database-create authority, and direct operations are denied.
- All four are checked for LOGIN, NOINHERIT and absence of SUPERUSER/CREATEDB/CREATEROLE/REPLICATION/BYPASSRLS.
- The test expands PUBLIC ACLs for the disposable probe schema, table-like objects and function, and asserts no default ACL rows exist for the four LOGINs or owner role in that disposable database.

This test is not a positive backup, reporting, or emergency proof. It does not establish migration-runner credential isolation or bind the real migration process to the tested identity. The schema owner is a uniquely named disposable test role, not a production owner role registration.

The runtime `SaveAsync` INSERT fallback has been removed: missing accounts now produce a fail-closed exception. A new PostgreSQL end-to-end assertion invokes the store through the exact disposable `runtime_app` principal and verifies that an unprovisioned account cannot be created through the runtime store. Existing account update paths remain unchanged.

Exact-head CI was queued at record time. Until successful PostgreSQL 18 execution and log inspection, these additions are source candidates only.

**Disposition:** O-20 remains OPEN. No production grants, credentials, role memberships, object ownership, or operational capability was enabled.


## 10. O20-C — executable migration identity contract

The DbMigrator CLI now requires configured expected LOGIN and owner role names. The runner validates the authenticated `session_user`, role attributes, exact membership closure and membership options, then executes `SET ROLE` and verifies `current_user` before it opens the migration transaction.

The disposable CI model uses:
- `migration_runner`: LOGIN, NOINHERIT, no elevated role attributes, exactly one membership.
- `mdgsm_schema_owner`: NOLOGIN, no elevated role attributes, the sole membership target.
- Membership: ADMIN FALSE, INHERIT FALSE, SET TRUE.
- Database and `public` CREATE are granted only to `mdgsm_schema_owner`; the LOGIN must have no effective direct CREATE.
- The Linux and Windows PostgreSQL 18 migration jobs execute the actual CLI as the dedicated LOGIN; Linux uses runner-local trust only inside the disposable service, while Windows uses a per-run random password in process environment. Linux additionally runs the O-20 support identity catalog test explicitly.

The single NOLOGIN owner currently owns objects created by the existing migration scripts, including objects still located in `public`. This proves a bounded deployment migration identity, not final per-module schema-owner segregation. Module-specific schemas/owner roles and the complete catalog snapshot remain future O20-G requirements.

The backup, reporting and emergency identities remain no-grant. No production credential or privilege is provisioned by this implementation.


## 11. O20-C exact-head proof result

On SHA `c29d22f02a413885d9d68ee36151a84ca7769b17`, Persons Module Gates run `44924828632` passed. Linux PostgreSQL job `136908626163` and Windows PostgreSQL job `136908626115` both passed.

The actual CLI execution on both platforms authenticated as `migration_runner`, then assumed `mdgsm_schema_owner` with the sole membership configured ADMIN FALSE / INHERIT FALSE / SET TRUE. The LOGIN had no elevated attributes and no effective direct CREATE on the database/public schema. First migration execution applied versions 1–8; second execution applied zero and found eight previously applied. Linux additionally passed the 38-test PostgreSQL Integration suite and the explicitly invoked O20 support identity/catalog proof.

The evidence proves the current CLI execution boundary in disposable PostgreSQL 18 CI. It does not prove production secret delivery/rotation, module-specific owner roles, or the complete database-wide ACL inventory. The backup, reporting and emergency LOGINs remain no-grant and non-operational.

**O20-C: IMPLEMENTED + PROVEN (disposable CI). Overall O-20: OPEN.**
