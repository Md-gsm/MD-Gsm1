# Versioning rules for Persons contracts

- Use a versioned OpenAPI contract before adding any Persons route.
- Compatible additive changes may remain in the same API major version only after consumer-contract tests pass.
- Removing/renaming a field, changing meaning, tightening validation, changing authorization scope, or changing idempotency semantics is breaking and requires a major contract decision/ADR.
- Store a compatibility/diff result as a CI artifact tied to commit SHA; redact personal data.
- Consumer registrations must be updated in the same change that begins storing/transmitting PersonId.
- A module may not persist PersonId until it has a reviewed participant contract for person merge.
