# MDG Security Stage 08 — Controlled Register Addendum

| Field | Value |
|---|---|
| Register ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 1.0 / 2026-11-21 |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner-policy reference | ADR-0013, including the explicit session-policy correction dated 2026-11-07 |
| Supersedes | v0.9 as current register; historical versions retained |
| Package under review | PR #86 — Draft; not merged |

## 1. Registered Stage 08 security artifacts

| Artifact | Version | Current status |
|---|---:|---|
| `MDG-SECURITY-STAGE-08-DB-SESSION-RECOVERY-HASHCHAIN-001` | 0.1 | Integrated security design draft; read with reconciliation addendum; no implementation authority |
| `MDG-SECURITY-STAGE-08-RECONCILIATION-001` | 0.2 | Controlled successor addendum; option D recorded; O-20/O-21/O-45/O-46 evidence gates remain open |
| `MDG-SECURITY-STAGE-08-REAUDIT-001` | 0.2 | Internal consistency review only; not external independent attestation |
| `MDG-ARCH-N3-ADR-0013-RECONCILIATION-001` | 0.3 | Option D and O-25 owner identity model reconciled; SYSTEM_MANAGER role-to-code mapping blocker recorded; global session enforcement not implemented |
| `MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001` | 0.8 | runtime_app disposable proof PASS on SHA 7ef35cca…; email-only Recovery code-runtime access removed in source; fresh CI pending; full N.6 matrix open |
| `MDG-SECURITY-O21-HASHCHAIN-IMPLEMENTATION-READINESS-001` | 0.4 | Disposable PostgreSQL PoC integration test PASS in 37/37 suite on SHA 7ef35cca…; production module, external WORM, key custody and independent review open |
| `MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001` | 0.5 | Five-identity/materialized-object matrix; runtime_app proof PASS on SHA 7ef35cca…; offline recovery-code runtime rights removed in source; refreshed CI pending |
| `MDG-SECURITY-O45-SESSION-LIFECYCLE-AUDIT-001` | 0.1 | Source audit; heartbeat/logout candidate added in PR #86; remaining lifecycle and tests open |
| `MDG-SECURITY-O46-BOOTSTRAP-RECOVERY-AUDIT-001` | 0.3 | Blocker audit plus email-only/offline-code least-privilege correction; no Bootstrap/Recovery runtime implementation; trust/verified-email/SMTP/key/schema gates open |

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
- Historical five-user/computer seat wording is clarified as five distinct active accounts system-wide.
- Fixed absolute TTL and one-session-per-account rule are superseded. Ordinary accounts may have multiple active sessions; heartbeat 30 seconds and lease 180 seconds remain the approved liveness contract.
- **Owner option D (2026-11-21):** no N.3-specific numeric cap on provisioned System Manager accounts; at most one active System Manager session globally across all System Manager accounts.
- O-25 (2026-11-19) additionally says the owner has one account carrying OWNER, GENERAL_MANAGER and SYSTEM_MANAGER together, and SYSTEM_MANAGER alone does not confer commercial/financial authority.
- IdentityAccess code currently has `super_admin` and `manager` but no `SYSTEM_MANAGER`/`OWNER`/`GENERAL_MANAGER` key. `super_admin` carries all permissions, so equating it with SYSTEM_MANAGER requires an explicit controlled mapping to preserve O-25's least-authority rule. N.3 v0.3 records this as an implementation blocker; global one-session enforcement is NOT implemented.
### O-20 — Database privileges
- The current object inventory and five-identity expected matrix are recorded in O-20 audit v0.8 and role/object matrix v0.5.
- The disposable `runtime_app` direct SQL + HTTP API proof passed on SHA `7ef35cca3b3c86757ce1893656f9b6f617cdb4d9`: PostgreSQL 18 integration job `136871018414` reported 37/37; checksum job `136871018380` passed.
- Since that PASS, PR #86 removed `ConsumeRecoveryCodeAsync` from the application port and PostgreSQL store, removed runtime SELECT/UPDATE on `identity.recovery_codes`, added an idempotent REVOKE ALL for legacy grants, and expanded the disposable test matrix to 16 checks plus direct denial probes.
- The offline-code least-privilege change is source-updated but **fresh exact-head CI is pending**. The prior 37/37 PASS does not validate these later changes.
- Full N.6 catalog/object/default ACL/function/sequence/extension/RowScope proof and independent review remain OPEN. No production grants, credentials, memberships or ownership were changed.
### O-21 — Hash Chain
- The disposable PostgreSQL Hash Chain PoC integration test passed on SHA `7ef35cca3b3c86757ce1893656f9b6f617cdb4d9` as part of the same 37/37 PostgreSQL 18 integration suite (job `136871018414`).
- The test covers separate writer LOGINs, append serialization, rollback, backend termination, unknown-commit replay, checkpoint signature verification and read-only tamper/gap detection.
- Production AuditSecurity ledger, transactional outboxes, external WORM anchor, production key custody/backup/recovery/revocation, non-forgeable recorded-time authority, restore continuity, alerts, runbook and independent attestation remain OPEN.

