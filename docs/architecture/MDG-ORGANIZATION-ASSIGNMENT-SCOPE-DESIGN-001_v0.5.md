# Organization Module — Branch/Warehouse and Account Assignment Logical Design
| Field | Value |
|---|---|
| Document ID | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 |
| Version / Date | 0.5 / 2026-11-22 |
| Status | OWNER POLICIES THROUGH OD-05 ACCEPTED; LOGICAL DESIGN UPDATED; TECHNICAL CONTRACTS/PROOFS AND G1 STILL OPEN |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.5 |
| Scope | Organization ownership needed to support O20-E Reporting RowScope |

## 1. Purpose and decision status

This successor carries forward the logical Organization boundary and incorporates the additional owner decisions recorded in O20-E Data Ownership v0.3. It does not authorize implementation. The repository's module topology policy states that modules are introduced only after G1 authorization; no Organization runtime module exists in the inspected PR #86 tree. Therefore this document remains a logical design draft until the module's G1 authorization, detailed physical design and independent review are complete.

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
8. Every Warehouse has exactly one authoritative current Branch relationship; reassignment is a distinct authorized/audited command and relationship history is retained.
9. BranchCode is unique across Organization; WarehouseCode is unique within its current Branch; both are required, normalized before uniqueness comparison and not silently reusable after archive. The exact normalization algorithm remains open.
10. Branch/Warehouse lifecycle states are ACTIVE, INACTIVE and ARCHIVED; archived masters are not physically deleted.
11. Assignment activation is immediate on approved server-side commit; no future-dated activation or automatic expiry is enabled; revocation is terminal and a later grant receives a new AssignmentId.
12. Self-assignment is prohibited for all actors, including the designated Product Owner. Non-owner assignment create/change/revoke requires a distinct second approver. The designated Product Owner may perform those operations for any other account without a second approver; this exception does not bypass authentication, audit, explicit assignment rules or IdentityAccess ownership.
13. Warehouse scope requires the exact active AccountWarehouseAssignment plus active Warehouse and its active current Branch relationship; a separate AccountBranchAssignment to that parent Branch is not additionally required. IdentityAccess permission remains mandatory.

## 5. Proposed module contracts

These are logical contract responsibilities, not published C# interfaces or approved method signatures.

### 5.1 Organization-owned command boundary
- CreateBranch / ChangeBranchStatus
- CreateWarehouse / ChangeWarehouseBranch / ChangeWarehouseStatus
- CreateAccountBranchAssignment / RevokeAccountBranchAssignment
- CreateAccountWarehouseAssignment / RevokeAccountWarehouseAssignment

Every command requires authenticated actor context, the exact Organization authorization decision, a reason, idempotency, concurrency/version protection and audit evidence. Self-assignment is prohibited for every actor, including the Product Owner. Non-owner assignment create/change/revoke commands require a distinct second approver before effect; the designated Product Owner may perform those commands for any other account without second-person approval. Product Owner authority must be resolved by trusted server-side binding and is not inferred from the super_admin role, client claim, username convention or GitHub identity.

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
3. Organization confirms the Warehouse exists, is active and has exactly one valid active current Branch relationship.
4. Organization confirms an explicit active AccountWarehouseAssignment for the authenticated AccountId and that exact WarehouseId.
5. The source business module confirms the row's authoritative Warehouse context.
6. The SQL predicate constrains the query to the authorized WarehouseId set before data is materialized.

A separate AccountBranchAssignment to the parent Branch is not additionally required. A Branch assignment alone cannot satisfy the Warehouse assignment requirement. A request-supplied WarehouseId is only a selector; actual row scope must be obtained from its authoritative source.

For Branch-scoped data, an explicit active AccountBranchAssignment is required. A Branch grant must never silently expand into Warehouse grants.

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

The owner has accepted the decisions listed in O20-E Data Ownership v0.3. The remaining items are intentionally not invented:
- the effect of Warehouse-to-Branch reassignment on existing AccountWarehouseAssignments;
- the exact BranchCode/WarehouseCode normalization algorithm, editability and collision/rename workflow;
- detailed ACTIVE/INACTIVE/ARCHIVED transition prerequisites and dependent-resource handling;
- the approval-pending workflow, trusted Product Owner principal binding/recovery, and second-approver evidence/transaction semantics;
- exact Organization permission keys/role mapping and the versioned IdentityAccess public principal/permission contract;
- AuditSecurity event/outbox boundary, durable acknowledgement, retry/quarantine and retention;
- ID representation/generation policy and physical persistence constraints;
- cross-module Unit-of-Work and lock-order rules;
- assignment-history retention/legal hold;
- resolver API shape, transaction consistency, export revalidation and all future source-module RowScope contracts.

