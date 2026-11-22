# Organization Assignment Scope — PostgreSQL Physical Design Proposal

| Field | Value |
|---|---|
| Document ID | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 |
| Version / Date | 1.0 / 2026-11-22 |
| Supersedes | v0.9 |
| Supersedes | v0.8 |
| Supersedes | v0.7 |
| Supersedes | v0.6 |
| Supersedes | v0.5 |
| Status | REVISED PHYSICAL DESIGN PROPOSAL — OWNER POLICY THROUGH OD-05 ACCEPTED; TECHNICAL DETAIL/INDEPENDENT REVIEW OPEN; G1 NOT AUTHORIZED |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.5 |
| Logical design | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.5 |
| Related ownership contract | OWN-INV-018 v1.1 |
| Scope | Organization-owned Branch/Warehouse references and explicit Account-to-Branch / Account-to-Warehouse assignment storage required for O20-E |
| Explicit exclusions | Runtime module, C# contracts, EF mappings, DDL, migration, seed, grants, API, reporting view, feature activation and production operation |

## 1. Authority and interpretation

This document advances the logical design to a reviewable PostgreSQL physical-design proposal for the O20-E slice. It is subordinate to MDG-ARCH-MASTER-001 v3.0.3 and the owner-accepted policy decisions in O20E data ownership v0.3.

The Master architecture assigns Organization ownership of OrganizationUnit, Employee, Employment, Branch, Warehouse and the Warehouse → Section → Shelf → Bin/StorageLocation hierarchy. OWN-INV-018 v1.1 independently confirms that Organization owns Warehouse/location master data while Inventory owns stock facts. IdentityAccess owns Account, Credential, Session, Role and Permission. The owner has additionally accepted that Organization owns AccountBranchAssignment and AccountWarehouseAssignment; employment is not an access grant; multiple active assignments are permitted; and Branch assignment never implies Warehouse access.

The current source tree has no Organization runtime module. src/Modules/README.md prohibits introducing a module before G1 authorization. This document therefore describes proposed physical objects only. It is not a migration specification approved for execution and does not authorize creating any source folder, schema, table, role, endpoint or database object.

The distinctions below are deliberate:
- OWNER-ACCEPTED: decisions explicitly accepted by the product owner.
- PROPOSED: recommended physical design for review; not yet accepted as an implementation contract.
- OPEN: a decision or proof still required.
- NOT AUTHORIZED: no implementation or operational action may be taken.

## 2. Design invariants

1. Organization is the sole writer and lifecycle owner for Branch, Warehouse, their organizational relationship, and the two explicit assignment kinds.
2. IdentityAccess remains the sole owner of Account and security grants. Organization stores AccountId as an opaque reference; it never creates, updates, disables or authenticates an account.
3. An assignment is a separate domain fact, not a Role, Permission, Employment, claim, request parameter or client-side filter.
4. A valid permission is necessary but not sufficient for branch/warehouse row access. The source module must supply the authoritative resource context and Organization must prove the matching explicit assignment.
5. A Branch assignment cannot be expanded to its Warehouses. Warehouse access requires an AccountWarehouseAssignment for that exact WarehouseId.
6. For a Warehouse-scoped row, an explicit AccountWarehouseAssignment to that exact WarehouseId, an active Warehouse and its valid active current Branch relationship are required. A separate AccountBranchAssignment to the parent Branch is not required. This is owner-accepted under PD-ORG-13; IdentityAccess permission remains independently mandatory.
7. Employment status alone never creates, extends, restores or broadens an assignment.
8. Reporting is a consumer, never the source of master or assignment truth. A read model does not transfer data ownership.
9. No cross-module private-table access, cross-module write, or unapproved cross-schema foreign key is introduced.
10. Missing, inactive, revoked, ambiguous, stale, timed-out or unavailable authorization context is fail-closed. No client, cache or IdentityAccess role-name fallback may broaden access.

## 3. Proposed PostgreSQL namespace and object map

### 3.1 Namespace

PROPOSED: use one dedicated PostgreSQL schema named organization for Organization-owned persistence. Do not place Organization-owned business tables in public, identity or inventory. Schema creation itself remains gated.

The O20-E minimum object set is:

| Proposed object | Purpose | Owner |
|---|---|---|
| organization.branches | Canonical Branch master and lifecycle | Organization |
| organization.warehouses | Canonical Warehouse master and current Branch relationship | Organization |
| organization.account_branch_assignments | Current state of explicit Account-to-Branch assignments | Organization |
| organization.account_warehouse_assignments | Current state of explicit Account-to-Warehouse assignments | Organization |
| organization.assignment_events | Append-only assignment lifecycle history | Organization |
| organization.assignment_outbox | Durable delivery of assignment audit/event envelopes to AuditSecurity | Organization |

The physical location hierarchy (Section, Shelf and Bin/StorageLocation) is also Organization-owned by the Master architecture and OWN-INV-018. It is intentionally not designed or persisted by this O20-E slice. Its complete physical model must be aligned with the current Inventory LocationRef/StorageLocation contract in a separately controlled design section before any location DDL is written. The O20-E Warehouse RowScope proof requires WarehouseId, not a new Inventory-owned location master.

### 3.2 Branch master — proposed minimum shape

| Column | PostgreSQL type | Nullability | Purpose / constraint proposal |
|---|---|---|---|
| branch_id | uuid | NOT NULL | Stable primary key |
| branch_code | text | NOT NULL | Required normalized business code; unique across Organization; exact normalization/editability rules OPEN |
| display_name | text | NOT NULL | Human-readable name |
| lifecycle_state | text | NOT NULL | Proposed controlled state; exact state machine OPEN |
| row_version | bigint | NOT NULL | Optimistic concurrency version, starts at 1 |
| created_at_utc | timestamptz | NOT NULL | Server-generated creation time |
| created_by_account_id | uuid | NOT NULL | Opaque actor reference |
| updated_at_utc | timestamptz | NOT NULL | Server-generated last change time |
| updated_by_account_id | uuid | NOT NULL | Opaque actor reference |

OWNER-ACCEPTED policy: BranchCode is unique across Organization, required/nonblank, normalized before uniqueness comparison and not silently reusable after archive. PROPOSED constraints: primary key on branch_id; unique normalized-code key; nonblank code/name; row_version >= 1; lifecycle_state constrained to ACTIVE/INACTIVE/ARCHIVED; no physical DELETE. The exact normalization algorithm, editability/rename process and archival prerequisites remain OPEN before final DDL. No legal address, tax identity, cost center, branch phone, manager or accounting dimension is added by this O20-E scope.

### 3.3 Warehouse master — proposed minimum shape

