# O20-E Data Ownership Resolution — Consolidated Owner Policy Successor

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 |
| Version / Date | 0.7 / 2026-11-23 |
| Supersedes | v0.6 for the consolidated policy text below; prior versions remain historical |
| Status | OWNER POLICY ACCEPTED WITHIN STATED SCOPE; TECHNICAL CONTRACTS / IMPLEMENTATION / LEGAL OPERATIONS / G1 OPEN |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |
| Controlling owner decision | MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.6 |
| Owner decision record | PR #92 comment 7094120301 |

## 1. Purpose and precedence

This successor consolidates the accepted O20-E ownership, assignment, Product Owner, report-access and retention rules in one current policy artifact. It corrects conflicting historical phrases without rewriting prior versions. Where a historical section says all actors are prohibited from self-assignment, or describes Product Owner transfer/replacement, the normative rules in this successor control.

The installation-created Product Owner principal remains immutable and non-transferable. Recovery can restore only the same AccountId/principal. No owner successor, transfer or replacement is permitted. If safe recovery of that exact principal cannot be proved, owner-only privileges fail closed.

## 2. Canonical ownership

| Fact | Sole owner |
|---|---|
| Account, Credential, Session, Role, Permission grant | IdentityAccess |
| Installation identifier and bootstrap trust boundary | Platform |
| Product Owner binding policy | Product Owner; technical binding implementation ownership requires IdentityAccess/Platform designation |
| Branch/Warehouse and their lifecycle/relationship history | Organization |
| AccountBranchAssignment / AccountWarehouseAssignment | Organization |
| Stock, movement, reservation, custody, valuation and stock projection | Inventory |
| Canonical shared AuditEvent / SecurityEvent | AuditSecurity |

Organization stores opaque AccountId references only. It must not create, authenticate, disable or mutate IdentityAccess accounts, credentials, sessions, roles or grants. IdentityAccess must not create or infer Organization assignments. Inventory consumes Organization-owned warehouse/location references and does not mutate the Organization masters.

## 3. Assignment and Product Owner rules

1. Employment alone grants no access. An explicit active assignment and the required IdentityAccess permission are both necessary.
2. Branch assignment does not imply access to a Warehouse. Warehouse access requires an explicit assignment to that exact WarehouseId.
3. Multiple active Branch and/or Warehouse assignments may coexist; every assignment is individually identifiable, auditable and explicitly revocable.
4. A Warehouse has exactly one authoritative current Branch relationship. Reassignment is a separately authorized, reasoned and audited command; relationship history is retained. Existing Warehouse assignments must not be silently broadened, copied or inferred from the new Branch.
5. Assignment activation is immediate on approved server-side commit. Future-dated activation and automatic assignment expiry are not enabled. Revocation is terminal; a later grant creates a new AssignmentId and history.
6. **Self-assignment is prohibited for every actor except the designated Product Owner.** The Product Owner may create, change or revoke the Product Owner's own Branch/Warehouse assignments and those of any other account.
7. For any actor other than the designated Product Owner, every assignment create/change/revoke operation requires a second distinct authenticated approver before effect. The approver must be neither the initiating actor nor the subject AccountId.
8. **The designated Product Owner is exempt from the second-approver requirement** for Organization Branch/Warehouse assignment create/change/revoke, including self-assignment. This is direct owner execution, not self-approval.
9. Product Owner authority is derived only from the trusted immutable installation-scoped owner binding. It cannot be inferred from role, username, request field, client claim, GitHub identity, local OS administration or ordinary System Manager powers.
10. The Product Owner exception removes only the second-person approval requirement for these Organization assignment commands. It does not bypass authentication, exact resource validity, explicit assignment rules, IdentityAccess permission where required, concurrency/version checks, idempotency, immutable history, audit or source-module RowScope.
11. The Product Owner identity cannot be transferred, replaced, deleted or disabled by ordinary management. Recovery preserves the exact AccountId. If continuity cannot be proved, Product Owner authority is unavailable and owner-only operations deny.

## 4. Effective access and reporting

The controlling predicate is:

`EffectiveAccess = Permission ∩ OrganizationScope ∩ DataClassification`

A role alone never grants global visibility. The requester must have the operation permission, belong to the explicit scope of the data and be permitted for its classification.

Explicit scope dimensions:
- `LegalEntityScope`
- `BranchScope`
- `WarehouseScope`
- `DepartmentScope`
- `AssignedRecordScope` where applicable.

