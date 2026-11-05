-- Finance owns the dedicated receivables detail account for each canonical Party.
-- Deliberately no FK to Persons: PartyId is a cross-module identity reference (ADR-0004/0005/0015).
CREATE SCHEMA IF NOT EXISTS finance;

CREATE TABLE finance.party_accounts (
    account_id uuid PRIMARY KEY,
    party_id uuid NOT NULL,
    account_status smallint NOT NULL DEFAULT 1
        CHECK (account_status IN (1, 2, 3)),
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    created_by varchar(128) NOT NULL
        CHECK (btrim(created_by) <> ''),
    row_version bigint NOT NULL DEFAULT 1
        CHECK (row_version >= 1),
    CONSTRAINT ck_finance_party_accounts_nonzero_party
        CHECK (party_id <> '00000000-0000-0000-0000-000000000000'::uuid),
    CONSTRAINT uq_finance_party_accounts_party_id UNIQUE (party_id)
);

COMMENT ON TABLE finance.party_accounts IS
    'Finance-owned receivables detail accounts. Created lazily on first financial use; one account per PartyId.';
COMMENT ON COLUMN finance.party_accounts.party_id IS
    'Canonical Persons PartyId reference. No cross-module FK by design; the caller must supply a validated PartyId.';
COMMENT ON COLUMN finance.party_accounts.account_status IS
    '1=Active, 2=MergeTransferPending, 3=Closed. Closed accounts remain reserved and are never recreated.';

-- Account identity and historical references are permanent. Merge/transfer must use
-- the explicit Finance merge protocol; physical deletion is never a supported operation.
CREATE OR REPLACE FUNCTION finance.reject_party_account_delete()
RETURNS trigger
LANGUAGE plpgsql
AS $function$
BEGIN
    RAISE EXCEPTION 'Finance party accounts are permanent; use the controlled merge protocol.';
    RETURN OLD;
END;
$function$;

CREATE TRIGGER trg_finance_party_accounts_no_delete
BEFORE DELETE ON finance.party_accounts
FOR EACH ROW
EXECUTE FUNCTION finance.reject_party_account_delete();
