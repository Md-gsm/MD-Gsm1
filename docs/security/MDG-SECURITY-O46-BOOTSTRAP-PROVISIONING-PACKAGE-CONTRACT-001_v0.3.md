# O-46 — Bootstrap Provisioning Package and Maintenance Operation Contract

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-BOOTSTRAP-PROVISIONING-PACKAGE-CONTRACT-001 |
| Version / Date | 0.3 / 2026-11-23 |
| Status | **OWNER-APPROVED MAINTENANCE BOUNDARY RECONCILED — DESIGN ONLY; IMPLEMENTATION NOT AUTHORIZED** |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Supersedes | v0.2 for the reconciled authority/channel boundaries below |

## 1. Purpose and controlling boundary

This contract describes a future, signed and versioned maintenance package used only by the existing central-server PostgreSQL cluster-administrator root of trust. It may provision and verify the two owner-approved PostgreSQL principals, install/upgrade/verify the exact two IdentityAccess bootstrap routines, and independently revoke the executor after the claim.

The package is a local maintenance tool, not an application service. It is invoked explicitly and interactively on the designated central Windows Server. It is not part of the Setup Wizard, Setup Service, API, Desktop client or DbMigrator. It does not use the Setup Named Pipe, a TCP/HTTP endpoint, scheduled task, service, background worker or remote execution path.

The cluster-administrator credential is never stored in or exposed to the package, Wizard, Setup Service, API, Desktop client, DbMigrator, executor, process arguments, environment variables, source, logs or evidence. It is supplied only through the already-approved local administrative authentication channel; exact credential-entry mechanics remain OPEN.

## 2. Owner-approved invariants

1. The only maintenance actor is the pre-existing approved PostgreSQL cluster superuser/root of trust. Database-owner-only, migration, runtime, executor and ordinary user identities are rejected.
2. No third persistent deployment LOGIN or any new role/membership edge is created.
3. The five N.6 operational identities and existing `migration_runner → mdgsm_schema_owner` SET-only membership remain unchanged.
4. The only two new PostgreSQL principals are `mdgsm_identity_bootstrap_fn_owner` (NOLOGIN) and `mdgsm_bootstrap_executor` (initially NOLOGIN).
5. Exactly two fixed-signature SECURITY DEFINER routines may be installed:
   - `identity_bootstrap.issue_initial_owner_challenge_v1(...)`
   - `identity_bootstrap.claim_initial_product_owner_v1(...)`
6. Both routines are owned by the NOLOGIN function-owner. Executor receives only schema USAGE and EXECUTE on those two exact signatures after separate activation gates. It has no direct table/sequence privilege, membership, ownership, schema CREATE, DDL or role administration.
7. Function-owner grants are aggregate at role level. The approved challenge issuer delta is exact challenge-column INSERT plus conditional UPDATE of `revoked_at_utc` only; this is not a per-function database sandbox. Fixed reviewed bodies, signatures and separate fingerprints must enforce intended side-effect separation, with independent Security/Architecture risk acceptance.
8. Provisioning and routine installation do not create an Account, challenge, Owner binding, Owner credential, session, role/permission assignment or business data.

## 3. Signed package and trust

Each immutable release contains a typed manifest, fixed executable/dependencies, only allow-listed SQL resources, detached signature, canonical manifest/file SHA-256 hashes, source commit/build provenance, PostgreSQL 18 and Windows compatibility declaration, exact expected catalog postconditions, compatibility matrix and release note.

The trust anchor is provisioned independently of the bundle. Signature, signer, revocation/validity, canonical manifest and every file hash are verified before opening a privileged database session. Reject unknown/expired/revoked signers, hash mismatch, downgrade, path traversal, duplicate or unexpected files. Package integrity is not Owner approval, independent review, CI evidence or production authorization.

No password, connection string, secret, private key, raw challenge, Owner credential or environment-specific value is included in the bundle.

## 4. Fixed operation surface

Only these typed operations are allowed:

- `Preflight` — read-only checks.
- `ProvisionPrincipals` — create or exact-match verify the two accepted roles, both initially NOLOGIN.
- `InstallRoutinePair` — install/upgrade only the exact two approved routines and their exact metadata/ACLs atomically.
- `VerifyDeployment` — read-only catalog and fingerprint comparison.
- `RevokeExecutor` — independent post-claim NOLOGIN/session termination and cleanup verification.
- `VerifyRevocation` — read-only proof of revocation and cleanup.
- `ReconcileOperation` — read-only classification of an interrupted/unknown operation.

No arbitrary SQL, shell, generic dispatcher, caller-supplied role/schema/routine/database name, free-form file path or connection string is accepted. The package resolves the designated target from trusted local deployment metadata and verifies it against the signed manifest.

## 5. State machine and hard gates

| State | Meaning | Transition |
|---|---|---|
| S0 Absent | No O46 objects expected | Verified package → S1 |
| S1 PackageTrusted | Signature, hashes, host and target pass | Read-only preflight → S2 |
| S2 PreflightPassed | Correct cluster superuser, host, PostgreSQL 18, DB, installation and clean catalog | Provision two dormant principals → S3 |
| S3 PrincipalsProvisioned | Exact roles; executor NOLOGIN; zero memberships | Install exact routine pair atomically → S4 |
| S4 RoutinePairInstalledDormant | Function owner/schema/routines/ACL/fingerprints verified; executor NOLOGIN | Separate secret/HBA/loopback authorization and proof only → S5 |
| S5 ExecutorEnabled | Narrow executor capability separately authorized for initial claim | Durable claim receipt and binding independently verified → S6 |
| S6 ClaimCommitted | Original InstallationId/SetupOperationId has one immutable binding and matching receipt | Independent maintenance revocation → S7 |
| S7 Revoked | Executor NOLOGIN, sessions absent, credential erased, Setup channel closed | Final evidence verified → S8 |
| S8 Closed | All postconditions verified | Terminal |

