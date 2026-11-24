# IdentityAccess ↔ Organization — Principal and Permission Contract Proposal

| Field | Value |
|---|---|
| Document ID | MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 |
| Version / Date | 0.7 / 2026-11-22 |
| Supersedes | v0.6 |
| Supersedes | v0.5 |
| Supersedes | v0.4 |
| Supersedes | v0.8 |
| Status | PUBLIC IDENTITYACCESS CONTRACT v1.1 SOURCE-PUBLISHED; LIVE-SESSION PERMISSION QUERY IMPLEMENTED; OWNER BINDING / ORGANIZATION PERMISSION / COMMIT-TIME CONCURRENCY OPEN |
| Architecture baseline | MDG-ARCH-MASTER-001 v3.0.3 |
| Related design | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.5 |
| Implementation boundary | No runtime contract, permission seed, grant, API, schema or module is authorized |

## 1. Purpose and ownership

IdentityAccess remains the exclusive owner of Account, Credential, Session, Role and Permission grant. Organization owns Branch/Warehouse masters and assignment facts. Organization must not query IdentityAccess private tables or mutate IdentityAccess data. This document defines a versioned boundary proposal for review; it is not a published contract. The current IdentityAccess Contracts README states that no runtime public contract exists.

## 2. Trust requirements

1. The API derives AccountId only from the validated server-side session. Client AccountId, actor IDs, roles, permissions and owner flags are ignored.
2. The principal context is request-scoped and immutable for the request. Session/account revocation or disablement is checked according to IdentityAccess live-session semantics.
3. A successful principal resolution returns a typed authenticated principal; missing, disabled, revoked, expired, stale, ambiguous or unavailable returns Denied/Unavailable and fails closed.
4. Permission evaluation is a separate live IdentityAccess decision. Organization assignment does not replace permission; permission does not replace assignment.
5. No role-name or wildcard inference. Permission keys are exact, case-sensitive, versioned strings.
6. Product Owner authority is not a role. It is a separate protected server-side principal binding with enrollment, change and recovery controls. The contract exposes a typed OwnerAuthority decision only to the Organization authorization boundary; it never accepts a caller-supplied owner boolean.

## 3. Proposed v1 contract surface

The following are semantic responsibilities, not approved C# signatures:

- ResolveCurrentPrincipal(): AuthenticatedPrincipal | Denied | Unavailable.
- HasPermission(AccountId, PermissionKey): Granted | Denied | Unavailable.
- ResolveProductOwnerAuthority(): OwnerBound(AccountId, bindingVersion) | NotOwner | Unavailable.

AuthenticatedPrincipal minimum fields: AccountId (opaque UUID), SessionId (opaque non-secret identifier), AccountState, SessionState, PrincipalVersion and AuthenticatedAtUtc. It must not expose credential hashes, access-token hashes, recovery material, password data or session bearer tokens. Permission results include exact permission key, decision, policy/catalog version and evaluated-at UTC. No broad permission list or credential-bearing claim is required.

## 4. Proposed Organization permission namespace

The following keys are proposed for IdentityAccess owner review; none currently exists in PermissionCatalog and none is seeded or granted by this document:

- organization.branches.read
- organization.branches.manage
- organization.warehouses.read
- organization.warehouses.manage
- organization.assignments.read
- organization.assignments.create
- organization.assignments.change
- organization.assignments.revoke
- organization.assignments.approve
The following report permissions already exist in the Product-Owner-approved IdentityAccess matrix and are not Organization-specific permission keys:
- reports.read
- reports.export
- reports.print

The owner-approved matrix grants reports.read and reports.print to all ten initial roles; reports.export is granted only to super_admin, manager, warehouse, purchaser and accountant. The approved dashboard permissions are dashboard.read and dashboard.configure.self for all roles, and dashboard.assign for super_admin only. PermissionCatalog.cs now contains this approved dashboard/report subset, and a focused golden test locks those grants. This does not establish row scope: the applicable reports.* permission is conjunctive with the authoritative Organization assignment predicate and source-side SQL RowScope.

The organization.* keys above remain proposals only. Their exact role mapping, approval and publication remain open; no organization.* or reporting.* key is added to PermissionCatalog and no role grant/seed is authorized. Assignment administration permissions are distinct from resource RowScope. A user with organization.warehouses.read still needs an exact active AccountWarehouseAssignment to read Warehouse-scoped business rows. Owner authority is an independent server-side exception for the assignment administration policy only; it does not imply every IdentityAccess permission or report scope.

