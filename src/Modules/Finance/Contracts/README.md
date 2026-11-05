# Finance Contracts

Finance publishes versioned contracts for financial records and payroll. Finance/Payroll owns salary, commission, payroll, and financial records; these do not belong to Persons.

## Accounts Receivable — contract v1.0

- `IAccountsReceivableAccountProvisioner` creates or returns the one dedicated receivables detail account for a canonical Persons `PartyId`.
- Provisioning is lazy: it is called on first financial use only. Persons registration, role assignment, and contact changes must never create a financial account.
- The caller supplies its active PostgreSQL transaction. Finance never commits, rolls back, or disposes a caller-owned transaction (ADR-0005).
- A unique database constraint on `finance.party_accounts.party_id` and PostgreSQL `INSERT ... ON CONFLICT ... RETURNING` enforce one account under retries and concurrent first use.
- The Persons `IPartyIdentityLookup` contract validates the PartyId without coupling Finance to Persons internals.
- The account row is permanent. Closing an account does not free its PartyId or permit a second account.
- The host must provide the authenticated actor context; actor identifiers are not accepted from an untrusted client payload.
- Finance stores PartyId as a cross-module reference and intentionally does not add a foreign key into the Persons schema.

Person-merge financial handling remains a separate, explicit participant protocol in ADR-0015. This contract does not collapse accounts or rewrite historical financial documents.
