# MDG Security Stage 08 — Controlled Register Addendum

| Field | Value |
|---|---|
| Register ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 4.5 / 2026-11-22 |
| Supersedes | v4.4 |
| Supersedes | v4.3 |
| Supersedes | v4.2 |
| Supersedes | v4.1 |
| Supersedes | v4.0 |
| Supersedes | v3.9 |
| Supersedes | v3.8 |
| Supersedes | v3.7 |
| Supersedes | v3.6 |
| Supersedes | v3.5 |
| Supersedes | v3.4 |
| Package | O20-E/O20-F closure work; PR #91 revert remains Draft/unmerged; this branch is not merged |
| Branch | `security/o20-o21-final-closure` |
| Documentation baseline before this successor | `2e3ae7f9fb5ff2acefc7eb49139d7812420099ff` |
| O20-G evidence head (bounded CI proof) | `5f541e342e8b2cb02c6f88d816a137b5ab409224` |
| O20-D evidence head (bounded CI proof) | `07ec5b2246ffb3c59299aa66f37942eb0fc823b3` |
| Overall O-20 | OPEN |

## 1. Purpose and status discipline

This successor consolidates O20-E progress and its G1 readiness disposition. It supersedes only the status narrative in v2.6; it does not erase earlier evidence or turn a design artifact, owner policy approval, or source inventory into implementation acceptance.

Use the following status meanings:
- **PASS (bounded disposable CI scope):** the cited test/evidence was successful only in the explicitly described disposable PostgreSQL environment and at its exact SHA.
- **OWNER-ACCEPTED:** an explicit product policy decision was recorded; this does not authorize implementation or G1.
- **DOCUMENTED / PROPOSED:** a design artifact exists but its implementation contract has not been accepted.
- **OPEN / NOT GRANTED / NOT RUN:** the relevant acceptance, authorization or execution has not occurred.

No status in this register authorizes a production database change, runtime activation, G1 entry, operational release or merge.

## 2. O20-D — logical backup and isolated restore

**Disposition: PASS for the bounded disposable PostgreSQL CI proof only.**

The recorded exact-head verification on `07ec5b2246ffb3c59299aa66f37942eb0fc823b3` reported:
- MD-Gsm Persons Module Gates run `44939095722`: SUCCESS (13/13 jobs);
- Windows Preview Installer run `44939095741`: SUCCESS;
- CodeQL run `44939095719`: SUCCESS;
- Project Charter Owner Gate run `44939095756`: SUCCESS;
- PostgreSQL integration: 38/38 passed;
- backup_operator executed logical `pg_dump` with read-only source privileges; a separate DBA restored to a marked isolated database; selected migration/IdentityAccess invariants matched; the backup identity was denied write, database-create and restore-target access;
- the explicit four-support-login least-privilege test passed.

This proof is not production backup acceptance. Production scheduling, off-host retention, encryption/key custody, WAL/PITR, restore drills, RTO/RPO and live production identity binding remain outside this bounded evidence.

## 3. O20-G — PostgreSQL catalog and effective-privilege inventory

**Disposition: PASS for the bounded disposable PostgreSQL 18.6 CI proof only.**

The recorded O20-G evidence completed on exact source head `5f541e342e8b2cb02c6f88d816a137b5ab409224`:
- Workflow `44943071363`, job `136964343495`, SUCCESS;
- PostgreSQL server version `180006` (18.6);
- inventory: 20 roles, 4 membership edges, 5 schemas, 522 relations, 35 explicit column ACL entries, 3,414 routines, 665 types, 0 default ACL entries, 1 extension, 4 languages, 0 FDWs, 0 foreign servers, 0 large objects, 2 tablespaces, 24 ownership dependencies and 227 mapped effective-privilege records;
- artifact `o20-g-catalog-inventory`, ID `14722596755`;
- JSON SHA-256 `278a2fb1e7e5fbd55cd594f1afdd97c37a2f3e00fc71a788e8bc73c28b17b50f`;
- ZIP SHA-256 `e0e24e7b69cf8dfd774843cef76fc214473b325ee2d2807c1d568b4339600237`.

This does not constitute a production-cluster scan or prove deployed production credentials/LOGIN lifecycle. O20-G's bounded disposable scope is accepted; the overall O-20 package is not closed.

## 4. O20-E — owner decisions and design records

### 4.1 Owner-accepted policy

The base owner-accepted decisions are carried forward and expanded by `MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.3`:
1. Organization owns Account-to-Branch and Account-to-Warehouse assignment facts.
2. Employment by itself does not grant access; an explicit active assignment is required.
3. Multiple active Branch and Warehouse assignments may coexist; each is individually auditable.
4. Branch assignment does not imply Warehouse access; access to a Warehouse requires an explicit assignment to that exact Warehouse.

The governing Master Architecture v3.0.3 and `OWN-INV-018 v1.1` place Branch/Warehouse/location master data and the Warehouse → Section → Shelf → Bin/StorageLocation hierarchy under Organization; Inventory owns stock facts and movement. IdentityAccess remains owner of Account, Credential, Session, Role and Permission. These are ownership/policy conclusions, not G1 approval.

### 4.1A Additional owner decisions recorded on 2026-11-22

Owner policy v0.3 records the following accepted outcomes:
- Exactly one current Branch per Warehouse; controlled, audited reassignment with retained relationship history. The effect on existing Warehouse assignments after reassignment remains open.
- BranchCode unique across Organization; WarehouseCode unique within current Branch; both required, normalized and not silently reusable after archive. Exact normalization/editability remains open.
- Branch/Warehouse lifecycle vocabulary ACTIVE / INACTIVE / ARCHIVED; no physical deletion. Transition prerequisites remain open.
- Assignment activation is immediate after approved server-side commit; no future-dated activation or automatic expiry; revocation is terminal; a later grant receives a new AssignmentId.
- Self-assignment is prohibited for all actors, including the Product Owner. Non-owner assignment create/change/revoke commands require a second distinct authenticated approver before effect. The designated Product Owner may manage Branch/Warehouse assignments for any other account without second-person approval.
- The Product Owner exception removes only the second-approver requirement. It does not permit impersonation, credential/session access or IdentityAccess account/permission mutation. Product Owner actions remain authenticated, reasoned and audited. Trusted server-side owner-principal binding is still a technical G1 prerequisite and must not be inferred from `super_admin`, a client claim, username or GitHub identity.
- Exact active Warehouse assignment + active Warehouse/current Branch facts suffice for Warehouse scope; a separate parent Branch assignment is not additionally required. IdentityAccess permission remains mandatory.

