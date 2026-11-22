# O20-E Organization G1 — Preliminary Technical Readiness Review

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 |
| Version / Date | 1.7 / 2026-11-22 |
| Supersedes | v1.6 |
| Supersedes | v1.5 |
| Supersedes | v1.4 |
| Supersedes | v1.3 |
| Supersedes | v1.2 |
| Supersedes | v1.1 |
| Supersedes | v1.0 |
| Supersedes | v0.9 |
| Supersedes | v0.8 |
| Supersedes | v0.7 |
| Status | PRELIMINARY CROSS-CONTRACT REVIEW — INITIAL PERMISSION CATALOG SOURCE-ALIGNED; PUBLIC CONTRACT/ORGANIZATION PERMISSIONS AND G1 GATES OPEN |
| Repository | Md-gsm/MD-Gsm1 |
| Exact source baseline inspected | `e10c96d23a4f132e53381aa30b00e5757ffdc254` |
| Reviewed proposal | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v1.0 |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.5 |
| Current RowScope inventory | MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 v0.4 |

## 1. Executive disposition

The owner-accepted ownership boundary and the additional decisions accepted on 2026-11-22 are internally consistent with the current Master Architecture and Inventory ownership decision. The physical proposal has been revised to reflect them, but it is not yet a final implementation contract or eligible for Organization G1 authorization.

The exact-head source discovery confirms that Organization, Inventory and Reporting runtime modules and Branch/Warehouse report consumers are still absent. The current IdentityAccess Contracts directory has no published runtime contract. The repository contains no Organization permission keys, no published Organization assignment API, and no AuditSecurity module contract for the proposed outbox integration.

**Disposition: NOT READY FOR G1.** Keep RowScope fail-closed, create no Organization source module/schema/migration/grant/view, do not request independent reviewer approval yet, and do not merge PR #86.

This is an internal preliminary cross-contract review, not an independent reviewer attestation and not owner approval.

## 2. Governing evidence rechecked

1. `MDG-ARCH-MASTER-001 v3.0.3` assigns Employee/Employment, Branch/Warehouse/Location masters to Organization; states that Application coordinates Use Case/Unit of Work without owning business data; prohibits direct access to another module's private tables; requires cross-module physical FK only through an explicit ADR; requires a Lock-Order Registry and real concurrency/deadlock/retry tests; and assigns canonical shared AuditEvent/SecurityEvent to AuditSecurity.
2. `OWN-INV-018 v1.1` confirms Organization owns Warehouse/location masters and the Organization → Warehouse → Section → Shelf → Bin/StorageLocation hierarchy, while Inventory owns stock facts and movements. It does not establish Warehouse-to-Branch cardinality or reassignment semantics.
3. `MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.2` records the four owner-accepted rules: Organization owns both assignment facts; Employment alone grants no access; multiple active assignments are permitted; Branch assignment does not imply Warehouse access and Warehouse assignment must be explicit.
4. `identityaccess-permission-matrix-approval-fa.md` establishes deny-by-default, independent permission scopes, explicit grant semantics and the need for an independent resource-assignment check. The approval is policy only, not runtime or G1 authorization.
5. `src/Modules/README.md` says modules are introduced only after G1 authorization.
6. The current recursive source tree and API route inventory are recorded in RowScope Source Inventory v0.2.

## 3. Cross-contract review findings

### 3.1 Ownership and dependency direction — ALIGNED

Organization is the correct owner for Branch/Warehouse master data and explicit AccountBranchAssignment / AccountWarehouseAssignment facts. IdentityAccess remains the owner of Account, Credential, Session, Role and Permission. Inventory remains the owner of stock facts. No cross-module FK into `identity.accounts` should be introduced; AccountId must remain an opaque identifier in Organization.

No ownership change is required. The current logical boundary is consistent with the governing records.

### 3.2 Branch-to-Warehouse cardinality — OWNER DECISION OPEN

Neither Master v3.0.3 nor OWN-INV-018 v1.1 establishes whether a Warehouse has exactly one current Branch, whether reassignment is allowed, or how the historical relationship is preserved. A `NOT NULL branch_id` and local FK in the physical proposal are therefore still proposals, not established business policy.

Recommended option for owner consideration: exactly one current Branch per Warehouse; allow reassignment only through a separate authorized, audited Organization command; preserve relationship history; and explicitly decide whether existing Warehouse assignments survive reassignment or must be revoked/reconfirmed. No DDL may be finalized until accepted.

### 3.3 Business codes and master lifecycle — OWNER DECISIONS OPEN

No governing source defines BranchCode/WarehouseCode normalization, uniqueness scope, case rules, editability, code reuse after archive, or the complete Branch/Warehouse state machine.

Recommended option for owner consideration:
- BranchCode is unique across the Organization.
- WarehouseCode is unique within its current Branch.
- Codes are normalized before uniqueness checks; codes are not silently reused after archival.
- Use explicit ACTIVE / INACTIVE / ARCHIVED states; archived records are never physically deleted, and archive transition prerequisites are explicit.

