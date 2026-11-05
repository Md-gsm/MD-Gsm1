# Modular application boundary

This directory records the approved target topology. Each module owns its Domain, Application, Infrastructure, and versioned Contracts. Cross-module implementation dependencies are prohibited; consumers reference published Contracts only.

## Current implementation state

- **Finance:** G1 implementation has started with a narrowly scoped runtime slice for Finance-owned Party subledger account identity. It uses a unique PartyId constraint and transaction-bound idempotent provisioning. It does not create ledger entries, balances, API endpoints, or production deployment behavior.
- **Persons:** domain/application/infrastructure remain transitional under BuildingBlocks and require coordinated relocation.
- **IdentityAccess and RepairOperations:** Contracts exist; business runtime integration is not complete.

The Finance slice does not change the global Persons G1 authorization flag. Production activation remains blocked until all applicable architecture, identity, security, migration, and acceptance evidence is complete.
