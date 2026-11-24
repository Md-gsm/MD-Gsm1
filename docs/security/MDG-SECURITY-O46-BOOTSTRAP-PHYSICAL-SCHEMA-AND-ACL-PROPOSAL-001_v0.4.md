# O-46 — Bootstrap Physical Schema and Function-Owner ACL Reconciliation

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-BOOTSTRAP-PHYSICAL-SCHEMA-AND-ACL-PROPOSAL-001 |
| Version / Date | 0.5 / 2026-11-24 |
| Status | **OWNER-APPROVED DESIGN CONSTRAINTS AND JRN-REC-01 TWO-PRINCIPAL BOUNDARY RECONCILED — NO DDL / MIGRATION AUTHORIZED** |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Supersedes | v0.4 only for the reconciliation and precedence rules below |

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

## 2. Physical object model — v0.4 proposed physical contract

The IdentityAccess-owned objects are the following three tables. This is a frozen design proposal for review, not DDL or migration authorization.

### 2.1 `identity.product_owner_bindings`

| Column | Type | Nullability / constraint | Meaning |
|---|---|---|---|
| `installation_id` | `uuid` | PK, NOT NULL | One immutable original Owner binding per central InstallationId. |
| `account_id` | `uuid` | NOT NULL, UNIQUE; FK to `identity.accounts(account_id)`, ON DELETE RESTRICT | The exact original Product Owner AccountId. |
| `setup_operation_id` | `uuid` | NOT NULL, UNIQUE | Setup operation that committed the binding. |
| `bound_at_utc` | `timestamptz` | NOT NULL | Database-clock binding time. |

Also require `UNIQUE (installation_id, account_id)` for the receipt's composite FK. No UPDATE, DELETE, transfer, replacement or second binding path is allowed. The function-owner receives only the exact SELECT/INSERT columns required by the fixed claim body; no UPDATE/DELETE/TRUNCATE.

### 2.2 `identity.bootstrap_challenges`

| Column | Type | Nullability / constraint | Meaning |
|---|---|---|---|
| `challenge_id` | `uuid` | PK, NOT NULL | IdentityAccess-generated challenge identifier. |
| `installation_id` | `uuid` | NOT NULL | Stable central installation. |
| `setup_operation_id` | `uuid` | NOT NULL | Stable local setup operation. |
| `challenge_request_id` | `uuid` | NOT NULL | Issuer idempotency key. |
| `purpose` | `text` | NOT NULL, CHECK fixed to `initial_product_owner` | Routine-owned purpose; caller cannot choose it. |
| `operator_sid` | `text` | NOT NULL | SID attested from the authenticated local Windows token. |
| `verifier_digest` | `bytea` | NOT NULL, UNIQUE, exactly 32 bytes | SHA-256 digest of the raw 32-byte challenge only. |
| `created_at_utc` | `timestamptz` | NOT NULL | PostgreSQL clock. |
| `expires_at_utc` | `timestamptz` | NOT NULL | Greater than creation and no more than 30 minutes later. |
| `revoked_at_utc` | `timestamptz` | NULL | Issuer-only conditional revocation transition. |
| `consumed_at_utc` | `timestamptz` | NULL | Claim-only conditional single-use transition. |

Constraints/indexes: UNIQUE `(challenge_id, installation_id, setup_operation_id)` for the receipt FK; UNIQUE `(installation_id, setup_operation_id, challenge_request_id)`; UNIQUE `verifier_digest`; CHECK `expires_at_utc > created_at_utc` and `expires_at_utc <= created_at_utc + interval '30 minutes'`; CHECK that a row cannot be both revoked and consumed; and a partial unique index on `(installation_id, setup_operation_id, purpose)` WHERE both `revoked_at_utc` and `consumed_at_utc` are NULL. The issuer must revoke any prior unconsumed row for the same installation/operation/purpose before inserting a replacement, including an expired-but-not-yet-revoked row. No raw challenge is stored.

### 2.3 `identity.bootstrap_operations`