## 5. Failure and version behavior

Unknown contract major version, unknown permission key, unavailable resolver, inconsistent principal version, expired session or failed account-state check denies. Minor versions may add optional fields only; changes to identity meaning, permission semantics, owner-binding semantics or decision result are major-version changes. Consumers must not cache allow decisions beyond the request. No stale-cache fallback.

## 6. Acceptance tests required before publication

- forged client AccountId/actor/owner flags cannot alter principal or authority;
- session revoke/account disable during concurrent protected request obeys a documented linearization point;
- permission allow/deny/unavailable and unknown key behavior are exact and fail closed;
- Product Owner binding cannot be inferred from super_admin, username, GitHub identity or client claim;
- owner binding rotation/recovery is audited and cannot be self-enrolled by an untrusted principal;
- Organization consumer references only the published Contracts assembly and never IdentityAccess private storage;
- contract version mismatch fails closed.

## 7. Disposition

IdentityAccess owner acceptance, permission catalog reconciliation, trusted Product Owner enrollment/recovery and PostgreSQL concurrency proof remain OPEN. The current code's authenticated actor context and permission store are implementation details, not a published cross-module contract. This proposal does not authorize source implementation, permission grants, role mappings, G1 or production access.

**END — MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 v0.1**


## 8. Controlled permission-name reconciliation — 2026-11-22 (v0.2)

The prior proposal's `reporting.branch.read`, `reporting.warehouse.read` and `reporting.export` suggestions are withdrawn. They were not part of the owner-approved permission matrix and must not be treated as authorization keys. The existing owner-approved `reports.read`, `reports.export` and `reports.print` keys are the report authorization layer; Branch/Warehouse isolation is enforced separately through Organization assignment and source-query RowScope.

The implementation alignment is limited to the already approved dashboard/report subset:
- `dashboard.read`, `dashboard.configure.self`, `dashboard.assign`;
- `reports.read`, `reports.export`, `reports.print`.

The focused test is `tests/MDGsm.Domain.Tests/IdentityAccess/ApprovedDashboardReportPermissionCatalogTests.cs`. It validates the approved roles for these six keys, including export restrictions, and verifies that `reporting.branch.read` remains unknown/denied. It is not a golden test for the entire owner matrix. No Organization management permission key, role mapping, seed or database grant has been added.

The existing `src/Modules/IdentityAccess/Contracts/README.md` still states that no runtime public cross-module contract is published. `ModuleContractVersion.cs` is a version marker only, not by itself a principal/permission contract. IdentityAccess module-owner acceptance, trusted Product Owner binding/enrollment/recovery, interface implementation and concurrency linearization remain OPEN.

**Disposition:** report/dashboard permission subset reconciled to current owner policy in code and focused tests; full contract publication and Organization permission namespace remain OPEN. No G1 or production access is authorized.

**END — MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 v0.2**


## 9. Controlled full PermissionCatalog alignment — 2026-11-22 (v0.3)

The IdentityAccess domain PermissionCatalog is now aligned to the complete owner-approved initial role/permission matrix in `docs/security/identityaccess-permission-matrix-approval-fa.md`, while preserving the separately documented sensitive-person-data permission gate:

- All permission names in the owner-approved initial matrix are represented with their exact role grants. The unapproved legacy code keys `persons.delete` and `finance.voucher.delete` are not retained as grants.
- The existing `persons.sensitive.read` permission remains as a separate, documented sensitive-field control and is granted to `super_admin` only. It is required by the existing Persons sensitive-data authorizer contract and its explicit test; it is not silently inferred from `persons.read`.
- All ten initial role grant sets are explicitly represented and compared by a golden test: `tests/MDGsm.Domain.Tests/IdentityAccess/ApprovedPermissionCatalogMatrixTests.cs`. The same test verifies the supplemental sensitive permission remains super_admin-only.
- Sensitive/administrative permissions remain limited exactly as the applicable accepted policy requires. Empty cells remain deny.
- Role permission grants do not carry a resource scope. The matrix's self/assigned/branch/warehouse scopes remain separate runtime authorization requirements and cannot be inferred from this catalog.
- The focused dashboard/report test remains as additional negative/positive coverage. Unapproved `reporting.*` and `organization.*` names remain unknown and denied.

