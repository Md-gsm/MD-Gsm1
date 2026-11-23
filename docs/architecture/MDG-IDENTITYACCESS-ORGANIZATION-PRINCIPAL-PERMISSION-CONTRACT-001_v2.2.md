# IdentityAccess - Organization Principal, Permission, and Transaction Contract

| Field | Value |
|---|---|
| Document ID | MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 |
| Version / Date | 2.2 / 2026-11-23 |
| Supersedes | v2.1 |
| Public IdentityAccess Contracts API version | 1.2 |
| Branch | security/o20-o21-final-closure |
| Status | IDENTITYACCESS OWNER ACCEPTANCE RECORDED FOR IDENTITYACCESS-OWNED API 1.2 BASELINE; ORGANIZATION CONCURRENCE, ORGANIZATION PERMISSION MAPPING, PLATFORM LOCK-ORDER / LEASE-EXPIRY ACCEPTANCE, EXACT-HEAD CI, AND INDEPENDENT REVIEW OPEN |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |
| Related physical design | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v1.5 |

## 1. Purpose and authority boundary

This is the consolidated cross-module contract and acceptance package for IdentityAccess and the future Organization consumer. It records the public API that is already present in source, its tested behavior, and the decisions still required before production use.

IdentityAccess exclusively owns Account, Credential, Session, Role, Permission, permission grants, and their persistence. Organization owns Branch/Warehouse masters and assignment facts. Organization must consume only the versioned IdentityAccess.Contracts assembly; it must never query IdentityAccess private tables or reference IdentityAccess Domain, Application, or Infrastructure implementation types.

This document does not grant Organization G1 and does not authorize a production Organization module, schema, migration, endpoint, permission grant, or runtime consumer.

## 2. Published IdentityAccess Contracts API

The source-published contract is:
- Assembly: src/Modules/IdentityAccess/Contracts
- Interface: IIdentityAccessAuthorizationContract
- Current marker: ModuleContractVersion.Current = 1.2
- Contract source: IdentityAccessAuthorizationContract.cs
- Public documentation: src/Modules/IdentityAccess/Contracts/README.md

The interface exposes four operations:

1. ResolveCurrentPrincipalAsync: resolves principal metadata only from the host's validated server-side request context. It accepts no caller-supplied AccountId, actor ID, role, owner flag, token, or session secret.
2. EvaluateCurrentPermissionAsync(permissionKey): evaluates one exact key against the current server principal and live IdentityAccess account/session/credential/grant facts.
3. EvaluateCurrentPermissionInTransactionAsync(permissionKey, connection, transaction): re-resolves the current principal and evaluates the exact permission using the caller's already-open connection and transaction. The transaction must belong to that same open connection.
4. ResolveCurrentProductOwnerAuthorityAsync: returns a typed Product Owner binding decision. The current implementation intentionally returns Unavailable with owner_binding_not_implemented because the protected installation binding and lifecycle are not implemented.

The typed decision families distinguish:
- Authenticated / Unauthenticated / Unavailable principal resolution;
- Allowed / Denied / Unavailable permission decisions;
- BoundOwner / NotOwner / Unavailable Product Owner decisions.

No decision exposes bearer tokens, credential hashes, recovery secrets, or private persistence entities.

## 3. Request-time permission semantics

A request-time permission decision is tied to the AccountId, SessionId, credential version, enabled account, non-revoked and unexpired session, and exact active permission grant established by IdentityAccess.

Unknown permission keys are Denied. A missing or invalid validated session is Denied. Resolver, persistence, or dependency failures are Unavailable. Neither Denied nor Unavailable can become Allow through a role name, wildcard, cached stale result, client claim, or fallback path.

A request-time decision is a point-in-time read. It is not commit-time authorization for a mutating cross-module command.

## 4. Caller-transaction permission guard

The transaction-scoped method requires:
- an already-open DbConnection;
- a DbTransaction belonging to that exact connection;
- the same local transaction to persist the protected mutation, immutable history, and local audit-intent/outbox;
- the current principal sourced only from the validated server-side request context.

The IdentityAccess-owned PostgreSQL query rechecks:
- exact AccountId and SessionId;
- enabled account state;
- current account credential version and session issue credential version;
- non-revoked session and unexpired lease at the guard evaluation point;
- exact active permission key;
- active account-role membership, role, role-permission grant, and permission.

For an Allow decision, the query uses FOR SHARE over the rows that establish the decision. The adapter drains and validates the complete result set before returning Allow, so a multi-role permission union does not leave later contributing membership/grant rows unlocked. The locks remain until the caller transaction commits or rolls back.

Transaction/connection mismatch, unknown key, absent/stale principal, missing permission, row identity inconsistency, and persistence failure return Denied or Unavailable as appropriate. Cancellation propagates; it is not converted to Allow.

This mechanism is a reusable IdentityAccess authorization participant. It does not by itself approve a global cross-module lock order or authorize a production Organization writer.

## 5. Disposable PostgreSQL evidence and exact-head baseline

