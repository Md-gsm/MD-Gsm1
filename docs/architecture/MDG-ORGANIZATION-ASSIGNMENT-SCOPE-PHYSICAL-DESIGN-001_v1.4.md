# Organization Assignment Scope — Physical Design Policy Reconciliation

| Field | Value |
|---|---|
| Document ID | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 |
| Version / Date | 1.4 / 2026-11-23 |
| Supersedes | v1.3 only for the clauses explicitly reconciled below |
| Base physical design | v1.3 |
| Controlling policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.7 |
| Product Owner binding | MDG-IDENTITYACCESS-PRODUCT-OWNER-PRINCIPAL-BINDING-001 v0.2 |
| Logical design successor | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.7 |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |
| Status | OWNER POLICY RECONCILED; PHYSICAL TECHNICAL REVIEW / PLATFORM LOCK ORDER / G1 OPEN |

## 1. Authority and interpretation

This document is a controlled successor addendum to the PostgreSQL physical-design proposal v1.3. It resolves the Product Owner self-assignment and second-approver contradictions only. All unaffected v1.3 object maps, append-only history, transaction envelope, local audit-intent outbox, indexing, constraints and fail-closed requirements remain proposed design content, not production authorization.

## 2. Normative replacement for v1.3 §6.1

The v1.3 clause stating that the subject AccountId cannot equal the actor AccountId, including for the Product Owner, is superseded.

A Branch/Warehouse assignment command is accepted only when:
- actor and subject account state are resolved from trusted server-side IdentityAccess context;
- the exact active Organization resource and its current valid relationship are checked;
- the actor holds the exact approved Organization assignment-management permission, or the separately bound Product Owner authority resolves to the current authenticated installation-created Product Owner AccountId for the narrow direct-owner path;
- for a non-owner actor, the subject AccountId is not the actor's own AccountId;
- for a non-owner actor, a second authenticated approver distinct from both actor and subject approves before the state transition takes effect;
- for the designated Product Owner, the Product Owner may create/change/revoke Branch/Warehouse assignments for any subject AccountId, including the Product Owner's own AccountId, without a second approver;
- the owner exception is based only on the immutable server-side installation binding, never on role, username, claim, request field or client input;
- all commands remain reasoned, idempotent, expected-version checked, concurrency-protected and audited; successful owner-direct commands record `PRODUCT_OWNER_DIRECT`, actor and subject (which may be equal), and no second approver;
- immutable assignment history and local audit-intent outbox are committed atomically with the Organization state change under an accepted local transaction boundary.

The owner exception removes only the second-person approval and self-assignment prohibition for these Organization assignment commands. It does not bypass exact-resource validation, required IdentityAccess permission, Branch/Warehouse validity, RowScope for reads, immutable history, audit, concurrency or idempotency.

## 3. Owner binding and module boundary

The Product Owner identity is the account created as the installation's Product Owner during attended install/bootstrap. Exactly one AccountId is bound to the installation. The binding is immutable and non-transferable. Recovery can restore only the same AccountId. If the original principal cannot be safely recovered, owner-only operations fail closed; no successor/replacement account is permitted.

IdentityAccess remains owner of Account/Credential/Session/Role/Permission data; Organization stores only opaque AccountId references and consumes a typed server-side owner-authority decision. The binding's implementation owner and contract must be designated by IdentityAccess/Platform. This addendum does not authorize a binding table, schema, DDL, migration, seed, API, permission grant or production consumer.

## 4. Transaction and persistence constraints retained

A future direct-owner or second-person assignment command must preserve all approved physical safety constraints:
- command receipt/idempotency, aggregate state, immutable assignment event and local audit-intent outbox are atomic within the Organization database transaction;
- a pending non-owner approval is not an effective assignment;
- final approval revalidates current actor/approver eligibility, permissions, exact resource, owner-binding state, idempotency and aggregate versions under the final command transaction;
- a stale version or changed precondition denies without partial state;
- Product Owner direct path skips only the second-approver workflow; it does not skip the same transaction envelope or audit;
- revocation remains terminal; a later grant creates a new AssignmentId;
- no distributed transaction or cross-module direct write is inferred;
- cross-module lock ranks/transaction interaction remain blocked on the accepted Platform Lock-Order Registry and Inventory/cross-module owner acceptance.

## 5. Required physical acceptance tests

- Non-owner self-assignment and self-expansion fail at the database/domain command boundary.
- Non-owner approval rejects missing, duplicate, actor-equal or subject-equal approvers.
- A bound Product Owner may create/change/revoke its own and other accounts' assignments without a second approver.
- An ordinary System Manager, super_admin or DBA cannot impersonate the Product Owner or invoke direct-owner approval mode.
- Missing, conflicting, stale or unavailable binding fails closed.
- Owner direct path preserves required permission/resource checks, idempotency, expected version, event history and outbox atomicity.
- Audit-intent failure rolls back the assignment state and history.
- Product Owner account/binding replacement, transfer, delete or ordinary disable path is denied; credential maintenance preserves AccountId.
- Concurrent assignment/approval/revocation and global lock-order behavior are tested only after Platform and affected module owners accept the concrete rank/linearization contract.

## 6. Disposition and non-authorization

This addendum resolves the v1.3 owner-assignment wording conflict. It does not establish production readiness. Organization remains without an authorized production module/runtime consumer at this gate. No schema, DDL, migration, grant, permission seed, role, production binding, report consumer or data access is created or authorized.

Formal IdentityAccess contract acceptance, Organization owner/consumer acceptance, Platform lock order, AuditSecurity durable ACK, exact-head Linux/Windows validation, legal controls and independent review remain open.

**O20-E remains OPEN. Organization G1 is NOT GRANTED.**

**END — MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v1.4**
