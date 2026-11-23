# O-46 Bootstrap Database Provisioning and Function Deployment Path — Proposal

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-BOOTSTRAP-DB-PROVISIONING-PATH-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | **TECHNICAL RECOMMENDATION — OWNER DECISION REQUIRED — NON-AUTHORIZING** |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Owner-accepted principal baseline | O-46 Bootstrap Database Principal Decision v0.1 |
| Role catalog baseline | O-20 Database Role/Object Matrix v1.8 |
| Related implementation boundary | O-20-C migration_runner → mdgsm_schema_owner SET-only mapping |

## 1. Decision sought

This proposal selects the deployment mechanism for installing and upgrading the isolated IdentityAccess bootstrap function without:

- adding a third PostgreSQL LOGIN principal;
- adding any membership edge to `migration_runner`;
- giving `mdgsm_bootstrap_executor` DDL, ownership or role-management rights;
- giving the Setup Wizard, Setup Service or ordinary API access to a database-owner / cluster-administrator credential; or
- creating another owner-enrollment path.

**Recommendation:** use the already-established PostgreSQL cluster-administrator root of trust, invoked only through a separate, local, explicitly authorized maintenance/provisioning operation on the central server. The existing cluster-administrator credential is not stored in the application, not used by DbMigrator, and is never made available to the Setup Wizard or Setup Service. No new database LOGIN is introduced for deployment.

The operation temporarily executes `SET LOCAL ROLE mdgsm_identity_bootstrap_fn_owner` while connected as the existing cluster superuser. PostgreSQL allows a superuser to select any role with `SET ROLE`; this creates no persistent role-membership edge. The five N.6 identities and the current migration membership graph therefore remain unchanged.

This proposal does not authorize the operation, roles, schema, function, grants, secret, migration, or runtime feature. It requests explicit owner acceptance of the deployment boundary described here.

## 2. Verified repository constraints

The current branch establishes these relevant constraints:

1. `tools/MDGsm.DbMigrator/Program.cs` accepts a dedicated migration LOGIN and a NOLOGIN schema-owner role, then delegates to the migration runner. The runner verifies the authenticated identity and exact SET-only membership before DDL.
2. The current migration boundary is exactly `migration_runner` → `mdgsm_schema_owner`, with `INHERIT FALSE`, `ADMIN FALSE`, `SET TRUE`. No second membership is accepted.
3. `identityaccess-runtime-role-grants.sql` deliberately withholds Account INSERT, role/permission writes, DDL, schema CREATE and ownership from the ordinary runtime role.
4. The accepted bootstrap executor has no memberships and may execute only the one typed initial-owner claim routine. It is not a deployment identity.
5. The accepted function-owner role is NOLOGIN and owns only the isolated `identity_bootstrap` schema and its approved routine(s). It is not an application or migration identity.
6. Existing deployment documentation already separates migration execution from DBA/deployment-controlled runtime grants. It does not define a separate deployment LOGIN for this bootstrap routine.

No existing source contract authorizes an additional deployment LOGIN or any expansion of the migrator's SET ROLE path.

## 3. Why the existing cluster-administrator boundary is required

A PostgreSQL role with only database ownership is not automatically sufficient to create cluster-wide roles. PostgreSQL roles are cluster-wide, and `CREATE ROLE` requires `CREATEROLE` or superuser authority. In this proposal, the existing cluster superuser is used because the accepted design forbids granting `CREATEROLE` or a membership path to the application migration identity.

The trusted deployment actor must therefore be an already-existing cluster superuser controlled by the central-server deployment boundary. The deployment process must verify its effective authenticated identity and stop if it is merely a database owner without the required authority. It must not create a new superuser, grant `CREATEROLE` to an application role, or quietly substitute the schema owner.

The PostgreSQL cluster superuser is an explicitly acknowledged root of trust, like the host OS Administrator/SYSTEM boundary. This proposal narrows when and where it is used; it does not claim to protect against compromise of that root.

## 4. Separation of the two execution planes