The PR branch includes tests against a marked disposable PostgreSQL IdentityAccess schema. The transaction guard proof exercises the actual IdentityAccess Infrastructure adapter, not a replacement mock.

The relevant proof covers a principal holding the same permission through two distinct roles. While one caller transaction retains the complete permission-row lock set, seven independent competing mutations attempt to:
- revoke the session;
- disable the account or change its credential version;
- remove the first role membership;
- remove the second role membership;
- remove the first role-permission grant;
- remove the second role-permission grant;
- deactivate the permission.

The proof observes actual blocking relationships through PostgreSQL pg_blocking_pids() before allowing the protected transaction to finish. The same disposable transaction also records synthetic command receipt, approval, Branch, Warehouse, assignment, immutable history, and local outbox facts. Those synthetic tables are test fixtures only; they are not an Organization module or production schema.

Baseline exact-head evidence before this document successor:
- PR source/test SHA: 43d995306227d2e463d75328dfbb4cb144d14bd2
- Persons Module Gates run 45135179767: SUCCESS, 13/13 jobs
- PostgreSQL job 137559662174: 62/62 base integration tests PASS; explicit IdentityAccess live-session, revocation-linearization, and transaction-scoped guard proofs PASS
- CodeQL run 45135179652: SUCCESS
- Windows Preview Installer run 45135179709: SUCCESS
- Project Charter Owner Gate run 45135179658: SUCCESS

This is baseline CI evidence for the SHA above only. Because this successor changes controlled documentation and SHA256SUMS.txt, all required workflows must be rerun on the resulting new PR head before that new head is called CI-PASS.

## 6. Authorization composition for future Organization commands

Permission and Organization assignment scope are separate requirements. Product Owner binding is a third, separately protected fact. They must not be collapsed into one role or one boolean.

For every protected Organization assignment command:
- the authenticated principal must be resolved by IdentityAccess;
- the exact command-specific IdentityAccess permission must be Allowed;
- the target Branch/Warehouse and account facts must be validated by Organization and the owning data source;
- the command's reason, idempotency, expected version, concurrency, history, audit-intent, and transaction rules must pass.

For a non-owner actor, self-assignment is denied and a distinct second approver is required; that approver must be neither the initiating actor nor the subject account.

For the designated Product Owner, the trusted immutable installation binding must independently resolve to the same current authenticated AccountId/session. The owner may directly create/change/revoke Branch/Warehouse assignments for the owner's own account or any other account without a second approver. The Product Owner still requires the exact command-specific IdentityAccess permission. Owner binding is not a permission grant and never substitutes for permission.

The direct-owner exception removes only the non-owner self-assignment and second-approver restrictions. It does not bypass authentication, permission, resource validation, data ownership, RowScope, idempotency, concurrency, immutable history, audit, or transaction requirements.

This composition reconciles the owner-approved O20-E data-ownership policy v0.7 and logical Organization design v0.7. The contradictory OR wording in physical design v1.4 is superseded by physical design v1.5.

## 7. Permission namespace and role mapping remain unapproved

At the source baseline, PermissionCatalog contains no organization.* keys and no reporting.* keys. Existing generic keys include reports.read, reports.export, and reports.print. Those generic keys do not implement Organization RowScope and must not be treated as a branch/warehouse scope decision.

A prior proposal listed the following candidate Organization keys for review only:
- organization.branches.read
- organization.branches.manage
- organization.warehouses.read
- organization.warehouses.manage
- organization.assignments.read
- organization.assignments.create
- organization.assignments.change
- organization.assignments.revoke
- organization.assignments.approve

These names are not approved, are not present in PermissionCatalog, and are not seeded or granted. The earlier reporting.* candidates must be reconciled against the existing reports.* vocabulary; no duplicate reporting.* namespace should be introduced without an explicit IdentityAccess owner decision and Organization concurrence.

The complete command-to-key matrix and role eligibility remain open. In particular:
- no wildcard or role-name inference is allowed;
- the Product Owner binding does not imply SuperAdmin or any permission grant;
- the installation-created Product Owner account's initial exact permission, if any, must be a separate controlled and auditable IdentityAccess decision;
- the current PermissionCatalog maps SuperAdmin to every key in its catalog, so adding an Organization key to the catalog may silently expand SuperAdmin authority unless the complete matrix is intentionally reviewed and accepted;
- System Manager, DBA, Service, Worker, Backup, Recovery, and ANCHOR_WRITER identities remain separate authority categories.

No permission key, role mapping, seed, or grant is authorized by this document.

## 8. RowScope and data classification

A permission is necessary but not sufficient for scoped business data. Effective report/read access remains:

EffectiveAccess = Permission AND OrganizationScope AND DataClassification

An empty Organization scope denies. IdentityAccess permission scope cannot broaden Organization assignment scope. A caller-supplied BranchId/WarehouseId, role name, or Employment record cannot create an assignment.

