# MDG Security Stage 08 — Controlled Register Addendum

| Field | Value |
|---|---|
| Register ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 2.7 / 2026-11-22 |
| Supersedes | v2.6 |
| Package | PR #86 — open, non-Draft, unmerged |
| Branch | `security/stage08-n3-adr0013-reconciliation` |
| Documentation baseline before this successor | `74dc74203e6563f5ed2ebbc11acc11180221f68a` |
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

The owner-accepted decisions recorded in `MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.2` remain:
1. Organization owns Account-to-Branch and Account-to-Warehouse assignment facts.
2. Employment by itself does not grant access; an explicit active assignment is required.
3. Multiple active Branch and Warehouse assignments may coexist; each is individually auditable.
4. Branch assignment does not imply Warehouse access; access to a Warehouse requires an explicit assignment to that exact Warehouse.

The governing Master Architecture v3.0.3 and `OWN-INV-018 v1.1` place Branch/Warehouse/location master data and the Warehouse → Section → Shelf → Bin/StorageLocation hierarchy under Organization; Inventory owns stock facts and movement. IdentityAccess remains owner of Account, Credential, Session, Role and Permission. These are ownership/policy conclusions, not G1 approval.

### 4.2 Controlled logical and physical design

- Logical design: `MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.1`.
- Physical design: `MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v0.1`, explicitly a proposal only.
- The physical proposal describes candidate Organization persistence concepts, but does not authorize source module, schema, tables, DDL, migrations, seed, resolver, endpoint, database role/grant, reporting view or runtime.
- `src/Modules/README.md` still requires G1 authorization before a module is introduced. The Organization directory does not exist in the inspected source tree.

### 4.3 Source/query-path inventory v0.2

`MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 v0.2` revalidated the source tree and API composition root at exact inspected commit `9838250e97be5f362f98e1e82a5e6508cf34cac5`.

The current executable module directories are Accounting, Finance, IdentityAccess, Persons and RepairOperations. No executable Organization, Inventory or Reporting module is present. The API composition root registers health/readiness, system information, IdentityAccess login/password/heartbeat/logout, and `GET /api/v2/persons/{partyId}`. No report/dashboard aggregate/print/PDF/CSV/XLSX/export/Inventory query route was found. The Party detail route has no BranchId/WarehouseId source context and is not a current O20-E RowScope consumer.

The inventory also records an unrelated documentation/code discrepancy: API README files describe `/api/v1/persons/{partyId}`, whereas the current Program.cs registration is `/api/v2/persons/{partyId}`. This discrepancy was not modified in this O20-E package.

**Disposition:** current-snapshot discovery is verified for the stated SHA only. There is no current reportable Branch/Warehouse query path to test, so positive/negative RowScope PostgreSQL tests remain NOT RUN / NOT APPLICABLE to the absent runtime consumer. Future consumer/source inventory remains OPEN and must be repeated as query/export paths are introduced.

## 5. O20-E — preliminary G1 readiness review

`MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 v0.1` is an internal preliminary cross-contract review, not an independent reviewer attestation and not an owner authorization.

**Disposition: NOT READY FOR G1.** The review found no basis to introduce Organization implementation until the following are resolved:

| Design item | Current status | Closure required |
|---|---|---|
| PD-ORG-01 Warehouse-to-Branch cardinality/reassignment/history | OPEN — owner | Define cardinality, reassignment process, history and fate of existing Warehouse assignments |
| PD-ORG-02 Branch/Warehouse codes | OPEN — owner/business | Define normalization, uniqueness scope, editability and reuse |
| PD-ORG-03 master lifecycle | OPEN — owner/business | Define active/inactive/archive transitions and prerequisites |
| PD-ORG-04 assignment lifecycle | OPEN — owner | Define effective dates, expiry and reactivation/new identity |
| PD-ORG-05 actor/reason/separation of duties | OPEN — owner/security | Decide self-assignment, maker-checker and exact authorization rules |
| PD-ORG-06 permission keys and role grants | OPEN — IdentityAccess/security + owner | Reconcile approved permission matrix with PermissionCatalog and publish versioned public principal/permission contracts |
| PD-ORG-07 IdentityAccess scope composition | PARTIAL / IMPLEMENTATION BLOCKED | Enforce permission AND Organization assignment; IdentityAccess scope may narrow only; publish compatible versioned contract |
| PD-ORG-08 AuditSecurity outbox/event contract | PARTIAL / OPEN | Publish/accept canonical event envelope, durable ACK, retry/quarantine, worker authority and retention |
| PD-ORG-09 identifier generation | OPEN — Platform | Set Organization ID representation/generation policy; PartyId UUIDv7 alone is not a global rule |
| PD-ORG-10 Unit of Work and cross-module lock order | OPEN — Platform/Application | Publish global Lock-Order Registry and prove revocation/write concurrency on PostgreSQL |
| PD-ORG-11 assignment-history retention/legal hold | OPEN — owner/legal | Set retention, legal hold, archive and disposal policy |
| PD-ORG-12 source RowScope inventory | CURRENT SNAPSHOT VERIFIED; FUTURE COVERAGE OPEN | Re-inventory and test every new source/query/report/export surface |
| PD-ORG-13 Branch prerequisite for Warehouse scope | OPEN — owner | Decide explicitly whether exact Warehouse assignment alone is sufficient or an active parent Branch assignment is also required |