No item above authorizes runtime, DDL, migration, permissions, grants, production changes, G1 or merge.

### 4.2 Controlled logical and physical design

- Logical design: `MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.2`.
- Physical design: `MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v0.2`, still a proposal and not implementation authorization.
- The physical proposal describes candidate Organization persistence concepts, but does not authorize source module, schema, tables, DDL, migrations, seed, resolver, endpoint, database role/grant, reporting view or runtime.
- `src/Modules/README.md` still requires G1 authorization before a module is introduced. The Organization directory does not exist in the inspected source tree.

### 4.3 Source/query-path inventory v0.2

`MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 v0.2` revalidated the source tree and API composition root at exact inspected commit `9838250e97be5f362f98e1e82a5e6508cf34cac5`.

The current executable module directories are Accounting, Finance, IdentityAccess, Persons and RepairOperations. No executable Organization, Inventory or Reporting module is present. The API composition root registers health/readiness, system information, IdentityAccess login/password/heartbeat/logout, and `GET /api/v2/persons/{partyId}`. No report/dashboard aggregate/print/PDF/CSV/XLSX/export/Inventory query route was found. The Party detail route has no BranchId/WarehouseId source context and is not a current O20-E RowScope consumer.

The inventory also records an unrelated documentation/code discrepancy: API README files describe `/api/v1/persons/{partyId}`, whereas the current Program.cs registration is `/api/v2/persons/{partyId}`. This discrepancy was not modified in this O20-E package.

**Disposition:** current-snapshot discovery is verified for the stated SHA only. There is no current reportable Branch/Warehouse query path to test, so positive/negative RowScope PostgreSQL tests remain NOT RUN / NOT APPLICABLE to the absent runtime consumer. Future consumer/source inventory remains OPEN and must be repeated as query/export paths are introduced.

## 5. O20-E — preliminary G1 readiness review

`MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 v0.2` is an updated internal preliminary cross-contract review, not an independent reviewer attestation and not an owner authorization.

**Disposition: NOT READY FOR G1.** The review found no basis to introduce Organization implementation until the following are resolved:

| Design item | Current status | Closure required |
|---|---|---|
| PD-ORG-01 Warehouse-to-Branch relation | OWNER-ACCEPTED with bounded detail OPEN | Exactly one current Branch; controlled reassignment; retain history; decide existing assignment effect after reassignment |
| PD-ORG-02 Branch/Warehouse codes | OWNER-ACCEPTED with bounded detail OPEN | BranchCode globally unique; WarehouseCode unique in current Branch; required, normalized and not reused after archive; exact algorithm/editability OPEN |
| PD-ORG-03 master lifecycle | OWNER-ACCEPTED with bounded detail OPEN | ACTIVE/INACTIVE/ARCHIVED; no physical delete; transition prerequisites OPEN |
| PD-ORG-04 assignment lifecycle | OWNER-ACCEPTED | Immediate activation; no future dating/automatic expiry; revocation terminal; regrant new AssignmentId |
| PD-ORG-05 actor/reason/separation of duties | OWNER-ACCEPTED with technical binding OPEN | No self-assignment; non-owner requires distinct second approver; Product Owner may manage any other account without second approval; trusted owner binding OPEN |
| PD-ORG-06 permission keys and role grants | OPEN — IdentityAccess/security + owner | Reconcile approved permission matrix with PermissionCatalog and publish versioned public principal/permission contracts |
| PD-ORG-07 IdentityAccess scope composition | OWNER POLICY ACCEPTED; CONTRACT OPEN | Permission AND Organization assignment; IdentityAccess scope narrows only; publish compatible versioned contract |
| PD-ORG-08 AuditSecurity outbox/event contract | PARTIAL / OPEN | Publish/accept canonical event envelope, durable ACK, retry/quarantine, worker authority and retention |
| PD-ORG-09 identifier generation | OPEN — Platform | Set Organization ID representation/generation policy; PartyId UUIDv7 alone is not a global rule |
| PD-ORG-10 Unit of Work and cross-module lock order | OPEN — Platform/Application | Publish global Lock-Order Registry and prove revocation/write concurrency on PostgreSQL |
| PD-ORG-11 assignment-history retention/legal hold | OPEN — owner/legal | Set retention, legal hold, archive and disposal policy |
| PD-ORG-12 source RowScope inventory | CURRENT SNAPSHOT VERIFIED; FUTURE COVERAGE OPEN | Re-inventory and test every new source/query/report/export surface |
| PD-ORG-13 Branch prerequisite for Warehouse scope | OWNER-ACCEPTED | No parent Branch assignment required when exact Warehouse assignment and active Warehouse/current Branch facts are valid |

The updated readiness review records the owner decisions above and retains implementation blockers in the existing IdentityAccess integration: current `PermissionCatalog.cs` contains no `organization.*` keys and lacks some report permissions in the already approved policy matrix; `src/Modules/IdentityAccess/Contracts/README.md` says no runtime public contract is published. It also records that Master requires an explicit Lock-Order Registry and PostgreSQL concurrency evidence, and that AuditSecurity remains sole owner of canonical shared audit events.

The review distinguishes accepted owner policy from open technical design. Remaining recommendations are options only and do not become implementation contracts without the accountable owner/platform/security acceptance.

## 6. O20-E gate state

