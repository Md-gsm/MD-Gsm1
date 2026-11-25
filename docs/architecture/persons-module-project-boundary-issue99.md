# Persons module-owned project boundary (Issue #99)

The Persons implementation is owned by three independent assemblies:

- `MDGsm.Persons.Domain` — `src/Modules/Persons/Domain`; no project references.
- `MDGsm.Persons.Application` — `src/Modules/Persons/Application`; references only Persons.Domain and Persons.Contracts.
- `MDGsm.Persons.Infrastructure` — `src/Modules/Persons/Infrastructure`; references only Persons.Domain, Persons.Application and Persons.Contracts plus platform/provider packages.

The shared BuildingBlocks projects retain only generic foundations and cross-cutting infrastructure. They do not contain Persons implementation classes. API-only adapters that translate authenticated IdentityAccess permissions into Persons Application ports live in `src/Host/Api/Persons`, at the composition boundary; Persons.Infrastructure never references IdentityAccess internals.

The API Host composes `AddMdGsmPersistence`, `AddPersonsPersistence` and the IdentityAccess-to-Persons authorization adapters. Desktop and Host Client consume published Persons.Contracts only. The DbMigrator composes both the shared Infrastructure migration assembly and Persons.Infrastructure. Persons Infrastructure embeds the existing Persons SQL files using the exact historical logical names `MDGsm.Migrations.{Filename}{Extension}`; the shared DatabaseMigrationRunner accepts an explicit list of additional assemblies and validates the combined migration sequence.

No migration file was renamed, rewritten or newly allocated by this relocation. Migration execution remains limited to the approved disposable CI runner. Search quotas/abuse controls, complete Party/Contact/Address lifecycle history and G1 authorization remain independent open gates.
