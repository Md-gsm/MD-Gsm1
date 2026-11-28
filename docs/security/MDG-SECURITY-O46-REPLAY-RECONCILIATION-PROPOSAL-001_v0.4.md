# O-46 — Exact-Replay Reconciliation Without a Third PostgreSQL Principal

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-REPLAY-RECONCILIATION-PROPOSAL-001 |
| Version / date | 0.4 / 2026-11-28 |
| Status | **REVISED DESIGN PROPOSAL — REQUEST CHANGES / NOT ACCEPTED; implementation, policy acceptance and independent review remain open** |
| Supersedes | v0.3 only; v0.1, v0.2 and v0.3 remain immutable history |
| Tracking | Issue #118; replacement for the Reader/View reconciliation proposal |
| Baseline | `main` `ae63f4ca131b1e7f52ba0b2af86631c7730c5ef4` |

## 0. Review disposition

PR #124 v0.1 was rejected. v0.2 defined the first transaction/replay contract; v0.3 recorded the two Product Owner decisions on same-SID second-session denial and fail-closed behavior after Journal loss. This v0.4 is a new, immutable proposal revision that addresses the technical pre-review by specifying a service-side Windows logon-session evidence tuple, explicit pipe-token capture and impersonation handling, a conservative service-restart boundary, and a TPM-counter/Journal crash state machine.

This revision does **not** claim that the proposed Windows token flow, TPM protocol, file durability assumptions, recovery procedure, routine signatures, function ACLs or deployed fingerprints have been implemented or proven. The service-restart limitation is deliberately conservative and is **not a new Product Owner decision**; Product Owner acceptance is required before implementation adopts it. The Cluster Administrator recovery boundary is not an available procedure. All implementation and authorization gates remain open.

## 1. Owner decision and hard boundaries

The Product Owner prohibits a third PostgreSQL principal/LOGIN for O-46 Reconciliation.

- No third principal, LOGIN, membership, role, View, grant or privilege expansion.
- No table/View SELECT for `mdgsm_bootstrap_executor`; no expansion of `mdgsm_runtime`; no runtime use of `migration_runner`.
- Exactly the two existing design-approved routines only; no third SECURITY DEFINER routine, generic dispatcher, dynamic SQL or new public endpoint.
- Executor retains only its already-authorized EXECUTE rights on the exact two routine signatures.
- The existing shared NOLOGIN function-owner and its aggregated ACL risk remain subject to code-integrity review and hostile tests.
- Migration `0010` remains `RESERVED — SQL NOT AUTHORIZED`. No source, SQL/DDL, role, grant, key, credential, Wizard, Owner binding, Login, feature flag or runtime activation is authorized.

## 2. Reconciliation model

The separate Reader, View-owner and shared-lock Reader path are removed from the target architecture. The Executor never reads bootstrap tables. Unknown outcomes are reconciled by replaying the same immutable command through the existing Claim routine. PostgreSQL binding and receipt remain authoritative.

Replay is **not read-only**: it may create the one permitted initial Owner when no prior claim exists and the exact challenge is still valid. That behavior is part of this proposal and must be explicitly accepted in the independent review. It can never create a second Account or Owner binding.

## 3. Routine contract — proposed, not source-verified

The following parameter/type/order contract is the proposed interface to freeze before implementation. It must be checked against the accepted schema and owner-approved Fingerprint v1; no production definition currently exists to hash or verify. If any parameter or result must change, issue a reviewed successor rather than silently diverging.

### 3.1 Issue

`identity_bootstrap.issue_initial_owner_challenge_v1(`

`p_installation_id uuid,`

`p_setup_operation_id uuid,`

`p_challenge_request_id uuid,`

`p_challenge_id uuid,`

`p_local_sid text,`

`p_purpose text,`

`p_verifier bytea,`

`p_request_fingerprint bytea)`

`RETURNS TABLE(outcome text, challenge_id uuid, expires_at_utc timestamptz)`