| Column | Type | Nullability / constraint | Meaning |
|---|---|---|---|
| `setup_operation_id` | `uuid` | PK, NOT NULL | Idempotent claim receipt key. |
| `installation_id` | `uuid` | NOT NULL | Stable installation. |
| `account_id` | `uuid` | NOT NULL | Resulting Product Owner AccountId. |
| `challenge_id` | `uuid` | NOT NULL, UNIQUE | Challenge consumed by this claim. |
| `payload_fingerprint_version` | `smallint` | NOT NULL, CHECK = 1 | Canonicalization version. |
| `payload_fingerprint` | `bytea` | NOT NULL, exactly 32 bytes | SHA-256 of the v1 canonical claim envelope. |
| `completed_at_utc` | `timestamptz` | NOT NULL | PostgreSQL claim timestamp. |

FK `(installation_id, account_id)` to the corresponding immutable binding; and FK `(challenge_id, installation_id, setup_operation_id)` to the consumed challenge using a matching UNIQUE key on the challenge table. Receipt rows are append-only: the function-owner receives only exact SELECT/INSERT rights, never UPDATE/DELETE/TRUNCATE.

### 2.4 Cross-table invariants

- One InstallationId has at most one immutable binding and one original AccountId.
- One SetupOperationId has at most one receipt; one consumed challenge can back at most one receipt.
- The receipt, binding, Account insertion, challenge consumption and IdentityAccess audit-intent insertion commit or roll back atomically.
- The binding row's existence is authoritative for completed Owner binding; `bootstrap_completed_at_utc` remains compatibility/diagnostic only and cannot independently authorize an Owner.
- UUIDs use the approved UUIDv7 provider at the trusted IdentityAccess/Platform boundary; exact generation ownership for each identifier and catalog/FK proof remain required before DDL.
- No cross-module FK is introduced for InstallationId or SetupOperationId.

### 2.5 Owner Account input contract — Product Owner accepted

The initial Product Owner Account profile is exactly these three user-supplied fields:

| Wizard field | IdentityAccess persistence mapping | Requirement |
|---|---|---|
| `username` | `identity.accounts.login_name` and its established `login_name_normalized` value | Required; the existing username normalization/uniqueness policy remains authoritative. No new login identifier is introduced. |
| `display_name` | New nullable-for-legacy `identity.accounts.display_name` column | Required for the initial Product Owner; non-empty after the IdentityAccess input validator. |
| `email` | Existing `identity.accounts.email` column | Required for the initial Product Owner; non-empty and validated by the IdentityAccess email validator. It remains a contact/recovery address, not a login identifier. |

No mobile/phone field is collected or required by this bootstrap contract. The existing `phone_e164` column is not populated by this flow. No extra profile field is implied.

The existing base migration names the login field `login_name`, not `username`, and already has nullable `email` and `phone_e164`; it has no `display_name`. Therefore the future reserved IdentityAccess migration must add `identity.accounts.display_name` as nullable for compatibility with existing non-bootstrap accounts, and add a CHECK constraint requiring non-empty `display_name` and `email` whenever `is_bootstrap_account = TRUE`. The Owner claim must set `is_bootstrap_account = TRUE`, `must_change_password = FALSE`, and supply all three approved fields. It must not set a phone number or create any role/permission/session data.

The accepted field set is frozen. Email syntax/canonicalization and display-name validation details must reuse a reviewed IdentityAccess validator; this decision does not invent trimming, case folding, email canonicalization or new Unicode rules. For fingerprinting, the exact validated strings are encoded as UTF-8 without any additional transformation; username fingerprint input is the established `login_name_normalized`, not the raw Wizard spelling.

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

## 5. Proposed payload fingerprint v1 — owner approval and test vectors pending

The proposed claim payload fingerprint is computed by the trusted IdentityAccess server boundary and persisted as a 32-byte SHA-256 digest. It is not keyed: no new key, secret, role or credential boundary is introduced. It is domain-separated and includes the final salted/versioned password-hash output, not the plaintext password. The exact v1 binary envelope is a cross-document technical proposal in Challenge Protocol v0.4; it is not yet Product Owner-approved. Cross-language test vectors remain required.


