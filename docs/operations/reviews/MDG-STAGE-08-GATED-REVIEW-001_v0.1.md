# Stage 08 / Gate D — Independent Technical Re-Audit and Acceptance Record

| Field | Value |
|---|---|
| Review ID | MDG-STAGE-08-GATED-REVIEW-001 |
| Version | 0.1 |
| Review date | 2026-11-20 |
| Reviewed source head | `5a9ffca45ff2446f874a733cb7aee343417828f3` |
| Gate D evidence PR | #78 |
| Stage 08 design PR | #74 |
| Governing architecture | `MDG-ARCH-MASTER-001 v3.0.3` |
| Review type | Separate adversarial technical re-audit of the integrated design and available execution evidence |
| Reviewer limitation | Conducted by ChatGPT as a separate review pass. It is not an independently authenticated external human/organization attestation and must not be represented as one. |
| Overall disposition | **DESIGN DOCUMENT: TECHNICALLY ACCEPTABLE WITH STRICT SCOPE LIMITS. GATE D: NOT ACCEPTED / OPEN.** |

## 1. Executive decision

The Stage 08 security specification is coherent as an integrated design-only document and preserves the current owner decisions and v3.0.3 authority boundary. The evidence in Gate D proves a limited set of repeatable PoCs for the currently implemented Infrastructure/IdentityAccess schema and preview installer.

The evidence does **not** establish production-grade full-system recovery, PITR, RPO/RTO, off-site backup, external key/file recovery, AuditSecurity Hash Chain operation, accounting/inventory reconciliation, trusted installer signing, application bootstrap/health, update/rollback, or five physical Desktop clients.

Accordingly:

1. **Stage 08 design specification:** technically acceptable as a design artifact only, subject to the findings and explicit exclusions in this report. This is not an implementation or release acceptance.
2. **Gate D:** remains **OPEN / NOT VERIFIED** and is not accepted.
3. **Independent attestation:** not satisfied by this review; a separately authenticated reviewer must review the exact final SHA if the project gate requires independent human review.
4. No G1 activation, production migration, recovery enablement, Posting Release or Go-Live is authorized.

## 2. Nine-domain audit

| # | Audit domain | Result | Basis |
|---:|---|---|---|
| 1 | Architecture authority and decision consistency | PASS — design | Stage 08 is subordinate to v3.0.3; it preserves 30s heartbeat, 180s lease, five distinct active accounts, no absolute TTL, no per-account cap, email-only recovery and AuditSecurity ownership. |
| 2 | PostgreSQL roles, trust boundaries and transport | PARTIAL — design only | Role separation and fail-closed TLS/secret requirements are described. Exact production grants, TLS topology, external secret store and deployment proof are not established. Existing grant checks use a disposable DB; the application integration suite itself connects as the PostgreSQL service user, not a dedicated LOGIN runtime identity. |
| 3 | Session, token, lockout and concurrency | PARTIAL — selected runtime evidence | PostgreSQL integration proves lockout persistence, heartbeat lease update/revocation and the five-distinct-account concurrency behavior. It does not prove a full five-process Windows Desktop-to-one-API deployment or all cross-process failure/race cases in the Stage 08 test catalogue. |
| 4 | Account recovery | DESIGN ONLY — NOT IMPLEMENTED/NOT RUN | Email OTP/HMAC, verified-email, SMTP, quotas, replay, provider failure and mailbox-threat controls are specified. Recovery remains disabled; SEC-T-012..018 have no runtime PASS evidence. |
| 5 | AuditSecurity canonical Hash Chain | DESIGN ONLY — NOT IMPLEMENTED/NOT RUN | Stream sequence, canonical serialization, signed external checkpoints, WORM anchor and verifier are specified. The existing IdentityAccess security-audit immutability trigger is not a canonical AuditSecurity Hash Chain. SEC-T-019..030 remain NOT RUN. |
| 6 | Backup, restore and recovery objectives | PARTIAL — narrow logical PoC | DR-01..DR-08 passed against the current Infrastructure/IdentityAccess disposable schema. Source snapshot contains zero accounts, sessions and audit rows. There is no WAL/PITR, full business data, external key/file restore, off-site encryption, timed RPO/RTO or Journal/Movement/Subledger tie-out evidence. |
| 7 | Windows installer and client deployment | PARTIAL — preview only | Run 44713440756 on reviewed head 5a9ffca passed build, clean install/uninstall and five hosted Windows runner installs. The installer is unsigned; launch/health, bootstrap, upgrade, forward-fix, rollback and five physical machines are unproven. |
| 8 | Exact-SHA CI and evidence integrity | PARTIAL — current run incomplete | On head 5a9ffca, CodeQL run 44713440640 passed, Windows preview installer run 44713440756 passed, and the Linux Persons gates in run 44713440757 passed. At review time, `persons-postgres-windows-migrations` in run 44713440757 was still in progress. EVD v0.1 still describes the older 2da420... run state and must be reconciled to the newer 5a9... run before it can be treated as the current final evidence register. |
| 9 | Governance, independent review and release acceptance | BLOCKED | PR #74 and PR #78 have no submitted review records. This report is not an external attestation. Gate D and O-28 remain open; no owner release authorization is evidenced. |

