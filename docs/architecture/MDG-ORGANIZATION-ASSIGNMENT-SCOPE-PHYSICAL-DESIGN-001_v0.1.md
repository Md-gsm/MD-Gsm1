# Organization Assignment Scope — PostgreSQL Physical Design Proposal

| Field | Value |
|---|---|
| Document ID | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 |
| Version / Date | 0.1 / 2026-11-22 |
| Status | PHYSICAL DESIGN PROPOSAL — OWNER POLICY ACCEPTED; DETAILED DECISIONS AND INDEPENDENT REVIEW OPEN; G1 NOT AUTHORIZED |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.2 |
| Logical design | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.1 |
| Related ownership contract | OWN-INV-018 v1.1 |
| Scope | Organization-owned Branch/Warehouse references and explicit Account-to-Branch / Account-to-Warehouse assignment storage required for O20-E |
| Explicit exclusions | Runtime module, C# contracts, EF mappings, DDL, migration, seed, grants, API, reporting view, feature activation and production operation |

## 1. Authority and interpretation

This document advances the logical design to a reviewable PostgreSQL physical-design proposal for the O20-E slice. It is subordinate to MDG-ARCH-MASTER-001 v3.0.3 and the four owner-accepted rules in O20E data ownership v0.2.

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
6. For a Warehouse-scoped row, an explicit Warehouse assignment is mandatory and is evaluated against the current active Warehouse and its current valid active Branch relationship. Whether a matching active Branch assignment is an additional prerequisite is not explicitly settled by owner policy v0.2. The current logical design v0.1 treats the explicit Warehouse assignment plus active Warehouse/Branch masters as sufficient, but that remains a design interpretation, not a separately recorded owner decision. PD-ORG-13 therefore remains OPEN; no runtime may encode either interpretation before confirmation.
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
| branch_code | text | NOT NULL | Business code; normalization and uniqueness scope OPEN |
| display_name | text | NOT NULL | Human-readable name |
| lifecycle_state | text | NOT NULL | Proposed controlled state; exact state machine OPEN |
| row_version | bigint | NOT NULL | Optimistic concurrency version, starts at 1 |
| created_at_utc | timestamptz | NOT NULL | Server-generated creation time |
| created_by_account_id | uuid | NOT NULL | Opaque actor reference |
| updated_at_utc | timestamptz | NOT NULL | Server-generated last change time |
| updated_by_account_id | uuid | NOT NULL | Opaque actor reference |

PROPOSED constraints: primary key on branch_id; nonblank code/name; row_version >= 1; lifecycle_state constrained to the accepted state vocabulary once defined; no physical DELETE. Whether code is globally unique, case-folded, normalized, user-editable, reusable after archive, or immutable is OPEN and must be decided before final DDL. No legal address, tax identity, cost center, branch phone, manager or accounting dimension is added by this O20-E scope.

### 3.3 Warehouse master — proposed minimum shape

| Column | PostgreSQL type | Nullability | Purpose / constraint proposal |
|---|---|---|---|
| warehouse_id | uuid | NOT NULL | Stable primary key |
| branch_id | uuid | NOT NULL | Current authoritative Branch relationship |
| warehouse_code | text | NOT NULL | Business code; normalization and uniqueness scope OPEN |
| display_name | text | NOT NULL | Human-readable name |
| lifecycle_state | text | NOT NULL | Proposed controlled state; exact state machine OPEN |
| row_version | bigint | NOT NULL | Optimistic concurrency version, starts at 1 |
| created_at_utc | timestamptz | NOT NULL | Server-generated creation time |
| created_by_account_id | uuid | NOT NULL | Opaque actor reference |
| updated_at_utc | timestamptz | NOT NULL | Server-generated last change time |
| updated_by_account_id | uuid | NOT NULL | Opaque actor reference |

PROPOSED constraints: primary key on warehouse_id; restrictive local foreign key from branch_id to branches.branch_id; nonblank code/name; row_version >= 1; no physical DELETE. A Warehouse must not be resolvable as active if its Branch is absent, inactive or inconsistent.

OPEN OWNER DECISION: the logical proposal implies one authoritative Branch relationship for each Warehouse at a time, but the owner has not explicitly confirmed whether Warehouse-to-Branch is exactly-one, whether reassignment is permitted, or the historical semantics of reassignment. The NOT NULL branch_id / local FK above is a proposed one-current-Branch model, not an accepted business rule. No Warehouse DDL can be finalized until this cardinality and reassignment decision is confirmed.

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

