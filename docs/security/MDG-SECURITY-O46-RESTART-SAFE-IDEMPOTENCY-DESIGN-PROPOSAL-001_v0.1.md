# O-46 — Restart-Safe Owner-Claim Idempotency Design Proposal

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O46-RESTART-SAFE-IDEMPOTENCY-DESIGN-PROPOSAL-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | **NON-NORMATIVE DESIGN PROPOSAL — OWNER DECISION AND SECURITY REVIEW REQUIRED; NO IMPLEMENTATION AUTHORIZED** |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #92 Draft |

## 1. Problem

The current `PlatformPasswordHasher` uses ASP.NET Core Identity V3 PBKDF2. `Hash(password)` generates a fresh random salt and accepts no caller-supplied salt. Rehashing the same password after restart therefore produces a different encoded hash. Because proposed fingerprint v1 binds the exact encoded hash, re-entry after restart cannot reproduce the same fingerprint.

The design must preserve the exact hash/fingerprint once a claim might have been dispatched, never persist plaintext password, reconcile against authoritative PostgreSQL state before retry, and prevent a second Account or immutable Product Owner binding.

## 2. Options evaluated

### A — Protected durable operation journal (preferred candidate)

Hash once, compute the fingerprint, then persist the exact encoded hash and fingerprint in an encrypted/authenticated local journal before any claim request can be sent. Restrict the journal with an explicit protected NTFS DACL to the dedicated Setup Service identity and SYSTEM. After restart, reuse the exact stored hash/fingerprint; never rehash for that SetupOperationId.

Advantages: retains the existing Identity V3 hash format and random-salt behavior; exact payload continuity; simple retry semantics.

Risks: local credential-verifier custody, service identity/profile continuity, DACL/key protection, atomic durable writes, backup/restore, anti-rollback and deletion limitations. The journal is recovery material, not authoritative claim state.

DPAPI CurrentUser under a dedicated service identity is a candidate only, not selected. DPAPI LocalMachine alone is not an access-control boundary; ciphertext ACL and local attacker assumptions require proof. CNG/TPM-backed protection may also be evaluated.

### B — Deterministic per-operation salt / custom hasher

Persist or derive a unique random salt per SetupOperationId and introduce a versioned hasher contract that reproduces the same output. This avoids storing the final hash as a separate durable artifact, but changes the hasher format/contract, introduces salt/key custody and compatibility complexity, and needs cryptographic review plus cross-version vectors. A public UUID alone is not a sufficient secret source. Not recommended as the first solution.

### C — Ask the user to re-enter the password

Re-entry does not reproduce the original salted hash. It is safe only if authoritative reconciliation proves no claim was dispatched and no transaction can still commit. It is not a retry strategy for an unknown claim outcome.

**Recommendation:** advance Option A to threat review; do not implement yet.

## 3. Proposed journal and dispatch barrier

Minimum protected journal fields:
- format/record version;
- InstallationId, SetupOperationId and AccountId;
- exact validated login_name_normalized, display_name and email;
- password-hash version and exact encoded password_hash;
- fingerprint version and exact 32-byte fingerprint;
- state marker and monotonic local sequence number.

Never store plaintext password, raw challenge, challenge verifier digest, cluster-superuser credential, executor secret, private key or database connection string. Raw challenge remains memory-only.

### State machine

| State | Meaning | Rule |
|---|---|---|
| J0 — Absent | No operation record | New operation may begin |
| J1 — IntentDurable | IDs and validated non-secret profile durably stored | Hash entered password once |
| J2 — PayloadDurable | Exact hash and fingerprint protected and durably flushed | Challenge/claim orchestration may begin |
| J3 — ClaimMayHaveBeenSent | Dispatch-intent durably committed before sending claim | Restart must reconcile first |
| J4 — CommittedMatch | DB receipt and immutable binding match exact IDs/fingerprint | Terminal; never claim again |
| J5 — ProvenNotCommitted | DB proves no receipt/binding and no earlier claim can still commit | Retry exact journaled payload only |
| JX — Conflict/Unknown | Mismatch, unavailable DB, uncertain transaction or corrupt state | Fail closed |

Required ordering:
1. Persist J1.
2. Hash plaintext once in memory.
3. Compute fingerprint over that exact encoded hash.
4. Protect and durably commit J2, including durable flush and safe atomic replacement.
5. Only after J2 is durable may challenge/claim orchestration proceed.
6. Persist J3 before the claim request can leave the process.
7. After restart, reconcile against PostgreSQL before any retry or new challenge decision.

If a crash occurs before J2, the system must prove no claim could have been dispatched; only then may it re-prompt and hash again. Once J3 is durable, rehashing is prohibited.

### Challenge loss

The raw challenge is never journaled. If it is lost after restart while J2 exists and no claim may have been sent, a replacement challenge may be requested under the same SetupOperationId with a new challenge_request_id; the issuer must revoke the prior unconsumed challenge atomically. If J3 exists, reconcile first; do not issue a replacement merely because the raw token was lost.