| Gate | Current disposition |
|---|---|
| Organization Branch/Warehouse ownership | ESTABLISHED |
| AccountBranchAssignment / AccountWarehouseAssignment ownership | OWNER-ACCEPTED |
| Base and additional assignment policy decisions in O20E v0.3 | OWNER-ACCEPTED, including Product Owner second-approver exception |
| Logical design | UPDATED v0.2 |
| Physical design | PROPOSED v0.2; technical details/independent review open; not implementation-authorized |
| Current repository source inventory | REVALIDATED snapshot at stated SHA |
| Preliminary G1 readiness | NOT READY; owner decisions updated; cross-contract/technical blockers remain |
| Organization G1 authorization | NOT GRANTED |
| Organization runtime/module/resolver | NOT IMPLEMENTED |
| Schema/DDL/migration/seed | NOT CREATED / NOT AUTHORIZED |
| Positive/negative PostgreSQL RowScope tests | NOT RUN |
| ReportingReadOnly RowScope permission/grant | NOT GRANTED |
| O20-E | OPEN |

## 7. O20-F — emergency allow-list lifecycle

**Disposition: OPEN / NOT ACCEPTED.**

The emergency identity's allow-list, independently authorized activation, bounded expiry, revocation, immutable audit evidence and fail-closed execution have not been fully specified and proved in this successor. No emergency LOGIN, credential, membership, grant, allow-list record or production emergency access was provisioned. O20-F remains a required part of overall O-20 closure.

## 8. Exact-source and checksum control

This register is a controlled documentation successor within the existing PR #86 branch. The two newly recorded O20-E artifacts are:
- Source inventory v0.2 SHA-256: `51611ab147dbe05c6f56380fea409b8ea6cd6b30717f64781189908a6f679d24`.
- Owner policy v0.3 SHA-256: `e3c16c0794a776a656ec79282465e565825de3097396b1bb6288c8ceb36421b9`.
- Logical design v0.2 SHA-256: `da95680e624ef01cc6069080204bd015dbcf06f4dd36c510ef590266827e1991`.
- Physical design proposal v0.2 SHA-256: `aca2433e5e03cb667080fd322786464e8e05249ccf6f49c67fdde25d9ae21725`.
- G1 readiness review v0.2 SHA-256: `50497d80f6bcae4a235f91fbe4f37ecae436a2c519b61491a6f6c3d1f1a54081`.

The corresponding entries and this register's own digest must be recorded in `SHA256SUMS.txt`. The checksum file is not self-hashed. CI on the eventual final documentation/checksum head must be retrieved and inspected before any exact-head PASS is claimed; no current-head all-workflow PASS is asserted by this register.

## 9. Consolidated Stage 08 disposition

| Item | Disposition |
|---|---|
| O20-D | PASS — bounded disposable logical backup/isolated restore only |
| O20-G | PASS — bounded disposable PostgreSQL 18.6 catalog/ACL inventory only |
| O20-E | OPEN — owner policy v0.3 accepted; logical v0.2 / physical proposal v0.2 documented; G1 NOT READY; runtime and RowScope tests NOT RUN |
| O20-F | OPEN — emergency allow-list lifecycle and evidence incomplete |
| Overall O-20 | **OPEN** |

No new Organization module, schema, table, DDL, migration, seed, permission key, role/grant, ReportingReadOnly privilege, endpoint, report view, outbox contract, production credential, or production access was created or authorized. Reporting remains fail-closed.

No new independent reviewer request is made. Per the existing sequencing direction, consolidated reviewer re-request is deferred until both O20-E and O20-F are complete. PR #86 remains open and unmerged.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v2.8**


## 10. Controlled O20-E/O20-F successor — 2026-11-22

This v2.9 successor records the ordered eight-step continuation on the existing PR #86 branch. The source snapshot reviewed for RowScope is PR head fe330b35b85486f0b142f03ab0f0f8b0e71ee616; subsequent changes in this successor are documentation-only.

1. **Owner policy correction — recorded:** O20-E Data Ownership v0.4 supersedes v0.3 on self-assignment. Non-owners cannot assign to themselves; the designated Product Owner may assign to the Product Owner's own account and any other account without a second approver. All actions remain authenticated, reasoned, versioned and audited. Trusted owner binding remains a technical prerequisite.
2. **IdentityAccess ↔ Organization contract — proposed:** contract v0.1 defines server-derived principal, live permission decision and separate trusted Product Owner binding. It is not yet published/accepted; no permission keys or grants were added.
3. **Branch/Warehouse codes — designed, owner acceptance open:** logical/physical v0.3 specify NFC + outer trim, reject empty/control/format/remaining whitespace, case-sensitive bytewise C-collation comparison, and retained historical code reservations. No DDL or code migration was run.
4. **Warehouse reassignment — designed, owner acceptance/PoC open:** assignment remains bound to exact WarehouseId; reassignment only while Warehouse INACTIVE; no automatic grant/revoke; inactive denies; explicit reactivation restores only still-active exact-Warehouse assignments.
5. **Lifecycle/archive — designed, owner acceptance/PoC open:** ACTIVE/INACTIVE/ARCHIVED; inactive denies; archive terminal; required child/assignment prerequisites; no physical deletion; history retained.
6. **AuditSecurity + RowScope — design/source inventory recorded:** local transactional audit-intent outbox is distinct from canonical AuditEvent; stable EventId and durable ACK required. Source inventory v0.3 finds no current Organization/Inventory/Reporting route or RowScope consumer; no reporting_ro role, grant or view is authorized. No RowScope runtime test is applicable yet.
7. **G1 readiness — NOT READY:** v0.3 identifies remaining IdentityAccess contract/permission acceptance, owner-principal enrollment/recovery, code/lifecycle owner acceptance, Platform ID/lock-order, records retention, AuditSecurity ACK, PostgreSQL concurrency/outbox tests, threat model and independent exact-SHA review.
8. **O20-F — design specified, proof OPEN:** O20-F v0.1 defines allow-list, two-person activation, proposed 30-minute maximum one-use window, synchronous expiry/revocation, audit intent and disposable PostgreSQL proof matrix. No emergency LOGIN, credential, grant, membership or production access was created. The 30-minute proposal and approver/identity controls still require acceptance and proof.

