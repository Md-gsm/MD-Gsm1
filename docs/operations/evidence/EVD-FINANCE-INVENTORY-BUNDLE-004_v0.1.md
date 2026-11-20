# Finance & Inventory — Exact-SHA Evidence Register

| Field | Value |
|---|---|
| Evidence ID | EVD-FINANCE-INVENTORY-BUNDLE-004 |
| Version | 0.1 |
| Evidence snapshot | 2026-11-20 |
| Recorded at | 2026-11-20 13:45 UTC |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |
| Repository | Md-gsm/MD-Gsm1 |
| Evidence branch | `evidence/finance-inventory-bundle-004-20261120` |
| Gate result | **PARTIAL — checksum and structural checks PASS; full CI and independent review remain OPEN** |
| Scope | Package 4 evidence for Finance/Inventory design work and the exact PR heads #71–#73 |

## 1. Purpose and evidential boundary

This register binds the observed checksum, structural-validation, build, security and installer outcomes to the precise source PR head SHAs listed below. A green document-structure check is evidence of structural consistency only. It is not evidence of PostgreSQL Inventory runtime behavior, production accounting persistence, Posting correctness, or release approval.

This register is a dated evidence snapshot. Workflow states marked PENDING/IN PROGRESS must not be read as successful or complete.

## 2. Exact source revisions

| PR | Branch | Exact head SHA | GitHub PR merge-ref SHA observed | State at snapshot |
|---|---|---|---|---|
| #71 | `docs/accounting-physical-schema-design-20261118` | `b165e957ff7bf886616de21933018395d76bbff9` | `48f78fcaecda8dc7d605edff94b09ed90f5e0fca` | OPEN; non-draft metadata; title retains [Draft] |
| #72 | `docs/architecture-master-v3.0-20261119` | `3d8262bea8a140056cbdc1c6efcae02e558a6201` | `2aa0064fa3fd27387733c3bc7e45304375c68ee6` | OPEN; non-draft; mergeable |
| #73 | `docs/inventory-pd-v0.2-blocker-closures` | `107ba63ebcdaf10dcce604f197e4c8e73f778c99` | `f23b91943e01cc273ac20a49d32006ceb6960b1a` | OPEN; non-draft; mergeable after branch realignment |

The #73 head was realigned on top of the #72 head. Git compare confirms #73 is 12 commits ahead and 0 behind `3d8262bea8a140056cbdc1c6efcae02e558a6201`; that SHA is its merge base. The integration commit retains the previous #73 history as a second parent. No PR was merged by this operation.

## 3. SHA-256 manifest repair

### PR #72 head `3d8262bea8a140056cbdc1c6efcae02e558a6201`

The previous manifest omission and three content mismatches were corrected. SHA values below are SHA-256 of UTF-8 file contents, not Git blob identifiers.

| File | SHA-256 recorded |
|---|---|
| `docs/architecture/MDG-ARCH-MASTER-001_v3.0.3.txt` | `2eac5d75a7834d0ee796ad72a643eb7e7843697b8367fa4281f534c4a63bbd52` |
| `docs/modules/inventory/MDG-INVENTORY-STAGE-05-EQUATIONS-CUSTODY-STOCKTAKE-001_v0.1.md` | `5730d920ed6ad4a845ef165eb6386a7b8469e2e7098848b8af38aa81431f51e1` |
| `docs/modules/inventory/MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001_v0.1.md` | `0673e7718565b7853217327122432f83f7e2137d270e0066b65d05f5d9d40afc` |
| `docs/finance/workpapers/p4h/MDG-WP06-DOCUMENT-REGISTER-001_v0.8.md` | `7fd9454810068ce44aa91eac695a9e81bb2b60664f05b90e160730e691b77b15` |
| `docs/governance/MDG-OPEN-ISSUE-REGISTER-001_v1.0.md` | `26efd4e0e52c498ba1ad43d1b29491d813b2fa6ec35f2901af324d3a7bd9e105` |

### PR #73 head `107ba63ebcdaf10dcce604f197e4c8e73f778c99`

After branch realignment, the manifest was rebuilt from the #72 baseline and reconciled for the five PR #73 document files:

| File | SHA-256 recorded |
|---|---|
| `docs/finance/workpapers/p4h/MDG-WP06-DOCUMENT-REGISTER-001_v0.7.md` | `0945470ec5d26f4553ae2fb3480a995c520c575fa7ec1a31d2755e4f863bc5e7` |
| `docs/modules/inventory/MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001_v0.2.md` | `835ee39d6ca03a3a81a4d813b8fffb2eec2bc0af84e1309f82fbbb0a2ad16c97` |
| `docs/modules/inventory/MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-001_v0.3.md` | `c14fc7ec96f1d5f5f692688aacf8a2fa7fe9d046a50d93e1f56e0995f29ceb4b` |
| `docs/modules/inventory/MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-REAUDIT-002_v0.1.md` | `2e0d8c38f1ad363f77614ade7ee55b3e2b3a6a6a094f5a4c62456ebc17e27bd6` |
| `docs/modules/inventory/MDG-INVENTORY-POSTGRESQL-PHYSICAL-DESIGN-REAUDIT-003_v0.1.md` | `0bd6f2032a218d0a852fbc7983141268b45d22a51364167aa03eba305a37b025` |

## 4. Exact-head CI evidence

