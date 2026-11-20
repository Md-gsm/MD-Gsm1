# MDG Security Stage 08 — Internal Design Re-Audit

| Field | Value |
|---|---|
| Audit ID | MDG-SECURITY-STAGE-08-REAUDIT-001 |
| Version | 0.1 |
| Date | 2026-11-20 |
| Audited artifact | MDG-SECURITY-STAGE-08-DB-SESSION-RECOVERY-HASHCHAIN-001 v0.1 |
| Baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Review type | Internal adversarial design review by the authoring workflow; NOT independent attestation |
| Result | Four requested domains CLOSED IN DESIGN; implementation/release gates OPEN |

## 1. Executive result

The Stage 08 specification now covers PostgreSQL security, IdentityAccess session lifecycle, account recovery and AuditSecurity hash chaining. The four requested domains are complete as an integrated design specification. They are not implemented, independently approved or runtime-proven.

## 2. Finding matrix

| Domain | Design result | Remaining evidence/dependency |
|---|---|---|
| Database security | CLOSED IN DESIGN | Exact role/grant matrix, TLS/deployment validation, secret store and PostgreSQL privilege tests |
| Session lifecycle | CLOSED IN DESIGN | Exact-SHA API/PostgreSQL race, lease, revoke, five-account concurrency and restore tests |
| Account recovery | CLOSED IN DESIGN | Verified-email proof, endpoint trust boundary, SMTP/secret configuration, atomic orchestration, mailbox threat review and runtime tests |
| Hash Chain | CLOSED IN DESIGN | Canonical serialization vectors, stream schema, signing provider, external WORM anchor, verifier and restore tests |

## 3. Adversarial checks

- Desktop is not a database client; API is the enforcement boundary.
- Runtime, migrator, object owner, worker and audit-ingest identities are separated; exact grants remain intentionally unassigned pending module/platform contracts.
- The current session policy is preserved: 30-second heartbeat, 180-second lease, five distinct active accounts initially, no absolute TTL and no approved per-account session cap.
- Password change/reset and account disablement invalidate all account sessions atomically; server checks PostgreSQL state rather than trusting a local session cache.
- Recovery preserves email-only/600-second OTP/five-attempt/24-hour lock/resend policy; SMS and offline codes remain outside this phase.
- A low-entropy OTP must not be protected by plain SHA-256 alone. The specification was hardened to require a dedicated external HMAC-SHA-256 key and key version, separate from privacy digest and bearer-token hashing.
- Desktop must retain its bearer token in process memory while the app is open; persistent local token storage is not silently introduced.
- Hash chain sequence is stream-local and transactionally serialized. No false global order is claimed from timestamps.
- Hash chaining alone does not defeat a privileged DB attacker. Independently stored signed checkpoints are required to detect rewriting/deletion of history up to the last anchor.
- Canonical event storage remains exclusively owned by AuditSecurity; source modules publish transactional audit intents and do not create competing canonical ledgers.
- No retention period, signing algorithm, deployment vendor, grant or key material was invented.

## 4. Consistency and residual risks

No contradiction was found with ADR-0013's latest owner decisions or the v3.0.3 non-release boundary. Recovery remains disabled until operational dependencies and independent review close. HTTP login feature flag, bootstrap and production listener remain under their existing gates. `g1Authorized=false` remains controlling.

Residual risks are explicit: email mailbox compromise/phishing; missing verified-email state; unselected SMTP and Platform key/signing providers; unapproved audit retention/legal hold; canonical serialization specification not yet frozen; external checkpoint operations not implemented; exact DB privileges not accepted.

## 5. Test evidence

SEC-T-001..030 are design specifications only. No PostgreSQL, API, concurrency, restore, cryptographic-vector, TLS, secret-store or end-to-end recovery test was executed in this audit. No PASS is claimed. CI for the exact PR head had no workflow run visible at audit time.

## 6. Decision

**Stage 08: COMPLETE IN DESIGN.** Ready for independent exact-SHA security review and planning of implementation/test work. Not ready for DDL freeze, migration, runtime activation, G1 or Go-Live. No implementation or operational authorization is granted.

**END — MDG-SECURITY-STAGE-08-REAUDIT-001 v0.1**
