# Stage 4 — HTTP API contracts

## Success responses
API response shapes are currently endpoint-specific and documented in their versioned OpenAPI contracts; there is not yet a single envelope applied consistently across all existing endpoints. The request identifier is diagnostic metadata for error responses. `/health/live` remains a minimal liveness endpoint.

## Errors
Errors use ASP.NET Core Problem Details (`application/problem+json`). Unhandled exceptions return HTTP 500 with a generic title and a `requestId` extension. Internal exception messages and stack traces are not returned. The handler logs the request ID and exception type only. Status-code pages are enabled for otherwise empty error responses.

## Versioning
Current routes are mixed-version: `/api/v1/system/info`, `/api/v1/identity/*`, and `/api/v2/persons/{partyId}`. This is implementation state, not a fully approved versioning policy. Before adding/changing public routes, approve compatibility/deprecation rules, OpenAPI grouping and contract tests. Do not rewrite Persons v2 to v1 without a compatibility decision. `/health/*` remains unversioned infrastructure health.

## Input validation
Future input endpoints must validate at the HTTP boundary and return HTTP 400 Problem Details for invalid input, with field errors that do not echo sensitive values. No business-input endpoint or business validation rule is implemented in this stage.

## Request IDs
`HttpContext.TraceIdentifier` is returned as `requestId`. It is diagnostic metadata, not an authentication credential. Distributed tracing and externally supplied correlation-ID policy are deferred.

## Current endpoints
- `GET /health/live`: process liveness only; no database readiness claim.
- `GET /api/v1/system/info`: non-sensitive technical metadata only.
- `POST /api/v1/identity/login` and `POST /api/v1/identity/password/change`: feature-gated IdentityAccess operations; login defaults off.
- `GET /api/v2/persons/{partyId}`: protected Persons detail endpoint; response maps through the versioned `PartyResponse` contract and defaults to masked/non-sensitive fields.\n- PR #96 (Draft, not merged): canonical `POST /api/v2/persons` and `POST /api/v2/persons/legal` use transactional duplicate resolution, require both `persons.create` and `persons.read`, require `Idempotency-Key`, and remain disabled by the server-side `Persons:WriteApiEnabled` flag. There is no separate direct-create HTTP bypass.

## Not implemented
A unified API versioning policy and negotiation/deprecation strategy remain open. Persons Search HTTP is still withheld pending persistent quotas and abuse-lock controls; full ContactPoint/Address lifecycle and the remaining module acceptance criteria are not complete. The PR #96 write routes are a Draft candidate, are disabled by default, and do not authorize production activation.

## Desktop client (stage 5)
The Desktop calls `GET /api/v1/system/info` through `MDGsm.Client`, requires the `data` and `requestId` fields, and treats non-success responses as a safe typed exception. It does not display raw server response bodies. The default local development endpoint is `http://127.0.0.1:5080/`.
