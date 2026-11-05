-- AC-31..AC-38: temporary workflow staging stores ciphertext only.
-- No searchable PII columns, plaintext payload, or derived-data table is permitted here.
CREATE TABLE persons_temporary_workflows (
    workflow_record_id uuid PRIMARY KEY,
    owner_module varchar(80) NOT NULL CHECK (btrim(owner_module) <> ''),
    purpose_code varchar(80) NOT NULL CHECK (btrim(purpose_code) <> ''),
    created_by varchar(128) NOT NULL CHECK (btrim(created_by) <> ''),
    created_at_utc timestamptz NOT NULL,
    expires_at_utc timestamptz NOT NULL,
    absolute_expires_at_utc timestamptz NOT NULL,
    renewal_count smallint NOT NULL DEFAULT 0 CHECK (renewal_count BETWEEN 0 AND 1),
    renewal_reason varchar(500),
    renewed_by varchar(128),
    renewed_at_utc timestamptz,
    lifecycle_status varchar(16) NOT NULL DEFAULT 'open'
        CHECK (lifecycle_status IN ('open', 'closed', 'expired', 'purged', 'promoted')),
    idempotency_key uuid NOT NULL UNIQUE,
    promoted_party_id uuid,
    promoted_at_utc timestamptz,
    purged_at_utc timestamptz,
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    CONSTRAINT ck_persons_temp_expiry_order CHECK (
        created_at_utc < expires_at_utc
        AND expires_at_utc <= absolute_expires_at_utc
        AND absolute_expires_at_utc <= created_at_utc + interval '30 days'
    ),
    CONSTRAINT ck_persons_temp_renewal_metadata CHECK (
        (renewal_count = 0 AND renewal_reason IS NULL AND renewed_by IS NULL AND renewed_at_utc IS NULL)
        OR
        (renewal_count = 1 AND renewal_reason IS NOT NULL AND btrim(renewal_reason) <> ''
            AND renewed_by IS NOT NULL AND renewed_at_utc IS NOT NULL)
    ),
    CONSTRAINT ck_persons_temp_promotion_metadata CHECK (
        (lifecycle_status = 'promoted' AND promoted_party_id IS NOT NULL AND promoted_at_utc IS NOT NULL)
        OR
        (lifecycle_status <> 'promoted' AND promoted_party_id IS NULL AND promoted_at_utc IS NULL)
    ),
    CONSTRAINT ck_persons_temp_payload_lifecycle CHECK (
        (lifecycle_status IN ('open', 'closed') AND purged_at_utc IS NULL)
        OR
        (lifecycle_status IN ('expired', 'purged') AND purged_at_utc IS NOT NULL)
        OR
        (lifecycle_status = 'promoted' AND promoted_party_id IS NOT NULL AND promoted_at_utc IS NOT NULL)
    )
);
CREATE TABLE persons_temporary_workflow_payloads (
    workflow_record_id uuid PRIMARY KEY
        REFERENCES persons_temporary_workflows(workflow_record_id) ON DELETE CASCADE,
    encrypted_payload bytea NOT NULL CHECK (octet_length(encrypted_payload) >= 28),
    encryption_key_version varchar(80) NOT NULL CHECK (btrim(encryption_key_version) <> ''),
    created_at_utc timestamptz NOT NULL DEFAULT now()
);
CREATE INDEX ix_persons_temp_expiry_open
    ON persons_temporary_workflows (expires_at_utc, workflow_record_id)
    WHERE lifecycle_status = 'open' AND encrypted_payload IS NOT NULL;
CREATE INDEX ix_persons_temp_owner_status
    ON persons_temporary_workflows (owner_module, lifecycle_status);

COMMENT ON TABLE persons_temporary_workflows IS
    'Temporary workflow metadata and ciphertext only. Never store plaintext PII, searchable PII, or derived PII here.';
COMMENT ON COLUMN persons_temporary_workflows.encrypted_payload IS
    'Authenticated ciphertext only; encryption/decryption and key management belong to the application boundary.';

COMMENT ON TABLE persons_temporary_workflow_payloads IS
    'Ciphertext-only payload storage separated from workflow metadata so expired payload bytes can be purged independently.';
COMMENT ON COLUMN persons_temporary_workflow_payloads.encrypted_payload IS
    'Authenticated ciphertext only; plaintext PII is prohibited.';