The normalization algorithm and transition table must be defined before indexes/CHECK constraints are approved. These recommendations are not accepted policy.

### 3.4 Assignment effective dates and reactivation — OWNER DECISION OPEN

The accepted policy requires explicit active assignments and revocation but does not define future-effective starts, scheduled expiry, reactivation-in-place or history retention.

Recommended first-tranche option: assignments take effect immediately; no scheduled start or automatic expiry; revocation is terminal; a later re-grant creates a new AssignmentId and a new auditable lifecycle. Owner acceptance is required before this becomes the physical lifecycle contract.

### 3.5 Assignment administration and separation of duties — OWNER POLICY ACCEPTED; TECHNICAL BINDING OPEN

Owner policy v0.3 resolves PD-ORG-05:
- Self-assignment is prohibited for every actor, including the designated Product Owner.
- Every assignment create/change/revoke command initiated by a non-owner requires approval by a second, distinct authenticated person before the state transition takes effect. The approver cannot be the initiating actor or the subject account.
- The designated Product Owner may create/change/revoke Branch/Warehouse assignments for any other account without second-person approval.
- The exception is limited to Organization assignment administration; it does not permit impersonation, credentials/session access, password reset or mutation of IdentityAccess roles/permissions.
- Product Owner operations remain authenticated, reasoned, idempotent and fully auditable. The exception removes only the second approver.
- Product Owner authority must be bound to a specific trusted server-side principal. It is not inferred from a request, username convention, GitHub login or the broad `super_admin` role.

The exact owner-principal binding/enrollment/recovery process and approval-pending transaction model remain G1 prerequisites. No runtime exception may be implemented until that binding is independently reviewed.

### 3.6 IdentityAccess permission and scope integration — CATALOG SOURCE-ALIGNED; PUBLIC CONTRACT STILL BLOCKED

The owner-approved policy requires an IdentityAccess permission and an independent Organization assignment predicate. IdentityAccess scope may only narrow; it must never broaden Organization's explicit assignment set. Branch-only assignment must never generate Warehouse access.

At exact source snapshot `db338fbfd94306de086c18be3eb07193acee3b3c`, `PermissionCatalog.cs` contains the complete owner-approved initial permission-name set and role grants, plus the existing separately documented `persons.sensitive.read` control granted only to super_admin. The test `tests/MDGsm.Domain.Tests/IdentityAccess/ApprovedPermissionCatalogMatrixTests.cs` compares the exact matrix catalog and all ten role grant sets, and verifies the sensitive permission is super_admin-only. The approved dashboard/report subset is also covered by the focused test `ApprovedDashboardReportPermissionCatalogTests.cs`. The unapproved `persons.delete`, `finance.voucher.delete`, `reporting.*` and `organization.*` keys are not recognized or granted. This is source alignment; exact-head CI verification remains pending.

Still open:
- Organization assignment-management permission keys and role mapping are not included in the approved matrix and remain absent. No new `organization.*` key is authorized by this review.
- Branch/Warehouse report isolation uses the approved `reports.*` permission conjunctively with Organization assignment and source-side SQL RowScope; no new `reporting.*` permission is to be invented.
- `src/Modules/IdentityAccess/Contracts/README.md` still states that no runtime public contract is published. `ModuleContractVersion.cs` is only a version marker. The Organization module cannot read IdentityAccess private tables or depend on internal implementation types as a substitute for that contract.

The IdentityAccess ↔ Organization contract proposal v0.3 records the complete matrix alignment but remains a proposal. IdentityAccess module-owner acceptance, runtime contract publication, trusted Product Owner binding/enrollment/recovery, interface implementation and concurrent session-revocation linearization remain open. No Organization permission key, role grant, seed or database privilege is authorized by this review.

### 3.7 Warehouse scope composition / PD-ORG-13 — OWNER-ACCEPTED

Owner policy v0.3 explicitly accepts that an exact active AccountWarehouseAssignment plus an active Warehouse and its valid active current Branch relationship is sufficient for Warehouse-scoped access. A separate AccountBranchAssignment to that parent Branch is not additionally required. IdentityAccess permission remains independently necessary. Branch assignment alone still never grants Warehouse access. The logical and physical design successors both incorporate this decision.

### 3.8 AuditSecurity and outbox — OWNERSHIP ALIGNED; CONTRACT OPEN

Master v3.0.3 establishes AuditSecurity as the sole owner of canonical shared AuditEvent/SecurityEvent. Organization may retain domain-specific assignment lifecycle history, but it must not create a competing canonical security-audit ledger.

The repository does not expose a published AuditSecurity event/outbox contract, durable acknowledgement semantics, worker identity, retry/quarantine contract or retention policy. Therefore the proposed `assignment_outbox` fields are placeholders only. AuditSecurity/Platform must accept the event envelope, stable EventId/digest semantics, acknowledgement/retry behavior, access boundary and retention before the outbox table can be finalized.

### 3.9 Identifier generation — OPEN

