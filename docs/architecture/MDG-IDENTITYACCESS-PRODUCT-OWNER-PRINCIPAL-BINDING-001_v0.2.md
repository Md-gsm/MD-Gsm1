# IdentityAccess Product Owner Principal Binding — Owner-Decision-Aligned Design

| Field | Value |
|---|---|
| Document ID | MDG-IDENTITYACCESS-PRODUCT-OWNER-PRINCIPAL-BINDING-001 |
| Version / Date | 0.2 / 2026-11-23 |
| Supersedes | v0.1 |
| Status | OWNER POLICY ACCEPTED; IDENTITYACCESS / PLATFORM / SECURITY DESIGN REVIEW AND IMPLEMENTATION EVIDENCE OPEN |
| Governing policy | MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.6 |
| Owner decision | PR #92 comment 7093819016 and 7094120301 |
| PR | #92 Draft / unmerged |

## 1. Binding decision and precedence

The sole Product Owner of an installation is the account created and established as the installation's Product Owner account during the attended application installation/bootstrap process. The installation process must create that account and bind its exact IdentityAccess AccountId to the installation identifier as part of the controlled bootstrap ceremony.

There is exactly one Product Owner principal per installation. The binding is immutable and non-transferable. It cannot be reassigned, transferred, replaced, succeeded, deleted or changed to another AccountId by ordinary administration, a special transfer ceremony, recovery, restore or support operation. This supersedes the v0.1 proposal's change/transfer lifecycle.

Recovery is permitted only to restore authentication and access for the same bound AccountId/principal. A recovery workflow may restore credentials/session usability for that same account; it may not enroll a different account as Product Owner. If identity continuity for the original AccountId cannot be cryptographically and operationally established, Product Owner privileged operations remain unavailable and fail closed. There is no alternate-owner or successor fallback.

The Product Owner identity must not be inferred from `super_admin`, System Manager, username, display name, email/mobile, a client claim, GitHub identity, machine administrator status or request payload.

## 2. Ownership and identity boundaries

- IdentityAccess remains the authoritative owner of AccountId, credentials, sessions, roles and permissions.
- Platform is the authority for the installation/system-scope identifier and the platform bootstrap trust boundary.
- The accountable owner of the binding aggregate and its public contract must be explicitly designated by IdentityAccess and Platform; this document does not unilaterally assign that implementation ownership.
- The binding links the installation identifier to one opaque AccountId. It is not itself an IdentityAccess role or a general permission grant.
- The installation-created Product Owner account does not automatically receive System Manager privileges, DBA privileges, database ownership, ledger posting rights or WORM administration rights. Distinct capabilities remain subject to the approved identity-separation matrix and independently authorized grants.
- Password rotation, username/profile change and ordinary credential maintenance preserve the same AccountId and do not mutate the owner binding.

## 3. Protected binding record and invariants

A future protected record may contain only:
- immutable BindingId;
- immutable installation identifier;
- exact opaque AccountId;
- schema/contract version and integrity metadata;
- installation/bootstrap evidence reference and trusted creation timestamp;
- non-secret audit correlation/reference metadata.

It must not contain password hashes, bearer/session tokens, recovery codes in plaintext, private signing keys or credentials owned by another module.

Normative invariants:
1. One installation identifier maps to exactly one immutable Product Owner AccountId.
2. No second active binding, owner replacement, transfer or successor is permitted.
3. The AccountId must exist as the installation-created owner account at bootstrap.
4. A successful binding cannot be changed through application API, domain service, ordinary database role or a client-supplied flag.
5. The owner account cannot be deleted, disabled, replaced or have its protected owner identity removed through ordinary user-management operations. Credential lockout/session revocation controls may operate only while preserving the AccountId and immutable binding; those controls do not create another owner.
6. If binding cardinality, installation scope, AccountId, integrity, version or resolver state is missing, inconsistent, ambiguous, unavailable or unverifiable, resolve as `Unavailable` / `NotOwner` and deny owner-only operations.
7. Restoring a backup must restore the same installation identifier and same AccountId binding. Restore must never re-enroll or substitute an owner. If continuity cannot be proved, privileged Product Owner authority remains unavailable.

## 4. Bootstrap and initial binding

The binding must be established by the trusted, attended installation/bootstrap process, not by a later ordinary System Manager action. The installer must:
- create the installation scope identifier;
- create/establish the initial Product Owner account through IdentityAccess;
- atomically or through a recoverable, single-use controlled ceremony associate that exact AccountId with the installation;
- record completion with secret-free, tamper-evident audit intent;
- prevent ordinary business/API listeners and owner-only privileged functions from treating the installation as ready until binding integrity and account identity are verified.

