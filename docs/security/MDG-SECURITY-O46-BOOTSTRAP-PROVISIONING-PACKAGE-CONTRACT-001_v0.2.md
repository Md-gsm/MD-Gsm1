# O-46 Bootstrap Provisioning Package and Maintenance Operation Contract

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-BOOTSTRAP-PROVISIONING-PACKAGE-CONTRACT-001 |
| Version / Date | 0.2 / 2026-11-23 |
| Status | **OWNER-ACCEPTED MAINTENANCE BOUNDARY + TWO-ROUTINE DESIGN; IMPLEMENTATION NOT AUTHORIZED** |
| Owner | Product Owner / IdentityAccess Module Owner |
| Related decision | MDG-SECURITY-O46-BOOTSTRAP-DB-PROVISIONING-DECISION-001 v0.2 |
| Related principal decision | MDG-SECURITY-O46-BOOTSTRAP-DATABASE-PRINCIPAL-DECISION-001 v0.2 |
| Related design | MDG-SECURITY-O46-BOOTSTRAP-DB-PROVISIONING-PATH-001 v0.2 |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Supersedes | MDG-SECURITY-O46-BOOTSTRAP-PROVISIONING-PACKAGE-CONTRACT-001 v0.1 |

## 1. Purpose and scope

This contract specifies the future local maintenance package used only by the existing central-server deployment trust boundary to provision and verify the owner-approved PostgreSQL bootstrap principals, install or upgrade the exact pair of isolated IdentityAccess bootstrap routines, and independently revoke the temporary executor capability.

It is a design contract, not executable SQL, a migration, an installer, or authorization to create database objects. It does not create an additional application process, Windows service, database LOGIN, human participant, owner-enrollment route or migration path.

The approved operator uses the already-existing PostgreSQL cluster-administrator root of trust in a separately invoked local maintenance session. The cluster credential is never delivered to the Setup Wizard, Setup Service, API, Desktop client, DbMigrator or bootstrap executor.

## 2. Non-negotiable invariants

1. Only the designated central Windows server; interactive, explicit local invocation. No remote execution, listener, API, Named Pipe, scheduled task, service, background worker or application-triggered elevation.
2. Only the pre-existing approved cluster superuser. A database owner, `migration_runner`, `mdgsm_schema_owner`, `mdgsm_runtime`, executor or ordinary user is rejected.
3. No third persistent deployment LOGIN, no `CREATEROLE` or `SUPERUSER` grant to an application identity, and no new membership edge. The existing five N.6 operational identities and sole `migration_runner → mdgsm_schema_owner` SET-only membership remain unchanged.
4. No secret in source, package, repository, installer, process arguments, environment variables, shell history, logs, telemetry or evidence. The cluster-admin secret is entered/obtained only through the already-approved local administrative credential channel and is never persisted by this package.
5. Package operations are typed, fixed and allow-listed. No arbitrary SQL input, dynamic identifiers, free-form command, generic SQL console or caller-selected database/object names.
6. The package must be integrity- and authenticity-verified before any privileged connection is opened. Signature or trust failure is a hard stop.
7. Every database-changing operation is transactional where PostgreSQL supports transactional DDL; non-transactional side effects are explicitly separated, independently reconciled and idempotent.
8. Any unexpected existing object, owner, ACL, role flag, membership, migration version, package version or InstallationId is a hard stop. No automatic takeover, repair, privilege broadening or destructive cleanup.
9. The package cannot create an Account, claim an Owner, issue an Owner credential, invoke the Wizard claim routine, or bypass setup gates.
10. No operation enables business runtime, IdentityAccess recovery, G1, Organization, Inventory, O-20/O-21 or production activation.

## 3. Package format and trust

### 3.1 Release bundle

Each release is an immutable bundle containing:
- a typed package manifest;
- a versioned, fixed-operation maintenance executable and its exact dependencies;
- fixed SQL resources for only the declared operation IDs;
- a detached release signature;
- SHA-256 for every file and the complete canonical manifest;
- source commit SHA, build provenance, target PostgreSQL major version (18), target Windows architecture/runtime and compatibility declaration;
- expected role/schema/routine identifiers and exact catalog postconditions;
- an explicit upgrade-from / upgrade-to compatibility matrix; and
- a release note enumerating allowed catalog deltas and non-deltas.

