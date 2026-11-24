# O-46 Bootstrap Database Principal and Provisioning Boundary — Design Proposal

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-BOOTSTRAP-DATABASE-PRINCIPAL-DESIGN-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | **TECHNICAL RECOMMENDATION — OWNER DECISION REQUIRED — NON-AUTHORIZING** |
| Scope | PostgreSQL 18 authority for the one-time initial Product Owner claim |
| Governing owner decision | O-46 Bootstrap Owner Decision v0.1 |
| Related architecture | IdentityAccess Module Baseline v1.0; O-20 N.6 five-identity model; O-20 Role/Object Matrix v1.4; O-46 local IPC threat review v0.1 |
| PR / branch | #92 / security/o20-o21-final-closure |

## 1. Executive recommendation

Use a narrowly scoped, one-time PostgreSQL capability behind the already accepted central-server Setup Wizard:

1. A dedicated, non-login PostgreSQL function-owner role owns one isolated `SECURITY DEFINER` claim routine and has only the exact table/column privileges required to create the first Account, bind the immutable Product Owner principal, consume the one-time challenge, record the idempotent claim result and write the IdentityAccess audit intent.
2. A separate, pre-activation `LOGIN` role (proposed name: `mdgsm_bootstrap_executor`) can connect only to the designated central database and execute only that one claim routine. It receives no direct table privileges, no role memberships, no object ownership, no DDL, no role-management privilege and no general application access.
3. Its credential is generated during trusted central-server database provisioning and held only in an OS-protected secret store accessible to the dedicated local Setup Service SID. It is never exposed to the Desktop client, ordinary API process, command line, environment variables, config files, installer package, logs or telemetry.
4. The local Setup Service authenticates the operator through the accepted Windows Named Pipe boundary, validates the final Wizard input through IdentityAccess and calls the single database routine. The database function checks the one-time claim state and challenge, then atomically creates the Account and Product Owner binding.
5. On successful claim, the Setup Service destroys the local bootstrap credential and closes the setup channel. A separate, trusted deployment/database-owner cleanup step must disable the database LOGIN. The function also permanently denies any second claim after the original claim commits.

This is the recommended least-privilege approach. It is **not yet accepted as an exception to the current N.6 five-canonical-operational-identity model**, and no role or grant is created by this proposal. The Product Owner must explicitly accept the transient bootstrap-only database capability classification before schema or role implementation.

## 2. Source facts observed on the current PR #92 branch

The source tree establishes the following boundaries:

- `database/operations/identityaccess-runtime-role-grants.sql` creates only NOLOGIN `mdgsm_runtime`. It explicitly withholds INSERT on `identity.accounts`, role/permission writes, schema CREATE, DDL, ownership, DELETE and TRUNCATE.
- `PostgresIdentityAccessStores.SaveAsync(Account)` throws when the Account row is absent. The ordinary IdentityAccess runtime therefore cannot provision a missing Account.
- The O-20 N.6 model has five canonical operational identities: `runtime_app`, `migration_runner`, `backup_operator`, `reporting_ro` and `emergency_operator`.
- The O-20 Role/Object Matrix v1.4 records `mdgsm_schema_owner` as a separate NOLOGIN object-owner role, not as a sixth application identity. The tested `migration_runner` has one SET-only path to that schema-owner role.
- `MDGsm.DbMigrator` is a migration-only process. It verifies its configured LOGIN and SET-only owner mapping before DDL. Its authority is intentionally broader than an application command and must not be borrowed by the Setup Wizard for account creation.

These facts mean the normal runtime grant must **not** be expanded with account INSERT, and neither `runtime_app` nor `migration_runner` is an acceptable bootstrap execution identity.

## 3. Alternatives evaluated