Organization must resolve the authoritative active Branch/Warehouse assignment; the owning source module must provide authoritative row context; queries must apply RowScope before materialization across list, search, detail, count, pagination, aggregates, dashboards, reports, print, export, and API surfaces. Sensitive export and print remain separately permissioned and audited.

The accepted Warehouse composition is: an exact active Warehouse assignment plus an active Warehouse and its current valid Branch relationship. A parent Branch assignment is not additionally required for Warehouse-scoped access.

## 9. Platform lock order and lease-expiry semantics remain open

The transaction guard's local FOR SHARE locks and disposable proof do not make the Platform Lock-Order Registry v0.7 normative. That registry remains a proposal. Platform and every affected data owner must accept one total resource order and the relevant isolation, linearization, timeout, deadlock, cancellation, bounded retry, rollback, crash, unknown-commit, and replay semantics before any production cross-module writer uses the guard.

A specific timing decision is also outstanding. The current SQL checks session lease_expires_at_utc > clock_timestamp() when the guard query executes. Row locks prevent concurrent account/session/role/grant changes from committing first, but wall-clock time continues to advance. The existing guard does not by itself decide whether a protected mutation may commit after the session lease expires if the guard passed before expiry.

IdentityAccess and Platform must explicitly define the authorization linearization point, maximum protected transaction duration, required remaining lease margin, and whether expiry is judged at guard acquisition or must remain valid through commit. Until that policy is accepted and tested, no production consumer may rely on the transaction guard for mutating Organization commands.

## 10. Pre-G1 and post-G1 sequencing

The approved modular rule states that a production module is introduced only after its G1 authorization. The current source tree has no executable Organization module, Organization UnitOfWork, schema, or production consumer.

Therefore, before G1, the permitted work is limited to:
1. IdentityAccess module-owner acceptance of the IdentityAccess-owned API 1.2 semantics and transaction guard is RECORDED in `MDG-IDENTITYACCESS-MODULE-BASELINE-001 v1.0`; this does not accept Organization consumer semantics or global lock order;
2. Organization data-owner concurrence on the consumer-facing semantics;
3. explicit IdentityAccess owner acceptance of any Organization-specific permission namespace, command matrix, and role mapping remains OPEN;
4. Platform and affected module-owner acceptance of the total lock order and lease/revocation linearization semantics;
5. isolated disposable PostgreSQL proof and independent exact-SHA Security/Architecture review.

Production Organization module code, schema, migrations, permissions/grants, and a production transaction participant/consumer are POST-G1 implementation work and must not be treated as pre-G1 prerequisites or created early.

## 11. Required acceptance record

The following cross-module acceptances are tracked separately. IdentityAccess's own API 1.2 baseline acceptance is recorded in `MDG-IDENTITYACCESS-MODULE-BASELINE-001 v1.0`; the remaining items below are still open:

| Accountable owner | Required explicit acceptance |
|---|---|
| IdentityAccess module owner | **RECORDED for API 1.2:** server-validated principal source, exact permission evaluation, typed fail-closed outcomes, caller-transaction guard and local row-lock semantics, error handling, and public version marker. **OPEN for cross-module use:** lease validity through commit / transaction duration, Organization permission namespace and role mapping, and any global lock order. |
| Organization data owner | Ownership split, assignment command semantics, RowScope composition, Warehouse scope rule, Product Owner narrow direct path, and post-G1 consumer obligations |
| Platform owner | Global lock ranks, transaction participant order, linearization, timeout/deadlock/cancellation/retry/recovery behavior, and transaction-duration/lease relationship |
| Security reviewer | Threat-model review of principal spoofing, permission/role mapping, Product Owner binding fail-closed behavior, revocation races, and evidence boundary |
| Product Owner | Separate G1 decision only after all required technical, module, Platform, security, legal, repository, and independent-review gates pass |

The IdentityAccess owner acceptance above is evidenced by the controlled module baseline document and its owner-attributed commit. It does not replace Organization data-owner concurrence, Platform lock-order acceptance, exact-head CI, or independent Security/Architecture review.

## 12. Non-authorization and disposition

The public IdentityAccess API and transaction guard source are published. The IdentityAccess owner has accepted the IdentityAccess-owned API 1.2 baseline as recorded in `MDG-IDENTITYACCESS-MODULE-BASELINE-001 v1.0`. The bounded disposable PostgreSQL proof and CI evidence listed in section 5 apply only to their stated historical SHAs. Organization concurrence, Organization-specific permission namespace/role mapping, Product Owner binding lifecycle, Platform-wide lock order, lease-expiry-through-commit semantics, production Organization integration, legal controls, latest exact-head CI and independent review remain open.

No production Organization runtime, schema, DDL, migration, permission seed/grant, Product Owner binding, emergency grant, worker credential, WORM/KMS/HSM resource, reporting grant, data deletion, G1 authorization, or Go-Live is created or authorized.

**Disposition:** source and baseline tests are documented; cross-module acceptance is NOT COMPLETE. O20-E, O-20, and O-21 remain OPEN. Organization G1 is NOT GRANTED.

**END - MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 v2.2**
