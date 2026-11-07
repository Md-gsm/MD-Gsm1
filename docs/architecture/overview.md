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
- Persons create/search endpoints, complete Contact Point lifecycle and full operational Desktop forms remain incomplete. Main now contains a partial temporary-workflow ciphertext store/retention worker and a transactional Party-merge coordinator with a Finance participant; operational alert delivery, all consumers and complete AC-31..AC-45 acceptance remain open.
- IdentityAccess has server-side session/security runtime and tested HTTP login/password-change paths. HTTP login is feature-gated and defaults off; production bootstrap/recovery and production listener/TLS deployment remain open.
- Persons business migration(s) and the application-owned DbMigrator exist. Current CI validates them against disposable PostgreSQL 18 Linux/Windows databases only; no production/real-data database was migrated.
- The dashboard/Persons preview UI continues to use synthetic data and is not the operational Persons workflow.

## Current main integration status after PR #50

PR #50 is merged into main at `2754e261d05f1e0410de51dbfbd5909ad638eb33`. The integrated change adds a limited temporary-workflow ciphertext store/retention worker, a transactional Party-merge coordinator with a Finance account participant, and a transaction-bound Finance Party-account provisioner. It preserves Persons Contracts 2.0 and uses separate versioned internal cross-module contracts. These components are partial: they do not complete Persons/Finance workflows or close AC-31..AC-45. The PR-head CI run `42408907287` and associated CodeQL, Charter Gate and Windows Preview Installer runs succeeded; the merge commit itself has not been independently re-run through CI. All database migration tests used disposable PostgreSQL 18 databases only. No production migration or G1 activation is authorized.

## Liveness and readiness

`GET /health/live` indicates only that the API process is responding. It is not a readiness, migration, authentication, or accounting health check. `GET /health/ready` checks configured database reachability; it does not certify schema compatibility or production security.

## Change discipline

A new dependency, persistence technology, API contract, or domain capability must be justified by an MD-Gsm requirement and tested independently. Keep secrets out of source control and configuration files. Never introduce prior-application implementation artifacts.

## Desktop API connection (stage 5)

The Desktop depends only on the small `MDGsm.Client` HTTP-client project. The technical system-info endpoint remains available for connection checks; later business endpoints must use the approved API contracts and server-side authorization.