| Plane | Allowed actor | Allowed work | Explicitly forbidden |
|---|---|---|---|
| Application setup | Local Setup Service SID, reached through the accepted local Windows Named Pipe | Validate local Wizard request; obtain final owner password through IdentityAccess; call the one bootstrap claim routine using the executor credential | DDL, role management, schema changes, migration execution, reading cluster-admin secret |
| Ordinary API/runtime | Existing runtime LOGIN and `mdgsm_runtime` ACL role | Normal, separately approved application operations | Any bootstrap provisioning, initial Owner claim outside its designated routine, DDL or deployment |
| Schema migration | Existing `migration_runner` → `mdgsm_schema_owner` SET-only path | Apply WG-0-reserved, reviewed schema migrations through DbMigrator | SET ROLE to function-owner; create/replace bootstrap SECURITY DEFINER function; role administration |
| Bootstrap maintenance | Existing central-server cluster-administrator root, through a separately invoked local maintenance operation | Provision/verify the two accepted roles; create/replace the isolated function under its NOLOGIN owner; enable/disable executor; record deployment evidence | Owner account creation, invoking the Wizard claim on behalf of the user, ordinary application traffic, credential sharing with setup/runtime |

The bootstrap maintenance operation is not an application service and is not exposed as an API, named pipe, TCP listener, scheduled background service or remote administration endpoint. It is an explicit, local deployment/maintenance action. It does not create a new human project participant; it uses the existing owner/administrator boundary.

## 5. Initial provisioning sequence

The following sequence is recommended. Each step is fail-closed and independently verifiable.

### 5.1 Preflight

The local maintenance package must be versioned, reviewed and integrity-checked before execution. It must:

- run only on the designated central server host;
- require an explicitly invoked administrative maintenance session;
- connect through the approved local DBA administration channel, never through the application API;
- verify that the authenticated `session_user` is the pre-existing cluster superuser and that `current_user` has not been altered unexpectedly;
- verify PostgreSQL major version 18, the exact target database, the installation/deployment identity, and the expected migration state;
- verify that there is no pre-existing role, schema or routine collision with unexpected attributes/ownership; a mismatch must abort rather than silently repair or overwrite;
- verify that the required backup/restore and change-control preconditions are satisfied; and
- write no secret, password, password verifier, connection string or private key to console output, command history, environment variables, process arguments, repository files, logs or evidence artifacts.

The maintenance package must not accept arbitrary database names, role names, schema names, function signatures or SQL from a caller. Its inputs are a small typed allow-list of the approved central installation and the approved package version.

### 5.2 Create dormant principals

In one transaction, the cluster administrator creates only the two owner-approved principals:

- `mdgsm_identity_bootstrap_fn_owner`: NOLOGIN, NOSUPERUSER, NOCREATEDB, NOCREATEROLE, NOREPLICATION, NOBYPASSRLS.
- `mdgsm_bootstrap_executor`: initially **NOLOGIN** during provisioning; it later receives LOGIN only after its authentication method, local-only connection rules and Setup Service secret custody are proven.

No role membership is created. In particular, no membership is granted to or from `migration_runner`, `mdgsm_schema_owner`, `mdgsm_runtime`, or the bootstrap executor. The two roles have no ADMIN, INHERIT or SET membership edges.

If either role already exists, the operation must compare every required attribute and the complete membership/ownership graph against the expected catalog. Any unexpected difference is a hard stop; no automatic ALTER/REPAIR or takeover is allowed.

### 5.3 Create the isolated function schema and defaults

Within the same controlled provisioning transaction, the cluster superuser executes `SET LOCAL ROLE mdgsm_identity_bootstrap_fn_owner` and creates the isolated `identity_bootstrap` schema. The schema is owned by the function-owner role. The function-owner then establishes secure default privileges for future functions in that schema so EXECUTE is not accidentally granted to PUBLIC.

The operation explicitly revokes PUBLIC privileges on the schema. The bootstrap executor receives no CREATE privilege on the schema.

No IdentityAccess Account, challenge, binding, claim result or business data is created in this step. No function is exposed to the executor until the routine has been installed and positively verified.

### 5.4 Apply IdentityAccess table/column grants only after schema prerequisites

The future IdentityAccess-owned schema migration must be WG-0 reserved before its SQL is written. That migration is responsible for the durable IdentityAccess tables/constraints and for granting the function-owner only the exact object/column privileges the reviewed function requires.

