# IdentityAccess–Organization Authorization and Lease Acceptance Proposal

| Field | Value |
|---|---|
| Document ID | MDG-IDENTITYACCESS-ORGANIZATION-AUTHORIZATION-ACCEPTANCE-PROPOSAL-001 |
| Version / Date | 0.1 / 2026-11-23 |
| Status | PRE-G1 TECHNICAL PROPOSAL — NOT ACCEPTED / NOT EXECUTABLE |
| Related contract | MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 v2.1 |
| Public contract API | IdentityAccess.Contracts v1.2 |
| Branch | security/o20-o21-final-closure |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |

## 1. Purpose and authority boundary

This proposal turns the unresolved Organization command-permission namespace, role eligibility, and IdentityAccess session-lease linearization questions into an explicit technical package for module-owner review.

It is a pre-G1 design artifact only. It does not add permission keys to source, approve role grants, create a Product Owner binding, provision a seed/database grant, designate an unverified canonical Branch/Warehouse data owner, create an Organization module/UnitOfWork/schema, or authorize a production consumer.

IdentityAccess, Organization, and Platform must accept the parts they own. Product Owner policy decisions already recorded in PR #92 remain controlling; this proposal does not request that the Product Owner repeat them.

## 2. Controlling accepted constraints

1. Product Owner is the installation-created, immutable, non-transferable principal. Recovery may restore only the same AccountId/principal; there is no successor or replacement.
2. Product Owner may directly create, change, or revoke Branch/Warehouse assignments for the Product Owner's own account or any other account without a second approver.
3. The Product Owner direct path still requires authentication, the exact active IdentityAccess command permission, authoritative Branch/Warehouse validation, applicable RowScope, idempotency, concurrency, immutable history, audit intent, and the accepted transaction protocol.
4. Every non-owner actor is prohibited from self-assignment and requires a distinct authenticated second approver for assignment create/change/revoke. The approver must be neither the actor nor the subject account.
5. Product Owner binding is not a role, permission, wildcard, or fallback. It bypasses only the non-owner self-assignment and second-approver restrictions for the stated assignment commands.
6. IdentityAccess owns Account, Session, Role, Permission, and permission grants. Organization owns its Branch/Warehouse and assignment facts.
7. Read/report permission, OrganizationScope, and DataClassification remain independent conditions. A permission alone never creates row scope.
8. Production Organization implementation remains POST-G1.

## 3. Candidate permission namespace

The following exact, case-sensitive keys were already identified by the cross-module contract. They remain absent from PermissionCatalog and unapproved:

| Candidate key | Purpose | Explicitly does not authorize |
|---|---|---|
| `organization.branches.read` | Read Branch master metadata | Assignment changes or source business rows |
| `organization.branches.manage` | Create/update/activate/deactivate Branch master facts | Permanent deletion, Warehouse changes, or account assignment |
| `organization.warehouses.read` | Read Warehouse metadata and its authoritative Branch relationship | Assignment changes or all stock/business data |
| `organization.warehouses.manage` | Create/update/reparent/activate/deactivate Warehouse master facts | Permanent deletion or account assignment |
| `organization.assignments.read` | Read Branch/Warehouse assignment metadata within authorized scope | Read access to source-module business records |
| `organization.assignments.create` | Create one assignment for a target AccountId and exact resource | Change, revoke, approve, or non-owner self-assignment |
| `organization.assignments.change` | Change an existing assignment's exact subject/resource/scope facts | Create, revoke, or approve |
| `organization.assignments.revoke` | Revoke an existing assignment | Create, change, approve, or destructive deletion |
| `organization.assignments.approve` | Approve an immutable pending assignment command | Execute the command or approve one's own/subject assignment |

Rules:

- The five assignment commands are distinct. Do not introduce a broad `organization.manage` key.
- Branch/Warehouse master-management permissions are not assignment-management permissions. Their lifecycle and maker-checker classification must be accepted by the relevant canonical data owners before implementation.
- `assignments.approve` is not an execution permission. A non-owner command requires the initiator's exact action key and an eligible independent approval bound to the exact command fingerprint and expected version.
- Approval does not grant permission to mutate. Execution does not grant permission to approve.
- Existing `reports.read`, `reports.export`, and `reports.print` are not substitutes for any Organization key and do not implement RowScope. No duplicate `reporting.*` namespace is proposed.
- Unknown, inactive, missing, or unavailable permissions remain Deny/Unavailable. No wildcard, prefix match, role-name inference, or permission fallback is allowed.

## 4. Command-to-permission contract

| Command | Exact candidate permission | Additional mandatory conditions |
|---|---|---|
| Branch metadata read | `organization.branches.read` | Canonical Branch source, authorized Branch scope, no source-record leakage |
| Branch master mutation | `organization.branches.manage` | Canonical Branch owner; expected version; immutable history; audit; sensitive-change approval policy |
| Warehouse metadata read | `organization.warehouses.read` | Canonical Warehouse source; current valid Branch relationship; authorized resource scope |
| Warehouse master mutation | `organization.warehouses.manage` | Canonical Warehouse owner; exact current Branch relationship; expected version; immutable history; audit; sensitive-change approval policy |
| Assignment metadata read | `organization.assignments.read` | Authoritative assignment source; scope before materialization |
| Assignment create | `organization.assignments.create` | Exact active resource; subject AccountId; reason; idempotency; expected version; history; audit intent |
| Assignment change | `organization.assignments.change` | Existing active assignment; exact before/after facts; reason; idempotency; expected version; history; audit intent |
| Assignment revoke | `organization.assignments.revoke` | Existing active assignment; explicit reason; idempotency; expected version; history; audit intent |
| Independent assignment approval | `organization.assignments.approve` | Authenticated approver distinct from actor and subject; exact immutable fingerprint/version; valid approval state |

For Branch/Warehouse assignment commands, composition is:

`AuthenticatedPrincipal AND ExactActionPermission AND AuthoritativeResourceValidity AND TransactionGuard AND CommandInvariants AND (BoundProductOwnerDirectPath OR ValidIndependentApprovalForNonOwner)`

The Product Owner direct path replaces only the final non-owner approval/self-assignment restriction. It does not replace or weaken any earlier condition.

For source records and reports:

`EffectiveRead = ExactPermission AND OrganizationScope AND DataClassification`

Assignment-read permission must not expose source-module rows. Every future source consumer must resolve true resource context from its owning module and apply RowScope in SQL before materialization.

## 5. Role eligibility and grant safety

No effective role grants for the new keys are proposed in v0.1. The safe default for every new key is Deny until its exact role/subject grant and scope are accepted.

| Principal / role | Candidate eligibility rule |
|---|---|
| Bound Product Owner | Binding selects the narrow direct-owner path only. Each action still requires its exact permission grant. Binding alone grants no permission; initial owner grant/enrollment is a separate IdentityAccess decision and is not provisioned here. |
| `super_admin` | Do not add Organization keys while the current `PermissionCatalog` maps this role to `AllPermissions`; doing so silently grants every new key. Before catalog expansion, freeze current approved grants as an explicit allow-list and test that future keys do not appear without deliberate inclusion. |
| `manager` | No Organization administration grant is implied. This role is not the Product Owner and is not a generic IdentityAccess administrator. |
| Other built-in roles | Deny by default for all new Organization keys. Any grant requires a versioned role-matrix entry and exact scope. Existing inventory, report, or dashboard grants do not imply Organization permission. |
| System Manager delegation | System Manager is not a value in the current `RoleCatalog`. Do not alias it to `manager` or `super_admin`. A future delegation must be an explicit, bounded, auditable grant for exact keys/scope with enforceable expiry and revocation. The current static role map does not prove per-principal delegation exists. |
| Independent approver | Must hold a separately accepted `organization.assignments.approve` grant. This permission cannot be inferred from manager status, subject status, Product Owner binding, or assignment execution. Actor/subject separation remains mandatory. |

