# MDG Security Stage 08 — Controlled Register Addendum

| Field | Value |
|---|---|
| Register ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version | 0.2 |
| Date | 2026-11-21 |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner-policy reference | ADR-0013, including the explicit session-policy correction dated 2026-11-07 |
| Supersedes | v0.1 as current register; historical v0.1 retained |

## 1. Registered deliverables

| Artifact | Version | Status |
|---|---:|---|
| `MDG-SECURITY-STAGE-08-DB-SESSION-RECOVERY-HASHCHAIN-001` | 0.1 | Integrated design draft, read together with Reconciliation Addendum v0.1; not implementation authority |
| `MDG-SECURITY-STAGE-08-RECONCILIATION-001` | 0.1 | Controlled security design amendment; baseline reconciliation items remain open |
| `MDG-SECURITY-STAGE-08-REAUDIT-001` | 0.2 | Internal consistency review; not independent attestation |

## 2. Owner decisions preserved

- Login uses username/password only.
- Session heartbeat is 30 seconds and lease is 180 seconds.
- Under the later ADR-0013 correction, there is no fixed absolute TTL and no approved per-account concurrent-session cap.
- Initial system cap is five distinct active accounts; an authorized administrator may change it only with a dedicated permission and audit.
- Ordinary login lockout: five failures in a 15-minute window, followed by a 15-minute lock.
- Recovery is email-only for this phase; OTP TTL is 600 seconds; five failed attempts trigger a 24-hour recovery lock; resend quotas remain as stated in ADR-0013.
- SMS and offline recovery-code issuance remain outside this phase.
- Recovery remains disabled until its dependencies, tests and independent security review close.

## 3. Open controlled dependencies

- v3.0.3 N.3 still contains historical 5/2/1 session/seat wording that conflicts with the later ADR-0013 correction. Controlled master-document reconciliation remains OPEN; this register does not amend v3.0.3.
- Canonical database logical identities remain those in v3.0.3 N.6: `runtime_app`, `migration_runner`, `backup_operator`, `reporting_ro`, `emergency_operator`. Exact LOGIN principals, memberships, object ownership, grants and RowScope remain OPEN under O-20.
- N.7/O-21 two-writer Hash Chain concurrency, rollback/crash/replay, external checkpoint, key custody/backup/recovery/revocation and verifier proof remain OPEN / NOT RUN.
- O-45 session token rotation and stale-session cleanup remain OPEN / NOT RUN.
- O-46 bootstrap/recovery capture-then-invalidate and secret/logging proof remain OPEN / NOT RUN.
- SEC-T-001..036 are specifications only and NOT RUN.

## 4. Non-authorization boundary

No code, SQL, DDL, migration, grants, production credentials, feature activation, production listener, audit purge, G1 activation, Posting Release or Go-Live is authorized. Gate D remains OPEN / NOT VERIFIED.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v0.2**