| Candidate | Decision | Reason |
|---|---|---|
| Add INSERT on `identity.accounts` to `mdgsm_runtime` | **REJECT** | Gives every ordinary API process an account-creation capability and breaks the accepted runtime boundary. It still would not enforce the one-time immutable Product Owner binding or challenge lifecycle. |
| Use `migration_runner` for owner creation | **REJECT** | Its manifest-controlled SET ROLE path reaches a schema/object owner and DDL authority. That is excessive for a single Account claim and couples owner enrollment to schema migration authority. |
| Connect as PostgreSQL database owner/superuser from the Wizard | **REJECT** | Exposes the highest-trust database credential to an application setup path and enables far more than one Account claim. The database-owner boundary remains a provisioning root, not a routine Wizard identity. |
| Grant direct INSERT/UPDATE on Account/binding tables to a bootstrap LOGIN | **REJECT** | Table DML permits arbitrary rows and bypasses the narrow one-time claim state machine. |
| One function-only bootstrap executor | **RECOMMENDED, OWNER ACCEPTANCE REQUIRED** | The LOGIN can invoke exactly one typed operation; a narrowly privileged NOLOGIN function owner performs the atomic write. |

## 4. Proposed principal classification

### 4.1 Existing N.6 operational identities remain unchanged

No membership or privilege is added to `runtime_app`, `backup_operator`, `reporting_ro` or `emergency_operator`. The migration identity remains limited to the reviewed migration manifest and its declared NOLOGIN owner mapping.

### 4.2 Proposed NOLOGIN function-owner role

Proposed name: `mdgsm_identity_bootstrap_fn_owner`.

Required attributes and boundaries:

- NOLOGIN, NOSUPERUSER, NOCREATEDB, NOCREATEROLE, NOREPLICATION, NOBYPASSRLS.
- Owns only an isolated setup schema, proposed as `identity_bootstrap`, and its single initial-owner claim routine.
- Does not own the `identity` schema or the Account, Role, Permission, Session or Audit tables.
- Receives only the exact object/column privileges required by the claim routine:
  - INSERT into the Account table for the initial Account row;
  - SELECT/INSERT/UPDATE only on the future immutable Product Owner binding and bootstrap-claim/operation state;
  - the minimum read/lock privileges needed to validate the one unclaimed InstallationId and its challenge;
  - INSERT-only access to the IdentityAccess audit-intent table;
  - column-limited UPDATE of the existing bootstrap-completion marker, only if the final schema design retains that marker for this operation.
- No access to role/permission definition or assignment tables, sessions, unrelated Accounts, Recovery, Persons, Organization, Accounting, Platform setup state or business data.

The final privilege list must be generated from the exact approved SQL routine and verified from PostgreSQL effective-privilege catalogs; this document does not pre-approve table grants.

### 4.3 Proposed one-time LOGIN

Proposed name: `mdgsm_bootstrap_executor`.

Required attributes and boundaries:

- LOGIN, NOINHERIT, NOSUPERUSER, NOCREATEDB, NOCREATEROLE, NOREPLICATION, NOBYPASSRLS.
- CONNECTION LIMIT 1 (or a lower limit if the deployment and connection-pool design permits it).
- No role memberships; in particular no membership in `mdgsm_runtime`, `migration_runner`, `mdgsm_schema_owner`, or any role/permission-management group.
- No schema/table/sequence ownership and no direct privileges on `identity.accounts` or any other business table.
- CONNECT only to the designated central application database, USAGE only on the isolated `identity_bootstrap` schema and EXECUTE only on the exact versioned initial-owner claim routine.
- No GRANT OPTION, SET ROLE path, database creation, role creation, schema CREATE, object DDL, arbitrary function EXECUTE or direct table SELECT/DML.
- Authentication must use a strong generated secret with SCRAM-SHA-256 or an independently approved client-certificate alternative. `trust`, hard-coded passwords and shared/default credentials are prohibited.
- The role name and grants are installation/database security metadata, not a new human account, staff member, application login or N.6 operational service identity. It is a pre-activation, one-purpose setup capability. Its existence is nevertheless an explicit additional PostgreSQL LOGIN and therefore requires owner acceptance and O-20 matrix reconciliation.

### 4.4 Windows service and credential custody

