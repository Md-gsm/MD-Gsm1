# O20-E Data Ownership Resolution — Owner-Accepted Assignment Policy

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 |
| Version / Date | 0.6 / 2026-11-23 |
| Supersedes | v0.5 |
| Supersedes | v0.4 |
| Status | OWNER-ACCEPTED POLICY INCLUDING OD-05 RETENTION BASELINE; DETAILED IMPLEMENTATION AND G1 NOT AUTHORIZED |
| Governing baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Related decisions | OWN-INV-018 v1.1; IdentityAccess permission-matrix approval; O20-E logical and physical design; O20-E records retention policy v0.6 |

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
- retention duration by record class/jurisdiction, Legal Hold authority/scope/release, lawful disposal workflow, and treatment of backups/replicas/exports (the default retain/no-automatic-disposal baseline is accepted);
- future reportable source-module RowScope contracts and query/export coverage.

These are not permission to invent implementation details. Each must be closed by its accountable owner or technical authority before the dependent runtime design is finalized.

## 6. Authority and non-authorization

This document records owner policy only. It does not constitute independent review, G1 authorization, implementation acceptance, migration authorization, database grant, operational release or Go-Live approval.

No Organization runtime, schema, table, DDL, migration, seed, role, permission grant, resolver, endpoint, reporting view, production identity or production database change is authorized or performed by this document.

**O20-E remains OPEN. Overall O-20 remains OPEN.**

**END — MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.3**


## 7. Controlled clarification accepted by Product Owner — 2026-11-22 (v0.4 successor)

This section supersedes the conflicting self-assignment sentence in v0.3. The exact controlling rule is:

1. For every actor other than the designated Product Owner, assigning or expanding access for the actor's own AccountId is prohibited.
2. **The designated Product Owner is exempt from the self-assignment prohibition.** The Product Owner may create, change or revoke the Product Owner's own Branch/Warehouse assignments and those of any other account.
3. The Product Owner performs these operations directly without second-person approval. This is not self-approval; it is a separately authorized owner execution path.
4. A non-owner's create/change/revoke operation requires approval by a distinct authenticated account. The approver must be neither the initiating actor nor the subject AccountId. An actor cannot approve their own operation.
5. Product Owner authority is resolved only from a protected server-side owner-principal binding. Neither a client claim, username, role name, super_admin, GitHub identity nor request field can establish the exception.
6. The exception is confined to Organization assignment administration. It does not grant account impersonation, credential/session visibility, password reset, IdentityAccess role/permission mutation or bypass of the exact-resource assignment predicate.
7. Every owner operation, including self-assignment, remains authenticated, reasoned, idempotent, version-checked and auditable. The event must explicitly record PRODUCT_OWNER_DIRECT, actor and subject (which may be equal), and that no second approver was required.

**Normative replacement:** “تخصیص دسترسی به حساب خود فرد ممنوع است؛ این محدودیت شامل مالک سیستم نمی‌شود.” The Product Owner is the only exception. All other accepted v0.3 policy clauses remain unchanged.

**Authority boundary:** this records the Product Owner's policy decision; trusted principal enrollment/recovery and technical enforcement remain G1 prerequisites. It does not authorize runtime, DDL, migration, grants, G1 or Go-Live.

**END — MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.4**


## 8. Controlled records-retention decision — 2026-11-22 (v0.5)

OD-05 in `MDG-SECURITY-O20E-O20F-OWNER-DECISION-ACCEPTANCE-001 v0.2` is OWNER-ACCEPTED. The governing records policy is `MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.1` and supersedes any earlier statement that the entire retention baseline is awaiting owner decision.

The accepted baseline is: retain Organization assignment and Branch/Warehouse relationship history by default; do not physically delete or automatically purge; do not run expiry, anonymization, irreversible masking or cryptographic erasure until a lawful, versioned retention schedule and disposal process have been approved; dispose only after the applicable retention period has elapsed, no Legal Hold covers the record, and documented authorization from the accountable records/legal authority exists. An active or unresolved hold blocks disposal.

The duration is deliberately unset. Jurisdiction-specific schedules, Legal Hold authority/scope/release, backup/replica/export handling, privacy treatment and technical disposal enforcement remain open. Until those items are resolved, no automatic deletion is allowed.

This addendum does not change the Product Owner assignment exception in section 7, does not make Organization history the canonical AuditSecurity ledger, and does not authorize G1, runtime, DDL, migration, grants, production access or disposal.

**END — MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.5**


## 9. Current-law retention crosswalk correction — 2026-11-23 (v0.6)

The controlling OD-05 baseline is unchanged. The current legal-source crosswalk has been refined in `MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.6`:
- VAT Executive Regulation Article 103 provides record-class-specific periods (ordinary tax records/books 5 years from tax-period end; capital-asset records 5 years after the adjustment-period-ending tax period; real-estate records 15 years from tax-period end; possible written extension up to 5 additional years). The retrieved bilingual PDF labels its English translation unofficial; Persian primary text and applicability must be confirmed by counsel.
- The old ten-year Article 34 of the 1387 VAT Act must not be carried forward as current: Article 55 of the 1400 VAT Act repeals the 1387 Act's VAT provisions subject to listed exceptions, and Article 34 is not one of the listed exceptions.
- Commercial Code Article 13's ten-year rule attaches to the specified commercial books, subject to Article 6 and the Article 19 small-trader classification; it is not a universal term for Organization operational history.
- AML Law Article 7 sets a five-year minimum for legally covered persons and preserves longer duties. The 1404 AML amendments to Articles 149 and 158 affect the non-financial profession/activity boundary. The specific effect on a mobile retail/repair operator remains for Iran counsel/regulator classification. Article 142's ten-year financial-institution rule is not inferred to apply to all operators or all Organization history.

These are scoped statutory crosswalk observations, not an approved period for AccountBranchAssignment, AccountWarehouseAssignment or Warehouse-Branch relationship history. No numeric schedule is accepted by this addendum.

OD-05 still requires retain-by-default/no-automatic-deletion. Disposal remains blocked until the actual operator, legal form, activity, exact record class, triggering event, applicable statute, extension/hold rules and accountable disposal authority are documented and approved. The newer source crosswalk v0.6 supersedes any broad or incomplete reading of the prior v0.5 summary; it does not supersede the accepted owner retention baseline.

**Disposition:** owner retention policy remains ACCEPTED; operator-specific schedule and operational Legal Hold OPEN; O20-E OPEN; G1 NOT GRANTED.

**END — MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.6**
