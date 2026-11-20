# MDG Security Stage 08 — Controlled Register Addendum

| Field | Value |
|---|---|
| Register ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version | 0.1 |
| Date | 2026-11-20 |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |

## Registered deliverable

| Artifact | Version | Status |
|---|---:|---|
| `MDG-SECURITY-STAGE-08-DB-SESSION-RECOVERY-HASHCHAIN-001` | 0.1 | Integrated design specification; exact-SHA independent review and implementation gates remain open |
| `MDG-SECURITY-STAGE-08-REAUDIT-001` | 0.1 | Internal adversarial design re-audit; not independent attestation; runtime tests NOT RUN |

## Scope

The registered specification covers PostgreSQL role separation and transport, IdentityAccess session/token lifecycle, accepted email-only recovery policy and safe orchestration, AuditSecurity canonical hash chain, external signed checkpoints, verification/restore, privacy, backup and 30 design acceptance tests.

## Owner decisions preserved

- Username/password login only.
- Session heartbeat 30 seconds; lease 180 seconds; no absolute TTL; no per-account concurrent-session cap.
- Initial system cap: five distinct active accounts; PostgreSQL transactional enforcement.
- Ordinary login lockout: five failures in 15 minutes, then 15-minute lock.
- Recovery: email-only, OTP 600 seconds, digest-only, five failed attempts then 24-hour lock; resend at most once per rolling hour and five per rolling 24 hours.
- SMS and offline recovery-code issuance are outside this phase.
- Recovery remains disabled pending verified-email proof, SMTP/secret configuration, implementation, tests and independent review.

## Status boundary

SEC-T-001..030 are specifications only and NOT RUN. Database grant values, deployment secrets, verified-email provisioning, recovery endpoint trust boundary, canonical serialization vectors, signing provider, external checkpoint operations, retention/legal hold, independent review, runtime tests and G1 authorization remain open. No code, SQL, DDL, migration, grant, secret, feature activation, production listener, purge or Go-Live is authorized.