- Caller/Setup Service generates the 32-byte raw challenge, ChallengeId and ChallengeRequestId using CSPRNG/UUID generation before the first DB call. The raw challenge is never returned by PostgreSQL.
- The verifier is the approved SHA-256 digest of the challenge under the exact v1 domain/context encoding; the encoding and fingerprint canonicalization must be frozen in the signed contract.
- Database time sets expiry, no later than 30 minutes from issuance.
- Exact replay of the same ChallengeRequestId and identical immutable inputs returns the same ChallengeId/expiry without mutation. Same request ID with altered fields returns `CONFLICT`. A lost Issue response is recovered by replaying this exact Issue request; it does not mint a new challenge.
- The Issue routine stores only the verifier, not raw challenge bytes.

### 3.2 Claim

`identity_bootstrap.claim_initial_product_owner_v1(`

`p_installation_id uuid,`

`p_setup_operation_id uuid,`

`p_challenge_id uuid,`

`p_local_sid text,`

`p_purpose text,`

`p_raw_challenge bytea,`

`p_account_id uuid,`

`p_username text,`

`p_password_hash text,`

`p_display_name text,`

`p_email text,`

`p_request_fingerprint bytea)`

`RETURNS TABLE(outcome text, account_id uuid, receipt_id uuid)`

The Claim signature is a fixed, non-dynamic interface. Password hash is the final versioned hash generated once by the Wizard; it is not recomputed on retry. Username/profile/email and AccountId are immutable for this SetupOperationId/ChallengeId/fingerprint.

### 3.3 Routine definition requirements

Both routines must be `VOLATILE SECURITY DEFINER`, owned by the existing NOLOGIN function-owner, with fixed safe function configuration and fully schema-qualified object references. No untrusted schema may precede trusted objects in `search_path`; no dynamic SQL. The exact owner, `proconfig`, `provolatile`, `prosecdef`, ACL (including PUBLIC), column grants, effective EXECUTE privileges and signed function-definition/package fingerprints must be captured from the final definitions and independently reviewed.

**No exact production signature, body hash or effective ACL is claimed by this document.** The above is a proposed typed contract only; gate A remains open until the definitions and catalog evidence exist and match.

## 4. Transaction, isolation and lock contract

- Every Issue and Claim call must execute in a short `READ COMMITTED` transaction. Reject a transaction whose `transaction_isolation` is not `read committed`; do not attempt to repair it silently.
- Both routines are `VOLATILE`. PostgreSQL 18.6 tests must prove that each internal state-reading command after lock acquisition observes a fresh READ COMMITTED snapshot, including after waiting for a competing transaction.
- Before the first bootstrap-state read, both routines acquire the same exclusive transaction advisory lock derived inside PostgreSQL:
  
  `pg_catalog.hashtextextended('MDGSM:IDENTITYACCESS:O46:OWNER-BOOTSTRAP:V1:' || lower(p_installation_id::text), 0::bigint)`

  followed by `pg_catalog.pg_advisory_xact_lock(key)`.
- The namespace string and seed above are canonical for v1. The 64-bit collision policy is conservative: collision may serialize unrelated installations (false contention), but must never split the same InstallationId into different locks. No caller-supplied key and no .NET-side derivation.
- Lock order: transaction-level InstallationId advisory lock first; then reads/row locks of binding, receipt, challenge in one documented fixed order. No row lock may be acquired before the advisory lock. No network, UI, TPM or secret-store operation while holding DB lock.
- Lock timeout/deadlock/cancel is an infrastructure error, not a business outcome. It must abort/rollback; client classifies the result as unresolved and replays only the same operation.
- Prove existing effective `pg_catalog` EXECUTE permissions. Do not add a grant to make the expression work. If the approved Executor cannot call the required primitives under existing ACLs, stop for owner/security decision.

## 5. Claim decision table and commit semantics

Under the exclusive lock, inspect durable binding and receipt **before** evaluating challenge expiry or consumed status.