Any unexpected state or mismatch is FailClosed. No automatic repair, takeover, destructive cleanup, second Owner claim or return to S0 is permitted. No package operation automatically advances S4 to S5; activation requires a separate explicit authorization.

## 6. Operation contracts

### 6.1 Preflight

Read-only. Verify local interactive execution on the designated central Windows Server, trusted package, approved cluster-superuser session identity, PostgreSQL major 18, target DB/InstallationId, migration/schema compatibility, backup/change-control preconditions, and complete role/membership/ownership/schema/routine/ACL/default-ACL inventory. Reject database-owner-only, migration_runner, mdgsm_schema_owner, runtime, executor and all non-approved actors. No catalog mutation.

### 6.2 ProvisionPrincipals

Create exactly the two accepted roles with exact flags. Both are NOLOGIN at creation. The executor remains NOLOGIN; no password/secret is assigned. Existing roles are accepted only on exact catalog equality; any unexpected attributes, memberships, ownership or ACL cause hard stop. No third LOGIN, role or membership is created.

### 6.3 InstallRoutinePair

Preconditions: WG-0 has reserved the IdentityAccess migration number; the approved table-only migration is applied; exact IdentityAccess tables/constraints and reviewed function-owner grants are present; package compatibility is verified.

In one transaction, use the authenticated cluster-superuser session with transaction-local `SET LOCAL ROLE mdgsm_identity_bootstrap_fn_owner` to create/replace exactly the two named fixed-signature routines. Pin safe `search_path` with `pg_temp` last, fully qualify references, prohibit dynamic SQL and caller-selected identifiers, revoke PUBLIC EXECUTE, then grant executor schema USAGE and EXECUTE only on the two exact signatures. Verify owners, SECURITY DEFINER, signatures, definitions, separate fingerprints, exact ACLs, default privileges and effective grants before commit. No intermediate PUBLIC-executable state is allowed. The executor remains NOLOGIN.

The package must not install any third routine, helper callable by executor, generic dispatcher or unrelated object.

### 6.4 VerifyDeployment

Read-only verification of role flags, memberships, database CONNECT, schema USAGE/CREATE, exact routine EXECUTE, direct/effective table and sequence privileges, ownership, PUBLIC/default ACL, function security attributes, search_path, definition fingerprints, migration version and no change to N.6 or migration membership graph. Any mismatch fails; no repair.

### 6.5 Executor activation — separate authorization

Activation is not part of provisioning or routine installation. It remains blocked until the final authentication method, Setup Service secret custody, loopback-only connection path, full effective `pg_hba.conf` ordering and negative remote/non-target database tests are proven. The package does not choose DPAPI, Credential Manager, CNG, certificate authority or another secret technology. Executor LOGIN remains NOLOGIN until a separate explicit authorization.

### 6.6 RevokeExecutor / VerifyRevocation

Only after the exact claim receipt and immutable binding are independently reconciled, the existing cluster-superuser maintenance actor sets executor NOLOGIN, terminates all executor backends, verifies no session remains, verifies zero membership/object ownership, and verifies the Setup Service credential is erased and the local Setup Named Pipe path is closed. The executor cannot revoke itself. Cleanup is retryable against the same InstallationId/SetupOperationId and never permits another claim.

### 6.7 ReconcileOperation

Read-only first. Reconcile using the same operation ID, package hash, InstallationId and target database. Durable receipt and immutable binding determine claim outcome. Client timeout is not success. No new claim, owner, binding or operation identity is created to resolve ambiguity.

## 7. Channel separation — mandatory interpretation

There are two different local channels and they must never be conflated:

- **Maintenance channel:** explicit interactive local administrative operation using the existing cluster-superuser root. It is used only for principal/schema/routine provisioning, verification and post-claim executor revocation. It is not a Named Pipe, Setup Service, Wizard or application endpoint.
- **Setup channel:** authenticated local Windows Named Pipe between the Setup Wizard and Setup Service, used for operator SID attestation and one-time challenge delivery/claim orchestration. It never carries the cluster-superuser credential and cannot perform DDL or role provisioning.

The Setup Service may use the executor only after separate activation gates. It never becomes a maintenance actor.

## 8. Evidence, failures and acceptance

Evidence records operation/package IDs, signer, source commit, hashes, authenticated maintenance identity, host, target InstallationId/database, PostgreSQL version, before/after catalog fingerprints, exact allowed delta, migration version, pre/postcondition outcomes, transaction/reconciliation/cleanup result and timestamps. It excludes secrets, passwords, hashes/verifiers, raw challenge, private keys and connection strings.

All O46-PKG and O46-DEP acceptance tests remain **NOT RUN / NOT VERIFIED**. Required cases include package signature/tamper/downgrade rejection; local-only and approved-superuser-only preflight; exact two-role/membership graph; atomic exact routine pair; aggregate function-owner ACL side-effect tests; no PUBLIC/default EXECUTE; no executor access outside two routines; no secret leakage; unknown commit reconciliation; independent revocation; restore continuity; Windows central-host proof; PostgreSQL 18 catalog proof; and independent review.

## 9. Non-authorization

No role, schema, table, routine, grant, membership, credential, secret, migration, Owner, challenge, LOGIN activation, Setup Wizard execution, recovery, G1 or production activation is authorized by this contract. It is a design-only contract.

**END — v0.3**