- The Setup Service is a dedicated Windows service with its own service SID; do not default to LocalSystem.
- Only that service SID can read the protected bootstrap DB credential and protected InstallationId metadata.
- The ordinary API/runtime process cannot read the bootstrap credential or invoke the pre-activation local setup pipe.
- The raw credential is generated by the trusted database-provisioning step, not embedded in the installer or source. The exact OS secret-store mechanism (DPAPI/CNG/Windows Credential Manager or an approved equivalent), ACL and backup/restore handling remain to be selected and tested by Platform.
- The credential must never be passed via process arguments, environment variables, config files, PowerShell command history, logs, traces, crash dumps, support bundles or CI artifacts.
- After successful claim, the Setup Service removes the local secret, terminates the DB connection, closes the pipe and disables its setup operation. A separately authenticated deployment/database-owner cleanup operation must set the bootstrap LOGIN to NOLOGIN (or drop it after safely removing all dependencies). Neither the executor nor the function-owner receives CREATEROLE to perform this cleanup.
- The exact time-bound lifecycle of the LOGIN before a successful claim, including restart/resume and server-repair behavior, remains an open technical sub-gate. Do not invent an arbitrary credential TTL from the 30-minute challenge TTL: the challenge lifetime and database-login lifecycle are separate controls.

## 5. The single allowed database operation

Proposed routine name: `identity_bootstrap.claim_initial_product_owner_v1(...)`.

The routine is the only public database capability available to `mdgsm_bootstrap_executor`. It is not a general account-create API or SQL proxy.

The request is bound to:

- the authoritative UUIDv7 InstallationId;
- the stable SetupOperationId for the one initial setup;
- purpose `InitialOwnerClaim`;
- challenge identifier and raw one-time proof, with verifier/digest-only stored in PostgreSQL;
- the exact operator SID enrolled and authenticated by the Windows Setup Service;
- the final normalized username and owner profile fields;
- the final versioned password hash produced by IdentityAccess after applying its accepted PasswordPolicy.

The routine must never receive or store the raw owner password. Raw password validation and hashing remain inside the trusted IdentityAccess boundary. The database routine receives only the resulting supported password hash and its version.

On a valid call, in one IdentityAccess-owned transaction it must:

1. Lock and verify the durable bootstrap state for the exact InstallationId.
2. Verify the challenge digest, purpose, SetupOperationId, enrolled operator SID and database-clock expiry.
3. Verify that the InstallationId has not previously claimed a Product Owner and that no conflicting claim/result exists.
4. Create exactly one AccountId and its final username/password-hash/profile record.
5. Create the immutable InstallationId → AccountId Product Owner binding.
6. Consume the challenge and durably record the one-time claim result under the same SetupOperationId.
7. Set the IdentityAccess bootstrap-completion marker if required by the accepted schema design.
8. Insert secret-free IdentityAccess audit intent.
9. Return only a typed result and the AccountId required by the setup orchestrator; do not return password/hash, other account data or role/permission catalog contents.

The routine must return a deterministic already-applied result when the same SetupOperationId and same payload are retried after an unknown commit response. A different operation, different payload, expired/replayed challenge, second claim or changed InstallationId must be denied. Concurrent claim attempts must serialize so exactly one creates the Account and binding.

No role, permission, account-role assignment, SuperAdmin expansion, database LOGIN, Organization record, Accounting period, opening balance or business data is created by this routine. Role/permission provisioning remains a separately reviewed O46-G04 scope. Organization and Accounting setup continue through their own module-owned commands after IdentityAccess returns a durable owner receipt.

## 6. SECURITY DEFINER routine safeguards

The routine should be `SECURITY DEFINER` owned by `mdgsm_identity_bootstrap_fn_owner`, not by the global `mdgsm_schema_owner` or a LOGIN role.

Required safeguards:

- Use a dedicated, non-user-writable `identity_bootstrap` schema. The function-owner role owns this schema; the bootstrap executor receives USAGE only.
- Explicitly revoke EXECUTE from PUBLIC and grant it only to the bootstrap executor. Check and control default privileges so future routines in the setup schema are not accidentally executable by PUBLIC.
- Pin a safe function `search_path` to trusted schemas only, with `pg_temp` last; fully qualify every non-catalog table, type and helper reference.
- No dynamic SQL, arbitrary object/function names, caller-selected schema/table, caller-supplied SQL, generic command dispatcher or file/system command.
- Strict typed parameters, size bounds, Unicode/username validation at the IdentityAccess boundary, no unexpected JSON keys where JSON is used, and generic denial outcomes that do not leak whether an Account/owner exists.
- The function owner has no LOGIN, no CREATEROLE, no schema ownership outside the isolated setup schema and no direct role/permission-management privileges.
- Record the function owner, schema owner, function definition hash, EXECUTE ACL, default ACL and dependent table/column privileges in the O-20 object catalog.
- The migration/deployment process must be able to install or replace the function under the declared owner mapping, but the ordinary runtime and bootstrap executor must not be able to alter the function. The current O-20 single SET-only migration-owner mapping may need a reviewed, exact additional function-owner mapping; no such membership is approved here.

