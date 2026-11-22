# IdentityAccess ↔ Organization — Principal and Permission Contract Proposal

| Field | Value |
|---|---|
| Document ID | MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 |
| Version / Date | 0.3 / 2026-11-22 |
| Supersedes | v0.2 |
| Status | CROSS-MODULE CONTRACT PROPOSAL — FULL INITIAL ROLE PERMISSION CATALOG RECONCILED TO OWNER-APPROVED MATRIX; PUBLIC CONTRACT AND ORGANIZATION PERMISSIONS STILL OPEN |
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

The IdentityAccess domain PermissionCatalog is now aligned to the complete owner-approved initial role/permission matrix in `docs/security/identityaccess-permission-matrix-approval-fa.md`:

- The catalog contains the approved permission names only; the unapproved legacy code keys `persons.delete` and `finance.voucher.delete` are not retained as grants.
- All ten initial role grant sets are explicitly represented and compared by a golden test: `tests/MDGsm.Domain.Tests/IdentityAccess/ApprovedPermissionCatalogMatrixTests.cs`.
- Sensitive/administrative permissions remain limited exactly as approved by the matrix. Empty cells remain deny.
- Role permission grants do not carry a resource scope. The matrix's self/assigned/branch/warehouse scopes remain separate runtime authorization requirements and cannot be inferred from this catalog.
- The previously added focused dashboard/report test remains as additional negative/positive coverage. Unapproved `reporting.*` and `organization.*` names remain unknown and denied.

This is source/test alignment to an already owner-approved policy. It does not seed database permissions, mutate role assignments, grant new database access, authorize any Organization permission, or publish the cross-module runtime interface.

### 9.1 Exact acceptance boundary

The full golden test and repository CI must pass on the exact PR head before this reconciliation is marked test-verified. IdentityAccess module-owner acceptance and publication of the versioned principal/permission contract remain separate prerequisites. The Product Owner binding, enrollment/recovery, session-revocation linearization and PostgreSQL cross-module concurrency proof remain OPEN.

**Disposition:** initial PermissionCatalog names and role grants SOURCE-ALIGNED; exact-head CI verification PENDING; IdentityAccess public contract NOT PUBLISHED; Organization permission namespace NOT APPROVED/NOT ADDED; O20-E OPEN; G1 NOT GRANTED.

**END — MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 v0.3**
