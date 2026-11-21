# Organization Module — Branch/Warehouse and Account Assignment Logical Design
| Field | Value |
|---|---|
| Document ID | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 |
| Version / Date | 0.1 / 2026-11-21 |
| Status | LOGICAL DESIGN DRAFT — OWNER POLICY ACCEPTED; MODULE G1 AUTHORIZATION AND PHYSICAL DESIGN PENDING |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.2 |
| Scope | Organization ownership needed to support O20-E Reporting RowScope |

## 1. Purpose and decision status

This design translates the owner-accepted assignment policy into a bounded Organization module contract. It does not authorize implementation. The repository's module topology policy states that modules are introduced only after G1 authorization; no Organization runtime module exists in the inspected PR #86 tree. Therefore this document remains a logical design draft until the module's G1 authorization, detailed physical design and independent review are complete.

## 2. Exclusive ownership

Organization owns:
- Organization and OrganizationUnit master facts;
- Employee and Employment master facts;
- Branch master facts and Branch lifecycle;
- Warehouse master facts and the Warehouse-to-Branch relationship;
- the Warehouse → Section → Shelf → Bin/StorageLocation hierarchy;
- explicit AccountBranchAssignment and AccountWarehouseAssignment facts and their lifecycle.

Organization does not own:
- Account, credential, session, role or permission grant (IdentityAccess);
- Party identity/contact/business roles (Persons);
- products, variants, UOM or tracking policy (Catalog);
- stock, movement, reservation, custody, valuation or stock projections (Inventory);
- repair technician skill/technical assignment (RepairOperations).

Cross-module references are opaque identifiers and versioned contracts. Do not introduce cross-module direct table writes.

## 3. Logical concepts

| Concept | Owner | Logical responsibility |
|---|---|---|
| Organization | Organization | The business organization context; no multi-company behavior is enabled by this design |
| OrganizationUnit | Organization | Organizational grouping where explicitly modeled |
| Branch | Organization | A branch master with active/inactive lifecycle and stable BranchId |
| Warehouse | Organization | A warehouse master with stable WarehouseId and authoritative Branch relationship |
| StorageLocation | Organization | Physical hierarchy below Warehouse; stable location identity and parent relationship |
| AccountBranchAssignment | Organization | Explicit, auditable assignment of opaque AccountId to BranchId |
| AccountWarehouseAssignment | Organization | Explicit, auditable assignment of opaque AccountId to WarehouseId |
| Assignment lifecycle evidence | Organization | Who authorized/changed/revoked the assignment, when, why, and its version |

No physical table names, schema names, FK declarations, SQL types or migration sequence are approved here. The logical concepts do not prescribe whether assignments are represented as separate aggregates, temporal rows or append-only events plus current projections.

## 4. Owner-accepted policy invariants

1. Organization is the sole owner of Account-to-Branch and Account-to-Warehouse assignment facts.
2. Employment does not imply authorization. An explicit active assignment is required.
3. Multiple active Branch assignments and multiple active Warehouse assignments may coexist for an account.
4. Branch assignment does not imply access to any Warehouse. Warehouse access requires an explicit Warehouse assignment.
5. An assignment references AccountId as an opaque IdentityAccess identifier. Organization must not create or mutate accounts or credentials.
6. Every assignment is individually auditable and explicitly revocable.
7. No resource or assignment may be inferred from request values, role names, employee status, stale claims or cached results.

## 5. Proposed module contracts

These are logical contract responsibilities, not published C# interfaces or approved method signatures.

### 5.1 Organization-owned command boundary
- CreateBranch / ChangeBranchStatus
- CreateWarehouse / ChangeWarehouseBranch / ChangeWarehouseStatus
- CreateAccountBranchAssignment / RevokeAccountBranchAssignment
- CreateAccountWarehouseAssignment / RevokeAccountWarehouseAssignment

Every command requires authenticated actor context, an Organization permission decision, reason where required by policy, concurrency/version protection and audit evidence. The precise permission catalog and approval separation must be settled in the G1 detailed design. Self-assignment and caller-controlled actor identity must be denied.

### 5.2 Organization-owned read/resolution boundary
- ResolveAccountBranchAssignments(AccountId, evaluation instant)
- ResolveAccountWarehouseAssignments(AccountId, evaluation instant)
- ResolveActiveBranch(BranchId)
- ResolveActiveWarehouseAndBranch(WarehouseId)
- ResolveResourceScope(AccountId, resource type, resource identifier, evaluation instant)

Resolution must return a typed result distinguishing AuthorizedContext, Denied and Unavailable. Only AuthorizedContext can proceed. Missing/inactive/revoked/ambiguous/expired resource or assignment, timeout, dependency failure and data-integrity failure all fail closed.

### 5.3 Consumer responsibilities
- IdentityAccess authenticates the opaque session and supplies the server-validated AccountId. It continues to own permission grants and role checks.
- A protected read is allowed only when the required IdentityAccess permission and the Organization assignment predicate both pass.
- Reporting/query services obtain authoritative row context from the owning business module and apply RowScope in the database query before materialization.
- Inventory consumes Warehouse/StorageLocation contracts and references; it does not own or mutate Organization masters.
- No consumer may reach into Organization tables or maintain a competing assignment cache.

## 6. Authorization predicate

