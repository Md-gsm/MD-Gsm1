# Organization Assignment Scope - Physical Design Policy and Authorization Reconciliation

| Field | Value |
|---|---|
| Document ID | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 |
| Version / Date | 1.5 / 2026-11-23 |
| Supersedes | v1.4 only for the authorization-composition clauses identified below |
| Base physical design | v1.4 |
| Logical design | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.7 |
| Data ownership policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.7 |
| IdentityAccess contract | MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 v2.1 |
| Owner binding policy | MDG-IDENTITYACCESS-PRODUCT-OWNER-PRINCIPAL-BINDING-001 v0.2 |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |
| Status | AUTHORIZATION COMPOSITION RECONCILED; MODULE / PLATFORM ACCEPTANCE AND IMPLEMENTATION OPEN |

## 1. Purpose and limited supersession

This controlled successor resolves one authorization-composition contradiction in v1.4. It supersedes only the v1.4 wording that allows Product Owner authority to substitute for an IdentityAccess permission. All unaffected physical-design content in v1.4 remains unchanged and in force within its proposal status.

This document does not publish a permission key, approve a role mapping, create a module, authorize a production consumer, or grant G1.

## 2. Normative authorization composition

The v1.4 phrase that accepted either an exact Organization assignment-management permission OR Product Owner authority is withdrawn.

Every Branch/Warehouse assignment create, change, or revoke command MUST satisfy all common conditions:

1. The actor is derived only from the current server-validated IdentityAccess principal and live session.
2. The exact command-specific IdentityAccess permission decision is Allowed for that principal. The permission key must be an exact, approved, active key; unknown, absent, stale, denied, or unavailable decisions fail closed.
3. The target AccountId and exact active Branch/Warehouse resource are resolved and validated by their owning modules. Caller-supplied identity, role, permission, owner, or resource claims are not authoritative.
4. Reason, idempotency fingerprint, expected aggregate version, concurrency preconditions, immutable history, and local audit-intent requirements are satisfied.
5. The command participates in one accepted local transaction containing the IdentityAccess transaction guard, the Organization state transition, required history, and local outbox/audit intent.
6. The command follows the Platform-approved global lock order, once that order is formally accepted by Platform and all affected data owners.

The Product Owner binding is an additional, independent condition for selecting the direct-owner path. It is not an alternative to condition 2 and never produces an IdentityAccess permission decision.

### 2.1 Non-owner path

For an actor who is not the bound Product Owner:

- The subject AccountId must not equal the actor AccountId for create or expansion.
- A distinct authenticated second approver is required before the transition takes effect.
- The approver must be neither the initiating actor nor the subject AccountId.
- Approval must bind to the exact immutable command fingerprint and expected aggregate version and must be revalidated in the final command transaction.
- Missing, duplicate, stale, self, subject-equal, or unavailable approval evidence denies.

### 2.2 Bound Product Owner direct path

For the designated Product Owner:

- IdentityAccess must still return Allowed for the exact command permission.
- The protected installation-scoped Product Owner binding must independently resolve as BoundOwner for the same current authenticated AccountId and session.
- The Product Owner may create, change, or revoke Branch/Warehouse assignments for the Product Owner's own AccountId or any other account.
- No second approver is required for this narrow direct-owner assignment path.
- A successful action records approval mode PRODUCT_OWNER_DIRECT, actor, subject, reason, idempotency reference, aggregate version, and audit intent. Actor and subject may be equal.
- The binding removes only the non-owner self-assignment and second-approver restrictions. It does not bypass permission, authentication, resource validity, data ownership, RowScope, transaction, history, audit, idempotency, or concurrency requirements.

If the binding is missing, inconsistent, stale, unavailable, or cannot be proven to belong to the current authenticated principal, the direct-owner path denies. There is no fallback to super_admin, System Manager, username, client claim, email/mobile, GitHub identity, machine administrator, or a caller-selected AccountId.

## 3. Required consistency and transaction boundary

The IdentityAccess permission guard must be evaluated using the caller's already-open PostgreSQL connection and transaction. Its row locks must remain held until the Organization command commits or rolls back.

The future Product Owner binding decision must have an equivalent accepted consistency/linearization rule for the same command transaction. The currently published IdentityAccess method ResolveCurrentProductOwnerAuthorityAsync returns Unavailable and is not transaction-scoped. It is therefore not usable as a production authorization proof.

The future Organization command must not proceed when:
- the permission check is performed outside the protected transaction;
- the binding decision is point-in-time only without an accepted commit-time consistency rule;
- the command cannot join one local transaction for guard, mutation, history, and outbox;
- Platform lock order is not accepted;
- lock timeout, deadlock, cancellation, rollback, crash, unknown-commit, or replay behavior is not defined and tested.

No distributed transaction is implied. AuditSecurity network delivery occurs after the local transaction commits; the worker must not hold domain locks while delivering remotely.

## 4. Permission-key and role-mapping boundary

This design does not choose or publish the Organization permission namespace. No organization.* key is currently present in the IdentityAccess PermissionCatalog and no such key is seeded or granted by this document.

The exact command-to-permission mapping, role eligibility, Product Owner initial permission assignment, and treatment of existing generic reports.read / reports.export / reports.print permissions require formal IdentityAccess owner acceptance and Organization concurrence. The Product Owner binding is not an implicit grant.

The existing PermissionCatalog maps SuperAdmin to all keys in its catalog. Adding a new key to the catalog can therefore expand SuperAdmin's effective permissions. No key may be added to the catalog or broad role matrix until the resulting role mapping is explicitly reviewed and accepted. No wildcard or role-name inference is permitted.

## 5. Acceptance tests required before implementation

The following cases are mandatory:

- A non-owner with the exact permission but no distinct second approver is denied.
- A non-owner cannot assign or expand an assignment to their own AccountId.
- A bound Product Owner without the exact command permission is denied.
- A bound Product Owner with the exact command permission may manage own or other accounts' Branch/Warehouse assignments without a second approver, subject to every other common condition.
- An unbound, mismatched, stale, unavailable, or forged owner identity cannot invoke the direct-owner path.
- A System Manager, SuperAdmin, DBA, service identity, client flag, username, or GitHub identity cannot impersonate the Product Owner.
- An unknown or inactive command permission denies; an IdentityAccess Unavailable result never becomes Allow.
- Session/account/credential/role/grant changes and owner-binding lifecycle changes obey the accepted transaction linearization point.
- Guard, binding, aggregate mutation, immutable history, and local outbox either commit together or roll back together.
- Timeout, deadlock, cancellation, crash-before-commit, unknown-commit, same-key replay, changed-payload replay, and audit-intent failure are covered by independent PostgreSQL tests.
- Product Owner direct actions retain audit actor/subject attribution and approval mode PRODUCT_OWNER_DIRECT.
- RowScope and data-classification checks remain independent for every read, report, print, and export path.

## 6. Non-authorization and disposition

This successor changes design wording only. It does not authorize a new IdentityAccess permission, role grant, Organization schema, DDL, migration, runtime, endpoint, production binding, report consumer, database access, or data change.

The Organization module and production UnitOfWork remain post-G1 implementation work. Before G1, only contract review, owner acceptance, Platform lock-order acceptance, and disposable isolated proof work may proceed. A synthetic PostgreSQL fixture is not a production consumer.

**Disposition:** the v1.4 OR ambiguity is removed; IdentityAccess permission and Product Owner binding are independent conjunctive conditions. IdentityAccess owner acceptance, Organization concurrence, Platform lock order, binding transaction semantics, and independent review remain OPEN. O20-E remains OPEN. Organization G1 is NOT GRANTED.

**END - MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v1.5**
