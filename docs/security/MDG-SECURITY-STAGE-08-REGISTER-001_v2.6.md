# MDG Security Stage 08 — Controlled Register Addendum

| Field | Value |
|---|---|
| Register ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 2.6 / 2026-11-22 |
| Supersedes | v2.5 |
| Package | PR #86 — open, non-Draft, unmerged |
| Tested head for O20-G | `a878b0d502b42944b3479a97efbe61ebe7a00e43` |
| O20-G disposition | PASS — exact disposable PostgreSQL 18.6 CI proof and reviewed artifact |
| Controlled document head | `6121705b7de8aec603057b95349aa7f9be6a86ed` |
| Overall O-20 | OPEN |

## 1. O20-D carried forward

O20-D remains PASS for the bounded disposable logical-backup/isolated-restore proof recorded in O20 audit v1.4. This is not production backup/PITR acceptance.

## 2. O20-G accepted evidence

The O20-G proof and artifact-upload steps completed SUCCESS in workflow `44942680167`, job `136963199892`, on exact source head `a878b0d502b42944b3479a97efbe61ebe7a00e43`.

- PostgreSQL `server_version_num=180006`.
- Cluster roles: 20; membership edges: 4; schemas: 5; relations: 522; column ACL entries: 35; routines: 3,414; types: 665; default ACL entries: 0; extensions: 1; languages: 4; FDWs: 0; foreign servers: 0; large objects: 0; tablespaces: 2; target ownership dependencies: 24; mapped effective privilege records: 227.
- JSON SHA-256: `278a2fb1e7e5fbd55cd594f1afdd97c37a2f3e00fc71a788e8bc73c28b17b50f`.
- Uploaded artifact `o20-g-catalog-inventory`, ID `14721784775`, compressed size 48,192 bytes, ZIP digest `sha256:5af5896a9544c6bb8c12ce7b8500a81f502f529678438bfd63ab7ffda90c817c`.
- Artifact was downloaded from GitHub, JSON parsed independently, size verified (2,080,277 bytes) and the JSON digest recomputed.

Runtime/migration ACL boundaries passed; `mdgsm_schema_owner` held the 24 observed object-ownership dependencies; migration_runner had exactly one ADMIN=false / INHERIT=false / SET=true membership; backup_operator/reporting_ro/emergency_operator were absent after their allowed test lifecycle boundaries.

## 3. Remaining O-20 work — controlled status on 2026-11-22

- **O20-E:** Organization data ownership is resolved at the policy level. Master Architecture v3.0.3 and OWN-INV-018 v1.1 place Branch, Warehouse and the Warehouse → Section → Shelf → Bin/StorageLocation hierarchy under Organization. The owner accepted Organization ownership of AccountBranchAssignment and AccountWarehouseAssignment and the four assignment rules in O20E data ownership v0.2.
- **O20-E logical design:** `MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.1` is recorded.
- **O20-E physical design:** `MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v0.1` is recorded as a proposal. Its latest refinement explicitly records PD-ORG-13 because owner policy v0.2 requires an explicit Warehouse assignment and denies Branch-to-Warehouse inheritance, but does not expressly resolve whether the parent Branch assignment is an additional prerequisite for Warehouse-scoped access.
- **O20-E source inventory:** `MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 v0.1` inventories current module topology and API query surfaces. It found no executable Organization, Inventory or Reporting module, no report/export endpoint, and no current branch/warehouse RowScope source query to test. It also records a documentation/code route-version discrepancy for the existing Persons detail endpoint. This is a repository snapshot; it does not close future-consumer coverage.
- **O20-E still OPEN:** PD-ORG-01 through PD-ORG-13 and independent review remain open. Organization G1 authorization is NOT GRANTED. Positive/negative PostgreSQL RowScope evidence is NOT RUN. Reporting access remains fail-closed.
- **O20-F:** Emergency allow-list, independent authorization, expiry, revocation, audit and fail-closed execution evidence remain OPEN. No emergency role or production credential is provisioned.
- O20-D and O20-G retain only their bounded disposable PostgreSQL CI dispositions stated in their controlled evidence records; neither is a production acceptance.

## 4. O20-E owner policy reconciliation — accepted, not implementation authorization

The canonical owner of Branch and Warehouse masters and of the Warehouse/location hierarchy is Organization under Master Architecture v3.0.3 and OWN-INV-018 v1.1. The owner-approved O20E data ownership v0.2 resolves assignment ownership and policy:
1. Organization owns Account-to-Branch and Account-to-Warehouse assignment facts.
2. Employment alone does not grant access; an explicit active assignment is required.
3. Multiple active assignments are permitted for both Branch and Warehouse; each is individually auditable.
4. Branch assignment does not imply Warehouse access; a specific explicit Warehouse assignment is required.

These decisions supersede the pending-owner wording in Stage 08 Register v2.4 sections 4 and 5. They do not constitute G1 authorization, implementation approval, technical acceptance or evidence of runtime behavior. The prior IdentityAccess data-owner contract remains valid as a fail-closed boundary, with its old statement that canonical ownership was unknown superseded only by these later owner-approved sources.