The migration runs through the existing DbMigrator path and existing SET-only `migration_runner` → `mdgsm_schema_owner` mapping. The migration may grant object privileges to `mdgsm_identity_bootstrap_fn_owner` as the relevant table owner; it must not grant role membership or object ownership to that role.

The privilege list must be mechanically derived from the final routine implementation, not copied from a conceptual list. It must be independently tested from PostgreSQL effective-privilege catalogs. No grant is authorized by this proposal, and Migration 0009 remains reserved for Accounting. A new sequence number must be allocated by WG-0 before any migration file is created; 0010+ remains unallocated until that process completes.

### 5.5 Install or update the SECURITY DEFINER routine

After the reserved schema migration is applied and verified, the same separately invoked local cluster-administrator maintenance process installs the versioned routine package:

1. Verify the expected table names, column definitions, constraints, grants and current migration history.
2. Start a transaction.
3. Execute `SET LOCAL ROLE mdgsm_identity_bootstrap_fn_owner`. Because the authenticated maintenance session is the pre-existing cluster superuser, this is a session-scoped privilege switch, not a GRANT or membership edge.
4. Create or replace only the exact, versioned, typed claim routine in `identity_bootstrap`.
5. Pin its safe `search_path` to trusted schemas only, placing `pg_temp` last; fully qualify all application objects; use no dynamic SQL, caller-selected object names, generic SQL dispatcher or arbitrary function execution.
6. Revoke EXECUTE from PUBLIC in the same transaction. Apply the explicit, exact EXECUTE grant to `mdgsm_bootstrap_executor` only after the routine definition and owner are verified. Grant the executor USAGE on the isolated schema, but no CREATE.
7. Record the routine release version and source-package SHA-256 in a non-secret PostgreSQL function comment and in the external deployment attestation. Capture the database-side function definition/owner/ACL fingerprint for independent comparison.
8. Verify the effective privilege matrix and object ownership from PostgreSQL catalogs before commit.

The transaction must never expose a new SECURITY DEFINER routine with PUBLIC EXECUTE. If any verification fails, the entire installation/update transaction rolls back. For a first installation, the executor remains NOLOGIN until the secret and local connection controls are ready.

Because the deployment session uses the root-of-trust superuser, it can run `SET ROLE` without adding any persistent role-membership row. It must reset role state and close the privileged session immediately after the approved operation. The migration runner and Setup Service never receive this capability.

### 5.6 Enable the executor only after the local secret boundary is ready

The executor remains NOLOGIN until all of the following have been proven:

- a strong generated authentication secret or separately approved client certificate is ready;
- the authentication material is held in OS-protected storage accessible only to the dedicated Setup Service SID;
- the ordinary API, Desktop clients, user sessions and unrelated Windows service identities cannot read it;
- PostgreSQL connection rules allow the executor only from the central host's loopback interface to the designated database and reject all other database/host combinations;
- the rule ordering has been checked against the complete effective `pg_hba.conf` file set, so an earlier broad rule cannot bypass the restriction;
- the target database grants the exact required CONNECT/USAGE/EXECUTE access, and the executor has no direct table/sequence privileges, no memberships and no object ownership;
- a negative connection test from another host and to another database has been rejected; and
- the application runtime process cannot read the secret or open the local Setup Pipe.

Only then may the cluster administrator set LOGIN for `mdgsm_bootstrap_executor`. The recommended password-based mode is SCRAM-SHA-256 with a CSPRNG-generated secret, set through a secure password-entry/API path that does not expose plaintext in command arguments, environment variables, SQL source or logs. The exact secret provisioning technology and the final PostgreSQL authentication rule must be separately tested before activation; this proposal does not preselect DPAPI, Windows Credential Manager, CNG or a certificate authority.

The executor's LOGIN availability is bounded to the initial claim lifecycle, but the exact pre-claim credential expiry and interrupted-setup resume semantics remain open. The 30-minute challenge lifetime is not reused as a password TTL.

## 6. Post-claim revocation and cleanup

The initial-owner claim routine itself must not receive CREATEROLE, ADMIN, role-membership or role-alteration capability. It cannot disable its own LOGIN.

After the claim commits, the setup flow is not considered fully closed until the following separately authorized maintenance cleanup is complete:

