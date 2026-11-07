# Persons API endpoint placement

This directory documents the intended endpoint placement. The current protected detail endpoint `GET /api/v1/persons/{partyId}` is implemented in the API host. It uses the PostgreSQL-validated IdentityAccess session boundary and enforces `persons.sensitive.read` for sensitive fields.

Create/search endpoints and the complete operational write API remain incomplete. Do not infer G1 authorization or production readiness from the existing detail endpoint. Keep endpoint contracts versioned and enforce authentication, permission and data scope on the server.
