# IdentityAccess — Stage 01 Audit and Gap Register

| Field | Value |
|---|---|
| Document ID | MDG-IDENTITYACCESS-STAGE-01-AUDIT-001 |
| Version / Date | 0.1 / 2026-11-24 |
| Status | **STAGE 01 SOURCE AUDIT COMPLETE — IMPLEMENTATION GAPS REGISTERED; STAGE 02 NOT STARTED** |
| Repository / Branch | `Md-gsm/MD-Gsm1` / `security/o20-o21-final-closure` |
| PR | #92 — Draft / open / unmerged |
| Exact audited HEAD | `4fd4ef26b1356105b0197c683b00f4bb64727294` |
| Controlling baseline | `MDG-IDENTITYACCESS-MODULE-BASELINE-001 v1.2` |

## 1. Scope and limitations

This closes the first of the eight approved IdentityAccess stages: source-and-test audit. The review inspected the controlling module baseline, owner decision documents, current API composition root and middleware, Domain/Application/Infrastructure source, IdentityAccess test inventory, O-46 design documents, current readiness registers, PR #92 and WG-0 Issue #93.

This is a repository source review and exact-head CI evidence review. It is not an independent Security/Architecture review, live production database inspection, production deployment test, or G1 authorization. A handler's existence or a green general workflow is not accepted as proof of a complete production feature.

No code, migration, DDL, PostgreSQL principal, grant, seed, bootstrap/recovery runtime, production listener, or G1 activation was created or enabled during this audit.

## 2. Exact-head CI

At exact HEAD `4fd4ef26b1356105b0197c683b00f4bb64727294`, GitHub reported **17/17 check-runs completed successfully**:

`persons-ci-evidence`, `CodeQL`, `persons-api-contract`, `persons-temporary-data-retention`, `persons-merge-contract`, `persons-architecture-boundary`, `foundation-build-test`, `persons-spec-lint`, `persons-postgres-migrations`, `persons-security-tests`, `persons-postgres-windows-migrations`, `persons-e2e-disposable`, `persons-ci-enforcement-test`, `persons-consumer-registry`, `windows-preview-installer`, `project-charter-owner-gate`, `codeql-csharp`.

The four associated workflow runs were also completed/success: MD-Gsm Persons Module Gates, Project Charter Owner Gate, Windows Persons Preview Installer, and CodeQL.

These results establish repository workflow success at this exact SHA only. They do not prove every IdentityAccess acceptance scenario below, and do not replace independent review, production proof or G1. Historical test totals and green SHAs in older documents are not transferred to this HEAD without exact-head artifact evidence.

## 3. Gap register

Status: **PARTIAL** means code exists but contract/wiring/atomicity/acceptance evidence is incomplete; **MISSING** means no usable runtime path was found; **NOT VERIFIED** means fail-closed design exists but live lifecycle proof is absent; **PASS (bounded)** applies only to the narrow behavior stated.