PostgreSQL documents that SECURITY DEFINER functions execute with their owner's privileges and warns that writable schemas in search_path can mask intended objects. PostgreSQL also makes role LOGIN, privilege attributes, connection limits and password validity explicit role properties; role membership SET/INHERIT options are separate and must be verified from effective memberships. The exact PostgreSQL 18 references are linked in section 10.

## 7. Data ownership and migration boundary

| Data / authority | Sole owner | Bootstrap routine behavior |
|---|---|---|
| Account and final credential hash | IdentityAccess | Creates the original Account only |
| Installation-scoped Product Owner binding | IdentityAccess | Creates immutable binding to the new AccountId |
| Challenge and owner-claim receipt | IdentityAccess | Consumes challenge and records idempotent result |
| Local operator SID / InstallationId enrollment facts | Platform deployment/setup boundary | Validates/attests context; does not own Account |
| SetupOperationId and cross-module step receipts | Platform | Not written by the IdentityAccess SQL routine except the operation reference required for IdentityAccess's own claim receipt |
| Workshop identity | Organization | Not created or mutated |
| Initial FiscalPeriod | Accounting | Not created or mutated |
| Canonical AuditSecurity/WORM archive | Existing assigned owner | IdentityAccess writes only its approved local audit intent/outbox boundary |

The current Migration 0003 has bootstrap-related flags/metadata but no complete operational state machine or immutable Product Owner binding lifecycle. The exact future schema must include the minimal challenge, installation binding, one-time claim and idempotency facts only after IdentityAccess data-owner review and WG-0 migration reservation.

Migration 0009 is RESERVED in the current control register and must not be consumed by this proposal. No DDL or SQL file is created here. No new role, user, password, grant, membership or function is provisioned by this document.

## 8. O-20 and O-46 impacts

This proposal requires explicit reconciliation before implementation:

1. N.6's five canonical operational identities remain unchanged.
2. The proposed `mdgsm_bootstrap_executor` is a one-purpose, pre-activation technical capability outside those five operational identities; it is still a real PostgreSQL LOGIN and must therefore appear in the complete role/catalog inventory while setup is pending.
3. `mdgsm_identity_bootstrap_fn_owner` is a NOLOGIN object/function owner, analogous in classification (not privilege scope) to the existing NOLOGIN schema-owner role. It must be separately enumerated in the ownership/effective privilege inventory.
4. The O-20 matrix must explicitly state that the executor is not a member of any operational identity, owns nothing and can execute only the one claim routine.
5. The O-20 matrix must explicitly enumerate the function owner's minimal privileges, schema ownership, function ownership, security-definer status, safe search_path, PUBLIC revocation and migration SET-only owner mapping.
6. The O-20 exact catalog tests must prove both roles' positive and negative effective privileges on PostgreSQL 18 Linux and Windows.
7. The current runtime negative test for Account INSERT must continue to pass unchanged. No runtime grant is broadened.
8. The setup LOGIN's disable/drop operation and secret deletion must be proven after successful claim; the bootstrap LOGIN must not remain a general-purpose operational credential.

O46-G04 therefore remains OPEN until the owner accepts the transient-login classification, WG-0 reserves the schema/migration delta, deployment principal and local secret custody are implemented, and exact-head negative/positive PostgreSQL evidence is independently reviewed.

## 9. Required proof catalogue — NOT RUN

