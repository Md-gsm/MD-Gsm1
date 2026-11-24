# O-46 — Owner Decision: Bootstrap Database Principal Pair

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-BOOTSTRAP-DATABASE-PRINCIPAL-DECISION-001 |
| Version / Date | 0.2 / 2026-11-23 |
| Status | **OWNER-ACCEPTED CONSTRAINT — TWO EXACT ROUTINE CAPABILITIES; IMPLEMENTATION / PROVISIONING NOT AUTHORIZED** |
| Decision authority | Product Owner and IdentityAccess Module Owner |
| Approval source | Principal-pair approval plus explicit two-routine extension in PR #92 comment 7110645790 |
| Governing proposal | MDG-SECURITY-O46-BOOTSTRAP-DATABASE-PRINCIPAL-DESIGN-001 v0.1, reconciled with Challenge Issuance Protocol Proposal v0.2 |
| Related O-20 baseline | MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 v1.8 |
| PR / branch | #92 Draft / security/o20-o21-final-closure |
| Supersedes | MDG-SECURITY-O46-BOOTSTRAP-DATABASE-PRINCIPAL-DECISION-001 v0.1 |

## 1. Decision recorded

The Product Owner approves the same two PostgreSQL principal classifications required for the one-time initial Product Owner bootstrap:

1. A dedicated, pre-activation PostgreSQL LOGIN named `mdgsm_bootstrap_executor`, outside the five canonical N.6 operational identities.
2. A separate, narrowly privileged PostgreSQL NOLOGIN function-owner role named `mdgsm_identity_bootstrap_fn_owner`, which owns only the isolated bootstrap schema and exactly two approved versioned bootstrap routines.

The Product Owner has additionally approved exactly two fixed-signature SECURITY DEFINER capabilities, both executable only by the existing bootstrap executor:

- `identity_bootstrap.issue_initial_owner_challenge_v1(...)`
- `identity_bootstrap.claim_initial_product_owner_v1(...)`

The extension is recorded in PR #92 comment 7110645790. This approval accepts the bounded architecture and classifications only. It does not authorize creation or provisioning of either role, function, grant, credential or database object.

## 2. Accepted boundaries

### 2.1 Five N.6 identities remain unchanged

The accepted N.6 logical operational identity set remains exactly:

- `runtime_app`
- `migration_runner`
- `backup_operator`
- `reporting_ro`
- `emergency_operator`

The bootstrap executor is an additional **technical, pre-activation, two-capability database identity**, not a sixth canonical operational identity, not an application user, not a staff member and not a human participant. It must be enumerated in the complete PostgreSQL cluster/database role inventory for as long as it exists.

No privileges, memberships, credentials or role purposes of the five N.6 identities are changed by this decision.

### 2.2 Bootstrap executor — accepted constraints

`mdgsm_bootstrap_executor` must be:

- LOGIN, NOINHERIT, NOSUPERUSER, NOCREATEDB, NOCREATEROLE, NOREPLICATION and NOBYPASSRLS.
- Limited to one concurrent connection, and CONNECT only to the designated central application database.
- A member of **no** PostgreSQL role; in particular, no membership or SET ROLE path to `mdgsm_runtime`, `migration_runner`, `mdgsm_schema_owner`, the function-owner role or any other protected role.
- Owner of no schema, table, sequence, function or other database object.
- Without direct table, sequence, DDL, role-management, ownership, GRANT OPTION or general application privileges.
- Granted USAGE only on the isolated bootstrap schema and EXECUTE only on the two exact approved function signatures above. Effective access to every other routine/object must be denied.
- Authenticated with a generated strong credential or a separately reviewed client-certificate mechanism; no trust authentication, shared/default password or embedded credential.
- Usable only by the dedicated central-server Windows Setup Service. Its credential must be held in OS-protected storage accessible only to that service SID, and never exposed to Desktop client, ordinary API process, command line, environment, config, logs, traces, telemetry, crash/support bundles or CI artifacts.

