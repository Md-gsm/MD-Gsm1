# Persistence and migration operating rules

## Current implementation

- PostgreSQL provider: Npgsql for EF Core 10.
- Context: `MDGsm.Infrastructure.Persistence.MdGsmDbContext` in `src/BuildingBlocks/MDGsm.Infrastructure`.
- Registration: `AddMdGsmPersistence` reads `ConnectionStrings:MDGsmDatabase`.
- No connection string means no `DbContext` is registered; the API may run for non-database endpoints.
- `/health/ready` is dependency-aware; `/health/live` is not.
- The application-owned CLI is `tools/MDGsm.DbMigrator`; its runner is implemented in `src/BuildingBlocks/MDGsm.Infrastructure` and loads SQL migrations as embedded resources.
- The runner uses a PostgreSQL advisory transaction lock, a migration history table with SHA-256 checksums, checksum/name drift detection, and a PostgreSQL transaction for the migration batch.
- By default, the runner refuses to operate unless the target database comment contains `MDGSM_DISPOSABLE_TEST_DB`.
- `--allow-unmarked-database` additionally requires the environment variable `MDGSM_UNMARKED_DB_CONFIRMATION=APPLY-TO-NON-DISPOSABLE-DATABASE`; without it the tool exits with code 2. An explicitly approved deployment invocation may opt out using `--allow-unmarked-database`; credentials are read only from `ConnectionStrings__MDGsmDatabase` in the process environment.
- The API never applies migrations on startup. Migration execution is explicit.
- The repository contains Persons identity migrations 0002/0003 and the Finance Accounts Receivable party-account migration 0004. Finance migration 0004 creates only the lazy party-account identity table; no general ledger, chart of accounts, journal posting, payment settlement, or payroll schema is implemented.

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
