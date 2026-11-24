# MD-GSM IdentityAccess Module Baseline

| Field | Value |
|---|---|
| Document ID | MDG-IDENTITYACCESS-MODULE-BASELINE-001 |
| Version / Date | 1.2 / 2026-11-24 |
| Status | **IDENTITYACCESS MODULE-OWNER ACCEPTED BASELINE — O-46 OWNER BOOTSTRAP DECISIONS RECONCILED; IMPLEMENTATION AND RELEASE GATES OPEN** |
| Module owner | `Md-gsm` (also Product Owner; one existing person/account) |
| Independent reviewer | `mrdeljoe4` (existing reviewer; no project membership expansion) |
| Public contract assembly | `src/Modules/IdentityAccess/Contracts` |
| Public contract version | `1.2` |
| Governing owner decisions | `identityaccess-owner-decisions-fa.md`; `identityaccess-five-open-items-fa.md`; `MDG-SECURITY-O46-BOOTSTRAP-OWNER-DECISION-001_v0.1.md`; accepted Product Owner policy decisions recorded in PR #92 |

## 1. Purpose and acceptance scope

This baseline freezes the IdentityAccess module's own responsibility boundary, its public cross-module contract surface, and the already accepted Product Owner policies that implementation must obey.

This is an IdentityAccess-owner acceptance only. It does **not** accept Organization's command matrix, Organization role mapping or permission namespace, a Platform-wide lock order, a production consumer, or any post-G1 Organization implementation. Those remain separate owner responsibilities and gates.

The baseline does not declare IdentityAccess G1, production-ready, or authorized for production activation. Existing runtime feature flags, migration restrictions, security review, exact-head CI and release controls remain binding.

## 2. Human responsibility boundary

Only the two existing human participants are recognized for this work:

- `Md-gsm`: Product Owner and IdentityAccess Module Owner. The same account holds both capacities; this is not an additional person.
- `mrdeljoe4`: Independent Security/Architecture Reviewer. The reviewer examines the exact source SHA, tests and evidence, records findings, and cannot unilaterally change product policy or substitute for owner acceptance.

No Platform Technical Owner, developer, manager, team, or additional project participant is appointed. Platform duties in this baseline are technical contracts, not human assignments.

## 3. IdentityAccess data and capability ownership

IdentityAccess is the sole authoritative owner of:

1. Account identity and account lifecycle, including enabled/disabled state.
2. Username normalization and uniqueness rules.
3. Password credentials, password versioning, password-change and administrative-reset state.
4. Login-attempt state and account lockout.
5. Authenticated sessions, opaque bearer-token digests, session lease/heartbeat, revocation and concurrent-account policy.
6. Role definitions, permission catalog, account-role membership and role-permission grants.
7. IdentityAccess recovery challenges and their one-time consumption/rate-limit state.
8. Product Owner installation binding and its protected lifecycle (not yet implemented).
9. Emergency Access request, approval, execution authorization and lifecycle identity facts (technical runtime remains separately gated).
10. IdentityAccess-originated audit facts and audit intent. Canonical AuditSecurity storage/retention and immutable evidence infrastructure remain under their separately assigned boundary; IdentityAccess must not claim ownership of that canonical store.

IdentityAccess does not own Persons, Branch, Warehouse, Inventory, Finance, Backup contents, Organization assignment facts, or canonical AuditSecurity/WORM storage. Other modules must not read IdentityAccess private tables or depend on its Domain, Application, or Infrastructure implementation types.

## 4. Published public contract boundary — API 1.2

The only public cross-module IdentityAccess assembly is `MDGsm.IdentityAccess.Contracts`. Its current published version marker is `ModuleContractVersion.Current = "1.2"`.

The public authorization interface is `IIdentityAccessAuthorizationContract` and exposes:

1. `ResolveCurrentPrincipalAsync`: resolve only the current server-validated request principal; no caller-supplied AccountId, actor ID, role, owner flag, token, or session secret.
2. `EvaluateCurrentPermissionAsync(permissionKey)`: evaluate one exact permission key against the current live AccountId/SessionId, credential version, enabled account, non-revoked/unexpired session and active permission grant.
3. `EvaluateCurrentPermissionInTransactionAsync(permissionKey, connection, transaction)`: re-evaluate the exact permission on the caller's already-open local PostgreSQL connection and transaction, locking every IdentityAccess row contributing to an Allow decision until that transaction ends.
4. `ResolveCurrentProductOwnerAuthorityAsync`: return a typed Product Owner binding decision. Until the protected installation binding lifecycle is implemented and accepted, this operation returns Unavailable.