IdentityAccess migration 0003 uses PostgreSQL `uuid` for AccountId, and Master v3.0.3 explicitly names PartyId as UUIDv7. That does not establish a global UUIDv7 generation policy for BranchId, WarehouseId, AssignmentId or event IDs. Storage representation and server-side generation policy must be confirmed by Platform Architecture; do not silently extend PartyId's rule to all Organization identifiers.

### 3.10 Transaction, Unit of Work and lock order — OPEN

Master v3.0.3 makes Application the Use Case/Unit-of-Work coordinator and requires a documented Lock-Order Registry plus real PostgreSQL race/deadlock/retry evidence. The current `IdentityAccessUnitOfWork` is a bounded IdentityAccess implementation and is not an approved Organization/cross-module transaction contract.

The proposed assignment/master/source lock order is a target outcome, not a validated protocol. Platform/Application Architecture must define the transaction boundary and global lock order, and PostgreSQL integration tests must prove revocation versus protected writes, serialization, rollback and deadlock behavior. Normal reads must bind the authorization set and source rows to a consistent query snapshot; exports must revalidate before delivery.

### 3.11 Assignment history retention — CORE POLICY OWNER-ACCEPTED; LEGAL/TECHNICAL DETAILS OPEN

OD-05 is owner-accepted and recorded in `MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.1`: retain Organization assignment and Branch/Warehouse relationship history by default; no physical deletion or automatic purge; disposal only after the applicable approved legal retention period has elapsed, no Legal Hold covers the record, and documented authorization from the accountable records/legal authority exists. The duration remains deliberately unset. Legal Hold authority/scope/release, jurisdiction-specific schedule, privacy obligations, backup/replica/export handling, lawful disposal workflow and technical enforcement remain open. Until those are approved, automatic deletion is disabled.

### 3.12 Current RowScope source coverage — SNAPSHOT REVALIDATED; FUTURE COVERAGE OPEN

Source Inventory v0.2 confirms there is currently no Organization, Inventory or Reporting runtime module and no Branch/Warehouse report/export consumer in the API host. The only current Party detail route has no Branch/Warehouse source context. Therefore positive/negative RowScope PostgreSQL tests cannot yet be run against a real consumer; marking them PASS would be false.

PD-ORG-12 is closed only for the inspected current repository snapshot. It automatically reopens for each new reportable source/query/export surface until that surface is inventoried and tested.

## 4. Consolidated decision and readiness matrix

| Item | Current disposition | Required closure |
|---|---|---|
| PD-ORG-01 Warehouse-to-Branch relation | OWNER-ACCEPTED with bounded detail OPEN | Exactly one current Branch; controlled reassignment; retain history; decide existing assignment effect after reassignment |
| PD-ORG-02 Branch/Warehouse codes | OWNER-ACCEPTED with bounded detail OPEN | BranchCode globally unique; WarehouseCode unique in current Branch; required, normalized, not reused after archive; exact algorithm/editability OPEN |
| PD-ORG-03 Master lifecycle | OWNER-ACCEPTED with bounded detail OPEN | ACTIVE/INACTIVE/ARCHIVED; no physical delete; transition prerequisites OPEN |
| PD-ORG-04 Assignment lifecycle | OWNER-ACCEPTED | Immediate activation; no future dating/automatic expiry; revocation terminal; regrant new AssignmentId |
| PD-ORG-05 Actor/reason/separation | OWNER-ACCEPTED with technical binding OPEN | No self-assignment; non-owner requires distinct second approver; Product Owner may manage any other account without second approval; trusted owner binding OPEN |
| PD-ORG-06 Permission keys/grants | OPEN — IdentityAccess/security + owner | Reconcile owner matrix and catalog; publish contract; approve role mapping |
| PD-ORG-07 IdentityAccess scope composition | OWNER POLICY ACCEPTED; CONTRACT OPEN | Permission AND Organization assignment; IdentityAccess scope narrows only; publish compatible versioned contract |
| PD-ORG-08 Audit event/outbox | PARTIAL | AuditSecurity/Platform publish and accept durable event/ACK contract |
| PD-ORG-09 Identifier generation | OPEN — Platform | Accept Organization ID representation/generation/version policy |
| PD-ORG-10 Transaction/lock order | OPEN — Platform/Application | Publish Lock-Order Registry and pass PostgreSQL concurrency PoC |
| PD-ORG-11 History retention/legal hold | OPEN — owner/legal | Accept retention, legal-hold and disposal policy |
| PD-ORG-12 Source RowScope inventory | CURRENT SNAPSHOT VERIFIED | Re-run for every new reportable source/query/export surface |
| PD-ORG-13 Branch prerequisite for Warehouse scope | OWNER-ACCEPTED | No parent Branch assignment required when exact Warehouse assignment and active Warehouse/current Branch facts are valid |

## 5. G1 package still required

Organization G1 remains NOT GRANTED. Before a G1 request can be considered, the package still needs:

