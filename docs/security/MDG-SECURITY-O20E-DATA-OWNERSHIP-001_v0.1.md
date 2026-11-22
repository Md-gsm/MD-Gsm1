# O20-E Data Ownership Resolution — Organization / Branch / Warehouse
| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 |
| Version / Date | 0.1 / 2026-11-21 |
| Status | CONTROLLED ARCHITECTURE PROPOSAL — EXISTING OWNER DECISIONS CROSSWALKED; ASSIGNMENT POLICY OWNER CONFIRMATION REQUIRED |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Related decisions | OWN-INV-018 v1.1; O20-E IdentityAccess scope data-owner contract |
| Implementation authority | NONE — no runtime, schema, migration, grant, or positive RowScope authorization |

## 1. Purpose

Resolve the previously reported uncertainty about the canonical owner of Branch and Warehouse for O20-E without inventing database objects or changing the accepted architecture master. This proposal distinguishes already-authorized ownership from the still-open account-assignment policy.

## 2. Evidence-based ownership already established

The current accepted architecture and Inventory owner decisions establish the following:

| Domain fact | Canonical owner | Evidence / boundary |
|---|---|---|
| Organization and organizational units | Organization | MDG-ARCH-MASTER-001 v3.0.3 module ownership catalogue |
| Branch master and lifecycle | Organization | Master architecture identifies Branch among Organization-owned masters |
| Warehouse master and lifecycle | Organization | OWN-INV-018 v1.1 INV-EVT-002; Inventory logical ERD v0.4 |
| Warehouse → Section → Shelf → Bin/StorageLocation hierarchy | Organization | OWN-INV-018 v1.1 INV-EVT-002 |
| Inventory stock, movements, reservations, custody, valuation and projections | Inventory | OWN-INV-018 v1.1 INV-EVT-001 and Inventory logical ERD |
| UserAccount, credentials, sessions, roles and permission grants | IdentityAccess | Master architecture module ownership catalogue |
| Employee and Employment | Organization | Master architecture module ownership catalogue |

**Finding:** Branch and Warehouse do not require a newly invented owner module. Their canonical master-data owner is the existing Organization module. Inventory consumes Organization-owned Warehouse/Location references and must not create or mutate them.

## 3. Remaining gap: principal-to-organization assignments

The reviewed owner records do not establish an authoritative runtime entity or lifecycle for:
- Account/Principal → Branch membership;
- Account/Principal → Warehouse assignment;
- effective dates, suspension, revocation and historical reassignment;
- whether membership is derived from Employment or is an independently managed access-placement relationship.

IdentityAccess remains the owner of Account and security grants, not these domain assignments. Employee/Employment ownership by Organization does not by itself prove that every account maps one-to-one to an employee, nor that employment automatically grants branch or warehouse access.

## 4. Recommended ownership proposal — requires explicit owner confirmation

The recommended boundary is to keep all organizational placement facts in Organization:

1. Organization owns Branch, Warehouse and Warehouse-to-Branch relationship.
2. Organization owns explicit AccountBranchAssignment and AccountWarehouseAssignment records as organizational placement/access facts. Each record references AccountId as an opaque external identity reference; Organization does not own credentials, account status, roles or permission grants.
3. IdentityAccess supplies the authenticated AccountId and evaluates the relevant permission grant. It does not create, update, infer or cache organizational assignments.
4. Employment may be used as an assignment eligibility input only if a separately accepted rule explicitly defines that relationship. Employment must not silently imply access.
5. Inventory owns physical stock facts and consumes authoritative Warehouse/Location references. It does not own warehouse master data or user assignment.
6. Reporting/read-model consumers must resolve scope through Organization's authoritative contract and apply the predicate in the source query before materialization.

This avoids a new cross-cutting module, avoids IdentityAccess/Organization ownership inversion, and keeps warehouse master data consistent with the already accepted Inventory boundary.

## 5. Proposed logical contract (not a physical schema)

Proposed Organization contract operations:
- ResolveActiveBranchAssignment(AccountId, BranchId, AsOfUtc)
- ResolveActiveWarehouseAssignment(AccountId, WarehouseId, AsOfUtc)
- ResolveActiveWarehouseBranch(WarehouseId, AsOfUtc)
- ResolveResourceScope(ResourceType, ResourceId, AsOfUtc)

Each result must distinguish:
- AllowedWithAuthoritativeContext;
- Denied;
- Unavailable.

Unavailable, ambiguous, inactive, revoked, expired or timed-out resolution is fail-closed. AccountId must come from the authenticated server principal, never from request payload. Client-supplied BranchId/WarehouseId is only a selector, not proof of assignment. No stale cache fallback is allowed for authorization.

No schema/table/key/FK/interface signature is approved by this proposal. Physical key representation, versioning, history, audit integration and concurrency/linearization rules require detailed design and review before implementation.

## 6. O20-E query enforcement requirements

After owner confirmation and G1/implementation authorization:
- SQL-level RowScope is applied before materialization for list, search, detail, dashboard, report, print, export and API paths.
- A Branch grant does not automatically authorize every Warehouse in that Branch unless the owner explicitly accepts that inheritance rule.
- A Warehouse grant is valid only when the Warehouse is active, its Branch relationship is authoritative, and the Account-to-Warehouse assignment is active.
- Source-record branch/warehouse context must be resolved by the owning business module; request filters and report parameters are not authoritative.
- Scope checks for writes are repeated at the transaction's serialization/commit boundary.
- Assignment revocation/reassignment races, mixed-scope bulk operations and export leakage receive explicit PostgreSQL integration tests.
- No positive reporting_ro grant or broad reporting view is enabled before these proofs pass.

## 7. Required owner decisions before design closure

| Decision | Proposed default | Status |
|---|---|---|
| Canonical owner of Branch | Organization | ESTABLISHED by v3.0.3 |
| Canonical owner of Warehouse and location hierarchy | Organization | ESTABLISHED by OWN-INV-018 |
| Owner of Account→Branch assignment | Organization | PROPOSED — owner confirmation required |
| Owner of Account→Warehouse assignment | Organization | PROPOSED — owner confirmation required |
| Employment automatically grants access | No; explicit assignment required | PROPOSED — owner confirmation required |
| Multiple active Branch assignments per account | Allow only explicit individually audited assignments | OPEN — owner confirmation required |
| Multiple active Warehouse assignments per account | Allow only explicit individually audited assignments | OPEN — owner confirmation required |
| Branch assignment implies Warehouse access | No | PROPOSED — owner confirmation required |
| Assignment lifecycle, effective dates and historical semantics | Explicit versioned lifecycle with audit | DETAILED DESIGN OPEN |

## 8. Gate disposition

- Branch owner: RESOLVED — Organization.
- Warehouse/location owner: RESOLVED — Organization.
- Account assignment owner: PROPOSED — Organization, awaiting owner confirmation.
- Authoritative runtime resolver: NOT IMPLEMENTED / NOT VERIFIED.
- Reporting RowScope positive and negative proof: NOT RUN.
- O20-E: OPEN.
- Overall O-20: OPEN.

This document does not authorize runtime implementation, DDL, migration, database grants, role creation, reporting access, or positive RowScope. Until the assignment owner and semantics are explicitly accepted and the runtime resolver is implemented and tested, reporting access remains deny-by-default.
