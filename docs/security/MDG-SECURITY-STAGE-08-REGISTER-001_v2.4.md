# MDG Security Stage 08 — Controlled Register Addendum

| Field | Value |
|---|---|
| Register ID | MDG-SECURITY-STAGE-08-REGISTER-001 |
| Version / Date | 2.4 / 2026-11-21 |
| Supersedes | v2.3 |
| Package | PR #86 — open, non-Draft, unmerged |
| Tested head for O20-G | `a878b0d502b42944b3479a97efbe61ebe7a00e43` |
| O20-G disposition | PASS — exact disposable PostgreSQL 18.6 CI proof and reviewed artifact |
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

## 3. Remaining O-20 work

- O20-E: reporting views/read models and positive/negative RowScope evidence remain OPEN. Canonical Branch/Warehouse owners and authoritative resolver sources have not been established; access remains fail-closed.
- O20-F: emergency allow-list, independent authorization, expiry, revocation and audit proof remain OPEN.
- Production deployment LOGIN binding, credential lifecycle and live cluster attestation remain outside this disposable CI proof.
- Fresh independent security review has not yet been requested for the final complete O-20 package.

O20-G is accepted for its bounded disposable CI scope only. O-20 remains OPEN. No production role, LOGIN, credential, grant, membership, ownership, migration, backup or restore operation was created or executed. PR #86 remains open and unmerged.


## 4. O20-E data-owner reconciliation — controlled proposal

The owner-approved Master Architecture v3.0.3 and Inventory owner decision OWN-INV-018 v1.1 establish **Organization** as the canonical owner of Branch, Warehouse and the Warehouse → Section → Shelf → Bin/StorageLocation hierarchy. Inventory owns stock facts and consumes those references; IdentityAccess owns accounts and security grants, not organizational placement assignments.

The remaining gap is no longer the Branch/Warehouse master owner. It is the authoritative lifecycle and runtime source for Account/Principal → Branch membership and Account/Principal → Warehouse assignment. The controlled proposal `MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.1` recommends that Organization own these explicit assignment facts using opaque AccountId references, while IdentityAccess supplies the authenticated principal and evaluates security grants.

Owner confirmation is still required for assignment ownership and semantics, including Employment linkage, multiple active assignments and whether Branch membership implies Warehouse access. No schema, migration, resolver, positive reporting grant or RowScope runtime was created. The existing fail-closed boundary remains in force.

**Disposition:** O20-E remains OPEN. Branch/Warehouse master ownership is evidence-resolved; assignment ownership/policy confirmation, authoritative runtime resolver, and positive/negative PostgreSQL RowScope proof remain outstanding. Overall O-20 remains OPEN.


## 5. O20-E owner acceptance — assignment policy resolved

On 2026-11-21, the project owner accepted the four policy decisions recorded in `MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.2`:
1. Organization owns Account-to-Branch and Account-to-Warehouse assignment facts.
2. Employment alone does not grant access; explicit active assignment is required.
3. Multiple active Branch and/or Warehouse assignments are permitted and individually auditable.
4. Branch assignment does not imply Warehouse access; Warehouse assignment must be explicit.

This resolves the outstanding ownership/policy decision from v0.1. It does not establish a runtime resolver or RowScope proof. Repository tree inspection at the acceptance-time PR #86 head found no Organization runtime module or canonical Organization persistence/resolver implementation. Therefore no schema, migration, API, grant or reporting view was guessed or introduced.

**O20-E remains OPEN** pending Organization module detailed design, implementation, independent review, and exact-SHA positive/negative PostgreSQL RowScope evidence. Fail-closed remains mandatory. Overall O-20 remains OPEN.


## 6. Organization logical design prepared

The owner-accepted policy has been translated into `MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.1`. It defines exclusive data ownership, logical assignment concepts, consumer boundaries, fail-closed resolution, SQL-before-materialization RowScope, revocation consistency requirements, privacy/security controls and the required PostgreSQL acceptance matrix.

This is a logical design only. The repository module topology policy says modules are introduced only after G1 authorization. No Organization runtime module or canonical persistence model exists in the inspected PR #86 tree. The design deliberately does not invent physical schema, keys, migrations, resolver method signatures or permission keys.

**O20-E remains OPEN** pending detailed domain/physical design, Organization G1 authorization, implementation, independent review and exact-SHA positive/negative PostgreSQL RowScope evidence. Reporting access remains fail-closed. Overall O-20 remains OPEN.