1. Close the remaining bounded details of owner-accepted PD-ORG-01..05: treatment of existing Warehouse assignments after reassignment, code normalization/editability, lifecycle prerequisites, approval-pending protocol and trusted Product Owner principal binding. PD-ORG-13 is resolved by owner policy v0.3.
2. IdentityAccess/security reconciliation of PD-ORG-06/07, including a published, versioned principal/permission contract and a permission-catalog reconciliation plan.
3. AuditSecurity/Platform contract acceptance for PD-ORG-08.
4. Platform/Application Architecture acceptance for PD-ORG-09/10, including the ID-generation and lock-order boundaries.
5. The Organization module public dependency/Contracts design, threat model, final object-level privilege matrix, migration reservation request through WG-0, and complete acceptance-test specification.
6. Independent Architecture/Security review against one exact final SHA, with findings and retest evidence.
7. A separate explicit owner decision `AUTHORIZE G1` against that exact independently reviewed SHA.

G1 authorization is not implied by accepting the four O20-E assignment policy rules, approving this preliminary review, passing CI, or merging a document-only PR.

## 6. Non-authorization boundary

This review performs no implementation or operational authorization. No `src/Modules/Organization` directory, schema, table, DDL, migration, seed, role, permission grant, resolver, API, reporting view, ReportingReadOnly privilege, production identity or production database change is authorized or created.

O20-E remains OPEN; O-20 remains OPEN. PR #86 must remain unmerged until the full Stage 08 closure, exact-head CI, independent reviewer approval and owner acceptance are satisfied.

**END — MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 v0.2**


## 7. Successor review — 2026-11-22 (v0.3)

### 7.1 Policy correction

The Product Owner self-assignment prohibition in v0.2 is superseded: the designated Product Owner may assign/change/revoke the Product Owner's own access and any other account's access without a second approver. Non-owners cannot self-assign and require a distinct second approver for every assignment create/change/revoke. All actions remain server-authorized and audited. Owner binding must be a trusted server-side principal registration, not a role/client claim/username/GitHub identity.

### 7.2 Detailed-design disposition

| Item | Successor disposition | Remaining gate |
|---|---|---|
| Warehouse reassignment | Existing assignments stay bound to exact WarehouseId; Warehouse must be INACTIVE; inactive denies; reactivation restores only still-active exact assignments | Owner acceptance of detailed behavior; lock-order and PostgreSQL concurrency proof |
| Business codes | NFC + trim + reject remaining whitespace/control/format; case-sensitive bytewise C collation; historical reservations retained | Owner acceptance; .NET/PostgreSQL equivalence tests and collision migration plan |
| Lifecycle | ACTIVE/INACTIVE/ARCHIVED; inactive denies; archive terminal and requires dependent assignments/children resolved | Owner acceptance and transition tests |
| Assignment approvals | Non-owner PENDING_APPROVAL then distinct approver; atomic revalidation/effect; Product Owner direct path incl. self | Trusted owner binding, expiry/retention and concurrency proof |
| IdentityAccess contract | Current principal AccountId is server-derived; no public versioned contract exists; PermissionCatalog lacks organization keys | IdentityAccess owner must publish/accept v1 contract and permission matrix |
| AuditSecurity | Local transactional audit-intent outbox; canonical EventId/durable ACK owned by AuditSecurity | AuditSecurity owner acceptance and delivery/recovery PoC |
| RowScope | No current reportable source route/module; no reporting grant; source snapshot revalidated | Future consumer tests before any reporting access |
| ID / lock order / records | UUID storage proposed; ID version and global lock order open; core retention policy OWNER-ACCEPTED with duration/hold/disposal details open | Platform and records/legal decisions plus PostgreSQL proof |

### 7.3 IdentityAccess/Organization contract gate

The existing IdentityAccess contracts README says no runtime public contract is published. The API currently derives actor AccountId from the authenticated server session and uses the permission store, but that implementation is not a published Organization-consumable contract. PermissionCatalog has no organization.* keys. No Organization permission grant, role mapping, module, schema or API is authorized. A versioned contract and permission matrix must be accepted before G1.

### 7.4 O20-F emergency access status

O20-F has a separate successor design proposal (O20-F v0.1). It specifies a fail-closed emergency allow-list lifecycle, two-person activation, bounded one-use window, immediate revocation, independent audit intent and disposable proof requirements. It does not create a LOGIN, credential, grant, membership, production access or runtime. Until the disposable PostgreSQL proof and independent security review pass, O20-F remains OPEN and overall O-20 remains OPEN.

### 7.5 G1 decision

**NOT READY FOR G1.** The logical and physical design have progressed, but owner acceptance of detailed proposals, IdentityAccess public contract/permission reconciliation, trusted Product Owner binding, AuditSecurity acceptance, Platform ID/lock-order decisions, retention schedule/Legal Hold implementation, PostgreSQL concurrency and outbox/emergency proofs, threat-model closure and independent exact-SHA review remain prerequisites. No Organization runtime, DDL, migration, role/grant, ReportingReadOnly access or feature activation is authorized.

**END — MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 v0.3**


## 8. Controlled review successor — OD-05 retention acceptance (2026-11-22)

The Product Owner approved OD-05. The owner decision acceptance record v0.2 and records-retention policy v0.1 now establish the baseline: preserve Organization assignment and Branch/Warehouse relationship history by default; physical deletion and automatic purge are not allowed; lawful disposal requires expiry of an approved applicable retention schedule, no active Legal Hold, and documented authorization from the accountable records/legal authority. The duration remains unset.

