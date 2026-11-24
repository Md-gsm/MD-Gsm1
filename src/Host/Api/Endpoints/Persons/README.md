# Persons API endpoint placement

The API host exposes the protected detail route GET /api/v2/persons/{partyId}, direct natural/legal registration routes POST /api/v2/persons and POST /api/v2/persons/legal, and quick-registration routes POST /api/v2/persons/quick/natural and POST /api/v2/persons/quick/legal.

All Persons routes require a PostgreSQL-validated IdentityAccess bearer session. Registration also requires server-side permission checks and a non-empty Idempotency-Key. Quick registration requires both persons.read and persons.create. Duplicate candidates are returned with masked mobile values and require explicit candidate selection unless ForceNew is requested.

All write routes are fail-closed behind Persons:WriteApiEnabled, whose default is false. The Desktop client does not set or override this server configuration. Search/list HTTP exposure is deliberately not implemented yet because the approved persistent per-actor/per-purpose rate limits, global warning counter, abuse-lock and audited admin-unlock control plane still need an Owner/WG-0 migration reservation.

These routes represent partial implementation progress, not Persons G1 authorization, production readiness, schema-change authorization or production activation. Authentication, permission checks, duplicate resolution and data-scope decisions remain server-side.
