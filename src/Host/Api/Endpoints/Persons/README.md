# Persons API endpoint placement

The protected detail route is `GET /api/v2/persons/{partyId}`. PR #96 (Draft, not merged) adds two canonical registration routes:

- `POST /api/v2/persons` for natural persons.
- `POST /api/v2/persons/legal` for legal entities.

Both POST routes use `QuickRegisterNaturalAsync` / `QuickRegisterLegalAsync` inside a caller-owned PostgreSQL transaction. They resolve a matching mobile, reuse a same-name Party, return masked candidates for explicit selection, or create a new Party only when no match exists or `ForceNew` is explicitly requested. A separate direct-create HTTP route is not exposed.

All Persons routes require a PostgreSQL-validated IdentityAccess bearer session. Registration additionally requires live `persons.create` and `persons.read` checks, a UUID `Idempotency-Key`, and server-derived actor identity. Candidate disclosure is recorded in the append-only access audit before candidate data is returned.

All write routes are fail-closed behind `Persons:WriteApiEnabled`, whose default is false. The Desktop client cannot set or override this server configuration. Search HTTP is implemented with persistent per-actor/per-purpose quotas, a global warning counter, the approved abuse-lock policy and audited admin unlock; it remains absent unless `Persons:SearchApiEnabled` is explicitly enabled (default false).

These routes are a partial implementation candidate, not Persons G1 authorization, production readiness, schema-change authorization or production activation. Contact/Address lifecycle, Outbox, complete merge-consumer acceptance and independent security review remain open. Search activation remains disabled pending the applicable G1/release gates and fresh independent review.

## Search security boundary (PR #96)

Search is gated by `Persons:SearchApiEnabled` (default false) and the same server-side,
fail-closed `PersonsGlobalReadAuthorization` used by Party details. Technician is denied
from global Search until authoritative assignment-based row scope exists. Denials are
audited before quota consumption or database search. Search responses are allow-listed to
PartyId, PartyType, display name and masked mobile; internal business roles, city and
creation timestamps are not serialized. Per-actor quotas and the approved Abuse Lock are
additional controls, not substitutes for authorization.