## 12. Gate disposition

| Gate | Status |
|---|---|
| Owner of Branch/Warehouse and account assignments | OWNER-ACCEPTED |
| Base assignment policy and additional owner decisions in v0.3 | OWNER-ACCEPTED |
| Logical Organization boundary | DOCUMENTED |
| Detailed domain/physical design | UPDATED; physical and cross-contract details OPEN |
| Organization G1 authorization | NOT GRANTED / NOT VERIFIED |
| Runtime module and resolver | NOT IMPLEMENTED |
| Reporting RowScope SQL enforcement | NOT IMPLEMENTED |
| Positive/negative PostgreSQL evidence | NOT RUN |
| O20-E | OPEN |
| Overall O-20 | OPEN |

This document is a logical design artifact only. It does not authorize code, DDL, migrations, grants, role creation, reporting access, production changes or Go-Live.


**END — MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.2**


## 13. Controlled design closure addendum — 2026-11-22 (v0.3)

### 13.1 Corrected Product Owner rule

The v0.2 wording that prohibited self-assignment for every actor is superseded. A non-owner cannot assign or expand access for the actor's own AccountId and requires a distinct second approver for each create/change/revoke operation. The designated Product Owner may assign, change or revoke the Product Owner's own assignments and any other account's assignments without second-person approval. The owner exception is established only by a protected server-side principal binding; it does not derive from role or client input. All actions remain authenticated, reasoned, idempotent, concurrency-checked and audited.

### 13.2 Warehouse reassignment semantics

An AccountWarehouseAssignment is bound to the immutable WarehouseId, not to a BranchId. Reassigning that Warehouse does not create, copy, broaden or revoke assignments and does not consult AccountBranchAssignment. Reassignment is permitted only while the Warehouse is INACTIVE. While inactive, all Warehouse RowScope is denied. After a successful reassignment and explicit reactivation, the same still-active exact-Warehouse assignments become effective again, subject to IdentityAccess permission and the new current Branch being ACTIVE. If business policy later requires branch-change revocation, that must be a separate explicit revocation command; it is never inferred.

### 13.3 Business-code normalization decision proposal

The deterministic v1 proposal is: Unicode NFC; trim leading/trailing whitespace; reject empty values, control/format characters and any remaining whitespace; preserve punctuation and non-ASCII letters/digits; compare the resulting UTF-8 value bytewise using deterministic C collation. No Persian/Arabic digit folding, diacritic stripping, punctuation removal, transliteration or locale-dependent case conversion occurs. Codes are case-sensitive in v1. A change to these equivalence rules requires a versioned data migration and collision report. This is a technical proposal requiring owner acceptance before DDL.

BranchCode is unique across all Branch lifecycle states. WarehouseCode is unique by (current BranchId, normalized WarehouseCode) and archived code reservations remain retained for the Branch in which they were reserved. A Warehouse reassignment must check uniqueness in the destination Branch before commit. Codes are immutable after first assignment/reference; before first reference, a rename may occur only through a reasoned audited command. Archived code reservations are never silently released.

### 13.4 Lifecycle transition rules proposal

ACTIVE may transition to INACTIVE; INACTIVE may return to ACTIVE; ACTIVE or INACTIVE may transition to ARCHIVED only through the archive prerequisites below. ARCHIVED is terminal. No physical deletion is permitted.

- Branch INACTIVE: first all child Warehouses must be INACTIVE or ARCHIVED; Branch RowScope is denied immediately. Existing assignments are retained but grant no access while the Branch is inactive. Reactivation restores only assignments that remain explicitly ACTIVE.
- Warehouse INACTIVE: Warehouse RowScope is denied immediately; its assignments and relationship history remain unchanged. Reactivation restores only still-active exact-Warehouse assignments.
- Warehouse ARCHIVED: requires INACTIVE state, no active AccountWarehouseAssignments (they must be explicitly revoked), no active child StorageLocation, and no unresolved dependent operation. Assignment/event history remains retained.
- Branch ARCHIVED: requires INACTIVE state, all child Warehouses ARCHIVED, no active AccountBranchAssignments (explicitly revoked), and no unresolved dependent operation. History and code reservation remain retained.

Any dependent resource or pending approval makes the transition fail closed. Reactivation is not permitted after ARCHIVED. Exact storage-location dependency validation is a future Organization physical-model requirement and cannot be skipped when that hierarchy is introduced.

### 13.5 Approval and command transaction semantics

