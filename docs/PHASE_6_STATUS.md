# Stage 6 — Persistence foundation

## Implemented

- Decision recorded: PostgreSQL + EF Core/Npgsql, server-side database access only.
- Added the empty `MdGsmDbContext` as the persistence boundary; no speculative business tables were created.
- Added conditional dependency injection registration using the `MDGsmDatabase` connection string.
- Added `/health/ready`, distinct from process liveness, returning 503 when database configuration/reachability is missing.
- Added automated tests for persistence registration with and without an explicit connection string.
- Documented secret handling and the required reviewed/versioned migration lifecycle.

## Not implemented

No database instance is created or modified; no real connection string is included; no entities, migrations, accounting schema, identity tables, or financial workflows exist. Readiness checks connection reachability only, not schema compatibility.

## Validation status

Archive integrity is checked when packaging. This revision must be restored, built, and tested on the user's Windows/.NET 10 environment before this stage is accepted. NuGet package restore is required for EF Core 10 and Npgsql EF Core 10.


## Windows validation follow-up

The first Windows build exposed missing explicit test-project references for `ConfigurationBuilder` and in-memory configuration. The test project now explicitly references `Microsoft.Extensions.Configuration` and `Microsoft.Extensions.Configuration.Memory` version 10.0.0. Re-run restore, build, and tests on Windows; this revision has not yet been validated on the user machine.


### Windows validation follow-up — Fix2

The test project now uses the `Microsoft.AspNetCore.App` shared-framework reference for `Microsoft.Extensions.Configuration` and its in-memory configuration provider. The attempted `Microsoft.Extensions.Configuration.Memory` package reference was invalid (NuGet NU1101); it has been removed. This revision still requires restore/build/test validation on Windows.
