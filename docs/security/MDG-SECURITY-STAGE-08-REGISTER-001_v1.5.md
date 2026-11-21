# MDG Security Stage 08 — Controlled Register Addendum

| Field | Value |
|---|---|
| Register ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 1.5 / 2026-11-21 |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner-policy reference | ADR-0013, including the explicit session-policy correction dated 2026-11-07 |
| Supersedes | v1.2 as current register; historical versions retained |
| Package under review | PR #86 — Open, non-Draft; not merged |

## 1. Registered Stage 08 security artifacts

| Artifact | Version | Current status |
|---|---:|---|
| `MDG-SECURITY-STAGE-08-DB-SESSION-RECOVERY-HASHCHAIN-001` | 0.1 | Integrated security design draft; read with reconciliation addendum; no implementation authority |
| `MDG-SECURITY-STAGE-08-RECONCILIATION-001` | 0.5 | Controlled successor; N.3/O-25 gap and O-20/O-21/O-45/O-46 exact PostgreSQL evidence documented |
| `MDG-SECURITY-STAGE-08-REAUDIT-001` | 0.2 | Internal consistency review only; not external independent attestation |
| `MDG-ARCH-N3-ADR-0013-RECONCILIATION-001` | 0.3 | Option D and O-25 owner identity model reconciled; SYSTEM_MANAGER role-to-code mapping blocker recorded; global session enforcement not implemented |
| `MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001` | 1.0 | runtime_app 16-assertion privilege proof, including offline-code access denial, PASS on SHA 284ac9db…; full N.6 matrix open |
| `MDG-SECURITY-O21-HASHCHAIN-IMPLEMENTATION-READINESS-001` | 0.6 | Owner-approved RSA-3072/RSA-PSS/SHA-256 and Object Lock Compliance policy; production module, provider proof and independent review open |
| `MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001` | 0.7 | Owner-approved five-identity least-privilege policy baseline; executable support-identity/catalog/RowScope proof remains open |
| `MDG-SECURITY-O45-SESSION-LIFECYCLE-AUDIT-001` | 0.2 | Heartbeat/current-session logout and database-clock E2E PASS in 37/37 suite on SHA 284ac9db…; remaining lifecycle open |
| `MDG-SECURITY-O46-BOOTSTRAP-RECOVERY-AUDIT-001` | 0.4 | Email-only/offline-code runtime denial PASS on SHA 284ac9db…; no Bootstrap/Recovery runtime implementation; trust/verified-email/SMTP/key/schema gates open |

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
- Exact SHA `284ac9dbb94fbbd05d19de18146ccc27d35d8cbf` passed the full PostgreSQL 18 integration stage: workflow `44914448417`, job `136876940898`, **37 passed / 0 failed / 0 skipped**.
- The successful suite includes runtime_app membership/ownership and 16 effective IdentityAccess privilege assertions, denial of `identity.recovery_codes` SELECT/UPDATE under the email-only policy, HTTP login/session lifecycle, heartbeat/current-session logout/database clock, and the Hash Chain PoC.
- The checksum gate `persons-spec-lint`, job `136876940885`, passed: **459 tracked files** matched `SHA256SUMS.txt` on that SHA.
- O-20 remains OPEN at full N.6 level: four support identity positive/negative credential workflows, all database ACL/default ACL/object inventory, RowScope and independent review are not complete.
- O-21 remains OPEN at production level: AuditSecurity production module, transactional outboxes, external WORM anchor, platform key custody/recovery/revocation, restore continuity, alerts, runbook and independent review are not complete.
- O-45 remains OPEN beyond the tested heartbeat/logout/database-clock path: token rotation, stale-session cleanup, account-disable revoke, full API default-deny and crash/reconnect threat coverage remain.
- O-46 remains NOT IMPLEMENTED: only offline-code runtime access denial is tested. No Bootstrap credential path, email OTP Recovery handler, verified-email proof, SMTP, HMAC key provider or runtime endpoint has been enabled.
- Gate D remains OPEN / NOT VERIFIED. PR #86 is open, non-Draft and unmerged; this status is not operational acceptance.
- On SHA `284ac9dbb94fbbd05d19de18146ccc27d35d8cbf`, CodeQL (`44914448284`), Windows Preview Installer (`44914448335`) and Project Charter Owner Gate (`44914448279`) completed PASS. The Linux build/tests and PostgreSQL 18 integration job passed; `persons-spec-lint` passed (459 files). The required Windows PostgreSQL migration job `136876940873` was CANCELLED while subsequent controlled documentation/manifest commits were pushed; the `persons-ci-evidence` job consequently failed its required-gate enforcement because that job's result was `cancelled`, not because the Linux PostgreSQL integration suite failed. The final branch head after these evidence successors requires a fresh uninterrupted full workflow run.
- Independent security reviewer attestation and owner release acceptance remain pending.

## 5. Non-authorization boundary

PR #86 is an open review package (currently non-Draft, not merged and not accepted). No production role, LOGIN credential, membership, grant, revoke, migration, bootstrap credential, OTP, recovery key, SMTP secret, signing key, WORM checkpoint, production listener, feature activation, audit purge, G1 activation, Posting Release or Go-Live is authorized. No Stage 08 item is closed by document registration or CI success alone.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v1.5**


## 4.1 Owner approval recorded — O-20 and O-21 (2026-11-21)

