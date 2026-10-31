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

The current `src/MDGsm.*` projects are the existing preparation shell, not the final module topology. They must not be mistaken for proof that the modular target has been implemented. Migration from the shell to this topology is a controlled architecture task and must be covered by build and dependency tests; no business implementation is authorized by creating this target specification.

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

The module directories, ADRs, contracts documentation, CI workflow, and preview UI are preparation artifacts. They do not constitute implemented Persons, IdentityAccess, database migrations, runtime contracts, or G1 approval.