| Order | Durable state / proof | Routine outcome | Client action |
|---|---|---|---|
| 1 | Exactly one binding + receipt match InstallationId, SetupOperationId, ChallengeId, AccountId and fingerprint | `COMMITTED_MATCH` | Return original AccountId/receipt; no mutation. Expiry/consumed challenge does not invalidate this already-committed match. |
| 2 | Any partial, duplicate or mismatched binding/receipt; consumed challenge without matching receipt; same identifiers with changed payload | `CONFLICT` | Fail closed; preserve evidence; no repair/new Owner. |
| 3 | No binding/receipt; exact challenge exists and is live by DB time; verifier, SID, purpose and immutable payload match | `CLAIMED_NOW` (provisional routine result) | Commit the transaction. Treat as success only after successful COMMIT acknowledgement. |
| 4 | No binding/receipt; exact challenge expired/revoked by DB time | `EXPIRED_OR_ABANDONED` | Do not reuse challenge. A new operation is allowed only after terminal proof and no binding/receipt. |
| 5 | No binding/receipt; challenge missing, wrong or unclassifiable | `CONFLICT` | Fail closed; do not mint replacement automatically. |

`CLAIMED_NOW` is returned from the function before the enclosing transaction's commit is known. It is **not** a success signal by itself. Only a successful transaction COMMIT acknowledgement is a confirmed client success. If the response/ack is lost, the client state is `UNKNOWN` and it exact-replays.

`UNKNOWN` is **client-side only**, never a routine return value. It means transport failure, DB unavailable, lock/transaction error, lost commit acknowledgement, invalid/unavailable Journal or unclassifiable client evidence. SQL/business outcomes, SQL exceptions and transport/Journal states must remain separate. No DB routine returns `UNKNOWN`.

## 6. Issue idempotency and cross-operation serialization

- Setup Service creates the immutable operation identifiers and challenge bytes locally. The Issue request's canonical fingerprint covers InstallationId, SetupOperationId, ChallengeRequestId, ChallengeId, SID, purpose, verifier and contract version.
- Issue response loss is handled by exact replay of the same ChallengeRequestId and payload. The database never needs to reveal/reconstruct raw challenge bytes.
- Under the same exclusive lock, Issue rejects an existing Owner binding, conflicting request reuse, and any different SetupOperationId with a live unrevoked/unconsumed challenge. It must not silently revoke another operation's challenge.
- A new SetupOperationId is allowed only after the prior challenge is proven expired/revoked by database time and no binding/receipt exists. Unknown commit or live challenge means same-operation replay only.

## 7. Write-ahead Journal and crash ordering

Before the first Issue request, and therefore before any Claim request, the Setup Service must durably persist and verify a complete protected Journal record containing at least:

- InstallationId, SetupOperationId, ChallengeRequestId, ChallengeId, contract version and canonical request fingerprint;
- raw challenge bytes (protected), original challenge verifier/context, purpose;
- final AccountId, username/profile/email, final versioned password hash and its original salt/hash parameters;
- initiating OS SID plus the approved Windows logon-session binding;
- state/sequence and receipt fields when later known.

Required order:

1. Generate immutable identifiers, raw challenge and final credential/hash exactly once.
2. Build and authenticate/encrypt the complete Journal record.
3. Flush durable storage, atomically publish the record, reopen/read it and verify integrity and exact fields.
4. Only then call Issue. Issue response loss -> exact Issue replay.
5. Before Claim, persist/verify the exact Claim payload and state transition.
6. Send Claim. Successful COMMIT acknowledgement -> persist committed receipt state. Lost response/ack -> exact Claim replay; never regenerate hash/challenge/AccountId.

Power-cut tests must inject failure before, during and after each Journal write/flush/rename and around Issue/Claim/COMMIT. Partial records are rejected; no request is sent from an unverified record.

## 8. Journal cryptography and anti-rollback

Preserve the previously owner-approved target; do not weaken it:

