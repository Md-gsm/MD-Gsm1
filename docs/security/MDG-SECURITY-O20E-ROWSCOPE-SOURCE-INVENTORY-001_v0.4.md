# O20-E Reporting RowScope — Exact Source-Path Inventory

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 |
| Version / Date | 0.4 / 2026-11-22 |
| Supersedes | v0.3 |
| Status | EXACT-HEAD SNAPSHOT REVALIDATED — CURRENT BRANCH/WAREHOUSE CONSUMERS ABSENT; FUTURE COVERAGE OPEN |
| Repository / Branch | Md-gsm/MD-Gsm1 / security/o20-o21-final-closure |
| Inspected commit | `3322d7738a576fc17f4b5168713dc4a68410ae87` |
| API composition root blob | `1297659e8ba4eca8459792674192896557affd31` |
| Permission catalog source | src/Modules/IdentityAccess/Domain/PermissionCatalog.cs |
| Related design | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.5 |
| Owner policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.5 |

## 1. Evidence boundary

This is a point-in-time source discovery at the exact inspected branch head. It establishes only which source modules and API routes are present at that SHA. It does not establish production runtime behavior, a positive/negative Organization RowScope implementation, or future query-path safety.

The recursive Git tree and `src/Host/Api/Program.cs` were inspected. No production database or deployed environment was queried.

## 2. Executable module topology

The immediate executable module directories under `src/Modules` are:
- Accounting
- Finance
- IdentityAccess
- Persons
- RepairOperations

There is no executable Organization, Inventory or Reporting runtime module at this head. `src/Modules/README.md` continues to gate module introduction on G1 authorization.

## 3. API composition root

The current routes registered in Program.cs are:

| Route | Purpose | Branch/Warehouse source-row context |
|---|---|---|
| GET /health/live | Process liveness | None |
| GET /health/ready | PostgreSQL readiness | None |
| GET /api/v1/system/info | System metadata | None |
| POST /api/v1/identity/login | IdentityAccess login | None |
| POST /api/v1/identity/password/change | Own password change | None |
| POST /api/v1/identity/heartbeat | Session lease heartbeat | None |
| POST /api/v1/identity/logout | Current session logout/revocation | None |
| GET /api/v2/persons/{partyId} | Protected Party detail | No BranchId/WarehouseId source context |

No Branch/Warehouse report, dashboard aggregate, Inventory query, report preview, print/PDF, CSV/XLSX export, generic reporting search or export-worker endpoint is registered.

The Persons detail path obtains AccountId from the validated server-side `IAuthenticatedActorContext`, checks IdentityAccess `persons.read`, denies Technician pending an authoritative assignment source, and loads Party detail. PartyId does not supply a BranchId/WarehouseId scope. This is not an O20-E RowScope consumer.

## 4. Updated IdentityAccess findings

The current branch's `PermissionCatalog.cs` now contains the owner-approved `reports.read`, `reports.export` and `reports.print` permission names/grants, as well as the approved dashboard subset and the separately preserved `persons.sensitive.read` control. This is permission catalog source alignment only; it does not create a Reporting query path or an Organization assignment.

`src/Modules/IdentityAccess/Contracts/README.md` still says no public runtime module-facing contract has been published. The Contracts project has login/password/session HTTP DTOs and a version marker, but no typed Organization-facing principal/permission/Product Owner-authority decision contract. The host `IAuthenticatedActorContext` and internal Application `IRolePermissionStore` do not become cross-module public interfaces by existing.

The IdentityAccess crosswalk is recorded in `MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 v0.5`.

## 5. O20-E RowScope disposition

| Requirement | Exact-head result |
|---|---|
| Organization runtime/resolver | Absent |
| Branch/Warehouse assignment runtime | Absent |
| Inventory source query consumers | None found |
| Reporting module and report/export routes | None found |
| Current Persons detail path | Exists; no Branch/Warehouse source identity |
| Permission names | Owner-approved reports.* catalog entries now source-aligned; no Organization row resolver |
| Positive/negative SQL RowScope PostgreSQL test | NOT APPLICABLE to absent consumer; NOT RUN |
| ReportingReadOnly role/grant/view | Must remain absent/unprovisioned |
| Current source discovery | REVALIDATED at exact head above |
| Future query and export surfaces | OPEN; re-inventory at every new source/route/worker |

For any future consumer, inventory must record authoritative row BranchId/WarehouseId source, module owner, required IdentityAccess permission, exact Organization assignment predicate, SQL predicate before materialization, detail/list/search/count/aggregate/pagination behavior, print/exports, worker revalidation, DB identity/object privileges, test vectors and exact source/test hashes.

No client-side filter or report permission alone satisfies RowScope. For Warehouse-scoped data, the accepted rule is exact active Warehouse assignment + active Warehouse/current Branch + IdentityAccess permission; no parent Branch assignment is an extra prerequisite.

## 6. Non-authorization

This inventory does not authorize a Reporting/Organization/Inventory module, DDL, migration, seed, resolver, route, view, role, grant, ReportingReadOnly or production data access. Future positive/negative RowScope acceptance cannot be claimed until a real source owner and query consumer exist and are tested.

**Disposition:** current exact-head discovery verified; no active Branch/Warehouse RowScope consumer exists. Future source coverage OPEN; O20-E OPEN; G1 NOT GRANTED.

**END — MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 v0.4**
