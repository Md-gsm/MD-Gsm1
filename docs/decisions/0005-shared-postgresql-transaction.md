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