### 10.1 Current gate state

| Gate | State |
|---|---|
| O20-D | PASS — bounded disposable logical backup/isolated restore only |
| O20-G | PASS — bounded disposable PostgreSQL catalog/ACL evidence only |
| O20-E owner policy | OWNER-ACCEPTED v0.4, including Product Owner self-assignment exception |
| O20-E detailed design | SUCCESSOR DOCUMENTED; technical/owner acceptance and proofs OPEN |
| O20-E RowScope | No current consumer; no positive/negative runtime proof; no reporting grants |
| O20-E G1 | NOT READY / NOT GRANTED |
| O20-F | DESIGN PROPOSED; owner/security acceptance and disposable proof NOT RUN |
| Overall O-20 | **OPEN** |

### 10.2 Integrity control

The SHA256SUMS entries for the five O20-E v0.4/v0.3 successors, IdentityAccess contract proposal, O20-F proposal and this register successor are maintained in SHA256SUMS.txt. SHA256SUMS.txt is not self-hashed. Exact-head CI must run after the final checksum commit; no PASS is asserted until the workflow and logs are inspected.

PR #86 remains open, unmerged and not ready for final reviewer re-request until O20-F proof and all other open gates are satisfied.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v2.9**


## 11. Controlled owner-acceptance successor — 2026-11-22 (v3.0)

The Product Owner explicitly accepted the four bounded proposals. The controlled acceptance record is `docs/security/MDG-SECURITY-O20E-O20F-OWNER-DECISION-ACCEPTANCE-001_v0.1.md`.

| Decision | Owner disposition | Verification still required |
|---|---|---|
| OD-01 — NFC + outer trim, reject invalid whitespace/control/format, preserve punctuation/non-ASCII, case-sensitive UTF-8 bytewise C-collation comparison | OWNER-ACCEPTED | .NET/PostgreSQL equivalence, Unicode vectors and collision tests; no DDL yet |
| OD-02 — Warehouse assignment remains bound to exact WarehouseId across inactive-only Branch reassignment; no implicit grant/revoke/copy/broadening | OWNER-ACCEPTED | PostgreSQL concurrency, inactive denial and explicit-reactivation proof |
| OD-03 — ARCHIVED terminal; no reactivation or physical deletion | OWNER-ACCEPTED | Lifecycle and dependency-precondition proof; records retention remains open |
| OD-04 — emergency activation requires two distinct approvers; one-use, maximum 30 minutes; synchronous expiry | OWNER-ACCEPTED | Disposable PostgreSQL 18 proof and independent security review |

### 11.1 Updated artifacts

- Logical Organization assignment design v0.4 — accepted policy addendum; G1 remains NOT GRANTED.
- PostgreSQL physical design v0.4 — accepted policy addendum; technical details, lock order, retention and implementation remain open.
- O20-F emergency lifecycle v0.2 — two-approver / 30-minute one-use policy accepted; disposable proof remains NOT RUN.
- O20-E/O20-F owner decision acceptance record v0.1 — records the exact four accepted decisions and their limits.

### 11.2 Current gate state

| Gate | State |
|---|---|
| O20-E owner policy and OD-01..03 | OWNER-ACCEPTED |
| IdentityAccess ↔ Organization contract | PROPOSED; owner publication/permission keys OPEN |
| Trusted Product Owner binding/enrollment/recovery | OPEN |
| Platform ID and lock-order registry | OPEN |
| Records retention/legal hold | OPEN |
| AuditSecurity canonical event/durable ACK | OPEN |
| Organization G1 | NOT READY / NOT GRANTED |
| Organization runtime / DDL / migrations / grants | NOT AUTHORIZED / NOT CREATED |
| O20-E PostgreSQL concurrency and canonicalization proofs | NOT RUN |
| O20-F OD-04 policy | OWNER-ACCEPTED |
| O20-F disposable PostgreSQL 18 proof | NOT RUN |
| Overall O-20 | **OPEN** |

No production identity, credential, role, membership, grant, reporting access, emergency allow-list entry, schema, migration or runtime module was created. PR #86 remains open and unmerged. Exact-head CI must be rerun after this successor and SHA256SUMS update; no PASS is claimed from prior-head workflows.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v3.0**


## 12. Controlled successor — OD-05 records-retention acceptance (2026-11-22; v3.1)

This section is the controlling current status for the records-retention gate and supersedes earlier statements in historical sections 10–11 that the entire records-retention policy was awaiting owner approval.

### 12.1 Owner decision

The Product Owner explicitly approved OD-05. The controlling records are:
- `MDG-SECURITY-O20E-O20F-OWNER-DECISION-ACCEPTANCE-001 v0.2`;
- `MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.1`;
- `MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.5`.

Accepted baseline:
- Organization assignment and Branch/Warehouse relationship history is retained by default.
- Physical deletion and automatic expiry/purge are not allowed by default.
- Disposal is considered only after an approved applicable legal retention period has elapsed, no Legal Hold covers the record, and documented authorization from the accountable records/legal authority exists.
- A current or unresolved Legal Hold blocks disposal.
- The exact duration is intentionally unset; no statutory period or jurisdiction is guessed.

The decision does not transfer canonical AuditEvent/SecurityEvent ownership away from AuditSecurity and does not authorize a purge worker, deletion, DDL, migration, G1 or production disposal.

### 12.2 Revised O20-E gate state