Do not add these keys to PermissionCatalog, a seed, or the role matrix until:

1. IdentityAccess records the exact namespace and role-eligibility matrix.
2. The SuperAdmin automatic-expansion hazard is removed without changing any already approved grant.
3. System Manager delegation is represented by an enforceable model or remains disabled.
4. Product Owner exact-permission enrollment is accepted and testable.
5. Organization data ownership, scope, and approval obligations are accepted.

This proposal intentionally leaves the per-role grant table unassigned rather than guessing from job titles. That is a fail-closed boundary, not an invitation to add a wildcard.

## 6. Scope and authority-source constraints

- IdentityAccess may evaluate an exact permission and carry opaque scope identifiers; it does not prove a Branch/Warehouse exists, is active, or is assigned to the subject.
- Canonical Branch, Branch-membership, Warehouse, Warehouse-to-Branch, and account-assignment owners must be identified with real source/schema/migration evidence before any resolver or schema is designed.
- Current repository evidence does not establish a production canonical Branch/Warehouse source. No module, schema, table, key, or foreign key may be invented to close this gap.
- Caller-supplied AccountId, BranchId, WarehouseId, role, scope, approval flag, or Product Owner flag is untrusted input.
- Product Owner's approved ability to assign any account to any valid Branch/Warehouse is enforced through immutable binding plus the exact action permission. It does not create `scope=all` for reports or business records.
- A non-owner delegated administrator needs an exact resource scope. Empty or unavailable scope denies. `all` must be explicit for that same permission and cannot transfer between permissions.
- An assignment does not grant access to source records until the owning source module resolves and applies it as authoritative RowScope.

## 7. Session lease and authorization linearization proposal

### 7.1 Observed source baseline

The current IdentityAccess default is `SessionPolicy.Initial = (MaxActiveUsers: 5, HeartbeatInterval: 30 seconds, LeaseDuration: 180 seconds)` in `IdentityAccessPrimitives.cs`. This is a heartbeat/liveness lease, not a separate user-configured inactivity timeout.

The transaction-scoped adapter currently checks `session.lease_expires_at_utc > clock_timestamp()` and retains `FOR SHARE` locks on account, session, active account-role membership, role, role-permission grant, and permission rows contributing to Allow. It drains the complete result set before returning Allow. The disposable PostgreSQL proof demonstrates that seven competing identity mutations wait while the protected transaction holds these locks.

### 7.2 Proposed linearization point

The recommended authorization linearization point is successful completion of the fully-drained transaction-guard query after every row establishing the exact permission has been locked.

- If a revoke/disable/credential/role/grant mutation obtains its conflicting lock first, the guard must observe the committed new state and return Denied.
- If the guard obtains the complete lock set first and returns Allowed, the protected command is ordered before the competing revocation. The competing mutation waits until the protected transaction commits or rolls back, then becomes effective.
- The guard decision is valid only for that caller-owned transaction. It must not be cached or reused by another request/transaction.
- A later lease expiry does not retroactively reorder a command already linearized by a successful guard. The protected transaction must nevertheless have a finite, server-enforced maximum duration so the locks cannot be held indefinitely.

This is a recommendation for IdentityAccess and Platform acceptance; the current source does not yet prove a total transaction deadline.

### 7.3 Lease-safety bound

A finite maximum guarded-transaction duration and commit/finalization reserve must be selected from measured PostgreSQL workload evidence. Define:

- `T_guard_max`: maximum permitted duration from successful guard result to transaction completion;
- `T_commit_reserve`: bounded time reserved for commit/finalization;
- `T_safety`: operational safety margin for scheduling and the bounded retry policy.

Candidate guard condition:

`lease_expires_at_utc > clock_timestamp() + T_guard_max + T_commit_reserve + T_safety`

