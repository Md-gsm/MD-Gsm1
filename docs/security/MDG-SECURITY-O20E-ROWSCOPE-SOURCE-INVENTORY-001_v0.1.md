# O20-E Reporting RowScope — Source-Path Inventory

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 |
| Version / Date | 0.1 / 2026-11-22 |
| Status | REPOSITORY DISCOVERY SNAPSHOT — SOURCE ROWSCOPE CONSUMERS NOT YET AVAILABLE; FOLLOW-UP REQUIRED |
| Repository | Md-gsm/MD-Gsm1 |
| Inspected commit | 46b2412e6cf73e185e8e3eee7a258065db0091e6 |
| Related design | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v0.1 |
| Related policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.2 |
| Scope | Existing module topology, API endpoints, published source contracts and present report/export surfaces relevant to Branch/Warehouse RowScope |

## 1. Purpose and limits

This inventory identifies the current executable source/query paths that could require Organization RowScope. It is a repository snapshot, not a proof that future reports, exports or endpoints are secure. It does not replace the required inventory at each subsequent source-module implementation SHA.

The tree was inspected at the exact commit above. The executable module directories present under src/Modules are Accounting, Finance, IdentityAccess, Persons and RepairOperations. There is no executable Organization, Inventory or Reporting module directory at this commit. Inventory design artifacts exist under docs/modules/inventory, but these documents are not Inventory runtime or query endpoints.

## 2. Current API host paths

The API composition root is src/Host/Api/Program.cs. The route registrations visible there are:

| Route | Current purpose | Branch/Warehouse row context |
|---|---|---|
| GET /health/live | Process liveness | None |
| GET /health/ready | PostgreSQL readiness | None |
| GET /api/v1/system/info | System metadata | None |
| POST /api/v1/identity/login | IdentityAccess login | None |
| POST /api/v1/identity/password/change | Own password change | None |
| POST /api/v1/identity/heartbeat | Session lease heartbeat | None |
| POST /api/v1/identity/logout | Logout/revoke session | None |
| GET /api/v2/persons/{partyId} | Protected Party detail | No BranchId or WarehouseId source context supplied by the route |

The single Party detail endpoint reads the authenticated AccountId from IAuthenticatedActorContext, checks the Technician role fail-closed pending authoritative RepairOperations assignment ownership, checks persons.read, and delegates the Party detail read to the Persons repository. The request contains a PartyId, not a BranchId or WarehouseId. This endpoint therefore does not currently demonstrate a Branch/Warehouse RowScope consumer. Any future Party-to-Branch relationship must not be inferred from the Party identifier or client request.

### Route documentation discrepancy

src/Host/Api/Endpoints/Persons/README.md and src/Host/Api/README.md describe a /api/v1/persons/{partyId} endpoint, while the inspected Program.cs registers /api/v2/persons/{partyId}. This is a documentation-to-code route-version discrepancy. It is recorded as a follow-up finding only; this O20-E inventory does not modify the endpoint or its unrelated API versioning contract.

No report preview, dashboard aggregate, print, PDF, CSV/XLSX export, generic search or reporting endpoint is registered in the inspected Program.cs.

## 3. Module and contract inventory