This is source/test alignment to the already owner-approved role matrix plus preservation of the existing separate sensitive-person permission gate. It does not seed database permissions, mutate role assignments, grant new database access, authorize any Organization permission, or publish the cross-module runtime interface.

### 9.1 Exact acceptance boundary

The full golden test and repository CI must pass on the exact PR head before this reconciliation is marked test-verified. IdentityAccess module-owner acceptance and publication of the versioned principal/permission contract remain separate prerequisites. The Product Owner binding, enrollment/recovery, session-revocation linearization and PostgreSQL cross-module concurrency proof remain OPEN.

**Disposition:** initial PermissionCatalog names and role grants SOURCE-ALIGNED; exact-head CI verification PENDING; IdentityAccess public contract NOT PUBLISHED; Organization permission namespace NOT APPROVED/NOT ADDED; O20-E OPEN; G1 NOT GRANTED.

**END — MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 v0.3**


## 10. Supplemental sensitive-person permission control — 2026-11-22 (v0.4)

The full matrix reconciliation preserves `persons.sensitive.read` as a separate permission because the existing documented Persons sensitive-authorizer contract uses this exact key and limits it to `super_admin`. The full golden test verifies this separate gate for every initial role. Possession of `persons.read` does not imply access to sensitive fields.

This supplement avoids breaking the already-established sensitive-data authorization boundary while removing obsolete unapproved `persons.delete` and `finance.voucher.delete` keys. It does not widen access to sensitive data and does not introduce a new role grant.

**Disposition:** full owner-approved role matrix plus the supplemental sensitive-person gate are source-aligned; exact-head CI test verification remains pending. Contract publication, owner binding, Organization management keys and G1 remain OPEN.

**END — MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 v0.4**


## 11. Crosswalk to existing IdentityAccess contracts and runtime — 2026-11-22 (v0.5)

A source/documentation crosswalk was performed to avoid treating an existing HTTP DTO or version marker as the missing Organization-facing authorization contract.

### 11.1 Existing artifacts and their actual scope

- `docs/security/identityaccess-authorization-runtime-contract-fa.md` is the existing detailed authorization design based on the owner-approved permission matrix. It defines default deny, separate role/permission/scope/resource ownership, live session/grant checks, permission-scoped grant union, authoritative resource resolution, SQL-before-materialization RowScope, maker-checker, audit and a 41-case acceptance-test plan. It explicitly records `g1Authorized=false` and says runtime implementation/tests have not been completed. It is authoritative design input, but not a cross-module .NET interface.
- `src/Modules/IdentityAccess/Contracts/IdentityAccessDtos.cs` contains Login/Password/Session HTTP data-transfer records. It does not expose a typed validated principal, a permission decision result, a Product Owner authority binding or an Organization scope resolver.
- `src/Modules/IdentityAccess/Contracts/ModuleContractVersion.cs` declares a version marker `1.0`; a marker alone is not a callable contract or proof of semantic compatibility.
- `src/Host/Api/Security/AuthenticatedActorContext.cs` is an HTTP-host implementation that reads the validated `MDGsm.Session` principal. It is not a module-owned public contract for an Organization application service.
- `src/Modules/IdentityAccess/Application/IdentityAccessPorts.cs` currently declares `ISessionIdentityResolver` and `IRolePermissionStore` as IdentityAccess application ports. Those implementation-level interfaces are not published through the Contracts assembly and must not be referenced by Organization directly.
- `src/Modules/IdentityAccess/Contracts/README.md` states that no runtime public cross-module contract is published and implementation is blocked pending ADR-0006 security review and explicit G1 authorization.

Accordingly, it is inaccurate to state that IdentityAccess has no runtime authentication at all. It does have live session validation and HTTP-level authenticated actor handling. The precise gap is the absence of a published, versioned module-facing principal/permission/owner-authority interface suitable for Organization without private-layer coupling.

### 11.2 Contract requirements before publication

The request-scoped public interface has now been published in `src/Modules/IdentityAccess/Contracts/IdentityAccessAuthorizationContract.cs`:
- `ResolveCurrentPrincipalAsync` returns a typed principal decision from the validated server-side request context, with no caller-supplied AccountId.
- `EvaluateCurrentPermissionAsync` evaluates one exact known permission for that current principal and distinguishes Allowed, Denied and Unavailable.
- `ResolveCurrentProductOwnerAuthorityAsync` returns a typed, version-capable decision and is fail-closed until the protected owner binding exists.

