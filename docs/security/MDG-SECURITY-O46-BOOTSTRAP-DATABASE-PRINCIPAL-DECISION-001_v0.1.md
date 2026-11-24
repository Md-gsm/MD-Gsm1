# O-46 — Owner Decision: Bootstrap Database Principal Pair

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-BOOTSTRAP-DATABASE-PRINCIPAL-DECISION-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | **OWNER-ACCEPTED CONSTRAINT — IMPLEMENTATION / PROVISIONING NOT AUTHORIZED** |
| Decision authority | Product Owner and IdentityAccess Module Owner |
| Approval source | Explicit owner response in the project conversation: “هر دو پیشنهادت تایید” (2026-11-23) |
| Governing proposal | MDG-SECURITY-O46-BOOTSTRAP-DATABASE-PRINCIPAL-DESIGN-001 v0.1 |
| Related O-20 baseline | MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 v1.8 |
| PR / branch | #92 Draft / security/o20-o21-final-closure |

## 1. Decision recorded

The Product Owner approves **both** proposed PostgreSQL principal classifications required for the one-time initial Product Owner claim:

1. A dedicated, function-only, pre-activation PostgreSQL LOGIN named `mdgsm_bootstrap_executor`, outside the five canonical N.6 operational identities.
2. A separate, narrowly privileged PostgreSQL NOLOGIN function-owner role named `mdgsm_identity_bootstrap_fn_owner`, which owns only the isolated bootstrap schema and its versioned initial-owner claim routine.

This approval accepts the bounded architecture and classification described below. It does not authorize creation or provisioning of either role.

## 2. Accepted boundaries

### 2.1 Five N.6 identities remain unchanged

The accepted N.6 logical operational identity set remains exactly:

- `runtime_app`
- `migration_runner`
- `backup_operator`
- `reporting_ro`
- `emergency_operator`

The bootstrap executor is an additional **technical, pre-activation, one-purpose database capability**, not a sixth canonical operational identity, not an application user, not a staff member, and not a new human participant. It must nevertheless be enumerated in the complete PostgreSQL cluster/database role inventory for as long as it exists.

No privileges, memberships, credentials or role purposes of the five N.6 identities are changed by this decision.

### 2.2 Bootstrap executor — accepted constraints

`mdgsm_bootstrap_executor` must be:

- LOGIN, NOINHERIT, NOSUPERUSER, NOCREATEDB, NOCREATEROLE, NOREPLICATION and NOBYPASSRLS.
- Limited to one concurrent connection, and CONNECT only to the designated central application database.
- A member of **no** PostgreSQL role; in particular, no membership or SET ROLE path to `mdgsm_runtime`, `migration_runner`, `mdgsm_schema_owner`, the function-owner role or any other protected role.
- Owner of no schema, table, sequence, function or other database object.
- Without direct table, sequence, DDL, role-management, ownership, GRANT OPTION or general application privileges.
- Granted USAGE only on the isolated bootstrap schema and EXECUTE only on the exact versioned initial-owner claim routine. Effective access to every other routine/object must be denied.
- Authenticated with a generated strong credential or a separately reviewed client-certificate mechanism; no trust authentication, shared/default password or embedded credential.
- Usable only by the dedicated central-server Windows Setup Service. Its credential must be held in OS-protected storage accessible only to that service SID, and never exposed to the Desktop client, ordinary API process, command line, environment, config, logs, traces, telemetry, crash/support bundles or CI artifacts.

After a successful claim, the Setup Service must erase its local credential, terminate its database connection and close/disable the setup channel. A separately authenticated trusted deployment/database-owner operation must set the executor to NOLOGIN or drop it after verifying dependencies. The executor cannot disable itself or alter PostgreSQL roles.

The 30-minute one-time challenge lifetime is **not** a database LOGIN credential TTL. The exact pre-claim credential lifecycle, restart/resume handling and cleanup failure procedure remain design/test sub-gates. No arbitrary LOGIN expiry is approved by this decision.

### 2.3 Function owner — accepted constraints

`mdgsm_identity_bootstrap_fn_owner` must be:

- NOLOGIN, NOSUPERUSER, NOCREATEDB, NOCREATEROLE, NOREPLICATION and NOBYPASSRLS.
- Owner only of the isolated `identity_bootstrap` schema and the exact versioned initial-owner claim routine(s) needed for the approved operation.
- Not owner of the `identity` schema, Account, Role, Permission, Session, Recovery, Audit or business tables.
- Granted only the minimum table/column rights required by the SECURITY DEFINER routine: create the initial Account; create the immutable InstallationId-to-AccountId binding; validate/consume the single-use challenge; persist the idempotent claim receipt; and write the required IdentityAccess audit intent. Any additional object or column privilege requires explicit source/SQL justification and effective-privilege proof.
- Without role/permission catalog mutation, account-role assignment, session creation, recovery, unrelated-account access, Organization, Accounting, Persons, DDL outside its isolated schema, or role-management authority.