| Module / area | Evidence inspected | Existing source responsibility | RowScope readiness |
|---|---|---|---|
| Organization | src/Modules tree; src/Modules/README.md | Canonical organizational masters/assignments are assigned by governing architecture/policy, but no runtime module exists | No resolver or source rows |
| Inventory | docs/modules/inventory design set; src/Modules tree | Design documents identify Inventory ownership of stock facts and Organization ownership of Warehouse/Location masters; no executable Inventory module exists | No Inventory query contract or source-row implementation |
| Reporting | src/Modules tree and API route registrations | Read Model/report/query projection is an architecture target, not an executable module at this commit | No report/export consumer to inventory yet |
| Persons | src/Modules/Persons/Contracts/README.md; API Program.cs; endpoint README | Owns Party identity; current Party detail endpoint | No authoritative Branch/Warehouse context in this endpoint |
| Finance | src/Modules/Finance/Contracts/README.md | Party subledger-account identity and authenticated actor boundary; no report/query surface published in inspected contract | No Branch/Warehouse source query surface found in published contract |
| Accounting | src/Modules/Accounting/Contracts/README.md | Public posting command not yet published pending persistence/events/acceptance | No report/query source surface published |
| RepairOperations | src/Modules/RepairOperations/Contracts/README.md | Owns technician skills, capabilities, scheduling, repair assignment and case lifecycle; no runtime repair source-event contract published | Domain owner identified; authoritative runtime assignment/source rows absent |
| IdentityAccess | database/migrations/0003_identityaccess_core.sql; Contracts/README.md; PermissionCatalog.cs | Owns account, credential, session, role and permission data | It does not own or persist Organization assignment facts; no Organization resolver contract published |

## 4. Confirmed boundaries and security consequences

1. The current IdentityAccess schema contains accounts, roles, permissions, account-role relations, role-permission relations, sessions, login/recovery and security audit structures. It is not the canonical source for Account-to-Branch or Account-to-Warehouse assignment.
2. The existing Persons detail endpoint's PartyId is not a substitute for BranchId/WarehouseId and does not prove organizational assignment.
3. RepairOperations is the conceptual owner of technician-to-repair assignment, but its Contracts README explicitly says no runtime repair source-event contract is published. A Technician role or Persons work-role fact must not be used as a substitute.
4. Inventory's accepted ownership policy assigns Warehouse/Location master data to Organization and stock facts to Inventory. Inventory cannot manufacture or author Warehouse identity.
5. No executable Reporting module or reporting endpoint was found in the inspected module tree/API registration. Consequently there is no present reporting SQL path on which to run a positive RowScope test. This is absence of current executable consumer capability, not proof of secure future implementation.
6. The source modules must publish authoritative BranchId/WarehouseId row-context contracts when their reportable aggregates are implemented. Reporting cannot infer source context from report parameters, role names, PartyId, Employee/Employment or the caller's selected filter.

## 5. O20-E implications

| Requirement | Snapshot result |
|---|---|
| Organization master/resolver runtime | Absent; remains gated by G1 |
| Account-to-Branch / Account-to-Warehouse assignment runtime | Absent |
| Inventory source query paths | No executable Inventory module found |
| Reporting read model/query paths | No executable Reporting module or endpoint found |
| Current Party detail source path | Exists; no Branch/Warehouse context exposed |
| Repair technician assignment source | Owner identified, runtime contract absent |
| Report/print/export route inventory | No such routes registered in Program.cs |
| Positive/negative PostgreSQL RowScope test | Not applicable to absent consumer/runtime; NOT RUN |
| ReportingReadOnly RowScope grant | Must remain absent/disabled |
| O20-E | OPEN |

## 6. Required update trigger

Repeat and extend this inventory when any of the following is proposed:
- Organization G1 package publishes its reviewed resolver/assignment contracts;
- Inventory adds executable stock/source query contracts;
- Reporting or another consumer adds list, search, detail, aggregate, dashboard, print or export paths;
- Persons, Finance, Accounting or RepairOperations adds reportable rows with BranchId/WarehouseId context;
- any database view, materialized projection or ReportingReadOnly grant is proposed.

For each new surface, the inventory must record exact route/query name, source aggregate owner, source of BranchId/WarehouseId, permission key, assignment check, SQL predicate location, count/aggregate/pagination behavior, export worker revalidation, database identity/object privileges, tests and exact source/test SHA.

## 7. Disposition

This snapshot closes discovery of the currently visible API and module topology only. It does not close PD-ORG-12 for future consumers and does not prove any runtime RowScope behavior. No implementation, DDL, migration, grant, reporting view, data change or production operation was performed.

O20-E remains OPEN. Authorization must remain fail-closed until Organization G1, the authoritative source contracts and positive/negative PostgreSQL evidence are complete.

**END — MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 v0.1**
