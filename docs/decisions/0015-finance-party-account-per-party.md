# ADR-0015 — Finance-owned Party subledger account

- Status: Accepted for the isolated Finance G1 implementation slice
- Date: 2026-11-05
- Scope: Finance, Persons Contracts, PostgreSQL persistence

## Decision

1. Finance owns the identity of a Party subledger account. Persons owns Party identity and validates that a supplied `PartyReference` identifies an existing, active Party. Constructing the DTO alone is not proof of existence.
2. The Persons-owned validator executes `SELECT ... FOR SHARE` using the caller's connection and transaction. Unknown or inactive Parties are rejected; the row lock prevents deactivation until the financial transaction commits or rolls back. No cross-module FK is created.
3. A Party has at most one Finance party-account identity. PostgreSQL enforces this with a non-null unique constraint on `finance_party_accounts.party_id`; application pre-checks are not the uniqueness guarantee.
4. Provisioning is lazy: Finance creates the account inside the first applicable financial operation, not when a Party is registered or merely assigned a role.
5. `PostgresPartyAccountProvisioner` requires the caller's open Npgsql connection and READ COMMITTED transaction, the Persons validator, and a server-side `IFinanceActorContext`. ActorId must come from the authenticated principal, not request data.
6. Provisioning uses `INSERT ... ON CONFLICT DO NOTHING` followed by a separate SELECT. The unique constraint resolves concurrent first-use races; READ COMMITTED ensures the subsequent statement sees the committed winner. Replays do not update the row or alter creation metadata.
7. The table stores only account identity and creation audit metadata. It stores no balances, credit limits, payment methods, or ledger postings. No unused row_version is stored.
8. Party deactivation does not delete or silently reassign the financial account; future transfer requires an explicit audited financial operation.

## Verification

- Migration `0004_finance_party_accounts.sql` adds the unique Party constraint without a cross-module FK or unused row_version.
- PostgreSQL integration tests exercise concurrent first-use requests, rollback, distinct Party isolation, unknown/inactive Party rejection, replay metadata preservation, and actor validation.
- Linux and Windows migration gates apply four migrations and verify second-run idempotence.

## Non-claims

This slice does not implement a general ledger, receivable/payable postings, credit-limit policy, Finance API, desktop integration, or production deployment. Global `g1Authorized` remains false until remaining G1 prerequisites and evidence are satisfied.