- CNG Microsoft Platform Crypto Provider with TPM 2.0; non-exportable key material and service-SID ACL.
- AES-256-GCM for confidentiality/authentication; DEK wrapped by TPM-protected key.
- Separate ECDSA P-256/SHA-256 signing key for integrity; signing and encryption keys are distinct.
- Hash-chain records for tamper evidence; **hash-chain alone does not prevent rollback to an older valid Journal**.
- Anti-rollback uses a TPM-backed monotonic NV counter bound to the signed Journal sequence/checkpoint. No software, exportable-key or DPAPI fallback is permitted. The counter/index must never be reset, decremented, silently reprovisioned or replaced after loss.
- Provisioning, NV-index attributes/authorization, key rotation and lifecycle are not authorized here. They require a separate reviewed implementation plan and proof on the target TPM/platform.

### 8.1 Proposed monotonic Journal state machine — not yet implementation-approved

For one InstallationId, let `C` be the authoritative TPM NV counter. The last committed Journal record has sequence `C`. Only one Journal transition may be pending at a time; a local serialization primitive must prevent concurrent writers. Each transition from `n` to `n+1` follows this order:

1. Construct the complete next record in memory, including sequence `n+1`, previous-record hash, operation/fingerprint, exact state transition and all required protected payload. Authenticate/encrypt and sign it using the approved, separate CNG keys.
2. Write a separate **PREPARED** record for `n+1` without overwriting the last **COMMITTED** record for `n`. Flush the file, publish it using a same-volume durability strategy, reopen it and verify its complete bytes, signature, AEAD tag, sequence, previous hash and immutable operation fields. No TPM increment is issued until this PREPARED record has passed read-back verification.
3. Issue exactly one TPM NV increment for this pending transition. If the command result/response is lost, read the authoritative NV counter before deciding anything; never blindly issue a second increment.
4. Once the counter is proven to equal `n+1`, publish/finalize the corresponding COMMITTED record from the already authenticated PREPARED bytes, flush, reopen and verify it. Retain the prior committed record until the new committed record is verified. Remove the pending record only after final verification and only under a separately proven safe cleanup ordering.

Recovery classification is based only on authenticated records and an authoritative TPM counter read:

| Observed state | Permitted disposition |
|---|---|
| `C=n`, COMMITTED `n`, no pending record | Healthy; the next transition may begin. |
| `C=n`, COMMITTED `n`, one valid PREPARED `n+1` matching the prior hash and exact operation | The TPM increment is not reflected in the counter. Only the exact pending transition may be retried; no new operation or changed payload is permitted. |
| `C=n+1`, COMMITTED `n`, one valid PREPARED `n+1` matching the prior hash and exact operation | The TPM increment succeeded. Finalize only from those exact PREPARED bytes; do not regenerate payload, repeat a business/DB Claim, or increment again. |
| `C=n+1`, COMMITTED `n+1`, no pending record | Healthy after finalization. |
| Counter ahead by more than one; counter behind the highest committed sequence; missing/multiple pending records; invalid signature/tag/hash; unexpected sequence; missing/reset TPM index/key; or an older backup without the exact matching PREPARED record | **BLOCKED / FAIL CLOSED.** No counter reset/decrement, automatic reconstruction, new challenge, new Owner or software fallback. |

A failed rename/publication after the NV increment is therefore recoverable only when the exact, previously flushed and authenticated PREPARED `n+1` record still exists and verifies against the TPM counter. If the PREPARED record is lost/corrupt, or a restored backup does not contain it, the counter-ahead state is blocked. A matching PREPARED record may be finalized as journal bookkeeping only; it does not by itself authorize continuing the Windows Wizard or replaying a database Claim.

This protocol depends on proving that the chosen Windows filesystem and publication operations make the PREPARED data durable before the TPM increment. A successful `FlushFileBuffers`, an atomic rename, or an immediate read-back must not be assumed to prove power-loss durability of both file contents and directory metadata. Exact Windows API ordering, filesystem assumptions, backup semantics and power-cut evidence remain implementation gates. If durable PREPARED publication cannot be demonstrated on every supported target, this counter protocol is not acceptable and the flow remains disabled.

