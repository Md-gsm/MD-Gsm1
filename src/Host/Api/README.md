# API Host

This directory contains the executable `MDGsm.Api` host project and its composition root. It references shared building blocks under `src/BuildingBlocks/*` and the versioned public Contracts assemblies under `src/Modules/*/Contracts`.

The API host is the server-side boundary for persistence, database connections, configuration, and secrets. It may compose module contracts, but it must not reach into module-private Domain or Infrastructure implementations.

## Protected Persons request boundary

- Requests under `/api/v1/persons` require a single opaque Bearer token in the Authorization header.
- The host hashes the token and resolves its digest against PostgreSQL on every request. Resolution requires a non-revoked, unexpired session, an enabled account, and matching current credential version.
- A successful request renews the server-side lease and installs the account ID as an authenticated server principal. Client-provided account/actor IDs are never read.
- The Persons endpoint obtains its audit actor only from `IAuthenticatedActorContext`; sensitive fields are separately authorized through the live IdentityAccess permission store.
- Invalid/missing sessions return 401; unavailable session infrastructure returns 503; persistence/permission failures do not fall back to allow.
- No login HTTP endpoint, production migration, permission seed, bootstrap credential, or G1 activation is introduced by this change. Existing login application handlers remain the only token-issuance implementation pending their separately reviewed host integration.
