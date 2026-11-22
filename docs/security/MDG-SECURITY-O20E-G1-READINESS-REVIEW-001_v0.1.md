# O20-E Organization G1 — Preliminary Technical Readiness Review

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 |
| Version / Date | 0.1 / 2026-11-22 |
| Status | PRELIMINARY CROSS-CONTRACT REVIEW — G1 NOT READY; OWNER/PLATFORM/SECURITY DECISIONS OPEN |
| Repository | Md-gsm/MD-Gsm1 |
| Exact source baseline inspected | `9838250e97be5f362f98e1e82a5e6508cf34cac5` |
| Reviewed proposal | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v0.1 |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |
| Owner policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.2 |
| Current RowScope inventory | MDG-SECURITY-O20E-ROWSCOPE-SOURCE-INVENTORY-001 v0.2 |

## 1. Executive disposition

The owner-accepted ownership boundary and four assignment-policy rules are internally consistent with the current Master Architecture and Inventory ownership decision. The physical proposal is a useful design candidate, but it is not ready to become an implementation contract or receive Organization G1 authorization.

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

### 3.5 Assignment administration and separation of duties — SECURITY/OWNER DECISION OPEN

The approved IdentityAccess matrix supplies role/scope principles but does not define Organization-specific command permissions or self-assignment/approval rules. Every command must use a server-derived actor, exact permission, reason, idempotency and audit. The proposal's prohibition on self-assignment is a security recommendation, not an already accepted Organization policy.

Before G1, decide whether an actor may grant/revoke their own assignments, whether a second distinct approver is required for high-privilege assignment changes, and the exact command permissions/role mapping. Do not infer that `super_admin` automatically bypasses maker-checker controls.

### 3.6 IdentityAccess permission and scope integration — PARTIALLY RESOLVED; IMPLEMENTATION BLOCKED

The owner-approved policy requires an IdentityAccess permission and an independent Organization assignment predicate. IdentityAccess scope may only narrow; it must never broaden Organization's explicit assignment set. Branch-only assignment must never generate Warehouse access.

However:
- The current `PermissionCatalog.cs` has no `organization.*` permissions.
- The current catalog also lacks several report permissions listed in the owner-approved matrix (including `reports.read`, `reports.export` and `reports.print`); this is a policy-to-code reconciliation gap and must not be hidden by inventing fallback permissions.
- `src/Modules/IdentityAccess/Contracts/README.md` states that no runtime contract is published. The Organization module cannot read IdentityAccess private tables or depend on internal implementation types as a substitute for that contract.

Before implementation, reconcile the approved permission matrix with the actual IdentityAccess catalog and publish a versioned server-side principal/permission contract. No new permission key, role grant, seed or database privilege is authorized by this review.

### 3.7 Warehouse scope composition / PD-ORG-13 — OWNER DECISION OPEN

The accepted rules establish that an explicit Warehouse assignment is required and that Branch assignment alone is insufficient. They do not literally state whether the parent Branch assignment is an additional prerequisite after an explicit Warehouse assignment is present.

Recommended interpretation for owner consideration: a valid explicit AccountWarehouseAssignment plus active Warehouse and its valid active current Branch relationship is sufficient for Warehouse scope; a separate AccountBranchAssignment is not additionally required. This preserves the independent nature of the two assignment kinds and avoids converting a Warehouse assignment into an implicit Branch assignment. IdentityAccess permission remains independently necessary.

Do not encode this recommendation until the owner explicitly accepts or rejects it. The decision must update both the logical and physical designs together.

### 3.8 AuditSecurity and outbox — OWNERSHIP ALIGNED; CONTRACT OPEN

Master v3.0.3 establishes AuditSecurity as the sole owner of canonical shared AuditEvent/SecurityEvent. Organization may retain domain-specific assignment lifecycle history, but it must not create a competing canonical security-audit ledger.

The repository does not expose a published AuditSecurity event/outbox contract, durable acknowledgement semantics, worker identity, retry/quarantine contract or retention policy. Therefore the proposed `assignment_outbox` fields are placeholders only. AuditSecurity/Platform must accept the event envelope, stable EventId/digest semantics, acknowledgement/retry behavior, access boundary and retention before the outbox table can be finalized.

### 3.9 Identifier generation — OPEN

