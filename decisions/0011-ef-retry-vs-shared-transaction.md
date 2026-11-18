# ADR-0011 — EF Core execution strategy (retry) vs ADR-0005 shared transaction

- Status: PROPOSED — draft by Claude for Project Owner / architecture review. Not a decision.
- Related: ADR-0005, `PersistenceRegistration.cs` (`EnableRetryOnFailure(maxRetryCount: 3)`).

## Problem
`EnableRetryOnFailure` installs a retrying execution strategy. EF Core rejects a user-initiated transaction (`BeginTransaction`) unless the whole unit of work runs inside `strategy.ExecuteAsync(...)`. ADR-0005 requires several module DbContexts to share one connection and transaction. Code that opens that shared transaction without the strategy wrapper will throw at runtime; code that wraps it must be fully re-executable (idempotent, no external side effects inside the delegate), otherwise a retry can repeat work.

No runtime shared-transaction code exists yet, so this is latent, not an active defect.

## Options
1. **Keep retry, mandate the wrapper.** The application orchestrator runs the whole use case inside one `CreateExecutionStrategy().ExecuteAsync`, with the shared connection/transaction created inside it. Requires every delegate to be re-runnable; external effects stay outside (consistent with ADR-0005's staging rule).
2. **Disable retry for module contexts.** Simplest; no transient-fault masking. Failures surface to the caller, which relies on `OperationId` idempotency (V1) to retry safely.
3. **Retry only for read-only contexts** (separate registration).

## Recommendation (advisory)
Option 2 for V1 (single local PostgreSQL, loopback; transient faults are rare), revisit when the deployment topology (ADR-0012) includes a network hop. Whichever is chosen, ADR-0005's "required proof" must include a test that runs the shared-transaction path with the chosen strategy and an injected transient failure.

## Decision
Pending owner.
