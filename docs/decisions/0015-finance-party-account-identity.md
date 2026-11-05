# ADR-0015 — Finance-owned party receivables account identity

- Status: accepted for the Finance G1 account-provisioning slice
- Date: 2026-11-05
- Scope: Finance / Accounts Receivable and its Persons PartyId reference

## Context

Persons owns the canonical natural/legal party identity. A receivables detail account is a financial record and must therefore be owned by Finance, not by Persons. The V1 product is single-company. The business rule is one dedicated receivables detail account per Party, but creating an account for every new person or role would produce unused financial records.

## Decisions

1. **Lazy creation.** A party account is created only on the first financial use that requires a receivables account. Person registration, contact creation, role assignment, and profile edits do not provision it.
2. **One account per Party.** `finance.party_accounts.party_id` is NOT NULL and UNIQUE. This is the authoritative concurrency guard; application checks alone are insufficient.
3. **Idempotent first use.** Finance uses one PostgreSQL `INSERT ... ON CONFLICT (party_id) DO UPDATE ... RETURNING` statement. Retries and concurrent first-use requests return the same immutable `account_id`; the original creation actor and timestamp are retained.
4. **Caller-owned transaction.** The caller supplies an active PostgreSQL transaction. Finance uses that exact connection and transaction and never commits or disposes it. If the financial operation fails and the caller rolls back, account creation rolls back with it (ADR-0005).
5. **Permanent account identity.** Account rows cannot be physically deleted. Closing an account does not release its PartyId or permit a replacement account. The account ID remains the stable reference for future financial documents.
6. **Cross-module boundary.** Finance stores PartyId as a canonical identity reference. It does not create a Persons-owned table, write Persons data, or introduce a cross-module foreign key. The Finance use case validates the reference through the read-only Persons `IPartyIdentityLookup` contract.
7. **Authenticated actor.** The host supplies Finance's actor context from the authenticated server-side identity boundary. No actor ID is accepted from a client request.
8. **Merge is not automatic.** Person merge must preserve all existing financial document links and account IDs. If only the source has an account, the merge participant may transfer that account reference without rewriting documents. If both parties have accounts, the accounts remain distinct; any balance movement requires an explicit traceable accounting document. The merge remains pending until that transfer is completed. The operation must be transactional, idempotent, retryable, and recoverable after failure. No account is deleted or silently collapsed.

## Schema and executable proof

- Migration `0004_finance_party_accounts.sql` adds the Finance-owned account table and unique PartyId constraint.
- The PostgreSQL adapter uses the caller's Npgsql transaction and the unique constraint as the race arbiter.
- Integration tests cover lazy creation, repeat calls, rollback, concurrent first use, closed-account reservation, and deletion rejection.
- The migration is embedded in the application-owned migrator and is applied only to marked disposable databases in CI. No production database is modified.

## Explicit non-claims

This slice does not implement the general ledger, chart of accounts, postings, credit-limit policy, payment settlement, customer-facing API, Desktop workflow, or the complete person-merge coordinator. Those remain separate Finance/Accounting stages. Persons G1 authorization remains governed by its own entry checklist; this ADR authorizes only this Finance account-provisioning slice.
