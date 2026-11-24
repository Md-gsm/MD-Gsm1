# O-46 — Initial Product Owner Challenge Issuance Protocol Proposal

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-CHALLENGE-ISSUANCE-PROTOCOL-PROPOSAL-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | **TECHNICAL RECOMMENDATION — PRODUCT OWNER DISPOSITION REQUIRED; NOT AUTHORIZED FOR IMPLEMENTATION** |
| Owner | IdentityAccess |
| Related decisions | O-46 principal decision; O-46 bootstrap DB provisioning decision; physical schema/ACL proposal v0.1; provisioning package contract v0.1 |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |
| Target | Windows central server + PostgreSQL 18 |

## 1. Decision to resolve

The one-time initial Product Owner setup requires an unpredictable challenge that is:
- generated from at least 256 bits of CSPRNG entropy;
- delivered exactly once to the local first-run Wizard over the authenticated local channel;
- persisted only as a verifier/digest, never as raw challenge;
- bound to the immutable InstallationId, SetupOperationId, fixed purpose and OS-authenticated operator SID;
- valid for no more than 30 minutes;
- consumed atomically with the initial Account and immutable Owner binding; and
- unusable for any second claim, different operation or installation.

IdentityAccess owns the challenge state and the claim decision. Platform may transport InstallationId and SetupOperationId and coordinate first-run orchestration; it does not own the authoritative challenge state.

The current approved DB contract authorizes the bootstrap executor for one exact claim routine only. That leaves no authorized database caller for the initial challenge INSERT. This proposal resolves the caller path without adding a PostgreSQL principal or giving the executor direct table access, but it requires an explicit Product Owner decision because it adds one narrowly typed routine capability and a corresponding function-owner INSERT/limited UPDATE privilege.

## 2. Recommended design

**Use two separate, fixed-signature SECURITY DEFINER routines, both callable only by the existing `mdgsm_bootstrap_executor` principal.**

1. `identity_bootstrap.issue_initial_owner_challenge_v1(...)`
2. `identity_bootstrap.claim_initial_product_owner_v1(...)`

Both routines are owned by `mdgsm_identity_bootstrap_fn_owner` (NOLOGIN). The executor receives only schema USAGE and EXECUTE on these two exact signatures. It receives:
- no table or sequence privileges;
- no schema CREATE;
- no role membership;
- no DDL or role-administration capability;
- no arbitrary SQL or caller-selected object/operation dispatch;
- no permission to create accounts, roles, sessions or business records except the single fixed initial Owner claim capability.

No new persistent principal, login, membership, service credential or deployment root is introduced. The existing executor remains initially NOLOGIN and is only separately enabled under the already approved secret-custody, SCRAM/HBA, loopback and negative-test gates. Independent post-claim revocation remains mandatory.

### Why two routines rather than one mode-dispatch routine

A single routine with an `operation`/phase parameter would combine issuance and claim under a dispatcher-like surface. Even with a closed enum, it makes review of accepted inputs, side effects and ACL boundaries less direct. Two routines expose two auditable, fixed capabilities and permit separate tests and versioning. This is not permission for a generic routine family: only the two named, exact signatures are in scope.

## 3. Challenge creation and transport

### 3.1 Trusted generation

The dedicated Setup Service generates exactly 32 random bytes using the operating system CSPRNG through the platform cryptographic API (for Windows/.NET, the supported `RandomNumberGenerator` API). It computes SHA-256 over those 32 bytes.

- Raw challenge exists only in Setup Service memory until it is delivered once to the Wizard.
- The raw challenge is not sent to PostgreSQL, written to disk, placed in process arguments/environment, logged, telemetered or included in evidence.
- Only the fixed-length 32-byte SHA-256 digest is sent to the issue routine and stored in `identity.bootstrap_challenges.verifier_digest`.
- The digest is not a low-entropy password verifier: it represents a uniformly random 256-bit bearer value. The separate unresolved payload-fingerprint question remains governed by its own decision register.

The Setup Service must use a cryptographically secure API, fail closed if generation fails, clear mutable buffers where practical, and never return the raw challenge after the single local Wizard delivery. Managed-memory zeroization limitations must be documented; no claim of guaranteed erasure from all process memory is made.

