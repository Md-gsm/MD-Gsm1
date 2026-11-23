# Organization Assignment Scope — Logical Design Policy Reconciliation

| Field | Value |
|---|---|
| Document ID | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 |
| Version / Date | 0.7 / 2026-11-23 |
| Supersedes | v0.6 only for the clauses explicitly reconciled below |
| Base logical design | v0.6 |
| Controlling ownership policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.7 |
| Owner binding design | MDG-IDENTITYACCESS-PRODUCT-OWNER-PRINCIPAL-BINDING-001 v0.2 |
| Governing owner decision | MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.6 |
| Status | OWNER POLICY RECONCILED; LOGICAL/TECHNICAL REVIEW AND IMPLEMENTATION GATES OPEN |

## 1. Authority and scope

This controlled successor reconciles Product Owner assignment authority in the logical design with the explicit owner decisions recorded on PR #92. It does not rewrite historical versions or approve implementation. All unaffected logical ownership, Warehouse/Branch, lifecycle, idempotency, audit, RowScope and transaction constraints in v0.6 remain in force unless they conflict with the normative clauses below.

## 2. Normative replacement for Product Owner assignment authority

Any v0.6 sentence stating that self-assignment is prohibited for every actor, including the Product Owner, is superseded.

The controlling rule is:
1. Every actor other than the designated Product Owner is prohibited from creating or expanding an assignment whose subject AccountId is that actor's own AccountId.
2. The designated Product Owner may create, change or revoke the Product Owner's own Branch/Warehouse assignments and those of any other account.
3. The Product Owner performs these Organization assignment commands directly without a second approver. This is a separately authorized direct-owner path, not self-approval.
4. For every non-owner actor, each assignment create/change/revoke command requires a distinct authenticated second approver before effect. The approver must be neither the initiating actor nor the subject account.
5. The Product Owner exception is valid only when the exact immutable installation-scoped Product Owner binding resolves to the current authenticated AccountId. It is not inferred from `super_admin`, System Manager role, username, client claim, request payload, email/mobile or GitHub identity.
6. Owner direct execution remains subject to authentication, exact active resource checks, the approved IdentityAccess permission/authorization contract, reason, idempotency, expected aggregate version, concurrency control, immutable assignment history, Audit intent and source RowScope. Only the second-approver/self-assignment restrictions are excepted.
7. Product Owner identity is installation-created, immutable and non-transferable. Recovery may restore only the same AccountId. Unverifiable recovery denies owner-only operations.

## 3. Authorization composition

The binding is not a general IdentityAccess permission and is not a replacement for Organization resource assignment. Organization must still resolve the exact Branch/Warehouse and enforce applicable assignment-management permission and resource validity. Product Owner binding only selects the approved direct-owner maker-checker path.

The Product Owner's direct assignment exception does not grant:
- account impersonation or cross-account login;
- Credential, Session, password or recovery-secret access;
- IdentityAccess role/permission mutation;
- DBA, infrastructure or WORM administration;
- Journal/Posting or arbitrary business-data mutation;
- RowScope bypass for reading reports or source-module data.

## 4. Required logical acceptance tests

- Non-owner self-assignment and non-owner self-expansion deny.
- Non-owner create/change/revoke without a second distinct approver denies.
- Approver equal to actor or subject denies.
- Bound Product Owner create/change/revoke of own or another account's Branch/Warehouse assignment succeeds without a second approver only when all other required authorization/resource checks pass.
- An unbound, mismatched, unavailable or forged owner identity denies and cannot fall back to System Manager or super_admin.
- Product Owner exception never changes IdentityAccess roles/permissions, credentials/sessions, report RowScope or unrelated business authority.
- Every successful direct-owner action has an auditable actor/subject pair, `PRODUCT_OWNER_DIRECT` approval mode, reason, idempotency and immutable history.

## 5. Disposition

This addendum resolves the owner-policy contradiction in the logical design. It is not an Organization module contract, executable consumer, runtime proof, DDL/migration approval, permission seed, G1 authorization or Go-Live authorization. Formal IdentityAccess and Organization contract acceptance, a trusted binding implementation, a real Organization UnitOfWork, AuditSecurity ACK and independent review remain open.

**END — MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.7**
