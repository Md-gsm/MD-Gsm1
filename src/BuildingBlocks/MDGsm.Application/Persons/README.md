# Persons Application — transitional implementation placement

The current Persons application ports/use-case components are located in this shared BuildingBlocks project as a documented transitional placement under ADR-0014. The module-owned target boundary remains Persons; this physical placement does not transfer data ownership.

Implementation is partial. Create/search, the full Contact Point lifecycle, temporary-data cleanup and merge workflows remain incomplete. Consumers must depend on published Contracts, not another module's internal implementation.