| Gate | Current state |
|---|---|
| OD-01 code canonicalization | OWNER-ACCEPTED; cross-runtime equivalence/collision proof OPEN |
| OD-02 Warehouse reassignment | OWNER-ACCEPTED; PostgreSQL concurrency proof OPEN |
| OD-03 ARCHIVED terminal lifecycle | OWNER-ACCEPTED; dependency/transition proof OPEN |
| OD-05 retention baseline | OWNER-ACCEPTED; duration, Legal Hold and disposal implementation OPEN |
| IdentityAccess ↔ Organization contract | PROPOSED v0.1; IdentityAccess owner publication/acceptance OPEN |
| PermissionCatalog reconciliation | OPEN; no Organization keys/grants added |
| Trusted Product Owner binding/enrollment/recovery | OPEN |
| AuditSecurity canonical EventId/durable ACK/outbox | OPEN |
| Platform ID generation and global lock order | OPEN |
| Current RowScope inventory | Existing point-in-time inventory only; future consumers require new contract and tests; no reporting grants |
| PostgreSQL O20-E integration proofs | Incomplete; exact-head technical tests and evidence OPEN |
| Independent exact-SHA Architecture/Security review | NOT REQUESTED / NOT PASSED |
| Organization G1 | NOT READY / NOT GRANTED |
| O20-E | **OPEN** |
| Overall O-20 | **OPEN** |

### 12.3 Historical PR status correction

The earlier v2.8–v3.0 text describing PR #86 as open/unmerged is historical and is not the current repository state. PR #86 was merged prematurely. PR #91 is the Draft full-revert proposal and remains unmerged. The current O20-E/O20-F work is being prepared on `security/o20-o21-final-closure`; neither this register nor this branch changes `main` or merges PR #91.

### 12.4 Integrity and test status

The new successor documents are entered in `SHA256SUMS.txt`; the checksum file itself is not self-hashed. No CI PASS is claimed for this new documentation head until the exact resulting commit's workflows and logs are retrieved and inspected. This documentation update is not a substitute for the required runtime contracts, PostgreSQL proofs, threat model, legal schedule or independent review.

**Current disposition:** OD-05 policy baseline is accepted; O20-E technical closure is incomplete. Do not mark O20-E or O-20 closed, do not grant Organization G1, and do not create production roles, credentials, permissions, grants, schema or runtime.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v3.1**


## 13. Controlled successor — IdentityAccess approved report/dashboard permission alignment (2026-11-22; v3.2)

This section records a bounded source/test change after the OD-05 retention decision. It does not close IdentityAccess ↔ Organization integration.

### 13.1 Source change

`src/Modules/IdentityAccess/Domain/PermissionCatalog.cs` now includes the already owner-approved dashboard/report keys:
- `dashboard.read` — all ten initial roles;
- `dashboard.configure.self` — all ten initial roles;
- `dashboard.assign` — super_admin only;
- `reports.read` — all ten initial roles;
- `reports.print` — all ten initial roles;
- `reports.export` — super_admin, manager, warehouse, purchaser and accountant.

A focused golden test was added at `tests/MDGsm.Domain.Tests/IdentityAccess/ApprovedDashboardReportPermissionCatalogTests.cs`. It checks the six catalog keys and role decisions, and confirms `reporting.branch.read` remains unknown/denied.

### 13.2 Boundary

The change reuses only permission keys and role grants already present in the Product-Owner-approved matrix. It does not add or grant any `organization.*` key; it does not seed roles, alter database grants, create Organization runtime, or authorize reporting. Report permission is still conjunctive with the exact Organization assignment and source-side SQL RowScope. The earlier `reporting.branch.read`, `reporting.warehouse.read`, `reporting.export` proposals are withdrawn by IdentityAccess ↔ Organization contract proposal v0.2.

The IdentityAccess Contracts README still says no runtime public contract is published. `ModuleContractVersion.cs` remains only a version marker. Trusted Product Owner binding, IdentityAccess owner acceptance, Organization permission namespace, AuditSecurity contract, Platform ID/lock-order and PostgreSQL concurrency remain open.

### 13.3 Exact source/test integrity

The modified PermissionCatalog and the new focused test are registered in `SHA256SUMS.txt`. The new documentation successors (contract proposal v0.2 and O20-E readiness review v0.5) are also registered. No CI PASS is claimed until the exact resulting branch head is tested and its logs are inspected.

**Current disposition:** dashboard/report permission subset aligned to approved policy; PD-ORG-06 PARTIAL; PD-ORG-07 OPEN; O20-E OPEN; Organization G1 NOT READY / NOT GRANTED.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v3.2**


## 14. Controlled successor — complete approved IdentityAccess permission catalog alignment (2026-11-22; v3.3)

### 14.1 Source and test

The current branch's `src/Modules/IdentityAccess/Domain/PermissionCatalog.cs` has been replaced with the complete owner-approved initial permission-name set and role-grant matrix from `docs/security/identityaccess-permission-matrix-approval-fa.md`.

The complete golden test is `tests/MDGsm.Domain.Tests/IdentityAccess/ApprovedPermissionCatalogMatrixTests.cs`. It verifies:
- exact permission names in the catalog;
- exact grants for all ten initial roles;
- unknown-role fail-closed behavior;
- unapproved keys such as `persons.delete`, `finance.voucher.delete`, `reporting.branch.read`, `reporting.warehouse.read` and `organization.assignments.create` remain unknown and denied.

The prior dashboard/report-focused test remains as an additional focused test. The catalog now excludes permission names that were present in the former code but absent from the approved matrix, including `persons.delete` and `finance.voucher.delete`.

### 14.2 Acceptance boundary

This is a source-level representation of existing product-owner-approved policy, not a new grant or seed. It does not select row scopes: approved scopes remain separately enforced by authoritative source data and Organization assignment once a reviewed public contract/runtime exists. The proposed unapproved `reporting.*` namespace remains withdrawn. No `organization.*` permission key, role mapping, database seed/grant, production access, Organization runtime or migration was introduced.

IdentityAccess public contract publication and module-owner acceptance remain open. Trusted Product Owner binding/enrollment/recovery, AuditSecurity durable ACK/outbox, Platform ID/lock order, PostgreSQL concurrency/RowScope evidence and independent exact-SHA review remain open.

### 14.3 Test status

