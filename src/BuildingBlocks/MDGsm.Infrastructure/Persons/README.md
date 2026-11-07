# Persons Infrastructure — transitional implementation placement

The current PostgreSQL Persons repository/persistence implementation is located in this shared BuildingBlocks project as a documented transitional placement under ADR-0014. Persons business migration(s) are registered with the application-owned DbMigrator; CI validates them against disposable PostgreSQL 18 databases only.

The module-owned target boundary remains Persons; this physical placement does not transfer data ownership. No production/real-data database migration is claimed, and G1 remains unauthorized.