Owner policy v0.2 explicitly prohibits inferring Warehouse access from Branch assignment and requires an exact Warehouse assignment. It does not expressly state whether the parent Branch assignment is an additional narrowing prerequisite. The logical design v0.1 currently treats it as unnecessary; this is retained as a proposed interpretation only and remains subject to PD-ORG-13 owner confirmation.

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
| reason | text | NOT NULL | Required reason for the transition |
| occurred_at_utc | timestamptz | NOT NULL | Server-generated UTC timestamp |
| correlation_id | uuid | NOT NULL | Request/operation correlation |
| idempotency_key | uuid | NOT NULL | Stable mutation retry identity |
| request_digest | bytea | NOT NULL | Digest of canonical request; never raw secrets |

The row must reference exactly one of branch_assignment_id and warehouse_assignment_id; the selected FK is restrictive. A unique constraint on the selected assignment identity + aggregate_version prevents duplicate event versions. Event rows are append-only: UPDATE, DELETE and TRUNCATE are prohibited for runtime identities. The event records only identifiers and reason/audit metadata; it must not copy account PII, credentials, tokens or unnecessary source payloads.

### 3.7 Assignment audit outbox

PROPOSED physical table: organization.assignment_outbox. One outbox envelope is inserted in the same transaction as the assignment state change and assignment event.

Minimum fields: outbox_id uuid primary key; assignment_event_id uuid unique restrictive FK; event_id uuid unique; contract_name text; contract_version text; payload_schema_version smallint; payload_hash_algorithm text; payload_hash bytea; payload jsonb; delivery_state text; attempt_count integer; next_attempt_at_utc timestamptz; lease_until_utc timestamptz nullable; created_at_utc timestamptz; acknowledged_at_utc timestamptz nullable.

The payload is an allow-listed envelope containing actor, subject AccountId, assignment identity, resource identity, action, before/after state, reason, aggregate version, correlation and server timestamps. It must not include password, credential, session token, recovery code, full account profile or arbitrary request JSON.

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
- the actor holds the exact approved Organization assignment-management permission;
- required reason and command idempotency metadata are valid;
- the proposed assignment does not duplicate a non-revoked account/resource pair;
- audit event and outbox can be persisted atomically.

Employment is not consulted as a grant source. It may be shown as organizational context, but no employment state creates permission or assignment.

### 6.2 Revocation

Revocation is an explicit Organization command. It changes the current-state row to REVOKED, sets server revocation metadata, increments row_version, appends exactly one event version, and inserts the audit outbox envelope in one database transaction. Repeating the same idempotency key and canonical request returns the original result. Same key with a different digest fails closed. Revocation cannot be silently undone by a retry, login, role change or Employment update.

### 6.3 Reassignment, expiry and reactivation — OPEN

The owner-approved policy says assignments are active and revocable, but does not settle future-effective scheduling, automatic expiry, reactivation identity, or whether a revoked assignment can ever be reactivated. Recommended first-release behavior is immediate effect only; revocation is terminal; a later grant creates a new AssignmentId and event stream. No scheduled activation, scheduled expiry or reactivation-in-place should be implemented until the owner accepts these semantics.

### 6.4 Resource deactivation and Warehouse reassignment — OPEN

An inactive/archived Branch or Warehouse grants no effective row scope regardless of retained assignment rows. Assignment history is preserved. Branch/Warehouse archive transitions and their effects on historical reports require a separate lifecycle decision.

The owner must confirm whether a Warehouse may be reassigned to another Branch. Recommended fail-closed handling is to serialize reassignment with Warehouse assignment resolution and require a controlled, audited Warehouse-master change. Whether existing AccountWarehouseAssignments remain effective after the Branch changes, are invalidated, or require explicit reaffirmation is OPEN; the implementation must not infer this behavior.

## 7. Authorization composition and RowScope enforcement

### 7.1 Independent checks

The effective permission decision is a conjunction of:
1. valid authenticated IdentityAccess principal and session;
2. exact active permission grant for the requested operation (for example read, export or print; exact Organization permission keys are not defined here);
3. a valid Organization assignment for the authenticated AccountId and requested resource scope;
4. authoritative source-row context from the module that owns the reportable business fact;
5. source-state and other domain predicates required by that owner.

