# Persistence and migration operating rules

## Current implementation

- PostgreSQL provider: Npgsql for EF Core 10; target server is PostgreSQL 18.
- Context: `MDGsm.Infrastructure.Persistence.MdGsmDbContext` in `src/BuildingBlocks/MDGsm.Infrastructure`.
- Registration: `AddMdGsmPersistence` reads `ConnectionStrings:MDGsmDatabase`.
- No connection string means no `DbContext` is registered; the API may run for non-database endpoints.
- `/health/ready` is dependency-aware; `/health/live` is not.
- The application-owned CLI is `tools/MDGsm.DbMigrator`; SQL migrations are embedded into the Infrastructure assembly.
- The runner uses a PostgreSQL advisory transaction lock, a migration history table with SHA-256 checksums, checksum/name drift detection, and one PostgreSQL transaction for the migration batch.
- By default, the runner refuses to operate unless the target database comment contains `MDGSM_DISPOSABLE_TEST_DB`.
- `--allow-unmarked-database` additionally requires `MDGSM_UNMARKED_DB_CONFIRMATION=APPLY-TO-NON-DISPOSABLE-DATABASE`; credentials are read only from `ConnectionStrings__MDGsmDatabase` in the process environment.
- The API never applies migrations on startup. Migration execution is explicit.

## Migration inventory

| Version | File | Scope |
|---|---|---|
| 0001 | `0001_infrastructure_schema_version.sql` | Legacy infrastructure schema marker |
| 0002 | `persons/0002_persons_core.sql` | Persons master data, contact/address, idempotency and access audit |
| 0003 | `0003_identityaccess_core.sql` | IdentityAccess accounts, roles, permissions, sessions, login attempts, recovery, audit and singleton session policy |
| 0004 | `0004_persons_reserve_national_identifiers.sql` | Persons national-identifier reservation and uniqueness safeguards |
| 0005 | `0005_persons_nonblank_identity_names.sql` | Persons nonblank identity-name constraints |

## Migration additions on current main (integrated by PR #50)

| Version | File | Scope |
|---|---|---|
| 0006 | `0006_finance_party_accounts.sql` | Finance-owned unique Party subledger account identity; no cross-module FK or balance duplication |
| 0007 | `0007_persons_temporary_workflows.sql` | Temporary workflow metadata and encrypted payload lifecycle |
| 0008 | `0008_persons_party_merge.sql` | Persons-owned Party merge operation records and merge-state columns |

The duplicate national-identifier migration from the older PR #47 branch was not imported because main already owns version 0004. The unused row-version follow-up was folded out before application; migration 0006 is the single initial Finance schema migration. The eight-version sequence was validated on disposable PostgreSQL 18 Linux/Windows CI. Exact main SHA `f011c94d52bc06c7e213abb6e480180e9ea73f40` later passed push CI (Persons 13/13, CodeQL and Charter; 15/15 check-runs). This remains disposable CI only.

Migration 0003 creates the IdentityAccess schema and singleton `identity.system_settings` row (5 active accounts, 30-second heartbeat, 180-second lease), required by `IdentityAccessUnitOfWork`.

Migration 0003 deliberately does **not** create an administrator, password, recovery secret, role assignment, permission catalog seed, or role-permission grant. The proposed role/permission matrix requires explicit owner approval; until a separately reviewed seed/bootstrap operation exists, permission checks deny access by default. No credential is embedded in SQL, source, CI, or repository secrets.

## IdentityAccess runtime database role

The reviewed grant template is `database/operations/identityaccess-runtime-role-grants.sql`. Run it as the database deployment/owner identity after migrations. It creates only a `NOLOGIN` group role named `mdgsm_runtime`; the DBA must create the actual API login outside the repository and explicitly grant it membership. No password or login credential belongs in SQL, CI, or source control.