| Column | PostgreSQL type | Nullability | Purpose / constraint proposal |
|---|---|---|---|
| warehouse_id | uuid | NOT NULL | Stable primary key |
| branch_id | uuid | NOT NULL | Current authoritative Branch relationship |
| warehouse_code | text | NOT NULL | Required normalized business code; unique within current Branch; exact normalization/editability rules OPEN |
| display_name | text | NOT NULL | Human-readable name |
| lifecycle_state | text | NOT NULL | Proposed controlled state; exact state machine OPEN |
| row_version | bigint | NOT NULL | Optimistic concurrency version, starts at 1 |
| created_at_utc | timestamptz | NOT NULL | Server-generated creation time |
| created_by_account_id | uuid | NOT NULL | Opaque actor reference |
| updated_at_utc | timestamptz | NOT NULL | Server-generated last change time |
| updated_by_account_id | uuid | NOT NULL | Opaque actor reference |

OWNER-ACCEPTED policy: each Warehouse has exactly one authoritative current Branch relationship; reassignment is allowed only through a separately authorized, reasoned and audited Organization command, and prior/current relationship history must be retained. WarehouseCode is unique within its current Branch, required/nonblank, normalized before comparison and not silently reusable after archive. PROPOSED constraints: primary key on warehouse_id; NOT NULL branch_id with restrictive local FK to branches.branch_id; unique normalized-code key within the current Branch; nonblank code/name; row_version >= 1; lifecycle_state constrained to ACTIVE/INACTIVE/ARCHIVED; no physical DELETE. A Warehouse must not be resolvable as active if its Branch is absent, inactive or inconsistent. The normalization algorithm and exact reassignment history representation remain OPEN. The effect of reassignment on existing AccountWarehouseAssignments is not yet owner-decided; no grant may be inferred from the new parent Branch.

### 3.4 Branch assignment current-state table

PROPOSED physical table: organization.account_branch_assignments.

| Column | PostgreSQL type | Nullability | Purpose |
|---|---|---|---|
| assignment_id | uuid | NOT NULL | Stable identity of this individual assignment |
| account_id | uuid | NOT NULL | Opaque IdentityAccess AccountId; no cross-module FK |
| branch_id | uuid | NOT NULL | Organization-owned Branch reference; local FK |
| lifecycle_state | text | NOT NULL | Proposed ACTIVE / REVOKED states |
| assigned_at_utc | timestamptz | NOT NULL | Server time when the grant became effective |
| assigned_by_account_id | uuid | NOT NULL | Opaque actor reference |
| assignment_reason | text | NOT NULL | Required business reason |
| revoked_at_utc | timestamptz | NULL | Server time of revocation |
| revoked_by_account_id | uuid | NULL | Opaque actor reference |
| revocation_reason | text | NULL | Required when revoked |
| row_version | bigint | NOT NULL | Monotonic optimistic concurrency version |
| correlation_id | uuid | NOT NULL | End-to-end operation correlation |

Required invariant: only lifecycle_state ACTIVE with revoked_at_utc IS NULL can grant access, and the referenced Branch must currently be active. A local restrictive FK to branches.branch_id is appropriate because Branch is owned by the same Organization persistence boundary.

PROPOSED duplicate-prevention constraint: at most one non-revoked assignment for the same account_id + branch_id, using a partial unique index over that pair. This still permits one account to have multiple different active Branch assignments and many accounts to have the same Branch assignment. Revocation preserves the row and history; it never physically deletes it.

### 3.5 Warehouse assignment current-state table

PROPOSED physical table: organization.account_warehouse_assignments.

The shape mirrors AccountBranchAssignment, with warehouse_id replacing branch_id:

| Column | PostgreSQL type | Nullability | Purpose |
|---|---|---|---|
| assignment_id | uuid | NOT NULL | Stable identity of this individual assignment |
| account_id | uuid | NOT NULL | Opaque IdentityAccess AccountId; no cross-module FK |
| warehouse_id | uuid | NOT NULL | Organization-owned Warehouse reference; local FK |
| lifecycle_state | text | NOT NULL | Proposed ACTIVE / REVOKED states |
| assigned_at_utc | timestamptz | NOT NULL | Server time when the grant became effective |
| assigned_by_account_id | uuid | NOT NULL | Opaque actor reference |
| assignment_reason | text | NOT NULL | Required business reason |
| revoked_at_utc | timestamptz | NULL | Server time of revocation |
| revoked_by_account_id | uuid | NULL | Opaque actor reference |
| revocation_reason | text | NULL | Required when revoked |
| row_version | bigint | NOT NULL | Monotonic optimistic concurrency version |
| correlation_id | uuid | NOT NULL | End-to-end operation correlation |

Required invariant: Warehouse scope is granted only by an explicit active assignment to that exact WarehouseId, while both Warehouse and its current Branch are active and the Warehouse relationship is valid. A Branch assignment is never consulted as a substitute for this row. The local FK references warehouses.warehouse_id. The partial unique rule is one non-revoked assignment per account_id + warehouse_id, while different Warehouses may be simultaneously assigned to the same account.

Owner policy v0.3 explicitly resolves PD-ORG-13: an exact active AccountWarehouseAssignment plus an active Warehouse and valid active current Branch relationship is sufficient. A separate parent AccountBranchAssignment is not an additional prerequisite. Branch assignment alone still never grants Warehouse access.

### 3.6 Append-only assignment event history

PROPOSED physical table: organization.assignment_events. This table is domain lifecycle history, not a competing canonical AuditSecurity AuditEvent store.

| Column | PostgreSQL type | Nullability | Purpose |
|---|---|---|---|
| assignment_event_id | uuid | NOT NULL | Event identity / primary key |
| assignment_kind | text | NOT NULL | BRANCH or WAREHOUSE |
| branch_assignment_id | uuid | NULL | FK for a Branch assignment event |
| warehouse_assignment_id | uuid | NULL | FK for a Warehouse assignment event |
| aggregate_version | bigint | NOT NULL | Version after the transition |
| event_type | text | NOT NULL | Proposed ASSIGNED or REVOKED |
| subject_account_id | uuid | NOT NULL | Account affected; opaque reference |
| resource_id | uuid | NOT NULL | BranchId or WarehouseId according to assignment_kind |
| actor_account_id | uuid | NOT NULL | Authenticated server-derived actor |
| approval_mode | text | NOT NULL | Proposed SECOND_PERSON or PRODUCT_OWNER_DIRECT decision mode |
| approver_account_id | uuid | NULL | Distinct authenticated approver for non-owner operations; NULL for Product Owner direct action |
| approved_at_utc | timestamptz | NULL | Server time of second-person approval; required only for SECOND_PERSON mode |
| reason | text | NOT NULL | Required reason for the transition |
| occurred_at_utc | timestamptz | NOT NULL | Server-generated UTC timestamp |
| correlation_id | uuid | NOT NULL | Request/operation correlation |
| idempotency_key | uuid | NOT NULL | Stable mutation retry identity |
| request_digest | bytea | NOT NULL | Digest of canonical request; never raw secrets |