A non-owner command is first persisted as PENDING_APPROVAL with canonical request digest, actor, subject, resource, reason, idempotency key, expected aggregate version and expiry. It has no authorization effect. A distinct approver approves or rejects it. Approval revalidates actor/session status, approver/session status, subject, resource state, permissions, Product Owner status, expected versions and idempotency under the final command transaction. The approved state transition, immutable assignment event and local audit-intent outbox are committed atomically. Stale versions or changed preconditions reject without partial state. Product Owner commands use DIRECT_OWNER and bypass only the pending/second-approver stage.

The approval-request expiry interval and retention remain policy configuration decisions; no future-dated assignment activation is introduced. No request-supplied AccountId can replace the server-derived actor.

### 13.6 Identifier, Unit-of-Work and retention boundaries

UUID is the proposed storage representation, consistent with current IdentityAccess AccountId. Organization BranchId/WarehouseId/AssignmentId generation version and authority remain Platform decisions; no global UUIDv7 rule is inferred. Cross-module foreign keys into private IdentityAccess tables remain prohibited. No distributed transaction is assumed. Each Organization mutation is one local PostgreSQL transaction containing command receipt, domain state, assignment event and audit-intent outbox. Global lock order, Organization lock ranks and cross-module lock interaction remain blocked on the Platform Lock-Order Registry; do not implement until published and tested. Assignment-history retention/legal hold requires an explicit records-owner policy and remains open.

**Status:** logical rules 13.1–13.5 are a coherent design successor. Code normalization, lifecycle and approval details are proposed and require accountable technical/owner acceptance; cross-module contracts, lock order, retention and independent review remain open. G1 is NOT GRANTED.

**END — MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.3**


## 14. Owner acceptance addendum — 2026-11-22 (v0.4)

The four decisions in `MDG-SECURITY-O20E-O20F-OWNER-DECISION-ACCEPTANCE-001 v0.1` are OWNER-ACCEPTED. This addendum supersedes the proposal/open-acceptance wording in sections 11 and 13 only to the extent described below:

- OD-01: BranchCode/WarehouseCode canonicalization is accepted as Unicode NFC, outer-whitespace trim, reject empty/control/format/remaining whitespace, preserve punctuation/non-ASCII, case-sensitive UTF-8 bytewise comparison under PostgreSQL C collation; no digit folding, transliteration, diacritic stripping, punctuation removal or locale-sensitive case conversion. .NET/PostgreSQL equivalence and collision proof remains required.
- OD-02: AccountWarehouseAssignment remains attached to immutable WarehouseId across Branch reassignment; reassignment only while Warehouse INACTIVE; no implicit grant/revoke/copy/broadening; inactive scope denied; explicit reactivation restores only still-active exact-Warehouse assignments subject to active current Branch and IdentityAccess permission.
- OD-03: ARCHIVED is terminal and cannot be reactivated; physical deletion remains prohibited. Detailed archive prerequisites and dependency checks remain subject to technical/records review.

No adjacent proposal is accepted by implication. Trusted owner binding, IdentityAccess contract publication, Platform lock order/ID policy, records retention, AuditSecurity durable ACK, query contracts, concurrency design and all PostgreSQL proofs remain open. G1 is NOT GRANTED; no runtime, DDL, migration, grant, reporting access or production change is authorized.

**END — logical design v0.4 owner acceptance addendum**


## 15. Records-retention policy addendum — 2026-11-22 (v0.5)

OD-05 is OWNER-ACCEPTED as recorded in `MDG-SECURITY-O20E-O20F-OWNER-DECISION-ACCEPTANCE-001 v0.2` and the normative policy `MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.1`:

- Organization assignment and Branch/Warehouse relationship history is retained by default; physical deletion is not an ordinary lifecycle operation.
- Automatic expiry, purge, hard-delete, irreversible anonymization/masking and cryptographic erasure remain disabled until an approved lawful retention schedule and disposal process exist.
- Disposal requires expiry of the applicable retention period, absence of an active Legal Hold, and documented authorization from the accountable records/legal authority.
- A current or unresolved Legal Hold blocks disposal.
- The retention duration is UNSET; no statutory period or jurisdiction-specific schedule is inferred.

This addendum supersedes the earlier logical-design statement that the retention policy itself was wholly unaccepted. The retention implementation details remain open: hold authority/scope/release, jurisdictional schedule, privacy obligations, handling of replicas/backups/exports, disposal separation of duties, technical enforcement and proofs. Canonical shared audit events remain owned by AuditSecurity; Organization's domain history must not become a duplicate canonical audit ledger.

No purge worker, deletion endpoint, DDL, migration, runtime module, grant or production operation is authorized. G1 remains NOT GRANTED and O20-E remains OPEN.

**END — logical design v0.5 records-retention addendum**
