# MDG Security Stage 08 — Controlled Register Addendum

| Field | Value |
|---|---|
| Register ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 3.1 / 2026-11-22 |
| Supersedes | v3.0 |
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
