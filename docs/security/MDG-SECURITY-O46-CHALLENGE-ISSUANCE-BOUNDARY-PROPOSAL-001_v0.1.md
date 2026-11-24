# O-46 — Challenge Issuance Boundary Proposal

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-CHALLENGE-ISSUANCE-BOUNDARY-PROPOSAL-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | **ENGINEERING RECOMMENDATION — PRODUCT OWNER DISPOSITION REQUIRED; NOT AUTHORIZED FOR IMPLEMENTATION** |
| Related | O-46 provisioning decision v0.1; provisioning package contract v0.1; physical schema/ACL proposal v0.1; physical schema decision register v0.1 |
| Scope | Define the missing runtime caller for challenge issuance without a new principal, direct table access, cluster-admin exposure or generic dispatcher |

## 1. Decision sought

Approve or reject this bounded design:

**Two separately named, separately typed SECURITY DEFINER routines in the isolated `identity_bootstrap` schema, both callable only by the existing `mdgsm_bootstrap_executor`:**

1. `issue_owner_bootstrap_challenge_v1(...)`
2. `claim_initial_product_owner_v1(...)`

The executor receives EXECUTE only on these two exact signatures. It receives no table, sequence, schema CREATE, role, membership or other function privileges. No third principal, persistent deployment LOGIN, generic SQL/operation dispatcher or Platform-owned challenge state is introduced.

This expands the current approved executor capability from one exact claim routine to two exact bootstrap routines. **That expansion requires explicit Product Owner approval before the package contract, function ACL or implementation design is revised.**

## 2. Why the current design is incomplete

The accepted policy requires challenge state to be persisted verifier-only and bound to InstallationId, SetupOperationId, fixed purpose and authenticated local operator SID. Wizard and Setup Service may not write IdentityAccess tables directly. The function-owner role is NOLOGIN. The current executor contract permits only the claim routine. Therefore, no currently authorized runtime caller can perform the initial challenge INSERT.

Granting direct table INSERT to the executor, letting Platform own authoritative challenge state, exposing the NOLOGIN function-owner role, using cluster-admin at runtime, or adding a persistent LOGIN would violate existing boundaries.

## 3. Proposed end-to-end flow

1. Wizard connects only to the local Setup Service through the approved Windows Named Pipe. Remote clients remain rejected.
2. Setup Service authenticates the local caller from the pipe OS security context, extracts the exact SID, then reverts impersonation. SID is never accepted from Wizard-supplied profile data.
3. Setup Service creates a 256-bit challenge with the OS CSPRNG and computes SHA-256 over its bytes. The raw challenge exists only in Setup Service memory.
4. Setup Service calls `issue_owner_bootstrap_challenge_v1` using its exclusive executor DB credential. Typed inputs: ChallengeId, InstallationId, SetupOperationId, fixed purpose, operator SID and 32-byte verifier digest. PostgreSQL assigns created/expiry timestamps from its own clock; lifetime is fixed and no more than 30 minutes.
5. After successful persistence, Setup Service delivers the raw challenge exactly once to the local Wizard over the authenticated pipe. The database routine never returns the raw challenge or digest.
6. Wizard collects final Owner username/password/profile and returns the raw challenge with the typed claim request to Setup Service. Setup Service hashes the challenge and calls `claim_initial_product_owner_v1`.
7. Claim verifies InstallationId, SetupOperationId, purpose, SID, digest, expiry, non-revoked and unconsumed state using PostgreSQL time. It atomically creates Account, immutable Owner binding, append-only receipt, audit intent, marker update and challenge consumption.
8. The independent maintenance actor performs the already-approved post-claim executor revocation and verifies credential deletion, backend termination and channel closure. Executor cannot revoke itself.

The database never receives the raw challenge. Wizard never receives the executor credential. Cluster-admin credentials remain exclusively with the separate local maintenance actor.

## 4. Exact routine boundaries

### 4.1 Issuer routine

The issuer is fixed-purpose: no caller-selected schema/table/SQL, arbitrary object argument or operation name. It may only:

- verify that no immutable Owner binding exists for the InstallationId;
- serialize issuance for that InstallationId;
- validate typed identifiers, fixed purpose, SID shape and exact 32-byte digest;
- insert a challenge with database-generated timestamps;
- enforce at most one unexpired, unconsumed, non-revoked challenge per InstallationId;
- return a typed success/expiry result, never the raw challenge or verifier;
- treat an exact retry with the same ChallengeId and immutable inputs as idempotent; and
- reject conflicting retries, a different live challenge, or issuance after an Owner binding exists.

It cannot create accounts, bindings, roles, permissions, sessions, recovery state or business data. It cannot update or delete challenge rows.

### 4.2 Claim routine

Claim remains the only capability that can create the initial Owner Account and binding. It must:

- reconcile an already-committed identical SetupOperationId/payload fingerprint before rejecting a consumed challenge, so unknown-commit retries return the exact committed AccountId/result;
- reject same operation ID with a different fingerprint;
- reject expired, revoked, consumed, mismatched or wrong-purpose challenges;
- conditionally consume the challenge and create Account, immutable binding, receipt and audit intent in one transaction;
- enforce the single-owner invariant under concurrency; and
- create no role/permission grants, sessions, recovery capability, Organization, fiscal period or other business data.

### 4.3 PostgreSQL ACL

