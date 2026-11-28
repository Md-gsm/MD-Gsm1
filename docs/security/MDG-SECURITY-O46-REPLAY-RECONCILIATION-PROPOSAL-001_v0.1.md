# O-46 — Exact-Replay Reconciliation Without a Third PostgreSQL Principal

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-REPLAY-RECONCILIATION-PROPOSAL-001 |
| Version / date | 0.1 / 2026-11-28 |
| Status | **OWNER-DIRECTED TECHNICAL PROPOSAL — INDEPENDENT SECURITY/ARCHITECTURE REVIEW REQUIRED; NOT IMPLEMENTATION AUTHORIZATION** |
| Tracking | Issue #118; proposed successor to the separate Reader/View approach |
| Baseline | `main` `ae63f4ca131b1e7f52ba0b2af86631c7730c5ef4` |

## 1. Owner decision and fixed boundaries

The Product Owner explicitly prohibits a third PostgreSQL principal/LOGIN for O-46 Reconciliation. Preserve these limits:

- No third principal, LOGIN, membership, role, View, grant or privilege expansion.
- No View/base-table SELECT for `mdgsm_bootstrap_executor`; no expansion of `mdgsm_runtime`; no runtime use of `migration_runner`.
- No generic dispatcher, dynamic SQL, third SECURITY DEFINER routine or new public endpoint.
- Keep exactly the two fixed-signature routines accepted by Issue #118: `identity_bootstrap.issue_initial_owner_challenge_v1(...)` and `identity_bootstrap.claim_initial_product_owner_v1(...)`.
- Keep the existing function-owner and Executor boundaries; Executor retains only its already-authorized EXECUTE rights on those exact signatures.
- Migration `0010` remains `RESERVED — SQL NOT AUTHORIZED`. This proposal authorizes no code, SQL/DDL, grants, credentials, keys, Wizard, Owner binding or runtime activation.

## 2. Architectural change: reconciliation by exact replay

Remove the separate PostgreSQL Reconciliation Reader, approved View and shared-lock read path from the target architecture. The Executor does not read database state directly. After Restart/timeout/lost response/Unknown Commit, it resubmits the exact immutable claim request to the existing `claim_initial_product_owner_v1` routine.

That routine runs under its existing SECURITY DEFINER owner, obtains the same InstallationId-scoped exclusive transaction lock used by challenge issuance, and internally inspects private binding/challenge/receipt state. The caller receives only a bounded typed outcome; it receives no row access or general query capability.

This changes reconciliation from **external read access** to **idempotent command replay**. PostgreSQL remains authoritative for whether the claim committed.

## 3. Exact replay requirements

The protected CNG/TPM Journal must recover the same InstallationId, SetupOperationId, ChallengeId, original challenge material, immutable claim payload, AccountId, versioned password hash/fingerprint and request-contract version. Sensitive fields remain encrypted/authenticated and are never logged or written to ordinary config, command line, dumps or CI artifacts.

After restart the Setup Service must:
1. authenticate the local Wizard channel;
2. verify Journal integrity, TPM-bound key availability and anti-rollback proof;
3. invoke the same fixed Claim signature with byte-for-byte identical immutable inputs;
4. never mint a new challenge, generate a new salt/hash, change AccountId/SetupOperationId, or silently repair the Journal;
5. treat timeout, connection loss, empty response or local Journal presence as neither proof of commit nor rollback.

The Claim routine must return only these bounded outcomes:

- `COMMITTED_MATCH`: exactly one immutable Owner binding and one receipt match every Journal-bound identifier/fingerprint; return the original AccountId/receipt identity without mutation.
- `CLAIMED_NOW`: no prior binding/receipt exists, the exact challenge is live and valid, all proofs match, and this invocation atomically commits the one permitted claim.
- `CONFLICT`: mismatch, duplicate/partial state, consumed challenge without matching receipt, conflicting request reuse, wrong installation/SID/purpose/fingerprint, or impossible state. Fail closed and preserve evidence.
- `EXPIRED_OR_ABANDONED`: exact challenge is expired/revoked by database time and no binding/receipt exists. Never reuse that challenge.
- `UNKNOWN`: database/lock/Journal/cryptographic proof unavailable or state unclassifiable. Preserve evidence; do not start a new operation.

A routine must never return `COMMITTED_MATCH` from Journal data alone; it must verify durable PostgreSQL binding and receipt while holding the lock.

## 4. Atomicity and issue serialization