## 3. Findings requiring closure

### F-01 — Evidence register is stale relative to the reviewed head
**Severity: HIGH — evidence-control blocker**

The current reviewed source head is `5a9ffca45ff2446f874a733cb7aee343417828f3`. The EVD-GATE-D-EXECUTION-001 v0.1 still identifies `2da420c41c2502bd659353104069cc063965241a` as its final PR head and says the Windows migration, installer rerun and CodeQL were in progress. Newer exact-head runs exist:
- Persons Module Gates: 44713440757 — Linux jobs passed; Windows PostgreSQL migration was still running at review time.
- Windows Preview Installer: 44713440756 — success, including five hosted runner installs.
- CodeQL: 44713440640 — success.

**Required:** after run 44713440757 reaches a terminal result, update EVD with exact head/run/job outcomes, artifact IDs/digests and the final checksum manifest. Do not state all checks passed until every required job has a terminal successful result.

### F-02 — Restore snapshot is not a full structural-integrity comparison
**Severity: HIGH — limits the strength of the PoC claim**

The restore script compares a compact row-count/version snapshot: IdentityAccess table count, system-settings rows, account/session/audit row counts and migration-history count/version. It does not compare a canonical catalog fingerprint covering columns/types, constraints, indexes, triggers, sequences, extensions, ownership, grants and default privileges. The source also has zero account/session/audit rows.

**Required:** retain the current 8/8 result as logical restore smoke-PoC evidence, but do not describe it as complete structural or populated-database recovery. Add catalog/object parity checks and a separately populated disposable fixture before claiming stronger restore integrity.

### F-03 — Runtime least-privilege behavior is not exercised under a runtime login
**Severity: HIGH — security-evidence gap**

The workflow creates/checks the `mdgsm_runtime` NOLOGIN role and verifies selected privilege predicates. The PostgreSQL integration suite uses the service connection configured as the PostgreSQL CI user. It does not authenticate the application through a dedicated least-privileged LOGIN role that is a member of `mdgsm_runtime`.

**Required:** create a disposable, run-scoped LOGIN role with no elevated membership, grant only the reviewed runtime role, then run the application migration boundary and required IdentityAccess operations through that identity. Assert denied DDL, ownership, account provisioning, role mutation, audit update/delete and other prohibited operations.

### F-04 — Recovery is design-only
**Severity: RELEASE BLOCKER for recovery activation**

Verified-email proof, SMTP delivery, external recovery HMAC key deployment/rotation, atomic quota enforcement, provider-failure behavior and replay/unknown-commit tests are not implemented or run.

**Required:** keep recovery disabled until SEC-T-012..018 pass on an exact SHA and an independent security reviewer accepts the threat model.