The full matrix golden test has been added and is awaiting the current exact-head PR CI result. The test is not marked PASS until the job completes and its logs are inspected. The exact updated source/test checksums must be present in `SHA256SUMS.txt`.

**Disposition:** initial PermissionCatalog source-aligned; exact-head CI pending; PD-ORG-06 catalog work SOURCE-ALIGNED, contract/Organization namespace OPEN; O20-E OPEN; G1 NOT GRANTED.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v3.3**


## 15. Controlled successor — preserve existing sensitive-person authorization (2026-11-22; v3.4)

The complete PermissionCatalog alignment has been corrected to preserve the existing documented `persons.sensitive.read` authorization boundary. That exact permission is required separately by the Persons sensitive-data authorizer and is granted only to `super_admin`. It is not implied by `persons.read`. The full golden matrix test now checks the supplemental sensitive permission explicitly.

The main owner-approved matrix role/permission grants remain exact. Obsolete, unapproved `persons.delete` and `finance.voucher.delete` entries remain absent. Unapproved `reporting.*` and `organization.*` keys remain absent and unknown/denied.

The exact-source snapshot used for this review is `db338fbfd94306de086c18be3eb07193acee3b3c`. The updated code/test and successor documents must all be present in `SHA256SUMS.txt`. The earlier Windows preview run at `f0b6153ea711041a06230d976f23dee56c2ff2dd` exposed a regression because this separate sensitive permission had been omitted; that run's test suite failed with 233 passed / 1 failed. The current source correction restores the permission and a follow-up CI run is required before any PASS claim.

**Disposition:** initial matrix and supplemental sensitive permission source-aligned; regression correction made; exact-head CI retest REQUIRED; PD-ORG-06 source reconciliation only; Organization public contract/G1/O20-E remain OPEN.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v3.4**


## 16. Controlled successor — immutable Warehouse Branch relationship history proof (2026-11-22; v3.5)

### 16.1 New executable PoC

Added `tests/MDGsm.Domain.Tests/Security/O20OrganizationRelationshipHistoryPostgresProofTests.cs` to the marked disposable PostgreSQL suite. The test requires reassignment only while Warehouse is INACTIVE and destination Branch is ACTIVE, preserves the current WarehouseId and appends a new versioned history event. It verifies rejected attempts leave current state/history unchanged and the history event ledger rejects UPDATE/DELETE.

The proof uses an isolated disposable schema. It does not create production tables, DDL/migrations, a runtime Organization module, actor binding, global lock-order implementation, concurrency/race proof or canonical AuditSecurity integration.

### 16.2 Prior exact-head CI evidence

The preceding exact-head run `45015818229` at SHA `83b4c5ac4c40c4a7eaee5a54e0a354e9923eb549` completed these relevant checks:
- Release build and all 235 .NET tests passed;
- Linux PostgreSQL end-to-end integration 41/41 passed;
- O20-E OD-01/02/03 policy PoC tests 2/2 passed;
- source checksum manifest/spec lint and required Persons CI policy validation succeeded.

That run predates the newly added history test and is not evidence for the latest branch head. Fresh full CI, Windows migration, CodeQL and Windows preview evidence remain required for the updated exact SHA.

### 16.3 Current gate disposition

- OD-01 canonicalization: disposable .NET/PostgreSQL equivalence and collision PoC PASS at the prior exact SHA.
- OD-02/03 warehouse identity, inactive-only reassignment, active scope, terminal archive/no physical delete: bounded disposable PostgreSQL PoC PASS at the prior exact SHA.
- Warehouse-to-Branch append-only history: new dedicated PoC added; exact-head CI pending.
- Production history schema/retention: NOT IMPLEMENTED; legal schedule/Legal Hold and disposal controls open.
- Global lock-order/concurrency, trusted owner binding, IdentityAccess public contract and AuditSecurity ACK/outbox remain OPEN.
- O20-E remains OPEN; G1 NOT GRANTED; overall O-20 remains OPEN.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v3.5**


## 17. Controlled successor — O20-E cross-owner proposals and threat inventory (2026-11-22; v3.6)

The following review artifacts now exist on `security/o20-o21-final-closure`:

| Artifact | Purpose | Status |
|---|---|---|
| IdentityAccess ↔ Organization contract proposal v0.5 | Reconciles actual IdentityAccess HTTP runtime/DTOs with missing public module principal/permission/owner contract | PROPOSED; owner acceptance/publication OPEN |
| Platform Lock-Order Registry v0.1 | Lists Organization resources and 12 concurrent/failure experiments | PROPOSED; no global order accepted |
| Organization Audit Event/Outbox contract v0.1 | Candidate canonical envelope, hash/ACK/delivery/quarantine semantics | PROPOSED; AuditSecurity/Platform acceptance OPEN |
| Platform Identifier Policy Request v0.1 | UUID version, generator, clock and multi-instance decisions | OPEN — Platform decision required |
| O20-E Threat Model v0.1 | Fifteen threat scenarios and evidence requirements | INTERNAL DRAFT; independent Security review NOT PERFORMED |
| Physical design proposal v0.7 | Links the cross-owner technical dependencies | PROPOSED; no implementation authorization |

The IdentityAccess crosswalk confirms a live HTTP session validation path and `IAuthenticatedActorContext` exist. It also confirms the Contracts project currently has login/password/session DTOs and a module version marker, while `IRolePermissionStore` remains an internal Application port and no public Organization-facing principal/permission/owner-authority interface is published. This is a narrower and more accurate gap statement than saying IdentityAccess has no runtime authentication.

None of these proposals creates an accepted lock order, AuditSecurity interface, UUID global standard, owner enrollment path or production module. O20-E remains OPEN; Organization G1 NOT READY / NOT GRANTED; no production DDL, role, grant, migration or runtime has been authorized.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v3.6**


## 18. Controlled successor — exact source/RowScope revalidation and trusted owner binding proposal (2026-11-22; v3.7)

### 18.1 Exact source inventory

`MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 v0.4` inspected source head `3322d7738a576fc17f4b5168713dc4a68410ae87` and Program.cs blob `1297659e8ba4eca8459792674192896557affd31`. It confirms no executable Organization, Inventory or Reporting module and no current Branch/Warehouse report/query/export route. The report permission subset is source-aligned but does not implement RowScope. Current positive/negative RowScope proof is NOT APPLICABLE / NOT RUN; no reporting role/grant/view is provisioned.

### 18.2 Product Owner binding

Added `MDG-IDENTITYACCESS-PRODUCT-OWNER-PRINCIPAL-BINDING-001 v0.1` and linked it from contract proposal v0.6. It defines a proposed typed, versioned server-side owner-authority decision limited to Organization assignment administration and lists bootstrap/change/recovery/security tests. It deliberately leaves binding cardinality, aggregate ownership, enrollment ceremony, recovery authority and independent approval open. No owner flag or role-based shortcut was implemented.

### 18.3 Current blockers

- IdentityAccess module-facing principal/permission/owner contract: NOT PUBLISHED / NOT OWNER-ACCEPTED.
- Product Owner binding/enrollment/change/recovery: PROPOSED; NOT IMPLEMENTED / NOT PROVED.
- Organization management permission namespace/role mapping: OPEN; no keys or grants added.
- Platform lock order and identifier generation: decision requests only; OPEN.
- AuditSecurity canonical envelope/durable ACK/worker/outbox: proposal only; OPEN.
- Organization RowScope consumers: absent; future consumer source/query path proof OPEN.
- Legal retention schedule and Legal Hold implementation: OPEN; OD-05 retain/no-auto-delete baseline OWNER-ACCEPTED.
- PostgreSQL concurrent revocation/reassignment/approval/failure evidence: OPEN.
- Independent exact-SHA Security/Architecture review: NOT PERFORMED.

**Disposition:** O20-E OPEN; Organization G1 NOT READY / NOT GRANTED; overall O-20 OPEN.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v3.7**


## 19. Controlled successor — assignment maker-checker and Product Owner exception PostgreSQL proof (2026-11-22; v3.8)

Added `tests/MDGsm.Domain.Tests/Security/O20OrganizationAssignmentApprovalPostgresProofTests.cs` and a dedicated explicit CI workflow step. It executes only against the marked disposable IdentityAccess PostgreSQL test database and verifies:
- non-owner self-assignment blocked even with forged caller owner hint;
- non-owner request remains pending until independent approval;
- actor and subject are rejected as approvers;
- a distinct approver can authorize the exact pending request;
- super_admin without protected binding is not treated as Product Owner;
- a principal in the synthetic trusted binding can perform direct self/other assignment without second approver;
- Product Owner direct mode, binding version and audit intent are recorded.

This does not implement or prove production owner binding enrollment/rotation/recovery; it does not test live IdentityAccess permission contract, idempotency/version concurrency, canonical AuditSecurity durable ACK or production Organization runtime. It is a policy PoC, not production readiness.

A dedicated workflow step now runs the new test by its fully-qualified class name. It must pass on the exact latest SHA; no PASS is claimed until that log is retrieved.

**Current disposition:** maker-checker/owner exception policy proof added; exact CI pending. O20-E OPEN; G1 NOT READY / NOT GRANTED; overall O-20 OPEN.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v3.8**


## 20. Controlled successor — IdentityAccess public principal/permission contract source publication (2026-11-22; v3.9)

The missing module-facing authorization interface has been added in source at contract version 1.1:

- `src/Modules/IdentityAccess/Contracts/IdentityAccessAuthorizationContract.cs` publishes typed current-principal, permission and Product Owner decisions.
- `src/Host/Api/Security/IdentityAccessAuthorizationContractAdapter.cs` implements the interface using the middleware-validated `IAuthenticatedActorContext` and live `IRolePermissionStore`.
- `src/Host/Api/Program.cs` registers it scoped.
- `src/Modules/IdentityAccess/Contracts/README.md` now documents the public boundary.
- `ModuleContractVersion.Current` is incremented to 1.1.
- `tests/MDGsm.Domain.Tests/IdentityAccess/IdentityAccessAuthorizationContractAdapterTests.cs` covers principal resolution, unknown keys, allow/deny, permission-store outage and owner fail-closed behavior.

Unknown keys deny without querying the store; dependency failure returns Unavailable, never Allow. No method accepts arbitrary AccountId or client-supplied owner flag. The Product Owner resolver deliberately returns Unavailable until the protected binding store, enrollment and recovery lifecycle have been implemented and accepted.

This source publication closes the "no interface type" gap only. IdentityAccess module-owner acceptance, organization consumer integration, live revocation linearization, owner binding, and latest exact-head CI remain OPEN. No Organization permission key, seed, grant, schema, runtime or G1 is authorized.

**Current disposition:** IdentityAccess public principal/permission interface SOURCE-PUBLISHED; adapter SOURCE-IMPLEMENTED; owner authority NOT IMPLEMENTED; module-owner acceptance and exact-head tests OPEN; O20-E OPEN; G1 NOT GRANTED.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v3.9**


## 21. Controlled successor — O20 outbox/idempotency proof and O21 explicit test gate (2026-11-22; v4.0)

Added and wired these disposable PostgreSQL proofs:
- `O20OrganizationAuditOutboxPostgresProofTests`: local transaction atomicity, remote outage/pending intent, durable ACK replay, changed-hash quarantine and concurrent worker claim exclusion;
- `O20OrganizationIdempotencyConcurrencyPostgresProofTests`: competing idempotency replay, different-fingerprint denial and unique assignment convergence;
- explicit O-21 `AuditSecurityHashChainPostgresProofTests` workflow invocation.

These proofs are test-only and must not be represented as production AuditSecurity, WORM, HSM/KMS or Organization outbox implementation.

