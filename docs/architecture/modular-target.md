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
- **Organization:** branch and warehouse master records, organizational structure, and user membership/scope. Inventory owns quantities, stock movements, and transfers.
- Each module publishes versioned Contracts. Consumers depend on Contracts, not another module's internal Domain or Infrastructure implementation.
- Cross-module database atomicity follows ADR-0005.
- Access-token behavior follows ADR-0006.

## Platform baseline

- PostgreSQL 18.6 is the V1 database baseline.
- Windows is a first-class validation target, including PostgreSQL 18 integration validation on Windows.
- Default phone region is IR (+98); digit normalization and domestic mobile normalization follow ADR-0004.
- Secrets are supplied through protected environment/configuration facilities and must never enter this public repository.

## Current implementation status (main, 2026-11-07)

The topology and ownership rules above describe the target boundaries; the following records the actual implementation without changing those boundaries.

- **Persons:** partial business implementation exists. The shared BuildingBlocks transition placement currently contains the Party domain/application/infrastructure implementation; a protected `GET /api/v1/persons/{partyId}` detail API is present, with PostgreSQL-backed persistence and sensitive-field permission enforcement. Persons business migration(s) are present in the application-owned migration runner. Create/search APIs, complete Contact Point lifecycle, full operational UI, temporary-data cleanup runtime and merge execution are not complete.
- **IdentityAccess:** server-side security/session runtime is present. HTTP login and password-change paths have been implemented and covered by disposable PostgreSQL/API integration tests; HTTP login remains feature-gated and disabled by default. Production bootstrap, final admin recovery, production listener/TLS rollout and independent security acceptance remain open.
- **Contracts:** four separately compiled Contracts projects are present and included in the solution/API composition. Their existence does not imply complete business implementations in RepairOperations or Finance.
- **Persistence validation:** current main CI validated the application migration runner against disposable PostgreSQL 18 on Linux and Windows. No production/real-data database migration is claimed.
- **Desktop:** the preview dashboard/Persons panel still uses synthetic preview data; it is not the complete operational Persons UI and does not prove end-to-end write flows.

## Explicit non-claims

This repository is **not** a fully implemented ERP, and Persons/IdentityAccess are not complete or G1-authorized. The presence of a partial API, runtime components, business migrations or Contracts projects must not be described as completion of the whole module. `g1Authorized=false` remains in force. No production authentication deployment, production data migration, or activation of Persons is authorized by this status description.

## Draft PR #50 branch delta (unmerged)

The following is present only on the main-based reconstruction branch until PR #50 is accepted and merged:

- A partial Persons temporary-workflow persistence/retention slice: encrypted payload storage, bounded expiry/renewal, purge worker and alert adapter.
- A partial transactional Party-merge coordinator with a Finance participant for Finance-owned Party subledger account references.
- Finance account identity provisioning and versioned Persons/Finance contracts; the Finance runtime is a separate project and consumes only published contracts.
- Three new migrations are sequenced after the accepted main baseline: `0006_finance_party_accounts.sql`, `0007_persons_temporary_workflows.sql`, and `0008_persons_party_merge.sql`. The duplicate national-identifier migration and obsolete intermediate row-version migration from PR #47 were not imported.
- The branch retains main's newer Persons Contracts 2.0 API surface, sensitive-data authorization, IdentityAccess persistence and host composition rather than replacing them with older PR #47 versions. No main-only files are removed.

These are partial runtime slices, not complete Persons or Finance modules. AC-31..AC-45 remain unmapped as runtime acceptance criteria; PostgreSQL validation is limited to disposable CI databases. No production migration, G1 activation, or change to `g1Authorized=false` is authorized by this branch.

Main validation snapshot for commit `194720455d01a5f88b1996ed9b100b7bc7ec363b`:
- Persons Module Gates run `42381030785`: 13/13 jobs successful, including disposable PostgreSQL 18 Linux/Windows.
- CodeQL run `42381030784`: successful.
- Windows Preview Installer run `42381030763`: successful.
- Project Charter Owner Gate run `42381030735`: successful.

These are CI/installer results only; they do not replace independent review, operational acceptance or owner authorization.
