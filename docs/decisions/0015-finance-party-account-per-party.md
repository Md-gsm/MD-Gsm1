# ADR-0015 — Finance-owned Party subledger account

- Status: Accepted for the isolated Finance G1 implementation slice
- Date: 2026-11-05
- Scope: Finance, Persons Contracts, PostgreSQL persistence

## Decision

1. Finance owns the identity of a Party subledger account. Persons owns Party identity and issues the stable `PartyReference`; Persons registration never creates a financial account.
2. A Party has at most one Finance party-account identity. PostgreSQL enforces this with a non-null unique constraint on `finance_party_accounts.party_id`; application pre-checks are not the uniqueness guarantee.
3. Provisioning is lazy: Finance creates the account inside the first applicable financial operation, not when a Party is registered or merely assigned a role.
4. `PostgresPartyAccountProvisioner` requires the caller's open Npgsql connection and transaction. Its upsert returns the existing account on replay and serializes concurrent first-use attempts. Account creation therefore commits or rolls back with the financial operation.
5. The table stores only account identity and creation audit metadata. It stores no balances, credit limits, payment methods, or ledger postings. Balances are derived from posted Finance transactions.
6. No cross-module FK to `persons` is created. The Party reference comes from the Persons contract. Party merge/deactivation does not delete or silently reassign the financial account; future transfer requires an explicit audited financial operation.

## Verification

- Migration `0004_finance_party_accounts.sql` adds the unique Party constraint.
- PostgreSQL integration tests exercise 12 concurrent first-use requests, transaction rollback, and distinct Party isolation.
- Linux and Windows migration gates apply four migrations and verify second-run idempotence.

## Non-claims

This slice does not implement a general ledger, receivable/payable postings, credit-limit policy, Finance API, desktop integration, or production deployment. Global `g1Authorized` remains false until remaining G1 prerequisites and evidence are satisfied.
