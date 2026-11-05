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

The four Contracts projects are compiled boundary artifacts. Finance has begun its first isolated runtime slice: a Finance-owned Party subledger account provisioner and PostgreSQL migration. The Finance runtime references only Finance.Contracts and the published Persons.Contracts; it does not expose an API or connect to the Host. Persons, IdentityAccess, and RepairOperations remain without complete business-module runtime implementations.

The Persons domain/application/infrastructure files introduced on the isolated PR 30/32 stack currently reside under `src/BuildingBlocks/MDGsm.*` as a transitional implementation location. They are not generic shared BuildingBlocks and this physical location does not satisfy the final module boundary. ADR-0014 defines the target ownership and requires a coordinated project/namespace/DI/migration-resource/test relocation to `src/Modules/Persons/{Domain,Application,Infrastructure}` before G1. Do not treat the source-tree move as complete merely because the Contracts project exists.

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

Persons has a partial, isolated domain and PostgreSQL persistence implementation on the PR 30/32 stack, but it is not yet relocated into its final business-module projects and has no operational API or connected Desktop workflow. Finance has a limited internal runtime for idempotent Party-account provisioning, but no posting/ledger workflow or Host integration. IdentityAccess business authorization is not wired to Persons. The current dashboard Persons panel is a synthetic UI preview only. These slices do not authorize production deployment or set g1Authorized=true; remaining G1 prerequisites must still be met.