For a Warehouse-scoped resource, all of the following must be true:
1. The session resolves to an enabled AccountId.
2. IdentityAccess grants the required permission.
3. Organization confirms the Warehouse exists and is active.
4. Organization confirms the Warehouse's current Branch relationship is valid and active.
5. Organization confirms an explicit active AccountWarehouseAssignment for the authenticated AccountId and requested WarehouseId.
6. The source business module confirms the row's authoritative Warehouse context.
7. The SQL predicate constrains the query to the authorized WarehouseId set before data is materialized.

A Branch assignment alone cannot satisfy item 5. A request-supplied WarehouseId is only a selector. The actual row's scope must be obtained from its authoritative source.

For Branch-scoped data, the same rule applies to explicit AccountBranchAssignment. A Branch grant must not silently expand into Warehouse grants.

## 7. Query-path coverage

The same source-level RowScope predicate must protect:
- list and paginated list;
- search and autocomplete;
- detail and nested detail;
- dashboard counters and aggregates;
- report preview and generated reports;
- print and PDF generation;
- CSV/XLSX and other exports;
- API endpoints and bulk reads.

Filtering after materialization, client-only filtering, or applying scope only to the detail endpoint is prohibited. Count, total, pagination and aggregate queries must use the same predicate to prevent metadata leakage.

## 8. Consistency and revocation requirements

The physical design must specify the authorization linearization point. Assignment revocation or Warehouse deactivation concurrent with a protected write must not permit a write after the revocation boundary. Candidate mechanisms (serializable transaction, row lock, version check or equivalent) must be selected and tested in the detailed design; this document does not select one.

For reads and exports, the detailed design must specify whether the authorization decision is snapshot-bound to query execution or revalidated for long-running exports. No stale-cache fallback is permitted. Audit evidence must identify actor, subject AccountId, assignment/resource IDs, action, outcome, reason/correlation and policy version without leaking secrets.

## 9. Security and privacy requirements

- Server-derived AccountId only; never trust AccountId from payload.
- No cross-module direct writes or cross-module FK assumption without approved architecture.
- Deny by default; resolver Unavailable is not a permissive fallback.
- Avoid distinguishable responses that reveal whether an out-of-scope resource exists, subject to the API's error contract.
- Prevent IDOR through nested IDs, bulk identifiers, export filters and alternate endpoint paths.
- Avoid PII in logs; audit identifiers and correlation only.
- Ensure report totals/counts/aggregates do not disclose out-of-scope rows.
- Ensure assignment administration is permission-protected, audited and resistant to self-escalation.

## 10. Required acceptance tests

The implementation gate must include .NET unit/contract tests and real PostgreSQL integration tests for:
1. active Branch assignment permits only that Branch scope;
2. Branch assignment without Warehouse assignment denies Warehouse rows;
3. active Warehouse assignment permits only explicitly assigned Warehouse rows;
4. multiple assignments produce the exact authorized set and no broader union;
5. Employment without assignment denies;
6. revoked assignment denies immediately at the defined linearization point;
7. inactive Branch/Warehouse denies;
8. Warehouse-to-Branch mismatch or invalid relationship denies;
9. request-supplied AccountId/BranchId/WarehouseId cannot elevate access;
10. list/search/detail/dashboard/report/print/export/count/aggregate paths have identical RowScope;
11. pagination totals and aggregates exclude out-of-scope rows;
12. bulk mixed-scope reads/writes do not leak or partially authorize;
13. resolver timeout, DB outage, ambiguity and data-integrity error fail closed;
14. concurrent revocation and protected writes obey the chosen serialization rule;
15. cross-branch/cross-warehouse IDOR and export leakage probes fail;
16. no direct reporting_ro base-table access bypasses the approved read boundary.

All tests must run against the exact final PR head SHA and produce retained evidence. Test names, fixtures and SQL must be added only after physical model and G1 implementation authorization are approved.

## 11. Unresolved detailed-design decisions

The following are intentionally not invented:
- Branch/Warehouse required attributes, naming/numbering, uniqueness and lifecycle transition rules;
- whether one Warehouse may ever belong to more than one Branch (the current model implies one authoritative Branch relationship, but the physical cardinality must be confirmed against the master model);
- assignment effective-date semantics, history retention and whether reactivation creates a new assignment identity;
- command approval/separation-of-duties policy and exact permission keys;
- audit event schema and transaction/outbox boundary;
- ID representation and physical persistence schema;
- resolver API shape, transaction consistency and export revalidation strategy;
- how every reportable source module supplies its authoritative row context.

## 12. Gate disposition

| Gate | Status |
|---|---|
| Owner of Branch/Warehouse and account assignments | OWNER-ACCEPTED |
| Four assignment policy rules | OWNER-ACCEPTED |
| Logical Organization boundary | DOCUMENTED |
| Detailed domain/physical design | OPEN |
| Organization G1 authorization | NOT GRANTED / NOT VERIFIED |
| Runtime module and resolver | NOT IMPLEMENTED |
| Reporting RowScope SQL enforcement | NOT IMPLEMENTED |
| Positive/negative PostgreSQL evidence | NOT RUN |
| O20-E | OPEN |
| Overall O-20 | OPEN |

This document is a logical design artifact only. It does not authorize code, DDL, migrations, grants, role creation, reporting access, production changes or Go-Live.