1. The trusted cluster-administrator maintenance operation executes `ALTER ROLE mdgsm_bootstrap_executor NOLOGIN`.
2. It terminates every remaining backend authenticated as `mdgsm_bootstrap_executor` and verifies none remain.
3. It verifies that the claim receipt for the exact InstallationId/SetupOperationId is durable, that the immutable Owner binding exists, and that no conflicting or second claim exists.
4. The Setup Service erases its local executor credential, closes the setup pipe and disables its bootstrap client path.
5. The operator verifies the role is NOLOGIN, has zero memberships, owns no objects, and cannot connect or execute the function; the secret-sentinel check confirms no credential leak.

If any step fails, the Wizard does not expose a second claim path or enable ordinary application features. It closes the local pipe and remains in a recoverable, fail-closed setup state. The same InstallationId and SetupOperationId are used for reconciliation; the original owner is not recreated. Cleanup must be safely retryable.

The database administrator identity used for this cleanup remains the existing deployment root of trust. No new persistent cleanup LOGIN, privileged background service or elevated permission is created by this proposal.

## 7. Routine upgrade, rollback and recovery

- Routine updates are released as a new, reviewed, versioned package with SHA-256 and commit provenance.
- Only the same local cluster-administrator maintenance path may create/replace the function. The Setup Wizard, Setup Service, executor, runtime and migration_runner cannot install or alter it.
- Each update requires a preflight that checks the database schema version and exact expected owner/ACL. A mismatch is a stop condition.
- Function replacement, owner check, PUBLIC revoke, exact executor grant, version/hash comment and post-install catalog verification occur in one transaction.
- Security-function releases are forward-only. Rollback means deploying a previously reviewed compatible function package through the same maintenance boundary, not granting routine DDL to the application roles.
- Any table/column shape change remains an IdentityAccess-owned WG-0-reserved migration. The routine package and schema migration must declare compatible versions, and deployment order is explicit.
- Restore must preserve InstallationId, original AccountId/Product Owner binding, role state and the installed function version. A restored state mismatch is fail-closed; it must never trigger a second bootstrap.
- The package and deployment attestation contain no database password, executor secret, owner password, password hash, private key or raw challenge.

## 8. Rejected alternatives

| Alternative | Disposition | Reason |
|---|---|---|
| Add SET-only membership from migration_runner to function-owner | REJECTED | Explicitly outside the owner-approved membership graph; couples broad migration authority to an unrelated security function owner |
| Grant CREATEROLE/SUPERUSER to migration_runner or mdgsm_schema_owner | REJECTED | Breaks the current least-privilege deployment boundary |
| Use mdgsm_bootstrap_executor to install/replace its own function | REJECTED | Would require DDL/function-owner/role capabilities and turn the single-purpose claim credential into a deployment credential |
| Give Setup Wizard, Setup Service or API the cluster-admin/database-owner credential | REJECTED | Crosses the accepted trust boundary and exposes broad database authority to application processes |
| Create a third permanent deployment LOGIN | NOT RECOMMENDED | Adds a database principal/secret/lifecycle when a cluster-administrator root already exists; would require a new owner decision and complete O-20 reconciliation |
| Use a temporary superuser/CREATEROLE application role | REJECTED | A newly created high-trust database principal is unnecessary and would be difficult to constrain safely |

## 9. Required acceptance tests — NOT RUN

