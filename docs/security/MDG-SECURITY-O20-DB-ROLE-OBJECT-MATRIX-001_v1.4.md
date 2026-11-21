# O-20 Database Role/Object Matrix — O20-G Accepted Catalog Evidence

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 |
| Version / Date | 1.4 / 2026-11-21 |
| Supersedes | v1.3 |
| Policy baseline | Owner-approved N.6 five-identity model |
| Tested head | `a878b0d502b42944b3479a97efbe61ebe7a00e43` |
| Workflow / Job | `44942680167` / `136963199892` |
| O20-G | PASS — disposable PostgreSQL 18.6 CI evidence |
| Overall O-20 | OPEN |

## 1. Logical identities and exact CI mapping

| N.6 logical identity | CI database role | Observed state |
|---|---|---|
| runtime_app | `mdgsm_runtime` | NOLOGIN, non-elevated, NOINHERIT, no membership edges; 180 effective-privilege records |
| migration_runner | `migration_runner` | Non-elevated LOGIN, NOINHERIT; one SET-only membership to `mdgsm_schema_owner`; 47 effective-privilege records |
| backup_operator | `backup_operator` | Ephemeral during O20-D only; absent after cleanup |
| reporting_ro | `reporting_ro` | Not provisioned; deny until RowScope/view contract and proof |
| emergency_operator | `emergency_operator` | Not provisioned; deny until O20-F authorization lifecycle proof |

`mdgsm_schema_owner` is a separate NOLOGIN ownership role, not a sixth application identity. The CI artifact observed 24 ownership dependencies held by this role.

## 2. Exact O20-G inventory

On PostgreSQL 18.6 (`server_version_num=180006`), the generated JSON inventory recorded: 20 roles; 4 membership edges; 5 schemas; 522 relations; 35 explicit column ACL entries; 3,414 routines; 665 types; 0 default ACL entries; 1 extension; 4 languages; 0 FDWs; 0 foreign servers; 0 large objects; 2 tablespaces; 24 object ownership dependencies; and 227 mapped effective-privilege records.

Evidence JSON SHA-256:
`278a2fb1e7e5fbd55cd594f1afdd97c37a2f3e00fc71a788e8bc73c28b17b50f`

GitHub Actions artifact:
- Name: `o20-g-catalog-inventory`
- Artifact ID: `14721784775`
- Compressed size: 48,192 bytes
- ZIP digest: `sha256:5af5896a9544c6bb8c12ce7b8500a81f502f529678438bfd63ab7ffda90c817c`

## 3. Boundaries and disposition

O20-G PASS is limited to the marked disposable CI database and cluster role catalog. It covers role flags, role graph, database/schema/relation/column/routine/type/extension/default ACL metadata, special object ACL catalogs, ownership dependencies and effective privileges for mapped identities.

The runtime mapping is an ACL-role fixture, not proof of the deployment LOGIN membership. Absent backup/reporting/emergency principals are not evidence of positive access. No production credentials, grants, memberships or ownership changes were performed.

O20-E RowScope and O20-F emergency allow-list/expiry/revocation remain open. Overall O-20 remains OPEN pending those items, production identity lifecycle evidence and fresh independent review.