The bundle contains no passwords, secret values, connection strings, private keys, raw challenges, owner credentials or environment-specific data.

### 3.2 Signature verification

- The central server has a deployment trust policy provisioned separately from the package. It pins the approved release-signing certificate/public-key identity and permitted algorithm; trust is not established by a key shipped inside the bundle.
- Verify the detached signature, signer identity, validity/revocation policy, manifest canonicalization and every file hash before database connection.
- Reject unsigned, expired, revoked, unknown-signer, hash-mismatched, duplicate-path, path-traversal, unexpected-file and downgrade packages.
- The signature proves package provenance/integrity only; it is not Owner approval, reviewer approval, CI evidence or production authorization.
- Package trust-anchor rotation/revocation is a separate controlled platform operation and must not be performed by the package being verified.

### 3.3 Fixed command surface

The maintenance executable exposes only these typed operations:
- `Preflight`
- `ProvisionPrincipals`
- `InstallRoutine`
- `VerifyDeployment`
- `RevokeExecutor`
- `VerifyRevocation`
- `ReconcileOperation`

No free-form SQL, shell command, file path, role name, schema name, routine name, database name or connection string is accepted from an operator. The target installation and database identity are resolved from trusted local deployment metadata and compared with the signed manifest. The executable does not launch a general-purpose SQL shell.

## 4. State machine and permitted transitions

| State | Meaning | Permitted next state |
|---|---|---|
| S0 — Absent | No O-46 bootstrap objects expected | S1 after package verification |
| S1 — PackageTrusted | Signature, hashes, host and target manifest pass | S2 after database preflight |
| S2 — PreflightPassed | Approved superuser, PostgreSQL 18, target DB, installation identity and clean catalog verified | S3 after principal provisioning |
| S3 — PrincipalsProvisioned | Exact two roles exist, initially non-login executor, no membership edges | S4 after schema and exact two-routine bundle install and verification |
| S4 — RoutinePairInstalledDormant | Function owner/schema/both exact routine definitions/ACL verified; executor remains NOLOGIN | S5 only after separate secret custody, local connection and HBA proofs |
| S5 — ExecutorEnabled | Exact executor capability is enabled for the one-time claim lifecycle | S6 after durable claim receipt is independently verified |
| S6 — ClaimCommitted | One immutable Owner binding and matching receipt exist for original InstallationId/SetupOperationId | S7 through independent revocation |
| S7 — Revoked | Executor NOLOGIN, sessions absent, credential erased, local setup path closed | S8 after final evidence verification |
| S8 — Closed | All postconditions and evidence verified | Terminal |

Any unexpected state or postcondition mismatch transitions to **F — FailClosed**. FailClosed does not automatically delete or repair objects. Recovery is an explicit, signed maintenance operation with a new operation ID and a documented reconciliation decision. It may resume the same operation only where idempotency rules prove that no second Owner claim can occur.

Routine upgrades use a separate transition from verified S4/S8-compatible state to a new verified routine release. They never re-enable a revoked executor merely to upgrade the routine.

## 5. Operation contracts

### 5.1 Preflight

Inputs: signed package ID/version/hash; trusted local installation identifier; approved target descriptor.

Checks, before any mutation:
- host is the designated central Windows server and local interactive session is present;
- package signature, signer trust, manifest and all hashes are valid;
- authenticated PostgreSQL `session_user` is exactly the pre-existing approved cluster superuser; database-owner-only and all application/migration identities fail;
- PostgreSQL major version is 18 and the target database/installation mapping is exact;
- expected current migration history and IdentityAccess schema contract are present;
- role, membership, ownership, schema, function, ACL and default-privilege inventory has no unexpected collision;
- backup/restore and maintenance preconditions are satisfied; and
- no incompatible package, concurrent maintenance operation or unfinished prior operation exists.

