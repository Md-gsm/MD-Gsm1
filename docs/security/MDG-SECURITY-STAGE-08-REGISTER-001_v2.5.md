# MDG Security Stage 08 — Controlled Register Addendum

| Field | Value |
|---|---|
| Register ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 2.5 / 2026-11-22 |
| Supersedes | v2.4 |
| Package | PR #86 — open, non-Draft, unmerged |
| Tested head for O20-G | `a878b0d502b42944b3479a97efbe61ebe7a00e43` |
| O20-G disposition | PASS — exact disposable PostgreSQL 18.6 CI proof and reviewed artifact |
| Controlled document head | `edc301064c64da5836a02958e2e323a3dad17f0f` |
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

- **O20-E:** Organization data ownership is now resolved at the policy level. Master Architecture v3.0.3 and OWN-INV-018 v1.1 place Branch, Warehouse and the Warehouse → Section → Shelf → Bin/StorageLocation hierarchy under Organization. The owner accepted Organization ownership of AccountBranchAssignment and AccountWarehouseAssignment and the four assignment rules in O20E data ownership v0.2.
- **O20-E logical design:** `MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.1` is recorded.
- **O20-E physical design:** `MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v0.1` has been added on controlled head `edc301064c64da5836a02958e2e323a3dad17f0f`. It is a proposal only. No Organization runtime module, schema, table, DDL, migration, seed, permission, grant, endpoint, resolver, reporting view or feature activation has been created.
- **O20-E still OPEN:** PD-ORG-01 through PD-ORG-12 require owner/technical determination and review, including Warehouse-to-Branch cardinality and reassignment, business-code uniqueness, lifecycle/archive rules, assignment effective/reactivation semantics, permission keys and approval separation, IdentityAccess scope composition, AuditSecurity outbox contract, UUID generation policy, cross-module transaction/lock order, retention/legal hold, and source-module RowScope path inventory. Organization G1 authorization is NOT GRANTED. Positive/negative PostgreSQL RowScope evidence is NOT RUN. Reporting access remains fail-closed.
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

## 6. O20-E physical design proposal — v0.1

The controlled physical proposal defines a candidate `organization` PostgreSQL namespace and proposed shapes for:
- Branch and Warehouse masters;
- explicit AccountBranchAssignment and AccountWarehouseAssignment current-state records;
- append-only assignment lifecycle events;
- local assignment audit outbox intent;
- local integrity/index proposals, source identifier boundaries and no cross-module FK into IdentityAccess;
- authorization composition, fail-closed typed resolution, SQL-before-materialization RowScope, snapshot/revocation consistency, export revalidation, and a 24-case PostgreSQL acceptance matrix.

**Important limitations:** this is not an approved executable schema. Warehouse-to-Branch cardinality/reassignment and the related assignment-history consequences remain open. Business-code uniqueness, lifecycle vocabularies, assignment effective dating/reactivation, permission keys, IdentityAccess scope intersection, AuditSecurity event/ACK contract, UUID generator version, transaction lock ordering, legal retention and source-module RowScope contracts remain open. No table/DDL/EF entity/migration/seed/grant/API/runtime has been introduced by the proposal. All such work remains blocked pending G1 authorization and subsequent execution gates.

## 7. Exact controlled source and integrity boundary

- Existing PR: #86, branch `security/stage08-n3-adr0013-reconciliation`, base `main`.
- Physical proposal creation commit: `edc301064c64da5836a02958e2e323a3dad17f0f`.
- This register is a controlled documentation successor. Its own commit and final manifest commit must be read from GitHub before declaring the final document head.
- The physical proposal and this register must both be recorded in `SHA256SUMS.txt`; the checksum file itself is not self-hashed.
- No CI PASS is claimed for the successor documentation head until exact-head workflow results and checksum validation are retrieved and inspected.

## 8. Stage 08 disposition

| Item | Current disposition |
|---|---|
| O20-D | Bounded disposable logical backup/isolated restore — PASS only within cited CI scope |
| O20-G | Bounded disposable PostgreSQL 18.6 catalog/ACL inventory — PASS only within cited CI scope |
| O20-E owner policy | OWNER-ACCEPTED |
| O20-E logical design | DOCUMENTED |
| O20-E physical design | PROPOSED v0.1; detailed decisions and review OPEN |
| O20-E Organization G1 | NOT GRANTED |
| O20-E runtime / RowScope tests | NOT IMPLEMENTED / NOT RUN |
| O20-F emergency allow-list lifecycle | OPEN |
| Overall O-20 | OPEN |

No fresh independent reviewer request is made at this stage. As previously directed, reviewer re-request is deferred until the remaining O-20 work, including O20-E and O20-F, is ready for consolidated review. PR #86 remains open and unmerged.

No production database, production identity, credential, grant, migration, reporting permission, emergency access or operational feature was created or changed.

**END — MDG-SECURITY-STAGE-08-REGISTER-001 v2.5**