The row must reference exactly one of branch_assignment_id and warehouse_assignment_id; the selected FK is restrictive. A unique constraint on the selected assignment identity + aggregate_version prevents duplicate event versions. Event rows are append-only: UPDATE, DELETE and TRUNCATE are prohibited for runtime identities. The event records only identifiers and reason/audit metadata; it must not copy account PII, credentials, tokens or unnecessary source payloads.

### 3.7 Assignment audit outbox

PROPOSED physical table: organization.assignment_outbox. One outbox envelope is inserted in the same transaction as the assignment state change and assignment event.

Minimum fields: outbox_id uuid primary key; assignment_event_id uuid unique restrictive FK; event_id uuid unique; contract_name text; contract_version text; payload_schema_version smallint; payload_hash_algorithm text; payload_hash bytea; payload jsonb; delivery_state text; attempt_count integer; next_attempt_at_utc timestamptz; lease_until_utc timestamptz nullable; created_at_utc timestamptz; acknowledged_at_utc timestamptz nullable.

The payload is an allow-listed envelope containing actor, subject AccountId, assignment identity, resource identity, action, before/after state, reason, approval mode, second approver identity/time when applicable, aggregate version, correlation and server timestamps. For a Product Owner direct action it records the Product Owner authority mode and absence of a second approver; it does not imply absence of audit. It must not include password, credential, session token, recovery code, full account profile or arbitrary request JSON.

The event/outbox contract, AuditSecurity durable-ACK semantics, worker identity, retry/quarantine state machine and retention are OPEN until AuditSecurity/Platform technical owners accept the integration. The table above is only a proposal and must not be mistaken for a published contract.

## 4. Cross-module identifiers and referential integrity

- IdentityAccess migration 0003 establishes identity.accounts.account_id as PostgreSQL uuid. Organization account_id and actor account references therefore use uuid for representation compatibility.
- Organization must not create a foreign key into identity.accounts. Existence, enabled account status and principal authenticity are verified through the published IdentityAccess application contract, not by reading or writing the private identity schema.
- BranchId and WarehouseId are Organization-owned identifiers. Organization tables may use local restrictive foreign keys between Branch, Warehouse, assignment and event rows.
- Inventory stores opaque WarehouseId / LocationId references and does not create or mutate Organization masters. No Inventory table is the canonical owner of Warehouse.
- No ID generation version is assumed. PostgreSQL uuid is the proposed storage type, but the platform's stable ID generation/version policy and the generator boundary remain OPEN. No extension, database default or client-generated identifier is authorized here.
- All technical timestamps use timestamptz and UTC. Server/application time is authoritative; client timestamps cannot activate or revoke an assignment.

## 5. Index and constraint proposal

The following are candidate indexes, subject to query-plan validation and final name conventions:

| Object | Candidate index / constraint | Why |
|---|---|---|
| branches | PK(branch_id) | Canonical identity |
| branches | Candidate unique key on normalized branch_code | Business-code uniqueness, only after code policy acceptance |
| warehouses | PK(warehouse_id) | Canonical identity |
| warehouses | FK index on branch_id | Branch lifecycle / warehouse listing |
| warehouses | Candidate unique key on branch_id + normalized warehouse_code or organization-wide code | Uniqueness scope OPEN |
| account_branch_assignments | Partial unique (account_id, branch_id) WHERE non-revoked | Prevent duplicate live pair; preserves multi-Branch assignments |
| account_branch_assignments | Partial (account_id, branch_id) WHERE non-revoked | Resolve the account's active Branch set |
| account_branch_assignments | Partial (branch_id, account_id) WHERE non-revoked | Branch administration / reverse lookup |
| account_warehouse_assignments | Partial unique (account_id, warehouse_id) WHERE non-revoked | Prevent duplicate live pair; preserves multi-Warehouse assignments |
| account_warehouse_assignments | Partial (account_id, warehouse_id) WHERE non-revoked | Resolve the account's active Warehouse set |
| account_warehouse_assignments | Partial (warehouse_id, account_id) WHERE non-revoked | Warehouse administration / reverse lookup |
| assignment_events | Unique (assignment identity, aggregate_version) | Event sequence integrity |
| assignment_events | (subject_account_id, occurred_at_utc DESC) | Audited assignment history by subject |
| assignment_events | (resource_id, occurred_at_utc DESC) | Resource assignment history |
| assignment_outbox | (delivery_state, next_attempt_at_utc, outbox_id) | Bounded worker claim query |

The final DDL must include explicit CHECK constraints for lifecycle states and metadata coherence; exact constraint names; FK delete behavior; NOT NULL rules; index predicates; query-plan evidence; and grant review. Do not add speculative indexes or broad JSONB indexes. A production-like PostgreSQL 18 dataset is required before performance claims.

## 6. Assignment lifecycle and authorization semantics

### 6.1 Creation

A create command is accepted only when:
- authenticated actor and enabled subject account are resolved server-side through IdentityAccess;
- a specific, active Organization Branch or Warehouse is resolved;
- the actor holds the exact approved Organization assignment-management permission, or the separately registered Product Owner authority is resolved from a trusted server-side binding;
- the subject AccountId is not the actor's own AccountId (self-assignment is prohibited, including for the Product Owner);
- for a non-owner actor, a second authenticated approver distinct from both actor and subject must approve the assignment change before the state transition takes effect;
- the designated Product Owner may create/change/revoke assignments for any other account without a second approver; this exception does not grant impersonation, credential/session access or IdentityAccess account/permission mutation;
- every operation, including the Product Owner direct path, records actor, target, reason, approval mode, approver where applicable, correlation and immutable audit evidence;
- required reason and command idempotency metadata are valid;
- the proposed assignment does not duplicate a non-revoked account/resource pair;
- audit event and outbox can be persisted atomically.

Employment is not consulted as a grant source. It may be shown as organizational context, but no employment state creates permission or assignment.

### 6.2 Revocation

Revocation is an explicit Organization command. It changes the current-state row to REVOKED, sets server revocation metadata, increments row_version, appends exactly one event version, and inserts the audit outbox envelope in one database transaction. Repeating the same idempotency key and canonical request returns the original result. Same key with a different digest fails closed. Revocation cannot be silently undone by a retry, login, role change or Employment update.

### 6.3 Reassignment, expiry and reactivation — OPEN

OWNER-ACCEPTED: a newly approved assignment becomes effective immediately at its server-side commit; future-dated activation and automatic expiry are not enabled; revocation is terminal; a later grant creates a new AssignmentId and new lifecycle history. A revoked assignment is not reactivated in place. The detailed approval-pending/rejection workflow and concurrency behavior remain OPEN and must ensure no unapproved grant becomes effective.

### 6.4 Resource deactivation and Warehouse reassignment — OPEN

