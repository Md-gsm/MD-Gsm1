# Stage 4 — HTTP API contracts

## Success responses
Successful versioned API endpoints use `ApiResponse<T>` with `data` and `requestId`. The request identifier is the ASP.NET Core trace identifier for the current request. `/health/live` remains a minimal liveness endpoint and does not use the envelope.

## Errors
Errors use ASP.NET Core Problem Details (`application/problem+json`). Unhandled exceptions return HTTP 500 with a generic title and a `requestId` extension. Internal exception messages and stack traces are not returned. The handler logs the request ID and exception type only. Status-code pages are enabled for otherwise empty error responses.

## Versioning
Technical and future business routes use explicit path versioning, currently `/api/v1/...`. Breaking changes require a new major route version. `/health/live` is infrastructure health and remains unversioned.

## Input validation
Future input endpoints must validate at the HTTP boundary and return HTTP 400 Problem Details for invalid input, with field errors that do not echo sensitive values. No business-input endpoint or business validation rule is implemented in this stage.

## Request IDs
`HttpContext.TraceIdentifier` is returned as `requestId`. It is diagnostic metadata, not an authentication credential. Distributed tracing and externally supplied correlation-ID policy are deferred.

## Current endpoints
- `GET /health/live`: process liveness only; no database readiness claim.
- `GET /api/v1/system/info`: non-sensitive technical metadata only.

## Not implemented
Authentication, authorization, database access, accounting operations, pagination conventions, idempotency keys, rate limiting, and API version negotiation remain outside this stage.

## Desktop client (stage 5)
The Desktop calls `GET /api/v1/system/info` through `MDGsm.Client`, requires the `data` and `requestId` fields, and treats non-success responses as a safe typed exception. It does not display raw server response bodies. The default local development endpoint is `http://127.0.0.1:5080/`.
