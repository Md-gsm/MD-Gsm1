# Decision 0002 — PostgreSQL and EF Core

- Status: accepted for the initial persistence foundation; review before production.
- Date: 2026-10-27.

## Decision

Use PostgreSQL as the database engine and Entity Framework Core with the Npgsql provider for the server-side persistence adapter. The WPF desktop client must not connect directly to the database; only the API/server process owns database credentials and database connections.

## Configuration

The API reads the connection string named `MDGsmDatabase` from standard .NET configuration. Local development may use user-secrets or environment variables such as `ConnectionStrings__MDGsmDatabase`. Secrets must not be committed to source control, checked into `appsettings.json`, placed in screenshots, or written to logs. Do not expose PostgreSQL to the public internet.

## Migration policy

Schema changes must be represented by reviewed, ordered, version-controlled EF Core migrations. Applying migrations is a deliberate deployment operation, not an automatic side effect of API startup. Production deployment must back up first, apply migrations with a deployment identity, verify the resulting schema, and only then start/enable the app. Application runtime credentials should not have schema-owner privileges. Never use `EnsureCreated` as a replacement for migrations, and never drop or recreate a database as an update strategy.

No business entities or schema migrations are included in this stage. The DbContext is intentionally empty until the first approved domain model exists.

## Readiness

`/health/live` reports process liveness only. `/health/ready` reports HTTP 503 if the database connection is not configured or cannot be reached, and HTTP 200 only when an explicitly configured PostgreSQL connection is reachable. Reachability does not prove that required schema migrations have been applied; a schema-version gate must be added when the first migration is introduced.

## Consequences

- PostgreSQL is the selected relational store for this project.
- Database access is server-side, behind the API.
- Local startup remains possible without a configured database, but readiness correctly remains negative.
- Schema versioning and release gates are required before business data is introduced.