| ID | Test | Expected result |
|---|---|---|
| O46-DEP-01 | Maintenance actor preflight | Only the pre-existing approved cluster superuser is accepted; database-owner-only, migration and runtime sessions are rejected |
| O46-DEP-02 | Principal provisioning | Exactly the two accepted roles are created with exact flags; no third LOGIN or superuser is created |
| O46-DEP-03 | Membership graph | Five N.6 identities unchanged; migration_runner has exactly its existing SET-only schema-owner edge; executor and function-owner have zero memberships |
| O46-DEP-04 | Schema ownership | identity_bootstrap schema and routine owned by function-owner; no other ownership added |
| O46-DEP-05 | Superuser SET ROLE | SET LOCAL ROLE function-owner succeeds only in maintenance session; no pg_auth_members edge is added |
| O46-DEP-06 | Routine DDL isolation | Runtime, Setup Service, executor and migration_runner cannot CREATE/ALTER/DROP the routine or schema |
| O46-DEP-07 | Function security | SECURITY DEFINER owner/search_path/fully-qualified references and RLS behavior match the reviewed package |
| O46-DEP-08 | PUBLIC/default ACL | No PUBLIC EXECUTE window; future routines in schema are not executable by PUBLIC by default |
| O46-DEP-09 | Executor allow-list | CONNECT is limited to the target database/loopback path; schema USAGE and exact function EXECUTE only |
| O46-DEP-10 | Negative connection | Non-loopback source and every non-target database reject executor login even where PUBLIC has CONNECT |
| O46-DEP-11 | Secret custody | Only Setup Service SID can read secret; no argument/env/config/log/telemetry/artifact leak |
| O46-DEP-12 | Post-claim cleanup | Executor NOLOGIN, all existing sessions terminated, no remaining backend, credential erased, pipe closed |
| O46-DEP-13 | Cleanup failure | Setup remains fail-closed; same SetupOperationId can reconcile; no second claim or account |
| O46-DEP-14 | Routine upgrade | Only cluster-admin maintenance can replace function; hash/owner/ACL verified atomically |
| O46-DEP-15 | Restore continuity | InstallationId, AccountId, binding and function release evidence survive restore; mismatch fails closed |
| O46-DEP-16 | Independent review | Exact package, deployment operation, catalog evidence, negative tests and cleanup evidence independently reviewed |

Every test is **NOT RUN / NOT VERIFIED**. The present O20-G role-catalog result does not cover these new objects or operations.

## 10. Open decisions and implementation gates

The following remain open even if the owner accepts the recommended deployment actor/path:

1. Final local authentication mechanism for the cluster-administrator maintenance session.
2. Final secure handoff and protection mechanism for the SCRAM secret (or separately approved client certificate) accessible only to Setup Service SID.
3. Exact Windows service/process boundary and administrative invocation UX for the maintenance package.
4. Exact IdentityAccess schema and table/column grant set, owned by IdentityAccess and reviewed against the final function body.
5. WG-0 allocation of a new migration sequence number; 0009 is reserved for Accounting and cannot be used.
6. The function package's release/signature verification, external deployment attestation schema and hash-verification implementation.
7. PostgreSQL 18 disposable tests and Windows central-server tests for all O46-DEP cases.
8. Independent Security/Architecture review of one exact final SHA.

No code, SQL, DDL, role, grant, membership, credential, schema, function, migration or runtime activation is authorized by this proposal.

## 11. PostgreSQL 18 references

1. CREATE ROLE: cluster-wide role creation requires CREATEROLE or superuser; role attributes and LOGIN/NOLOGIN: https://www.postgresql.org/docs/18/sql-createrole.html
2. SET ROLE: a superuser may select any role; membership SET options are required for non-superusers: https://www.postgresql.org/docs/18/sql-set-role.html
3. Role membership: SET and INHERIT are separate membership capabilities: https://www.postgresql.org/docs/18/role-membership.html
4. CREATE FUNCTION: SECURITY DEFINER executes with the function owner's privileges; secure search_path and PUBLIC EXECUTE revocation are required safeguards: https://www.postgresql.org/docs/18/sql-createfunction.html
5. ALTER FUNCTION: owner changes and routine properties: https://www.postgresql.org/docs/18/sql-alterfunction.html
6. ALTER ROLE: PostgreSQL warns that cleartext password handling may leak through command history/server logs and documents `psql \\password`: https://www.postgresql.org/docs/18/sql-alterrole.html

## 12. Owner decision requested

**Recommended decision:** accept use of the already-existing PostgreSQL cluster-superuser root of trust, only through the separate local maintenance/provisioning operation described in this proposal, for initial creation of the accepted two roles, isolated schema/function installation and update, and post-claim executor disable/termination. No new deployment LOGIN is created; no credential is shared with Wizard/API/Setup Service; no membership or privilege is added to migration_runner.

If this boundary is not accepted, the bootstrap routine installation path must remain unimplemented. Do not substitute a new deployment LOGIN, CREATEROLE grant, migration_runner membership, or application-held database-owner secret without a separate explicit decision.

**END — MDG-SECURITY-O46-BOOTSTRAP-DB-PROVISIONING-PATH-001 v0.1**
