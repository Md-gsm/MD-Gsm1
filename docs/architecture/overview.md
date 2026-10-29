# MD-Gsm architecture baseline

## Runtime shape

```text
WPF Desktop --HTTPS/HTTP API contract--> ASP.NET Core API
                                            |
                                            v
                                      Application
                                            |
                                            v
                                          Domain

API composition root --> Infrastructure adapters --> external systems (future)
```

The arrows express allowed dependency/call direction, not a claim that all links are implemented yet. The Desktop calls the API through the typed client in `MDGsm.Client`; the API composition root registers PostgreSQL persistence when a connection string is configured. No business persistence workflows are implemented yet.

## Project dependency rules

- `MDGsm.Domain`: no project references. Owns domain rules and value types.
- `MDGsm.Application`: may reference Domain only. Owns use cases and ports/contracts.
- `MDGsm.Infrastructure`: may reference Application and implement its ports. No UI dependency.
- `MDGsm.Api`: composition root; may reference Application and Infrastructure.
- `MDGsm.Desktop`: presentation only; no project reference to Domain, Application, Infrastructure, or a database provider. It will call the API through a typed client when a real workflow is approved.
- Tests may reference the layer they test. Architecture tests should guard these rules.

## Liveness

`GET /health/live` indicates only that the API process is responding. It is not a readiness, database, migration, authentication, or accounting health check.

## Change discipline

A new dependency, persistence technology, API contract, or domain capability must be justified by an MD-Gsm requirement and tested independently. Keep secrets out of source control and configuration files. Never introduce prior-application implementation artifacts.

## Desktop API connection (stage 5)
The Desktop depends only on the small `MDGsm.Client` HTTP-client project. It does not reference API, Infrastructure, or persistence projects directly. The client calls only the existing technical system-info endpoint at this stage.
