# Modular target architecture — MD-Gsm

**Repository authority:** `Md-gsm/MD-Gsm1` is the single active repository. No parallel `md-gsm` repository is part of this project.

## Physical source topology

The actual tree must not be confused with the intended target layout:

```text
src/
  BuildingBlocks/
    MDGsm.Domain/Persons/                 # transitional domain implementation
    MDGsm.Application/Persons/            # transitional application implementation
    MDGsm.Infrastructure/Persons/         # transitional repository/validator
    MDGsm.Infrastructure/IdentityAccess/  # IdentityAccess persistence
    MDGsm.Client/
  Host/Api/
  Host/Desktop/
  Modules/
    Persons/Contracts/                    # real Contracts project
    Persons/Domain/                       # README marker only
    Persons/Application/                  # README marker only
    Persons/Infrastructure/               # README marker only
    IdentityAccess/Domain/
    IdentityAccess/Application/
    IdentityAccess/Contracts/
    Finance/Contracts/
    Finance/Infrastructure/               # narrow runtime slice
    RepairOperations/Contracts/           # boundary marker only
```

The Persons module internal implementation remains transitional under BuildingBlocks; the Modules Persons Domain/Application/Infrastructure folders are README boundary markers, not relocated source. IdentityAccess persistence remains in BuildingBlocks. Finance has Contracts and a narrow runtime provisioner; RepairOperations currently has Contracts only. The target topology is not a statement of current physical placement.
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

## Current implementation status (main, 2026-11-08)

The topology and ownership rules above describe the target boundaries; the following records the actual implementation without changing those boundaries.

- **Persons:** partial business implementation exists. The shared BuildingBlocks transition placement currently contains the Party domain/application/infrastructure implementation; a protected `GET /api/v2/persons/{partyId}` detail API is present, with PostgreSQL-backed persistence and sensitive-field permission enforcement. Persons business migration(s) are present in the application-owned migration runner. Create/search APIs, complete Contact Point lifecycle, full operational UI, temporary-data cleanup runtime and merge execution are not complete.
- **IdentityAccess:** server-side security/session runtime is present. HTTP login and password-change paths have been implemented and covered by disposable PostgreSQL/API integration tests; HTTP login remains feature-gated and disabled by default. Production bootstrap, final admin recovery, production listener/TLS rollout and independent security acceptance remain open.
- **Contracts:** four separately compiled Contracts projects are present and included in the solution/API composition. Their existence does not imply complete business implementations in RepairOperations or Finance.
- **Persistence validation:** current main CI validated the application migration runner against disposable PostgreSQL 18 on Linux and Windows. No production/real-data database migration is claimed.
- **Desktop:** the preview dashboard/Persons panel still uses synthetic preview data; it is not the complete operational Persons UI and does not prove end-to-end write flows.

## Explicit non-claims

This repository is **not** a fully implemented ERP, and Persons/IdentityAccess are not complete or G1-authorized. The presence of a partial API, runtime components, business migrations or Contracts projects must not be described as completion of the whole module. `g1Authorized=false` remains in force. No production authentication deployment, production data migration, or activation of Persons is authorized by this status description.

## Current main integration status after PR #50

PR #50 was reviewed and merged into main at `2754e261d05f1e0410de51dbfbd5909ad638eb33`; PR #51 is the subsequent evidence reconciliation and current main is `f011c94d52bc06c7e213abb6e480180e9ea73f40`.

- Main now contains a partial Persons temporary-workflow persistence/retention slice: encrypted payload storage, bounded expiry/renewal, purge worker and alert adapter.
- Main now contains a partial transactional Party-merge coordinator with a Finance participant for Finance-owned Party subledger account references.
- Finance account identity provisioning and versioned Persons/Finance contracts are integrated; Finance runtime remains a separate project and consumes published contracts.
- Migrations `0006_finance_party_accounts.sql`, `0007_persons_temporary_workflows.sql`, and `0008_persons_party_merge.sql` follow the accepted 0001–0005 baseline. The duplicate national-identifier migration and obsolete intermediate row-version migration from PR #47 were not imported.
- The integration preserves main's newer Persons Contracts 2.0 API surface, sensitive-data authorization, IdentityAccess persistence and host composition; no main-only files were removed.

These are partial runtime slices, not complete Persons or Finance modules. AC-31..AC-45 remain `UNMAPPED` in the executable acceptance map; some focused tests exist, but they do not prove the complete acceptance criteria. PostgreSQL validation is limited to disposable CI databases. PR-head Persons gates run `42408907287`, CodeQL run `42408907310`, Windows Preview Installer run `42408907249`, and Charter Gate run `42409265387` succeeded on PR head `3c113e77169c8b6e1ab882df2ef49d2c39f0222c`. PR #51 reconciled evidence and pushed current main SHA `f011c94d52bc06c7e213abb6e480180e9ea73f40`; push-triggered CI passed: Persons Gates 13/13, CodeQL and Charter Gate (15/15 check-runs). Windows Preview Installer did not run on this SHA. No production migration, G1 activation, or change to `g1Authorized=false` is authorized.