The Claim transaction atomically creates Account + final credential hash + immutable InstallationId-to-AccountId Owner binding + challenge consumption + append-only receipt + secret-free audit intent. All commit or all roll back.

Receipt identity is unique and immutable for InstallationId + SetupOperationId + ChallengeId + request fingerprint. Exact replay returns the same committed result. Reusing an identifier with changed inputs is a hard conflict. Partial/corrupt states are not repaired silently.

The existing Issue routine uses the exact same server-derived InstallationId lock key and exclusive transaction lock. After locking it rejects an existing Owner binding, conflicting request reuse, and a new SetupOperationId while another operation has a live unrevoked unconsumed challenge. It must not silently revoke another operation's challenge. Expiry/revocation decisions use database time. A new operation is permitted only after the previous operation is proven terminal and no binding/receipt exists.

Both routines acquire the canonical lock before their first bootstrap-state read. Derive the domain-separated 64-bit key inside PostgreSQL from normalized InstallationId; callers cannot supply it and .NET must not independently derive it. No shared-lock Reader path remains. Do not add `pg_catalog` grants by inference; prove the exact existing function-owner/Executor execution path or stop.

## 5. Mandatory proof before acceptance

### PostgreSQL 18.6 disposable hostile tests
- commit succeeds but response is dropped; exact replay returns `COMMITTED_MATCH` and the same AccountId/receipt;
- rollback before commit; exact retry claims once;
- backend termination at commit boundaries; no duplicate Account/binding;
- identical replay is idempotent; same request ID with altered payload is rejected;
- wrong InstallationId, SetupOperationId, ChallengeId, SID, purpose, challenge secret, hash version or fingerprint is rejected;
- consumed challenge without receipt, mismatched receipt, duplicate binding and partial/corrupt state fail closed;
- DB-time expiry boundary and cross-SetupOperationId live-challenge rejection;
- concurrent issue/claim across same and different InstallationIds;
- exactly one Account, one immutable Owner binding and one receipt;
- effective catalog/ACL proof shows no new principal, membership, View or grant and no added Executor table/View access or `mdgsm_runtime` privilege.

### Windows and Journal proof
- real Windows Server/Windows 11 + TPM 2.0 PoC for CNG Platform Crypto Provider, non-exportable independent keys, authenticated encryption, signature/integrity verification, service-SID ACL and restart recovery;
- exact replay after process/service/host restart uses original protected request bytes and does not regenerate password hashes/challenges;
- TPM loss/reset, corrupted/rolled-back Journal, missing key and invalid signature fail closed without fallback;
- Named Pipe local-only access, remote-client denial, exact DACL, OS-token SID extraction, service authentication, impersonation/revert and pipe-squatting defenses pass;
- secret-sentinel scans cover logs, traces, crash/support bundles and CI artifacts.

Independent Security/Architecture review must examine the exact implementation SHA, both routine definitions, function-owner ACL aggregation, replay outcomes, Journal anti-rollback assumptions, IPC threat model, PostgreSQL hostile evidence and negative privilege catalog. CI must pass on the same SHA.

## 6. Supersession and non-authorization

Upon independent acceptance, this proposal supersedes only the separate Reader/View/shared-lock reconciliation approach in `MDG-SECURITY-O46-LOCK-AND-RECONCILIATION-CONTRACT-PROPOSAL-001_v0.1.md`. PR #120 remains bounded evidence for PostgreSQL lock primitives; its synthetic Reader ACL assertions are not an O-46 acceptance requirement under this design.

JRN-REC-03 should be redefined as “exact-replay reconciliation contract and proof”; it remains OPEN until all proof and independent review pass. Issue #117/#121 deployment identities remain separate and must not be used to create a Reader.

**Current status: proposal only.** No source code, SQL/DDL, Migration 0010, role, grant, View, key, credential, Wizard, Owner binding, Login, feature flag, runtime, G1 or Go-Live is authorized or performed.

## References

- Issue #118 — O-46 initial Product Owner Bootstrap.
- Issue #117 — central PostgreSQL and API deployment identity.
- Issue #121 — automatic central PostgreSQL/API identity provisioning.
- `MDG-SECURITY-O46-BOOTSTRAP-RECOVERY-AUDIT-001_v0.6.md`.
- `MDG-SECURITY-O46-LOCK-AND-RECONCILIATION-CONTRACT-PROPOSAL-001_v0.1.md`.
- PR #120 — bounded PostgreSQL 18.6 advisory-lock primitive proof.