### O-45 — Session lifecycle
- Existing token digest, PostgreSQL session validation, lease, capacity serialization and password-change revoke-all remain partial foundations.
- PR #86 adds heartbeat and idempotent current-session logout, with PostgreSQL `clock_timestamp()` as the IdentityAccess database clock.
- HTTP E2E exercises authenticated heartbeat, logout of only the presented session, preservation of another session for the same account, repeated logout and a two-day host-clock skew. These assertions passed within the 37/37 PostgreSQL 18 integration suite on SHA `7ef35cca…`.
- Token rotation/overlap/unknown-commit policy, stale-session cleanup, account-disable revoke, full API default-deny, client crash/reconnect and threat-model coverage remain OPEN.
### O-46 — Bootstrap and Recovery
- Bootstrap/Recovery routes, handlers, verified-email proof, SMTP adapter, key-management and end-to-end orchestration remain unimplemented and disabled.
- O-46 v0.3 records that offline recovery-code consumption was removed from the application port/store and the runtime grant script now revokes all access to the dormant `identity.recovery_codes` table, consistent with the approved email-only phase.
- This least-privilege source correction awaits a fresh exact-head PostgreSQL 18 CI run; it does not implement email OTP recovery or resolve the trusted Bootstrap/Recovery entrypoint, verified-email ownership, SMTP/key custody, migration schema reservation or independent review blockers.
- Migration 0009 remains RESERVED. No offline code issuance, SMS fallback, bootstrap credential or runtime recovery endpoint is authorized.
## 4. Test/evidence status

- SEC-T-001..036 remain specifications only; no blanket PASS is claimed.
- O-20 disposable runtime_app direct SQL/HTTP and selected privilege proof passed on SHA `7ef35cca…` (37/37 PostgreSQL 18 integration tests; checksum gate PASS). Later email-only Recovery code-access removal and expanded 16-assertion test have not yet passed fresh exact-head CI. Full N.6 acceptance remains NOT RUN / NOT PASS.
- O-21 disposable PoC integration test passed on SHA `7ef35cca…` within the 37/37 PostgreSQL 18 suite. Production external-anchor/key-operation proofs and independent reviewer attestation remain NOT RUN / NOT PASS.
- O-45 heartbeat/current-session logout E2E candidate tests passed within the 37/37 PostgreSQL 18 suite; remaining lifecycle and threat-model coverage is NOT RUN / NOT PASS.
- O-46 Bootstrap/Recovery tests are NOT RUN / NOT PASS. Offline recovery-code runtime access has been removed in source only; exact-head CI pending.
- Gate D remains OPEN / NOT VERIFIED.
- At the last exact-SHA check, Project Charter Owner Gate, CodeQL and Windows Preview Installer passed; the Persons Module Gates workflow still had its Windows PostgreSQL migration job in progress. Final package completion remains pending.
- Independent security reviewer attestation and owner release acceptance remain pending.

## 5. Non-authorization boundary

PR #86 is a Draft review package. No production role, LOGIN credential, membership, grant, revoke, migration, bootstrap credential, OTP, recovery key, SMTP secret, signing key, WORM checkpoint, production listener, feature activation, audit purge, G1 activation, Posting Release or Go-Live is authorized. No Stage 08 item is closed by document registration or CI success alone.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v1.0**
