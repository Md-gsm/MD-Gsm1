-- Persons source Outbox for the owner-approved first event: PartyRegistered only.
-- The envelope is PII-free by construction: no names, national identifiers, mobile numbers,
-- email addresses, addresses, plaintext payload or arbitrary JSON body are stored.
-- The event row and its initial delivery row must be inserted in the same business transaction
-- as creation of a new Party. Existing-match results and replays emit no event.

CREATE TABLE persons_outbox_events (
    event_id uuid PRIMARY KEY,
    event_type varchar(80) NOT NULL CHECK (event_type = 'PartyRegistered'),
    contract_version varchar(32) NOT NULL CHECK (btrim(contract_version) <> ''),
    aggregate_id uuid NOT NULL
        REFERENCES persons (party_id) ON DELETE RESTRICT,
    actor_reference varchar(128) NOT NULL CHECK (btrim(actor_reference) <> ''),
    occurred_at_utc timestamptz NOT NULL,
    correlation_id uuid,
    causation_id uuid,
    idempotency_key uuid NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_persons_outbox_event_aggregate
        UNIQUE (event_type, aggregate_id),
    CONSTRAINT uq_persons_outbox_event_idempotency
        UNIQUE (event_type, idempotency_key)
);

CREATE TABLE persons_outbox_deliveries (
    event_id uuid PRIMARY KEY
        REFERENCES persons_outbox_events (event_id) ON DELETE RESTRICT,
    delivery_status varchar(16) NOT NULL DEFAULT 'pending'
        CHECK (delivery_status IN ('pending', 'leased', 'delivered', 'quarantined')),
    attempt_count integer NOT NULL DEFAULT 0 CHECK (attempt_count >= 0),
    next_attempt_at_utc timestamptz NOT NULL DEFAULT now(),
    lease_owner varchar(128),
    lease_expires_at_utc timestamptz,
    last_attempt_at_utc timestamptz,
    delivered_at_utc timestamptz,
    last_error_code varchar(80),
    row_version bigint NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    CONSTRAINT ck_persons_outbox_delivery_lease CHECK (
        (delivery_status = 'leased'
            AND lease_owner IS NOT NULL AND btrim(lease_owner) <> ''
            AND lease_expires_at_utc IS NOT NULL)
        OR
        (delivery_status <> 'leased'
            AND lease_owner IS NULL AND lease_expires_at_utc IS NULL)
    ),
    CONSTRAINT ck_persons_outbox_delivery_completion CHECK (
        (delivery_status = 'delivered' AND delivered_at_utc IS NOT NULL)
        OR
        (delivery_status <> 'delivered' AND delivered_at_utc IS NULL)
    )
);

-- A committed event must always have its initial delivery record. The deferred
-- constraint permits both inserts in either order inside one transaction, but rejects
-- an event-only commit and therefore prevents lost initial delivery intent.
CREATE FUNCTION persons_outbox_require_initial_delivery()
RETURNS trigger
LANGUAGE plpgsql
AS $
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM persons_outbox_deliveries
        WHERE event_id = NEW.event_id
    ) THEN
        RAISE EXCEPTION 'Persons Outbox event % has no initial delivery record', NEW.event_id
            USING ERRCODE = '23514';
    END IF;
    RETURN NULL;
END;
$;

CREATE CONSTRAINT TRIGGER trg_persons_outbox_initial_delivery_required
AFTER INSERT ON persons_outbox_events
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW
EXECUTE FUNCTION persons_outbox_require_initial_delivery();

CREATE INDEX ix_persons_outbox_delivery_due
    ON persons_outbox_deliveries (next_attempt_at_utc, event_id)
    WHERE delivery_status = 'pending';
CREATE INDEX ix_persons_outbox_delivery_lease_expiry
    ON persons_outbox_deliveries (lease_expires_at_utc, event_id)
    WHERE delivery_status = 'leased';

-- Event envelopes are immutable. Delivery state is intentionally mutable for retry,
-- lease renewal, acknowledgement and reconciliation.
CREATE RULE persons_outbox_events_no_update AS
    ON UPDATE TO persons_outbox_events DO INSTEAD NOTHING;
CREATE RULE persons_outbox_events_no_delete AS
    ON DELETE TO persons_outbox_events DO INSTEAD NOTHING;

REVOKE UPDATE, DELETE, TRUNCATE
    ON persons_outbox_events FROM PUBLIC;
REVOKE ALL
    ON persons_outbox_events, persons_outbox_deliveries FROM PUBLIC;

COMMENT ON TABLE persons_outbox_events IS
    'PII-free Persons source Outbox. AC-37 emits PartyRegistered only for a newly created Party; PartyMerged is excluded.';
COMMENT ON TABLE persons_outbox_deliveries IS
    'One durable delivery state per EventId. Retries retain the original EventId and use at-least-once delivery semantics.';
COMMENT ON COLUMN persons_outbox_events.idempotency_key IS
    'Stable source operation key; for AC-37 this is the OperationId that created the Party.';