Typed outcomes are authoritative: principal Authenticated/Unauthenticated/Unavailable; permission Allowed/Denied/Unavailable; Product Owner BoundOwner/NotOwner/Unavailable. Unknown keys, missing or stale sessions, connection/transaction mismatch and persistence failures never produce Allow. Cancellation propagates.

The transaction-scoped API is an IdentityAccess authorization participant, not approval of a global cross-module lock order. It does not guarantee that a lease which was valid when the guard query ran remains valid until a later commit. Cross-module mutation consumers must not rely on it until the affected owners accept lock order, transaction duration, lease-expiry linearization, timeout/deadlock/retry/recovery and replay semantics.

Public DTOs and decisions must not expose password hashes, bearer tokens, recovery secrets, persistence entities or private module ports. All internal Application/Infrastructure ports remain private to IdentityAccess.

## 5. Accepted product policies that constrain implementation

The following are already accepted Product Owner policies and are frozen as implementation constraints. Their acceptance does not imply that runtime implementation or seed data is complete.

### 5.1 Login and username

- Login uses username and password only. Email and mobile are contact/recovery channels, not login identifiers.
- Username may contain supported digits, letters, or their combination under the accepted normalization policy.
- Username normalization policy is NFC, no silent trimming, bounded Unicode scalar length, permitted letter/decimal-digit categories, preservation of digit scripts, versioned Latin case folding, and rejection of unsafe mixed-script identifiers. The exact Unicode data version, .NET implementation, collision scan and fuzz evidence remain required technical work.

### 5.2 Password and account lockout

- A user may change their own password after proving the current password.
- Only an administrator may reset another user's forgotten password.
- A user-initiated change or administrative reset must increment credential version and revoke all sessions for the affected account atomically.
- Administrative reset requires a forced password change at the next login.
- Five failed login attempts within the accepted 15-minute window cause a 15-minute account lock. Login responses must not disclose whether an account exists. Source rate limiting is supplemental and must not become an easy denial-of-service vector against a victim account.
- Password storage uses a supported, versioned password hasher. Plaintext, reversible storage, custom cryptography, fixed/shared passwords and master-password bypasses are prohibited.

### 5.3 Sessions and concurrent accounts

- A session remains usable while the application is open and its heartbeat/lease is renewed.
- Initial policy: heartbeat 30 seconds, lease 180 seconds, and a configurable initial cap of five distinct active accounts across the system.
- Capacity counts distinct active accounts; another session for an already-active account does not consume another account slot.
- The cap can be changed only through its dedicated permission and an auditable administrative action.
- No absolute session TTL and no per-account simultaneous-session cap have been approved. Neither may be invented or implemented.
- PostgreSQL is authoritative. Local authorization/session caches cannot override revocation, account disablement or credential-version changes.

### 5.4 Recovery

- Email-only recovery is accepted for this phase and is explicitly **not MFA**.
- Email OTP is one-time, 10 minutes, digest-only in storage, maximum five failed attempts per challenge, followed by a 24-hour recovery lock.
- Resend is limited to at most one per rolling hour and five per rolling 24 hours; violating the limit causes the accepted 24-hour lock. Limits/counters must be server-side, atomic and based on the database clock.
- Resending invalidates the prior challenge. Provider timeout or ambiguous delivery is never proof of successful delivery.
- SMS and offline recovery codes are not required in this phase. No SMS provider, SDK, credential or sending path is authorized.
- SMTP provider/domain/secrets, complete orchestration, endpoint, delivery evidence, end-to-end replay/race/database-failure tests and independent mailbox-compromise threat review remain open. The Recovery runtime is not accepted as complete.

### 5.5 Initial Product Owner account and exclusive bootstrap path

The later explicit Product Owner decision in `MDG-SECURITY-O46-BOOTSTRAP-OWNER-DECISION-001_v0.1.md` controls this section and supersedes any earlier general first-login forced-change wording for the initial Product Owner account.