This closes the owner-policy decision component of PD-ORG-11 only. It does **not** close retention schedule/legal analysis, hold authority and technical enforcement, backup/replica/export disposition, lawful-disposal workflow or test evidence.

Current O20-E blockers remain: IdentityAccess public contract/permission catalog acceptance, trusted Product Owner binding/enrollment/recovery, AuditSecurity durable ACK/outbox acceptance, Platform identifier policy and global Lock-Order Registry, PostgreSQL concurrency and canonicalization/lifecycle/RowScope evidence where applicable, threat-model closure, and independent exact-SHA review.

**Disposition: O20-E OPEN; Organization G1 NOT READY / NOT GRANTED.** No runtime, DDL, migration, permission grant, ReportingReadOnly access or production change is authorized.

**END — MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 v0.4**


## 9. Controlled permission-catalog reconciliation — 2026-11-22

The owner-approved dashboard/report subset has been reconciled in `PermissionCatalog.cs` and protected by a focused test. This is a bounded implementation change, not full completion of the owner-approved permission matrix. Existing organization assignment-management keys remain unapproved and absent. The contract proposal v0.2 withdraws unapproved `reporting.*` names and composes existing `reports.*` permission with independent Organization RowScope.

No database seed, role assignment, permission grant, migration, production identity or Organization runtime was changed. The IdentityAccess public principal/permission contract is still not published; Product Owner trusted binding is still not implemented or proved.

**Disposition:** PD-ORG-06 PARTIAL; PD-ORG-07 OPEN; O20-E OPEN; Organization G1 NOT READY / NOT GRANTED. CI on the exact updated head and independent review are still required.

**END — MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 v0.5**


## 10. Controlled full PermissionCatalog reconciliation — 2026-11-22

The current branch replaces the incomplete permission catalog with the full product-owner-approved initial role/permission mapping and preserves the existing separate sensitive-person authorization key. The new golden test compares the complete matrix key set and exact grants for all ten initial roles, and separately verifies that persons.sensitive.read is super_admin-only. Unapproved permission strings are expressly asserted unknown and denied. Exact-head CI is pending; no PASS is claimed until that run finishes.

This closes the source-definition component of the initial IdentityAccess role matrix only. It does not publish the IdentityAccess ↔ Organization public contract, introduce Organization-specific management permissions, implement the trusted Product Owner binding, grant database permissions, implement report RowScope or prove session/revocation concurrency.

**Disposition:** PD-ORG-06 INITIAL CATALOG SOURCE-ALIGNED; CI TEST VERIFICATION PENDING; PD-ORG-07 OPEN; O20-E OPEN; Organization G1 NOT READY / NOT GRANTED.

**END — MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 v0.6**


## 11. Controlled supplemental sensitive-person gate preservation — 2026-11-22

The previous code/test/documentation established `persons.sensitive.read` as a distinct authorization requirement for sensitive Persons fields, limited to `super_admin`. The complete role matrix reconciliation retains that separate boundary; ordinary `persons.read` does not imply sensitive-field access. The full golden test explicitly protects this rule.

This is preservation of an existing documented security boundary, not a grant expansion. The unapproved old `persons.delete` and `finance.voucher.delete` entries remain removed. Exact-head CI is still pending.

**Disposition:** full approved role catalog plus sensitive-person permission source-aligned; public IdentityAccess contract remains unpublished; Organization permission names/roles remain unresolved; O20-E OPEN; G1 NOT GRANTED.

**END — MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 v0.7**


## 12. Controlled Warehouse Branch-history test addition — 2026-11-22

The current branch adds `O20OrganizationRelationshipHistoryPostgresProofTests` to the marked disposable PostgreSQL integration suite. It verifies append-only event history for Warehouse-to-Branch relationship changes, rejection of active-Warehouse reassignment and inactive-destination reassignment, rollback-preservation after rejection, same-WarehouseId continuity and denial of history UPDATE/DELETE.

The already completed exact-head CI run `45015818229` on `83b4c5ac4c40c4a7eaee5a54e0a354e9923eb549` reported:
- Release build succeeded with 0 warnings and 0 errors;
- .NET unit suite 235/235 passed;
- Linux PostgreSQL integration suite 41/41 passed;
- O20-E disposable PostgreSQL policy PoC methods OD-01/OD-02/OD-03 passed 2/2;
- SHA256SUMS validation and spec lint passed;
- Organization runtime remained absent and no production DDL/grants were created.

That CI run predates the newly added immutable relationship-history test. The test and updated manifest are on the newer branch head; a fresh exact-head CI run is required before marking it PASS. Windows PostgreSQL migrations, CodeQL and Windows preview installer statuses also must be inspected for the final head.

**Disposition:** OD-01/02/03 base PoCs are CI-PASS at the bounded prior SHA; relationship-history append-only proof is added but pending exact-head CI. No production authorization. O20-E OPEN; G1 NOT GRANTED.

