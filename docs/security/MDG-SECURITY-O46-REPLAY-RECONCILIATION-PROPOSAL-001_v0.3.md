# O-46 — Exact-Replay Reconciliation Without a Third PostgreSQL Principal

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-REPLAY-RECONCILIATION-PROPOSAL-001 |
| Version / date | 0.3 / 2026-11-28 |
| Status | **REVISED OWNER-DIRECTED PROPOSAL — REQUEST CHANGES / NOT ACCEPTED; implementation and independent review remain open** |
| Supersedes | v0.1 only; v0.1 remains immutable history |
| Tracking | Issue #118; replacement for the Reader/View reconciliation proposal |
| Baseline | `main` `ae63f4ca131b1e7f52ba0b2af86631c7730c5ef4` |

## 0. Review disposition

PR #124 v0.1 is not acceptable. The exact-replay direction remains a candidate, but v0.1 did not define the transaction/snapshot contract, separate routine outcomes from client transport state, cover lost Issue responses, prescribe write-ahead Journal ordering, define SID binding, or establish an anti-rollback root. This v0.3 records the required protocol, the two Product Owner policy decisions confirmed on 2026-11-28, and the remaining proof gates. It does **not** claim that the protocol has been implemented or that the full signatures/ACL/fingerprints have been verified against deployed function definitions.

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
- Anti-rollback must use a TPM-backed monotonic NV counter (or an independently reviewed equivalent hardware monotonic anchor), bound into the signed Journal sequence/checkpoint. Define provisioning, increment/write ordering, crash window, recovery when counter advances but Journal publication fails, backup/restore behavior and key rotation. Any mismatch fails closed; no software, exportable-key or DPAPI fallback.
- TPM loss/reset, NV-index loss, key loss, stale/rolled-back Journal and counter mismatch must be explicit blocked states, not auto-repair.

The monotonic-anchor design and failure recovery are **proposed and not yet proven**. A hash-chain or filesystem timestamp is not an anti-rollback proof.

## 9. SID and multiple-session binding

Every Wizard request must extract the SID from the current authenticated Windows token; caller-supplied SID and Journal SID are not authentication. Compare the current token SID to the SID bound to the initial operation. The protected Journal records the original SID only as a binding value.

**Product Owner decision confirmed 2026-11-28:** a second Windows logon/RDP session with the same SID must be denied continuation. Only the original session that initiated the bootstrap operation may continue it. Do not treat SID equality alone as session equivalence, rebind the operation, or create a replacement challenge. The approved policy must be implemented and tested against session identity/binding, disconnect/reconnect semantics, impersonation/revert and service restart before this gate can close.

## 10. Claim succeeded but Journal is lost

If the database has a matching binding and receipt but the protected Journal is irrecoverably lost/corrupt, automatic re-enrollment is prohibited. Issue is blocked by the immutable binding; no second Account/Owner is created.

**Product Owner decision confirmed 2026-11-28:** fail closed. Recovery is restricted to a controlled local Cluster Administrator maintenance path; it must not create a second Owner, identity, principal, grant or privilege. The path must independently verify the original AccountId/binding/receipt, produce auditable evidence, and undergo separate security review and implementation proof before use. This owner decision approves the recovery boundary, not the recovery procedure itself. Until that procedure is fully specified, reviewed and tested, the installation remains blocked/fail-closed; do not claim automatic recovery or authorize re-enrollment.

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
- monotonic NV counter provisioning/update and rollback/restore failure injection, including counter-ahead/Journal-write failure;
- power loss at each Journal write/flush/replace boundary; exact replay after process/service/host restart;
- TPM reset/loss, NV index loss, corrupted record, old valid record restore and key mismatch fail closed;
- Named Pipe local-only, remote denial, DACL, first-instance/pipe-squatting defense, service authentication, token SID extraction, impersonation/revert and SafeHandle lifecycle;
- same SID/different session policy tests once owner-approved;
- secret-sentinel scans over logs, traces, crash/support bundles and CI artifacts; zero raw secret/key/password/challenge leakage.

## 13. JRN-REC-02 / JRN-REC-03 disposition

The prior JRN-REC-02 View-owner candidate work (including `mdgsm_schema_owner`) is **superseded / not applicable to this no-Reader architecture**. No View is to be created or owned for reconciliation.

JRN-REC-03's Reader/View SELECT path is likewise **superseded / not applicable**; it must not be interpreted as permission to grant Executor SELECT or expand any existing role. Both tracking records should be closed/reclassified as “Reader/View approach superseded by exact-replay proposal,” while the replacement exact-replay contract/proof remains OPEN under Issue #118. If the tracker does not support a formal close/reclassify without review, record this disposition and leave the item open as administrative tracking only—not as a grant/Reader blocker.

PR #120 remains bounded evidence for lock primitives only. It does not prove these routine bodies, Journal, anti-rollback, Windows IPC or production ACLs.

## 14. Acceptance status and non-authorization

This v0.3 is a revised proposal with the two Product Owner policy decisions recorded, but it is **not accepted for implementation or merge**. The proposed signatures have not been verified against source/schema; function body hashes and effective ACLs do not exist as implementation evidence; Windows/TPM and PostgreSQL end-to-end tests have not run. The owner-policy questions are resolved; implementation and evidence gates remain open.

Acceptance requires:
1. independent review of this v0.3 and the specified implementation of the approved session-binding and fail-closed recovery policies;
2. final exact routine signatures, outputs/errors, owner, ACL/column grants, safe function config and signed definition fingerprints matched to implementation;
3. all PostgreSQL and Windows/TPM proof above passing on exact SHA;
4. CI and independent Security/Architecture review on that same SHA.

No implementation, SQL/DDL, Migration 0010, role/grant, View, credential/key, Wizard, Owner binding, Login, feature flag, runtime, G1 or Go-Live is authorized or performed by this document or the recorded policy approval.

## References

- Issue #118 — O-46 initial Product Owner Bootstrap.
- Issue #117 — central PostgreSQL and API deployment identity.
- Issue #121 — automatic central PostgreSQL/API identity provisioning.
- `MDG-SECURITY-O46-BOOTSTRAP-RECOVERY-AUDIT-001_v0.6.md`.
- `MDG-SECURITY-O46-LOCK-AND-RECONCILIATION-CONTRACT-PROPOSAL-001_v0.1.md` (historical Reader/View design; superseded only after review).
- PR #120 — bounded PostgreSQL 18.6 advisory-lock primitive proof.
