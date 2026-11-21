# MDG Security Stage 08 — Controlled Register Addendum

| Field | Value |
|---|---|
| Register ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 0.7 / 2026-11-21 |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner-policy reference | ADR-0013, including the explicit session-policy correction dated 2026-11-07 |
| Supersedes | v0.6 as current register; historical versions retained |
| Package under review | PR #86 — Draft; not merged |

## 1. Registered Stage 08 security artifacts

| Artifact | Version | Current status |
|---|---:|---|
| `MDG-SECURITY-STAGE-08-DB-SESSION-RECOVERY-HASHCHAIN-001` | 0.1 | Integrated security design draft; read with reconciliation addendum; no implementation authority |
| `MDG-SECURITY-STAGE-08-RECONCILIATION-001` | 0.2 | Controlled successor addendum; option D recorded; O-20/O-21/O-45/O-46 evidence gates remain open |
| `MDG-SECURITY-STAGE-08-REAUDIT-001` | 0.2 | Internal consistency review only; not external independent attestation |
| `MDG-ARCH-N3-ADR-0013-RECONCILIATION-001` | 0.2 | Owner-confirmed option D; no System Manager account-count cap; one active System Manager session globally |
| `MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001` | 0.5 | Runtime membership/ownership/effective column-privilege proof expanded; exact-head CI pending; full N.6 matrix open |
| `MDG-SECURITY-O21-HASHCHAIN-IMPLEMENTATION-READINESS-001` | 0.3 | Disposable PostgreSQL PoC integration test PASS once on SHA 8e0b3e10…; current package rerun, independent review, external WORM and platform key operations open |
| `MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001` | 0.2 | Five-identity/materialized-object matrix plus runtime_app disposable membership/ownership/column-level proof candidate; current exact-head CI pending; support identity proofs open |
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
- N.6 canonical identities remain `runtime_app`, `migration_runner`, `backup_operator`, `reporting_ro` and `emergency_operator`; no additional worker identity is approved.
- `MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001_v0.5.md` and `MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001_v0.2.md` now record the current materialized object inventory and the expected object/column privilege boundaries.
- PR #86 disposable-only `runtime_app` proof now checks LOGIN flags, membership only in `mdgsm_runtime`, no membership in the four support roles or test-owner placeholder, no schema/relation/function ownership, no CREATE on `identity`/`public`, and selected effective IdentityAccess table/column privileges.
- The candidate also tests cross-module Persons/Finance/temporary/merge/infra read and write denial, schema/table/sequence DDL denial, and role/SET ROLE escalation. GRANT escalation is checked by querying the target role's effective privilege because PostgreSQL can treat an unauthorized GRANT as warning/no-op rather than throwing an error.
- A previous integration run failed one assertion because the test incorrectly expected GRANT to throw. The assertion was corrected to verify that the target role receives no effective privilege; new membership/ownership/column tests were then added. Exact final-SHA PostgreSQL 18 CI and checksum verification remain pending.
- All four supporting identities' own allowlists and separate-credential execution, full live catalog/default ACL/function/sequence/extension inspection and RowScope proof remain OPEN. No production roles, credentials, memberships or grants were changed.
### O-21 — Hash Chain
- PR #86 adds a disposable PostgreSQL PoC candidate with independent writer LOGINs, owner-controlled SECURITY DEFINER append/lock, full typed event envelope, deterministic canonicalization/golden vector, concurrent writes, rollback/backend termination/unknown-commit replay, pinned RSA-PSS test checkpoints and a read-only verifier for tamper/gap detection.
- The PoC integration test passed once on PostgreSQL 18 on SHA `8e0b3e10a96e6e605be6513cc22e19673d8c89ac`, workflow `44910227811`, job `136864370902`. The full run had 37 integration tests: 36 passed and one separate IdentityAccess HTTP runtime-permission assertion failed. The Hash Chain test file blob is unchanged at the current PR head.
- The prior IdentityAccess failure was traced to expecting PostgreSQL GRANT to raise when it can instead return warning/no-op; the assertion now verifies the recipient's effective privilege remains false. The latest package workflow on updated SHA `0f9a4916…` is queued/pending; its PostgreSQL 18 and checksum results are not yet available.
- The source-level PoC PASS does not close O-21. Production AuditSecurity module, transactional outbox, checkpoint scheduling/external WORM provider, production key custody/backup/recovery/revocation, recorded-time non-forgeability, restore continuity, alerts, runbook and independent attestation remain OPEN.
### O-46 — Bootstrap and Recovery
- No secure installer/bootstrap workflow, trusted one-time secret delivery, first-login completion state machine, verified-email proof, Recovery orchestration/endpoint, SMTP adapter or complete quota/idempotency schema is present.
- O-46 v0.2 records that these are implementation blockers, not permission to guess. Bootstrap delivery/local trust, Recovery entrypoint and verified-email proof remain unresolved.
- SMTP provider, dedicated HMAC key custody/rotation, schema delta and WG-0 reservation are not complete. Migration 0009 remains reserved and must not be consumed.
- No raw bootstrap secret, password, OTP, bearer token, recovery key or SMTP credential may enter logs, telemetry, crash artifacts or CI artifacts.

## 4. Test/evidence status

- SEC-T-001..036 remain specifications only; no blanket PASS is claimed.
- O-20 disposable runtime_app direct SQL/HTTP, membership, ownership, selected IdentityAccess column privileges and cross-module denial tests are added. The previous failure on GRANT warning/no-op semantics has been corrected by checking effective target privileges. The current final package workflow `44912323200` on SHA `0f9a491611c65668c7a61df94e26a84115e0e605` is pending; full N.6 acceptance remains NOT RUN / NOT PASS.
- O-21 disposable PoC integration test has one PASS on SHA `8e0b3e10a96e6e605be6513cc22e19673d8c89ac`; current package rerun and independent reviewer attestation remain pending. Production external-anchor/key-operation proofs are NOT RUN / NOT PASS.
- O-45 heartbeat/logout E2E candidate tests are present in PR #86 but their exact-head CI is pending; all remaining O-45 lifecycle tests are NOT RUN / NOT PASS.
- O-46 bootstrap/recovery tests are NOT RUN / NOT PASS.
- Gate D remains OPEN / NOT VERIFIED.
- Independent security reviewer attestation and owner release acceptance remain pending.

## 5. Non-authorization boundary

PR #86 is a Draft review package. No production role, LOGIN credential, membership, grant, revoke, migration, bootstrap credential, OTP, recovery key, SMTP secret, signing key, WORM checkpoint, production listener, feature activation, audit purge, G1 activation, Posting Release or Go-Live is authorized. No Stage 08 item is closed by document registration or CI success alone.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v0.7**