| ID | Requirement | Source and test evidence at audited HEAD | Status / remaining work |
|---|---|---|---|
| IA-01 | Username/password login; email/mobile not login identifiers | `LoginHandler`, `Username.Parse`, `/api/v1/identity/login`; HTTP login flag defaults off. Hardening, HTTP E2E and PostgreSQL integration tests exist; exact-head security/E2E checks green. | **PARTIAL** — pin Unicode version/case-fold behavior; collision/fuzz/conformance evidence; retain feature gate until release approval. |
| IA-02 | Versioned password hashing and policy | `PlatformPasswordHasher`, versioned hash fields, `PasswordPolicy` (12–128 UTF-16 code units); boundary tests. | **PARTIAL** — review parameters/rehash, secret/configuration handling and all future provisioning/recovery paths. |
| IA-03 | Five failures in 15 minutes lock for 15 minutes | `Account.RecordFailedLogin`; account row selected `FOR UPDATE`; persisted failed-attempt PostgreSQL tests. | **PARTIAL** — prove exact time-window edges, DB-clock/concurrency behavior, enumeration equivalence and lockout DoS interaction. |
| IA-04 | Generic login failure / source throttling | Dummy hash for unknown/disabled/locked cases; HMAC source digest; direct peer IP, no forwarded-header trust; source throttle is 20 failures/5 minutes. | **PARTIAL** — add HTTP/timing-equivalence tests for malformed/unknown/disabled/locked/bad-password cases; verify distributed throttling and abuse characteristics. |
| IA-05 | Opaque session, digest-only storage, lease and heartbeat | Opaque token factory, 32-byte digest, PostgreSQL session store; initial policy 5 distinct accounts, heartbeat 30s, lease 180s; middleware resolves live DB session. | **PARTIAL** — verify actual UoW/serializable registration, concurrent capacity and retry/unknown-commit behavior; do not add absolute TTL or per-account cap. |
| IA-06 | Five distinct active-account capacity | Handler checks same-account exemption and distinct active accounts; UoW interface comment says serializable and locks singleton policy row; PostgreSQL test names concurrent capacity. | **PARTIAL / BLOCKER** — inspect actual UoW implementation and prove simultaneous cross-process logins at capacity, lease expiry, same-account second session and serialization retry. |
| IA-07 | Own password change revokes all sessions | `ChangeOwnPasswordHandler` updates hash/version, revokes sessions, appends audit inside UoW; HTTP E2E tests old token rejection/relogin. | **PARTIAL** — prove actual transaction wraps account update + revocation + audit; add rollback/injected-failure and concurrent-login tests. |
| IA-08 | Admin reset of another account | `AdminResetPasswordHandler` checks `identity.credentials.reset`, denies `super_admin`, sets forced-change and revokes sessions; reset-policy tests cover permission/target cases. | **PARTIAL** — handler is not registered in API composition root; no authenticated admin-reset endpoint/account-management surface found. Implement in stage 4 after dependencies and permission enforcement. |
| IA-09 | Forced password change after ordinary admin reset | Reset handler sets `MustChangePassword`; middleware restricts authenticated session to password-change route; middleware and HTTP E2E tests exist. | **PASS (bounded)** — ensure future route inventory cannot bypass middleware. Initial Product Owner is explicitly exempt from forced change after trusted Wizard setup. |
| IA-10 | Logout and session revocation | `LogoutHandler` revokes by digest and returns uniform 204; E2E/PostgreSQL tests exist. | **PARTIAL** — logout is not protected by middleware; explicitly review its unauthenticated/idempotent semantics, abuse and audit failure handling. |
| IA-11 | Route-by-route authentication coverage | Middleware currently protects only `/api/v2/persons`, password-change and heartbeat; login public by design. | **PARTIAL / BLOCKER** — logout is outside middleware; no admin reset, account, role, recovery or bootstrap routes exist. Produce a route inventory before adding any endpoint. |
| IA-12 | Roles and permission catalog | `RoleCatalog` and granular `PermissionCatalog`; approved matrix/catalog tests pass in exact-head CI. | **PARTIAL** — role/permission administration and approved DB seed/reconciliation remain gated/not implemented. Unknown keys must deny; no seed/activation before approval. |
| IA-13 | Account lifecycle and role assignment | Persistence ports/catalog exist; no complete account create/disable/update/role-assignment API/handlers found in Host composition root. | **MISSING** — stage 4/5 implementation with exact permission, audit, duplicate/disable/session-revocation, transaction and race tests. |
| IA-14 | Email OTP Recovery runtime | `IRecoveryStore` and PostgreSQL create/consume primitives exist; email-only, digest-only; new challenge revokes prior unconsumed challenge. No orchestration, provider delivery or endpoint found. | **MISSING (runtime)** — stage 3: centrally managed MD-Gsm Google/Gmail sender (no customer SMTP settings), 10-minute OTP, max 5 attempts, 24-hour lock, resend 1/hour and 5/24h, DB-clock atomic counters, delivery ambiguity, reset/version bump/session revocation, replay/race/failure E2E. Email recovery is not MFA; SMS/offline codes remain out of scope. |
| IA-15 | Initial Product Owner bootstrap and binding | Owner-accepted design: server-local Wizard, Named Pipe, immutable InstallationId→AccountId, one-time challenge. No runtime claim/binding path found. Bootstrap test only asserts fixed pair absence. | **MISSING (runtime)** — stage 2: close threat review, fingerprint/restart-safe idempotency, SID/IPC proof, WG-0 reservation and PostgreSQL 18 ACL/concurrency tests before any DDL. |
| IA-16 | Product Owner authority | Public contract exposes typed decision; baseline requires `Unavailable` until binding lifecycle exists. | **NOT VERIFIED / fail-closed by design** — preserve `Unavailable` until protected binding, same-AccountId recovery and restore continuity are proven. Never infer owner from role, username, email, caller flag or OS SID. |
| IA-17 | Audit completeness | Audit writer persists IdentityAccess event rows; selected login/password/logout/reset facts are appended. | **PARTIAL** — verify append-only enforcement, atomicity, fail behavior, secret-free allow-list, complete denied/success events, and canonical AuditSecurity integration/retention. |
| IA-18 | PostgreSQL atomicity and failure semantics | Account lookups lock rows; UoW interface documents serializable execution; disposable PG integration tests and migration workflows green. | **PARTIAL** — verify exact UoW implementation/DI, retry strategy, cancellation, rollback, unknown commit, lock ordering and isolation under concurrent requests. No live production DB inspected. |
| IA-19 | Unicode username normalization | NFC, 3–64 scalars, letter/digit categories, invariant uppercase and hand-coded script groups. Source comment explicitly leaves Script_Extensions/CaseFolding pinning open. | **PARTIAL / BLOCKER** — hand-coded groups are not full Unicode Script_Extensions; implement reviewed pinned Unicode data, collision scan and fuzz/conformance vectors. |
| IA-20 | Independent review and G1 | `mrdeljoe4` is requested reviewer; no completed independent review or G1 authorization recorded. | **MISSING / RELEASE BLOCKER** — stage 8 requires exact-final-SHA review/retests and separate explicit Product Owner G1 authorization. |