Preflight is read-only. It must not create a role, schema, table, function, grant, secret or migration.

### 5.2 ProvisionPrincipals

Precondition: S2, explicit maintenance confirmation and package allow-list.

Creates exactly:
- `mdgsm_identity_bootstrap_fn_owner`: NOLOGIN, NOSUPERUSER, NOCREATEDB, NOCREATEROLE, NOREPLICATION, NOBYPASSRLS;
- `mdgsm_bootstrap_executor`: NOLOGIN initially, NOSUPERUSER, NOCREATEDB, NOCREATEROLE, NOREPLICATION, NOBYPASSRLS, with no memberships.

No password is assigned while executor is NOLOGIN. No role membership, object ownership outside the declared function-owner scope, database-wide grant or role attribute beyond the signed contract is permitted.

If the roles already exist, only exact equality with the expected state is accepted; the operation records AlreadyProvisioned and makes no change. Any mismatch stops.

### 5.3 InstallRoutine — exact two-routine bundle

Preconditions:
- S3;
- WG-0 has approved and recorded the required IdentityAccess schema migration reservation and that migration is already applied and verified;
- the routine's required table/column grants have been separately reviewed and are present;
- the signed routine package declares exact schema compatibility and release hashes.

Within a transaction:
1. re-check target database, migration version and pre-install catalog fingerprint;
2. switch the superuser session locally to `mdgsm_identity_bootstrap_fn_owner`;
3. create or replace exactly these two versioned, fixed-signature, typed routines in `identity_bootstrap`, and no other callable routine:
   - `identity_bootstrap.issue_initial_owner_challenge_v1(...)`;
   - `identity_bootstrap.claim_initial_product_owner_v1(...)`;
   Both are owned by `mdgsm_identity_bootstrap_fn_owner`. No operation/phase parameter, generic dispatcher or caller-selected object name is permitted.
4. enforce fixed safe `search_path`, fully qualified references, no dynamic SQL and no caller-selected identifiers;
5. ensure schema CREATE is not granted to executor and no PUBLIC schema access or routine EXECUTE exists;
6. apply only schema USAGE and EXECUTE on the two exact routine signatures above to `mdgsm_bootstrap_executor`; grant no other function EXECUTE and leave executor NOLOGIN;
7. verify both routine owners, SECURITY DEFINER flags, languages, volatility/strictness where specified, exact signatures, definition fingerprints, ACLs, schema ACL, default ACL and effective privileges from catalogs; verify no other routine is executable by the executor;
8. verify no role membership, table privilege, sequence privilege, ownership or migration graph changed; and
9. commit only if all postconditions match the signed manifest.

A failed check rolls back the transaction. The maintenance session closes after completion. The function is not made externally callable by this operation until the separately gated executor activation step.

### 5.4 VerifyDeployment

Read-only catalog verification compares actual values against the signed manifest:
- role attributes and membership graph;
- database CONNECT, schema USAGE/CREATE and routine EXECUTE;
- direct/effective table and sequence privileges;
- schema, routine and dependent-object ownership;
- PUBLIC and default privileges;
- both exact routine signatures, shared function owner, `SECURITY DEFINER`, safe `search_path`, separate definition fingerprints, exact per-signature EXECUTE ACL and comment release identity;
- migration state and expected schema compatibility;
- no new N.6 role, membership or object ownership delta; and
- no application process or Setup Service can access cluster-admin credential material.

Any difference is a failure; the package does not repair it.

### 5.5 Executor activation (separate gate; not a package authorization)

Executor activation is not part of principal provisioning or routine installation. It requires a separate controlled authorization after Setup Service secret custody, SCRAM/client-auth mechanism, local-only connection path, complete effective `pg_hba.conf` ordering and negative remote/non-target database tests have passed.

The package contract allows no automatic transition from S4 to S5. It does not prescribe a secret storage technology or create/enable the executor in this design phase.

### 5.6 RevokeExecutor and VerifyRevocation

