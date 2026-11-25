-- AC-37: durable, PII-free promotion result and idempotency tombstone.
-- This migration creates schema only. Applying it remains gated by the project migration process.
-- Sensitive workflow payload bytes remain in persons_temporary_workflow_payloads and are
-- deleted by the application only after the promotion transaction commits successfully.

-- The composite key ensures a tombstone can reference only the Party recorded as the
-- workflow's promoted target. Existing workflow checks enforce that a non-null target
-- is present only when lifecycle_status = 'promoted'.
ALTER TABLE persons_temporary_workflows
    ADD CONSTRAINT uq_persons_temp_workflow_promotion_target
    UNIQUE (workflow_record_id, promoted_party_id);

CREATE TABLE persons_temporary_workflow_promotions (
    operation_id uuid PRIMARY KEY,
    workflow_record_id uuid NOT NULL UNIQUE,
    actor_id varchar(128) NOT NULL CHECK (btrim(actor_id) <> ''),
    party_id uuid NOT NULL,
    completed_at_utc timestamptz NOT NULL,
    outcome varchar(16) NOT NULL CHECK (outcome IN ('Existing', 'Created')),
    contract_schema_version varchar(32) NOT NULL
        CHECK (btrim(contract_schema_version) <> ''),
    CONSTRAINT fk_persons_temp_promotion_workflow_target
        FOREIGN KEY (workflow_record_id, party_id)
        REFERENCES persons_temporary_workflows (workflow_record_id, promoted_party_id)
        ON DELETE RESTRICT,
    CONSTRAINT fk_persons_temp_promotion_party
        FOREIGN KEY (party_id)
        REFERENCES persons (party_id)
        ON DELETE RESTRICT
);

CREATE TABLE persons_temporary_workflow_access_audit (
    audit_id uuid PRIMARY KEY,
    workflow_record_id uuid NOT NULL
        REFERENCES persons_temporary_workflows (workflow_record_id) ON DELETE RESTRICT,
    operation_id uuid,
    actor_id varchar(128) NOT NULL CHECK (btrim(actor_id) <> ''),
    action varchar(32) NOT NULL
        CHECK (action IN ('preview', 'promotion_attempt', 'promotion_succeeded', 'promotion_rejected', 'replay')),
    outcome_code varchar(40) NOT NULL CHECK (btrim(outcome_code) <> ''),
    correlation_id uuid,
    occurred_at_utc timestamptz NOT NULL
);

CREATE INDEX ix_persons_workflow_access_audit_workflow
    ON persons_temporary_workflow_access_audit (workflow_record_id, occurred_at_utc);
CREATE INDEX ix_persons_workflow_access_audit_actor
    ON persons_temporary_workflow_access_audit (actor_id, occurred_at_utc);

CREATE RULE persons_workflow_access_audit_no_update AS
    ON UPDATE TO persons_temporary_workflow_access_audit DO INSTEAD NOTHING;
CREATE RULE persons_workflow_access_audit_no_delete AS
    ON DELETE TO persons_temporary_workflow_access_audit DO INSTEAD NOTHING;

REVOKE UPDATE, DELETE, TRUNCATE
    ON persons_temporary_workflow_access_audit FROM PUBLIC;
REVOKE ALL
    ON persons_temporary_workflow_access_audit FROM PUBLIC;

COMMENT ON TABLE persons_temporary_workflow_access_audit IS
    'Append-only, PII-free access and decision audit for temporary workflow preview and AC-37 promotion. Outcome codes only; never stores payload content.';

CREATE INDEX ix_persons_temp_promotions_actor_completed
    ON persons_temporary_workflow_promotions (actor_id, completed_at_utc);

-- Promotion evidence is append-only. A correction is a new controlled operation,
-- never an UPDATE or DELETE of the original result.
CREATE RULE persons_temp_promotions_no_update AS
    ON UPDATE TO persons_temporary_workflow_promotions DO INSTEAD NOTHING;
CREATE RULE persons_temp_promotions_no_delete AS
    ON DELETE TO persons_temporary_workflow_promotions DO INSTEAD NOTHING;

REVOKE UPDATE, DELETE, TRUNCATE
    ON persons_temporary_workflow_promotions FROM PUBLIC;

COMMENT ON TABLE persons_temporary_workflow_promotions IS
    'PII-free, append-only AC-37 promotion tombstones. Stores identifiers and outcome only; no workflow payload or personal attributes.';
COMMENT ON COLUMN persons_temporary_workflow_promotions.operation_id IS
    'Stable Promotion OperationId. Replays by the same Actor return this stored result; reuse by another Actor is rejected by the application.';
COMMENT ON COLUMN persons_temporary_workflow_promotions.outcome IS
    'Existing when an explicitly selected or duplicate-reviewed Party was attached; Created only when a new Party was committed.';
