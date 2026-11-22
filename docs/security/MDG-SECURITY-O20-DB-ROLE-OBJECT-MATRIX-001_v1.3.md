# O-20 Database Role/Object Matrix — O20-G Complete Catalog Coverage

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-ROLE-OBJECT-MATRIX-001 |
| Version / Date | 1.3 / 2026-11-21 |
| Supersedes | v1.2 |
| Policy baseline | Owner-approved N.6 five-identity model |
| O20-G disposition | Executable catalog proof added; exact-head CI pending |
| Overall O-20 | OPEN |

## 1. Logical identities and observed CI role binding

| N.6 logical identity | Current CI database role | CI disposition / boundary |
|---|---|---|
| runtime_app | mdgsm_runtime | NOLOGIN privilege/ACL role; deploy-time LOGIN membership binding is not claimed |
| migration_runner | migration_runner | Non-elevated LOGIN; one non-inheriting SET-only edge to mdgsm_schema_owner |
| backup_operator | backup_operator | Ephemeral only during O20-D; must be absent after cleanup |
| reporting_ro | reporting_ro | Intentionally not provisioned; deny until approved RowScope/view design |
| emergency_operator | emergency_operator | Intentionally not provisioned; deny until O20-F authorization lifecycle proof |

mdgsm_schema_owner is a separate NOLOGIN ownership role, not a sixth application identity.

## 2. Catalog classes covered by O20-G

The inventory includes cluster roles and membership edges; target database ACL; all schemas and relation objects; table/view/sequence/foreign-table ACLs; column ACLs; routines/procedures and SECURITY DEFINER metadata; types; extensions and extension-member object counts; default ACLs; language, FDW, foreign-server, large-object and tablespace metadata/ACLs; target database object ownership dependencies; and effective privileges for each provisioned mapped N.6 role on database, schema, relation, sequence, column, routine and type objects.

ACLs are expanded into grantor, grantee, privilege type and grant-option records. Core database/schema/relation/routine/type ACL defaults are expanded using PostgreSQL acldefault semantics. Additional catalogs with nullable ACL arrays are represented by explicit ACL entries and the corresponding supported effective privileges are included where applicable.

## 3. Required assertions

The CI test verifies safe role attributes, exact migration SET ROLE edge options, runtime deny boundaries, object non-ownership by runtime/migration LOGINs, no runtime-owned SECURITY DEFINER routines, no runtime/migration default ACL ownership, and removal/non-provisioning of backup/reporting/emergency identities outside their scoped lifecycle tests.

This is a deny/least-privilege and inventory proof on a marked disposable PostgreSQL 18 database. It does not grant access or create persistent production principals. Runtime alias mapping does not prove deployment LOGIN membership. No reporting RowScope positive test or emergency operator authorization is implied.

## 4. Evidence

Exact-head CI run, counts, generated artifact and SHA-256 digest: pending execution. The final version must bind these values to the tested commit SHA and workflow job, not a prior head.