An inactive/archived Branch or Warehouse grants no effective row scope regardless of retained assignment rows. Assignment history is preserved. Branch/Warehouse archive transitions and their effects on historical reports require a separate lifecycle decision.

OWNER-ACCEPTED: Warehouse has exactly one current Branch and may be reassigned only by a controlled, separately authorized, reasoned and audited Organization command; prior/current relationship history is retained. Reassignment must be serialized with Warehouse scope resolution. OPEN: the owner has not yet decided whether existing AccountWarehouseAssignments remain effective, are suspended pending explicit reaffirmation, or are revoked/regranted after reassignment. The implementation must not infer or broaden access during this transition.

## 7. Authorization composition and RowScope enforcement

### 7.1 Independent checks

The effective permission decision is a conjunction of:
1. valid authenticated IdentityAccess principal and session;
2. exact active permission grant for the requested operation (for example read, export or print; exact Organization permission keys are not defined here);
3. a valid Organization assignment for the authenticated AccountId and requested resource scope;
4. authoritative source-row context from the module that owns the reportable business fact;
5. source-state and other domain predicates required by that owner.

An IdentityAccess role name, Employment record, all permission scope, or caller-supplied BranchId/WarehouseId must not bypass the explicit Organization assignment requirement for branch/warehouse-scoped rows. If IdentityAccess supplies an additional narrowing scope, it may not broaden Organization's assignment set. Owner-accepted PD-ORG-13 resolves Warehouse composition: exact active Warehouse assignment plus active Warehouse/current Branch facts is sufficient; the parent Branch assignment is not additionally required. PD-ORG-07 still requires the versioned IdentityAccess public permission/principal contract before implementation.

### 7.2 Typed resolver result

The Organization contract returns one of:
- AUTHORIZED_CONTEXT: an authoritative set of active BranchIds and WarehouseIds plus a consistency/version token and evaluation timestamp;
- DENIED: the identity/resource/assignment predicate is not satisfied;
- UNAVAILABLE: timeout, database failure, missing contract, ambiguity or integrity error.

Only AUTHORIZED_CONTEXT may proceed. DENIED never materializes out-of-scope rows. UNAVAILABLE returns a generic dependency failure (normally 503) and never falls back to cache, client values or broad access.

The resolver does not return credentials, employee details or unnecessary profile data. AccountId is taken from the server-authenticated principal, never from a request body or query.

### 7.3 Source-owned row context

Reporting must not infer a row's Branch/Warehouse from report parameters, employee status, a related Party, a default Branch or the first Warehouse returned. The business module that owns each row must provide its authoritative resource context through a published, versioned query contract.

For a Warehouse-scoped Inventory row, Inventory supplies the WarehouseId belonging to that authoritative row. Organization validates current Warehouse and Branch state and the explicit AccountWarehouseAssignment. For a Branch-scoped row, the source owner supplies the authoritative BranchId and Organization validates the explicit AccountBranchAssignment. If a row is Warehouse-scoped, Branch assignment alone is insufficient.

### 7.4 SQL-before-materialization

The exact allowed BranchId / WarehouseId set must be bound server-side and applied in the source SQL query before row materialization. The same predicate must cover:
- list, pagination and count;
- detail and nested detail;
- search and autocomplete;
- dashboard counters and aggregates;
- report preview and generated report;
- print and PDF generation;
- CSV/XLSX and all other exports;
- bulk reads and API direct access.

The query plan must constrain both returned rows and metadata. Pagination totals, grouping, sums, averages, existence checks and dashboard counters use the same scope predicate. In-memory filtering, client-only filtering and post-materialization trimming are prohibited.

Candidate SQL pattern for a source-owned WarehouseId column is source_row.warehouse_id = ANY(@authorized_warehouse_ids) where the parameter is a server-derived uuid array. This is a pattern, not an approved shared query API or table reference. Empty/NULL/ambiguous scope must return no business rows or a fail-closed error; it must never mean unrestricted.

ReportingReadOnly must not receive direct broad SELECT on Organization assignment/base tables or source-module base tables as a shortcut around the API contract. Any future reporting view/materialized projection requires a separate source-owner contract, approved RowScope design and exact object-level privilege review.

## 8. Transaction, concurrency and revocation consistency

### 8.1 Read linearization

For a normal report query, the authorization set and source rows must be evaluated under one PostgreSQL statement snapshot or a single explicit consistent transaction. The query must not resolve the allowed set, release its snapshot, then use stale scope against a later source query. No long-lived process cache is authoritative.

A read that began before a committed revocation may complete against its already-established database snapshot. A new read beginning after revocation commits must observe the revocation. This is the proposed read consistency rule and requires an integration test under real concurrent PostgreSQL sessions.

### 8.2 Mutations and serialization

Where a protected business write is in O20-E scope, assignment/resource validation must participate in the same server-side transaction as the write. The proposed lock protocol is:
1. resolve authenticated AccountId and exact permission;
2. acquire applicable assignment row locks in deterministic order by assignment_id;
3. acquire relevant Branch/Warehouse master row locks in deterministic order by resource ID;
4. acquire source aggregate locks under the source module's published transaction/lock contract;
5. re-evaluate assignment, resource active state and row_version after lock acquisition;
6. perform the source command and persist required audit/outbox metadata;
7. commit; all locks release with commit/rollback.

A revoke command obtains an exclusive row lock for the assignment. A protected mutation obtains a compatible shared lock and holds it through commit. If the mutation obtains the lock first, it must commit or roll back before revocation can complete; if revocation commits first, the mutation observes REVOKED and is denied. The exact PostgreSQL lock modes and global cross-module lock order must be reconciled with the application Unit-of-Work contract before implementation; this paragraph defines the desired serialization outcome, not final code.

### 8.3 Long-running exports

An export is not authorized merely because its job was queued. The worker resolves the actor's current principal, permission and Organization assignments when execution starts. Before publishing or allowing download of the generated artifact, the delivery boundary revalidates the permission and assignment version/revocation state. A revoked or unavailable context blocks delivery and records an auditable outcome. Artifact retention and cleanup are owned by the export/document subsystem and remain outside this physical schema proposal.

## 9. Audit, idempotency and retention boundary

- Every assign/revoke command is individually auditable with actor AccountId, subject AccountId, assignment identity, resource identity, before/after state, reason, policy/contract version, aggregate version, correlation, idempotency identity and UTC time.
- Assignment state, append-only assignment event and durable local outbox intent commit atomically. Failure to persist any required local item rolls back the command.
- AuditSecurity remains the sole owner of canonical shared AuditEvent, security-event retention and audit access/export. Organization does not create a competing global audit store.
- Delivery to AuditSecurity is at-least-once with stable EventId and payload digest. Duplicate same EventId + same digest returns the original durable acknowledgement; same EventId + different digest is an integrity failure. Exact acknowledgement, retries, quarantine and retention requirements must be adopted from the published AuditSecurity contract before implementation.
- Assignment history must not be hard-deleted by ordinary runtime or account deactivation. Account deletion is not part of this scope; opaque historical AccountId references remain intact.
- The business retention period and legal-hold requirements for Organization assignment history are OPEN. Do not invent a retention duration or purge/archive job.

