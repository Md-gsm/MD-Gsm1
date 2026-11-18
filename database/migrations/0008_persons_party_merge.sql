-- AC-39..AC-45: durable, PII-free merge identity and idempotency record.
ALTER TABLE persons
    ADD COLUMN merged_into_party_id uuid NULL
        REFERENCES persons(party_id) ON DELETE RESTRICT;

ALTER TABLE persons
    ADD CONSTRAINT ck_persons_merged_party_inactive
    CHECK (merged_into_party_id IS NULL OR (merged_into_party_id <> party_id AND is_active = false));

CREATE INDEX ix_persons_merged_into_party
    ON persons (merged_into_party_id)
    WHERE merged_into_party_id IS NOT NULL;

CREATE TABLE persons_party_merge_records (
    operation_id uuid PRIMARY KEY,
    source_party_id uuid NOT NULL UNIQUE REFERENCES persons(party_id) ON DELETE RESTRICT,
    target_party_id uuid NOT NULL REFERENCES persons(party_id) ON DELETE RESTRICT,
    actor_id varchar(128) NOT NULL CHECK (btrim(actor_id) <> ''),
    protocol_version varchar(32) NOT NULL CHECK (btrim(protocol_version) <> ''),
    completed_at_utc timestamptz NOT NULL,
    CONSTRAINT ck_persons_merge_distinct_parties CHECK (source_party_id <> target_party_id)
);
CREATE INDEX ix_persons_party_merge_target
    ON persons_party_merge_records (target_party_id, completed_at_utc);

-- Merge evidence is append-only. Corrections require a new audited operation, never mutation.
CREATE RULE persons_party_merge_records_no_update AS
    ON UPDATE TO persons_party_merge_records DO INSTEAD NOTHING;
CREATE RULE persons_party_merge_records_no_delete AS
    ON DELETE TO persons_party_merge_records DO INSTEAD NOTHING;

COMMENT ON COLUMN persons.merged_into_party_id IS
    'Canonical target PartyId after a controlled merge; source remains as an inactive historical identity.';
COMMENT ON TABLE persons_party_merge_records IS
    'PII-free append-only evidence for completed, idempotent Party merge operations.';
