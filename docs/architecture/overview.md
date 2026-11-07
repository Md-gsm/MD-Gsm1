# MD-Gsm architecture baseline

## Runtime shape

```text
WPF Desktop --HTTP API contract--> ASP.NET Core API
                                         |
                                         v
                                  Application
                                         |
                                         v
                                       Domain

API composition root --> Infrastructure adapters --> PostgreSQL / external systems
```

The arrows express allowed dependency/call direction, not a claim that every business workflow is complete. Desktop uses the typed client in `MDGsm.Client`; it must not connect directly to PostgreSQL. The API composition root registers persistence and server-side authorization/session components.

## Project dependency rules

- `MDGsm.Domain`: no project references. Owns domain rules and value types.
- `MDGsm.Application`: may reference Domain only. Owns use cases and ports/contracts.
- `MDGsm.Infrastructure`: may reference Application and implement its ports. No UI dependency.
- `MDGsm.Api`: composition root; may reference Application and Infrastructure.
- `MDGsm.Desktop`: presentation only; no project reference to Domain, Application, Infrastructure, or a database provider. It calls the API through a typed client.
- Tests may reference the layer they test. Architecture tests guard these rules.

## Current implementation snapshot — 2026-11-07

- Persons has a partial PostgreSQL-backed implementation and a protected detail endpoint `GET /api/v1/persons/{partyId}`; sensitive fields require server-side permission enforcement.
- Persons create/search endpoints, complete Contact Point lifecycle, full operational Desktop forms, temporary-data cleanup runtime and Person merge execution remain incomplete.
- IdentityAccess has server-side session/security runtime and tested HTTP login/password-change paths. HTTP login is feature-gated and defaults off; production bootstrap/recovery and production listener/TLS deployment remain open.
- Persons business migration(s) and the application-owned DbMigrator exist. Current CI validates them against disposable PostgreSQL 18 Linux/Windows databases only; no production/real-data database was migrated.
- The dashboard/Persons preview UI continues to use synthetic data and is not the operational Persons workflow.

## Liveness and readiness

`GET /health/live` indicates only that the API process is responding. It is not a readiness, migration, authentication, or accounting health check. `GET /health/ready` checks configured database reachability; it does not certify schema compatibility or production security.

## Change discipline

A new dependency, persistence technology, API contract, or domain capability must be justified by an MD-Gsm requirement and tested independently. Keep secrets out of source control and configuration files. Never introduce prior-application implementation artifacts.

## Desktop API connection (stage 5)

The Desktop depends only on the small `MDGsm.Client` HTTP-client project. The technical system-info endpoint remains available for connection checks; later business endpoints must use the approved API contracts and server-side authorization.