After a successful claim, the Setup Service must erase its local credential, terminate its database connection and close/disable the setup channel. A separately authenticated trusted deployment/database-owner operation must set the executor to NOLOGIN or drop it after verifying dependencies. The executor cannot disable itself or alter PostgreSQL roles.

The 30-minute one-time challenge lifetime is **not** a database LOGIN credential TTL. The exact pre-claim credential lifecycle, restart/resume handling and cleanup failure procedure remain design/test sub-gates. No arbitrary LOGIN expiry is approved by this decision.

### 2.3 Function owner — accepted constraints

`mdgsm_identity_bootstrap_fn_owner` must be:

- NOLOGIN, NOSUPERUSER, NOCREATEDB, NOCREATEROLE, NOREPLICATION and NOBYPASSRLS.
- Owner only of the isolated `identity_bootstrap` schema and the two exact versioned bootstrap routines.
- Not owner of the `identity` schema, Account, Role, Permission, Session, Recovery, Audit or business tables.
- Granted only the minimum table/column rights required by the two fixed routine bodies: the issuer may read the exact challenge/idempotency columns, INSERT only the approved challenge columns and UPDATE only `revoked_at_utc` to revoke a prior unconsumed challenge; the claim routine may perform the previously proposed exact Account INSERT, immutable binding INSERT, challenge verification and conditional `consumed_at_utc` update, append-only claim receipt and required IdentityAccess audit intent. No extra object or column privilege is approved without a new Product Owner decision.
- Without role/permission catalog mutation, account-role assignment, session creation, Recovery, unrelated-account access, Organization, Accounting, Persons, DDL outside its isolated schema or role-management authority.

Both routines must be SECURITY DEFINER with pinned safe `search_path` (trusted schemas only and `pg_temp` last), fully qualified object references, no dynamic SQL, strict typed inputs, PUBLIC EXECUTE revoked and controlled default privileges. Only the executor receives EXECUTE on the two exact signatures. RLS behavior and effective privileges must be tested rather than assumed.

**Shared-role privilege caveat and residual trust boundary:** PostgreSQL table/column ACLs attach to the shared function-owner role, not separately to each function. Consequently the approved table grants are aggregate across the two routine bodies. The executor's EXECUTE ACL is per exact function signature, but it does not itself create per-function table grants. PostgreSQL ACLs alone cannot independently sandbox the issuer's table capabilities from those also needed by the claim routine. Their side-effect split therefore depends on fixed, fully reviewed, non-dynamic function bodies plus the signed/versioned maintenance package's source and definition fingerprints. The issuer must not create Account, binding, receipt or audit state; the claim routine must not issue or rotate challenges. Adversarial PostgreSQL 18 testing and explicit independent Security/Architecture risk acceptance are mandatory. If DB-enforced privilege isolation per function is required, this design must return to Product Owner decision; no additional role is approved by the current decision.

### 2.4 Two database capabilities — fixed and non-dispatchable

The issuer accepts only the typed InstallationId, SetupOperationId, OS-attested operator SID, 32-byte challenge digest and challenge_request_id inputs. Purpose and maximum 30-minute lifetime are routine-controlled. It persists verifier-only challenge state and may conditionally revoke a prior unconsumed challenge for the same setup operation. It cannot create accounts, owner bindings, claim receipts, audit events, roles, permissions, sessions, Recovery or business data.

The claim operation validates InstallationId, SetupOperationId, fixed purpose, operator SID, digest and database-clock expiry; enforces single-use consumption; creates exactly one initial Account and immutable owner binding; writes the append-only idempotent claim receipt and secret-free audit intent; and commits those effects atomically.

The routines must use one shared InstallationId-scoped serialization protocol so issuance cannot race a successful claim. Exact signatures, identifier-generation details, lock primitive/key derivation and PostgreSQL 18 race proof remain open technical design gates. No generic `operation`/phase dispatcher, dynamic SQL or caller-selected object name is permitted.

