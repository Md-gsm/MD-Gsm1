# Persistence and migration operating rules

## Current implementation

- PostgreSQL provider: Npgsql for EF Core 10.
- Context: `MDGsm.Infrastructure.Persistence.MdGsmDbContext`.
- Registration: `AddMdGsmPersistence` reads `ConnectionStrings:MDGsmDatabase`.
- No connection string means no `DbContext` is registered; the API may run for non-database endpoints.
- `/health/ready` is dependency-aware; `/health/live` is not.
- No business tables or migrations are present yet.

## Local secret configuration

Prefer .NET user-secrets for local API development, or set `ConnectionStrings__MDGsmDatabase` in the API process environment. Never commit actual credentials. Use a dedicated disposable database for integration tests; do not point tests at a personal or production database.

## Migration lifecycle (required before first schema)

1. Model an approved domain change.
2. Generate an EF Core migration with a stable, descriptive name.
3. Review both `Up` and `Down`; destructive changes require explicit review and a backup/rollback plan.
4. Apply it only to a newly created disposable test database first.
5. Verify upgrade from the previous schema and migration history, then run integration tests.
6. Produce a reviewed deployment migration script/bundle; do not apply schema migrations implicitly on API startup.
7. Back up before deployment, apply using a deployment-only identity, verify schema version, then deploy the application.
8. Runtime application credentials must not be database owners and should receive only the permissions needed by application operations.

A database connection check alone is not a schema compatibility check. Add a required migration-version check when the first migration is added.