The owner explicitly approved the proposed decisions:
- O-20: five distinct canonical identities, least-privilege scopes, NOLOGIN schema/object ownership, no default superuser/role-creation/replication/BYPASSRLS privileges, positive/negative PostgreSQL proofs, and mandatory RowScope before reporting grants.
- O-21: SHA-256 hash chain; RSA-3072 / RSA-PSS / SHA-256 checkpoint signatures; Platform-managed key service; independent Object Storage with Object Lock Compliance mode (or proven equivalent); read-only verifier; restore comparison and quarantine on mismatch.

The detailed accepted contracts are recorded in O-20 matrix v0.8 and O-21 readiness v0.6. These owner decisions resolve policy choices only. They do not represent implementation, deployment, test PASS, production provisioning or security closure.

### Next controlled execution
- O-20: implement disposable five-identity role/ownership/catalog tests first; then add only those support-identity grants whose owning workflow exists and can be positively and negatively tested. Reporting and emergency roles remain deny-all for data until their view/allow-list contracts exist.
- O-21: implement provider-neutral interfaces and the production append/verify/outbox core; then bind a selected Platform key service and immutable Object Lock provider in a deployment-specific adapter. No provider-specific service or credential is created by this approval.
- Recompute and validate the complete SHA256SUMS manifest; run the full exact-head CI; request a fresh independent review because this commit changes the reviewed PR head.

The PR remains open and unmerged. O-20 and O-21 remain OPEN pending executable evidence and independent acceptance.

## 4.2 Ordered execution update — O-20 followed by O-21 (2026-11-21)

The owner-approved two-step execution order is recorded as follows.

### Step 1 — O-20 disposable database identity proof

Added `tests/MDGsm.Domain.Tests/Security/O20SupportIdentityPostgresProofTests.cs`. The test creates four unique ephemeral support LOGINs in the marked disposable PostgreSQL database and verifies their role attributes, disabled inheritance, zero membership edges, no object ownership, no effective operational grants and direct denial of representative data/DDL/function commands. The test source is committed; exact-head PostgreSQL 18 execution is PENDING.

This is intentionally a fail-closed baseline, not proof of the approved positive capabilities. Migration-only SET ROLE/manifest execution, backup/isolated restore, reporting RowScope and emergency allow-list lifecycle remain unimplemented and ungranted. The full catalog/default-ACL inventory and independent review are still open. O-20 remains OPEN.

### Step 2 — O-21 Hash Chain PoC hardening

The disposable PoC now uses ephemeral RSA-3072 keys with RSA-PSS/SHA-256. Its signed checkpoint envelope includes schema/canonicalization/hash identifiers, stream/sequence/head, key ID, UTC timestamp and a previous-checkpoint reference. Rotation links a new-key checkpoint to the prior content reference; tampered predecessor reference, unsupported canonicalization version and unsupported hash algorithm are rejected. The prior two-writer, atomicity, idempotency, rollback/crash, canonicalization, read-only verifier and tamper/deletion scenarios remain in the same test.

Exact-head PostgreSQL 18 execution is PENDING. This remains a test-only PoC with ephemeral keys and a local temporary checkpoint file; no external immutable Object Lock anchor, Platform key service, production AuditSecurity/outbox integration, production recovery/alerts/runbook or independent verifier deployment is claimed. O-21 remains OPEN.

### Exact-head acceptance sequence

1. Run the complete PR workflow on the final SHA, including PostgreSQL 18 integration, Windows migration, checksum, CodeQL, installer and required-gate evidence.
2. Inspect the actual logs for the two changed integration tests and verify cleanup of disposable roles/schema and temporary checkpoint files.
3. Resolve any failure before proceeding; do not report PASS from source inspection alone.
4. Request fresh independent review after the final SHA; prior approvals do not transfer across changed commits.
5. Keep PR #86 unmerged until O-20/O-21 evidence, remaining Stage 08 items, owner acceptance and independent review are all satisfied.

No production roles, credentials, grants, migrations, keys, WORM storage, bootstrap/recovery secrets or feature activation are authorized by these steps.


## 8. O-20 controlled implementation progress — 2026-11-21

PR #86 latest candidate SHA at this record: `dffa5c1fbb6b79a7b0304048ce4d98816e29fd9b`.

- Runtime account provisioning: the `PostgresIdentityAccessStores.SaveAsync` insert fallback has been removed. A missing account fails closed; the disposable runtime E2E test checks this using the same `runtime_app` connection identity as the API.
- Migration support identity: the disposable proof grants `migration_runner` exactly one non-inheriting, non-admin SET ROLE path to a NOLOGIN schema owner. It verifies owner-context DDL and reset to the login identity. This is not yet proof that the actual migrator process uses that login or that production owner mappings are registered.
- Backup/reporting/emergency: still deny-all and explicitly NOT IMPLEMENTED until logical backup/isolated restore, RowScope views, and emergency allow-list/expiry/revocation/audit contracts are implemented and proven.
- Catalog proof: scoped PUBLIC ACL and default ACL assertions were added; the complete database-wide object/ACL inventory and evidence digest remain open.
- Exact-head workflow run `44922854058` was queued. No PASS is claimed until every required job completes and logs are inspected.

O-20 remains OPEN. O-21 remains OPEN. No production provisioning or merge is authorized by this progress note.