## 5. O20-E Organization logical design

`MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.1` records the logical module boundary, ownership, explicit assignment concepts, server-side resolution, SQL-before-materialization RowScope, revocation consistency, security/privacy requirements and 16 acceptance scenarios.

The repository module topology rule in `src/Modules/README.md` permits introduction of a module only after G1 authorization. Therefore logical design did not create an Organization runtime module or authorize source-code implementation. It deliberately left physical schema, migrations, resolver signatures, permission keys and detailed lifecycle decisions open.

## 6. O20-E physical design proposal — v0.1, controlled refinement

The proposal defines a candidate `organization` PostgreSQL namespace and candidate shapes for Branch/Warehouse masters, explicit account assignment records, append-only assignment lifecycle events and a local audit outbox. It also defines candidate indexes, authorization composition, fail-closed resolution, SQL-before-materialization RowScope, snapshot/revocation consistency, export revalidation, privilege boundaries and 24 PostgreSQL acceptance cases.

The refinement at commit `46b2412e6cf73e185e8e3eee7a258065db0091e6` added **PD-ORG-13**: whether an active AccountBranchAssignment to the Warehouse's current parent Branch is additionally required when an AccountWarehouseAssignment exists. The current logical design treats the explicit Warehouse assignment plus active Warehouse/Branch masters as sufficient, but policy v0.2 does not explicitly decide this conjunction. It is not to be silently treated as owner-approved; runtime design remains blocked until the owner resolves it.

The following remain OPEN: Warehouse-to-Branch cardinality and reassignment; Branch/Warehouse code normalization and uniqueness; lifecycle/archive transitions; assignment scheduling, expiry and reactivation; permission keys and approval separation; IdentityAccess scope composition; AuditSecurity event/ACK contract; UUID generator policy; cross-module transaction/lock order; retention/legal hold; and full future-consumer RowScope coverage.

No runtime module, schema, table, DDL, EF entity, migration, seed, grant, API, resolver, view or reporting permission has been introduced. The document remains a proposal, not an executable schema authorization.

## 7. O20-E current source/query-path inventory

`MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 v0.1` was prepared against inspected commit `46b2412e6cf73e185e8e3eee7a258065db0091e6`.

The inspected executable module directories are Accounting, Finance, IdentityAccess, Persons and RepairOperations. No executable Organization, Inventory or Reporting module was present. The API composition root exposes health/readiness, system info, identity login/password/heartbeat/logout, and one Persons Party detail endpoint. No report, dashboard aggregate, print, PDF, CSV/XLSX export or generic report/search route is registered there.

The current Party detail endpoint does not supply BranchId/WarehouseId row context. RepairOperations owns technician-to-repair assignment conceptually but has no published runtime source-event contract. Finance and Accounting published contracts do not expose report query paths in the inspected snapshot. Inventory and Reporting remain design-only in this tree.

The snapshot records a documentation/code discrepancy: Program.cs registers `GET /api/v2/persons/{partyId}`, while the endpoint/API README files describe `/api/v1/persons/{partyId}`. It is not changed as part of O20-E. The source inventory will need refresh when new consumer modules or query/export paths are introduced. This discovery does not close PD-ORG-12 for future sources and is not a positive or negative RowScope execution test.

## 8. Exact controlled source and integrity boundary

- Existing PR: #86, branch `security/stage08-n3-adr0013-reconciliation`, base `main`.
- Physical design proposal initial commit: `edc301064c64da5836a02958e2e323a3dad17f0f`; latest refinement commit: `46b2412e6cf73e185e8e3eee7a258065db0091e6`.
- Source inventory creation commit: `6121705b7de8aec603057b95349aa7f9be6a86ed`.
- This register and the latest physical design/source inventory must be present in `SHA256SUMS.txt`; the checksum file itself is not self-hashed.
- No CI PASS is claimed for this successor documentation head until exact-head workflow results and checksum verification are retrieved and inspected.

## 9. Stage 08 disposition

| Item | Current disposition |
|---|---|
| O20-D | Bounded disposable logical backup/isolated restore — PASS only within cited CI scope |
| O20-G | Bounded disposable PostgreSQL 18.6 catalog/ACL inventory — PASS only within cited CI scope |
| O20-E owner policy | OWNER-ACCEPTED |
| O20-E logical design | DOCUMENTED |
| O20-E physical design | PROPOSED v0.1; PD-ORG-01..13 and independent review OPEN |
| O20-E current source inventory | SNAPSHOT PREPARED; future consumers remain unverified |
| O20-E Organization G1 | NOT GRANTED |
| O20-E runtime / RowScope tests | NOT IMPLEMENTED / NOT RUN |
| O20-F emergency allow-list lifecycle | OPEN |
| Overall O-20 | OPEN |

No fresh independent reviewer request is made at this stage. As previously directed, reviewer re-request is deferred until the remaining O-20 work, including O20-E and O20-F, is ready for consolidated review. PR #86 remains open and unmerged.

No production database, production identity, credential, grant, migration, reporting permission, emergency access or operational feature was created or changed.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v2.6**
