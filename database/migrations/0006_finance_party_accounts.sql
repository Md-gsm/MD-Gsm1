-- Finance-owned Party subledger account identity. PartyId is a stable cross-module reference.
-- No cross-module FK is created. The unique constraint is the concurrency guarantee.
-- This table stores no balance or ledger entries; balances are derived from Finance postings.
CREATE TABLE finance_party_accounts (
    account_id uuid PRIMARY KEY,
    party_id uuid NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    created_by varchar(128) NOT NULL,
        CONSTRAINT uq_finance_party_accounts_party UNIQUE (party_id),
    CONSTRAINT ck_finance_party_accounts_created_by CHECK (btrim(created_by) <> '')
);
COMMENT ON TABLE finance_party_accounts IS
    'Finance-owned one-to-one Party subledger account identity; not a balance or general-ledger account.';
COMMENT ON COLUMN finance_party_accounts.party_id IS
    'Stable PartyId reference owned by Persons; intentionally no cross-module foreign key.';
