# O-46 — Bootstrap Physical Schema and Function-Owner ACL Proposal

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-BOOTSTRAP-PHYSICAL-SCHEMA-AND-ACL-PROPOSAL-001 |
| Version / Date | 0.2 / 2026-11-23 |
| Status | **OWNER-APPROVED CHALLENGE PROTOCOL INCORPORATED — PHYSICAL SCHEMA/ACL STILL PROPOSAL; NO DDL / MIGRATION AUTHORIZED** |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Supersedes | MDG-SECURITY-O46-BOOTSTRAP-PHYSICAL-SCHEMA-AND-ACL-PROPOSAL-001 v0.1 |
| Source baseline | IdentityAccess migration 0003; IdentityAccess schema design; O-46 owner decisions and provisioning package contract |
| Target PostgreSQL | 18 |

## 1. Purpose and boundary

This proposal derives the minimum physical data surface for the one-time initial Product Owner claim. It is based only on the current MD-Gsm1 source tree and its accepted O-46 decisions. It is not SQL, a migration, an implementation instruction, or permission to provision roles, grants, secrets, schema objects or credentials.

IdentityAccess remains the sole owner of Account, credential, challenge state and immutable Product Owner binding. The Setup Wizard and Setup Service do not write IdentityAccess tables directly. The executor receives no direct table or sequence privileges; its only application-facing database capabilities are the two exact fixed-signature SECURITY DEFINER routines `identity_bootstrap.issue_initial_owner_challenge_v1(...)` and `identity_bootstrap.claim_initial_product_owner_v1(...)`. Both are owned by the existing NOLOGIN function-owner role and executable only by the existing `mdgsm_bootstrap_executor`. This is the narrowly bounded owner-approved design constraint recorded in PR comment 7110645790; it does not authorize installation or activation.

The approved product constraints remain controlling: one local Setup Wizard on the central Windows Server; final Owner username/password/profile are entered there; the Owner is not forced to change them after first login; no alternate Owner creation/replacement/transfer/re-enrollment path; same InstallationId and AccountId survive reinstall, repair, update and restore.

## 2. Source-grounded existing schema

Migration `0003_identityaccess_core.sql` currently creates schema `identity` and these relevant objects:

- `identity.accounts`: account_id, login_name, login_name_normalized, password_hash, password_hash_version, must_change_password, is_enabled, is_bootstrap_account, credential_version, failed_attempt_count, failure_window_started_at, locked_until, email, phone_e164, created_at_utc, updated_at_utc, created_by_account_id.
- `identity.system_settings`: singleton settings_id=1, max_active_users, heartbeat_interval_seconds, session_lease_seconds, bootstrap_completed_at_utc, updated_at_utc, updated_by_account_id.
- `identity.security_audit_events`: append-only event table with nullable actor_account_id/subject_account_id, event_type, outcome, correlation_id and JSONB details.
- Roles, permissions, account_roles, role_permissions, sessions, login_attempts, recovery_codes and recovery_challenges also exist, but the bootstrap claim must not mutate or access them.

The existing schema does **not** contain a durable InstallationId-to-AccountId owner binding, a setup-specific one-time challenge record, or an idempotent bootstrap operation receipt. The existing recovery_challenges table is not an acceptable substitute: it has different semantics and is owned by the Recovery workflow. The current bootstrap_completed_at_utc singleton marker is not sufficient as an owner binding or replay ledger.

## 3. Proposed minimal physical objects

The following are proposed additions owned by IdentityAccess. Names are candidates and must be frozen by the IdentityAccess owner before WG-0 reserves a migration.

### 3.1 `identity.product_owner_bindings` — immutable binding

Candidate columns:
- installation_id UUID PRIMARY KEY;
- account_id UUID NOT NULL UNIQUE REFERENCES identity.accounts(account_id) ON DELETE RESTRICT;
- claimed_at_utc TIMESTAMPTZ NOT NULL;
- claim_operation_id UUID NOT NULL UNIQUE.

Invariants:
- exactly one row per InstallationId and AccountId; no UPDATE or DELETE path;
- the inserted account must be the one initial bootstrap account;
- no successor, transfer, re-enrollment, replacement or second binding;
- restore/reinstall/repair/update preserve the same row and identifiers.
- Function owner receives SELECT on the columns needed for exact idempotent verification and INSERT on the exact insert columns; no UPDATE/DELETE/TRUNCATE.

### 3.2 `identity.bootstrap_challenges` — setup-only challenge state