### Current evidence status
- The prior CI at SHA `d5f098822f4750d4cce481f303b39126989aa60f` passed 43/43 Linux integration tests, OD-01/02/03 policy 2/2, immutable relationship history 1/1, assignment maker-checker 1/1, foundation suite 235/235, Windows migration/idempotence and CodeQL. It failed checksum validation because four late files were not listed and the workflow checksum was stale; the downstream evidence gate therefore did not pass.
- The checksum manifest has since been reconciled at a later branch SHA, and additional source/test changes were made after the prior run. These new exact-head checks have not yet been rerun.
- O-21 test source exists but remains a disposable PoC with ephemeral RSA keys and local temporary checkpoint files, not production key custody or WORM.

**Disposition:** O20 local test coverage expanded; latest exact-head CI pending. O20-E OPEN; O-20 OPEN; O-21 OPEN; G1 NOT GRANTED.

**END — Stage 08 register v4.0**


## 22. Controlled successor — explicit O-21 evidence and ordered O20/E/21 blockers (2026-11-22; v4.1)

The Hash Chain implementation-readiness successor v0.7 now distinguishes the existing one-scenario disposable PostgreSQL PoC from absent production AuditSecurity, durable event ACK, WORM/Object Lock, Platform key service, standalone verifier, recovery/restore continuity and operations.

The O20-E readiness review v1.4 maintains the ordered blocker ledger. Current source/testing progress does not authorize runtime or G1. Exact-head CI for the public IdentityAccess contract adapter, local Outbox and idempotency tests remains pending.

**O-20-E: OPEN. O-20: OPEN. O-21: OPEN. Organization G1: NOT READY / NOT GRANTED.** No production Organization module, DDL, grant, external provider or Go-Live is authorized.

**END — Stage 08 register v4.1**


## 23. Controlled successor — Organization-local lock-order proof (2026-11-22; v4.2)

Added `O20OrganizationDeterministicLockOrderPostgresProofTests` and a dedicated explicit workflow step. Two concurrent connections submit reversed lists of the same aggregate IDs; each applies the same canonical UUID text order before acquiring row locks. Both update the same rows and must complete within bounded lock/statement timeouts without a deadlock.

This is evidence for one proposed Organization-local resource class only. It does not close the platform global lock-order registry or cross-module race matrix. Platform acceptance, identifier policy and all untested IdentityAccess/Finance/Accounting/Inventory/AuditSecurity lock relationships remain open.

**Current state:** local ordering PoC added; exact-head CI pending; global Platform registry OPEN; O20-E OPEN; O20/O21 OPEN; G1 NOT GRANTED.

**END — Stage 08 register v4.2**


## 24. Controlled successor — session-bound IdentityAccess permission decision (2026-11-22; v4.3)

### Source changes
- IAuthenticatedActorContext now exposes the server-validated AccountId, SessionId, credential version and authentication timestamp.
- PostgresSessionIdentityResolver now returns SessionId with the validated session identity.
- SessionAuthenticationMiddleware validates the SessionId/version before lease renewal and creates the claims itself.
- IRequestAuthorizationStore and PostgresIdentityAccessStores.HasPermissionForLiveSessionAsync evaluate account/session/lease/revocation/credential-version/account-enabled/active-role/active-permission in one SQL statement.
- IdentityAccessAuthorizationContractAdapter uses this query for public permission decisions and returns Unavailable on persistence failure.
- Contract tests and a disposable PostgreSQL test for live/revoked/expired/version-mismatch/disabled/inactive states have been added.

This is a point-in-time request decision; no commit-time serialization against a future Organization mutation is implemented. No Organization consumer or route exists, and no Organization permission keys are added. The Product Owner resolver remains Unavailable.

**Disposition:** session-bound permission query SOURCE-IMPLEMENTED; exact-head CI PENDING; commit-time cross-module linearization OPEN; Product Owner binding OPEN; O20-E OPEN; O-20 OPEN; O-21 OPEN; G1 NOT GRANTED.

**END — Stage 08 register v4.3**


## 25. Controlled successor — IdentityAccess adapter moved behind module boundary (2026-11-22; v4.4)

The API Host no longer contains IdentityAccessAuthorizationContractAdapter. The public interface stays in IdentityAccess Contracts; the Host supplies the validated-principal accessor; IdentityAccess Infrastructure owns the adapter and the PostgreSQL live-session permission query. The persistence registration also wires an explicit unavailable store if no DB connection is configured, so service resolution remains fail-closed.

This corrects the source dependency direction. Organization consumers will use only IdentityAccess.Contracts and must not reach IdentityAccess private layers.

**Disposition:** source boundary correction committed; exact-head CI pending; identity module owner acceptance, Product Owner binding and commit-time authorization ordering remain open. O20-E OPEN; O-20 OPEN; O-21 OPEN; G1 NOT GRANTED.

**END — Stage 08 register v4.4**


## 26. Controlled successor — consolidated IdentityAccess v1.1 module-owned adapter (2026-11-22; v4.5)

The IdentityAccess v1.1 public authorization contract is now implemented behind the module boundary:
- public interface and typed decision records in IdentityAccess.Contracts;
- validated request principal and live-session permission ports in IdentityAccess.Application;
- Host.Api implements only the validated principal accessor and supplies middleware-created session metadata;
- IdentityAccess Infrastructure implements the public contract, current permission catalog and one-statement live-session/grant query;
- PersistenceRegistration wires the implementation; absent DB configuration maps to Unavailable.

A PostgreSQL test was added for active session, revocation, lease expiry, credential-version mismatch, disabled account and inactive grant. A focused unit test covers the public contract decisions. Exact-head CI remains pending.

The permission query is a request-time point-in-time authorization result; it is not a lock spanning a later Organization mutation. Organization consumer, commit-time revocation linearization, protected Product Owner binding, Organization assignment permission namespace, module-owner review and G1 remain open.

**Current disposition:** IdentityAccess contract/adapter SOURCE-IMPLEMENTED; test validation PENDING; O20-E OPEN; O-20 OPEN; O-21 OPEN; G1 NOT GRANTED.

**END — Stage 08 register v4.5**