### PR #71 — `b165e957ff7bf886616de21933018395d76bbff9`

| Workflow | Run ID | Conclusion |
|---|---:|---|
| Persons Module Gates | 44357487900 | SUCCESS |
| Windows Preview Installer | 44357487917 | SUCCESS |
| CodeQL | 44357487885 | SUCCESS |
| Project Charter Owner Gate | 44361437434 | SUCCESS |

### PR #72 — `3d8262bea8a140056cbdc1c6efcae02e558a6201`

| Workflow/job | Run/job ID | Snapshot result |
|---|---:|---|
| Persons checksum verification (`persons-spec-lint`) | job 136321627085, run 44746080861 | **PASS — 362 tracked files match SHA256SUMS.txt** |
| Finance Workpaper Structural Validation | 44746080871 | SUCCESS |
| Project Charter Owner Gate | 44746080812 | SUCCESS |
| Windows Preview Installer | 44746080878 | SUCCESS |
| CodeQL | 44746080814 | SUCCESS |
| Full Persons Module Gates workflow | 44746080861 | IN PROGRESS at snapshot; Windows PostgreSQL migration job had not completed |

### PR #73 — `107ba63ebcdaf10dcce604f197e4c8e73f778c99`

| Workflow/job | Run/job ID | Snapshot result |
|---|---:|---|
| Persons checksum verification (`persons-spec-lint`) | job 136322318339, run 44746278646 | **PASS — 366 tracked files match SHA256SUMS.txt** |
| Finance Workpaper Structural Validation | 44746277343 | SUCCESS |
| Project Charter Owner Gate | 44746277501 | SUCCESS |
| Windows Preview Installer | 44746277369 | SUCCESS |
| CodeQL | 44746277365 | SUCCESS |
| Full Persons Module Gates workflow | 44746278646 | IN PROGRESS at snapshot; Windows PostgreSQL migration job had not completed |

The two checksum job logs explicitly report successful two-way validation of tracked files against the manifest. They supersede the earlier checksum failure reports for these exact heads only. They do not certify other or later revisions.

## 5. Independent review status

- PR #71 has an `APPROVED` review by `mrdeljoe4` attached to exact head `b165e957ff7bf886616de21933018395d76bbff9`. The submitted review body is empty. It is recorded as an exact-SHA approval, but the empty body does not provide a detailed audit rationale.
- PR #72 has reviewer `mrdeljoe4` requested. A comment requests detailed review pinned to `3d8262bea8a140056cbdc1c6efcae02e558a6201`. Independent review is **PENDING**.
- PR #73 has reviewer `mrdeljoe4` requested after branch realignment. A comment requests detailed review pinned to `107ba63ebcdaf10dcce604f197e4c8e73f778c99`. Independent review is **PENDING**.
- Owner acceptance of architecture v3.0.3 is not a substitute for an independent review of each exact PR head.

## 6. Gate register

| Gate / criterion | Result at snapshot | Evidence / limitation |
|---|---|---|
| #72 checksum integrity | PASS | persons-spec-lint exact-head log; 362 tracked files |
| #73 checksum integrity | PASS | persons-spec-lint exact-head log; 366 tracked files |
| Finance 7B/7C/7D structural validation | PASS | runs 44746080871 and 44746277343; structure only |
| Installer / CodeQL / Charter for #72 and #73 | PASS | run IDs in section 4 |
| Full Persons CI on #72 | OPEN / IN PROGRESS | Windows PostgreSQL migration job pending at snapshot |
| Full Persons CI on #73 | OPEN / IN PROGRESS | Windows PostgreSQL migration job pending at snapshot |
| Detailed independent review #72 | PENDING | requested on exact head |
| Detailed independent review #73 | PENDING | requested on exact head |
| Accounting/Inventory runtime validation | NOT RUN / NOT VERIFIED | design documents and structural checks do not prove runtime |
| Final package acceptance | OPEN | cannot close before full exact-head CI and independent reviews |

## 7. Design and release boundary retained

The following limitations remain binding and are not closed by this evidence register:

1. **7B:** 39 event dispositions are recorded; accepted positive Posting Rules remain **zero**. No Posting Release.
2. **7C:** 78 identifiers are in scope; canonical account attributes and source-backed eligibility gaps remain OPEN / NOT VERIFIED.
3. **7D:** 67 Test IDs are defined; the finance runtime/end-to-end test set remains NOT RUN / NOT VERIFIED.
4. **Inventory physical design:** PD-T-001..039 are definitions only; they are NOT RUN. O-35 remains OPEN pending exact-SHA PostgreSQL watermark, concurrency/race, replay and tie-out evidence.
5. No DDL, SQL, EF mapping, database grants, migration, COA seed or Posting writer is authorized by this register. Migration 0009 remains RESERVED.
6. No production migration, Posting Release, Gate C/Gate D final acceptance or Go-Live is granted.

## 8. Decision

**EVD-FINANCE-INVENTORY-BUNDLE-004 v0.1 is recorded as a final evidence snapshot for the observed exact revisions and CI outcomes above. Package 4 is not accepted as fully closed.**

Checksum repair and finance structural validation are PASS for PR #72 and #73 heads listed in this document. Full Persons CI and independent reviews remain OPEN. Any subsequent change to a source branch requires fresh exact-head evidence and a new EVD revision or amendment.

**END — EVD-FINANCE-INVENTORY-BUNDLE-004 v0.1**