## 10. Database security boundary

This physical design issues no privilege. Prior to implementation, the exact object-level matrix must define:
- NOLOGIN schema owner and dedicated migration identity for Organization objects;
- application runtime principal limited to required Organization command/query operations;
- no runtime DDL, ownership, schema CREATE, TRUNCATE or audit-history mutation;
- append-only INSERT/SELECT treatment for assignment_events, with UPDATE/DELETE/TRUNCATE denied;
- narrowly scoped outbox worker claim/update rights, not broad Organization master mutation;
- no reporting_ro direct assignment/master-table reads unless a separately approved read interface explicitly requires them;
- no direct access by Windows clients; all authentication/authorization remains server-side.

The O20-G disposable catalog inventory is a useful baseline, not positive proof of Organization role grants. A new schema/table set requires refreshed catalog inventory and effective privilege evidence after implementation.

## 11. Acceptance test matrix

These tests are design requirements only. None has been implemented or run by this document.

| Test ID | Scenario | Required result |
|---|---|---|
| ORG-SCOPE-001 | Active Branch assignment and Branch-scoped row | Only that Branch's rows pass |
| ORG-SCOPE-002 | Branch assignment without Warehouse assignment | All Warehouse-scoped rows are denied |
| ORG-SCOPE-003 | Explicit active Warehouse assignment | Only the exact assigned Warehouse rows pass |
| ORG-SCOPE-004 | Multiple Branch and Warehouse assignments | Exact set union within each assignment kind; no inheritance |
| ORG-SCOPE-005 | Employment active without assignment | Deny |
| ORG-SCOPE-006 | AccountId/BranchId/WarehouseId forged in client input | No authorization effect |
| ORG-SCOPE-007 | Revoked assignment | Deny on the first request whose snapshot begins after revoke commit |
| ORG-SCOPE-008 | Inactive Branch or Warehouse | Deny |
| ORG-SCOPE-009 | Warehouse missing/invalid current Branch relationship | Deny |
| ORG-SCOPE-010 | IdentityAccess permission absent | Deny even when Organization assignment exists |
| ORG-SCOPE-011 | IdentityAccess all-scope or role name with no Organization assignment | Deny |
| ORG-SCOPE-012 | Additional IdentityAccess narrowing scope | Effective set cannot exceed its intersection with Organization assignments |
| ORG-SCOPE-013 | Resolver timeout, DB outage, ambiguous relationship or integrity error | Unavailable / fail-closed; no cache fallback |
| ORG-SCOPE-014 | List, search, detail, count, aggregate, dashboard, print and export | Same source RowScope; no metadata leakage |
| ORG-SCOPE-015 | Bulk identifiers mix authorized and unauthorized rows | No unauthorized rows; no partial write where command policy is atomic |
| ORG-SCOPE-016 | Concurrent revoke and protected mutation | Outcome follows the declared lock/linearization rule; no post-revoke commit |
| ORG-SCOPE-017 | Export created before revocation, delivered after revocation | Delivery denied after revalidation |
| ORG-SCOPE-018 | Duplicate same assignment command key and same digest | Original result returned once |
| ORG-SCOPE-019 | Same idempotency key with changed digest | Fail closed; no second assignment |
| ORG-SCOPE-020 | Event/outbox write failure | Entire assignment command rolls back |
| ORG-SCOPE-021 | ReportingReadOnly attempts direct base-table bypass | Denied |
| ORG-SCOPE-022 | Branch assignment is revoked while Warehouse assignment remains active | Warehouse access remains governed solely by the explicit Warehouse assignment, subject to active master relationship |
| ORG-SCOPE-023 | Duplicate live Account/resource assignment | Rejected by uniqueness/invariant |
| ORG-SCOPE-024 | Assignment history queried after revocation/account disablement | Historical record preserved; no grant restored |

Required evidence: PostgreSQL 18 disposable integration, multiple independent sessions for concurrency, test/build/schema SHA binding, command/environment, SQL diagnostics, cleanup, positive and negative result logs, and independent review. Unit tests alone or SQLite do not satisfy the database gate.

## 12. Open decisions required before design completion

| ID | Open item | Proposed default | Required closure |
|---|---|---|---|
| PD-ORG-01 | Warehouse-to-Branch cardinality/reassignment/history | Exactly one current Branch; reassignment only by a separate authorized/audited command; retain relationship history | OWNER-ACCEPTED; existing Warehouse assignment effect after reassignment remains OPEN |
| PD-ORG-02 | Branch/Warehouse business codes | BranchCode unique across Organization; WarehouseCode unique in current Branch; required, normalized and not silently reusable after archive | OWNER-ACCEPTED; exact normalization/editability/collision algorithm OPEN |
| PD-ORG-03 | Branch/Warehouse lifecycle vocabulary | ACTIVE / INACTIVE / ARCHIVED; no physical deletion | OWNER-ACCEPTED; transition prerequisites/dependent-resource handling OPEN |
| PD-ORG-04 | Assignment effective lifecycle | Immediate approved activation; no future dating/automatic expiry; revocation terminal; regrant new AssignmentId | OWNER-ACCEPTED; approval-pending workflow/concurrency OPEN |
| PD-ORG-05 | Assignment actor/reason/separation | No self-assignment including Owner; non-owner create/change/revoke requires a distinct second approver; Product Owner may perform those operations for any other account without second approval | OWNER-ACCEPTED; trusted Product Owner principal binding and approval transaction details OPEN |
| PD-ORG-06 | Exact permission keys and role grants | No key or grant introduced by this design | Reconcile with current IdentityAccess catalog and owner-approved permission matrix |
| PD-ORG-07 | IdentityAccess scope-model composition | Permission grant AND Organization assignment; IdentityAccess additional scope only narrows | POLICY OWNER-ACCEPTED; public versioned IdentityAccess contract and technical integration review OPEN |
| PD-ORG-08 | AuditSecurity event/outbox contract | Local event + outbox in same transaction, canonical AuditEvent owned by AuditSecurity | AuditSecurity/Platform acceptance; durable ACK and retention contract |
| PD-ORG-09 | Stable identifier generation version | uuid storage; generator policy not assumed | Platform UUID policy |
| PD-ORG-10 | Cross-module transaction/lock order | Assignment/master locks before source aggregate locks | Application/Platform Architecture review and PostgreSQL race proof |
| PD-ORG-11 | Assignment-history retention and legal hold | OWNER-ACCEPTED baseline: retain by default; no physical delete or automatic purge; disposal only after approved retention expiry, no Legal Hold and documented records/legal authorization; duration unset | Implement hold authority/scope/release, approved jurisdictional schedule, media handling, disposal workflow and PostgreSQL proof |
| PD-ORG-12 | Full source-module RowScope inventory | Begin with sources that publish authoritative BranchId/WarehouseId contracts; none may be inferred | Inventory all reportable list/search/detail/aggregate/print/export paths and obtain source-owner contracts |
| PD-ORG-13 | Does Warehouse scope require a separate AccountBranchAssignment to its parent Branch? | Exact active AccountWarehouseAssignment + active Warehouse + valid active current Branch; no separate parent Branch assignment required | OWNER-ACCEPTED on 2026-11-22; IdentityAccess permission remains independently required |

