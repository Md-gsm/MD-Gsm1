# ADR-0005 — Shared PostgreSQL transaction across module DbContexts

- Status: accepted V1 constraint.
- Persistence: PostgreSQL 18 with EF Core/Npgsql.

## Decision

When a V1 use case must update data owned by multiple modules in one atomic database operation, all participating DbContexts must explicitly share the same open `DbConnection` and the same `DbTransaction`. The transaction boundary is coordinated by the application use case/composition infrastructure and is observable in integration tests.

Database writes across modules must not be implemented as staged commits, eventual consistency, or compensating transactions. Staging/compensation is reserved for effects outside the PostgreSQL transaction boundary (for example, external services or file/object storage).

## Required proof before use

- Integration tests prove one shared connection and transaction across participating DbContexts.
- Injected failure in any participant rolls back every database write.
- Retry/idempotency and concurrent execution behavior are tested.
- No module bypasses its own persistence contract to write another module's tables directly.

## Scope guard

This ADR does not authorize adding business tables or starting G1. It defines a mandatory V1 constraint for later approved implementation.


## Clarification — independent module DbContexts

Each module owns and maintains its own DbContext and mappings. A module DbContext must not contain DbSet/mappings for another module's owned tables. A single global DbContext aggregating all modules is prohibited. Atomic cross-module use cases enlist these independent DbContexts in the same already-open DbConnection and DbTransaction; transaction coordination does not transfer data ownership. This clarification is part of ADR-0005 and requires architecture/integration tests proving both independent context ownership and shared transaction enlistment/rollback.

## Idempotency payload privacy

A payload digest that can be brute-forced over a low-entropy PII domain (including mobile numbers) must never be stored as an unkeyed hash. If payload equality must be persisted, use server-keyed HMAC-SHA-256 over a minimized canonical payload, store key version with the MAC, keep keys outside database/source control, support rotation, compare in constant time, and never log/export the MAC or source PII. Prefer storing only operation/result identifiers when payload equality is not required.
