# IdentityAccess Product Owner Principal Binding — Design Proposal

| Field | Value |
|---|---|
| Document ID | MDG-IDENTITYACCESS-PRODUCT-OWNER-PRINCIPAL-BINDING-001 |
| Version / Date | 0.1 / 2026-11-22 |
| Status | IDENTITYACCESS / PLATFORM / SECURITY REVIEW PROPOSAL — NOT ACCEPTED OR IMPLEMENTED |
| Related policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.5 |
| Related contract | MDG-IDENTITYACCESS-ORGANIZATION-PRINCIPAL-PERMISSION-CONTRACT-001 v0.5 |
| Related threat model | MDG-SECURITY-O20E-THREAT-MODEL-001 v0.1 |

## 1. Purpose

The accepted Product Owner exception is limited to Organization Branch/Warehouse assignment administration. It allows the designated Product Owner to create/change/revoke their own assignments and assignments for other accounts without a second approver. It does not imply super_admin, IdentityAccess permission administration, credential/session access, impersonation or RowScope bypass.

This exception requires a trusted server-side owner-principal binding. It cannot be inferred from role, username, display name, client claim, request field, email/mobile, GitHub identity or local-machine administrator status.

## 2. Authority and ownership gap

IdentityAccess owns Account, Credential, Session, Role and Permission grants. The repository currently has working HTTP session authentication and a validated host actor context, but no published module-facing Product Owner authority contract or implemented owner-binding lifecycle. The exact owner of the binding aggregate (IdentityAccess or Platform), installation scope, cardinality and bootstrap/recovery authority require explicit IdentityAccess/Platform/Security acceptance.

This document does not presume that the first administrator account is automatically the Product Owner, nor that the Product Owner is identified by a broad administrative role.

## 3. Candidate binding record

A future protected server-side binding may need:
- immutable BindingId and monotonic BindingVersion;
- installation/system scope identifier (Platform must define its representation);
- opaque AccountId bound as Product Owner;
- state and effective interval;
- enrollment/change/recovery/revocation reason code;
- authenticated initiating principal and independent approver/recovery authority evidence where required;
- created/changed timestamp from trusted server time;
- correlation and AuditSecurity evidence references;
- proof/reference to the approved bootstrap ceremony, not raw secrets.

It must not store password hashes, access/refresh/session bearer tokens, recovery codes in plaintext, private keys, or credentials belonging to another module.

## 4. Candidate lifecycle — subject to owner decisions

States and transitions must be finalized by the accountable owners. A possible review vocabulary is:
- UNBOUND — no usable Product Owner authority exists;
- ENROLLMENT_PENDING — trusted bootstrap evidence exists but binding is not active;
- ACTIVE — exactly the accepted bound principal resolves as Product Owner for the binding's system scope;
- CHANGE_PENDING / RECOVERY_PENDING — controlled change/recovery is being verified;
- REVOKED — previous binding is no longer usable; transition history is preserved.

No transition may be performed solely by a request carrying `isOwner=true`, `super_admin`, a user-selected account ID or a self-service permission. Unbound, pending, revoked, stale, ambiguous or unavailable binding returns NotOwner/Unavailable and fails closed.

The exact number of simultaneous Product Owners, who may initiate a binding change, whether change requires one or two independent verifiers, recovery authority, cooling-off delay and emergency disable path are open decisions. The no-second-approver exception for assignment commands must not be generalized to owner-binding lifecycle operations.

## 5. Runtime contract semantics

A future IdentityAccess/Platform public contract should expose a typed result, not a bool supplied by the caller:
- `OwnerAuthorityDecision`: BoundToCurrentPrincipal(bindingVersion, scope) | NotOwner | Unavailable;
- resolve only against the current authenticated AccountId/session and current binding state;
- binding changes invalidate prior versions immediately at the chosen linearization point;
- consumer receives no credential, recovery secret, private enrollment evidence or IdentityAccess persistence type;
- Organization is permitted to use the decision only for the narrowly scoped assignment maker-checker bypass.

The result must be request-scoped, immutable and fail closed. It is not a permission grant and cannot be combined with role-based fallback.

## 6. Enrollment, change and recovery controls to decide

IdentityAccess/Platform/Security must specify:
1. first installation/bootstrap trust anchor and how enrollment starts;
2. evidence proving the bound human/account identity;
3. whether enrollment uses an out-of-band ceremony or independent verifier(s);
4. account/session checks and forced password-change interaction;
5. change/rotation and removal process, including old-owner departure;
6. recovery after lost credentials or unavailable owner;
7. anti-self-enrollment and prevention of escalation by a normal super_admin;
8. emergency disable/recovery with no hidden always-on bypass;
9. audit/outbox, alerts, runbook and restoration of the binding from backup;
10. installation replacement/reinstall and data-restore continuity.

These are acceptance requirements, not a decision that any particular person, channel or two-person workflow has been selected.

## 7. Required tests

- an ordinary super_admin with any role combination is NotOwner unless exact binding exists;
- forged client owner flag, username, email, GitHub name and role cannot create owner authority;
- only the account in the active binding receives OwnerAuthority;
- other accounts, stale sessions, disabled accounts and revoked sessions receive Deny;
- unbound, pending, revoked, inconsistent version and resolver outage fail closed;
- binding rotation/recovery invalidates the previous principal at a documented linearization point;
- ordinary owner-binding change cannot be initiated and approved by the same principal if independent approval is required by the accepted lifecycle;
- Product Owner direct assignment works without a second approver only for Organization assignment administration;
- Product Owner direct assignment still requires actor authentication, reason, idempotency, expected version, exact valid resource, immutable history and AuditSecurity intent;
- owner authority cannot read credentials/sessions, impersonate accounts, change IdentityAccess roles/permissions or bypass source RowScope.

## 8. Disposition

Trusted Product Owner authority remains a G1 prerequisite. This proposal specifies a reviewable trust boundary and acceptance-test set only; it does not establish binding cardinality, enrollment or recovery policy, interface, database record, runtime, seed, migration or grant.

**Status: OPEN — IdentityAccess/Platform/Security owner acceptance and implementation evidence required. O20-E OPEN; G1 NOT GRANTED.**

**END — MDG-IDENTITYACCESS-PRODUCT-OWNER-PRINCIPAL-BINDING-001 v0.1**