- The initial Product Owner account is created only by the one-time local Setup Wizard running on the designated central server host. No API/HTTP/TCP route, Windows desktop client, administrative UI, CLI, script, migration, seed, support procedure or direct routine database operation may create, replace, transfer or re-enroll Product Owner authority.
- The local Wizard uses OS-local IPC; for the Windows server host the accepted transport is a Windows Named Pipe. Remote clients are rejected and the pipe ACL is restricted to the exact OS-authenticated local operator SID plus the minimum required service identities. The OS SID authorizes only local Wizard invocation; it is not the Product Owner identity or an OwnerAuthority source.
- The accepted root-of-trust assumption includes the Windows OS Administrator/SYSTEM and PostgreSQL deployment/database-owner boundaries. The local IPC design does not claim to resist a malicious actor with full privileges over those roots.
- The challenge has at least 256 bits of CSPRNG entropy, a maximum 30-minute lifetime, one-time local delivery, verifier-only database persistence, and binding to the InstallationId, SetupOperationId, purpose and authorized local SID. Claim is serialized and single-use; replay, mismatch, expiry, DB failure and unknown commit outcomes fail closed.
- The central InstallationId is UUIDv7 and remains unchanged across reinstall, Repair, update and restore. The exact first Product Owner AccountId is bound immutably to that InstallationId; transfer, second owner, successor and alternate enrollment are prohibited.
- The Wizard collects the Product Owner's final username, password and required profile fields. PasswordPolicy is enforced and only a supported versioned password hash is stored.
- **The initial Product Owner is not forced to change username, password or profile after the first login.** Successful completion of the trusted local Wizard is the initial credential/profile completion step. The initial owner bootstrap must not set `must_change_password=true` solely because the account was just created.
- This does not remove the owner's normal ability to change their own password later. Current-password proof, PasswordPolicy, credential-version increment and session revocation remain applicable. Existing reset/forced-change rules for other users are unaffected.
- No fixed, guessable, shared, embedded or reusable bootstrap credential is allowed. There is no second owner-creation path. If the original owner AccountId cannot be securely recovered, privileged Product Owner operations fail closed; recovery may restore only the same AccountId.
- Owner-policy decisions above are accepted. However, the threat review, runtime design, schema/migration reservation, deployment-principal boundary, seed/role review, implementation, PostgreSQL evidence, secret-sentinel scans, independent review and release gates remain open. No production credential, listener, migration, seed, role or feature activation is authorized by this baseline.

### 5.6 Roles and permissions

- The accepted role vocabulary is: `super_admin`, `manager`, `receptionist`, `technician`, `warehouse`, `salesperson`, `purchaser`, `cashier`, `accountant`, `viewer`.
- The granular permission matrix is owner-approved: deny-by-default, explicit permission keys only, blank cells deny, wildcard grants prohibited, and multiple roles combine only through the approved explicit grant union.
- Technician access is limited to self/assigned records and authoritative scope enforcement. Separation-of-duties and audit requirements remain mandatory.
- Manager does not receive IdentityAccess account/role/security administration authority through a generic manager role.
- Policy approval is not permission seed, grant, production role creation or runtime activation. The exact seed review, deployment gate and tests remain open.
- Adding new permission keys or cross-module permissions requires explicit owner review of the full role-to-permission matrix. A role name or Product Owner binding cannot implicitly grant a permission.

### 5.7 Product Owner binding

- Product Owner is an installation-scoped, unique, immutable and non-transferable binding to one existing AccountId.
- Recovery restores only that same AccountId; it must not create a replacement owner identity.
- Product Owner authority is never inferred from username, email, role name, SuperAdmin, client claim, GitHub identity or caller-supplied flag.
- The narrow Product Owner exception applies only to Branch/Warehouse assignment allocation/change/revocation and only to the second-approver requirement. Authentication, exact authorization, source validity, concurrency, idempotency, history and audit remain required.
- No broader IdentityAccess, session, credential, role, permission, impersonation or emergency-access bypass is created.
- The public contract currently returns Unavailable because the protected binding lifecycle is not implemented. This is the required fail-closed behavior until that lifecycle is built and accepted.

## 6. Host authentication boundary

The Host is responsible for applying the IdentityAccess session middleware to every protected route. The middleware must derive AccountId, SessionId, credential version and authenticated-at time only after opaque-token digest resolution against PostgreSQL and live-session validation. Client-supplied actor IDs or claims are ignored.

At the baseline source snapshot, middleware protection is explicitly limited to Persons, password-change and heartbeat routes. Login is feature-flagged off by default; route-by-route coverage, logout semantics, administrative/recovery endpoints and future module routes require explicit review before exposure. A public contract registration alone does not make an HTTP route authenticated.

A module consumer must fail closed when the authorization dependency is unavailable. Test-only stubs do not constitute PostgreSQL or production evidence.

## 7. Emergency Access boundary

Emergency Access is not a permanent role, SuperAdmin expansion, impersonation mechanism, alternate login, or bypass of IdentityAccess's own account/session/credential controls.

The existing IA-01..IA-03 document remains a technical proposal pending its own exact-source reconciliation, owner acceptance and independent review. The accepted O20-F policy constraints it must preserve include: one independent approver for ordinary operations; requester/approver/operator separation; two distinct approvers for isolated Backup restore; maximum 60-minute grant with fresh approval for any extension; synchronous expiry; a closed operation allow-list and explicit deny-list; immediate revocation and operator-session closure; idempotency and attributable audit.

This baseline does not publish or activate the Emergency Access proposal, create its runtime API/schema/migration/seed/role/credential, or grant production access.