**END — O20-E readiness review v0.8**


## 13. Cross-owner technical proposal inventory — 2026-11-22 (v0.9)

Four previously untracked technical dependencies now have controlled, explicitly non-normative artifacts:

- Platform Lock-Order Registry candidate v0.1: lists Organization aggregate resources and a 12-case concurrent/failure experiment matrix. It deliberately does not select a global lock order; Master v3.0.3 requires that order to be proven and Platform-approved.
- Organization Audit Event/Outbox contract proposal v0.1: defines candidate local atomic intent, event envelope, stable EventId/hash, at-least-once delivery, durable ACK, quarantine and reconciliation. It does not claim AuditSecurity has accepted it.
- Platform Identifier Policy Request v0.1: records UUIDv4/UUIDv7/platform abstraction alternatives; no global UUIDv7 rule is inferred from PartyId.
- O20-E Threat Model v0.1: records trust boundaries, 15 threat scenarios, required controls and future evidence mapping. Independent Security review has not been performed.

The IdentityAccess contract proposal v0.5 also crosswalks the actual repository state: live HTTP session validation and an HTTP actor context exist; the authorization policy document exists; the Contracts project contains login/password/session DTOs and a `1.0` version marker. However, neither these DTOs nor the host actor context nor internal `IRolePermissionStore` is a published Organization-facing principal/permission/Product Owner authority interface. The exact public contract and owner-binding lifecycle remain open.

These proposals convert dependencies into explicit review/acceptance artifacts; they do not close their technical or organizational gates. There is still no approved global lock order, AuditSecurity durable ACK contract, Platform ID generator, trusted Product Owner binding, production Organization schema/module, or current RowScope consumer.

**Disposition:** design and threat inventory expanded; cross-owner acceptance and executable proofs OPEN; O20-E OPEN; Organization G1 NOT READY / NOT GRANTED.

**END — O20-E readiness review v0.9**


## 14. Exact-head RowScope inventory and owner-binding proposal — 2026-11-22 (v1.0)

### 14.1 Current RowScope consumer discovery

`MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 v0.4` revalidates the recursive source tree and API composition root at exact head `3322d7738a576fc17f4b5168713dc4a68410ae87`. The only executable modules are Accounting, Finance, IdentityAccess, Persons and RepairOperations. No Organization/Inventory/Reporting runtime module or Branch/Warehouse query/report/export route is present. The Persons Party detail route has no authoritative BranchId/WarehouseId source context.

The reports.* permissions are now present in PermissionCatalog, but permission alone does not establish RowScope. There is no current Organization consumer for positive/negative RowScope PostgreSQL tests. Keep ReportingReadOnly absent/unprovisioned and repeat inventory for every future read/query/report/export/worker path.

### 14.2 Trusted Product Owner binding

`MDG-IDENTITYACCESS-PRODUCT-OWNER-PRINCIPAL-BINDING-001 v0.1` is a new IdentityAccess/Platform/Security design proposal. It records the exact trust boundary and test set, but binding cardinality, aggregate owner, installation scope, first enrollment, evidence ceremony, independent change/recovery, emergency disable and restoration remain OPEN. It does not make the owner exception role-based or self-asserted.

### 14.3 Updated O20-E disposition

Proposals are now tracked for:
- IdentityAccess public principal/permission/owner decision contract (v0.6);
- Product Owner protected-principal binding (v0.1);
- Platform global lock-order (v0.1);
- Organization AuditSecurity event/outbox integration (v0.1);
- Platform ID policy (v0.1);
- O20-E threat model (v0.1);
- exact-head current source/RowScope inventory (v0.4).

The first six are design artifacts only and await their accountable owner/security acceptance and executable evidence. The RowScope inventory is a verified snapshot only; no current consumer can be marked passed. Exact-head CI is pending for the latest branch tip.

**Disposition:** design gaps made explicit; no technical owner gates are claimed closed beyond the bounded source alignment already described. O20-E OPEN; Organization G1 NOT READY / NOT GRANTED.

**END — O20-E readiness review v1.0**


## 15. Assignment approval and Product Owner path proof — 2026-11-22 (v1.1)

A dedicated disposable PostgreSQL policy proof was added at `tests/MDGsm.Domain.Tests/Security/O20OrganizationAssignmentApprovalPostgresProofTests.cs`. It tests non-owner self-assignment denial, pending maker request, actor/subject approver rejection, independent approver success, role/client-owner-hint non-escalation, and direct self/other assignment for a principal seeded in the protected owner-binding fixture. Direct actions carry `PRODUCT_OWNER_DIRECT`, binding version and a local audit-intent record.

The proof is deliberately bounded: it seeds a synthetic owner binding and principals. It does not prove enrollment/recovery, actual IdentityAccess contract, permission-key authorization, idempotency/version control, canonical AuditSecurity delivery or production runtime. The workflow now has an explicit step for this proof; exact-head CI result is pending.

**Disposition:** bounded maker-checker/owner-exception PostgreSQL proof added; exact-head validation required. Trusted binding lifecycle, permission contract, Platform/AuditSecurity acceptance, lock order, RowScope consumers, retention legal detail and independent review remain open. O20-E OPEN; G1 NOT GRANTED.