Candidate columns:
- challenge_id UUID PRIMARY KEY;
- installation_id UUID NOT NULL;
- setup_operation_id UUID NOT NULL;
- challenge_request_id UUID NOT NULL (issuer idempotency key), with UNIQUE (installation_id, setup_operation_id, challenge_request_id);
- purpose TEXT NOT NULL with a single fixed claim-purpose value;
- operator_sid TEXT NOT NULL;
- verifier_digest BYTEA NOT NULL with exact length and uniqueness constraints;
- created_at_utc TIMESTAMPTZ NOT NULL;
- expires_at_utc TIMESTAMPTZ NOT NULL;
- consumed_at_utc TIMESTAMPTZ NULL;
- revoked_at_utc TIMESTAMPTZ NULL.

Invariants:
- challenge has >=256-bit CSPRNG entropy before one-way verifier derivation; raw challenge never persists;
- expiry is checked against PostgreSQL clock inside the claim transaction; lifetime <=30 minutes;
- binding to InstallationId, SetupOperationId, purpose and authenticated local operator SID;
- single-use consumption is conditional and atomic; no challenge may be rebound or reset;
- the same challenge_request_id plus identical immutable inputs is idempotent; reuse with different inputs fails closed;
- a new request ID may supersede one prior unconsumed challenge for the same InstallationId + SetupOperationId + fixed purpose by setting only revoked_at_utc; the prior challenge is never reactivated;
- the issue routine has no Account, binding, receipt, role, permission, session, recovery or business-data write capability;
- the claim routine alone performs conditional consumed_at_utc transition as part of the atomic Owner claim.
- Function owner receives SELECT on exact issue/claim verification and idempotency columns; INSERT only on the exact challenge columns (challenge_id, installation_id, setup_operation_id, challenge_request_id, purpose, operator_sid, verifier_digest, created_at_utc, expires_at_utc); UPDATE only revoked_at_utc for conditional prior-challenge revocation and consumed_at_utc for conditional one-time claim consumption; no DELETE/TRUNCATE.

### 3.3 `identity.bootstrap_operations` — idempotent claim receipt

Candidate columns:
- setup_operation_id UUID PRIMARY KEY;
- installation_id UUID NOT NULL;
- payload_fingerprint BYTEA NOT NULL with exact length;
- account_id UUID NOT NULL REFERENCES identity.accounts(account_id) ON DELETE RESTRICT;
- binding_id / binding reference as needed (or use the unique installation/account pair);
- completed_at_utc TIMESTAMPTZ NOT NULL;
- result_version SMALLINT NOT NULL.

Invariants:
- append-only receipt; same operation ID and same immutable payload fingerprint returns the exact committed AccountId/binding result;
- same operation ID with a different fingerprint, a different operation after claim, or any second claim fails closed;
- receipt contains no password, password hash, raw challenge, verifier, email OTP, secret or private key;
- Function owner receives SELECT on exact reconciliation columns and INSERT on exact receipt columns; no UPDATE/DELETE/TRUNCATE.

The exact fingerprint canonicalization and whether the receipt stores a binding identifier or only the AccountId plus InstallationId must be fixed in the detailed routine contract before DDL.

## 4. Minimum SECURITY DEFINER function-owner privileges

The role `mdgsm_identity_bootstrap_fn_owner` remains NOLOGIN, NOSUPERUSER, NOCREATEDB, NOCREATEROLE, NOREPLICATION, NOBYPASSRLS; it owns only schema `identity_bootstrap` and the exact versioned claim routine(s). It does not own schema `identity` or any application table.

Proposed privilege matrix (column-level wherever PostgreSQL supports it):

| Object | Required capability | Explicitly denied |
|---|---|---|
| Schema `identity` | USAGE only | CREATE |
| `identity.accounts` | INSERT only: account_id, login_name, login_name_normalized, password_hash, password_hash_version, must_change_password, is_enabled, is_bootstrap_account, credential_version, created_at_utc, updated_at_utc; SELECT only account_id for INSERT RETURNING / receipt binding | UPDATE, DELETE, TRUNCATE, broad SELECT, all role/permission/session/recovery access |
| `identity.product_owner_bindings` | SELECT exact binding identity columns for same-operation reconciliation; INSERT exact binding columns | UPDATE, DELETE, TRUNCATE |
| `identity.bootstrap_challenges` | SELECT only challenge_id, installation_id, setup_operation_id, challenge_request_id, purpose, operator_sid, verifier_digest, created_at_utc, expires_at_utc, consumed_at_utc, revoked_at_utc; INSERT only challenge_id, installation_id, setup_operation_id, challenge_request_id, purpose, operator_sid, verifier_digest, created_at_utc, expires_at_utc; UPDATE only revoked_at_utc (issuer, conditional revocation) and consumed_at_utc (claim, conditional one-time consumption) | UPDATE of identity/binding/verifier/created/expiry fields; DELETE; TRUNCATE; any other column privilege |
| `identity.bootstrap_operations` | SELECT setup_operation_id, installation_id, payload_fingerprint, account_id, completed_at_utc, result_version; INSERT exact receipt columns | UPDATE, DELETE, TRUNCATE |
| `identity.security_audit_events` | INSERT only event_id, occurred_at_utc, actor_account_id, subject_account_id, event_type, outcome, correlation_id, details | SELECT, UPDATE, DELETE, TRUNCATE |
| `identity.system_settings` | SELECT settings_id, bootstrap_completed_at_utc; UPDATE bootstrap_completed_at_utc, updated_at_utc, updated_by_account_id only, conditioned on singleton and NULL marker | all other columns, INSERT, DELETE, TRUNCATE |
| Sequences | none expected; all IDs supplied as UUIDv7 by trusted typed inputs or generated in the routine using approved CSPRNG source | USAGE, SELECT, UPDATE on every sequence |
| Any other schema/object | none | all privileges |