## 8. Explicit non-acceptances and open gates

The following remain OPEN and are not silently accepted by this baseline:

1. Implement and verify the accepted exclusive local Wizard bootstrap path for the initial Product Owner, including local IPC/ACL threat review, immutable InstallationId binding, challenge handling, deployment-principal separation and first-owner atomicity. No post-login forced credential change applies to this initial owner.
2. Complete email recovery workflow, provider integration, atomic limits and end-to-end PostgreSQL tests.
3. Complete account administration endpoints/handlers and admin-reset authorization.
4. Exact permission seed/reconciliation, full runtime role administration and all route-level authorization coverage.
5. Product Owner binding enrollment, immutable storage, same-AccountId recovery, disable/restore and PostgreSQL concurrency proof.
6. Emergency Access technical contract acceptance, runtime implementation and IA-01..IA-03 PostgreSQL proof.
7. Durable audit integration and end-to-end audit completeness.
8. .NET Unicode conformance, version pin, collision scan and fuzz testing.
9. PostgreSQL concurrency/security tests for lockout, session capacity, revocation, recovery replay/races and failure handling.
10. Exact-head CI, independent Security/Architecture review and explicit release/G1 authorization.

Organization-specific permission keys/role mapping, Organization data-owner concurrence, Platform global lock order, transaction/lease linearization and any production Organization consumer remain separate post-baseline gates. Organization production implementation remains post-G1.

## 9. Non-authorization

This baseline authorizes contract and implementation work within IdentityAccess's existing module boundary only. It does not authorize a production listener, production migration, production credential/bootstrap, permission seed/grant, Emergency Access grant, Product Owner binding activation, Organization runtime, G1, O-20/O-21 closure, Go-Live or PR merge.

PR #92 must remain Draft, open and unmerged until all applicable technical gates, exact-SHA CI, independent review and owner acceptance are complete.

## 10. O-46 owner decision reconciliation — 2026-11-23

The owner decision recorded in `MDG-SECURITY-O46-BOOTSTRAP-OWNER-DECISION-001_v0.1.md` is controlling for the first Product Owner account. It supersedes the v1.0 wording that required the initial administrator to change username, password and profile at first login. The initial owner's final credentials/profile are completed in the trusted server-local Wizard; there is no forced change after the first login. All ordinary password-change and other-user password-reset controls remain in effect.

The owner-accepted route is exclusively the server-local Wizard using the accepted local IPC trust boundary. This policy does not accept or authorize an implementation or a production setup route. O46-G01's owner-decision component is recorded; technical threat review and implementation evidence remain open.


**Successor note:** This v1.1 baseline supersedes v1.0 for the initial Product Owner bootstrap/first-login policy only. All other v1.0 contract boundaries and non-acceptances remain unchanged unless explicitly reconciled above.

**END — MDG-GSM IdentityAccess Module Baseline v1.1**


## Product Owner authority and recovery amendment — 2026-11-24

The Product Owner has designated **MDG-ARCH-MASTER-001 v3.0.4** as the sole current, accepted, official and governing Architecture/Accounting Master. Earlier v3.0.3-only authority statements in prior records are historical and superseded by baseline registration v1.3 and project authority control v1.0.

The Product Owner's recovery decision is now explicit:
1. The initial Product Owner identity and binding are immutable. Recovery may restore only the original AccountId and may not create a second owner, replacement, transfer or successor.
2. Owner recovery has two alternative methods: (a) a recovery code chosen by the Product Owner in the initial Setup Wizard, with only a securely salted/versioned verifier/hash persisted and bounded attempts/audit; or (b) a single-use Email OTP sent to the verified owner email.
3. The Product Owner's recovery does not require approval by the Product Owner account itself. Recovery of ordinary user accounts remains under Product Owner authority.
4. The product uses one centrally managed Google/Gmail sender account owned by MD-Gsm. Store customers enter only the recipient email address and do not configure SMTP, sender mailbox credentials or OAuth. The central sender account and authorization are created/configured during final release preparation, not customer setup.
5. Sender credentials/tokens must not be embedded in distributed binaries, source control or store databases. A protected central sending component/service, secure transport, OAuth/API authorization, token custody, delivery monitoring, abuse controls and privacy/retention rules remain design, implementation and test gates. The owner-defined recovery code remains independent of email-service availability.
6. This policy decision does not authorize a live Gmail account, OAuth client/secret, production sender service, runtime endpoint, database migration, grant, activation or Go-Live.

This amendment changes owner-policy and architecture authority only. All implementation, schema reservation, technical threat review, exact-head tests, independent review and release gates remain independently open unless separately evidenced.

**END — MDG-IDENTITYACCESS-MODULE-BASELINE-001_v1.2.md**
