# O20-E Data Ownership Resolution — Owner-Accepted Assignment Policy

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 |
| Version / Date | 0.3 / 2026-11-22 |
| Supersedes | v0.2 |
| Status | OWNER-ACCEPTED POLICY; DETAILED IMPLEMENTATION AND G1 NOT AUTHORIZED |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Related decisions | OWN-INV-018 v1.1; IdentityAccess permission-matrix approval; O20-E logical and physical design |

## 1. Accepted ownership and assignment rules carried forward

The following v0.2 decisions remain accepted and unchanged:

1. Organization is the canonical owner of Account-to-Branch and Account-to-Warehouse assignment facts.
2. Employment alone does not grant access. An explicit, active assignment is required.
3. An account may have multiple active Branch and/or Warehouse assignments; each assignment is individually recorded and auditable.
4. Branch assignment does not imply access to every Warehouse in that Branch. Warehouse access requires an explicit assignment to that exact Warehouse.
5. Organization stores opaque AccountId references only. It does not own or mutate IdentityAccess accounts, credentials, sessions, roles or permission grants.
6. Every assignment is individually identifiable, auditable and explicitly revocable.
7. IdentityAccess permission and Organization assignment are separate, conjunctive authorization conditions. Neither replaces the other.

## 2. Additional owner decisions accepted on 2026-11-22

### PD-ORG-01 — Warehouse-to-Branch cardinality and reassignment

- Each Warehouse has exactly one authoritative current Branch relationship.
- A Warehouse may be reassigned only through a separately authorized, server-side, reasoned and audited Organization command.
- The prior/current Branch relationship history must be retained; reassignment must not rewrite or erase historical relationship facts.
- The treatment of existing AccountWarehouseAssignments after a Warehouse is reassigned remains a bounded detailed-design item. The implementation must not silently broaden access or infer a new assignment from the new Branch relationship.

### PD-ORG-02 — Branch and Warehouse business codes

- BranchCode is unique across the Organization.
- WarehouseCode is unique within its current Branch.
- Codes are required, nonblank and normalized before uniqueness comparison.
- An archived BranchCode or WarehouseCode is not silently reusable.
- The exact normalization algorithm, editability rules, and collision/rename procedure remain detailed-design requirements; this decision does not invent a Persian/Latin normalization algorithm.

### PD-ORG-03 — Branch and Warehouse lifecycle

The accepted lifecycle vocabulary is:

- ACTIVE
- INACTIVE
- ARCHIVED

Archived Branches and Warehouses are not physically deleted. An inactive or archived resource grants no current RowScope. Exact transition prerequisites, effects on dependent Warehouses/assignments and historical reporting remain detailed-design requirements.

### PD-ORG-04 — Assignment effective lifecycle

- A newly approved assignment becomes effective immediately at its server-side commit.
- Future-dated assignment activation and automatic assignment expiry are not enabled by this decision.
- Revocation is terminal for that AssignmentId.
- A later grant after revocation creates a new AssignmentId and new lifecycle history; a revoked assignment is not reactivated in place.

### PD-ORG-05 — Assignment administration, self-assignment and second-person approval

- Self-assignment is prohibited: an actor cannot create or expand a Branch/Warehouse assignment whose subject AccountId is the actor's own AccountId. This restriction also applies to the designated Product Owner.
- For an actor other than the designated Product Owner, every assignment create/change/revoke command requires approval by a second, distinct authenticated person before the assignment state transition takes effect. The approver cannot be the initiating actor or the subject account.
- **The designated Product Owner is explicitly exempt from the second-person approval requirement.** The Product Owner may create, change or revoke Branch/Warehouse assignments for any other account without a second person's approval.
- The Product Owner exception is limited to Organization assignment administration. It does not permit account impersonation, credential/session access, password reset, role/permission mutation in IdentityAccess, or bypass of the accepted Branch/Warehouse assignment rules.
- Product Owner actions remain authenticated, reasoned, idempotent, server-authorized and fully auditable. The exception removes only the second-approver requirement; it does not remove the audit trail or any resource-validity condition.
- Product Owner authority must be bound to a specific trusted server-side principal through a controlled owner-identity registration process. It must not be inferred from a request value, a client claim, a username convention, a GitHub login or a broad `super_admin` role. The exact runtime binding and recovery/control process remain a G1 technical prerequisite.
- No actor may approve their own operation. The Product Owner's authorized exception is direct execution without a second approver, not self-approval.

