# Modular target architecture — MD-Gsm

**Repository authority:** `Md-gsm/MD-Gsm1` is the single active repository. No parallel `md-gsm` repository is part of this project.

## Physical source topology

The host/shared-project relocation is now reflected in the repository tree:

```text
src/
  BuildingBlocks/
    MDGsm.Domain/
    MDGsm.Application/
    MDGsm.Infrastructure/
    MDGsm.Client/
  Host/
    Api/
    Desktop/
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
```

The six technical application projects are physically located under `src/BuildingBlocks/*` and `src/Host/*`; their solution entries and project references use those paths. The four module Contracts assemblies are separate .NET projects under `src/Modules/*/Contracts`, included in `MDGsm.sln`, and referenced by the API composition project. The architecture gate checks the complete project inventory, dependency graph, Contracts isolation/version markers, and absence of the former flat `src/MDGsm.*` roots.

The four Contracts projects currently expose version markers only. They are compiled boundary artifacts, not implementations of the Persons, IdentityAccess, RepairOperations, or Finance business modules.

## Module ownership boundaries

- **Persons:** PartyId, natural/legal party identity, contact methods, and technician work-role identity.
- **RepairOperations:** technician skills, capabilities, scheduling, and assignment.
- **Finance/Payroll:** salary, commission, payroll and financial records.
- **IdentityAccess:** opaque access-token lifecycle, account/session state, authentication and authorization.
- Each module publishes versioned Contracts. Consumers depend on Contracts, not another module's internal Domain or Infrastructure implementation.
- Cross-module database atomicity follows ADR-0005.
- Access-token behavior follows ADR-0006.

## Platform baseline

- PostgreSQL 18.6 is the V1 database baseline.
- Windows is a first-class validation target, including PostgreSQL 18 integration validation on Windows.
- Default phone region is IR (+98); digit normalization and domestic mobile normalization follow ADR-0004.
- Secrets are supplied through protected environment/configuration facilities and must never enter this public repository.

## Explicit non-claims

No Persons/IdentityAccess business entities, use cases, persistence mappings, business module APIs, business database migrations, or G1 authorization are present. The current dashboard Persons panel is a synthetic UI preview only. The relocation closes the physical Host/BuildingBlocks topology task; it does not claim that the Persons business module is implemented or that G1 can start.
