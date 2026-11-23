# O-46 — Bootstrap Physical Schema and Function-Owner ACL Reconciliation

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-BOOTSTRAP-PHYSICAL-SCHEMA-AND-ACL-PROPOSAL-001 |
| Version / Date | 0.3 / 2026-11-23 |
| Status | **OWNER-APPROVED DESIGN CONSTRAINTS RECONCILED — PHYSICAL CONTRACT OPEN — NO DDL / MIGRATION AUTHORIZED** |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Supersedes | v0.2 only for the reconciliation and precedence rules below |

## 1. Purpose and controlling decisions

This is a controlled successor/reconciliation to v0.2. The detailed candidate object model and privilege analysis in v0.2 remain applicable unless explicitly narrowed here. This document is design-only. It is not SQL, DDL, a migration, a role/grant script, or implementation authorization.

The controlling owner-approved boundaries are:

1. Use only the already-existing PostgreSQL cluster-administrator/superuser root of trust, through a separately invoked, explicitly local maintenance operation on the designated central Windows Server, for provisioning the two accepted principals and installing/verifying the isolated routine pair.
2. The only accepted principals are `mdgsm_identity_bootstrap_fn_owner` (NOLOGIN) and `mdgsm_bootstrap_executor` (initially NOLOGIN). No third persistent deployment LOGIN, role, membership edge or migration identity is approved.
3. Exactly two fixed-signature SECURITY DEFINER routines are in scope:
   - `identity_bootstrap.issue_initial_owner_challenge_v1(...)`
   - `identity_bootstrap.claim_initial_product_owner_v1(...)`
   Both are owned by the function-owner and are the only routines the executor may execute.
4. The executor receives no direct table, column or sequence privileges, no schema CREATE, no role membership, no ownership and no DDL/role-administration capability.
5. The challenge issuer's additional function-owner capability is limited to exact challenge-column INSERT and conditional UPDATE of `revoked_at_utc`; claim retains only its separately specified verification and conditional `consumed_at_utc` transition plus exact Owner-claim writes.
6. PostgreSQL table/column ACLs for the shared function-owner are role-wide, not per-function. Issuer/claim side-effect separation is therefore a fixed-function-code-integrity boundary, not two independent database privilege sandboxes. Independent Security/Architecture review and adversarial PostgreSQL 18 proof must explicitly accept this residual risk.

The owner decisions are recorded in the O46 Bootstrap DB Provisioning Decision v0.2, Bootstrap Database Principal Decision v0.2 and PR #92 comment 7110645790. These decisions do not authorize implementation.

## 2. Physical object model retained from v0.2

The proposed IdentityAccess-owned objects remain:

- `identity.product_owner_bindings`: immutable one-row-per-InstallationId binding to the original AccountId; no update, delete, transfer, successor or replacement path.
- `identity.bootstrap_challenges`: setup-only verifier state, bound to InstallationId, SetupOperationId, fixed purpose and OS-attested operator SID; stores only the 32-byte SHA-256 digest, never raw challenge; one-use, expiring, revocable and idempotent by challenge_request_id.
- `identity.bootstrap_operations`: append-only claim receipt binding the SetupOperationId, InstallationId, canonical payload fingerprint and resulting AccountId; same-operation/same-fingerprint reconciliation only.

Exact column names, nullability, checks, unique constraints, FK behavior, fingerprint canonicalization and lock-key derivation remain OPEN. The v0.2 candidate columns are not frozen physical contract. No DDL may be drafted or applied until IdentityAccess owner acceptance and WG-0 migration reservation.

## 3. ACL reconciliation

### 3.1 Principals and routine surface

| Principal | State / authority |
|---|---|
| Existing cluster superuser | Separate local maintenance root only; not an application credential |
| `mdgsm_identity_bootstrap_fn_owner` | NOLOGIN; owns only `identity_bootstrap` and the exact two routines |
| `mdgsm_bootstrap_executor` | Initially NOLOGIN; zero memberships; no table/sequence privileges; only exact schema USAGE and EXECUTE on the two approved signatures after separate activation gates |
| `migration_runner` | Existing SET-only mapping to `mdgsm_schema_owner`; unchanged |
| `mdgsm_runtime` and other N.6 identities | Unchanged; no bootstrap capability |

