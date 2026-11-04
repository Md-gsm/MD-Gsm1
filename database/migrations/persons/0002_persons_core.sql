-- Persons core schema. Apply only through the guarded, explicit MD-Gsm migration runner.
-- Monetary values are integer Rial. Raw card PAN and authentication secrets are never stored.
CREATE TABLE persons (
    party_id uuid PRIMARY KEY,
    party_type smallint NOT NULL CHECK (party_type IN (1, 2)),
    business_roles smallint NOT NULL CHECK (business_roles BETWEEN 1 AND 3),
    given_name varchar(120),
    family_name varchar(160),
    legal_name varchar(300),
    national_id varchar(10),
    legal_national_id varchar(11),
    economic_code varchar(14),
    responsible_person_name varchar(200),
    acquaintance_source smallint NOT NULL DEFAULT 0 CHECK (acquaintance_source BETWEEN 0 AND 6),
    internal_note varchar(2000) NOT NULL DEFAULT '',
    credit_limit_rial bigint NOT NULL DEFAULT 0 CHECK (credit_limit_rial >= 0),
    default_discount_percent numeric(5,2) NOT NULL DEFAULT 0 CHECK (default_discount_percent BETWEEN 0 AND 100),
    default_payment_method smallint NOT NULL DEFAULT 1 CHECK (default_payment_method BETWEEN 1 AND 5),
    bank_card_last_four char(4),
    repair_status_sms_enabled boolean NOT NULL DEFAULT true,
    ready_for_delivery_sms_enabled boolean NOT NULL DEFAULT true,
    promotions_sms_enabled boolean NOT NULL DEFAULT false,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    created_by varchar(128) NOT NULL,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_by varchar(128) NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    work_classification smallint CHECK (work_classification BETWEEN 1 AND 3),
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    CONSTRAINT ck_persons_national_id_format CHECK (national_id IS NULL OR national_id ~ '^[0-9]{10}$'),
    CONSTRAINT ck_persons_legal_national_id_format CHECK (legal_national_id IS NULL OR legal_national_id ~ '^[0-9]{11}$'),
    CONSTRAINT ck_persons_economic_code_format CHECK (economic_code IS NULL OR economic_code ~ '^[0-9]{10,14}$'),
    CONSTRAINT ck_persons_card_last_four_format CHECK (bank_card_last_four IS NULL OR bank_card_last_four ~ '^[0-9]{4}$'),
    CONSTRAINT ck_persons_work_classification_scope CHECK (work_classification IS NULL OR (party_type = 1 AND (business_roles & 2) = 2)),
    CONSTRAINT ck_persons_party_identity CHECK (
      (party_type = 1 AND given_name IS NOT NULL AND family_name IS NOT NULL AND legal_name IS NULL AND legal_national_id IS NULL AND economic_code IS NULL)
      OR
      (party_type = 2 AND legal_name IS NOT NULL AND given_name IS NULL AND family_name IS NULL)
    )
);
CREATE UNIQUE INDEX ux_persons_natural_national_id ON persons(national_id)
 WHERE party_type = 1 AND national_id IS NOT NULL AND is_active;
CREATE UNIQUE INDEX ux_persons_legal_national_id ON persons(legal_national_id)
 WHERE party_type = 2 AND legal_national_id IS NOT NULL AND is_active;

CREATE TABLE persons_contact_points (
    contact_point_id uuid PRIMARY KEY,
    party_id uuid NOT NULL REFERENCES persons(party_id) ON DELETE RESTRICT,
    kind smallint NOT NULL CHECK (kind IN (1,2,3)),
    value varchar(254) NOT NULL,
    is_primary boolean NOT NULL DEFAULT false,
    is_messaging_allowed boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    created_by varchar(128) NOT NULL,
    CONSTRAINT ck_persons_mobile_e164 CHECK (kind <> 1 OR value ~ '^\+[1-9][0-9]{7,14}$')
);
CREATE UNIQUE INDEX ux_persons_primary_contact_kind ON persons_contact_points(party_id, kind)
 WHERE is_primary AND is_active;
-- A mobile number is unique within one party among active contacts; the same number on different parties is a soft duplicate.
CREATE UNIQUE INDEX ux_persons_mobile_per_party ON persons_contact_points(party_id, value)
 WHERE kind = 1 AND is_active;

CREATE TABLE persons_addresses (
    address_id uuid PRIMARY KEY,
    party_id uuid NOT NULL REFERENCES persons(party_id) ON DELETE RESTRICT,
    country_code char(2) NOT NULL DEFAULT 'IR',
    province varchar(100) NOT NULL,
    city varchar(100) NOT NULL,
    postal_code varchar(20) NOT NULL,
    address_line varchar(1000) NOT NULL,
    is_primary boolean NOT NULL DEFAULT false,
    is_active boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    created_by varchar(128) NOT NULL
);
CREATE UNIQUE INDEX ux_persons_primary_address ON persons_addresses(party_id) WHERE is_primary AND is_active;

CREATE INDEX ix_persons_name ON persons (family_name, given_name) WHERE party_type = 1 AND is_active;
CREATE INDEX ix_persons_legal_name ON persons (legal_name) WHERE party_type = 2 AND is_active;
CREATE INDEX ix_persons_roles ON persons (business_roles) WHERE is_active;
-- Suffix-4 search on active mobile numbers (owner-approved search policy). No general substring index on contact values.
CREATE INDEX ix_persons_mobile_suffix4 ON persons_contact_points (right(value, 4)) WHERE kind = 1 AND is_active;
CREATE INDEX ix_persons_addresses_city ON persons_addresses (city);

COMMENT ON TABLE persons IS 'Persons module master identities. Financial balances and accounting ledger entries are not stored here.';
COMMENT ON COLUMN persons.bank_card_last_four IS 'Only last four digits; full card PAN is prohibited.';

-- Idempotency for registration (V1: OperationId only; the stored party is returned on replay).
CREATE TABLE persons_operations (
    operation_id uuid PRIMARY KEY,
    party_id uuid NOT NULL REFERENCES persons(party_id) ON DELETE RESTRICT,
    actor varchar(128) NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

-- Append-only record of who read which party. Updates and deletes are silently discarded by rules.
CREATE TABLE persons_access_audit (
    audit_id uuid PRIMARY KEY,
    party_id uuid NOT NULL REFERENCES persons(party_id) ON DELETE RESTRICT,
    actor varchar(128) NOT NULL,
    action varchar(32) NOT NULL,
    sensitive_included boolean NOT NULL,
    occurred_at_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_persons_access_audit_party ON persons_access_audit (party_id, occurred_at_utc);
CREATE INDEX ix_persons_access_audit_actor ON persons_access_audit (actor, occurred_at_utc);

CREATE RULE persons_access_audit_no_update AS ON UPDATE TO persons_access_audit DO INSTEAD NOTHING;
CREATE RULE persons_access_audit_no_delete AS ON DELETE TO persons_access_audit DO INSTEAD NOTHING;