### F-05 — Canonical AuditSecurity Hash Chain is absent
**Severity: RELEASE BLOCKER for audit-integrity acceptance**

The current IdentityAccess security audit table/trigger does not implement the Stage 08 canonical AuditSecurity stream, signed external checkpoints, WORM anchoring or read-only verifier.

**Required:** do not claim Hash Chain implementation or restore acceptance. Implement the owning AuditSecurity contract, canonical vectors, checkpoint/signature operations and SEC-T-019..030 before accepting that scope.

### F-06 — Full Gate D recovery objectives remain unproven
**Severity: GATE BLOCKER**

WAL archiving/PITR/gap detection, measured RPO ≤15 minutes, RTO ≤4 hours, off-site encrypted retention, external file/key restore, global role/grant restore and full Journal/Movement/Subledger/Balance tie-out remain NOT RUN/BLOCKED.

**Required:** retain Gate D OPEN until end-to-end timed restore evidence includes all authoritative business data and external dependencies.

### F-07 — Preview installer is unsigned and lifecycle coverage is incomplete
**Severity: GATE BLOCKER**

The successful preview installer has Authenticode status NotSigned. The workflow verifies installed files and uninstallation, not actual app launch/API health or first-admin bootstrap. Upgrade/forward-fix/rollback and data-preservation drills are absent.

**Required:** approved signing identity and valid signature verification; installed-app startup/bootstrap/health checks; upgrade and rollback evidence.

### F-08 — Independent reviewer and owner acceptance are not recorded
**Severity: GOVERNANCE BLOCKER**

No submitted review records were returned for PR #74 or PR #78. The present review is a separate adversarial pass, but not an external reviewer identity or owner signature.

**Required:** record an authenticated independent review against the final exact SHA, resolve all blocking findings, and record the owner’s formal scope-specific acceptance. No approval should be inferred from this report alone.

## 4. Evidence reviewed

- Stage 08 design: `docs/security/MDG-SECURITY-STAGE-08-DB-SESSION-RECOVERY-HASHCHAIN-001_v0.1.md`
- Stage 08 internal re-audit: `docs/security/MDG-SECURITY-STAGE-08-REAUDIT-001_v0.1.md`
- Stage 08 register: `docs/security/MDG-SECURITY-STAGE-08-REGISTER-001_v0.1.md`
- Gate D evidence register: `docs/operations/evidence/EVD-GATE-D-EXECUTION-001_v0.1.md`
- Restore PoC: `scripts/ci/run_postgres_restore_poc.sh`
- PostgreSQL integration test: `tests/MDGsm.Domain.Tests/IdentityAccess/IdentityAccessPostgresIntegrationTests.cs`
- Runtime grant script: `database/operations/identityaccess-runtime-role-grants.sql`
- CI workflows: Persons Module Gates and Windows Preview Installer.
- Exact-head workflow runs: 44713440757, 44713440756, 44713440640.
- Prior restore evidence: 44712917686; prior preview installer evidence: 44712225234.

## 5. Acceptance record

| Deliverable/scope | Decision |
|---|---|
| Stage 08 integrated security design as a design artifact | **TECHNICALLY ACCEPTABLE WITH LIMITATIONS** |
| Stage 08 implementation / recovery activation | **NOT ACCEPTED — NOT IMPLEMENTED / NOT RUN** |
| AuditSecurity Hash Chain implementation | **NOT ACCEPTED — NOT IMPLEMENTED / NOT RUN** |
| Gate D full backup/restore and recovery | **NOT ACCEPTED — OPEN / NOT VERIFIED** |
| G1 / Posting Release / production / Go-Live | **NOT AUTHORIZED** |
| Independent external reviewer attestation | **PENDING** |
| Owner's formal acceptance | **PENDING** |

This review accepts only the coherence and completeness of the Stage 08 design specification as a controlled design artifact. It does not accept the Stage 08 runtime, close Gate D, or override any owner, accounting, platform, legal or independent-review gate.

**END — MDG-STAGE-08-GATED-REVIEW-001 v0.1**