The system must not create or assign roles/permissions, sessions, Organization records, FiscalPeriods, opening balances or business data in either routine. A same-operation retry may reconcile the exact committed result; a different operation, changed payload, replay, expired challenge or second claim must fail closed.

## 3. Explicit non-approvals and unresolved sub-gates

This approval **does not** approve or authorize:

1. Creating the roles, credential, schema, either function, grants, membership edges, tables or migration.
2. Adding any membership from `migration_runner` to `mdgsm_identity_bootstrap_fn_owner`, `mdgsm_schema_owner` or another role. The existing migration_runner-to-mdgsm_schema_owner SET-only edge is unchanged.
3. Giving the bootstrap executor any membership or SET ROLE capability.
4. Using runtime, migration, schema-owner, database-owner or superuser credentials from the Wizard.
5. Reserving or consuming Migration 0009. It remains reserved to Accounting; candidate 0010 remains unreserved until WG-0 makes and records a separate reservation.
6. Enabling executor LOGIN, Setup Service DB credential custody or the claim path. The local cluster-administrator provisioning boundary is owner-approved as a design constraint in the DB provisioning decision v0.2, but implementation and activation remain separately gated.
7. Creating the actual Windows service, secret-store entry, endpoint/listener, runtime code or Product Owner binding.
8. Enabling login, Recovery, role/permission grants, Organization setup, Accounting setup, G1, production or release.

No additional human participant is authorized. The technical principals above are non-human database security objects; the existing owner and independent reviewer remain the only human project participants.

## 4. Required proof before O46-G04 can close

All items below remain **NOT RUN / NOT VERIFIED** until evidence is produced against the exact final SHA:

- Complete role catalog and membership graph show the five N.6 identities unchanged, the executor with zero memberships, and the function owner NOLOGIN.
- Positive/negative effective-privilege proofs for database CONNECT, schema USAGE, EXECUTE on exactly the two routines, denial of every other routine, direct table/sequence access, DDL, GRANT and SET ROLE.
- Exact function-owner schema/object ownership and column-level grants; no ownership of IdentityAccess or business tables.
- Both SECURITY DEFINER owners, safe search_path, PUBLIC/default ACL controls, fully qualified references, RLS behavior and separate function-definition hashes.
- Adversarial proof of issuer/claim side-effect separation despite the aggregate function-owner ACL.
- Atomicity, concurrency, challenge binding, replay/expiry, issue-vs-claim race, unknown-commit reconciliation and immutable one-owner behavior.
- Setup Service SID secret custody, API/client/process isolation, secret-sentinel evidence and post-claim NOLOGIN/drop cleanup.
- Reinstall, Repair, update, restore and interrupted-setup continuity using the same InstallationId and AccountId.
- PostgreSQL 18 disposable catalog/integration proof plus Windows central-server acceptance.
- Independent Security/Architecture review of the exact final SQL, grants, role catalog, tests and cleanup evidence.

The proof catalogue O46-DBP-01 through O46-DBP-20 in the governing design remains the required baseline; none is marked PASS by this owner decision.

## 5. Status and authority

- Bootstrap principal **architecture classification**: OWNER-ACCEPTED within the boundaries above.
- Two fixed routine capabilities: **OWNER-APPROVED CONSTRAINT — DESIGN ONLY**.
- O46-G04: **OPEN** — schema, migration, role/ACL, credential custody, routine implementation and test evidence remain outstanding.
- O-20: **OPEN** — this decision changes no existing O20-G evidence and does not complete O-20.
- IdentityAccess G1 / production activation: **NOT GRANTED**.
- PR #92: remains Draft, open and unmerged pending exact-head CI, independent review and all other gates.

This is a binding owner decision on the bounded design exception. It is not an implementation, migration, production-access or release authorization.

**END — MDG-SECURITY-O46-BOOTSTRAP-DATABASE-PRINCIPAL-DECISION-001 v0.2**