## 13. G1 authorization readiness and execution boundary

The revised proposal incorporates owner-accepted PD-ORG-01 through PD-ORG-05 and PD-ORG-13. It is not a final implementation contract until the remaining bounded details are resolved, the technical cross-contract boundaries are accepted, and independent review is completed. A design review must also confirm the current IdentityAccess permission-scope contract, Application Unit-of-Work boundary, AuditSecurity event contract, UUID policy, cross-module dependency direction, and every source query path that can expose branch/warehouse data.

G1 package requirements:
1. Finalize owner decisions and this physical design as a versioned successor.
2. Publish the Organization module dependency and public Contract boundary without introducing a runtime module.
3. Prepare a migration reservation request through WG-0; reservation is not a migration or implementation authorization.
4. Prepare threat model, object privilege matrix, audit/outbox contract, RowScope source inventory and the full acceptance test specification.
5. Obtain independent Architecture/Security review against one exact final SHA, including findings, retest and reviewer identity.
6. Obtain an explicit product-owner decision AUTHORIZE G1 for Organization on that exact reviewed SHA. Prior O20-E policy acceptance is not G1 authorization.
7. Only after G1 authorization may the Organization module skeleton, Domain/Application/Infrastructure/Contracts, migration and runtime tests be created. Any later executable work remains separately subject to migration, CI, review and release gates.

Until all of the above are complete:
- g1Authorized remains false / not granted;
- no src/Modules/Organization directory is created;
- no Organization schema, table, migration, seed, permission, role grant, resolver, endpoint, query view or reporting_ro access is created;
- O20-E remains OPEN and reporting RowScope remains fail-closed;
- PR #86 remains unmerged; no reviewer request is made as part of this draft;
- no production database or credential is touched.

## 14. Disposition

| Dimension | Status |
|---|---|
| Organization ownership of Branch/Warehouse and hierarchy | Established by Master v3.0.3 and OWN-INV-018 v1.1 |
| Organization ownership of AccountBranchAssignment / AccountWarehouseAssignment | OWNER-ACCEPTED in O20E v0.3 |
| Base and additional assignment policy decisions | OWNER-ACCEPTED in O20E v0.3, including Product Owner second-approver exception |
| Logical design | UPDATED in v0.2 |
| O20-E physical design | PROPOSED v0.2; remaining technical details and independent review OPEN |
| Organization G1 authorization | NOT GRANTED |
| Organization runtime / DDL / migration | NOT IMPLEMENTED; NOT AUTHORIZED |
| Positive / negative PostgreSQL RowScope evidence | NOT RUN |
| ReportingReadOnly RowScope grants | NOT GRANTED |
| O20-E | OPEN |
| Overall O-20 | OPEN |

**END — MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v0.2**


## 11. Controlled physical-design successor — 2026-11-22 (v0.3)

### 11.1 Corrected actor invariants

Replace every v0.2 statement that prohibits Product Owner self-assignment. Non-owner self-assignment remains denied. The trusted Product Owner may grant/change/revoke assignments for the Product Owner's own AccountId or any other account without a second approver. Persist actor and subject independently even when equal. approval_mode=PRODUCT_OWNER_DIRECT; approver_account_id and approved_at_utc remain NULL by design. For non-owner commands, enforce approval_mode=SECOND_PERSON, approver distinct from actor and subject, and approved evidence before state mutation. The owner principal must be resolved by a protected server-side binding outside client claims and role names.

### 11.2 Code canonicalization and reservation

PROPOSED v1 canonicalizer: Unicode NFC, trim outer whitespace, reject empty, control/format characters and any remaining whitespace; preserve punctuation and all non-ASCII characters; case-sensitive bytewise comparison under PostgreSQL C collation. Do not fold Persian/Arabic digits, strip diacritics, transliterate, remove punctuation or use locale-sensitive comparison. This proposal must be accepted by the owner and validated identically in .NET and PostgreSQL before DDL.

Use retained code-reservation facts, not a partial unique index over only active rows, to ensure archived codes are not silently reused. Branch reservations are unique globally. Warehouse reservations are unique by BranchId + canonical code; on reassignment the destination Branch reservation must be checked and atomically acquired, while the source Branch reservation remains retained as historical. Rename is allowed only before first external reference; afterward code is immutable. Reassignment does not rewrite historical reservations.

### 11.3 Warehouse reassignment and existing assignments

AccountWarehouseAssignment remains attached to WarehouseId. It is not copied, reissued, silently broadened or automatically revoked on Branch reassignment. The Warehouse must first be INACTIVE; the reassignment transaction locks the Warehouse and both Branch rows in the yet-to-be-published global lock order, validates destination Branch ACTIVE and code uniqueness, appends a relationship-history row, increments Warehouse row_version and commits. While INACTIVE, all Warehouse scope is denied. After explicit reactivation, only pre-existing still-active exact-Warehouse assignments are effective again, and only while the current Branch is ACTIVE. A separate AccountBranchAssignment to the destination is not required. If an assignment is revoked, it is terminal; any later grant requires a new AssignmentId.

### 11.4 Lifecycle persistence constraints

Use constrained state values ACTIVE/INACTIVE/ARCHIVED and server-side transition commands. ARCHIVED is terminal; DELETE is unavailable to runtime roles. Branch deactivation is blocked until every child Warehouse is INACTIVE or ARCHIVED. Warehouse archival requires INACTIVE, no active Warehouse assignments, no active dependent StorageLocation and no unresolved dependent workflow. Branch archival requires INACTIVE, all Warehouses ARCHIVED and no active Branch assignments. Deactivation immediately denies current scope but retains assignment history; reactivation restores only still-active explicit assignments. Archive never restores access.

### 11.5 Local transaction and audit-intent boundary

The Organization command transaction contains idempotency receipt, assignment/master state transition, append-only domain event and a minimized local audit-intent outbox envelope. It does not attempt a distributed transaction with AuditSecurity. The committed local outbox is durable intent, not proof of canonical AuditEvent delivery. AuditSecurity must accept/deduplicate stable EventId and return a durable acknowledgement. Outbox delivery uses durable attempts, leases, bounded exponential backoff, terminal quarantine, alerting and reconciliation. Quarantine is security-critical and must not be silently discarded. No Organization runtime/DDL is authorized by this proposal.