The Host API adapter is `src/Host/Api/Security/IdentityAccessAuthorizationContractAdapter.cs`. It uses the validated `IAuthenticatedActorContext` and current `IRolePermissionStore`; unknown permission keys deny; permission-store failures produce Unavailable; and the Product Owner path currently always returns Unavailable rather than inferring ownership from super_admin or client data. The adapter is registered as a scoped service in the API composition root. Module contract version is now 1.1.

The focused test is `tests/MDGsm.Domain.Tests/IdentityAccess/IdentityAccessAuthorizationContractAdapterTests.cs`. It covers authenticated principal resolution, missing session, unknown key, allowed/denied grant, permission-store outage and fail-closed owner authority.

This is a source-published interface plus Host implementation; it is not yet independently accepted by the IdentityAccess module owner or consumed by an Organization module. Exact-head CI for these latest source files remains pending. The Product Owner binding itself is still not implemented.

### 11.3 Product Owner authority lifecycle

Product Owner authority is a separate protected server-side binding, not a role or permission grant. A future contract must identify:
- first enrollment/bootstrap authority and installation binding;
- proof of the designated principal's identity;
- independent approval/control for owner-binding change and recovery;
- prevention of self-enrollment or escalation by an ordinary super_admin;
- rotation/revocation and emergency disable;
- audit evidence and recovery from lost owner credentials;
- concurrency behavior when a binding changes during an active command.

The existing O46 bootstrap/recovery records are related platform prerequisites, not by themselves proof that this O20-E owner binding has been implemented or accepted. No identity, owner account, seed or recovery path is created by this proposal.

**Disposition:** existing runtime HTTP session path and policy design are recognized; Organization-facing public principal/permission/owner contract remains unpublished and unaccepted. O20-E OPEN; G1 NOT GRANTED.

**END — IdentityAccess ↔ Organization contract proposal v0.5**


## 12. Product Owner principal binding dependency — 2026-11-22 (v0.6)

The detailed design request is now tracked in `MDG-IDENTITYACCESS-PRODUCT-OWNER-PRINCIPAL-BINDING-001 v0.1`. It does not claim a trusted binding already exists.

The public contract must return a typed `OwnerAuthorityDecision` bound to the current server-authenticated AccountId/session and a monotonic binding version. Organization must never submit or consume a caller-provided owner boolean. The owner decision can only bypass the second-approver requirement for Organization assignment create/change/revoke; it does not create an IdentityAccess permission, enable role/permission mutation, reveal credentials/sessions, enable impersonation or bypass RowScope.

The Product Owner assignment exception is accepted policy. Owner-binding cardinality, aggregate ownership (IdentityAccess vs Platform), first bootstrap, evidence ceremony, change/rotation, independent recovery/approval, emergency disable and restoration remain explicit owner/technical decisions. The no-second-approver exception for assignment commands is not applied to owner-binding lifecycle operations by inference.

**Disposition:** public IdentityAccess principal/permission/owner contract unpublished; Product Owner binding specification proposed but not accepted/implemented; O20-E OPEN and G1 NOT GRANTED.

**END — IdentityAccess ↔ Organization contract proposal v0.6**


## 13. Controlled public-contract publication — 2026-11-22 (v0.7)

The source-level public boundary is now present at contract version 1.1. This resolves the previous "no public type exists" gap, but it does not close the full module integration gate.

### Contract and implementation

- `IIdentityAccessAuthorizationContract` accepts no actor AccountId, role, owner flag, token or scope from its caller.
- Principal resolution is based only on the validated request-scoped actor context installed by the session-authentication middleware.
- Permission decisions use the current server-side permission store and the exact known permission catalog; unknown keys are Denied and store failures are Unavailable.
- Product Owner authority has a typed decision, but this adapter returns Unavailable until the protected owner binding and lifecycle are implemented. No role-based or client-claim fallback exists.
- Contract version marker is 1.1; only public DTOs and decisions are exposed from Contracts.

### Remaining acceptance

IdentityAccess owner acceptance of the public interface semantics, Organization consumer integration, session/revocation linearization evidence, Product Owner binding enrollment/change/recovery, and exact-head CI remain OPEN. No Organization permission namespace, database grant, G1 or production Organization access is authorized.

