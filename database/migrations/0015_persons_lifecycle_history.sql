-- Persons Party/ContactPoint/Address lifecycle history (Issue #98, WG-0 slot 0015).
-- Forward-only. Finance-owned columns remain in their current historical migration and are not moved here.

ALTER TABLE persons
    ADD COLUMN last_change_reason varchar(300) NOT NULL DEFAULT 'legacy-migration-baseline'
        CHECK (btrim(last_change_reason) <> '');

ALTER TABLE persons_contact_points
    ADD COLUMN origin_party_id uuid,
    ADD COLUMN row_version bigint NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    ADD COLUMN updated_at_utc timestamptz NOT NULL DEFAULT now(),
    ADD COLUMN updated_by varchar(128),
    ADD COLUMN last_change_reason varchar(300) NOT NULL DEFAULT 'legacy-migration-baseline'
        CHECK (btrim(last_change_reason) <> '');

UPDATE persons_contact_points
SET origin_party_id = party_id,
    updated_by = created_by,
    updated_at_utc = created_at_utc
WHERE origin_party_id IS NULL OR updated_by IS NULL;

ALTER TABLE persons_contact_points
    ALTER COLUMN origin_party_id SET NOT NULL,
    ALTER COLUMN updated_by SET NOT NULL,
    ADD CONSTRAINT fk_persons_contact_origin_party
        FOREIGN KEY (origin_party_id) REFERENCES persons(party_id) ON DELETE RESTRICT;

ALTER TABLE persons_addresses
    ADD COLUMN origin_party_id uuid,
    ADD COLUMN row_version bigint NOT NULL DEFAULT 1 CHECK (row_version >= 1),
    ADD COLUMN updated_at_utc timestamptz NOT NULL DEFAULT now(),
    ADD COLUMN updated_by varchar(128),
    ADD COLUMN last_change_reason varchar(300) NOT NULL DEFAULT 'legacy-migration-baseline'
        CHECK (btrim(last_change_reason) <> '');

UPDATE persons_addresses
SET origin_party_id = party_id,
    updated_by = created_by,
    updated_at_utc = created_at_utc
WHERE origin_party_id IS NULL OR updated_by IS NULL;

ALTER TABLE persons_addresses
    ALTER COLUMN origin_party_id SET NOT NULL,
    ALTER COLUMN updated_by SET NOT NULL,
    ADD CONSTRAINT fk_persons_address_origin_party
        FOREIGN KEY (origin_party_id) REFERENCES persons(party_id) ON DELETE RESTRICT;

CREATE INDEX ix_persons_contact_origin_party
    ON persons_contact_points (origin_party_id, contact_point_id);
CREATE INDEX ix_persons_address_origin_party
    ON persons_addresses (origin_party_id, address_id);

CREATE TABLE persons_lifecycle_history (
    history_id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    entity_type varchar(16) NOT NULL CHECK (entity_type IN ('party', 'contact_point', 'address')),
    entity_id uuid NOT NULL,
    party_id uuid NOT NULL,
    row_version bigint NOT NULL CHECK (row_version >= 1),
    change_type varchar(24) NOT NULL CHECK (change_type IN
        ('baseline', 'created', 'updated', 'deactivated', 'reactivated', 'merge-source', 'merge-transfer', 'merge-deduplicated')),
    actor_id varchar(128) NOT NULL CHECK (btrim(actor_id) <> ''),
    occurred_at_utc timestamptz NOT NULL,
    reason varchar(300) NOT NULL CHECK (btrim(reason) <> ''),
    before_snapshot jsonb,
    after_snapshot jsonb NOT NULL,
    operation_id uuid
);
CREATE INDEX ix_persons_lifecycle_history_entity
    ON persons_lifecycle_history (entity_type, entity_id, occurred_at_utc DESC, history_id);
CREATE INDEX ix_persons_lifecycle_history_party
    ON persons_lifecycle_history (party_id, occurred_at_utc DESC, history_id);
CREATE INDEX ix_persons_lifecycle_history_operation
    ON persons_lifecycle_history (operation_id, history_id)
    WHERE operation_id IS NOT NULL;

-- Capture a one-time version-1 baseline before history triggers are installed.
INSERT INTO persons_lifecycle_history
    (entity_type, entity_id, party_id, row_version, change_type, actor_id, occurred_at_utc,
     reason, before_snapshot, after_snapshot)
SELECT 'party', p.party_id, p.party_id, p.row_version, 'baseline', p.updated_by,
       p.updated_at_utc, 'migration-baseline', NULL,
       to_jsonb(p) - ARRAY['credit_limit_rial','default_discount_percent','default_payment_method','bank_card_last_four']::text[]
FROM persons p;

INSERT INTO persons_lifecycle_history
    (entity_type, entity_id, party_id, row_version, change_type, actor_id, occurred_at_utc,
     reason, before_snapshot, after_snapshot)
SELECT 'contact_point', c.contact_point_id, c.party_id, c.row_version, 'baseline', c.updated_by,
       c.updated_at_utc, 'migration-baseline', NULL, to_jsonb(c)
FROM persons_contact_points c;

INSERT INTO persons_lifecycle_history
    (entity_type, entity_id, party_id, row_version, change_type, actor_id, occurred_at_utc,
     reason, before_snapshot, after_snapshot)
SELECT 'address', a.address_id, a.party_id, a.row_version, 'baseline', a.updated_by,
       a.updated_at_utc, 'migration-baseline', NULL, to_jsonb(a)
FROM persons_addresses a;

CREATE FUNCTION persons_stamp_party_lifecycle()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, public
AS $$
BEGIN
    IF TG_OP = 'UPDATE' THEN
        NEW.last_change_reason := coalesce(
            nullif(current_setting('persons.lifecycle_reason', true), ''),
            'unclassified-update');
    END IF;
    RETURN NEW;
END;
$$;

CREATE FUNCTION persons_stamp_contact_address_lifecycle()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, public
AS $$
DECLARE
    v_actor text := nullif(current_setting('persons.lifecycle_actor', true), '');
    v_reason text := nullif(current_setting('persons.lifecycle_reason', true), '');
    v_occurred text := nullif(current_setting('persons.lifecycle_occurred_at_utc', true), '');
BEGIN
    IF TG_OP = 'INSERT' THEN
        NEW.origin_party_id := coalesce(NEW.origin_party_id, NEW.party_id);
        NEW.row_version := 1;
        NEW.updated_at_utc := coalesce(v_occurred::timestamptz, now());
        NEW.updated_by := coalesce(v_actor, NEW.created_by);
        NEW.last_change_reason := coalesce(v_reason, 'registration');
    ELSE
        NEW.origin_party_id := OLD.origin_party_id;
        NEW.row_version := OLD.row_version + 1;
        NEW.updated_at_utc := coalesce(v_occurred::timestamptz, now());
        NEW.updated_by := coalesce(v_actor, nullif(NEW.updated_by, ''), OLD.updated_by);
        NEW.last_change_reason := coalesce(v_reason, 'unclassified-update');
    END IF;
    RETURN NEW;
END;
$$;

CREATE FUNCTION persons_capture_lifecycle_history()
RETURNS trigger
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, public
AS $$
DECLARE
    v_entity_type text;
    v_entity_id uuid;
    v_party_id uuid;
    v_version bigint;
    v_actor text;
    v_reason text;
    v_occurred timestamptz;
    v_operation uuid;
    v_change text;
    v_before jsonb;
    v_after jsonb;
BEGIN
    v_operation := nullif(current_setting('persons.lifecycle_operation', true), '')::uuid;
    v_actor := coalesce(nullif(current_setting('persons.lifecycle_actor', true), ''),
                        CASE WHEN TG_OP = 'INSERT' THEN NEW.created_by ELSE NEW.updated_by END);
    v_reason := coalesce(nullif(current_setting('persons.lifecycle_reason', true), ''),
                         CASE WHEN TG_OP = 'INSERT' THEN 'registration' ELSE NEW.last_change_reason END,
                         'unclassified-update');
    v_occurred := coalesce(nullif(current_setting('persons.lifecycle_occurred_at_utc', true), '')::timestamptz,
                            CASE WHEN TG_OP = 'INSERT' THEN NEW.created_at_utc ELSE NEW.updated_at_utc END,
                            now());

    IF TG_TABLE_NAME = 'persons' THEN
        v_entity_type := 'party';
        v_entity_id := NEW.party_id;
        v_party_id := NEW.party_id;
        v_version := NEW.row_version;
        v_after := to_jsonb(NEW) - ARRAY['credit_limit_rial','default_discount_percent','default_payment_method','bank_card_last_four']::text[];
        IF TG_OP = 'UPDATE' THEN
            v_before := to_jsonb(OLD) - ARRAY['credit_limit_rial','default_discount_percent','default_payment_method','bank_card_last_four']::text[];
        END IF;
    ELSIF TG_TABLE_NAME = 'persons_contact_points' THEN
        v_entity_type := 'contact_point';
        v_entity_id := NEW.contact_point_id;
        v_party_id := NEW.party_id;
        v_version := NEW.row_version;
        v_after := to_jsonb(NEW);
        IF TG_OP = 'UPDATE' THEN v_before := to_jsonb(OLD); END IF;
    ELSE
        v_entity_type := 'address';
        v_entity_id := NEW.address_id;
        v_party_id := NEW.party_id;
        v_version := NEW.row_version;
        v_after := to_jsonb(NEW);
        IF TG_OP = 'UPDATE' THEN v_before := to_jsonb(OLD); END IF;
    END IF;

    IF TG_OP = 'INSERT' THEN
        v_change := 'created';
    ELSIF TG_TABLE_NAME = 'persons' THEN
        IF OLD.merged_into_party_id IS NULL AND NEW.merged_into_party_id IS NOT NULL THEN
            v_change := 'merge-source';
        ELSIF OLD.is_active AND NOT NEW.is_active THEN
            v_change := 'deactivated';
        ELSIF NOT OLD.is_active AND NEW.is_active THEN
            v_change := 'reactivated';
        ELSE
            v_change := 'updated';
        END IF;
    ELSE
        IF OLD.is_active AND NOT NEW.is_active AND v_reason LIKE 'merge-deduplicated-%' THEN
            v_change := 'merge-deduplicated';
        ELSIF OLD.party_id <> NEW.party_id THEN
            v_change := 'merge-transfer';
        ELSIF OLD.is_active AND NOT NEW.is_active THEN
            v_change := 'deactivated';
        ELSIF NOT OLD.is_active AND NEW.is_active THEN
            v_change := 'reactivated';
        ELSE
            v_change := 'updated';
        END IF;
    END IF;

    INSERT INTO public.persons_lifecycle_history
        (entity_type, entity_id, party_id, row_version, change_type, actor_id,
         occurred_at_utc, reason, before_snapshot, after_snapshot, operation_id)
    VALUES
        (v_entity_type, v_entity_id, v_party_id, v_version, v_change, v_actor,
         v_occurred, v_reason, v_before, v_after, v_operation);
    RETURN NEW;
END;
$$;

CREATE FUNCTION persons_prevent_lifecycle_delete()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, public
AS $$
BEGIN
    RAISE EXCEPTION 'Persons lifecycle rows are retained; deactivate or merge instead of deleting/truncating.'
        USING ERRCODE = '55000';
END;
$$;

CREATE TRIGGER persons_party_lifecycle_stamp
    BEFORE UPDATE ON persons
    FOR EACH ROW EXECUTE FUNCTION persons_stamp_party_lifecycle();
CREATE TRIGGER persons_contact_lifecycle_stamp
    BEFORE INSERT OR UPDATE ON persons_contact_points
    FOR EACH ROW EXECUTE FUNCTION persons_stamp_contact_address_lifecycle();
CREATE TRIGGER persons_address_lifecycle_stamp
    BEFORE INSERT OR UPDATE ON persons_addresses
    FOR EACH ROW EXECUTE FUNCTION persons_stamp_contact_address_lifecycle();

CREATE TRIGGER persons_party_lifecycle_history
    AFTER INSERT OR UPDATE ON persons
    FOR EACH ROW EXECUTE FUNCTION persons_capture_lifecycle_history();
CREATE TRIGGER persons_contact_lifecycle_history
    AFTER INSERT OR UPDATE ON persons_contact_points
    FOR EACH ROW EXECUTE FUNCTION persons_capture_lifecycle_history();
CREATE TRIGGER persons_address_lifecycle_history
    AFTER INSERT OR UPDATE ON persons_addresses
    FOR EACH ROW EXECUTE FUNCTION persons_capture_lifecycle_history();

CREATE TRIGGER persons_party_no_delete
    BEFORE DELETE ON persons
    FOR EACH STATEMENT EXECUTE FUNCTION persons_prevent_lifecycle_delete();
CREATE TRIGGER persons_party_no_truncate
    BEFORE TRUNCATE ON persons
    FOR EACH STATEMENT EXECUTE FUNCTION persons_prevent_lifecycle_delete();

CREATE TRIGGER persons_contact_no_delete
    BEFORE DELETE ON persons_contact_points
    FOR EACH STATEMENT EXECUTE FUNCTION persons_prevent_lifecycle_delete();
CREATE TRIGGER persons_contact_no_truncate
    BEFORE TRUNCATE ON persons_contact_points
    FOR EACH STATEMENT EXECUTE FUNCTION persons_prevent_lifecycle_delete();

CREATE TRIGGER persons_address_no_delete
    BEFORE DELETE ON persons_addresses
    FOR EACH STATEMENT EXECUTE FUNCTION persons_prevent_lifecycle_delete();
CREATE TRIGGER persons_address_no_truncate
    BEFORE TRUNCATE ON persons_addresses
    FOR EACH STATEMENT EXECUTE FUNCTION persons_prevent_lifecycle_delete();

CREATE TRIGGER persons_lifecycle_history_no_update
    BEFORE UPDATE ON persons_lifecycle_history
    FOR EACH STATEMENT EXECUTE FUNCTION persons_prevent_lifecycle_delete();
CREATE TRIGGER persons_lifecycle_history_no_delete
    BEFORE DELETE ON persons_lifecycle_history
    FOR EACH STATEMENT EXECUTE FUNCTION persons_prevent_lifecycle_delete();
CREATE TRIGGER persons_lifecycle_history_no_truncate
    BEFORE TRUNCATE ON persons_lifecycle_history
    FOR EACH STATEMENT EXECUTE FUNCTION persons_prevent_lifecycle_delete();

REVOKE ALL ON persons_lifecycle_history FROM PUBLIC;
REVOKE DELETE, TRUNCATE ON persons, persons_contact_points, persons_addresses FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION persons_capture_lifecycle_history() FROM PUBLIC;
REVOKE EXECUTE ON FUNCTION persons_prevent_lifecycle_delete() FROM PUBLIC;

COMMENT ON TABLE persons_lifecycle_history IS
    'Append-only versioned Party/ContactPoint/Address before-after history. Finance-owned Party fields are stripped from snapshots.';
COMMENT ON COLUMN persons_contact_points.origin_party_id IS
    'Original Party owner retained across controlled merge transfer; source provenance is not overwritten.';
COMMENT ON COLUMN persons_addresses.origin_party_id IS
    'Original Party owner retained across controlled merge transfer; source provenance is not overwritten.';
