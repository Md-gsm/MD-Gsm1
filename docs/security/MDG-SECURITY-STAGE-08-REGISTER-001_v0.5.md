# MDG Security Stage 08 — Controlled Register Addendum

| Field | Value |
|---|---|
| Register ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 0.5 / 2026-11-21 |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner-policy reference | ADR-0013, including the explicit session-policy correction dated 2026-11-07 |
| Supersedes | v0.3 as current register; historical versions retained |
| Package under review | PR #86 — Draft; not merged |

## 1. Registered Stage 08 security artifacts

| Artifact | Version | Current status |
|---|---:|---|
| `MDG-SECURITY-STAGE-08-DB-SESSION-RECOVERY-HASHCHAIN-001` | 0.1 | Integrated security design draft; read with reconciliation addendum; no implementation authority |
| `MDG-SECURITY-STAGE-08-RECONCILIATION-001` | 0.2 | Controlled successor addendum; option D recorded; O-20/O-21/O-45/O-46 evidence gates remain open |
| `MDG-SECURITY-STAGE-08-REAUDIT-001` | 0.2 | Internal consistency review only; not external independent attestation |
| `MDG-ARCH-N3-ADR-0013-RECONCILIATION-001` | 0.2 | Owner-confirmed option D; no System Manager account-count cap; one active System Manager session globally |
| `MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001` | 0.4 | Disposable runtime_app LOGIN/API and escalation-test source added; exact-head CI pending; full N.6 matrix open |
| `MDG-SECURITY-O21-HASHCHAIN-IMPLEMENTATION-READINESS-001` | 0.2 | Disposable PostgreSQL PoC source added; exact-head CI pending; external WORM and platform key operations open |
| `MDG-SECURITY-O45-SESSION-LIFECYCLE-AUDIT-001` | 0.1 | Source audit; heartbeat/logout candidate added in PR #86; remaining lifecycle and tests open |
| `MDG-SECURITY-O46-BOOTSTRAP-RECOVERY-AUDIT-001` | 0.2 | Controlled blocker audit; no runtime implementation; trusted entrypoints, verified email, SMTP, key/schema and review gates open |

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
- **Owner option D (2026-11-21) resolves the two System Manager items:** no N.3-specific maximum count of provisioned System Manager accounts; at most one active System Manager session globally across all System Manager accounts.
- This one-session constraint is role-wide/global, not a per-account cap for ordinary accounts. Implementation must use the canonical System Manager role and an atomic PostgreSQL global slot across API instances; no implementation/activation is claimed.

### O-20 — Database privileges
- PR #86 adds disposable-only `runtime_app` LOGIN and API E2E candidate tests using the existing `mdgsm_runtime` NOLOGIN group and current IdentityAccess grant script.
- The candidate checks login attributes and denies selected DDL, direct DML, role/GRANT/SET ROLE escalation and direct Persons base-table access. It has not been accepted by exact-head CI.
- The complete object-by-object matrix across `runtime_app`, `migration_runner`, `backup_operator`, `reporting_ro`, `emergency_operator`, object ownership, default ACLs, sequences, functions, extensions and RowScope remains OPEN. No production roles, memberships, credentials or grants were changed.

### O-20 — Database privileges
- Object-by-object matrix and materialized table inventory are now recorded in `MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001_v0.1.md`.
- `runtime_app` candidate probes deny direct access to all current Persons, temporary workflow, merge, Finance Party-account and migration metadata tables in addition to escalation operations in IdentityAccess. Exact-head CI for the new probes is pending.
- `migration_runner`, `backup_operator`, `reporting_ro`, and `emergency_operator` remain unproven under distinct PostgreSQL LOGINs. Full catalog, object ownership, PUBLIC/default ACL, function/sequence/extension and RowScope proof remains OPEN.

### O-21 — Hash Chain
- PR #86 adds a disposable PostgreSQL PoC candidate: two separate LOGIN writers, owner-controlled SECURITY DEFINER append/lock, full typed event-envelope source, deterministic canonicalization/golden vector, transactional chain-head sequencing, rollback/backend termination/unknown-commit replay, pinned RSA-PSS test checkpoints and read-only verifier tamper/gap detection.
- Exact-head PostgreSQL 18 CI is pending. Temporary local checkpoint output and ephemeral test keys are not an external WORM anchor or production key-custody proof.
- Production AuditSecurity module, source-module transactional outboxes, checkpoint scheduling/external WORM provider, key custody/backup/recovery/revocation, restore continuity, alerts, runbook and independent attestation remain OPEN.

### O-45 — Session lifecycle
- Existing token digest, PostgreSQL session validation, lease, capacity serialization and password-change revoke-all are partial foundations.
- PR #86 adds a heartbeat endpoint protected by PostgreSQL-backed session middleware, an idempotent logout handler/endpoint that revokes only the presented session digest, and an `IDatabaseClock` based on PostgreSQL `clock_timestamp()` for session/login/revocation/audit time.
- HTTP E2E tests are extended to cover authenticated heartbeat, current-session-only logout, preservation of a second session for the same account, repeated logout, and a two-day host-clock skew proving PostgreSQL clock authority. These tests remain NOT RUN / NOT PASS until exact-head CI completes.
- Token rotation trigger/overlap/unknown-commit policy, stale-session cleanup, account-disable revoke, full API default-deny and crash/reconnect proof remain open.

### O-46 — Bootstrap and Recovery
- No secure installer/bootstrap workflow, trusted one-time secret delivery, first-login completion state machine, verified-email proof, Recovery orchestration/endpoint, SMTP adapter or complete quota/idempotency schema is present.
- O-46 v0.2 records that these are implementation blockers, not permission to guess. Bootstrap delivery/local trust, Recovery entrypoint and verified-email proof remain unresolved.
- SMTP provider, dedicated HMAC key custody/rotation, schema delta and WG-0 reservation are not complete. Migration 0009 remains reserved and must not be consumed.
- No raw bootstrap secret, password, OTP, bearer token, recovery key or SMTP credential may enter logs, telemetry, crash artifacts or CI artifacts.

## 4. Test/evidence status

- SEC-T-001..036 remain specifications only; no blanket PASS is claimed.
- O-20 disposable runtime_app direct SQL and HTTP API test source is added; exact-head CI has not yet passed, and the full N.6 matrix remains NOT RUN / NOT PASS.
- O-21 PostgreSQL PoC source is added; exact-head PostgreSQL 18 CI, independent reviewer attestation and production external-anchor/key-operation proofs are NOT RUN / NOT PASS.
- O-45 heartbeat/logout E2E candidate tests are present in PR #86 but their exact-head CI is pending; all remaining O-45 lifecycle tests are NOT RUN / NOT PASS.
- O-46 bootstrap/recovery tests are NOT RUN / NOT PASS.
- Gate D remains OPEN / NOT VERIFIED.
- Independent security reviewer attestation and owner release acceptance remain pending.

## 5. Non-authorization boundary

PR #86 is a Draft review package. No production role, LOGIN credential, membership, grant, revoke, migration, bootstrap credential, OTP, recovery key, SMTP secret, signing key, WORM checkpoint, production listener, feature activation, audit purge, G1 activation, Posting Release or Go-Live is authorized. No Stage 08 item is closed by document registration or CI success alone.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v0.5**
