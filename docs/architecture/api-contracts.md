# Stage 4 — HTTP API contracts

## Success responses
API response shapes are currently endpoint-specific and documented in their versioned OpenAPI contracts; there is not yet a single envelope applied consistently across all existing endpoints. The request identifier is diagnostic metadata for error responses. `/health/live` remains a minimal liveness endpoint.

## Errors
Errors use ASP.NET Core Problem Details (`application/problem+json`). Unhandled exceptions return HTTP 500 with a generic title and a `requestId` extension. Internal exception messages and stack traces are not returned. The handler logs the request ID and exception type only. Status-code pages are enabled for otherwise empty error responses.

## Versioning
Current routes are mixed-version: `/api/v1/system/info`, `/api/v1/identity/*`, and `/api/v2/persons/{partyId}`. This is implementation state, not a fully approved versioning policy. Before adding/changing public routes, approve compatibility/deprecation rules, OpenAPI grouping and contract tests. Do not rewrite Persons v2 to v1 without a compatibility decision. `/health/*` remains unversioned infrastructure health.

## Input validation
Input DTOs are validated at the HTTP boundary and mapped through strict versioned contracts. The Persons registration candidates return a generic HTTP 422 `validation_failed` response and do not echo submitted PII; duplicate identifiers and cross-actor idempotency conflicts return HTTP 409. This route-specific behavior is documented in the Persons OpenAPI contract. A single cross-module validation/error policy remains open.

## Request IDs
`HttpContext.TraceIdentifier` is returned as `requestId`. It is diagnostic metadata, not an authentication credential. Distributed tracing and externally supplied correlation-ID policy are deferred.

## Current endpoints
- `GET /health/live`: process liveness only; no database readiness claim.
- `GET /api/v1/system/info`: non-sensitive technical metadata only.
- `POST /api/v1/identity/login` and `POST /api/v1/identity/password/change`: feature-gated IdentityAccess operations; login defaults off.
- `GET /api/v2/persons/{partyId}`: protected Persons detail endpoint; response maps through the versioned `PartyResponse` contract and defaults to masked/non-sensitive fields.
- `POST /api/v2/persons` and `POST /api/v2/persons/legal`: PR #96 Draft candidates for natural/legal registration. Both always use transactional duplicate resolution, require `persons.create` and `persons.read`, derive actor from the authenticated server session and require `Idempotency-Key`. They remain disabled by `Persons:WriteApiEnabled` unless explicitly switched on server-side; this PR has not been merged or approved for activation.

## Not implemented
A unified API versioning policy and negotiation/deprecation strategy remain open. Persons Search HTTP is still withheld pending persistent quotas and abuse-lock controls; full ContactPoint/Address lifecycle and the remaining module acceptance criteria are not complete. The PR #96 write routes are a Draft candidate, are disabled by default, and do not authorize production activation.

## Desktop client (stage 5)
The Desktop calls the API through `MDGsm.Client` and `MDGsm.Host.Client`. The Persons workspace supports IdentityAccess login/password-change/logout, canonical natural/legal duplicate-aware registration, explicit candidate selection, retry with the same operation key, and masked/non-sensitive Party details. Opaque bearer tokens and pending keys remain in memory only; the client never connects directly to PostgreSQL or enables server feature flags. The default local development endpoint is `http://127.0.0.1:5080/`.
