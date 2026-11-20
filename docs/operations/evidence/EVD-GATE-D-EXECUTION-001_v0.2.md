# Gate D Execution Evidence Register — Update 0.2

| Field | Value |
|---|---|
| Record | EVD-GATE-D-EXECUTION-001 |
| Version | 0.2 |
| Updated | 2026-11-20 |
| Exact tested source SHA | `716cac7a91844dbbd868b9f670a6b3a5732b2f18` |
| Workflow | MD-Gsm Persons Module Gates |
| Workflow run | `44718235960` |
| PostgreSQL | 18.6 (Debian 18.6-1.pgdg12+2) |
| Restore artifact | `gate-d-restore-poc-evidence-44718235960-1` |
| Artifact ID | `14620337392` |
| Artifact digest | `sha256:d3c7fcc89aef9d2de7b6af5f9c5e1f73576db960db01dbaeacfbc4d6fd9ffe4e` |
| Restore script SHA-256 | `3fbe668c5c929856b7cac760549730576d9b156a2fc9e3b513ccf5c383acccb5` |

## 1. Exact-run results

The run on SHA `716cac7a91844dbbd868b9f670a6b3a5732b2f18` produced these results:

| Check | Result | Evidence |
|---|---|---|
| Application-owned migrations | PASS | 8 migrations applied; repeat run applied 0 and reported 8 already applied |
| PostgreSQL runtime LOGIN boundary | PASS — database-level only | Dedicated `mdgsm_ci_runtime_login` inherited only `mdgsm_runtime`; permitted reads passed; DDL, account insert and audit update returned permission-denied errors |
| DR-01 custom-format full restore | PASS | Snapshot, normalized schema catalog and security catalog parity |
| DR-02 plain SQL full restore | PASS | Snapshot and parity |
| DR-03 directory archive parallel restore | PASS | Snapshot and parity |
| DR-04 schema-only restore | PASS | 11 IdentityAccess tables; no row data; schema/security parity |
| DR-05 data-only restore | PASS | Snapshot and parity after schema restore |
| DR-06 selective system-settings restore | PASS | Session policy values preserved; schema parity |
| DR-07 ownership-independent restore | PASS | Snapshot and structural schema parity; ownership/ACL parity intentionally excluded |
| DR-08 corrupted/truncated archive rejection | PASS | `pg_restore` rejected the truncated archive |
| Persons PostgreSQL integration tests | PASS | 36 passed, 0 failed, 0 skipped |
| Restore PoC suite | PASS | 8 of 8 scenarios |

### Structural comparison semantics

The restore comparison now covers:
- A normalized PostgreSQL schema-only DDL digest.
- CHECK-constraint inventory and metadata (schema, table/domain, constraint name/type, validation, deferrability, local/inheritance and key/reference metadata).
- A security catalog fingerprint for schema/relation/routine/type owners, ACLs, default ACLs, non-system role attributes and role memberships.
- Implicit owner self-grants are normalized so that an omitted ACL and an explicit owner-only ACL are treated as equivalent effective privileges.

PostgreSQL 18 can pretty-print equivalent CHECK expressions differently after a dump/restore cycle. Therefore the DDL digest excludes textual CHECK-expression lines and compares their catalog inventory/metadata separately. This is a structural parity test, **not a formal proof that every CHECK expression is semantically equivalent**. Dedicated constraint behavior tests remain necessary before making that stronger claim.

DR-07 intentionally restores without owner/ACL and therefore checks structural schema parity only.

## 2. Runtime LOGIN test boundary

The CI test authenticates a real, run-scoped PostgreSQL LOGIN role and verifies database privilege enforcement. It does **not** yet prove that the running application/API uses that identity end-to-end; the current integration test suite still uses its service connection for fixture/setup. The application-level runtime identity test remains open.

The CI PostgreSQL service uses runner-local trust authentication. This is isolated disposable test infrastructure, not an acceptable production authentication configuration.

## 3. Scope limits of the restore evidence

The source snapshot is the current Infrastructure/IdentityAccess disposable database only. It is not a populated production business database and does not include Finance/Inventory posting data, Journal/Movement/Subledger/Balance data, or external files and keys.

The eight PoCs do not prove:
- WAL archiving, WAL gap detection or point-in-time recovery.
- RPO ≤15 minutes or RTO ≤4 hours for the full system.
- Off-site encrypted backup, retention enforcement or external key/file restore.
- Global role/grant reconstruction on a clean cluster.
- AuditSecurity Hash Chain/checkpoint/signature/WORM continuity.
- Full accounting/inventory reconciliation.

## 4. Installer and deployment evidence

Earlier exact-run preview installer evidence remains:
- Workflow run `44712225234`, source SHA `f8efacd9b3d7808a0cccb5c7a73af02438774ed8`.
- Clean install/uninstall on the build runner and 5/5 hosted Windows runners.
- Preview artifact `MD-Gsm-Persons-Preview-Setup-v0.2.10.exe`.
- Authenticode: **NotSigned**.

That evidence is hosted-runner install/uninstall compatibility only. It does not prove physical-device deployment, five Desktop clients connected concurrently to one API, actual application/API health, first-admin bootstrap, upgrade/forward-fix/rollback, or trusted signing.

The Windows PostgreSQL migration, CodeQL and Windows installer workflows for source SHA `716cac7a91844dbbd868b9f670a6b3a5732b2f18` were still running/pending when this register update was prepared; they must be recorded only after terminal results.

## 5. Gate disposition

**Gate D remains OPEN / NOT VERIFIED.**

This update closes the evidence-staleness issue for the recorded exact run and records the new database-level runtime-role and catalog-parity PoCs. It does not accept full-system recovery or production deployment.

No production migration, production backup/restore, recovery activation, G1 activation, Posting Release or Go-Live is authorized.

**END — EVD-GATE-D-EXECUTION-001 v0.2**