**Restart/idempotency gate — unresolved:** the current `PlatformPasswordHasher` uses ASP.NET Core Identity V3 PBKDF2, and the current `IPasswordHasher.Hash(password)` contract has no caller-supplied salt; each call generates a fresh random salt. Therefore hashing the same password again after restart produces a different encoded result. Because the proposed fingerprint binds the exact encoded hash, recomputing it after a process restart can produce a different fingerprint for the same human-entered credentials. Before fingerprint v1 can be accepted or implemented, IdentityAccess must select and threat-review a restart-safe strategy that preserves the exact hash/fingerprint for the same SetupOperationId (for example, protected operation state or a reviewed per-operation salt/hasher contract). The system must never silently regenerate a different hash under the same operation ID. A new SetupOperationId is permitted only after authoritative read-only reconciliation proves that no binding/receipt committed and the prior challenge cannot later claim Owner authority. No local hash/salt custody mechanism is selected or authorized here.

## 6. Required proof and unresolved decisions

All remain NOT RUN / NOT VERIFIED:

- PostgreSQL 18 effective-privilege/catalog proof, including column ACLs, PUBLIC/default ACL, ownership, memberships and RLS;
- proof that issuer and claim bodies cannot cross their intended side-effect boundaries despite aggregate ACLs;
- fixed search_path, fully qualified references, no dynamic SQL and exact signature checks;
- issue-vs-claim serialization, concurrent issuers/claims, idempotency, unknown commit, expiry, revocation, replay and rollback;
- UUIDv7 generation boundary, exact routine signatures and fixed data-access order;
- trusted SID attestation and stable InstallationId/SetupOperationId provenance;
- WG-0 migration number/path reservation and IdentityAccess owner acceptance;
- independent Security/Architecture acceptance of the aggregate-ACL residual risk.

## 7. Explicit non-authorization

No SQL, DDL, schema, table, routine, role, grant, membership, credential, secret, migration, Account, Owner binding, challenge, Setup Wizard execution, executor LOGIN, G1 or production activation is authorized. The physical contract remains a proposal and all tests remain NOT RUN.

**END — v0.4**


## 8. Reconciliation View boundary — JRN-REC-01 (Owner decision, 2026-11-24)

The Reconciliation View is an approved architecture target, not an implementation authorization. It is read-only and separate from the three durable Bootstrap tables described above.

The exact-two-principal boundary is binding:
- The only new PostgreSQL principals remain `mdgsm_identity_bootstrap_fn_owner` and `mdgsm_bootstrap_executor`.
- `JOURNAL_RECONCILIATION_READER` is a logical application-layer identity/capability only. It must not be created as a PostgreSQL role, LOGIN, membership or other database principal.
- The View owner must be selected from a pre-existing controlled NOLOGIN PostgreSQL role after role-inventory review. No third owner role may be created.
- SELECT must be routed only through a pre-existing execution identity whose existing Grant Matrix and effective privileges prove authorization. This identity is not presumed to be `mdgsm_bootstrap_executor`; Executor receives no direct SELECT and no privilege expansion.
- If no already-authorized SELECT path exists, View creation and Reconciliation execution remain blocked. Do not add grants, create a service identity or repurpose Executor to close the gap.

At the application layer, `JOURNAL_RECONCILIATION_READER` is checked by explicit application authorization. That logical permission grants no direct database access, no routine execution, no write capability and no access to private keys. Application routing must use the separately verified existing database execution identity.

The View definition must expose only minimum bootstrap operation reconciliation metadata. It must exclude password hashes, raw challenges, challenge verifier digests, profile PII, credentials, private keys and unrelated attributes. It must be non-updatable, have no DML rules or INSTEAD OF triggers, and be subject to PostgreSQL 18 proof for owner, RLS, security-barrier behavior, installation scoping, effective SELECT privileges and base-table ACLs.

The View's read path must participate in the same InstallationId-scoped transaction-level serialization barrier as claim: claim uses the exclusive form; reader uses the compatible shared form and then obtains a fresh READ COMMITTED snapshot. Exact lock primitive, lock namespace derivation, collision analysis and proof that every claim route takes the same lock remain OPEN. No third routine is allowed.

No View, role, grant, lock, routine, SQL, DDL or migration is created or authorized by this design update.
