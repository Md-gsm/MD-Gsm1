# O20-E Data Ownership Resolution — Owner-Accepted Assignment Policy
| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 |
| Version / Date | 0.2 / 2026-11-21 |
| Supersedes | v0.1 proposal |
| Status | OWNER-ACCEPTED LOGICAL OWNERSHIP / POLICY; IMPLEMENTATION NOT AUTHORIZED BY THIS DOCUMENT |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Related decisions | OWN-INV-018 v1.1; O20-E IdentityAccess scope data-owner contract |

## 1. Owner acceptance

The project owner accepted the following four rules on 2026-11-21:

1. Organization is the canonical owner of Account-to-Branch and Account-to-Warehouse assignment facts.
2. Employment alone does not grant access. An explicit, active assignment is required.
3. An account may have multiple active Branch and/or Warehouse assignments; each assignment is individually recorded and auditable.
4. Branch assignment does not imply access to every Warehouse in that Branch. Warehouse access requires an explicit Warehouse assignment.

This acceptance resolves the ownership and policy questions listed in v0.1. It does not constitute independent technical review, implementation acceptance, or production authorization.

## 2. Canonical ownership boundaries

| Fact | Owner |
|---|---|
| Organization, OrganizationUnit, Employee, Employment | Organization |
| Branch master and lifecycle | Organization |
| Warehouse master and Warehouse-to-Branch relationship | Organization |
| AccountBranchAssignment and AccountWarehouseAssignment | Organization |
| Account, Credential, Session, Role and Permission grant | IdentityAccess |
| Stock, movement, reservation, custody, valuation and stock projection | Inventory |

Organization stores opaque AccountId references only. It does not own IdentityAccess credentials, account lifecycle, role or permission grants. IdentityAccess must not author or infer Organization assignments. Inventory consumes Organization-owned Warehouse/Location references and must not mutate them.

## 3. Assignment semantics accepted

- Assignments are explicit, individually identifiable, auditable and revocable.
- Multiple simultaneous active assignments are permitted for both Branch and Warehouse.
- Employment does not create, extend or reactivate an assignment implicitly.
- A Branch assignment is not inherited by its child Warehouses.
- An active Warehouse assignment must resolve to an active Warehouse and its authoritative active Branch relationship.
- The authenticated AccountId is obtained from the server-authenticated principal. Client-supplied AccountId, BranchId or WarehouseId is never proof of authorization.
- Assignment changes, revocation and reactivation require an authorized Organization command and immutable audit evidence.
- Effective dating, concurrency/serialization semantics, historical retention and exact audit event schema remain detailed-design requirements; no behavior may be inferred beyond these accepted rules.

## 4. Runtime resolution contract

Organization must expose an authoritative server-side contract for resolving the active assignments of an authenticated AccountId and validating the current Branch/Warehouse relationship. The exact interface, persistence model, key representation, API transport and cache strategy are not approved here.

A resolver must return an explicit allow/deny/unavailable outcome. Missing, inactive, revoked, expired, ambiguous, stale, timed-out or failed resolution is Deny. No client filter, claim snapshot, stale cache, or caller-provided resource identifier can replace authoritative resolution.

IdentityAccess remains responsible for authentication and permission grants. A request is authorized only when both the required IdentityAccess permission and the Organization assignment predicate are satisfied.

## 5. O20-E reporting enforcement requirements

After the Organization runtime contract is implemented and independently reviewed:
- Apply RowScope in the source SQL/query before materialization for list, search, detail, dashboard, report, print, export and API paths.
- Use one shared, server-enforced predicate across equivalent read paths.
- A Branch-only assignment must not expose Warehouse-scoped rows.
- A Warehouse assignment must be validated against current active Warehouse and Branch master facts.
- Resolve source-record scope through the source domain owner; report parameters are filters, not proof.
- Revalidate write scope at the transaction serialization/commit boundary.
- Prove positive and negative cases, multiple assignments, revoked/expired assignments, inactive resources, ambiguous/unavailable resolver, cross-branch and cross-warehouse leakage, pagination, aggregates, exports and concurrent revocation.
- Do not grant reporting_ro broad base-table access. Reporting access remains disabled until approved views/query boundaries and exact-SHA PostgreSQL evidence pass.

## 6. Repository implementation boundary

The inspected PR #86 tree at the acceptance-time baseline contains no Organization runtime module or canonical Organization persistence/resolver implementation. Consequently:
- No guessed table, schema, FK, migration, repository, endpoint, role grant or reporting view is introduced.
- This document records the accepted logical ownership/policy only.
- Organization module foundation, detailed logical/physical design, migration plan and runtime implementation must be delivered as a separately scoped, reviewed implementation package under the existing project architecture and repository governance.
- Until that package exists and passes tests, O20-E remains OPEN and reporting authorization remains fail-closed.

## 7. Gate disposition

| Gate | Status |
|---|---|
| Branch/Warehouse master owner = Organization | RESOLVED |
| Account-to-Branch / Account-to-Warehouse assignment owner = Organization | OWNER-ACCEPTED |
| Explicit assignment; employment does not imply access | OWNER-ACCEPTED |
| Multiple active assignments permitted | OWNER-ACCEPTED |
| Branch assignment does not imply Warehouse access | OWNER-ACCEPTED |
| Organization runtime resolver | NOT IMPLEMENTED |
| Reporting SQL RowScope positive/negative evidence | NOT RUN |
| O20-E | OPEN |
| Overall O-20 | OPEN |

No runtime code, DDL, migration, database grant, reporting permission or production operation is authorized or performed by this document.
