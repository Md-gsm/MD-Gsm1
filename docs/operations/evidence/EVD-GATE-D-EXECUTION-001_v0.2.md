# Gate D — Execution Evidence Register

| Field | Value |
|---|---|
| Evidence register | EVD-GATE-D-EXECUTION-001 |
| Version | **0.2** |
| Updated | 2026-11-20 |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Gate status | **OPEN — NOT VERIFIED** |
| Scope | Executed PoCs and tests for the currently implemented infrastructure/IdentityAccess schema and preview installer only |

## 1. Latest exact-source evidence set

The evidence register has been reconciled to the completed CI execution triggered by the Stage 08/Gate D review PR #79.

- Review PR: [#79](https://github.com/Md-gsm/MD-Gsm1/pull/79)
- Exact PR head SHA: `be7e20615438adb2a787c7f7f55bf118212f3927`
- Exact GitHub PR merge SHA tested by the workflows: `40b333a427d3e0dce37fed6657b1e54a51cf5ad4`
- PR base/source baseline: `5a9ffca45ff2446f874a733cb7aee343417828f3`
- PostgreSQL/Linux Persons gates: [run 44714635434](https://github.com/Md-gsm/MD-Gsm1/actions/runs/44714635434) — **SUCCESS**
- Windows preview installer and five-runner matrix: [run 44714635260](https://github.com/Md-gsm/MD-Gsm1/actions/runs/44714635260) — **SUCCESS**
- CodeQL: [run 44714635349](https://github.com/Md-gsm/MD-Gsm1/actions/runs/44714635349) — **SUCCESS**
- Project Charter Owner Gate automation: [run 44714635236](https://github.com/Md-gsm/MD-Gsm1/actions/runs/44714635236) — **SUCCESS** (automated workflow result only; not a human owner acceptance of Gate D).

The evidence JSON records the merge SHA because GitHub supplies the pull-request merge ref to `GITHUB_SHA`. Both the source PR head and merge SHA are recorded to distinguish the reviewed source from the workflow checkout.

## 2. Executed PostgreSQL logical restore PoCs

### EVD-GATED-RESTORE-POC

<badge color="success">PASS — 8/8</badge>

Latest exact-run evidence:

- Run: [44714635434](https://github.com/Md-gsm/MD-Gsm1/actions/runs/44714635434)
- Job: `persons-postgres-migrations` — SUCCESS
- Artifact: `gate-d-restore-poc-evidence-44714635434-1`
- Artifact ID: `14618537325`
- Artifact digest: `sha256:2523d46426ed61b659a0df46dfc3f8358b148c4d480522e625bef7f98bb8866e`
- Artifact size: 996 bytes
- PostgreSQL: `18.6 (Debian 18.6-1.pgdg12+2)`
- Evidence JSON commit: `40b333a427d3e0dce37fed6657b1e54a51cf5ad4`
- Disposable source DB: `mdgsm_ci_disposable_44714635434_1`
- Required marker: `MDGSM_DISPOSABLE_TEST_DB`
- Source snapshot: `11|1|0|0|0|8|8` (11 IdentityAccess tables, one system-settings row, zero accounts, zero sessions, zero audit rows, eight migration-history rows)
- Restore script SHA-256: `4db0702b4aeb209d15782f273efe4b8794de6403743345a0144c9fc0dd2be287`
- Full custom dump SHA-256: `b12066b393d7480e38e5ed73f2e06e959b8047bd18526cab8dc2eb9963a4bbe2`

| EVD/Test ID | Executed scenario | Result |
|---|---|---|
| DR-01 | Custom-format full logical backup and restore; schema/data snapshot matches | PASS |
| DR-02 | Plain SQL full logical backup and restore; schema/data snapshot matches | PASS |
| DR-03 | Directory archive parallel restore; schema/data snapshot matches | PASS |
| DR-04 | Schema-only restore; 11 IdentityAccess tables present and data absent | PASS |
| DR-05 | Data-only restore after schema restore; schema/data snapshot matches | PASS |
| DR-06 | Selective system-settings restore; session policy values preserved | PASS |
| DR-07 | Ownership-independent restore into a fresh target; schema/data snapshot matches | PASS |
| DR-08 | Truncated archive rejected by pg_restore; target remains disposable/quarantined | PASS |

The run output explicitly reports all eight PostgreSQL logical restore PoCs passed. This is an exact-head rerun of the eight scenarios previously recorded in runs 44712225233 and 44712917686.

**Scope limitation:** These are eight PostgreSQL *logical restore PoCs* against the existing infrastructure/IdentityAccess schema. They are not eight full-system monthly Restore Drills. They do not include WAL/PITR, off-site encrypted storage, external keys, file assets, canonical AuditSecurity Hash Chain, or accounting/inventory data.

## 3. PostgreSQL migrations, integration and capacity

### EVD-GATED-CAPACITY-001

<badge color="success">PASS — exact-run PostgreSQL gates</badge>

- Run: [44714635434](https://github.com/Md-gsm/MD-Gsm1/actions/runs/44714635434)
- Job: `persons-postgres-migrations` — SUCCESS
- Job: `persons-postgres-windows-migrations` — SUCCESS
- PostgreSQL on Windows: verified server 18.6.
- Application-owned migrations: versions 1 through 8 applied on first run; second run applied zero new migrations and verified checksum history/idempotence.
- Integration test result: **36 passed, 0 failed, 0 skipped**.
- Capacity scenario: eight concurrent distinct-account login commands with `MaxActiveUsers=5`; five accepted and three rejected with `SessionCapacityReached`.
- A second session for an already-active account did not consume another distinct-account slot; active distinct-account count remained five.

The PostgreSQL job also reports migration repeat/idempotence, IdentityAccess schema invariants, selected least-privilege grant checks and backup/restore verification passed.

**Scope limitation:** This proves the server-side IdentityAccess handler/database concurrency policy. It is not a load test of five separate Windows Desktop processes connected to one shared API, and it is not a physical five-computer hardware compatibility test. The current integration test service connection is not proof that the application was exercised through a dedicated least-privileged LOGIN identity.

## 4. Windows preview installer and five-runner compatibility

### EVD-GATED-INSTALLER-001

<badge color="success">PASS — clean install/uninstall</badge>

- Run: [44714635260](https://github.com/Md-gsm/MD-Gsm1/actions/runs/44714635260)
- Installer: `MD-Gsm-Persons-Preview-Setup-v0.2.10.exe`
- Artifact: `MD-Gsm-Persons-Preview-Installer-v0.2.10`
- Artifact ID: `14619890114`
- Artifact size: 46,002,541 bytes
- Artifact digest: `sha256:2d8d53729bb2b36b28fa12b22b137b041fc262e52d3f7e95dc76a6ced8d51ceb`
- Windows hosted runner: build, silent clean install, payload verification, uninstaller registration and silent uninstall — PASS.
- Authenticode status: **NotSigned**.

### EVD-GATED-FIVE-RUNNER-INSTALL-001

<badge color="success">PASS — 5/5 hosted runners</badge>

The same workflow completed all five independent Windows hosted-runner matrix jobs successfully:

| Matrix slot | Result |
|---:|---|
| 1 | PASS |
| 2 | PASS |
| 3 | PASS |
| 4 | PASS |
| 5 | PASS |

This is five isolated hosted Windows VMs using the hosted runner image family. It is not evidence from five physical computers with different hardware/Windows configurations, and does not establish five real Desktop clients operating concurrently against one API.

**Scope limitation:** This is the UI preview installer; it has no Persons business database, no database migration and no real-data access. Trusted Windows signing remains OPEN. Application launch/health, initial-admin bootstrap, update/upgrade, forward-fix and rollback were not tested.

## 5. CI reconciliation

On the exact PR #79 head/merge execution recorded in section 1:

| Required workflow/job group | Run | Result |
|---|---:|---|
| Persons specification lint | 44714635434 | PASS |
| Foundation build/test | 44714635434 | PASS |
| PostgreSQL migrations/integration | 44714635434 | PASS |
| PostgreSQL Windows migrations | 44714635434 | PASS |
| Architecture boundary | 44714635434 | PASS |
| API contract | 44714635434 | PASS |
| E2E disposable tests | 44714635434 | PASS |
| Security tests | 44714635434 | PASS |
| CI enforcement and merge contract | 44714635434 | PASS |
| Temporary-data retention | 44714635434 | PASS |
| Commit-bound CI evidence generation | 44714635434 | PASS |
| Windows preview installer | 44714635260 | PASS |
| Five hosted Windows install/uninstall jobs | 44714635260 | PASS (5/5) |
| CodeQL C# | 44714635349 | PASS |

The commit-bound CI evidence job reports all required gates and .NET tests succeeded and generated a redacted evidence artifact. This means the listed CI workflows are green for the reviewed PR #79 merge SHA. It does **not** mean Gate D's unimplemented production requirements are satisfied.

Historical records remain preserved: initial restore run 44712225233, final earlier evidence run 44712917686, and earlier installer run 44712225234. They are superseded for latest-run status by the exact PR #79 execution above; they are not deleted or reclassified.

## 6. Gate D requirements still NOT RUN / BLOCKED

The following v3.0.3 requirements remain open. No evidence in sections 2–5 closes them.

| Requirement | Current state | Missing evidence |
|---|---|---|
| Full database backup and restore including all business data | BLOCKED | Current schema has no production Finance/Inventory Journal, Movement, Subledger and Balance data to restore |
| WAL archiving, PITR and gap detection | NOT RUN | Configured archive, recover-to-time drills, missing-WAL negative test and measured RPO |
| RPO ≤ 15 minutes / RTO ≤ 4 hours | NOT VERIFIED | Timed end-to-end recovery with database, WAL/PITR, files, keys and roles |
| Off-site encrypted backup and retention | NOT RUN | Independently hosted encrypted copy, key ownership, daily 14d / weekly 8w / monthly 24m / period-close evidence, WAL 14d and monitoring |
| Files and external key restore | BLOCKED | Canonical file-store contract, external key-management/backup and restore verification |
| Roles, grants and report_ro recovery | PARTIAL | Current disposable DB privilege checks passed; global roles/grants backup and clean-cluster restoration not proven |
| Runtime least-privilege LOGIN verification | NOT RUN | Application operations exercised through dedicated LOGIN role with denied privileged operations asserted |
| Audit/Hash Chain restore and checkpoint continuity | BLOCKED | Canonical AuditSecurity runtime, external signed checkpoints and verifier/restore drill not implemented |
| Journal/Movement/Subledger/Balance tie-out | BLOCKED | Corresponding production posting and inventory data stores/runtime not present; no tie-out can be claimed |
| TLS and deployment topology | NOT RUN | Realistic deployment TLS/certificate/firewall verification |
| Trusted Windows code signing | BLOCKED | Installer Authenticode result is `NotSigned`; approved signing certificate/provider and signature verification required |
| Installer update/upgrade/forward-fix/rollback | NOT RUN | Prior-version installation, update, recovery/rollback and data-preservation drill |
| Initial admin bootstrap and app health | NOT RUN | Clean installed app launch, secure bootstrap credential flow, forced first-login setup and API health proof |
| Five physical computers / five real Desktop clients | PARTIAL | Five hosted VM install tests and DB capacity integration passed; physical-device matrix and five real Desktop-to-one-API concurrency remain |

## 7. Audit finding reconciliation

Finding F-01 in `MDG-STAGE-08-GATED-REVIEW-001 v0.1` identified that EVD v0.1 was stale relative to the then-current reviewed source SHA and newer workflow runs.

This v0.2 update records the exact PR #79 head SHA, merge SHA, final completed Linux and Windows PostgreSQL gates, all eight restore PoCs, 36 integration tests, installer artifact digest, 5/5 hosted-runner results and CodeQL outcome. F-01 is therefore **addressed as an evidence-register freshness issue** for this execution set. It does not close the separate technical findings F-02 through F-08.

## 8. Gate decision

**Gate D remains OPEN / NOT VERIFIED. O-28 remains OPEN.**

The executed subset is accepted as reproducible evidence for the current IdentityAccess/infrastructure schema, PostgreSQL logical backup/restore methods, PostgreSQL-backed five-account concurrency, application-owned PostgreSQL 18 migrations on Linux and Windows, and preview-installer clean install/uninstall on five hosted Windows runners.

No claim is made that the full v3.0.3 Restore Drill, PITR, RPO/RTO, encrypted off-site backup, external key/file/audit recovery, accounting/inventory tie-out, Windows signing, update/rollback, or physical five-computer rollout has passed.

No production migration, production backup/restore, credential/key provisioning, G1 activation, Posting Release or Go-Live is authorized by this evidence.

**END — EVD-GATE-D-EXECUTION-001 v0.2**