The grant template is deliberately conservative:
- read-only access to roles, permissions and assignments;
- existing-account read/update for login, lockout and password-change fields, but no account INSERT (bootstrap/provisioning remains separate);
- session and login-attempt lifecycle access;
- recovery challenge issue/consume and recovery-code consume only;
- INSERT-only security audit access;
- a column-only no-op UPDATE on the singleton settings row to serialize session capacity;
- no DDL, ownership, DELETE, TRUNCATE, permission/role writes, or schema CREATE.

This is the IdentityAccess schema grant set, not a complete application-wide API role. Persons/business tables require their own separately reviewed grant matrix before the same login can serve those routes. Re-run and review the grant script after schema changes; do not introduce broad default privileges.

## Staging deployment protocol

1. Provision an isolated PostgreSQL 18 staging database. Never point staging at production or a developer database.
2. Use `MDGSM_DISPOSABLE_TEST_DB` only on disposable CI/test databases. A persistent staging database needs a separately approved deployment procedure; do not mark it disposable to bypass the migration guard.
3. Before migration, take a database-native backup and verify restoration into a separate empty database. Keep backups outside the repository and CI artifacts.
4. Apply migrations with the application-owned migrator using a dedicated deployment identity. The migrator uses one transaction, an advisory lock, and SHA-256 history checks.
5. Run the migrator a second time. It must report zero new migrations; checksum/name drift or a missing lower version is a hard failure.
6. Verify `mdgsm_migration_history`, `identity.system_settings`, IdentityAccess tables/indexes/constraints, and Persons schema. Run PostgreSQL integration and HTTP end-to-end suites against disposable databases.
7. Separate database owner/deployment and runtime roles. Runtime must not own schemas/tables; grant only exact required schema/table/sequence/function privileges. Review and test grants before API connection.
8. Do not seed initial admin credentials or activate HTTP login until independent security review, secure bootstrap/recovery design, TLS topology, and owner approvals are complete.

## Local secret configuration

Prefer .NET user-secrets for local API development, or set `ConnectionStrings__MDGsmDatabase` in the process environment. Never commit actual credentials. Integration tests must use dedicated disposable databases.

## Central migration reservation registry

The authoritative reservation ledger and current inventory are maintained by WG-0 in `docs/data/MIGRATION_SEQUENCE_REGISTRY_FA.md`. A number is reserved only after the WG-0 reservation PR is merged into `main`; a proposal or unmerged branch does not reserve it. Migration PRs follow that reservation. CI tests enforce global filename/LogicalName and numeric-version uniqueness across all migration subfolders and verify a one-to-one match between every SQL file and exactly one parsed registry row, reject stale registry paths, duplicate/missing versions, and enforce contiguous applied versions while allowing explicitly reserved future numbers. Do not move or rename already-applied migrations; `persons/0002_persons_core.sql` is a recorded historical path exception.

## Migration naming risk

Migration 0002 is physically under database/migrations/persons while others are at root. Logical resource identity uses filename, not relative path; filenames must be globally unique across subfolders. Add a collision test; never move/rename an applied migration.

## Migration lifecycle

1. Approve a domain/schema change before writing SQL.
2. Add a stable globally unique ordered filename `NNNN_descriptive_name.sql` under `database/migrations`; embedded resource names are based on filename.
3. Review full SQL, transaction behavior, privileges, indexes, and forward-recovery plan. Avoid destructive down-migrations for financial/security data; use reviewed forward fixes.
4. Test clean install, repeat/idempotence, checksum drift rejection, out-of-order rejection, constraint failures, and restore before staging promotion.
5. Never apply migrations implicitly at API startup. Production requires a separately approved deployment invocation and deployment-only identity.
6. Runtime application credentials must not be database owners and must have least-privilege grants.

A successful migration CI run proves schema application against an isolated PostgreSQL service; it does not provision or migrate staging/production. Only IdentityAccess has a runtime grant template; Persons and Finance grant matrices are absent, so least-privilege coverage is partial.