An empty scope means Deny, not All. Scope from one module does not automatically propagate to another. Backend queries must apply source-authoritative RowScope before materialization for list, search, detail, count, pagination, aggregate, dashboard, print, report, export and API paths. Export/print of sensitive information requires a separate permission and Audit evidence. PostgreSQL should enforce RowScope as an additional defense where practical. Aggregates and drill-downs must not reveal out-of-scope personal detail.

Initial role/data-classification boundaries:
- Ledger/financial reports: accountants, Product Owner and specifically authorized finance managers; System Manager has no automatic access from its technical role.
- User records: System Manager only the fields required for administration; credentials, hashes, secrets and bearer tokens are never readable.
- Audit reports: Product Owner and authorized security auditors; System Manager sees only technical events required for its duty.
- Inventory/warehouse reports: authorized roles constrained to WarehouseScope.
- Inventory valuation/cost: accountants and authorized finance roles; warehouse roles do not see full cost by default.
- Sales: seller, relevant manager and accountant within customer/operational scope.
- Purchasing: purchasing staff, accountant and authorized manager within purchasing/organization scope.
- Aggregate management: Product Owner and authorized managers; person-level detail requires a separate permission.

Product Owner visibility of aggregate management reports does not grant unrestricted personal-data inspection or export.

## 5. Report source ownership and future consumers

Reporting is a consumer, never the source of Organization or business master truth. Branch/Warehouse RowScope must be evaluated against authoritative source-module row context in the same authorized query boundary. A ReportingReadOnly role must not have broad base-table privileges that bypass the approved source query. No current Branch/Warehouse consumer is authorized by this policy. Until a consumer contract and SQL-level evidence exist, ReportingReadOnly remains absent and no reporting grant is created.

## 6. Retention and Legal Hold

The Product Owner's internal baseline, recorded in decision packet v0.6, is:
- commercial/financial evidence for sales, purchases, receipts, payments, inventory, valuation and ledger: 10 years from the end of the relevant financial year;
- sensitive permission, role and ownership changes: 10 years;
- purely technical/security logs not linked to a commercial/financial record: 2 years;
- evidence under judicial, audit or Legal Hold: until formal authorized release.

These periods are owner-approved internal policy baselines. They are not a blanket legal conclusion for every data class or personal record. Iran counsel/records authority must classify the actual operator, legal form, activity and every record class; confirm legal bases/triggers and approve hold authority/release, backups, replicas, exports, media and lawful disposal. Where classification or hold state is unresolved, retain and fail closed. No automatic deletion, purge, anonymization, irreversible masking, cryptographic erasure or backup pruning is enabled until the applicable legal and technical controls are approved and implemented.

The v0.6 records crosswalk remains a research input, not legal clearance. Commercial, VAT and AML scopes/triggers are not interchangeable. Do not infer a universal statutory retention period from one category.

## 7. Security, audit and failure handling

- AccountId is derived only from the current server-authenticated principal.
- No user may grant or increase their own permissions, subject only to the narrow Product Owner assignment exception in section 3.
- Sensitive management grants require an independent second person; requester and approver must differ.
- Missing, inactive, revoked, stale, ambiguous or unavailable authorization state denies.
- Owner authority and IdentityAccess permission are separate conjunctive conditions; neither substitutes for the other.
- All assignment and owner actions are authenticated, reasoned, idempotent, version-checked and audited.
- AuditSecurity remains canonical for shared audit/security events. Organization local history/outbox does not become a duplicate canonical audit ledger.
- Unavailable audit/identity/scope state must not trigger permissive fallback.

## 8. Open implementation and acceptance items

Still required before dependent runtime design or G1:
- formal IdentityAccess public contract acceptance and Organization concurrence;
- binding implementation owner designation, protected installation binding and same-AccountId recovery proof;
- approved Organization permission namespace/role mapping;
- detailed approval-pending and transaction semantics for non-owner assignment commands;
- concrete Platform/Inventory global lock order and cross-module Unit of Work participation;
- canonical AuditSecurity API/durable ACK, delivery identity, retry/quarantine and operations;
- source-module query/export RowScope contract and tests for each future consumer;
- legal/records classification and operational Legal Hold/disposal controls;
- technical owner review, exact-head validation and independent Security review.

## 9. Authority and non-authorization

This is an owner policy artifact. It does not grant G1, implement a production Organization module, authorize DDL/migrations/grants/seeds/credentials, create ReportingReadOnly, provision Workers/WORM/KMS, authorize disposal or permit Go-Live.

**Disposition:** O20-E owner policy — ACCEPTED within this scope; technical integration/implementation/legal operation/independent review — OPEN. O20-E remains OPEN; O-20 remains OPEN; G1 NOT GRANTED.

**END — MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.7**