The readiness review records implementation blockers in the existing IdentityAccess integration: current `PermissionCatalog.cs` contains no `organization.*` keys and lacks some report permissions in the already approved policy matrix; `src/Modules/IdentityAccess/Contracts/README.md` says no runtime public contract is published. It also records that Master requires an explicit Lock-Order Registry and PostgreSQL concurrency evidence, and that AuditSecurity remains sole owner of canonical shared audit events.

Any recommendations in the preliminary review are options only and do not become accepted policy without explicit owner/platform/security decisions.

## 6. O20-E gate state

| Gate | Current disposition |
|---|---|
| Organization Branch/Warehouse ownership | ESTABLISHED |
| AccountBranchAssignment / AccountWarehouseAssignment ownership | OWNER-ACCEPTED |
| Four assignment policy invariants | OWNER-ACCEPTED |
| Logical design | DOCUMENTED |
| Physical design | PROPOSED; not implementation-authorized |
| Current repository source inventory | REVALIDATED snapshot at stated SHA |
| Preliminary G1 readiness | NOT READY |
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
- Source inventory v0.2 SHA-256: `51611ab147dbe05c6f56380fea409b8ea6cd6b30717f64781189908a6f679d24`; creation commit `16e5069dbe926ee62486b43cff83c0e02b0ce780`.
- G1 readiness review v0.1 SHA-256: `2d32f53d3a3d37726204347101b908fe8bf1c2e45ab02bb30a122f4be7b1d36a`; creation commit `74dc74203e6563f5ed2ebbc11acc11180221f68a`.

The corresponding entries and this register's own digest must be recorded in `SHA256SUMS.txt`. The checksum file is not self-hashed. CI on the eventual final documentation/checksum head must be retrieved and inspected before any exact-head PASS is claimed; no current-head all-workflow PASS is asserted by this register.

## 9. Consolidated Stage 08 disposition

| Item | Disposition |
|---|---|
| O20-D | PASS — bounded disposable logical backup/isolated restore only |
| O20-G | PASS — bounded disposable PostgreSQL 18.6 catalog/ACL inventory only |
| O20-E | OPEN — owner policy accepted; designs documented; G1 NOT READY; runtime and RowScope tests NOT RUN |
| O20-F | OPEN — emergency allow-list lifecycle and evidence incomplete |
| Overall O-20 | **OPEN** |

No new Organization module, schema, table, DDL, migration, seed, permission key, role/grant, ReportingReadOnly privilege, endpoint, report view, outbox contract, production credential, or production access was created or authorized. Reporting remains fail-closed.

No new independent reviewer request is made. Per the existing sequencing direction, consolidated reviewer re-request is deferred until both O20-E and O20-F are complete. PR #86 remains open and unmerged.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v2.7**


## Current Architecture Master authority — 2026-11-24

The Product Owner has designated **MDG-ARCH-MASTER-001 v3.0.4** as the sole current, accepted, official and governing Architecture/Accounting Master. All earlier Master versions, including v3.0.3, are historical snapshots only. This register remains authoritative only for its own defined subject matter and status dimensions; this authority update does not change any O-item, workpaper, security finding, implementation, test, evidence or gate status unless a separate explicit record does so.

The current repository-level authority record is MDG-ARCH-BASELINE-REGISTRATION-001 v1.3. Historical records and earlier status statements are preserved as provenance and are superseded for current Master authority. No implementation, production credential, SQL/DDL, migration, activation, release or Go-Live is authorized by this notice.