### PD-ORG-13 — Warehouse scope composition

For a Warehouse-scoped resource, all of the following are sufficient and required:

1. The authenticated account has the required IdentityAccess permission.
2. Organization confirms the Warehouse is active and has exactly one valid, active current Branch relationship.
3. Organization confirms an explicit active AccountWarehouseAssignment for that exact WarehouseId.
4. The source module supplies the authoritative WarehouseId for the actual row, and SQL RowScope is applied before materialization.

A separate AccountBranchAssignment to the Warehouse's parent Branch is **not** an additional prerequisite. An AccountBranchAssignment alone still never grants Warehouse access. Any additional IdentityAccess scope can only narrow the result; it cannot broaden it.

## 3. Canonical ownership boundaries

| Fact | Owner |
|---|---|
| Organization, OrganizationUnit, Employee, Employment | Organization |
| Branch master and lifecycle | Organization |
| Warehouse master, one-current-Branch relationship and relationship history | Organization |
| AccountBranchAssignment and AccountWarehouseAssignment | Organization |
| Account, Credential, Session, Role and Permission grant | IdentityAccess |
| Stock, movement, reservation, custody, valuation and stock projection | Inventory |
| Canonical shared AuditEvent and SecurityEvent | AuditSecurity |

Organization must not create or mutate IdentityAccess account/credential/security data. IdentityAccess must not author or infer Organization assignments. Inventory consumes Organization-owned Warehouse/Location references and does not mutate those masters.

## 4. Authorization and fail-closed requirements

- The AccountId is taken only from the current server-authenticated principal.
- A client-supplied AccountId, BranchId, WarehouseId, role name, Employment fact, stale claim or cached assignment is not proof of authorization.
- IdentityAccess permission and Organization assignment are both required.
- Missing/inactive/revoked/ambiguous/stale/unavailable authorization context denies access. No fallback to `all`, a broad role name, a client filter or stale cache is permitted.
- Warehouse assignments are evaluated against the Warehouse's current active Branch relationship; the Branch assignment is not consulted as a hidden extra grant or prerequisite.
- Reporting applies the authoritative RowScope in the source SQL/query before materialization for list, search, detail, count, pagination, aggregates, dashboard, print, report, export and API paths.
- Export workers revalidate the current principal, permission and assignment before artifact delivery.
- ReportingReadOnly must not receive broad direct base-table access that bypasses the approved source query boundary.

## 5. Detailed design items that remain open

This policy decision does not resolve:
- the exact handling of existing AccountWarehouseAssignments after Warehouse-to-Branch reassignment;
- code normalization algorithm, editability and rename/collision workflow;
- exact ACTIVE/INACTIVE/ARCHIVED transition preconditions and dependency handling;
- the precise approval-pending workflow, transaction boundary, concurrency behavior and evidence for the second approver;
- trusted Product Owner principal binding, enrollment, recovery and control evidence;
- exact Organization permission keys, role mapping and the published IdentityAccess principal/permission contract;
- AuditSecurity event/outbox contract, durable acknowledgement, retry/quarantine and retention;
- Organization identifier generation/version policy;
- cross-module Unit-of-Work and global lock-order rules;
- assignment-history retention/legal hold;
- future reportable source-module RowScope contracts and query/export coverage.

These are not permission to invent implementation details. Each must be closed by its accountable owner or technical authority before the dependent runtime design is finalized.

## 6. Authority and non-authorization

This document records owner policy only. It does not constitute independent review, G1 authorization, implementation acceptance, migration authorization, database grant, operational release or Go-Live approval.

No Organization runtime, schema, table, DDL, migration, seed, role, permission grant, resolver, endpoint, reporting view, production identity or production database change is authorized or performed by this document.

**O20-E remains OPEN. Overall O-20 remains OPEN.**

**END — MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.3**
