# MDG Security Stage 08 — Controlled Register Addendum

| Field | Value |
|---|---|
| Register ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 0.3 / 2026-11-21 |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner-policy reference | ADR-0013, including the explicit session-policy correction dated 2026-11-07 |
| Supersedes | v0.2 as current register; historical versions retained |
| Package under review | PR #86 — Draft; not merged |

## 1. Registered Stage 08 security artifacts

| Artifact | Version | Current status |
|---|---:|---|
| `MDG-SECURITY-STAGE-08-DB-SESSION-RECOVERY-HASHCHAIN-001` | 0.1 | Integrated security design draft; read with reconciliation addendum; no implementation authority |
| `MDG-SECURITY-STAGE-08-RECONCILIATION-001` | 0.1 | Controlled design amendment; N.3/ADR-0013 and O-20/O-21/O-45/O-46 dependencies open |
| `MDG-SECURITY-STAGE-08-REAUDIT-001` | 0.2 | Internal consistency review only; not external independent attestation |
| `MDG-ARCH-N3-ADR-0013-RECONCILIATION-001` | 0.1 | Controlled crosswalk proposal; two System Manager policy questions open |
| `MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001` | 0.1 | Source audit complete; canonical role mapping, live catalog and independent credential proof open |
| `MDG-SECURITY-O21-HASHCHAIN-IMPLEMENTATION-READINESS-001` | 0.1 | Implementation/proof contract; AuditSecurity implementation, Platform key and external anchor decisions open |
| `MDG-SECURITY-O45-SESSION-LIFECYCLE-AUDIT-001` | 0.1 | Source audit; heartbeat/logout candidate added in PR #86; remaining lifecycle and tests open |
| `MDG-SECURITY-O46-BOOTSTRAP-RECOVERY-AUDIT-001` | 0.1 | Source audit; bootstrap, verified email, SMTP, recovery schema and secret-hygiene proof open |

All newly registered documents are on the PR #86 head branch and are not yet part of main.

## 2. Owner decisions preserved

- Login uses username/password only.
- Session policy follows the later ADR-0013 correction: no fixed absolute TTL and no approved per-account concurrent-session cap.
- Initial system cap is five distinct active accounts; an authorized administrator may change it only through a dedicated permission and audit.
- Heartbeat target is 30 seconds and lease is 180 seconds.
- Ordinary login lockout remains five failures in a 15-minute window followed by a 15-minute lock.
- Recovery is email-only for this phase; OTP lifetime is 600 seconds; five failed attempts trigger a 24-hour Recovery lock; resend quotas remain as stated in owner decisions.
- Email-only Recovery is not MFA. SMS and offline recovery-code issuance are outside this phase.
- The older “ten recovery codes” wording in O-46 is superseded for this phase and must not be implemented.
- No static bootstrap password, master password, backdoor or support bypass is permitted.

## 3. Controlled reconciliation items

### N.3 / ADR-0013
- The historical five-user/computer seat wording is clarified as five distinct active accounts system-wide.
- The one-session-per-account rule and fixed absolute TTL are superseded.
- The maximum of two provisioned System Manager accounts and maximum of one active System Manager session globally are not explicitly resolved by the recorded later correction. Both remain open pending owner clarification; implementation must not guess.

### O-20 — Database privileges
- N.6 canonical logical identities are `runtime_app`, `migration_runner`, `backup_operator`, `reporting_ro` and `emergency_operator`.
- The current `mdgsm_runtime` supporting role has not yet been mapped to the canonical model.
- Current runtime grant script covers only `identity.*`; Persons/Finance object grants, RowScope, ownership/default ACLs and all real LOGIN credentials remain unproven.
- The database-level LOGIN test does not prove the running API uses that identity end-to-end.
- Background Worker identity mapping must not create an unauthorized additional canonical identity.

### O-21 — Hash Chain
- No AuditSecurity canonical event stream, two-independent-writer PoC, signed external checkpoint, key custody/backup/recovery/rotation/revocation or independent read-only verifier has been implemented/proven.
- `identity.security_audit_events` remains IdentityAccess append-only evidence and is not the canonical cross-module Hash Chain.

### O-45 — Session lifecycle
- Existing token digest, PostgreSQL session validation, lease, capacity serialization and password-change revoke-all are partial foundations.
- PR #86 adds a heartbeat endpoint protected by PostgreSQL-backed session middleware, an idempotent logout handler/endpoint that revokes only the presented session digest, and an `IDatabaseClock` based on PostgreSQL `clock_timestamp()` for session/login/revocation/audit time.
- HTTP E2E tests are extended to cover authenticated heartbeat, current-session-only logout, preservation of a second session for the same account, repeated logout, and a two-day host-clock skew proving PostgreSQL clock authority. These tests remain NOT RUN / NOT PASS until exact-head CI completes.
- Token rotation trigger/overlap/unknown-commit policy, stale-session cleanup, account-disable revoke, full API default-deny and crash/reconnect proof remain open.

### O-46 — Bootstrap and Recovery
- No secure installer/bootstrap workflow, trusted one-time secret delivery, first-login completion state machine, verified-email proof, Recovery orchestration/endpoint, SMTP adapter or complete quota/idempotency schema is present in main.
- The bootstrap delivery channel and Recovery entrypoint are not decided.
- Migration 0009 remains unreserved; no schema change may consume it before WG-0 reservation.
- No raw bootstrap secret, password, OTP, bearer token, recovery key or SMTP credential may enter logs, telemetry, crash artifacts or CI artifacts.

## 4. Test/evidence status

- SEC-T-001..036 remain specifications only; no blanket PASS is claimed.
- O-20 independent-credential and end-to-end API identity tests are NOT RUN / NOT PASS.
- O-21 two-writer, failure-injection, checkpoint, key lifecycle and verifier tests are NOT RUN / NOT PASS.
- O-45 heartbeat/logout E2E candidate tests are present in PR #86 but their exact-head CI is pending; all remaining O-45 lifecycle tests are NOT RUN / NOT PASS.
- O-46 bootstrap/recovery tests are NOT RUN / NOT PASS.
- Gate D remains OPEN / NOT VERIFIED.
- Independent security reviewer attestation and owner release acceptance remain pending.

## 5. Non-authorization boundary

PR #86 is a Draft review package. No production role, LOGIN credential, membership, grant, revoke, migration, bootstrap credential, OTP, recovery key, SMTP secret, signing key, WORM checkpoint, production listener, feature activation, audit purge, G1 activation, Posting Release or Go-Live is authorized. No Stage 08 item is closed by document registration or CI success alone.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v0.3**
