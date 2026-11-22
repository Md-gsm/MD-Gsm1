# O20-E Reporting RowScope — Exact Source-Path Inventory

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 |
| Version / Date | 0.7 / 2026-11-22 |
| Supersedes | v0.6 |
| Supersedes | v0.5 |
| Supersedes | v0.4 |
| Status | EXACT-HEAD SNAPSHOT REVALIDATED — CURRENT BRANCH/WAREHOUSE CONSUMERS ABSENT; FUTURE COVERAGE OPEN |
| Repository / Branch | Md-gsm/MD-Gsm1 / security/o20-o21-final-closure |
| Inspected source commit | `5eb769cda992d05a784bc9bebc4fe9e30084f81a` |
| API composition root blob | `98eb172eb2572f69095f2a54168f74bcd91305b6` |
| Related design | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.5 |
| Owner policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.5 |

## 1. Evidence boundary

This inventory is a recursive Git-tree and API composition-root source inspection at one exact branch commit. It is not a deployed-environment inspection, database catalog audit, or proof of positive/negative RowScope enforcement.

## 2. Executable module topology

The immediate module directories under `src/Modules` are:
- Accounting
- Finance
- IdentityAccess
- Persons
- RepairOperations

There is no executable Organization, Inventory, Reporting or AuditSecurity module at this inspected head. The module README still gates introduction of new executable modules on G1 authorization.

## 3. API route inventory

The current API composition root registers exactly these route groups:

| Route | Purpose | Branch/Warehouse source-row scope |
|---|---|---|
| GET /health/live | Process liveness | None |
| GET /health/ready | PostgreSQL readiness | None |
| GET /api/v1/system/info | System metadata | None |
| POST /api/v1/identity/login | IdentityAccess login | None |
| POST /api/v1/identity/password/change | Own password change | None |
| POST /api/v1/identity/heartbeat | Session heartbeat | None |
| POST /api/v1/identity/logout | Current session logout/revocation | None |
| GET /api/v2/persons/{partyId} | Protected Party detail | No BranchId/WarehouseId source context |

No Branch/Warehouse report, dashboard aggregate, Inventory query, print/PDF, CSV/XLSX export, generic report search, scheduled export worker or RowScope consumer is registered.

The Persons detail route derives AccountId from the validated server-side actor context, checks IdentityAccess `persons.read`, denies Technician until its authoritative assignment source exists, and reads by PartyId. PartyId is not a BranchId/WarehouseId source identity, so this route is not an O20-E Branch/Warehouse RowScope consumer.

## 4. Current IdentityAccess permission boundary

The current branch's PermissionCatalog contains owner-approved `reports.read`, `reports.export`, `reports.print` and dashboard keys, and preserves the distinct `persons.sensitive.read` super_admin-only gate. This establishes permission names and role grants only. It does not implement a report query or Branch/Warehouse row predicate.

The Contracts project exposes request-time and transaction-scoped authorization methods. The transaction guard is an IdentityAccess mechanism, not a RowScope resolver. No Organization permission key or Org runtime has been introduced.

## 5. RowScope disposition

| Requirement | Current exact-source result |
|---|---|
| Organization module / assignment resolver | Absent |
| Inventory source query consumers | None found |
| Reporting module / report endpoints | None found |
| Print/export/scheduled worker paths | None found |
| Current Persons detail path | Exists; no BranchId/WarehouseId authoritative source |
| Approved report permission names | Present in catalog; no RowScope consumer |
| Positive/negative RowScope PostgreSQL test | NOT APPLICABLE to absent consumer; NOT RUN |
| ReportingReadOnly role / grant / view | Must remain absent and unprovisioned |
| Future read/query/report/export/worker coverage | OPEN; re-inventory every time a consumer is introduced |

For each future consumer, acceptance requires a complete source inventory: authoritative BranchId/WarehouseId, data-owning module, required IdentityAccess permission, exact active Organization assignment predicate, SQL predicate before materialization, detail/list/search/count/aggregate/pagination, print/export, asynchronous worker revalidation, effective DB object privileges, positive/negative tests and exact source/test checksums.

Client-side filtering or a `reports.*` permission alone never satisfies RowScope. Warehouse visibility requires the accepted exact active Warehouse assignment and valid active Warehouse/current Branch. Branch assignment is not an implied Warehouse grant.

## 6. Non-authorization

This inventory creates no Organization/Inventory/Reporting/AuditSecurity module, route, DDL, migration, resolver, view, seed, role, grant, ReportingReadOnly or production access.

**Disposition:** current source topology and absence of Branch/Warehouse consumers revalidated at the exact inspected commit; future query/worker RowScope remains OPEN. O20-E OPEN; G1 NOT GRANTED.

**END — MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 v0.5**


## 7. Exact-head source topology confirmation — 2026-11-22 (v0.6)

The recursive Git tree at `5096fe9693a65213321f1b123341a220a9826a7d` confirms the executable modules remain Accounting, Finance, IdentityAccess, Persons and RepairOperations. There is no executable Organization, Inventory, Reporting or AuditSecurity module and no Branch/Warehouse report, query or export endpoint in the API composition root. The current `Program.cs` blob is `98eb172eb2572f69095f2a54168f74bcd91305b6`.

The permission catalog's `reports.*` entries and the public IdentityAccess permission contract do not themselves create data RowScope. The future consumer remains gated: exact resource-owner-provided BranchId/WarehouseId, organization assignment contract, SQL predicate before materialization and full read/list/search/count/aggregate/print/export/worker tests are required when a real consumer is introduced.

**Disposition:** current source topology and absence of RowScope consumers revalidated at the specified exact source SHA. No ReportingReadOnly role/grant/view or production consumer exists. Future RowScope coverage OPEN; O20-E OPEN; G1 NOT GRANTED.

**END — O20-E RowScope inventory v0.6**


## 8. Exact current-head confirmation — 2026-11-22 (v0.7)

At source head `5eb769cda992d05a784bc9bebc4fe9e30084f81a`, the recursive Git tree and `src/Host/Api/Program.cs` were re-inspected. The executable modules remain Accounting, Finance, IdentityAccess, Persons and RepairOperations. Organization, Inventory and Reporting runtime modules remain absent. The API composition root still registers only health/system, IdentityAccess login/password/heartbeat/logout and Persons Party detail routes; no Branch/Warehouse report, query, count, export or worker route is registered.

The current IdentityAccess PermissionCatalog contains the approved `reports.read`, `reports.export` and `reports.print` entries, but this is only a permission vocabulary/grant matrix. It does not create an Organization resource resolver or SQL RowScope query consumer.

**Disposition:** exact-head current source inventory PASS; current RowScope consumers absent, so runtime RowScope is NOT APPLICABLE/NOT RUN. This is not a pass for a future Organization/Inventory/Reporting route. ReportingReadOnly remains absent/unprovisioned; O20-E OPEN; G1 NOT GRANTED.

**END — O20-E RowScope Source Inventory v0.7**
