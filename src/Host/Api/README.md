# API Host

This directory contains the executable `MDGsm.Api` host project and its composition root. It references shared building blocks under `src/BuildingBlocks/*` and the versioned public Contracts assemblies under `src/Modules/*/Contracts`.

The API host is the server-side boundary for persistence, database connections, configuration, and secrets. It may compose module contracts, but it must not reach into module-private Domain or Infrastructure implementations.

## Protected Persons request boundary

- Requests under `/api/v1/persons` require a single opaque Bearer token in the Authorization header.
- The host hashes the token and resolves its digest against PostgreSQL on every request. Resolution requires a non-revoked, unexpired session, an enabled account, and matching current credential version.
- A successful request renews the server-side lease and installs the account ID as an authenticated server principal. Client-provided account/actor IDs are never read.
- The Persons endpoint requires the live `persons.read` permission and obtains its audit actor only from `IAuthenticatedActorContext`; sensitive fields are separately authorized through the live `persons.sensitive.read` permission store.
- Invalid/missing sessions return 401; unavailable session infrastructure returns 503; persistence/permission failures do not fall back to allow.
## IdentityAccess HTTP login and forced password change

- `POST /api/v1/identity/login` accepts username, password, and a client-instance UUID. The source-rate-limit key is an HMAC digest of the direct peer IP; proxy-supplied forwarding headers are not trusted.
- A successful response returns a 256-bit random opaque Bearer token (43-character Base64Url form), lease expiry, and the mandatory-password-change flag. Only the SHA-256 digest is persisted; the raw token is returned once and is never logged.
- Invalid credentials return a uniform 401; source throttling returns 429; active-user capacity rejection returns 409. Login remains fail-closed if database or HMAC configuration is absent.
- `POST /api/v1/identity/password/change` requires the issued Bearer token. A session marked `MustChangePassword` can call only this endpoint; all Persons routes return 403 until the password is changed. Password change increments credential version and revokes every existing session, so the client must log in again.
- The HTTP login route is disabled unless `IdentityAccess:HttpLoginEnabled=true` is explicitly set. The default is disabled. This is a feature gate, not production authorization: no production listener, TLS topology, production migration, permission seed, bootstrap credential, or G1 activation is enabled by this change.
- The disposable PostgreSQL HTTP end-to-end test starts the real API host, obtains a token over HTTP, verifies forced-change restriction, changes the password, proves the old token is revoked, logs in again, and reaches the protected Persons permission boundary.