No static password, reusable master secret, plaintext bootstrap credential or request-supplied AccountId is accepted as proof. The still-open DP-02 installer trust controls, one-time secret handling, expiry, verifier, listener enablement and secret/key custody must be approved by the accountable Platform/IdentityAccess/Security owners before implementation.

The Product Owner is not a second account that can be designated later. The owner identity is the installation-created account selected in the initial bootstrap ceremony. Its exact AccountId becomes the immutable installation binding.

## 5. Recovery, unrecoverability and restore

Recovery restores only the same bound AccountId. It must rely on the separately accepted IdentityAccess recovery mechanism and current security controls; it must not create a new Account, map a different AccountId, import a client-selected identity or use a database/OS administrator claim as identity proof.

The previously accepted email-only IdentityAccess recovery policy (one-time OTP, 600-second TTL, dedicated HMAC key outside PostgreSQL, bounded attempts/send quotas, 24-hour lock and enumeration-resistant responses) remains a separate IdentityAccess control. This binding document neither weakens nor independently re-approves that policy.

If the original account's credentials are lost, the recovery operation may restore credentials for the same AccountId after identity proof. If the original AccountId has been deleted, replaced, corrupted beyond proof or cannot be securely recovered, no alternate Product Owner is enrolled. Privileged owner operations fail closed. Escalation is to incident handling and evidence preservation, not ownership transfer.

Backup/restore must verify installation ID and owner AccountId continuity. A backup lacking the binding or containing a conflicting owner binding is not eligible for privileged restoration without controlled investigation; it must not silently select the most recent or highest-privilege user.

## 6. Runtime authorization contract

A future IdentityAccess/Platform contract must expose a typed server-resolved result:
- `BoundToCurrentPrincipal(installationId, accountId, bindingVersion)`;
- `NotOwner`;
- `Unavailable`.

Resolve against the current authenticated AccountId/session and verified installation binding. Do not accept caller-supplied owner flags, identity values or mutable claims. Binding integrity is checked at the server-side authorization boundary. Any cache must have a formally accepted invalidation/consistency model; fail closed if a current decision cannot be established.

Organization may consume this decision only for the previously approved narrow Product Owner direct path for Branch/Warehouse assignment create/change/revoke, including self-assignment, without a second approver. This is not a blanket bypass of Organization permissions, resource validity, RowScope or audit. It grants no IdentityAccess credential/session access, impersonation, role/permission mutation, database administration, business posting or WORM administration.

## 7. Required acceptance tests

Before the binding can be implemented or used, tests must prove:
- installer binds exactly the newly created owner AccountId to the correct installation identifier;
- duplicate installation bindings, a second owner, owner transfer, successor enrollment and AccountId replacement are rejected;
- ordinary super_admin/System Manager, DBA, service identity or client owner flags cannot obtain owner authority;
- username, email/mobile, display name, GitHub login and local OS administrator claims cannot create or alter owner authority;
- API, domain service and database permission boundaries reject update/delete/replace/disable of the protected owner identity/binding through ordinary paths;
- credential/username/profile rotation preserves the exact AccountId binding;
- recovery restores only the original AccountId and rejects any alternate AccountId;
- missing/corrupt/stale/mismatched binding, installation ID mismatch, unavailable resolver and ambiguous state fail closed;
- backup restore preserves the exact installation ID and owner AccountId and never re-enrolls an owner;
- OwnerAuthority is not a substitute for IdentityAccess permission or Organization resource assignment;
- the narrow direct assignment exception works only for the designated owner and remains audited, reasoned, idempotent and concurrency/version protected;
- owner authority cannot read credentials/sessions, impersonate accounts, alter IdentityAccess roles/permissions, mutate ledger or delete/shorten WORM evidence.

## 8. Disposition and non-authorization

This document records the Product Owner policy decision and a reviewable design boundary. The implementation owner and public contract, bootstrap trust controls, persistence/lifecycle protection, IdentityAccess recovery integration, backup/restore continuity, threat model, tests and independent review remain open.

No binding table, schema, DDL, migration, seed, account grant, production runtime, recovery route, credential, secret, platform resource or Organization consumer is created or authorized by this document.

**Disposition:** binding policy — OWNER-ACCEPTED; technical design/owner acceptance — OPEN; implementation evidence — NOT PROVIDED; O20-E OPEN; G1 NOT GRANTED.

**END — MDG-IDENTITYACCESS-PRODUCT-OWNER-PRINCIPAL-BINDING-001 v0.2**
