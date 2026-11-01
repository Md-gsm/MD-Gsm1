# Persons Domain

Implementation status: **G1 initial slice in progress**.

The project is isolated under `src/Modules/Persons/Domain` and has no dependency on Host, EF Core, Infrastructure, API, UI, or other module internals.

Implemented:
- `PartyId`: opaque UUIDv7 value object shared by natural persons and legal entities. UUIDv7 exposes an approximate creation timestamp; PartyId is not a secret or authorization credential.
- UUIDv7 version and RFC variant validation at construction/parsing boundaries.
- Canonical UUID string representation.

Not yet implemented:
- Party aggregate and natural/legal profile variants.
- Contact normalization and contact invariants.
- Persistence, mappings, tables, migrations, API endpoints, or workers.

Do not treat this slice as module activation or as proof of any acceptance criterion beyond its own unit tests.