An IdentityAccess role name, Employment record, all permission scope, or caller-supplied BranchId/WarehouseId must not bypass the explicit Organization assignment requirement for branch/warehouse-scoped rows. If IdentityAccess supplies an additional narrowing scope, it may not broaden Organization's assignment set. The precise composition, including whether a Warehouse-scoped resource also requires the parent Branch assignment, remains subject to PD-ORG-07 and PD-ORG-13 and must be confirmed before implementation.

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
| PD-ORG-01 | Warehouse-to-Branch cardinality and reassignment | Exactly one current active Branch per Warehouse | Explicit owner acceptance; define history and effect on current Warehouse assignments |
| PD-ORG-02 | Branch/Warehouse business codes | Required nonblank code; uniqueness scope and normalization not assumed | Owner/business rule and collision policy |
| PD-ORG-03 | Branch/Warehouse lifecycle vocabulary | Active and inactive minimum; archive semantics separate | Owner accepts state machine and transition rules |
| PD-ORG-04 | Assignment scheduling / reactivation | Immediate assignment; revocation terminal; regrant creates a new AssignmentId | Owner accepts or defines alternatives |
| PD-ORG-05 | Assignment actor/reason and approval separation | Mandatory reason and server actor; no self-assignment; separate approval for sensitive changes if permission matrix requires | Owner/IdentityAccess security matrix |
| PD-ORG-06 | Exact permission keys and role grants | No key or grant introduced by this design | Reconcile with current IdentityAccess catalog and owner-approved permission matrix |
| PD-ORG-07 | IdentityAccess scope-model composition | Permission grant is necessary; Organization assignment remains an independent mandatory filter; any additional scope only narrows | Independent IdentityAccess/Organization contract review |
| PD-ORG-08 | AuditSecurity event/outbox contract | Local event + outbox in same transaction, canonical AuditEvent owned by AuditSecurity | AuditSecurity/Platform acceptance; durable ACK and retention contract |
| PD-ORG-09 | Stable identifier generation version | uuid storage; generator policy not assumed | Platform UUID policy |
| PD-ORG-10 | Cross-module transaction/lock order | Assignment/master locks before source aggregate locks | Application/Platform Architecture review and PostgreSQL race proof |
| PD-ORG-11 | Assignment-history retention and legal hold | Preserve, no deletion; no duration chosen | Owner and legal review |
| PD-ORG-12 | Full source-module RowScope inventory | Begin with sources that publish authoritative BranchId/WarehouseId contracts; none may be inferred | Inventory all reportable list/search/detail/aggregate/print/export paths and obtain source-owner contracts |
| PD-ORG-13 | Does Warehouse assignment alone authorize Warehouse-scoped rows, or must the account also hold an active assignment to the Warehouse's current parent Branch? | Current logical design v0.1 says Warehouse assignment plus active Warehouse/Branch master facts is sufficient; policy v0.2 explicitly says Branch assignment alone is insufficient but does not explicitly resolve this additional prerequisite | Explicit owner decision; until then, no runtime authorization rule is finalized |

## 13. G1 authorization readiness and execution boundary

The proposed physical design is not complete until all owner decisions in PD-ORG-01 through PD-ORG-12 are resolved or explicitly accepted as bounded open implementation prerequisites. A design review must also confirm the current IdentityAccess permission-scope contract, Application Unit-of-Work boundary, AuditSecurity event contract, UUID policy, cross-module dependency direction, and every source query path that can expose branch/warehouse data.

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
| Organization ownership of AccountBranchAssignment / AccountWarehouseAssignment | OWNER-ACCEPTED in O20E v0.2 |
| Four assignment policy invariants | OWNER-ACCEPTED |
| Logical design | DOCUMENTED in v0.1 |
| O20-E physical design | PROPOSED v0.1; owner decisions and independent review OPEN |
| Organization G1 authorization | NOT GRANTED |
| Organization runtime / DDL / migration | NOT IMPLEMENTED; NOT AUTHORIZED |
| Positive / negative PostgreSQL RowScope evidence | NOT RUN |
| ReportingReadOnly RowScope grants | NOT GRANTED |
| O20-E | OPEN |
| Overall O-20 | OPEN |

**END — MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v0.1**
