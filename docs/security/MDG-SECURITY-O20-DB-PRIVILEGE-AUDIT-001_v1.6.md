# O-20 Database Privilege Audit — O20-G Catalog Evidence Acceptance

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-DB-PRIVILEGE-AUDIT-001 |
| Version / Date | 1.6 / 2026-11-21 |
| Supersedes | v1.5 |
| Repository / PR | Md-gsm/MD-Gsm1 / PR #86 |
| Tested head | `a878b0d502b42944b3479a97efbe61ebe7a00e43` |
| Workflow / Job | MD-Gsm Persons Module Gates `44942680167` / `persons-postgres-migrations` job `136963199892` |
| O20-G | PASS — marked disposable PostgreSQL 18.6 CI cluster and database only |
| Overall O-20 | OPEN |

## 1. Exact execution and evidence identity

The O20-G proof step and its artifact-upload step completed SUCCESS on the exact tested head above. The job completed successfully. The same workflow's source checksum verifier also completed successfully and accepted `SHA256SUMS.txt` for this head.

- PostgreSQL `server_version_num=180006` (18.6).
- Evidence format: `MDGSM-O20-G-CATALOG-v1`.
- JSON output: `o20-g-catalog-inventory.json`.
- JSON evidence SHA-256: `278a2fb1e7e5fbd55cd594f1afdd97c37a2f3e00fc71a788e8bc73c28b17b50f`.
- GitHub Actions artifact: `o20-g-catalog-inventory`, artifact ID `14721784775`, 48,192 bytes compressed.
- Uploaded ZIP SHA-256: `5af5896a9544c6bb8c12ce7b8500a81f502f529678438bfd63ab7ffda90c817c`.
- Artifact retention expiry reported by GitHub: 2026-12-05T19:47:00Z.

The JSON artifact was downloaded and independently parsed after upload. Its byte count was 2,080,277 and its locally recomputed SHA-256 matched the value above.

## 2. Captured inventory counts

| Catalog/evidence class | Count |
|---|---:|
| Cluster roles | 20 |
| Role membership edges | 4 |
| Database schemas | 5 |
| Relations (tables, indexes, views and other relkinds) | 522 |
| Explicit column ACL entries | 35 |
| Routines | 3,414 |
| Types | 665 |
| Default ACL entries | 0 |
| Extensions | 1 |
| Languages | 4 |
| Foreign-data wrappers | 0 |
| Foreign servers | 0 |
| Large objects | 0 |
| Tablespaces | 2 |
| Object ownership dependencies for target database/shared target objects | 24 |
| Effective privilege records for mapped canonical identities | 227 |

## 3. Canonical identity observations

| Logical identity | CI role binding and result |
|---|---|
| runtime_app | Mapped to `mdgsm_runtime`, NOLOGIN, non-elevated, NOINHERIT; no membership edges; 180 effective-privilege records captured; database/schema CREATE denied |
| migration_runner | Dedicated non-elevated LOGIN, NOINHERIT; exactly one membership to `mdgsm_schema_owner` with ADMIN=false, INHERIT=false, SET=true; 47 effective-privilege records captured; no effective database/public-schema CREATE |
| backup_operator | Absent after the separate ephemeral O20-D lifecycle and cleanup |
| reporting_ro | Intentionally absent; this is not a positive RowScope grant or read-access proof |
| emergency_operator | Intentionally absent; no emergency access is enabled |

The 24 observed object ownership dependencies in the target database were owned by `mdgsm_schema_owner`. The separate ownership role is NOLOGIN and non-elevated. The membership graph contains the expected migration SET-only edge and the three PostgreSQL `pg_monitor` component memberships.

The role inventory deliberately excludes password verifiers and credentials. The JSON contains catalog metadata and ACLs only; it contains no business rows, account hashes, recovery tokens, session tokens or secret values.

## 4. Inventory coverage and guard behavior

The executable inventory records:
- all cluster role attributes and role membership edges, including ADMIN/INHERIT/SET options;
- target database ownership/configuration and expanded database ACL;
- all database schemas, relation ownership, relation ACLs, column ACLs, routines and SECURITY DEFINER/config metadata, types, extensions and extension membership counts;
- default ACLs, languages, foreign-data wrappers, foreign servers, large-object metadata and tablespace ACLs;
- target database object ownership dependencies;
- effective database, application-schema, relation, sequence, column, routine and type privileges for provisioned mapped canonical identities.

The runner refuses to operate unless the target name matches the per-run disposable database pattern, the database has the `MDGSM_DISPOSABLE_TEST_DB;` marker, and the audit connection is a catalog-audit superuser. It does not change grants, roles, memberships or ownership.

## 5. O20-G disposition and boundaries

**O20-G PASS** — complete catalog/ACL/ownership and mapped effective-privilege inventory was executed on the marked disposable PostgreSQL CI database; the evidence artifact was uploaded, independently downloaded and parsed, and its SHA-256 was recomputed.

This result is not a scan of a production PostgreSQL cluster and does not attest live deployment credentials or password lifecycle. The current CI mapping of `runtime_app` to the NOLOGIN ACL role `mdgsm_runtime` does not prove the deployment LOGIN binding. Absence of `reporting_ro` and `emergency_operator` is recorded as an intentional deny/unprovisioned state, not as positive authorization evidence.

O20-G does not prove O20-E reporting RowScope or O20-F emergency allow-list/expiry/revocation. It does not prove production backup/PITR, off-host retention, encryption/key custody, production scheduling or RTO/RPO.

**Overall O-20 remains OPEN** until O20-E, O20-F, deployment identity/credential lifecycle controls and independent security review are satisfied. No production role, LOGIN, credential, grant, membership, ownership change, migration, backup or restore was created or executed.
