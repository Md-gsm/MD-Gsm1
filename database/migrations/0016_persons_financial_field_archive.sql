-- Persons legacy financial-field archive (Issue #98, WG-0 reservation 0016).
-- Forward-only, transactional, archive-only preservation bridge. Never apply to production from CI.
-- This schema is not an operational Finance/Pricing/Treasury data store and receives no runtime grants.

CREATE SCHEMA mdgsm_migration_archive;
REVOKE ALL ON SCHEMA mdgsm_migration_archive FROM PUBLIC;

CREATE TABLE mdgsm_migration_archive.persons_financial_field_archive (
    party_id uuid PRIMARY KEY,
    credit_limit_rial bigint NOT NULL,
    default_discount_percent numeric(5,2) NOT NULL,
    default_payment_method smallint NOT NULL,
    bank_card_last_four char(4),
    source_created_at_utc timestamptz NOT NULL,
    source_created_by varchar(128) NOT NULL,
    source_updated_at_utc timestamptz NOT NULL,
    source_updated_by varchar(128) NOT NULL,
    source_row_version bigint NOT NULL CHECK (source_row_version >= 1),
    credit_limit_disposition varchar(40) NOT NULL
        DEFAULT 'PARTY_ONLY_SCOPE_REJECTED'
        CHECK (credit_limit_disposition = 'PARTY_ONLY_SCOPE_REJECTED'),
    default_discount_disposition varchar(32) NOT NULL
        DEFAULT 'PRICING_RULE_UNMAPPED'
        CHECK (default_discount_disposition = 'PRICING_RULE_UNMAPPED'),
    default_payment_method_disposition varchar(32) NOT NULL
        DEFAULT 'TREASURY_ENUM_UNMAPPED'
        CHECK (default_payment_method_disposition = 'TREASURY_ENUM_UNMAPPED'),
    bank_card_last_four_disposition varchar(40) NOT NULL
        DEFAULT 'TREASURY_LAST4_REFERENCE_ONLY'
        CHECK (bank_card_last_four_disposition = 'TREASURY_LAST4_REFERENCE_ONLY'),
    archive_migration_version smallint NOT NULL DEFAULT 16
        CHECK (archive_migration_version = 16),
    archived_at_utc timestamptz NOT NULL DEFAULT transaction_timestamp(),
    CONSTRAINT fk_persons_financial_archive_party
        FOREIGN KEY (party_id) REFERENCES persons(party_id) ON DELETE RESTRICT
);

COMMENT ON SCHEMA mdgsm_migration_archive IS
    'Private, non-operational preservation archive for forward-only schema migrations; not a runtime domain schema.';
COMMENT ON TABLE mdgsm_migration_archive.persons_financial_field_archive IS
    'Immutable preservation bridge for four retired Persons columns. Values are not authorized for operational credit, pricing or payment decisions.';
COMMENT ON COLUMN mdgsm_migration_archive.persons_financial_field_archive.credit_limit_disposition IS
    'Party-only legacy value is rejected as an effective credit limit; canonical scope is PartyId x Product/Item.';
COMMENT ON COLUMN mdgsm_migration_archive.persons_financial_field_archive.default_discount_disposition IS
    'Legacy percentage has no versioned Pricing rule/scope/validity and must not be applied.';
COMMENT ON COLUMN mdgsm_migration_archive.persons_financial_field_archive.default_payment_method_disposition IS
    'Legacy smallint mapping to canonical Treasury methods is unapproved and intentionally unresolved.';
COMMENT ON COLUMN mdgsm_migration_archive.persons_financial_field_archive.bank_card_last_four_disposition IS
    'Last-four metadata is reference-only and is not a payment instrument, token or credential.';

-- Copy every Party, including explicit default-valued rows. Do not infer a runtime mapping.
INSERT INTO mdgsm_migration_archive.persons_financial_field_archive
    (party_id, credit_limit_rial, default_discount_percent, default_payment_method,
     bank_card_last_four, source_created_at_utc, source_created_by,
     source_updated_at_utc, source_updated_by, source_row_version,
     credit_limit_disposition, default_discount_disposition,
     default_payment_method_disposition, bank_card_last_four_disposition,
     archive_migration_version)
SELECT
    p.party_id, p.credit_limit_rial, p.default_discount_percent, p.default_payment_method,
    p.bank_card_last_four, p.created_at_utc, p.created_by,
    p.updated_at_utc, p.updated_by, p.row_version,
    'PARTY_ONLY_SCOPE_REJECTED', 'PRICING_RULE_UNMAPPED',
    'TREASURY_ENUM_UNMAPPED', 'TREASURY_LAST4_REFERENCE_ONLY', 16
FROM persons AS p;

-- Fail the complete migration transaction unless every Party and every source value/provenance
-- has an exact archive counterpart. IS DISTINCT FROM makes NULL comparison explicit.
DO $archive_parity$
DECLARE
    source_count bigint;
    archive_count bigint;
    mismatch_count bigint;
BEGIN
    SELECT count(*) INTO source_count FROM persons;
    SELECT count(*) INTO archive_count
    FROM mdgsm_migration_archive.persons_financial_field_archive;

    IF source_count <> archive_count THEN
        RAISE EXCEPTION
            'Persons financial archive row-count mismatch: source %, archive %',
            source_count, archive_count;
    END IF;

    SELECT count(*) INTO mismatch_count
    FROM persons AS p
    FULL OUTER JOIN mdgsm_migration_archive.persons_financial_field_archive AS a
        ON a.party_id = p.party_id
    WHERE p.party_id IS NULL
       OR a.party_id IS NULL
       OR p.credit_limit_rial IS DISTINCT FROM a.credit_limit_rial
       OR p.default_discount_percent IS DISTINCT FROM a.default_discount_percent
       OR p.default_payment_method IS DISTINCT FROM a.default_payment_method
       OR p.bank_card_last_four IS DISTINCT FROM a.bank_card_last_four
       OR p.created_at_utc IS DISTINCT FROM a.source_created_at_utc
       OR p.created_by IS DISTINCT FROM a.source_created_by
       OR p.updated_at_utc IS DISTINCT FROM a.source_updated_at_utc
       OR p.updated_by IS DISTINCT FROM a.source_updated_by
       OR p.row_version IS DISTINCT FROM a.source_row_version
       OR a.credit_limit_disposition <> 'PARTY_ONLY_SCOPE_REJECTED'
       OR a.default_discount_disposition <> 'PRICING_RULE_UNMAPPED'
       OR a.default_payment_method_disposition <> 'TREASURY_ENUM_UNMAPPED'
       OR a.bank_card_last_four_disposition <> 'TREASURY_LAST4_REFERENCE_ONLY'
       OR a.archive_migration_version <> 16;

    IF mismatch_count <> 0 THEN
        RAISE EXCEPTION
            'Persons financial archive parity check failed for % row(s)',
            mismatch_count;
    END IF;
END;
$archive_parity$;

-- The archive accepts future reviewed append-only imports, but ordinary mutation is forbidden.
CREATE FUNCTION mdgsm_migration_archive.reject_persons_financial_archive_mutation()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog
AS $archive_guard$
BEGIN
    RAISE EXCEPTION
        'Persons financial migration archive is append-only; UPDATE, DELETE and TRUNCATE are forbidden.'
        USING ERRCODE = '55000';
END;
$archive_guard$;

CREATE TRIGGER persons_financial_archive_no_update
    BEFORE UPDATE ON mdgsm_migration_archive.persons_financial_field_archive
    FOR EACH STATEMENT
    EXECUTE FUNCTION mdgsm_migration_archive.reject_persons_financial_archive_mutation();

CREATE TRIGGER persons_financial_archive_no_delete
    BEFORE DELETE ON mdgsm_migration_archive.persons_financial_field_archive
    FOR EACH STATEMENT
    EXECUTE FUNCTION mdgsm_migration_archive.reject_persons_financial_archive_mutation();

CREATE TRIGGER persons_financial_archive_no_truncate
    BEFORE TRUNCATE ON mdgsm_migration_archive.persons_financial_field_archive
    FOR EACH STATEMENT
    EXECUTE FUNCTION mdgsm_migration_archive.reject_persons_financial_archive_mutation();

-- No PUBLIC or application-runtime visibility. Ownership remains with the migration schema owner.
REVOKE ALL ON TABLE mdgsm_migration_archive.persons_financial_field_archive FROM PUBLIC;
REVOKE ALL ON FUNCTION mdgsm_migration_archive.reject_persons_financial_archive_mutation() FROM PUBLIC;

-- Only after exact parity has been proven may the legacy Persons fields be removed.
ALTER TABLE persons
    DROP CONSTRAINT ck_persons_card_last_four_format;

ALTER TABLE persons
    DROP COLUMN credit_limit_rial,
    DROP COLUMN default_discount_percent,
    DROP COLUMN default_payment_method,
    DROP COLUMN bank_card_last_four;
