# O20-E Reporting RowScope — Source-Path Inventory

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 |
| Version / Date | 0.2 / 2026-11-22 |
| Supersedes | v0.1 |
| Status | REVALIDATED REPOSITORY SNAPSHOT — CURRENT CONSUMERS ABSENT; FUTURE CONSUMER COVERAGE OPEN |
| Repository | Md-gsm/MD-Gsm1 |
| Inspected commit | `9838250e97be5f362f98e1e82a5e6508cf34cac5` |
| Related design | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v0.1 |
| Related policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.2 |

## 1. Purpose and evidence boundary

This successor re-runs the source-tree and API-composition-root discovery previously recorded in v0.1. It is a point-in-time repository inventory only. It does not prove future query-path security, runtime authorization, or a positive/negative RowScope implementation.

The exact inspected source commit is `9838250e97be5f362f98e1e82a5e6508cf34cac5`. The inventory is based on the recursive Git tree, `src/Host/Api/Program.cs`, API endpoint documentation and the published Contracts README files at that commit. No Production database or environment was queried.

## 2. Executable module topology

The immediate executable module directories under `src/Modules` at the inspected commit are:

- Accounting
- Finance
- IdentityAccess
- Persons
- RepairOperations

There is no executable Organization, Inventory or Reporting module directory. `src/Modules/README.md` continues to prohibit introducing a module before G1 authorization. Inventory and Organization documentation therefore remains design/policy evidence, not executable source or query paths.

## 3. API route inventory

The current API composition root registers:

| Route | Current purpose | Branch/Warehouse row context |
|---|---|---|
| GET `/health/live` | Process liveness | None |
| GET `/health/ready` | PostgreSQL readiness | None |
| GET `/api/v1/system/info` | System metadata | None |
| POST `/api/v1/identity/login` | IdentityAccess login | None |
| POST `/api/v1/identity/password/change` | Own password change | None |
| POST `/api/v1/identity/heartbeat` | Session lease heartbeat | None |
| POST `/api/v1/identity/logout` | Logout/current-session revocation | None |
| GET `/api/v2/persons/{partyId}` | Protected Party detail | No BranchId/WarehouseId source context |

No report preview, dashboard aggregate, print, PDF, CSV/XLSX export, generic reporting search, Inventory query, or Warehouse-scoped API route is registered in the inspected `Program.cs`.

## 4. Existing Persons detail path

The Party endpoint obtains AccountId from `IAuthenticatedActorContext`, checks the current IdentityAccess permission and denies the Technician role pending authoritative RepairOperations assignment ownership. It delegates the detail read to the Persons repository. The route identifier is PartyId; no BranchId or WarehouseId context is supplied or derivable from PartyId. This endpoint is not a current O20-E Branch/Warehouse RowScope consumer.

A future Party-to-Branch or Party-to-Warehouse relationship must be declared by its domain owner and must not be inferred from a Party identifier, request filter or role name.

## 5. Contract and module findings

| Area | Current evidence | O20-E consequence |
|---|---|---|
| Organization | No runtime module or resolver in the inspected tree | Organization G1 and implementation remain gated |
| Inventory | No executable Inventory module or published stock query contract | No current Inventory source-row query on which to execute RowScope |
| Reporting | No executable Reporting module or report/export API route | No current reporting consumer to test |
| IdentityAccess | Runtime source exists, but `src/Modules/IdentityAccess/Contracts/README.md` states that no runtime contract is published | Account identity/permission integration must use a reviewed public contract; direct private-schema access is not an acceptable substitute |
| Persons | Party identity and one protected detail endpoint | No current Branch/Warehouse source context |
| Finance / Accounting | Runtime slices/contracts exist; inspected published contracts do not expose Branch/Warehouse report query surfaces | No current RowScope consumer identified |
| RepairOperations | Domain ownership of technician assignment is recognized, but no published runtime assignment/source-row contract was found | Technician assignment cannot be used as a substitute for Organization account assignments |

## 6. Documentation/code discrepancy

At the inspected commit, `src/Host/Api/Program.cs` registers `GET /api/v2/persons/{partyId}`, while `src/Host/Api/README.md` and `src/Host/Api/Endpoints/Persons/README.md` describe `/api/v1/persons/{partyId}`. This discrepancy remains open and is not changed by this O20-E inventory because it is unrelated to Branch/Warehouse RowScope. The active route for this snapshot is the one registered in `Program.cs`.

## 7. O20-E disposition

| Requirement | Snapshot result |
|---|---|
| Organization master/assignment/resolver runtime | Absent; G1 not authorized |
| Account-to-Branch / Account-to-Warehouse assignment runtime | Absent |
| Inventory source query paths | None found |
| Reporting read-model/query paths | None found |
| Current Party detail source path | Exists; no Branch/Warehouse context |
| Report/print/export route inventory | None registered |
| Positive/negative PostgreSQL RowScope test | NOT APPLICABLE to absent runtime/consumer; NOT RUN |
| ReportingReadOnly RowScope grant | Must remain absent/disabled |
| Current-snapshot discovery | REVALIDATED at `9838250e97be5f362f98e1e82a5e6508cf34cac5` |
| Future source/consumer coverage | OPEN; re-inventory required whenever a source/query/export surface is introduced |
| O20-E | OPEN |

For every future reportable surface, the inventory must record the exact route/query, source aggregate owner, source of BranchId/WarehouseId, IdentityAccess permission, Organization assignment check, SQL predicate location, count/aggregate/pagination behavior, export-worker revalidation, database identity/object privileges, test coverage and exact source/test SHA.

This inventory does not authorize implementation, DDL, migration, grant, reporting view, runtime resolver or production operation.

**END — MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 v0.2**


## 7. Revalidation at PR #86 source snapshot

The API composition root was re-fetched at source head fe330b35b85486f0b142f03ab0f0f8b0e71ee616 (Program.cs blob 1297659e8ba4eca8459792674192896557affd31). It registers health/readiness, system info, IdentityAccess login/password/heartbeat/logout and GET /api/v2/persons/{partyId}. No Organization, Inventory, Reporting, dashboard, report, print/PDF, CSV/XLSX or export route is registered. The Persons detail endpoint has no authoritative BranchId/WarehouseId row source and is not an O20-E RowScope consumer.

src/Modules/README.md at the same PR source snapshot states modules are introduced only after G1; the executable module topology contains no Organization, Inventory or Reporting runtime module. src/Modules/IdentityAccess/Contracts/README.md explicitly states that no versioned public runtime contract is published yet. PermissionCatalog.cs contains no organization.* permission keys. The source therefore provides no current principal/permission contract or report permission grant for Organization.

This is a source inspection, not a RowScope execution proof. No positive or negative PostgreSQL RowScope test is applicable to a current reportable route because no such route exists. No reporting_ro role, view, grant or query path may be provisioned. Future RowScope acceptance must be repeated against the exact source head for each newly introduced consumer and must include all query/export surfaces listed in the logical design.

**Disposition:** snapshot inventory revalidated for the cited source head; future source inventory and positive/negative PostgreSQL tests remain OPEN / NOT RUN. This document does not authorize runtime, G1 or reporting access.

**END — MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 v0.3**