The accounts INSERT column list deliberately omits email, phone, failed-login counters, lock fields and created_by_account_id unless a separately reviewed requirement proves they are indispensable. The initial Owner's final profile fields and normalization/password-hash validation must be fully specified in the application/routine contract; this proposal does not silently add email/phone or role assignment.

The audit event is an intent record only. The claim routine must not claim that an external durable AuditSecurity acknowledgement has occurred. Audit insertion must be part of the same claim transaction so failure rolls back the claim.

**Function-owner ACL caveat:** PostgreSQL table/column grants attach to the shared `mdgsm_identity_bootstrap_fn_owner` role, not separately to each SECURITY DEFINER function. The executor receives EXECUTE only on the two exact signatures, but the database does not create per-function table ACLs for the two routines. Therefore the narrow table privileges above are the aggregate maximum available to both function bodies. Their behavioral separation must also be enforced by fixed, fully reviewed, non-dynamic routine definitions: the issuer may only read the exact challenge/idempotency state, revoke the prior challenge and insert a challenge; it must not create or mutate Account/binding/receipt/audit/role/session state. The claim routine may verify/consume a challenge and perform only the approved atomic Owner-claim writes; it must not issue or rotate challenges. Static definition review and adversarial PostgreSQL 18 tests are mandatory. This caveat does not authorize another role or principal.

### 4.1 Important PostgreSQL privilege caveats

- Column grants must be verified using effective-privilege/catalog queries, not inferred from GRANT statements alone.
- INSERT ... RETURNING requires SELECT privilege on returned columns; only account_id is proposed.
- Foreign-key checks, unique indexes, triggers, row-level security and SECURITY DEFINER execution semantics must be tested on PostgreSQL 18. Do not assume that a narrowly granted function-owner role can perform the proposed writes until the disposable proof confirms it.
- No RLS policy may silently broaden access. The exact RLS state/policies for every touched table must be inventoried and tested.
- Default privileges must prevent PUBLIC or unrelated roles from acquiring EXECUTE on the routine or access to its schema.
- The routine must use a fixed safe search_path with pg_temp last, fully qualified references, no dynamic SQL, strict typed parameters, explicit transaction-safe locking and database-clock expiry checks.

## 5. Two-routine protocol and required transaction order

### 5.1 `issue_initial_owner_challenge_v1(...)`

The issuer is one fixed-signature SECURITY DEFINER routine. Its typed inputs are InstallationId, SetupOperationId, the OS-attested operator SID, exactly 32 bytes of challenge digest, and a challenge_request_id UUID. Purpose is a constant inside the routine; the caller cannot choose purpose or lifetime. The Setup Service generates the raw challenge from the OS CSPRNG, keeps it only in memory, and delivers it once over the authenticated local Named Pipe. Only the digest is passed to PostgreSQL and persisted. The routine uses the PostgreSQL clock and a routine-controlled lifetime no greater than 30 minutes, and returns only challenge_id plus expiry.

In one transaction the issuer acquires the same InstallationId-scoped bootstrap serialization lock used by the claim routine; rejects an existing immutable Owner binding; classifies a repeated challenge_request_id as exact idempotent retry only when all immutable inputs match; fails closed for a conflicting retry; conditionally revokes any prior unconsumed challenge for that same InstallationId + SetupOperationId + purpose by changing only revoked_at_utc; inserts the new verifier-only challenge; and commits before the Setup Service delivers the raw value. An unknown issue commit is reconciled by the same challenge_request_id before delivery. The issuer cannot create Account, binding, receipt, audit, role, permission, session, Recovery or business data.

### 5.2 `claim_initial_product_owner_v1(...)` — atomic claim transaction

The claim routine contract should enforce this sequence within one transaction:

1. Validate typed arguments, fixed purpose, InstallationId, SetupOperationId, authenticated operator SID and payload fingerprint.
2. Acquire the exact same deterministic transaction-scoped bootstrap serialization lock for the InstallationId as the issuer. The lock primitive, key derivation, namespace and collision behavior remain a pre-DDL design/proof item; an accidental hash collision may serialize unrelated installations but must never permit two operations for one installation to bypass mutual exclusion.
3. Read any existing operation receipt and owner binding. Exact same-operation/same-fingerprint replay returns the committed result; every conflicting or second claim fails closed.
4. Lock and validate the exact challenge row; compare verifier in constant time in trusted code or a rigorously reviewed database-safe mechanism; check all bindings, unused state and DB-clock expiry.
5. Insert one final-credential Account with is_bootstrap_account=true and must_change_password=false. The password hash is created by the approved password hasher before invocation; plaintext password is never passed to PostgreSQL or persisted.
6. Insert the immutable Product Owner binding and append-only operation receipt.
7. Consume the challenge conditionally and update the singleton bootstrap completion marker.
8. Insert secret-free IdentityAccess audit intent.
9. Return only the AccountId, InstallationId, SetupOperationId and stable result version. Any error rolls back all steps.

The precise ordering of steps 6–8 may be adjusted to satisfy FK and concurrency constraints, but they must remain one atomic transaction. Unknown commit outcome is reconciled only by the same operation ID and exact payload fingerprint.

## 6. Explicitly excluded objects and actions

The function owner and claim routine must not:
- create or assign roles or permissions, including any OWNER/SUPER_ADMIN grant;
- insert account_roles or role_permissions;
- create sessions, recovery challenges/codes, login attempts or SMTP/recovery state;
- mutate unrelated accounts or read arbitrary account data;
- create Organization/workshop, FiscalPeriod, accounting or business records;
- change N.6 roles, migration_runner membership, schema ownership, database roles or grants;
- install/alter/revoke its own routine or disable the executor;
- use arbitrary SQL, dynamic object names, caller-selected schema/database, shell commands or a generic command dispatcher.

## 7. Migration reservation request to WG-0

Requested reservation candidate: **0010**, subject to WG-0's authoritative sequence check and approval. It is not reserved by this proposal. 0009 remains reserved for Accounting. If WG-0 assigns another number, every reference must be updated before SQL is written.

Candidate migration scope (one IdentityAccess-owned migration):
- create the three bootstrap-specific tables and their constraints/indexes;
- establish immutable binding/receipt semantics and challenge single-use state;
- no changes to existing N.6 operational role grants;
- no production data backfill, role seed, permission seed, Owner account seed or credential creation;
- no function, SECURITY DEFINER routine, principal creation, LOGIN activation, secret or deployment grant in this migration unless a separately reviewed design explicitly requires it. Preferred separation: schema migration creates data structures only; the separately approved local maintenance package installs the routine and exact ACL after WG-0 and implementation gates.

WG-0 must validate global number/path uniqueness, migration ownership, dependency ordering, exact object scope, migration rollback/forward strategy, test plan and checksum/registry updates. The registry currently shows 0009 RESERVED for Accounting and 0010+ UNALLOCATED; therefore this is a request, not a reservation.

## 8. Required design decisions before WG-0 can reserve

1. Confirm the canonical table/column names and exact payload fingerprint algorithm.
2. Challenge issuance is now owner-approved as an IdentityAccess-owned fixed routine; finalize its exact typed signature and the shared lock primitive/key derivation before DDL. Platform remains orchestration-only.
3. Confirm how authenticated Windows operator SID is conveyed to and attested by the local Setup Service without trusting caller-supplied text.
4. Confirm where final Owner profile fields are stored; current accounts schema has email/phone columns but the accepted setup policy's exact profile contract is not yet frozen.
5. Confirm UUIDv7 generation boundary and audit event type/outcome vocabulary.
6. Confirm whether bootstrap_completed_at_utc remains a derived compatibility marker or is replaced by binding existence as the authoritative completion predicate.
7. Prove column-level ACL, FK/index/trigger/RLS behavior, concurrency, idempotency, unknown-commit reconciliation, rollback and no-second-owner behavior on disposable PostgreSQL 18.

## 9. Status

- Existing migration 0003 inventory: **SOURCE-INSPECTED**.
- Challenge issuer/claim architecture and bounded ACL delta: **OWNER-APPROVED CONSTRAINT; DESIGN ONLY**.
- Physical table/column schema and complete function-owner ACL: **DRAFT / NOT IMPLEMENTED OR CATALOG-VERIFIED**.
- WG-0 request: **ISSUE #93 OPEN / NOT RESERVED**.
- Migration 0010: **NOT RESERVED / NO SQL**.
- PostgreSQL 18 tests: **NOT RUN**.
- O46-G04: **OPEN**.
- IdentityAccess G1 / runtime activation: **NOT GRANTED**.

**END — MDG-SECURITY-O46-BOOTSTRAP-PHYSICAL-SCHEMA-AND-ACL-PROPOSAL-001 v0.1**
