# Persistence and migration operating rules

## Current implementation

- PostgreSQL provider: Npgsql for EF Core 10.
- Context: `MDGsm.Infrastructure.Persistence.MdGsmDbContext` in `src/BuildingBlocks/MDGsm.Infrastructure`.
- Registration: `AddMdGsmPersistence` reads `ConnectionStrings:MDGsmDatabase`.
- No connection string means no `DbContext` is registered; the API may run for non-database endpoints.
- `/health/ready` is dependency-aware; `/health/live` is not.
- The application-owned CLI is `tools/MDGsm.DbMigrator`; its runner is implemented in `src/BuildingBlocks/MDGsm.Infrastructure` and loads SQL migrations as embedded resources.
- The runner uses a PostgreSQL advisory transaction lock, a migration history table with SHA-256 checksums, checksum/name drift detection, and a PostgreSQL transaction for the migration batch.
- By default, the runner refuses to operate unless the target database comment contains `MDGSM_DISPOSABLE_TEST_DB`. An explicitly approved deployment invocation may opt out using `--allow-unmarked-database`; credentials are read only from `ConnectionStrings__MDGsmDatabase` in the process environment.
- The API never applies migrations on startup. Migration execution is explicit.
- No Persons, accounting, or other business tables or migrations exist; migration 0001 creates infrastructure metadata only.

## Local secret configuration

Prefer .NET user-secrets for local API development, or set `ConnectionStrings__MDGsmDatabase` in the API process environment. Never commit actual credentials. Use a dedicated disposable database for integration tests; do not point tests at a personal or production database.

## Migration lifecycle (required before first business schema)

1. Model and approve a domain change before writing schema.
2. Add a stable, ordered SQL migration named `NNNN_descriptive_name.sql` under `database/migrations`; SQL files are embedded into the Infrastructure assembly at build time.
3. Review the complete SQL, transaction behavior, access grants, and rollback/forward-recovery plan. Destructive changes require explicit approval and a verified backup strategy.
4. Create a new database and mark it with the PostgreSQL comment `MDGSM_DISPOSABLE_TEST_DB`.
5. Set `ConnectionStrings__MDGsmDatabase` in the process environment and run `dotnet run --project tools/MDGsm.DbMigrator/MDGsm.DbMigrator.csproj --configuration Release`.
6. Verify the migration history and SHA-256 checksums, repeat the command to prove idempotence, then run integration tests.
7. Do not apply schema migrations implicitly on API startup. Production use requires a separately approved invocation with a deployment-only identity and explicit review; the runner refuses unmarked databases unless `--allow-unmarked-database` is intentionally supplied.
8. Runtime application credentials must not be database owners and should receive only the permissions needed by application operations.

A database connection check alone is not a schema compatibility check. Add a required migration-version check when the first migration is added.


## Module ownership and global migration ordering (proposed before first business migration)

Module ownership does not mean per-module version counters. The shared mdgsm_migration_history.migration_version remains a single global integer sequence. New migration filenames use NNNN_<module>_<description>.sql; the coordinator allocates globally unique NNNN values under the advisory transaction lock. History records module_key, global version, migration name, checksum and applied time. Dependencies between modules are explicit, validated as an acyclic graph before any migration write, and execution follows global numeric order subject to dependency validation. Module teams own their migration content but cannot allocate local/reused version numbers or apply migrations independently. The coordinator alone executes and records migrations. ADR-0009 is Proposed; runner/history changes and PostgreSQL 18 Linux/Windows disposable integration evidence are required before the first business migration.