### 3.2 Issue routine inputs

The exact proposed typed inputs are:
- InstallationId (UUID);
- SetupOperationId (UUID);
- authenticated operator SID (canonical Windows SID text);
- challenge digest (exactly 32 bytes);
- challenge request identifier (UUID, idempotency key).

The purpose is a constant routine-owned value such as `initial_product_owner_claim`; the caller cannot choose a purpose or pass an arbitrary lifetime. The routine uses the PostgreSQL clock and a fixed maximum lifetime of 30 minutes. It returns only the challenge identifier and database expiry timestamp, never the raw challenge or digest.

The Setup Service obtains the SID from the OS-authenticated Named Pipe client token using the already specified impersonate-for-inspection/revert pattern. It must not accept a SID supplied by Wizard fields or an API request. The service binds the same SID to the challenge and re-attests the caller on the later claim request.

### 3.3 Persistence and retry semantics

The issue routine performs one transactionally bounded state transition:
1. acquire the bootstrap serialization lock;
2. reject if an immutable Product Owner binding already exists for the InstallationId, or if an incompatible committed operation receipt exists;
3. validate the exact InstallationId, SetupOperationId, SID and 32-byte digest input;
4. if the same challenge request identifier already exists with identical immutable inputs, return its original challenge ID/expiry without creating another row;
5. if the same request identifier is reused with different inputs, fail closed;
6. revoke any prior unconsumed challenge for the same InstallationId + SetupOperationId + fixed purpose, using only the permitted `revoked_at_utc` transition;
7. insert the new verifier-only challenge with database-generated created/expiry timestamps; and
8. commit before the Setup Service delivers the raw challenge to the Wizard.

A new challenge request identifier may supersede an earlier unconsumed challenge for the same setup operation. The old challenge is irreversibly revoked; it is never updated, rebound or reactivated. A failed/unknown issue commit is reconciled by the same challenge request identifier before delivery. The service must not deliver a challenge until the issue routine has returned a committed result.

The database must serialize issuance against claim so that a challenge cannot be issued concurrently with a successful Owner claim. The exact lock object/order and PostgreSQL 18 proof must be specified in the final routine contract; do not infer that the existing schema marker alone is authoritative.

## 4. Claim routine

The claim routine receives typed InstallationId, SetupOperationId, challenge ID, challenge digest, operator SID and the approved final Owner account inputs/hash. It:
- re-attests/binds the same SID at the Setup Service boundary;
- locks and validates the authoritative bootstrap state using PostgreSQL time;
- verifies challenge ID, InstallationId, SetupOperationId, fixed purpose, SID, digest, not-expired, not-revoked and not-consumed state;
- enforces one-time conditional consumption;
- creates the initial Account, immutable Product Owner binding, idempotent operation receipt and in-transaction audit intent atomically;
- returns the same committed AccountId/result for an exact same-operation retry after an unknown commit; and
- denies replay, a different operation, a different installation, altered payload or any second Owner claim.

The raw challenge never enters PostgreSQL. The Setup Service computes the digest for the returned Wizard challenge and passes only the digest to the claim routine. The challenge is a high-entropy local bearer value; there is no remote challenge-verification endpoint. Any claim that byte comparison is constant-time must be proven for the exact implementation; otherwise the threat review must document the bounded local-channel timing exposure and decide whether an approved constant-time comparison primitive is required.

Challenge expiry and consumption are checked using the database clock in the same transaction as the Owner claim. A Setup Service timeout is not success; it triggers read-only reconciliation by the same SetupOperationId and challenge request identifier. No new Owner or binding may be created to resolve an unknown commit.

## 5. Minimum additional function-owner ACL delta

Relative to the physical schema proposal v0.1, this recommendation requires the following narrowly bounded additional capabilities for `mdgsm_identity_bootstrap_fn_owner`:

| Object | Additional capability proposed | Explicitly denied |
|---|---|---|
| `identity.bootstrap_challenges` | INSERT only on exact challenge columns: challenge_id (if not DB-generated), installation_id, setup_operation_id, purpose, operator_sid, verifier_digest, created_at_utc, expires_at_utc; UPDATE only `revoked_at_utc` for conditional revocation of an unconsumed row | SELECT/UPDATE of verifier, binding, expiry or consumed state beyond the already proposed claim verification/consumption columns; DELETE; TRUNCATE |
| `identity.product_owner_bindings` | No change | UPDATE; DELETE; TRUNCATE |
| `identity.bootstrap_operations` | No change | UPDATE; DELETE; TRUNCATE |
| `identity.accounts` | No change | UPDATE; DELETE; role/session/recovery access |

The exact column-level ACL must be reconciled into the physical schema proposal and verified from PostgreSQL catalogs. The executor receives no direct privilege on any of these objects. The issue routine must not be able to insert an Account, Owner binding, role, session, permission or operation receipt.

The existing SECURITY DEFINER safety rules remain unchanged: fixed safe `search_path` with `pg_temp` last, fully qualified object references, no dynamic SQL, strict typed parameters, no caller-selected identifiers, PUBLIC EXECUTE revoked, no unexpected default privileges, exact owner and function-definition fingerprint, and effective-privilege catalog verification.

## 6. Serialization and race controls

Both routines must use one documented IdentityAccess-local serialization protocol. The recommended protocol is:
- acquire the same stable bootstrap guard row/lock first in both routines;
- then access binding, operation receipt and challenge state in one fixed order;
- perform no cross-module call while holding the lock;
- use database time for expiry and all state transitions;
- rely on unique constraints as a final invariant, not as a replacement for explicit conflict classification.

This is a local one-time bootstrap serialization rule, not approval of a global cross-module lock order. PostgreSQL 18 tests must cover concurrent issuers, issue-vs-claim, concurrent claims, expired/revoked/replayed challenge, duplicate request IDs, unknown commit and transaction rollback.

## 7. Alternatives considered

| Option | Disposition | Reason |
|---|---|---|
| Give executor direct INSERT/UPDATE on challenge table | **Reject** | Breaks the approved no-direct-table-access boundary and exposes broader persistence capability. |
| Add a third persistent database principal for challenge issuance | **Reject** | Unnecessary principal and secret/rotation/revocation surface; conflicts with the accepted two-principal design. |
| Let Platform own or write challenge state | **Reject** | Violates IdentityAccess ownership of challenge binding, expiry and single-use claim state. |
| One generic operation dispatcher | **Reject** | Broadens review surface and weakens exact capability clarity. |
| Two fixed SECURITY DEFINER routines, same executor, no table access | **Recommend; owner decision required** | Narrowest clear extension using the already accepted principal pair; separate typed capabilities and testable ACLs. |

## 8. Product Owner decision requested

Please accept or reject this exact bounded change:

**Accept two exact SECURITY DEFINER routines—one challenge issuer and one Owner claim—both executable only by the already approved `mdgsm_bootstrap_executor`; no new principal; no direct executor table privileges; add only the function-owner challenge INSERT and `revoked_at_utc)-only UPDATE described above.**

If accepted, the physical schema/ACL proposal and provisioning package contract must be revised together before any SQL/DDL is authored. If rejected, return the design for another option; do not silently broaden the executor, add a principal, or implement a generic dispatcher.

## 9. Explicit non-authorization and current status

This proposal does not authorize:
- SQL, DDL, migration authoring/application or WG-0 reservation changes;
- creation/alteration of principals, memberships, grants, credentials or secrets;
- creation/installation/activation of either routine;
- executor LOGIN activation or Setup Service DB credential provisioning;
- Owner Account/role/permission seed, Recovery, Organization, Inventory, G1 or production runtime;
- treating this technical recommendation as Product Owner approval, independent review or test evidence.

- Challenge state ownership: **IdentityAccess**.
- Issuance mechanism: **recommended, awaiting Product Owner decision**.
- Additional exact routine/ACL: **not approved / not implemented**.
- WG-0 Issue #93: **open; 0010 not reserved**.
- PostgreSQL 18 and Windows proofs: **NOT RUN / NOT VERIFIED**.
- O46-G04 and IdentityAccess G1: **OPEN / NOT GRANTED**.
- PR #92: **must remain Draft and unmerged**.

**END — MDG-SECURITY-O46-CHALLENGE-ISSUANCE-PROTOCOL-PROPOSAL-001 v0.1**
