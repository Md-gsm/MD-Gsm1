# Versioning rules for Persons contracts

- Use a versioned OpenAPI contract before adding any Persons route.
- Compatible additive changes may remain in the same API major version only after consumer-contract tests pass.
- Removing/renaming a field, changing meaning, tightening validation, changing authorization scope, or changing idempotency semantics is breaking and requires a major contract decision/ADR.
- Store a compatibility/diff result as a CI artifact tied to commit SHA; redact personal data.
- Consumer registrations must be updated in the same change that begins storing/transmitting PartyId.
- A module may not persist PartyId until it has a reviewed participant contract for Party merge.


## Cross-module identity identifier

- The only published cross-module identity identifier is opaque `PartyId` (UUIDv7); wire representations use `PartyId` in contracts and `party_id` in persisted schemas where applicable.
- A consumer must register its source path, contract version, and merge protocol version before it stores or transports PartyId. Registry validation scans both `PartyId` and `party_id` and rejects legacy `PersonId` / `person_id` outside explicit negative-test fixtures and policy documentation.
- Contracts assemblies must not reference Persons Domain, Infrastructure, DbContext, or persistence entities. Each consumer owns its own contract DTO/value representation; UUID serialization is the interoperability boundary.
- A module may not persist PartyId until it has a reviewed participant contract for any future Party merge operation. This rule does not imply merge execution is in V1.