## 9. Windows logon-session binding and pipe-token provenance

Every Wizard request must derive its identity from the **authenticated server-side token of the Named Pipe client**. A caller-supplied SID, PID, SessionId, LUID or Journal field is never authentication evidence.

### 9.1 Captured identity tuple

The proposed immutable session-binding tuple is:

- SID returned by `GetTokenInformation(TokenUser)`;
- logon AuthenticationId LUID returned by `GetTokenInformation(TokenStatistics)`;
- Windows Terminal Services session ID returned by `GetTokenInformation(TokenSessionId)`;
- a cryptographically random ServiceInstanceId created once when the Windows service starts and held by the service, not supplied by the client.

The AuthenticationId is the logon-session identifier, not the token-object TokenId. TokenId, process ID, window handle, username, SID alone, and Terminal Services SessionId alone must not be treated as a substitute for the tuple. The SID is also the only source for the existing `p_local_sid` parameter; the value must be derived by the service from the pipe client's token, never accepted from the request body.

### 9.2 Required server-side capture sequence

For each complete request message on the local-only pipe, the service must:

1. Call `ImpersonateNamedPipeClient` on the server pipe handle corresponding to that message. If it fails, reject the request; never continue under the service process identity as a fallback.
2. While impersonating, open/query the thread token and successfully retrieve `TokenUser`, `TokenStatistics.AuthenticationId` and `TokenSessionId`. Reject missing, malformed, unsupported or inconsistent token information. Capture the values into a bounded immutable server-side request context before dispatching work.
3. End impersonation in a guaranteed cleanup path with `RevertToSelf`. If revert fails, do not process another privileged request on that worker; quarantine/terminate the worker and fail closed. No DB, file, TPM or other privileged operation may accidentally execute under a failed or stale impersonation state.
4. Close token handles deterministically. Do not cache an impersonation token for later requests. Re-authenticate and recapture for every request message.

The Named Pipe DACL/local-only configuration remains a separate required gate. `GetNamedPipeClientProcessId` may be recorded for diagnostics only; PID is not an identity proof. Client disconnect/reconnect creates a new pipe connection and must pass the complete authentication/capture sequence again.

### 9.3 Continuation policy

**Product Owner decision confirmed 2026-11-28:** a second Windows logon/RDP session with the same SID is denied continuation. The service must compare the entire captured tuple to the tuple stored in the protected Journal. A different AuthenticationId is a different logon session even when the SID matches; a different Terminal Services SessionId is also denied. No rebinding, challenge replacement or operation transfer is allowed.

A new UI process that reconnects while the same original Windows logon session and the same running service instance remain alive may continue only after the new pipe token reproduces the exact original SID + AuthenticationId + TokenSessionId + ServiceInstanceId tuple. A different logon, RDP session, elevated/impersonated identity, or mismatching token is denied.

**Conservative unresolved lifecycle constraint:** Windows token LUID evidence is scoped to the live Windows logon-session lifecycle. This proposal does not rely on the undocumented `SystemBootEnvironmentInformation` native query as a production boot-identity contract. To avoid falsely treating a later logon after a service restart/reboot as the original session, ServiceInstanceId changes on every service start. If a Journal contains an earlier ServiceInstanceId, the operation becomes `SUSPENDED_AFTER_SERVICE_RESTART` and cannot continue automatically, even if the visible SID/LUID/session fields appear equal. This stricter service-restart behavior is a proposal, not an accepted Owner decision; Product Owner acceptance and independent review are mandatory before implementation. Until then, cross-service-restart continuation is unsupported and fail-closed.

Tests must prove: same SID + different AuthenticationId denied; same AuthenticationId + different TokenSessionId denied; a different process reconnecting in the same live logon session and same service instance follows the exact-match rule; logoff/new logon denied; service restart produces a different ServiceInstanceId and blocks continuation; failed impersonation never falls back to service identity; and failed revert prevents further privileged work.

## 10. Claim succeeded but Journal is lost — recovery unavailable