**Disposition:** public contract SOURCE-PUBLISHED; host principal/permission adapter IMPLEMENTED IN SOURCE; Product Owner authority UNAVAILABLE/NOT IMPLEMENTED; module-owner acceptance and exact-head CI OPEN; O20-E OPEN; G1 NOT GRANTED.

**END — IdentityAccess ↔ Organization contract proposal v0.7**


## 14. Live-session-bound permission decision — source and test successor (2026-11-22; v0.8)

The public principal snapshot now exposes:
- AccountId derived only from the middleware-installed principal;
- SessionId from the same database-resolved session row;
- PrincipalVersion equal to the credential version validated at authentication;
- AccountState and SessionState values representing the middleware-validated snapshot;
- AuthenticatedAtUtc from the database clock used by the middleware.

The ValidatedSessionIdentity now includes SessionId. SessionAuthenticationMiddleware validates SessionId and positive credential version before renewing the lease and adds the session ID, credential version and authentication timestamp as server-created claims. IAuthenticatedActorContext validates and exposes these claims; it does not read caller-supplied request data.

IRequestAuthorizationStore.HasPermissionForLiveSessionAsync is the persistence boundary for permission decisions. Its PostgreSQL query evaluates the following in one statement:
- exact AccountId and SessionId;
- no revocation and lease expiry strictly after PostgreSQL clock_timestamp();
- session credential version equals the current account credential version;
- account remains enabled;
- exact permission key is granted through an active account role and active permission.

Thus an expired/revoked session, disabled account, version mismatch or missing/inactive grant fails closed. Store errors return Unavailable. The adapter no longer uses a permission-only query for the public contract.

Executable coverage added:
- IdentityAccessAuthorizationContractAdapterTests for typed principal/session metadata, allowed/denied, unavailable, unknown keys and owner-unavailable behavior;
- IdentityAccessLiveSessionPermissionPostgresProofTests for the real PostgreSQL store query under active session, revoked session, expired lease, credential-version mismatch, disabled account and inactive permission;
- SessionAuthenticationMiddlewareTests for the server-created session claims.

### Security limitation that remains explicit

The database statement creates a request-time authorization decision with a consistent PostgreSQL statement snapshot. It is not a commit-time lock or distributed transaction. If a future Organization mutation relies on it, the Organization use-case/unit-of-work must revalidate/serialize authorization at the accepted mutation linearization point. Concurrent permission/session revocation vs cross-module write is not proven by this change and remains a blocker. No Organization route currently consumes the contract.

**Disposition:** IdentityAccess public contract v1.1 and live-session-bound permission check SOURCE-IMPLEMENTED; exact-head CI for these latest files PENDING; owner-binding UNAVAILABLE; Organization permission namespace OPEN; commit-time cross-module revocation linearization OPEN; O20-E OPEN; G1 NOT GRANTED.

**END — IdentityAccess ↔ Organization contract v0.8**


## 15. Module ownership correction — IdentityAccess owns the authorization adapter (2026-11-22; v0.9)

The initial v1.1 adapter was first placed in the API Host. A subsequent architecture review identified that this placed IdentityAccess Domain/Application implementation dependencies in the Host boundary. The implementation has been moved:

- Public interface remains in IdentityAccess Contracts.
- IdentityAccess Application now declares IValidatedRequestPrincipalAccessor, a Host-supplied port containing only validated request-scoped AccountId, SessionId, credential version and authentication timestamp.
- Host's IAuthenticatedActorContext implements that port and reads only middleware-created claims.
- IdentityAccess Infrastructure owns IdentityAccessAuthorizationContractAdapter and uses its own IRequestAuthorizationStore/PermissionCatalog.
- PersistenceRegistration wires the adapter and its stores; if database configuration is absent, an explicit UnavailableRequestAuthorizationStore returns Unavailable through the public contract.
- Host no longer contains or registers a module-private authorization implementation.

This corrects the module boundary: Host supplies validated transport principal facts; IdentityAccess owns permission semantics and persistence. Organization will consume only the public Contracts assembly.

The live-session permission query remains request-time only and does not lock authorization state through a future Organization mutation. IdentityAccess owner acceptance, Product Owner binding lifecycle, Organization consumer, commit-time revocation linearization, full 41-case validation, exact-head CI and independent review remain open.

**Disposition:** public module boundary corrected in source; latest exact-head CI pending; Product Owner binding UNAVAILABLE; O20-E OPEN; G1 NOT GRANTED.

**END — IdentityAccess ↔ Organization contract v0.9**