No new LOGIN, SUPERUSER, CREATEROLE, membership, ownership or migration path is authorized.

### 3.2 Function-owner aggregate grants

The detailed v0.2 privilege matrix remains a candidate minimum set, subject to final function-body review. The only newly owner-approved delta is:

- `identity.bootstrap_challenges`: INSERT on the exact issue columns required for a verifier-only challenge row;
- UPDATE on `revoked_at_utc` only, and only as a conditional transition of a prior unconsumed challenge;
- the claim routine's previously specified conditional UPDATE on `consumed_at_utc` remains separate in purpose;
- no DELETE or TRUNCATE on any bootstrap table.

All other grants remain limited to the exact columns needed by the fixed routines. No broad table grants, unrelated IdentityAccess access, role/permission/session/recovery mutation, sequence privileges or business-data access are allowed.

**Critical ACL limitation:** PostgreSQL grants these privileges to the shared function-owner role, so either SECURITY DEFINER routine technically runs with the aggregate privilege set. EXECUTE grants are per-signature, but table ACLs are not per-function. The issuer must be constrained by its immutable reviewed body to challenge-only effects; the claim body must be constrained to verify/consume and the atomic initial Owner claim. Signed package hashes, separate routine-definition fingerprints, static review and adversarial tests are mandatory. If independent reviewers require database-enforced per-routine table isolation, stop and return the design to the Product Owner; do not invent a third role.

### 3.3 IdentityAccess migration boundary

The future IdentityAccess-owned migration, only after WG-0 reservation, may create the three durable tables and constraints and apply the exact reviewed table/column grants to the function-owner. It must not create roles, memberships, SECURITY DEFINER routines, executor LOGIN, credentials, Owner seed, role/permission seed or production activation.

Migration 0009 remains reserved for Accounting. Candidate 0010 is not reserved. The existing DbMigrator path and `migration_runner → mdgsm_schema_owner` SET-only membership remain unchanged; no membership expansion is permitted.

## 4. Separation of provisioning and challenge execution

The privileged maintenance package is not the Setup Wizard or Setup Service. It is a separate, explicitly invoked local maintenance action using the existing cluster-administrator root. It does not use the Setup Named Pipe, accept Wizard payloads, issue a challenge, claim an Owner or run as a background service.

The Setup Service's local Named Pipe is a separate application trust channel for the one-time Wizard interaction. It authenticates the local Windows operator SID and transports the raw challenge once to the Wizard. It never receives the cluster-administrator credential and never performs DDL or role provisioning.

The challenge issue and claim routines are invoked only through the separately gated executor path; executor LOGIN remains disabled until secret custody, loopback-only PostgreSQL access, complete HBA ordering and negative connection tests are independently proven and separately authorized.

## 5. Required proof and unresolved decisions

All remain NOT RUN / NOT VERIFIED:

- PostgreSQL 18 effective-privilege/catalog proof, including column ACLs, PUBLIC/default ACL, ownership, memberships and RLS;
- proof that issuer and claim bodies cannot cross their intended side-effect boundaries despite aggregate ACLs;
- fixed search_path, fully qualified references, no dynamic SQL and exact signature checks;
- issue-vs-claim serialization, concurrent issuers/claims, idempotency, unknown commit, expiry, revocation, replay and rollback;
- exact profile-field contract and canonical payload fingerprint;
- trusted SID attestation and stable InstallationId/SetupOperationId provenance;
- WG-0 migration number/path reservation and IdentityAccess owner acceptance;
- independent Security/Architecture acceptance of the aggregate-ACL residual risk.

## 6. Explicit non-authorization

No SQL, DDL, schema, table, routine, role, grant, membership, credential, secret, migration, Account, Owner binding, challenge, Setup Wizard execution, executor LOGIN, G1 or production activation is authorized. The physical contract remains a proposal and all tests remain NOT RUN.

**END — v0.3**