IdentityAccess migration 0003 uses PostgreSQL `uuid` for AccountId, and Master v3.0.3 explicitly names PartyId as UUIDv7. That does not establish a global UUIDv7 generation policy for BranchId, WarehouseId, AssignmentId or event IDs. Storage representation and server-side generation policy must be confirmed by Platform Architecture; do not silently extend PartyId's rule to all Organization identifiers.

### 3.10 Transaction, Unit of Work and lock order — OPEN

Master v3.0.3 makes Application the Use Case/Unit-of-Work coordinator and requires a documented Lock-Order Registry plus real PostgreSQL race/deadlock/retry evidence. The current `IdentityAccessUnitOfWork` is a bounded IdentityAccess implementation and is not an approved Organization/cross-module transaction contract.

The proposed assignment/master/source lock order is a target outcome, not a validated protocol. Platform/Application Architecture must define the transaction boundary and global lock order, and PostgreSQL integration tests must prove revocation versus protected writes, serialization, rollback and deadlock behavior. Normal reads must bind the authorization set and source rows to a consistent query snapshot; exports must revalidate before delivery.

### 3.11 Assignment history retention — OWNER/LEGAL DECISION OPEN

The proposal correctly prohibits ordinary hard deletion, but the retention duration, legal-hold trigger, archival behavior, privacy constraints and authorized disposal process are not established. Do not invent a retention period or purge worker. Obtain an owner/legal policy before implementing retention or deletion.

### 3.12 Current RowScope source coverage — SNAPSHOT REVALIDATED; FUTURE COVERAGE OPEN

Source Inventory v0.2 confirms there is currently no Organization, Inventory or Reporting runtime module and no Branch/Warehouse report/export consumer in the API host. The only current Party detail route has no Branch/Warehouse source context. Therefore positive/negative RowScope PostgreSQL tests cannot yet be run against a real consumer; marking them PASS would be false.

PD-ORG-12 is closed only for the inspected current repository snapshot. It automatically reopens for each new reportable source/query/export surface until that surface is inventoried and tested.

## 4. Consolidated decision and readiness matrix

| Item | Current disposition | Required closure |
|---|---|---|
| PD-ORG-01 Warehouse-to-Branch relation | OPEN — owner | Accept cardinality, reassignment and history semantics |
| PD-ORG-02 Branch/Warehouse codes | OPEN — owner/business | Accept normalization, uniqueness, edit/reuse policy |
| PD-ORG-03 Master lifecycle | OPEN — owner/business | Accept state machine and archive prerequisites |
| PD-ORG-04 Assignment effective lifecycle | OPEN — owner | Accept immediate/expiry/reactivation semantics |
| PD-ORG-05 Actor/reason/separation | OPEN — owner/security | Accept self-assignment and maker-checker rules |
| PD-ORG-06 Permission keys/grants | OPEN — IdentityAccess/security + owner | Reconcile owner matrix and catalog; publish contract; approve role mapping |
| PD-ORG-07 IdentityAccess scope composition | PARTIAL | Preserve permission AND Organization assignment; finalize versioned contract |
| PD-ORG-08 Audit event/outbox | PARTIAL | AuditSecurity/Platform publish and accept durable event/ACK contract |
| PD-ORG-09 Identifier generation | OPEN — Platform | Accept Organization ID representation/generation/version policy |
| PD-ORG-10 Transaction/lock order | OPEN — Platform/Application | Publish Lock-Order Registry and pass PostgreSQL concurrency PoC |
| PD-ORG-11 History retention/legal hold | OPEN — owner/legal | Accept retention, legal-hold and disposal policy |
| PD-ORG-12 Source RowScope inventory | CURRENT SNAPSHOT VERIFIED | Re-run for every new reportable source/query/export surface |
| PD-ORG-13 Branch prerequisite for Warehouse scope | OPEN — owner | Explicitly decide whether Warehouse assignment alone is sufficient |

## 5. G1 package still required

Organization G1 remains NOT GRANTED. Before a G1 request can be considered, the package still needs:

1. Owner closure of PD-ORG-01 through PD-ORG-05, PD-ORG-11 and PD-ORG-13 (including an explicit written decision for Warehouse scope composition).
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

**END — MDG-SECURITY-O20E-G1-READINESS-REVIEW-001 v0.1**
