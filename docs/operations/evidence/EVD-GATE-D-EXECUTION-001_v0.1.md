# Gate D — Execution Evidence Register

| Field | Value |
|---|---|
| Evidence register | EVD-GATE-D-EXECUTION-001 |
| Version | 0.1 |
| Date | 2026-11-20 |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Gate status | **OPEN — NOT VERIFIED** |
| Scope | Executed PoCs and tests for the currently implemented infrastructure/IdentityAccess and preview installer only |

## 1. Exact tested source and workflow identities

The first complete execution source head was `f8efacd9b3d7808a0cccb5c7a73af02438774ed8`. The final evidence-register PR was then re-run on this exact head:

- Final PR head SHA: `2da420c41c2502bd659353104069cc063965241a`
- GitHub pull-request merge SHA used by the final Restore runner: `62f692a8775f9cf8356ea4b146225942c13635ba`
- Base SHA: `feba9f82a81e196bdbfea369214af7f496bbc85b`
- Final Restore/Integration workflow run: [44712917686](https://github.com/Md-gsm/MD-Gsm1/actions/runs/44712917686)
- Initial Windows installer/five-runner workflow run: [44712225234](https://github.com/Md-gsm/MD-Gsm1/actions/runs/44712225234)

The evidence JSON's `commit` field contains the pull-request merge SHA because GitHub sets `GITHUB_SHA` to the merge ref for `pull_request` workflows. The PR head SHA is the exact source branch revision under test; both identifiers are retained to avoid conflating them. The final PR head differs from the first tested head only by checksum-manifest and evidence-register changes; the restore, capacity and installer test logic is unchanged.

A follow-up checksum-only source revision is `5277672012a4946293d45a62b9f49132f24b5d60`. It corrects the manifest to the exact Linux checkout hash for the restore PoC script; the source test/workflow logic is unchanged. Its CI rerun is tracked separately in PR #77.

## 2. Executed evidence

### EVD-GATED-RESTORE-POC

<badge color="success">PASS — 8/8</badge>

- PostgreSQL server: `18.6 (Debian 18.6-1.pgdg12+2)`
- Run: [44712225233](https://github.com/Md-gsm/MD-Gsm1/actions/runs/44712225233)
- Artifact: `gate-d-restore-poc-evidence-44712225233-1`
- Artifact digest: `sha256:32e4e12b781ddd2aaa71f89c295aaf54dc54447f9139c715838ee4c82ddfac38`
- Artifact download: [Restore PoC evidence](https://github.com/Md-gsm/MD-Gsm1/actions/runs/44712225233)
- Source DB: `mdgsm_ci_disposable_44712225233_1`, marked `MDGSM_DISPOSABLE_TEST_DB`
- Snapshot: `11|1|0|0|0|8|8` (11 IdentityAccess tables, one system-settings row, zero accounts/sessions/audit rows, eight migration-history rows)
- Restore script SHA-256: `4db0702b4aeb209d15782f273efe4b8794de6403743345a0144c9fc0dd2be287`
- Full custom dump SHA-256 (first run): `d481bdf8e65b0e773b0c232632bd47deebf37f68e5ee1c48b6aaa09446622c26`
- Final exact-head rerun: [44712917686](https://github.com/Md-gsm/MD-Gsm1/actions/runs/44712917686), artifact ID `14618077507`, artifact digest `sha256:56104295817f2f38d904f7e379ec6c9e0e3b74026dead875cadc7d495742a2fd`
- Final run PR head: `2da420c41c2502bd659353104069cc063965241a`; merge SHA in JSON: `62f692a8775f9cf8356ea4b146225942c13635ba`
- Final full custom dump SHA-256: `38c6cf26c93d4ad09b6b595a70ec62dc1d026da39996cf51751203002bb773bb`

| EVD/Test ID | Executed scenario | Result |
|---|---|---|
| DR-01 | Custom-format full logical backup and restore; snapshot equality | PASS |
| DR-02 | Plain SQL full logical backup and restore; snapshot equality | PASS |
| DR-03 | Directory-format parallel backup and restore; snapshot equality | PASS |
| DR-04 | Schema-only restore; 11 IdentityAccess tables and no row data | PASS |
| DR-05 | Data-only restore onto schema-only target; snapshot equality | PASS |
| DR-06 | Selective restore of IdentityAccess system settings; 5/30s/180s values preserved | PASS |
| DR-07 | Restore without source owner/ACL dependencies | PASS |
| DR-08 | Truncated archive rejected by pg_restore | PASS |

**Scope limitation:** These are eight PostgreSQL *logical restore PoCs* against the existing infrastructure/IdentityAccess schema. They are not eight full-system monthly Restore Drills. They do not include WAL/PITR, off-site encrypted storage, external keys, file assets, canonical AuditSecurity Hash Chain, or accounting/inventory data.

### EVD-GATED-CAPACITY-001

<badge color="success">PASS — PostgreSQL integration</badge>

- Final exact-head run: [44712917686](https://github.com/Md-gsm/MD-Gsm1/actions/runs/44712917686)
- Job: `persons-postgres-migrations` — SUCCESS
- Integration result: 36 passed, 0 failed, 0 skipped.
- The PostgreSQL-backed capacity test launched eight distinct-account login commands concurrently with `MaxActiveUsers=5`.
- Expected and observed: five successful logins and three `SessionCapacityReached` rejections.
- A second session for an already-active account succeeded without consuming another distinct-account slot; the final active distinct-account count remained five.

**Scope limitation:** This proves the server-side IdentityAccess handler/database concurrency policy. It is not a load test of five separate Windows Desktop processes connected to one shared API, and it is not a physical five-computer hardware compatibility test.

### EVD-GATED-INSTALLER-001

<badge color="success">PASS — clean install/uninstall</badge>

- Run: [44712225234](https://github.com/Md-gsm/MD-Gsm1/actions/runs/44712225234)
- Installer: `MD-Gsm-Persons-Preview-Setup-v0.2.10.exe`
- Artifact size: 46,012,714 bytes
- Artifact digest: `sha256:1ca463afd6db8d3c1098726a48667ed25fbe3efee7eadb2dcf85d2181c201afd`
- Executed on Windows hosted runner: silent install, payload verification (`MDGsm.Desktop.exe`, `appsettings.json`, menu catalog), uninstaller registration, silent uninstall and executable removal — PASS.
- Authenticode status: **NotSigned**.

**Scope limitation:** This is the current UI preview installer, explicitly described by its build workflow as having no Persons business database, no database migration and no real-data access. Trusted Windows signing remains OPEN. Application launch/health, initial-admin bootstrap, update/upgrade, forward-fix and rollback were not tested.

### EVD-GATED-FIVE-RUNNER-INSTALL-001

<badge color="success">PASS — 5/5 hosted runners</badge>

The same exact-run installer artifact was downloaded, silently installed, payload-checked and uninstalled on five independent Windows hosted runners:

| Matrix slot | Result |
|---:|---|
| 1 | PASS |
| 2 | PASS |
| 3 | PASS |
| 4 | PASS |
| 5 | PASS |

This is five isolated hosted Windows VMs using the same hosted image family. It is not evidence from five physical computers with different hardware/Windows configurations, and does not establish five real Desktop clients operating concurrently against one API.

## 3. CI reconciliation and current follow-up

The first execution run `44712225233` completed the restore and PostgreSQL integration job successfully, but its overall Persons workflow was not green because `SHA256SUMS.txt` still contained the prior restore-script digest. The exact Linux checkout digest was read from the CI verifier and registered.

The checksum-corrected revision was then verified in [PR #77](https://github.com/Md-gsm/MD-Gsm1/pull/77), head `5277672012a4946293d45a62b9f49132f24b5d60`: its checksum-verification job, Foundation build/test and PostgreSQL migration/restore/integration job passed. PR #77 was closed as superseded by the consolidated evidence PR #78.

On final evidence PR #78, head `2da420c41c2502bd659353104069cc063965241a`, the checksum-verification, Foundation build/test, PostgreSQL migration/restore/integration and other completed Persons gate jobs passed. At the time this register was updated, the Windows PostgreSQL migration rehearsal, Windows installer rerun and CodeQL workflow were still in progress; overall CI is therefore not declared green. The initial checksum failure is not concealed or reclassified as PASS.

## 4. Gate D requirements still NOT RUN / BLOCKED

The following v3.0.3 requirements remain open. No evidence in section 2 closes them.

| Requirement | Current state | Missing evidence |
|---|---|---|
| Full database backup and restore including all business data | BLOCKED | Current schema has no production Finance/Inventory Journal, Movement, Subledger and Balance data to restore |
| WAL archiving, PITR and gap detection | NOT RUN | Configured archive, recover-to-time drills, missing-WAL negative test and measured RPO |
| RPO ≤ 15 minutes / RTO ≤ 4 hours | NOT VERIFIED | Timed end-to-end recovery with database, WAL/PITR, files, keys and roles |
| Off-site encrypted backup and retention | NOT RUN | Independently hosted encrypted copy, key ownership, daily 14d / weekly 8w / monthly 24m / period-close evidence, WAL 14d and monitoring |
| Files and external key restore | BLOCKED | Canonical file-store contract, external key-management/backup and restore verification |
| Roles, grants and report_ro recovery | PARTIAL | Current disposable DB privilege checks passed; global roles/grants backup and clean-cluster restoration not proven |
| Audit/Hash Chain restore and checkpoint continuity | BLOCKED | Canonical AuditSecurity runtime, external signed checkpoints and verifier/restore drill not implemented |
| Journal/Movement/Subledger/Balance tie-out | BLOCKED | Corresponding production posting and inventory data stores/runtime not present; no tie-out can be claimed |
| TLS and deployment topology | NOT RUN | Realistic deployment TLS/certificate/firewall verification |
| Trusted Windows code signing | BLOCKED | Installer Authenticode result is `NotSigned`; approved signing certificate/provider and signature verification required |
| Installer update/upgrade/forward-fix/rollback | NOT RUN | Prior-version installation, update, recovery/rollback and data-preservation drill |
| Initial admin bootstrap and app health | NOT RUN | Clean installed app launch, secure bootstrap credential flow, forced first-login setup and API health proof |
| Five physical computers / five real Desktop clients | PARTIAL | Five hosted VM install tests and DB capacity integration passed; physical-device matrix and five real Desktop-to-one-API concurrency remain |

## 5. Gate decision

**Gate D remains OPEN / NOT VERIFIED. O-28 remains OPEN.**

The executed subset is accepted as reproducible evidence for the current IdentityAccess/infrastructure schema, PostgreSQL logical backup/restore methods, PostgreSQL-backed five-account concurrency, and preview-installer clean install/uninstall on hosted Windows runners.

No claim is made that the full v3.0.3 Restore Drill, PITR, RPO/RTO, encrypted off-site backup, external key/file/audit recovery, accounting/inventory tie-out, Windows signing, update/rollback, or physical five-computer rollout has passed.

No production migration, production backup/restore, credential/key provisioning, G1 activation, Posting Release or Go-Live is authorized by this evidence.

**END — EVD-GATE-D-EXECUTION-001 v0.1**