### 11.6 Query authorization and database boundary

ReportingReadOnly receives no direct broad base-table grants. A source query must join/resolve authoritative WarehouseId and apply the authorized exact Warehouse set in SQL before rows, counts, aggregates or pagination metadata are materialized. Export artifact delivery revalidates current account/session, permission and assignment. Every query path (list, search, detail, nested detail, dashboard, count, aggregate, report, print, PDF, CSV/XLSX, bulk/API) must share one tested predicate. Missing/ambiguous/unavailable Organization resolver, stale authorization, timeout, inactive parent, revoked assignment or integrity failure denies. The current source inventory has no executable Organization/reporting/Inventory row consumer; therefore this design has no present runtime grant target.

### 11.7 Open physical prerequisites

The Platform Lock-Order Registry and PostgreSQL concurrent revocation/reassignment proof are not present in this slice; ID generation/version policy remains open. The core retention baseline is OWNER-ACCEPTED in OD-05; retention duration, Legal Hold implementation, disposal workflow and media handling remain open. Code canonicalization and lifecycle are proposals pending owner acceptance. AuditSecurity event schema/version/ACK contract requires its owner acceptance. These are explicit G1 blockers.

**END — MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v0.3**


## 15. Owner acceptance addendum — 2026-11-22 (v0.4)

OD-01, OD-02 and OD-03 in `MDG-SECURITY-O20E-O20F-OWNER-DECISION-ACCEPTANCE-001 v0.1` are OWNER-ACCEPTED and supersede only the corresponding proposal status in sections 11–14:

1. **Code comparison:** Unicode NFC; trim outer whitespace; reject empty, control/format and remaining whitespace; preserve punctuation/non-ASCII; case-sensitive canonical UTF-8 bytewise comparison using PostgreSQL C collation. No Persian/Arabic digit folding, transliteration, diacritic stripping, punctuation removal or locale-sensitive case conversion. .NET/PostgreSQL equivalence, Unicode vectors and collision behavior must be proved before DDL.
2. **Warehouse reassignment:** exact immutable WarehouseId remains the assignment target. Reassignment is allowed only while Warehouse INACTIVE, does not copy/broaden/grant/revoke assignments, and denies Warehouse scope while inactive. Explicit reactivation may restore only still-active exact-Warehouse assignments, subject to current Branch ACTIVE and IdentityAccess permission.
3. **Archive:** ARCHIVED is terminal; reactivation and physical deletion are prohibited. Archive preconditions, dependent-resource validation, locking and retained-history/records policy remain open for technical review and proof.

All other physical constraints remain proposals or blockers exactly as identified in this document. No Organization DDL, migration, schema, role, permission, grant, resolver, RowScope consumer or runtime module is authorized. G1 remains NOT GRANTED; O20-E remains OPEN.

**END — physical design v0.4 owner acceptance addendum**


## 16. Owner-accepted records-retention addendum — 2026-11-22 (v0.5)

OD-05 and `MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.1` are controlling for PD-ORG-11 and supersede any earlier proposal wording that the default retention decision itself remains open.

Physical-design constraints:
1. Assignment lifecycle facts and Warehouse-to-Branch relationship history are retained by default. Runtime identities must not have a hard-delete capability for these records.
2. No scheduled expiry/purge, irreversible anonymization, masking, cryptographic erasure or destructive cleanup process may be designed as active behavior until the retention schedule and lawful-disposal workflow have been approved.
3. A Legal Hold must block disposal even after ordinary retention expiry. Missing, unavailable, ambiguous or unresolved hold state fails closed for disposal.
4. A future disposal command must carry the approved schedule/version, exact record scope, evidence that the applicable period elapsed, verified no-hold result, attributable authorization, reason, idempotency key and concurrency/version precondition. It must emit durable audit evidence.
5. Retention and hold records must not duplicate canonical AuditSecurity events. Domain history remains Organization-owned; canonical AuditEvent/SecurityEvent remains AuditSecurity-owned.
6. No period is specified. Until an approved schedule exists, the implementation must retain history and disable automatic deletion.

Open implementation details include Legal Hold authority/scope/release, jurisdiction-to-record-class mapping, privacy obligations, backup/replica/export handling, disposal approval separation, worker identity, operational recovery and PostgreSQL concurrency/evidence. The policy does not authorize DDL, migration, schema, worker, role/grant, runtime or production disposal.

**PD-ORG-11 disposition:** core retention policy OWNER-ACCEPTED; detailed legal schedule and technical proof OPEN. G1 remains NOT GRANTED; O20-E remains OPEN.

**END — physical design v0.5 records-retention addendum**


## 17. Relationship-history technical proof successor — 2026-11-22 (v0.6)

The owner-accepted OD-02 reassignment rule requires the prior Warehouse-to-Branch relationship to remain reconstructable. The new disposable PostgreSQL integration PoC is:

`tests/MDGsm.Domain.Tests/Security/O20OrganizationRelationshipHistoryPostgresProofTests.cs`

It creates only a marked disposable database schema and verifies:
1. reassignment is rejected while the Warehouse is ACTIVE;
2. reassignment to an inactive destination Branch is rejected;
3. both rejected attempts leave the current Warehouse Branch and history unchanged;
4. a permitted reassignment while Warehouse is INACTIVE updates the current Branch and increments row_version;
5. the initial relationship and new reassignment are recorded as separate ordered history events tied to the same WarehouseId;
6. the historical event rows reject UPDATE and DELETE;
7. the successful change creates exactly one additional history event and does not alter the Warehouse identity.

This test is an isolated policy proof, not a production schema, Organization module, global Lock-Order proof or authorization implementation. It does not prove concurrent reassignment/revocation races, production actor/approver binding, durable AuditSecurity acknowledgement, or legal retention/hold handling. The original OD-01/OD-02/OD-03 PoC remains in `O20OrganizationPolicyPostgresProofTests.cs`.

At the time this successor was written, the new history test had been added and checksummed but its CI run had not yet completed. Do not mark this test PASS until the exact new head's PostgreSQL integration job and test output are inspected.

**Disposition:** relationship history append-only semantics now have dedicated executable disposable-PostgreSQL coverage pending CI; O20-E remains OPEN and G1 NOT GRANTED.

**END — physical design v0.6 relationship-history proof addendum**


## 18. Cross-contract and Platform proposal register — 2026-11-22 (v0.7)

The remaining dependencies are now split into named, reviewable proposals rather than being left as untracked prose:

