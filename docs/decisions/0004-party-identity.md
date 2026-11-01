# ADR-0004 — Party identity and person types

- Status: accepted design constraint; implementation is not authorized by this ADR alone.
- Scope: V1 identity model.

## Decision

- Use one public identifier named `PartyId` for both natural persons and legal entities.
- Generate `PartyId` as UUIDv7. The identifier is globally unique and must not encode business meaning.
- Model natural and legal person types explicitly under the shared party identity. Do not create separate incompatible identity namespaces for individuals and organizations.
- The Persons module owns party identity and contact data.
- A technician's work role belongs to Persons. Technician skills, capabilities, scheduling, and assignment belong to RepairOperations.
- Salary, commission, payroll, and compensation do not belong to Persons; they are owned by Finance/Payroll.
- The default phone region is Iran (IR, +98). Normalize Persian and Arabic digits; normalize a valid domestic `09…` mobile to `+989…`. Reject ambiguous or invalid numbers rather than guessing.

## Constraints

- No implementation, schema, or API is implied until G1 is separately authorized.
- External modules must use versioned Contracts and register every persisted or transported PartyId consumer.
- Legacy `PersonId` / `person_id` naming is not an accepted new contract.


## Stage 2 clarification (proposed baseline)

- Invalid, incomplete, or ambiguous mobile numbers are rejected; no unverified-contact persistence path is permitted.
- Persons exposes WorkClassification values Staff, Employee, and Technician separately from commercial roles and IAM permissions. Technician skills, capabilities, scheduling, and assignment remain owned by RepairOperations.
- New cross-module contracts use opaque PartyId only. PersonId/person_id are forbidden legacy identifiers; this ADR's mention above is solely an explicit prohibition and must never be copied into a new contract.