If the database has a matching binding and receipt but the protected Journal is irrecoverably lost/corrupt, automatic re-enrollment is prohibited. Issue is blocked by the immutable binding; no second Account/Owner is created.

**Product Owner decision confirmed 2026-11-28:** fail closed. A future recovery design is bounded to a controlled local Cluster Administrator maintenance boundary and must not create a second Owner, identity, PostgreSQL principal, grant or privilege.

That decision does **not** establish an executable recovery procedure. At this revision there is **no available Cluster Administrator recovery operation** and no person is authorized by this proposal to perform a recovery. Cluster Administrator membership by itself is not proof of authority to reconstruct bootstrap state and is not a bypass for the fail-closed state.

Before any future recovery can be considered, a separate runbook must explicitly define and independently review: who authorizes and who executes (including separation where required); the exact local authentication/elevation boundary; independent evidence for the existing AccountId, binding and receipt; which local state may and may not be reconstructed; backup and TPM-counter dependencies; immutable audit evidence; secret/database-credential isolation; and negative proof that no second Owner, identity, principal, grant or privilege can be created. It must then be implemented and tested on the exact supported platform and separately authorized by the Product Owner. Until all of that occurs, Journal loss, corruption, service-instance mismatch and unresolved TPM/backup states remain blocked. No manual SQL, direct database access, counter reset, secret export or re-enrollment is permitted.

## 11. PostgreSQL 18.6 hostile proof

Required on disposable PostgreSQL 18.6, exact implementation SHA:

- lost Issue response and exact Issue replay returns same ChallengeId/expiry; altered replay conflicts;
- Claim commits but response/commit acknowledgement is dropped; exact replay returns `COMMITTED_MATCH` and same AccountId/receipt;
- `CLAIMED_NOW` without confirmed COMMIT is never reported as success;
- rollback/backend termination before commit and at commit boundaries; exactly one Account/binding/receipt;
- READ COMMITTED accepted; REPEATABLE READ and SERIALIZABLE rejected; waiting transaction sees committed state in the required later statement;
- VOLATILE/security-definer/search_path/owner/ACL/definition fingerprint catalog proof;
- lock expression, namespace, collision behavior, order, timeout, cancellation and cross-operation challenge invariant;
- match-before-expiry replay after challenge TTL; partial/mismatched binding/receipt; consumed challenge without receipt; wrong SID/purpose/InstallationId/operation/fingerprint;
- two concurrent Issue/Claim clients, same SID multiple sessions, and separate InstallationIds;
- effective catalog/ACL negative proof: no new principal/membership/View/grant; no Executor SELECT; no `mdgsm_runtime` expansion; no hidden privilege or PUBLIC EXECUTE.

## 12. Windows/TPM/IPC proof

- real Windows Server/Windows 11 + TPM 2.0: CNG provider, non-exportability, AES-GCM, independent ECDSA key, service SID ACL, wrap/unwrap, signature verification, restart and key rotation;
- monotonic NV counter state-machine tests for PREPARED-before-increment, lost increment response, counter unchanged, counter advanced with Journal finalization/rename failure, missing PREPARED record, older-backup restore, counter reset/loss, and all blocked-state classifications;
- power loss at each Journal write/flush/replace boundary; exact replay after process/service/host restart;
- TPM reset/loss, NV index loss, corrupted record, old valid record restore and key mismatch fail closed;
- Named Pipe local-only, remote denial, DACL, first-instance/pipe-squatting defense, service authentication, per-message token capture (TokenUser, TokenStatistics.AuthenticationId, TokenSessionId), impersonation failure/revert failure handling, reconnect and SafeHandle lifecycle;
- same SID/different AuthenticationId denial; same AuthenticationId/different TokenSessionId denial; same-session reconnect from a different process within the same service instance; logoff/new logon denial; service-restart ServiceInstanceId mismatch and fail-closed behavior;
- secret-sentinel scans over logs, traces, crash/support bundles and CI artifacts; zero raw secret/key/password/challenge leakage.