Precondition: the exact initial claim receipt is durable and independently verified for the same InstallationId and SetupOperationId, or an explicitly approved fail-closed abandonment path exists.

The trusted maintenance actor:
1. sets executor NOLOGIN;
2. terminates all backends authenticated as executor;
3. queries catalogs and activity views to prove no executor backend remains;
4. verifies zero role memberships and zero object ownership;
5. confirms Setup Service credential deletion and bootstrap-channel closure through separate OS-side evidence;
6. confirms immutable Owner binding and claim receipt are present and mutually consistent; and
7. records the final revocation evidence.

The executor cannot revoke itself. If any step fails, setup remains closed/fail-closed and the same operation is reconciled; no second claim is permitted. Revocation is idempotent: already-NOLOGIN plus no sessions is success only when the expected claim receipt and Owner binding are verified.

### 5.7 ReconcileOperation

Reconciliation is read-only first. It binds to the original operation ID, package hash, InstallationId and target database. It must classify the operation as not started, committed, rolled back, partially completed outside a transaction, or inconsistent.

It never creates a new owner claim, rotates the Owner identity, resets the binding or infers success from a client timeout. If the commit outcome is unknown, the durable database receipt and immutable binding are authoritative. Conflicting operation IDs or installation identities are denied.

## 6. Upgrade, rollback and recovery

- Every upgrade is a new signed immutable release with explicit compatible source versions and exact allowed catalog delta.
- Upgrade only the function body and its declared metadata; never silently alter roles, grants, table schema, migration history or Owner binding.
- Routine replacement, owner/ACL enforcement and catalog verification occur atomically. No transient PUBLIC EXECUTE state is allowed.
- Rollback is a separately signed prior compatible routine release, installed through the same maintenance boundary after preflight; never a database restore or broad privilege rollback by default.
- If schema compatibility has moved forward, an older function package that is not declared compatible is rejected. No automatic downgrade.
- Restore preserves InstallationId and original AccountId/Owner binding. If restored catalog, claim receipt, function release or installation metadata disagree, fail closed and require explicit reconciliation.
- A package failure never automatically drops roles, schemas, functions or grants. Destructive cleanup requires a separately reviewed, signed operation and proof that no Owner claim or dependent object exists.

## 7. Audit and evidence contract

Each operation emits a canonical, append-only evidence record containing:
- operation ID and prior-operation reference;
- package ID/version, manifest hash, file hashes and signer identity;
- source commit/build provenance;
- authenticated maintenance identity and local host identity;
- target InstallationId and non-secret database identifier;
- PostgreSQL version;
- before/after catalog fingerprints and exact allowed delta;
- migration version and schema compatibility declaration;
- each precondition/postcondition result;
- start/end timestamps from trusted system/DB clocks;
- transaction outcome, reconciliation classification and cleanup outcome.

Evidence excludes all credentials, connection strings, owner/account PII beyond opaque IDs, password hashes, password verifiers, HMAC keys, challenge values, private keys and raw SQL connection diagnostics. Evidence is stored outside the application runtime boundary with write protection and is linked to the PR/commit by hash. The evidence is not a substitute for independent review or CI.

## 8. Failure and rollback matrix

| Failure point | Required behavior |
|---|---|
| Signature/hash/host/target mismatch | Stop before database connection; no mutation |
| Wrong PostgreSQL identity/version | Stop; no mutation |
| Unexpected role/schema/routine/ACL | Stop; no takeover or repair |
| Transaction failure during principal/schema/routine operation | Roll back; verify catalog unchanged; otherwise FailClosed |
| Function verification failure | Roll back; executor remains NOLOGIN |
| Secret custody/HBA/loopback proof failure | Never enable executor; remain S4/F |
| Claim client timeout / unknown commit | Read-only reconcile by same SetupOperationId; never create another claim |
| Revocation failure | Close local setup path; keep app gates closed; retry independent cleanup |
| Package signature revoked or upgrade incompatible | Reject package; preserve current state |
| Restore identity/binding mismatch | Fail closed; no Owner re-enrollment |

## 9. Required acceptance tests — all NOT RUN / NOT VERIFIED