## 4. Authoritative reconciliation

The journal is not the source of truth. Reconciliation must use the same InstallationId/SetupOperationId and the same per-installation serialization boundary as issuer and claim.

- **Committed match:** exactly one immutable binding and one receipt exist; InstallationId, SetupOperationId, AccountId, ChallengeId and fingerprint match. Treat as committed; never claim again.
- **Committed conflict:** any mismatch, duplicate or inconsistent Account/binding/receipt. Hard fail; no repair or replacement.
- **Proven not committed:** no receipt/binding exists and the DB serialization boundary proves no earlier claim transaction can still commit. Only then may the exact journaled payload be retried.
- **Unknown:** DB unavailable, lock unavailable, partial state, or inability to prove the previous transaction cannot commit. No retry, new operation, rehash or inference from timeout.

**Two-routine boundary issue:** the accepted design permits exactly two SECURITY DEFINER routines and no third routine. It has not shown how a strictly read-only authoritative reconciliation result is exposed. Review whether the existing claim routine can provide a non-mutating typed reconciliation result without becoming a generic dispatcher, or whether an already-authorized IdentityAccess application query can safely read the receipt/binding under a separately reviewed permission boundary. If neither is provable without broadening the approved boundary, stop and return to the Product Owner. Do not add a third routine or executor privilege by inference.

## 5. Journal protection and lifecycle

The journal must be outside the program directory, in stable machine-level application data that survives update/repair and is not available to ordinary user profiles. Exact path and technology remain unselected.

Required properties:
- OS-backed encryption and integrity protection;
- explicit protected NTFS DACL limited to dedicated Setup Service identity and SYSTEM;
- key/service identity continuity across restart, update, repair and supported restore;
- atomic write/replace and durable flush before advancing state;
- strict format/version/size validation;
- no plaintext temporary files, dumps, logs, telemetry or support-bundle inclusion;
- audit access without logging contents;
- no automatic deletion during update, repair or uninstall while outcome is unresolved;
- deletion only after committed claim, independent executor revocation, Setup channel closure and evidence verification; no claim of guaranteed media erasure;
- a restored/stale journal never overrides current PostgreSQL state.

A journal restored without matching DB state is not authority to claim. A missing/corrupt journal after J3 requires reconciliation; if no commit is proven but the exact payload cannot be recovered, do not invent a replacement payload under the same operation. Escalate through an explicitly reviewed recovery path.

## 6. Failure behavior

| Failure point | Required behavior |
|---|---|
| Before J1 | Start normally |
| J1 before hash | Re-prompt; no claim possible |
| Hash created but J2 not durable | Prove no dispatch; re-prompt and rehash only then |
| J2 before J3 | Reuse exact hash/fingerprint; challenge may be issued/replaced |
| J3 before response | Unknown; authoritative reconciliation first |
| Commit succeeded, response lost | Reconcile to committed match; never claim again |
| Transaction rolled back and no transaction remains | Retry exact journaled payload only |
| DB unavailable / reconciliation uncertain | Preserve journal and fail closed |
| Journal missing/corrupt after J3 | Reconcile; no regenerated payload or new operation |
| Journal restored from older backup | Reconcile current DB; reject stale state as authority |
| Key/service identity unavailable | No bypass; use separately approved recovery |

## 7. Invariants and acceptance gates

1. Plaintext password exists only in the trusted in-process input/hash path; zeroization is best-effort, not guaranteed.
2. Encoded password hash is treated as sensitive credential-verifier material.
3. Hash/fingerprint pair is immutable after J2.
4. J3 is durable before claim dispatch.
5. Timeout never means rollback.
6. A new SetupOperationId cannot bypass an unknown/conflicting prior operation.
7. PostgreSQL receipt and immutable binding determine claim outcome.
8. Per-installation serialization prevents a stale claim racing a replacement.
9. Journal contents never enter logs, telemetry, CI artifacts or support bundles.
10. No PostgreSQL principal, role, grant, routine, schema, migration, permission or production capability is added.

Before implementation require: Product Owner approval; IdentityAccess acceptance; independent Security/Architecture review of local attacker assumptions, DPAPI/CNG, service identity, DACL, key continuity, backup/restore, anti-rollback and deletion limits; Windows proof of durable atomic writes and restart/update/repair behavior; PostgreSQL 18 proof of reconciliation linearization and no outstanding claim after a negative result; cross-language fingerprint vectors; crash/tamper/restore/rollback/concurrency tests; and proof that the two-routine boundary supports reconciliation without privilege expansion.

All gates are **NOT RUN / NOT VERIFIED**. This proposal does not reserve migration 0010, select a protection technology, authorize schema/routine/ACL changes or authorize implementation.

## 8. Explicit non-authorization

No source code, SQL, DDL, migration, role, grant, routine, credential, secret, local journal, Windows key, Owner Account, binding, challenge, executor activation, Setup Wizard execution, recovery, G1 or production activation is authorized.

**END — v0.1**