The existing `mdgsm_bootstrap_executor` remains the only runtime DB LOGIN for this path and remains non-elevated, non-owner and without memberships. Its complete application ACL is exactly EXECUTE on the two named signatures and nothing else.

The NOLOGIN `mdgsm_identity_bootstrap_fn_owner` owns the isolated schema and both routines. It receives only minimum column-level privileges:

- issuer: INSERT on exact challenge columns and SELECT only columns needed for idempotent/live-challenge checks;
- claim: SELECT exact verification columns and conditional UPDATE of `consumed_at_utc` only, plus previously proposed exact Account/binding/receipt/audit/marker privileges;
- neither routine gets UPDATE/DELETE/TRUNCATE on Owner binding or receipt;
- executor gets no direct table/sequence privileges;
- PUBLIC and unrelated roles get no EXECUTE;
- pinned safe `search_path`, fully qualified references, no dynamic SQL, fixed typed signatures and effective-catalog verification remain mandatory.

The physical schema ACL proposal must be revised to include issuer privileges only after this design is accepted. This proposal does not authorize those grants.

## 5. Retry, expiry and interruption

- **Issuer unknown commit:** retry identical ChallengeId, SetupOperationId, InstallationId, SID and digest; return existing matching result without duplicate insertion.
- **Setup Service restart after issue:** raw challenge is lost from memory by design and cannot be recovered from DB. The existing challenge expires within 30 minutes. A new challenge may be issued for the same setup operation only after the previous one expires; new ChallengeId and digest are required. No second live challenge is allowed.
- **Claim unknown commit:** retry same SetupOperationId and identical payload fingerprint; return the committed receipt even though challenge is consumed.
- **Claim rollback:** Account, binding, receipt, audit intent and challenge consumption roll back together; same challenge is retryable only while valid.
- **Expiry:** PostgreSQL clock is authoritative; an expired challenge cannot be revived or extended.
- **Concurrency:** unique constraints plus transaction-level serialization and conditional state transitions ensure at most one Owner binding. Exact locking strategy requires PostgreSQL 18 proof.
- Raw challenge recovery through support, CLI, direct SQL or maintenance is prohibited.

## 6. Required acceptance tests — all NOT RUN / NOT VERIFIED

1. Executor can execute only the two exact signatures; direct table/sequence access and all other function calls are denied.
2. Function owner remains NOLOGIN and cannot create roles or alter its own grants.
3. PUBLIC, API runtime, migration runner, Wizard and unrelated roles cannot execute either routine.
4. Named Pipe remote rejection, exact SID ACL, impersonation/revert and Setup Service process authentication pass on supported Windows Server.
5. Challenge entropy is 256 bits from OS CSPRNG; only digest persists; raw challenge is absent from DB, logs, telemetry, arguments, environment and evidence.
6. Issuer rejects malformed digest, wrong purpose/InstallationId, invalid SID, existing Owner binding and concurrent live challenge.
7. Exact issuer retry is idempotent; conflicting retry is denied.
8. Restart after issuance cannot recover raw challenge; expired challenge cannot be used/revived; new issue is allowed only after expiry.
9. Claim rejects wrong digest, SID, InstallationId, SetupOperationId, purpose, expiry, revocation and replay.
10. Concurrent issue/claim yields at most one Owner binding.
11. Unknown-commit claim retry returns exact same AccountId/receipt; conflicting payload retry fails.
12. Failure rolls back Account, binding, receipt, audit intent and challenge consumption together.
13. Reinstall, Repair, update and restore preserve InstallationId and existing immutable Owner binding.
14. Independent executor revocation, backend termination, credential deletion and evidence checks pass.
15. PostgreSQL 18 catalog/ACL/locking/FK/trigger/RLS/SECURITY DEFINER proofs pass on exact implementation commit.

## 7. Alternatives

| Option | Disposition | Reason |
|---|---|---|
| Give executor direct challenge-table INSERT | Reject | Breaks no-direct-table-access and least privilege. |
| Let Platform own challenge state | Reject | Violates IdentityAccess data ownership. |
| Use cluster administrator at runtime | Reject | Violates maintenance-only cluster-admin boundary. |
| Add another persistent runtime/deployment LOGIN | Reject | Expands approved principal graph. |
| Generic SQL/operation dispatcher | Reject | Unbounded capability surface. |
| One routine with caller-selected operation mode | Not recommended | Risks turning the exact claim routine into an ambiguous dispatcher. |
| Two separately named exact routines under the same executor | **Recommended, subject to Owner approval** | Explicit, independently reviewable capabilities; no new principal, direct table access or generic dispatcher. |

## 8. Required Owner decision and non-authorization

- **APPROVE:** authorize controlled design/package/ACL revision so the existing executor may execute exactly the two routines defined here, with all other restrictions unchanged.
- **REJECT:** retain the one-routine boundary and return to architecture review without implementing an alternate path.

Approval would authorize only controlled design-document revisions. It would not authorize SQL, DDL, migration 0010, role/grant changes, secret provisioning, executor LOGIN activation, Windows deployment, runtime setup, Owner creation, Recovery, G1 or production activation.

WG-0 reservation, exact PostgreSQL 18 proof, Windows host proof, exact-head CI and independent Security/Architecture review remain separate gates. PR #92 remains Draft and unmerged.

**END — MDG-SECURITY-O46-CHALLENGE-ISSUANCE-BOUNDARY-PROPOSAL-001 v0.1**