| ID | Test |
|---|---|
| O46-PKG-01 | Reject unsigned, wrong-signer, expired/revoked, hash-mismatched and downgrade packages before DB connection |
| O46-PKG-02 | Reject path traversal, duplicate paths, unexpected files and manifest canonicalization ambiguity |
| O46-PKG-03 | Only fixed typed operation IDs accepted; arbitrary SQL, identifiers, shell commands and connection targets rejected |
| O46-PKG-04 | Local central-server interactive invocation only; remote, service, scheduled and application-triggered invocations rejected |
| O46-PKG-05 | Preflight accepts only pre-existing approved cluster superuser; rejects DB owner, migration and runtime identities |
| O46-PKG-06 | Preflight is read-only and leaves all catalogs unchanged |
| O46-PKG-07 | Exactly two principals with exact flags; no third LOGIN, role membership or N.6/migration graph delta |
| O46-PKG-08 | Installation of exactly the two approved routines is atomic; owner, SECURITY DEFINER, each signature, search_path, separate definition fingerprints and ACL exact; no extra callable routine |
| O46-PKG-09 | No PUBLIC/default EXECUTE; executor EXECUTE only on the two exact signatures; no transient PUBLIC-executable state |
| O46-PKG-10 | Executor remains NOLOGIN until separate secret/HBA/loopback gates pass |
| O46-PKG-11 | No credential leakage to process args/env, shell history, logs, telemetry, package or evidence |
| O46-PKG-12 | Executor cannot access tables/sequences, DDL, role administration or any function except the two exact approved signatures |
| O46-PKG-13 | Upgrade accepts only declared compatible versions; rejected downgrade leaves catalog unchanged |
| O46-PKG-14 | Reconcile after unknown commit never creates second Owner or second binding |
| O46-PKG-15 | Independent revocation sets NOLOGIN, terminates all backends, confirms secret deletion and pipe closure |
| O46-PKG-16 | Failure at every state transition remains fail-closed and idempotently recoverable |
| O46-PKG-17 | Restore/reinstall/repair/update preserves InstallationId and immutable Owner binding |
| O46-PKG-18 | Exact PostgreSQL 18 disposable catalog proof and Windows central-server proof |
| O46-PKG-19 | Independent reviewer verifies exact release package, signatures, catalog evidence, negative tests and cleanup |
| O46-PKG-20 | Exact-head CI, checksum and documentation-link checks pass |
| O46-PKG-21 | Adversarial PostgreSQL 18 tests prove issuer/claim side-effect separation despite the shared function-owner role's aggregate table privileges |

No acceptance test is reported as passed until executed against the exact final implementation and evidence is attached.

## 10. Open dependencies and explicit non-authorizations

1. WG-0 must reserve a new IdentityAccess schema migration number; 0009 remains RESERVED for Accounting and must not be consumed. Issue #93 requests candidate 0010 and remains open; no number is reserved.
2. IdentityAccess must own and review the exact schema and table/column grant set required by both routines. Because both routines share one function-owner role, ACLs are aggregated at role level; fixed routine bodies, static review and adversarial PostgreSQL 18 tests must prove issuer/claim side-effect separation. The grant set cannot be guessed from this document.
3. The platform must approve the release-signing trust anchor, rotation/revocation process and canonical manifest format.
4. The exact local PostgreSQL administrative authentication channel and Windows operator UX remain to be specified and tested.
5. Executor secret custody, authentication mechanism, loopback-only HBA and Setup Service SID isolation remain separate gates.
6. A disposable PostgreSQL 18 fixture and Windows central-server test environment are required.
7. Independent Security/Architecture review must inspect one exact final SHA.

This document does not authorize DDL, SQL, migrations, role creation/alteration, grants, secret generation, executor LOGIN, Setup Wizard claim execution, production runtime, O-20/O-21 closure or G1. All remain gated by separate change control and evidence.

**END — MDG-SECURITY-O46-BOOTSTRAP-PROVISIONING-PACKAGE-CONTRACT-001 v0.2**
