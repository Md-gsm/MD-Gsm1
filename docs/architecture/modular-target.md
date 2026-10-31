# Modular target architecture — MD-Gsm

**Repository authority:** `Md-gsm/MD-Gsm1` (the only active repository). No parallel `md-gsm` repository is an active project target.

## Target layout

```text
src/
  Modules/
    Persons/
      Domain/
      Application/
      Infrastructure/
      Contracts/
    IdentityAccess/
      Domain/
      Application/
      Infrastructure/
      Contracts/
    RepairOperations/
      Domain/
      Application/
      Infrastructure/
      Contracts/
    Finance/
      Domain/
      Application/
      Infrastructure/
      Contracts/
  Host/
    Api/
    Desktop/
```

The current `src/MDGsm.*` projects are still the host/shared-layer preparation shell; the physical relocation of those projects into `src/Host/*` and `src/BuildingBlocks/*` has not been completed. The four module Contracts assemblies are now separate .NET projects under `src/Modules/*/Contracts`, included in `MDGsm.sln`, and referenced by the current API composition project. Each exposes only a contract-version marker; no business DTO or behavior has been added. The Host and BuildingBlocks directories remain documented placeholders until the controlled physical relocation is completed and validated by build/dependency tests.

## Ownership boundaries

- Persons: PartyId, natural/legal party identity, contact methods, and technician work-role identity.
- RepairOperations: technician skills, capabilities, scheduling, and assignment.
- Finance/Payroll: salary, commission, payroll and financial records.
- IdentityAccess: opaque access-token lifecycle, account/session state, authentication and authorization.
- Each module publishes versioned Contracts. Consumers depend on Contracts, not another module's internal Domain or Infrastructure implementation.
- Cross-module database atomicity follows ADR-0005.
- Access-token behavior follows ADR-0006.

## Platform baseline

- PostgreSQL 18.6 is the V1 database baseline.
- Windows is a required first-class validation target, including PostgreSQL 18 integration validation on Windows.
- Default phone region is IR (+98); digit normalization and domestic mobile normalization follow ADR-0004.
- Secrets are supplied through protected environment/configuration facilities and must never enter this public repository.

## Explicit non-claims

Only the four version-marker Contracts assemblies are compiled module runtime artifacts at this stage. There are no Persons/IdentityAccess business entities, use cases, persistence mappings, module APIs, business database migrations, or G1 approval.
