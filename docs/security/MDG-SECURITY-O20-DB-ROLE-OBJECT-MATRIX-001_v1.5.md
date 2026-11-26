# O-20 Database Role/Object Matrix — O20-G Accepted Catalog Evidence

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 |
| Version / Date | 1.5 / 2026-11-25 |
| Supersedes | v1.4 |
| Policy baseline | Owner-approved N.6 five-identity model |
| Tested head | Pending — exact PR #96 head verification required |
| Workflow / Job | Pending — fresh exact-head CI run required |
| O20-G | PENDING — fresh disposable PostgreSQL 18 catalog evidence required |
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

## 2. Prior v1.4 O20-G inventory (historical baseline)

On PostgreSQL 18.6 (`server_version_num=180006`), the generated JSON inventory recorded: 20 roles; 4 membership edges; 5 schemas; 522 relations; 35 explicit column ACL entries; 3,414 routines; 665 types; 0 default ACL entries; 1 extension; 4 languages; 0 FDWs; 0 foreign servers; 0 large objects; 2 tablespaces; 24 object ownership dependencies; and 227 mapped effective-privilege records.

Evidence JSON SHA-256:
`278a2fb1e7e5fbd55cd594f1afdd97c37a2f3e00fc71a788e8bc73c28b17b50f`

GitHub Actions artifact:
- Name: `o20-g-catalog-inventory`
- Artifact ID: `14721784775`
- Compressed size: 48,192 bytes
- ZIP digest: `sha256:5af5896a9544c6bb8c12ce7b8500a81f502f529678438bfd63ab7ffda90c817c`

## 3. Boundaries and disposition

O20-G PASS in v1.4 is historical and predates Persons migrations 0012/0013. Current acceptance remains pending a fresh exact-head inventory on the expanded schema. The new objects are: It covers role flags, role graph, database/schema/relation/column/routine/type/extension/default ACL metadata, special object ACL catalogs, ownership dependencies and effective privileges for mapped identities.

The runtime mapping is an ACL-role fixture, not proof of the deployment LOGIN membership. Absent backup/reporting/emergency principals are not evidence of positive access. No production credentials, grants, memberships or ownership changes were performed.

O20-E RowScope and O20-F emergency allow-list/expiry/revocation remain open. Overall O-20 remains OPEN pending those items, production identity lifecycle evidence and fresh independent review.


## 4. Persons AC-37 object ownership and privilege boundary

| Schema | Object | Owner | Migration | Runtime access |
|---|---|---|---|---|
| public | persons_temporary_workflow_promotions | mdgsm_schema_owner | 0012 | No grant |
| public | persons_temporary_workflow_access_audit | mdgsm_schema_owner | 0012 | No grant |
| public | persons_outbox_events | mdgsm_schema_owner | 0013 | No grant |
| public | persons_outbox_deliveries | mdgsm_schema_owner | 0013 | No grant |

These are Persons-owned source objects, not new application identities. The current five-identity N.6 model is unchanged. No mdgsm_runtime privilege is granted on these objects, no Outbox worker LOGIN/role is created, and no worker delivery runtime is enabled. The promotion API is disabled by default and is not mapped unless the host explicitly enables Persons:WorkflowPromotionApiEnabled and registers an approved key-provider adapter.

The new schema source is not yet O20-G accepted. The next exact-head disposable PostgreSQL 18 catalog inventory must prove the four objects' ownership and the absence of effective privileges for runtime_app, migration_runner beyond its existing SET-only migration boundary, and all unprovisioned support identities. Overall O-20 remains OPEN.