| ID | Test | Required result |
|---|---|---|
| O46-DBP-01 | Canonical role preservation | All five N.6 operational identities retain their accepted purpose and membership boundaries |
| O46-DBP-02 | Executor role attributes | LOGIN only; NOINHERIT; no SUPERUSER/CREATEDB/CREATEROLE/REPLICATION/BYPASSRLS; connection limit 1 |
| O46-DBP-03 | Executor membership | Zero memberships; no SET ROLE path to runtime, migration, schema owner, function owner or any other role |
| O46-DBP-04 | Executor direct table access | No SELECT/INSERT/UPDATE/DELETE/TRUNCATE on accounts, binding, challenge, role, permission, session, audit or unrelated tables |
| O46-DBP-05 | Executor routine allow-list | USAGE only on identity_bootstrap; EXECUTE only on claim_initial_product_owner_v1; every other function denied |
| O46-DBP-06 | Executor DDL/role attempts | CREATE schema/table/function, ALTER/DROP, CREATE/ALTER ROLE, GRANT, SET ROLE, ownership change all denied |
| O46-DBP-07 | Function owner privilege scope | NOLOGIN/no elevated attrs; exact function/schema owner; exact minimal table/column privileges; no role-management |
| O46-DBP-08 | SECURITY DEFINER review | Correct owner, safe search_path with pg_temp last, fully qualified references, no dynamic SQL, PUBLIC EXECUTE revoked |
| O46-DBP-09 | Claim atomicity | Account, Product Owner binding, challenge consumption, operation result and audit intent commit/rollback together |
| O46-DBP-10 | Concurrent claim | Exactly one claim succeeds for one InstallationId; no duplicate AccountId/binding |
| O46-DBP-11 | Challenge binding | Wrong/expired/replayed challenge, wrong SID, InstallationId, SetupOperationId or purpose is denied |
| O46-DBP-12 | Unknown commit | Same SetupOperationId reconciles exact committed result; no second owner |
| O46-DBP-13 | Already claimed state | Re-execution or a new SetupOperationId cannot create/replace/transfer/re-enroll Product Owner |
| O46-DBP-14 | Runtime unchanged | Existing runtime_app account INSERT denial remains SQLSTATE 42501 |
| O46-DBP-15 | Migrator boundary | migration_runner cannot use the bootstrap routine except through the exact approved owner/migration context; no broader role membership |
| O46-DBP-16 | Secret custody | Executor credential readable only by Setup Service SID; absent from API process, arguments, environment, source, config, logs and artifacts |
| O46-DBP-17 | Post-claim cleanup | Credential removed, DB connection terminated, setup pipe disabled, executor role set NOLOGIN or otherwise proven unusable |
| O46-DBP-18 | Reinstall/update/restore | InstallationId and owner binding persist; no new bootstrap executor or second claim is silently created |
| O46-DBP-19 | Linux/Windows parity | Exact PostgreSQL 18 privilege and function behavior proven in supported CI and Windows server deployment target |
| O46-DBP-20 | Independent review | Reviewer inspects exact final SHA, SQL function source, effective grants, role catalog and failure logs |

Every test is **NOT RUN / NOT VERIFIED**. The existing O20-G catalog PASS does not prove this proposed role/function because neither has been implemented.

## 10. Official PostgreSQL 18 references

1. CREATE FUNCTION and SECURITY DEFINER semantics/safe search_path: https://www.postgresql.org/docs/18/sql-createfunction.html
2. CREATE ROLE attributes, LOGIN/NOLOGIN, NOINHERIT, connection limit and VALID UNTIL: https://www.postgresql.org/docs/18/sql-createrole.html
3. Role membership and SET/INHERIT options: https://www.postgresql.org/docs/18/role-membership.html
4. ALTER DEFAULT PRIVILEGES and function EXECUTE defaults: https://www.postgresql.org/docs/18/sql-alterdefaultprivileges.html
5. GRANT/REVOKE role membership options: https://www.postgresql.org/docs/18/sql-grant.html

## 11. Owner decision requested

**Recommended decision:** accept a function-only, pre-activation `mdgsm_bootstrap_executor` PostgreSQL LOGIN as a temporary technical capability outside the five N.6 operational identities, paired with a narrowly privileged NOLOGIN function-owner role. It must be available only to the local Setup Service through its protected secret store and be disabled after the first-owner claim. This does not introduce a new human participant or a second owner-creation path.

If the owner does not accept an additional temporary PostgreSQL LOGIN, implementation must pause while an alternative capability is designed; do not silently reuse `runtime_app`, `migration_runner` or the database-owner credential.

## 12. Non-authorization

This document is a technical recommendation only. It creates no PostgreSQL role/login, credential, password, function, schema, grant, membership, migration, table, Account, OwnerBinding, role/permission assignment, endpoint, listener or runtime code. It does not close O46-G04, O-20 or any G1/release gate.

**END — MDG-SECURITY-O46-BOOTSTRAP-DATABASE-PRINCIPAL-DESIGN-001 v0.1**