## 4. Material code findings

### 4.1 Recovery storage is not Recovery

The `IRecoveryStore` interface and PostgreSQL challenge methods are persistence primitives only. They do not implement OTP issuance/verification orchestration, resend and attempt counters, provider delivery, endpoint protection, reset transaction, or provider timeout/unknown-delivery semantics. Therefore Recovery runtime is not implemented.

### 4.2 Bootstrap design is not bootstrap runtime

The controlling baseline accepts the policy constraints, not production implementation. Issue #93 confirms candidate migration `0010` is **NOT RESERVED**. The two fixed-signature SECURITY DEFINER routines remain design-only. Fingerprint v1, stable salted-hash idempotency across restart/unknown commit, exact signatures/lock primitive, independent residual-risk review, Windows Named Pipe/SID proof and PostgreSQL 18 adversarial proof remain open. No DDL or routine installation is authorized.

### 4.3 Route protection is incomplete

`SessionAuthenticationMiddleware` bypasses every path other than Persons, password-change and heartbeat. Login is intentionally public and feature-gated. Logout is also outside middleware. No admin-reset, account-management, role-management, Recovery or bootstrap route was found in `Program.cs`. A route inventory and explicit protection policy are required before exposing these features.

### 4.4 Transaction atomicity needs implementation-level evidence

Handlers call `IIdentityAccessUnitOfWork`, and the port comment describes serializable execution and locking of the singleton session-policy row. The audit must not treat that comment as proof of the concrete implementation. Password changes, session capacity, account lockout and audit require transaction-level rollback/race/unknown-commit tests against PostgreSQL.

### 4.5 Username normalization is a concrete technical gap

The implementation uses NFC plus `ToUpperInvariant()` and a manually enumerated script grouping. It does not yet provide the accepted version-pinned Unicode Script_Extensions and CaseFolding behavior. This is a release blocker requiring a deterministic Unicode data version, collision scan and fuzz/conformance evidence.

## 5. Documentation reconciliation

1. `MDG-IDENTITYACCESS-MODULE-BASELINE-001_v1.2.md` is the controlling IdentityAccess baseline. It accepts module boundaries/policies but expressly leaves implementation and release gates open.
2. `identityaccess-five-open-items-fa.md` contains historical 2026-11-17 status and references to old PR #31 / an earlier gap register. Treat those as historical; current work is PR #92. PR #31 must not be described as open.
3. `MDG-SECURITY-O20E-G1-READINESS-REVIEW-001_v4.2.md` and `MDG-SECURITY-STAGE-08-REGISTER-001_v6.9.md` are O20/O21 readiness records, not IdentityAccess completion certificates. Their previous test totals/green SHAs are not evidence for this audited HEAD.
4. `MDG-SECURITY-O46-BOOTSTRAP-PHYSICAL-SCHEMA-AND-ACL-PROPOSAL-001_v0.4.md` has a reported filename/body version mismatch. Reconcile header, version register, references and checksum before Stage 2; do not silently rename.
5. Initial Product Owner account is completed in trusted local Wizard and is **not** forced to change password after first login. Ordinary admin reset forced-change behavior remains separate.
6. Recovery sender decision is a centrally managed MD-Gsm Google/Gmail account configured only during final release preparation. Customers do not configure SMTP. Owner email is recipient only; no SMS path is approved.

## 6. Stage 01 exit decision

**Stage 01 source audit is complete.** The controlling baseline, source paths, test inventory, current PR state and exact-head CI were checked; requirements have been classified and blockers registered.

This does **not** mean IdentityAccess is complete or G1-ready. The remaining work continues in the approved order:

1. Stage 2 — Bootstrap.
2. Stage 3 — Recovery.
3. Stage 4 — Account management.
4. Stage 5 — Roles and permissions.
5. Stage 6 — Session and authentication hardening.
6. Stage 7 — Security/integration/concurrency/failure tests.
7. Stage 8 — Independent exact-SHA review and separate G1 authorization.

No production activation, migration/DDL, role/grant, permission seed, Owner binding, Recovery sender credential, production listener, G1 authorization or PR merge is implied or authorized by this audit.

## 7. Stage 02 entry checklist

1. Reconcile physical-schema proposal filename/version and decision register.
2. Close fingerprint v1 canonicalization and stable salted-hash/idempotency across restart and unknown commit.
3. Review exact routine signatures, lock primitive/key derivation and fixed data-access order.
4. Complete Windows Named Pipe, local SID, remote-client rejection and service-identity threat review.
5. Obtain WG-0's explicit migration-number/path decision; until then `0010` remains unreserved.
6. Define PostgreSQL 18 adversarial catalog/ACL/privilege/concurrency/idempotency tests and Windows host proof.
7. Keep Product Owner authority fail-closed (`Unavailable`) until binding lifecycle is proven.

**END — IdentityAccess Stage 01 Audit v0.1**