## 13. JRN-REC-02 / JRN-REC-03 disposition

The prior JRN-REC-02 View-owner candidate work (including `mdgsm_schema_owner`) is **superseded / not applicable to this no-Reader architecture**. No View is to be created or owned for reconciliation.

JRN-REC-03's Reader/View SELECT path is likewise **superseded / not applicable**; it must not be interpreted as permission to grant Executor SELECT or expand any existing role. Both tracking records should be closed/reclassified as “Reader/View approach superseded by exact-replay proposal,” while the replacement exact-replay contract/proof remains OPEN under Issue #118. If the tracker does not support a formal close/reclassify without review, record this disposition and leave the item open as administrative tracking only—not as a grant/Reader blocker.

PR #120 remains bounded evidence for lock primitives only. It does not prove these routine bodies, Journal, anti-rollback, Windows IPC or production ACLs.

## 14. Acceptance status and non-authorization

This v0.4 is a revised design proposal, **not accepted for implementation or merge**. The signatures have not been verified against source/schema; function body hashes and effective ACLs do not exist as implementation evidence; Windows token capture, TPM crash ordering, filesystem durability and hostile tests have not run. The two Product Owner policy decisions recorded in v0.3 remain resolved. The stricter service-restart constraint introduced in v0.4 is still a proposal and requires explicit Product Owner acceptance. The recovery boundary is defined, but no recovery procedure is available.

Acceptance requires:
1. independent Security/Architecture review of this exact v0.4 revision;
2. explicit Product Owner acceptance or rejection of the proposed service-restart behavior before implementation;
3. a separately specified, reviewed and authorized recovery runbook before any recovery capability exists;
4. final exact routine signatures, outputs/errors, owner, ACL/column grants, safe function config and signed definition fingerprints matched to implementation;
5. PostgreSQL and Windows/TPM/Named Pipe/SafeHandle/power-loss/anti-rollback tests on the exact implementation SHA, including proof of durable PREPARED publication on supported Windows filesystems;
6. CI and genuinely independent Security/Architecture review on that same implementation SHA.

No implementation, SQL/DDL, Migration 0010, role/grant, View, credential/key, Wizard, Owner binding, Login, feature flag, runtime, G1 or Go-Live is authorized or performed by this document or the recorded policy approvals. PR #124 must remain Draft until all gates pass and the required authorizations are recorded.

## References

- Microsoft Learn — [LSA Logon Sessions](https://learn.microsoft.com/en-us/windows/win32/secauthn/lsa-logon-sessions): Windows logon-session lifetime and LUID identity.
- Microsoft Learn — [TOKEN_STATISTICS](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-token_statistics): AuthenticationId is the LUID assigned to the represented session.
- Microsoft Learn — [ImpersonateNamedPipeClient](https://learn.microsoft.com/en-us/windows/win32/api/namedpipeapi/nf-namedpipeapi-impersonatenamedpipeclient): pipe-client impersonation and required failure handling/RevertToSelf.
- Microsoft Learn — [GetNamedPipeClientProcessId](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getnamedpipeclientprocessid): client PID retrieval; PID is supplemental, not the session identity.
- Microsoft Learn — [NtQuerySystemInformation](https://learn.microsoft.com/en-us/windows/win32/api/winternl/nf-winternl-ntquerysysteminformation): native API compatibility caution; v0.4 does not make the undocumented boot-information class a production dependency.
- Issue #118 — O-46 initial Product Owner Bootstrap.
- Issue #117 — central PostgreSQL and API deployment identity.
- Issue #121 — automatic central PostgreSQL/API identity provisioning.
- `MDG-SECURITY-O46-BOOTSTRAP-RECOVERY-AUDIT-001_v0.6.md`.
- `MDG-SECURITY-O46-LOCK-AND-RECONCILIATION-CONTRACT-PROPOSAL-001_v0.1.md` (historical Reader/View design; superseded only after review).
- PR #120 — bounded PostgreSQL 18.6 advisory-lock primitive proof.
