# API Host

This directory contains the executable `MDGsm.Api` host project and its composition root. It references shared building blocks under `src/BuildingBlocks/*` and the versioned public Contracts assemblies under `src/Modules/*/Contracts`.

The API host is the server-side boundary for persistence, database connections, configuration, and secrets. It may compose module contracts, but it must not reach into module-private Domain or Infrastructure implementations.

## Protected Persons request boundary

- Requests under `/api/v2/persons` require a single opaque Bearer token in the Authorization header.
- The host hashes the token and resolves its digest against PostgreSQL on every request. Resolution requires a non-revoked, unexpired session, an enabled account, and matching current credential version.
- A successful request renews the server-side lease and installs the account ID as an authenticated server principal. Client-provided account/actor IDs are never read.
- The protected `GET /api/v2/persons/{partyId}` endpoint requires `persons.read`; the audit actor comes only from `IAuthenticatedActorContext`. Sensitive fields are separately authorized through live `persons.sensitive.read` permission checks.
- PR #96 (Draft, not merged) adds canonical `POST /api/v2/persons` and `POST /api/v2/persons/legal` routes. Both call the transactional duplicate-resolution workflows, require `persons.create` and `persons.read`, require `Idempotency-Key`, and remain disabled unless the server-side `Persons:WriteApiEnabled` setting is explicitly enabled. There is no direct-create HTTP bypass.
- Invalid/missing sessions return 401; unavailable session infrastructure returns 503; persistence/permission failures do not fall back to allow.
## IdentityAccess HTTP login and forced password change

- `POST /api/v1/identity/login` accepts username, password, and a client-instance UUID. The source-rate-limit key is an HMAC digest of the direct peer IP; proxy-supplied forwarding headers are not trusted.
- A successful response returns a 256-bit random opaque Bearer token (43-character Base64Url form), lease expiry, and the mandatory-password-change flag. Only the SHA-256 digest is persisted; the raw token is returned once and is never logged.
- Invalid credentials return a uniform 401; source throttling returns 429; active-user capacity rejection returns 409. Login remains fail-closed if database or HMAC configuration is absent.
- `POST /api/v1/identity/password/change` requires the issued Bearer token. A session marked `MustChangePassword` can call only this endpoint; all Persons routes return 403 until the password is changed. Password change increments credential version and revokes every existing session, so the client must log in again.
- The HTTP login route is disabled unless `IdentityAccess:HttpLoginEnabled=true` is explicitly set. The default is disabled. This is a feature gate, not production authorization: no production listener, TLS topology, production migration, permission seed, bootstrap credential, or G1 activation is enabled by this change.
- The disposable PostgreSQL HTTP end-to-end test starts the real API host, obtains a token over HTTP, verifies forced-change restriction, changes the password, proves the old token is revoked, logs in again, and reaches the protected Persons permission boundary.