**END — O20-E readiness review v1.1**


## 16. Public IdentityAccess authorization interface — source implementation status (2026-11-22; v1.2)

A published cross-module type now exists at contract version 1.1, with Host API implementation and focused tests:
- Contracts interface: `IIdentityAccessAuthorizationContract`;
- host adapter: `IdentityAccessAuthorizationContractAdapter`;
- request context: existing validated `IAuthenticatedActorContext`;
- permission source: existing live `IRolePermissionStore`;
- failure semantics: unknown permission Denied, missing session Denied, store outage Unavailable;
- Product Owner binding result: always Unavailable until a protected binding store and accepted lifecycle are implemented.

This is no longer an absent-interface gap. It remains an unaccepted/unconsumed integration contract until the IdentityAccess owner reviews it and a future Organization consumer depends only on the Contracts assembly. The interface does not permit arbitrary AccountId evaluation or accept a client owner flag.

The latest prior CI run on SHA `d5f098822f4750d4cce481f303b39126989aa60f` passed the foundation 235/235, Linux PostgreSQL integration 43/43, OD-01/02/03 proof 2/2, relationship-history proof 1/1, maker-checker policy proof 1/1, Windows migration and CodeQL. That SHA predates the new public-contract source and has a checksum-manifest failure. Therefore those results do not validate v1.1 source; exact-head CI remains pending.

**Disposition:** interface/adapter SOURCE-PUBLISHED; latest contract adapter test and exact-head CI PENDING; Product Owner authority UNAVAILABLE; IdentityAccess owner acceptance/Organization consumer OPEN; O20-E OPEN; G1 NOT GRANTED.

**END — O20-E readiness review v1.2**


## 17. O20-E outbox and assignment concurrency evidence expansion — 2026-11-22 (v1.3)

Two additional disposable PostgreSQL PoCs have been added and inserted into the explicit PR workflow:

1. **Outbox/ACK:** local fact+outbox atomicity, rollback, pending intent during remote outage, same EventId/hash replay to stable synthetic durable ACK, changed-hash quarantine, and competing-worker `SKIP LOCKED` claim exclusion.
2. **Assignment idempotency concurrency:** two writers with same key/fingerprint resolve to one assignment; changed fingerprint on same key fails; distinct keys for same Account/Warehouse converge on one assignment ID.

Both tests are isolated to a marked disposable PostgreSQL database. The synthetic canonical audit receiver is not the production AuditSecurity system and no production Organization outbox/schema is created. The race test is local to one aggregate shape and does not accept a Platform-wide order.

The workflow now explicitly invokes:
- `O20OrganizationAuditOutboxPostgresProofTests`;
- `O20OrganizationIdempotencyConcurrencyPostgresProofTests`;
- `AuditSecurityHashChainPostgresProofTests` for the O-21 PoC.

The successful prior run `45017785129` on SHA `d5f098822f4750d4cce481f303b39126989aa60f` predates these latest source additions and had only checksum/evidence-gate failure. The new public IdentityAccess contract, adapter tests, outbox/idempotency PoCs and workflow step changes require a new exact-head CI result.

**Disposition:** more local executable coverage added; exact latest CI NOT YET VERIFIED; AuditSecurity and Platform contract acceptance, global lock order, trusted Product Owner binding, production schema/runtime and independent review remain OPEN. O20-E OPEN; G1 NOT GRANTED.

**END — O20-E readiness review v1.3**


## 18. Current ordered blocker disposition — 2026-11-22 (v1.4)

| Order | Blocker | Current evidence | Remaining acceptance |
|---:|---|---|---|
| 1 | IdentityAccess request principal/permission contract | Public Contracts interface v1.1 and Host API adapter exist; focused unit tests added | Exact-head build/test, IdentityAccess owner review and Organization consumer integration |
| 2 | Organization assignment authorization and Product Owner exception | Maker-checker/owner-policy disposable PostgreSQL proof passed at SHA d5f0988; current adapter still returns owner Unavailable | Protected owner binding storage, bootstrap/enrollment, change/rotation/recovery, revocation and commit-time linearization |
| 3 | Permission matrix | Full ten-role catalog and golden matrix source/test exist; prior exact-head foundation run passed | Latest exact-head CI and module-owner acceptance; no organization.* keys approved |
| 4 | Organization immutable history | Dedicated WarehouseId-bound append-only PostgreSQL proof passed at SHA d5f0988 | Production schema/privileges and legal retention/Legal Hold |
| 5 | Assignment idempotency/concurrency | New two-writer PostgreSQL PoC added | Latest exact-head CI; cross-module Platform lock registry/race suite |
| 6 | Audit event/outbox | New local outbox/ACK simulation added | Latest exact-head CI; AuditSecurity-owned durable ACK/event API, worker, retry/quarantine/reconciliation and source-module contract acceptance |
| 7 | Platform IDs/lock order | Candidate decision requests and race matrix; one local Organization sorted-row two-writer PG PoC added | Latest CI; Platform owner acceptance, generator/version decision and total cross-module order plus the remaining two-writer race matrix |
| 8 | RowScope | Exact source inventory shows no current Branch/Warehouse query/report/export consumer | Re-inventory and positive/negative SQL RowScope tests for every future consumer; no ReportingReadOnly grant |
| 9 | Retention/Legal Hold | OD-05 default-retain/no-auto-delete policy accepted | Jurisdiction/record-class schedule, legal authority, hold lifecycle, media handling and fail-closed proof |
| 10 | O-21 Hash Chain | Disposable PoC source and explicit CI step exist; owner-approved SHA-256/RSA-3072/RSA-PSS/WORM policy is recorded | Latest exact-head test; production writer/worker, external key service, Object Lock Compliance, verifier, restore continuity, alerts/runbook and independent review |
| 11 | Independent review / G1 | No independent exact-SHA review accepted | Independent Architecture/Security review of one exact final head; all findings closed or formally accepted; owner G1 authorization |