The routine must be SECURITY DEFINER with a pinned safe search_path (trusted schemas only and pg_temp last), fully qualified object references, no dynamic SQL, strict typed inputs, PUBLIC EXECUTE revoked, controlled default privileges, and an exact EXECUTE grant to the bootstrap executor only. RLS behavior and effective privileges must be tested rather than assumed.

### 2.4 Database claim operation remains singular

The bootstrap executor can call only the typed, versioned initial-owner claim operation described by the governing O-46 design. The operation must atomically validate InstallationId, SetupOperationId, purpose, operator SID, challenge and database-clock expiry; create exactly one initial Account and immutable owner binding; consume the challenge; record an idempotent result; and write secret-free IdentityAccess audit intent.

It must not create or assign roles/permissions, sessions, Organization records, FiscalPeriods, opening balances or business data. A same-operation retry may reconcile the exact committed result; a different operation, changed payload, replay, expired challenge or second claim must fail closed.

## 3. Explicit non-approvals and unresolved sub-gates

This approval **does not** approve or authorize any of the following:

1. Creating the roles, credential, schema, function, grants, membership edges, tables or migration.
2. Adding any membership from `migration_runner` to `mdgsm_identity_bootstrap_fn_owner`, `mdgsm_schema_owner` or another role. The existing migration_runner-to-mdgsm_schema_owner SET-only edge is unchanged.
3. Giving the bootstrap executor any membership or SET ROLE capability.
4. Using runtime, migration, schema-owner, database-owner or superuser credentials from the Wizard.
5. Reserving or consuming Migration 0009. It remains reserved to Accounting; 0010+ remains unallocated. WG-0 must make and record a separate reservation before any bootstrap SQL is written.
6. Choosing the exact DDL installation/upgrade path for the SECURITY DEFINER routine. The design must use a separately authenticated trusted deployment/database-owner boundary without silently broadening the migration_runner membership graph. That exact principal, ownership transfer/install sequence, change-control and rollback/recovery procedure remain open for technical design and review.
7. Creating the actual Windows service, secret-store entry, endpoint/listener, runtime code or Product Owner binding.
8. Enabling login, Recovery, role/permission grants, Organization setup, Accounting setup, G1, production or release.

No additional human participant is authorized. The technical principals above are non-human database security objects; the existing owner and independent reviewer remain the only human project participants.

## 4. Required proof before O46-G04 can close

All items below remain **NOT RUN / NOT VERIFIED** until evidence is produced against the exact final SHA:

- Complete role catalog and membership graph show the five N.6 identities unchanged, the executor with zero memberships, and the function owner NOLOGIN.
- Positive/negative effective-privilege proofs for database CONNECT, schema USAGE, exact routine EXECUTE, all other routine access, direct table/sequence access, DDL, GRANT and SET ROLE.
- Exact function-owner schema/object ownership and column-level grants; no ownership of IdentityAccess or business tables.
- SECURITY DEFINER owner, safe search_path, PUBLIC/default ACL controls, fully qualified references, RLS behavior and function-definition hash.
- Atomicity, concurrency, challenge binding, replay/expiry, unknown-commit reconciliation and immutable one-owner behavior.
- Setup Service SID secret custody, API/client/process isolation, secret-sentinel evidence and post-claim NOLOGIN/drop cleanup.
- Reinstall, Repair, update, restore and interrupted-setup continuity using the same InstallationId and AccountId.
- Linux/Windows PostgreSQL 18 parity where supported, plus a Windows central-server acceptance run.
- Independent Security/Architecture review of the exact final SQL, grants, role catalog, tests and cleanup evidence.

The proof catalogue O46-DBP-01 through O46-DBP-20 in the governing design remains the required baseline; none is marked PASS by this owner decision.

## 5. Status and authority

- Bootstrap principal **architecture classification**: OWNER-ACCEPTED within the boundaries above.
- O46-G04: **OPEN** — schema, migration, deployment-principal, role/ACL, secret-custody and test evidence remain outstanding.
- O-20: **OPEN** — this decision changes no existing O20-G evidence and does not complete O-20.
- IdentityAccess G1 / production activation: **NOT GRANTED**.
- PR #92: remains Draft, open and unmerged pending exact-head CI, independent review and all other gates.

This is a binding owner decision on the bounded design exception. It is not an implementation, migration, production-access or release authorization.

**END — MDG-SECURITY-O46-BOOTSTRAP-DATABASE-PRINCIPAL-DECISION-001 v0.1**