| Dependency | Proposal | Status / authority still required |
|---|---|---|
| IdentityAccess principal, permission and Product Owner authority | MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 v0.5 | IdentityAccess module-owner acceptance, exact public C# contract, owner-binding lifecycle and session-revocation linearization |
| Global lock ordering / concurrency | MDG-PLATFORM-LOCK-ORDER-REGISTRY-001 v0.1 | Platform and all affected module-owner acceptance; total lock order and two-writer PostgreSQL failure/race evidence |
| Canonical audit event / durable outbox | MDG-ORGANIZATION-AUDIT-EVENT-OUTBOX-CONTRACT-001 v0.1 | AuditSecurity event schema, canonical hash, durable ACK, worker identity, retry/quarantine/replay and retention acceptance |
| Organization ID generation | MDG-PLATFORM-IDENTIFIER-POLICY-REQUEST-001 v0.1 | Platform decision on UUID version/generator/clock behavior/multi-instance compatibility and evidence |
| Security threats | MDG-SECURITY-O20E-THREAT-MODEL-001 v0.1 | Independent Security review, threat-to-test mapping and exact-SHA retest |

These are design requests, not accepted contracts. No cross-module interface, lock order, UUID version, event transport or threat control is asserted as implemented. No DDL, migration, source Organization module, database grant or production worker is authorized.

The existing IdentityAccess HTTP session and actor path is acknowledged, but `IdentityAccessDtos.cs`, `ModuleContractVersion.Current` and internal `IRolePermissionStore` do not satisfy the missing Organization-facing public authorization contract. See contract proposal v0.5 for the source-level crosswalk.

**Disposition:** named cross-owner proposals prepared; owner/platform/security acceptance and technical proof remain OPEN. O20-E OPEN; G1 NOT GRANTED.

**END — Physical design proposal v0.7 cross-contract addendum**


## 19. Exact source inventory and owner binding dependencies — 2026-11-22 (v0.8)

The current RowScope point-in-time revalidation is now recorded in `MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 v0.4` at source head `3322d7738a576fc17f4b5168713dc4a68410ae87`. It confirms no executable Organization, Inventory or Reporting module and no Branch/Warehouse reporting/export consumer. PermissionCatalog now contains the approved `reports.*` keys, but no source-row RowScope path exists. ReportingReadOnly remains unprovisioned; RowScope runtime tests remain NOT APPLICABLE to absent consumers.

The trusted Product Owner binding proposal is `MDG-IDENTITYACCESS-PRODUCT-OWNER-PRINCIPAL-BINDING-001 v0.1`. It separates owner authority from roles and permissions and identifies bootstrap, change, recovery, cardinality, authority ownership and versioned runtime decision as open. The accepted Product Owner self-assignment exception remains narrow; it does not supply the binding implementation.

Related cross-owner proposals remain non-normative: Platform Lock-Order Registry v0.1, Organization Audit Event/Outbox Contract v0.1, Platform Identifier Policy Request v0.1 and O20-E Threat Model v0.1.

**Disposition:** exact source topology revalidated; owner binding, public contracts, Platform/AuditSecurity decisions and future RowScope source consumers remain OPEN. No runtime, DDL, migration, grant or G1 authorization.

**END — physical design proposal v0.8 source/binding addendum**


## 20. Assignment maker-checker and Product Owner policy proof — 2026-11-22 (v0.9)

Added disposable PostgreSQL test `O20OrganizationAssignmentApprovalPostgresProofTests`. It seeds isolated synthetic principals and a synthetic trusted owner binding, then verifies:
- non-owner self-assignment is denied even when an untrusted caller-supplied owner hint is true;
- non-owner assignment request remains PENDING and creates no active assignment until a second person approves;
- actor and subject cannot approve; a distinct active approver can approve;
- `super_admin` role plus forged owner hint does not create Product Owner authority;
- a principal present in the trusted owner-binding table can execute direct self/other assignment without second-person approval;
- the direct path records `PRODUCT_OWNER_DIRECT`, binding version and audit intent.

The test is policy PoC only. It seeds the binding directly, so it does not prove bootstrap/enrollment, owner-binding rotation/recovery, production IdentityAccess contract, authorization permission checks, command idempotency/version check, cross-module transaction or canonical AuditSecurity ACK. It creates no production Organization module/schema or grants.

A dedicated explicit PostgreSQL CI workflow step has been added for this test. Test PASS must be confirmed against the exact latest SHA before it is marked complete.

**Disposition:** maker-checker/owner exception policy now has a focused disposable PostgreSQL proof; exact-head CI pending. Trusted owner binding lifecycle and all public contracts remain OPEN. O20-E OPEN; G1 NOT GRANTED.

**END — physical design proposal v0.9**


## 21. Local audit outbox and assignment idempotency PoCs — 2026-11-22 (v1.0)

Two additional disposable PostgreSQL integration proofs are now present and explicitly invoked by the PR workflow.

### 21.1 Local outbox / canonical acknowledgement simulation

`tests/MDGsm.Domain.Tests/Security/O20OrganizationAuditOutboxPostgresProofTests.cs` verifies:
- aggregate fact and local outbox intent commit in one transaction;
- the transaction rollback removes both fact and intent;
- an unavailable remote service leaves local intent in PENDING;
- a remote durable acceptance followed by lost local response can be replayed with the same EventId/hash and returns the same ACK;
- reusing EventId with a changed payload hash records a quarantine conflict without overwriting the original canonical event;
- two competing workers using `FOR UPDATE SKIP LOCKED` do not claim the same row.

The test fixture's `canonical_audit_event` table/function is a synthetic remote-service simulation inside a marked disposable database. It is not the real AuditSecurity module, Object Lock, durable external trust anchor, platform key service, worker deployment or operational alerting.

### 21.2 Concurrent command/idempotency proof

`tests/MDGsm.Domain.Tests/Security/O20OrganizationIdempotencyConcurrencyPostgresProofTests.cs` verifies:
- two independent writers replaying the same idempotency key and request fingerprint resolve to one durable assignment result;
- reuse of the same key with a different fingerprint is denied;
- different idempotency keys attempting the same Account/Warehouse assignment converge on one active assignment ID.

The PoC demonstrates one local PostgreSQL race shape only. It is not the Platform-wide Lock-Order Registry, does not cover cross-module revocation or owner-binding races, and does not establish production authorization or an accepted global transaction order.

### 21.3 CI boundary

The workflow now explicitly invokes the outbox and idempotency proof classes. These new tests are added after the previously inspected successful O20-E run; their exact latest-head CI result is pending. No PASS is claimed for these new tests until full CI logs are retrieved.

**Disposition:** local outbox/idempotency PoC coverage expanded; AuditSecurity acceptance, Platform lock order, WORM/key custody, production integration and independent review OPEN. O20-E OPEN; O20/O21 not closed; G1 NOT GRANTED.

**END — physical design proposal v1.0 local outbox/idempotency addendum**