The CI run 45017785129 at SHA d5f098822f4750d4cce481f303b39126989aa60f demonstrated:
- foundation .NET tests 235/235 passed;
- Linux PostgreSQL integration 43/43 passed;
- OD-01/02/03 PoCs 2/2 passed;
- immutable Warehouse relationship history 1/1 passed;
- maker-checker/owner policy PoC 1/1 passed;
- application migration first-run 8 migrations and second-run 0 new migrations passed;
- Windows migration, CodeQL and Windows preview installer passed.

That run failed the source-checksum lint because late files were not listed and the workflow checksum was stale. The downstream evidence gate therefore failed. The manifest was corrected afterward, then additional IdentityAccess contract, adapter tests, outbox and idempotency source/tests were added; no CI result for those later exact-head changes is claimed yet.

**Disposition:** the table is a current work plan, not a closure declaration. O20-E, O-20 and O-21 remain OPEN; Organization G1 NOT READY / NOT GRANTED.

**END — O20-E readiness review v1.4**


## 19. Organization-local lock-order proof addition — 2026-11-22 (v1.5)

A marked disposable PostgreSQL test now executes two concurrent writers with opposite caller input order. Both canonicalize the resource list using the same proposed UUID ordering, acquire the rows in that order and update the rows. The explicit PR workflow step runs `O20OrganizationDeterministicLockOrderPostgresProofTests`.

This is one local order experiment, not a Platform-wide Lock-Order acceptance. The test does not establish cross-module ordering, owner-binding changes, permission/session revocation, cross-module multi-aggregate races, worker/domain lock interaction or finance/inventory ordering. Exact-head CI is required before this new test is marked PASS.

**Disposition:** local Organization deterministic row-lock experiment added; Platform global order and broader race evidence OPEN; O20-E OPEN; G1 NOT GRANTED.

**END — O20-E readiness review v1.5**


## 20. Live-session permission conjunction — implementation evidence added (2026-11-22; v1.6)

The IdentityAccess contract v0.8 records an additional source-level control:
- a validated request principal carries opaque AccountId, SessionId, credential/principal version and authentication timestamp;
- the middleware installs those claims only after PostgreSQL session resolution and successful lease renewal;
- the public permission adapter evaluates exact active permission and live session/account/credential state in one PostgreSQL statement using the same SessionId and account;
- unknown permission keys deny and persistence errors return Unavailable;
- commit-time revocation ordering remains unproven.

This materially improves the prior adapter, which evaluated a current role/permission grant separately from the authenticated session snapshot. It still does not establish commit-time authorization ordering against concurrent session/permission revocation because the query is a statement snapshot and does not hold an authorization lock through a future Organization write.

The explicit PostgreSQL proof is added to the PR workflow. Exact-head CI has not yet validated these changes. The full 41-case IdentityAccess runtime contract, Organization consumer, Organization permission names and trusted Product Owner binding remain open.

**Disposition:** live-session-bound permission decision SOURCE-IMPLEMENTED; exact-head CI PENDING; commit-time revocation linearization OPEN; Product Owner binding OPEN; O20-E OPEN; G1 NOT GRANTED.

**END — O20-E readiness review v1.6**


## 21. IdentityAccess adapter module-boundary correction — 2026-11-22 (v1.7)

The public authorization adapter is now owned by IdentityAccess Infrastructure, not the API Host. Host implements only the validated-principal accessor port using claims created by SessionAuthenticationMiddleware. IdentityAccess Infrastructure contains permission catalog evaluation and PostgreSQL live-session/grant query. PersistenceRegistration wires the public contract. When no connection string is configured, a fail-closed Unavailable store is registered rather than leaving the public interface absent.

This corrects a module-boundary defect in the earlier placement and ensures Organization can depend only on the public Contracts assembly. Latest exact-head CI is required to validate all project references and architecture tests.

**Disposition:** module boundary corrected in source; exact-head CI PENDING; Product Owner binding and commit-time revocation linearization OPEN; O20-E OPEN; G1 NOT GRANTED.

**END — O20-E readiness review v1.7**
