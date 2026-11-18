# Versioning rules for Persons contracts

- Use a versioned OpenAPI contract before adding any Persons route.
- Compatible additive changes may remain in the same API major version only after consumer-contract tests pass.
- Removing/renaming a field, changing meaning, tightening validation, changing authorization scope, or changing idempotency semantics is breaking and requires a major contract decision/ADR.
- Store a compatibility/diff result as a CI artifact tied to commit SHA; redact personal data.
- Consumer registrations must be updated in the same change that begins storing/transmitting PersonId.
- A module may not persist PersonId until it has a reviewed participant contract for person merge.


## Current Party contract baseline

- Current public Party contract: **2.0**.
- The 2.0 breaking change removes Finance-, Pricing-, and Treasury-owned credit, discount, and payment-instrument fields from Persons request/response contracts and moves the protected party-details route to `/api/v2`.
- Primary mobile remains optional; when supplied it must pass Persons normalization and validation.
- The registered ApiHost consumer is updated to `Persons.Contracts/2.0` in the same change.
- No production migration is run by this contract change; legacy columns remain until an independently reviewed data migration plan exists.
