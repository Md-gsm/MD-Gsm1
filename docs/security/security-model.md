# Security model — current baseline and implementation status

## Trust boundaries

1. Desktop is an untrusted client. Hiding a menu or disabling a button is not authorization.
2. API is the enforcement boundary. Every protected operation must authenticate the caller and authorize the requested action on the server.
3. Domain/Application code must not trust user IDs, roles, totals, account IDs, or permission claims supplied by the client without server-side validation.
4. Infrastructure owns credential and persistence integrations; secrets must not be embedded in the executable or repository.

## Implemented in part

- IdentityAccess has server-side account/session/security runtime backed by PostgreSQL.
- Opaque session tokens are validated at the API boundary; the raw token must not be persisted or logged.
- Protected Persons detail reads use the IdentityAccess session boundary; sensitive fields require the explicit `persons.sensitive.read` permission.
- HTTP login and password-change paths are implemented and covered by API/PostgreSQL disposable integration tests.
- HTTP login is guarded by `IdentityAccess:HttpLoginEnabled` and defaults to disabled.
- The current main CI includes security tests and CodeQL; green CI is not a substitute for an independent threat-model/security review.

## Required controls and open work

- Deny by default; permission and data scope are checked on every protected API request. UI visibility is never an authorization control.
- Use maintained password hashing and versioned credential handling; plaintext, reversible passwords and custom cryptography are prohibited.
- Enforce the agreed login lockout policy (five failed attempts in the defined window, followed by a 15-minute lock) and independent source rate limiting.
- Session validity, lease/heartbeat, concurrent-account cap, revocation and credential-version changes must be enforced server-side against PostgreSQL.
- Complete initial administrator bootstrap and mandatory first-login profile/password change before enabling normal operations.
- Complete the administrator recovery protocol with independent email/mobile verification and offline one-time recovery material; no backdoor or fixed production credential.
- Complete granular permission matrix review, audit retention decisions, secret-free audit verification, TLS/firewall deployment review and negative authorization tests.
- Complete production hardening and independent security/privacy review on the exact release commit.

## Explicit limits

Persons create/search and full operational Desktop write flows remain incomplete. HTTP login remains disabled by default; no production authentication deployment or production listener/TLS rollout is claimed. Persons temporary-data cleanup runtime and merge execution remain incomplete. No production/real-data database migration has been performed.

`g1Authorized=false` remains mandatory until all acceptance evidence, independent reviews and explicit Project Owner authorization are recorded. This document is not a security certification or authorization to activate G1.