The database/server must enforce the corresponding transaction deadline; a client-only timeout is insufficient. If the remaining lease cannot cover the accepted bound, the guard fails closed. It must not extend the lease implicitly or turn an unavailable timing guarantee into Allow.

Exact numeric values are deliberately not invented here. IdentityAccess and Platform must measure legitimate command duration, heartbeat contention, PostgreSQL commit/finalization time, and timeout/retry behavior before selecting them. The selected bound must fit within the configured lease policy and pass exact-boundary tests.

The session heartbeat may contend with the guard because both touch the session row. The accepted implementation must prove that a bounded protected transaction cannot consume the lease before a queued heartbeat can safely renew it, and that a blocked/failed heartbeat never creates stale authorization for a subsequent request.

### 7.4 Required timing and revocation tests

1. Healthy lease with sufficient remaining margin: Allow; mutation/history/outbox commit atomically.
2. Guard after lease expiry: Denied.
3. Remaining lease at or below the accepted bound: fail closed.
4. Revocation obtains the row lock first: guard waits and then denies.
5. Guard obtains the complete lock set first: revoke is observably blocked until commit/rollback; a fresh request after revoke is denied.
6. Heartbeat renewal races with guard: no unbounded wait, missed-renewal failure, or stale session reuse; prove with independent PostgreSQL connections.
7. Transaction exceeds server-enforced maximum: entire local transaction rolls back; no partial mutation, history, or outbox remains.
8. Lock timeout, deadlock, cancellation, DB restart, and unknown-commit/replay: bounded behavior, durable idempotency result, no duplicate side effect.
9. Multi-role permission union: every contributing membership/grant row remains locked until transaction end.
10. Boundary tests use PostgreSQL's clock and observed transaction times, not application-host clocks or sleep-only assertions.

## 8. Acceptance responsibilities and evidence

| Accountable owner | Required acceptance/evidence |
|---|---|
| IdentityAccess module owner | Exact key names, command semantics, role eligibility, SuperAdmin expansion protection, System Manager delegation boundary, API v1.2 compatibility, decision/error semantics, and lease linearization |
| Organization data owner | Canonical Branch/Warehouse/assignment ownership, command-to-key mapping, authoritative scope/resolver obligations, approval fingerprint/version rules, and no source-row leakage |
| Platform owner | Global lock ranks, transaction participant ordering, transaction duration, timeout/deadlock/cancellation/retry/recovery semantics, and lease margin |
| Security reviewer | Principal spoofing, role expansion, owner-binding isolation, maker-checker independence, revocation races, and exact-head test evidence |
| Product Owner | Already accepted product policies remain controlling; separate G1 decision only after mandatory technical, legal, repository, and independent-review gates pass |

Required evidence is source-level contract review, a versioned acceptance record attributable to each accountable module owner, disposable PostgreSQL race/boundary tests against the actual IdentityAccess adapter, and independent exact-SHA Security/Architecture review.

A green CI run validates the tests that ran; it does not constitute any of these owner acceptances.

## 9. Pre-G1 permitted work and explicit non-authorization

Before G1, permitted work is limited to document/source-contract review, test design, and marked-disposable PostgreSQL experiments that do not create a production Organization module or consumer.

The following remain prohibited without separate post-G1 authorization: adding these keys to PermissionCatalog; changing role grants; inserting permission seeds/grants; creating a Product Owner binding or initial permission grant; adding Branch/Warehouse/assignment production schema or resolver; introducing Organization UnitOfWork/runtime/routes; or deploying any production cross-module consumer.

**Disposition:** the command-permission namespace and lease/revocation protocol are expressed as a reviewable technical proposal. No key, role grant, seed, binding, schema, runtime, or Organization consumer has been created. IdentityAccess, Organization, Platform, Security, legal, independent review, O20-E/O-20/O-21, and G1 gates remain separate and open.

**END — MDG-IDENTITYACCESS-ORGANIZATION-AUTHORIZATION-ACCEPTANCE-PROPOSAL-001 v0.1**
