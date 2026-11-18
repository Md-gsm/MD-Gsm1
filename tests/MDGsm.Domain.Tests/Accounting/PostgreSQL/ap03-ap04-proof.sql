\set ON_ERROR_STOP on
CREATE SCHEMA ap_proof;
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mdgsm_ap_proof_runtime') THEN
        EXECUTE 'CREATE ROLE mdgsm_ap_proof_runtime NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT';
    END IF;
END $$;

CREATE TABLE ap_proof.journals (
    journal_id uuid PRIMARY KEY,
    status text NOT NULL CHECK (status IN ('draft', 'finalized')),
    source_reference text NOT NULL
);

CREATE TABLE ap_proof.journal_lines (
    line_id uuid PRIMARY KEY,
    journal_id uuid NOT NULL REFERENCES ap_proof.journals(journal_id) ON DELETE RESTRICT,
    debit_irr bigint NOT NULL DEFAULT 0,
    credit_irr bigint NOT NULL DEFAULT 0,
    CHECK (
        (debit_irr > 0 AND credit_irr = 0) OR
        (credit_irr > 0 AND debit_irr = 0)
    )
);

CREATE FUNCTION ap_proof.assert_journal_balanced(p_journal_id uuid)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    v_count bigint;
    v_debits numeric;
    v_credits numeric;
BEGIN
    SELECT count(*), COALESCE(sum(debit_irr), 0), COALESCE(sum(credit_irr), 0)
      INTO v_count, v_debits, v_credits
      FROM ap_proof.journal_lines
     WHERE journal_id = p_journal_id;

    IF v_count < 2 THEN
        RAISE EXCEPTION 'Journal % must contain at least two lines', p_journal_id
            USING ERRCODE = '23514';
    END IF;
    IF v_debits <> v_credits THEN
        RAISE EXCEPTION 'Journal % is not balanced: debit %, credit %',
            p_journal_id, v_debits, v_credits
            USING ERRCODE = '23514';
    END IF;
    IF v_debits > 9223372036854775807 OR v_credits > 9223372036854775807 THEN
        RAISE EXCEPTION 'Journal % total exceeds bigint IRR range', p_journal_id
            USING ERRCODE = '22003';
    END IF;
END;
$$;

CREATE FUNCTION ap_proof.check_journal_balance_deferred()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF TG_OP = 'DELETE' THEN
        PERFORM ap_proof.assert_journal_balanced(OLD.journal_id);
    ELSE
        PERFORM ap_proof.assert_journal_balanced(NEW.journal_id);
        IF TG_OP = 'UPDATE' AND OLD.journal_id IS DISTINCT FROM NEW.journal_id THEN
            PERFORM ap_proof.assert_journal_balanced(OLD.journal_id);
        END IF;
    END IF;
    RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER trg_ap_proof_journal_balance
AFTER INSERT OR UPDATE OR DELETE ON ap_proof.journal_lines
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION ap_proof.check_journal_balance_deferred();

CREATE FUNCTION ap_proof.check_journal_header_balance_deferred()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    PERFORM ap_proof.assert_journal_balanced(NEW.journal_id);
    RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER trg_ap_proof_journal_header_balance
AFTER INSERT OR UPDATE ON ap_proof.journals
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW EXECUTE FUNCTION ap_proof.check_journal_header_balance_deferred();

CREATE FUNCTION ap_proof.guard_journal_header()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.status = 'finalized' THEN
        RAISE EXCEPTION 'Finalized journal headers are immutable'
            USING ERRCODE = '55000';
    END IF;
    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_ap_proof_journal_header_immutable
BEFORE UPDATE OR DELETE ON ap_proof.journals
FOR EACH ROW EXECUTE FUNCTION ap_proof.guard_journal_header();

CREATE FUNCTION ap_proof.guard_journal_lines()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    v_status text;
BEGIN
    IF TG_OP <> 'INSERT' THEN
        SELECT status INTO v_status
          FROM ap_proof.journals WHERE journal_id = OLD.journal_id;
        IF v_status = 'finalized' THEN
            RAISE EXCEPTION 'Lines of finalized journals are immutable'
                USING ERRCODE = '55000';
        END IF;
    END IF;
    IF TG_OP <> 'DELETE' THEN
        SELECT status INTO v_status
          FROM ap_proof.journals WHERE journal_id = NEW.journal_id;
        IF v_status = 'finalized' THEN
            RAISE EXCEPTION 'Cannot add or move lines into a finalized journal'
                USING ERRCODE = '55000';
        END IF;
    END IF;
    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END;
$$;

CREATE TRIGGER trg_ap_proof_journal_lines_immutable
BEFORE INSERT OR UPDATE OR DELETE ON ap_proof.journal_lines
FOR EACH ROW EXECUTE FUNCTION ap_proof.guard_journal_lines();

CREATE FUNCTION ap_proof.guard_finalized_journal_truncate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM ap_proof.journals WHERE status = 'finalized') THEN
        RAISE EXCEPTION 'Cannot truncate journals containing finalized documents'
            USING ERRCODE = '55000';
    END IF;
    RETURN NULL;
END;
$$;
CREATE TRIGGER trg_ap_proof_journal_truncate
BEFORE TRUNCATE ON ap_proof.journals
FOR EACH STATEMENT EXECUTE FUNCTION ap_proof.guard_finalized_journal_truncate();

CREATE FUNCTION ap_proof.guard_finalized_lines_truncate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM ap_proof.journal_lines l
        JOIN ap_proof.journals j USING (journal_id)
        WHERE j.status = 'finalized'
    ) THEN
        RAISE EXCEPTION 'Cannot truncate lines belonging to finalized documents'
            USING ERRCODE = '55000';
    END IF;
    RETURN NULL;
END;
$$;
CREATE TRIGGER trg_ap_proof_lines_truncate
BEFORE TRUNCATE ON ap_proof.journal_lines
FOR EACH STATEMENT EXECUTE FUNCTION ap_proof.guard_finalized_lines_truncate();

CREATE TABLE ap_proof.audit_events (
    event_id text PRIMARY KEY,
    event_type text NOT NULL
);
CREATE TABLE ap_proof.period_transition_events (
    transition_id uuid PRIMARY KEY,
    transition_type text NOT NULL
);
CREATE TABLE ap_proof.coa_snapshots (
    snapshot_id uuid PRIMARY KEY,
    status text NOT NULL CHECK (status IN ('draft', 'accepted', 'retired'))
);

CREATE FUNCTION ap_proof.reject_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    RAISE EXCEPTION '% is append-only; % is forbidden', TG_TABLE_NAME, TG_OP
        USING ERRCODE = '55000';
END;
$$;

CREATE TRIGGER trg_ap_proof_audit_no_update_delete
BEFORE UPDATE OR DELETE ON ap_proof.audit_events
FOR EACH ROW EXECUTE FUNCTION ap_proof.reject_mutation();
CREATE TRIGGER trg_ap_proof_audit_no_truncate
BEFORE TRUNCATE ON ap_proof.audit_events
FOR EACH STATEMENT EXECUTE FUNCTION ap_proof.reject_mutation();

CREATE TRIGGER trg_ap_proof_transition_no_update_delete
BEFORE UPDATE OR DELETE ON ap_proof.period_transition_events
FOR EACH ROW EXECUTE FUNCTION ap_proof.reject_mutation();
CREATE TRIGGER trg_ap_proof_transition_no_truncate
BEFORE TRUNCATE ON ap_proof.period_transition_events
FOR EACH STATEMENT EXECUTE FUNCTION ap_proof.reject_mutation();

CREATE FUNCTION ap_proof.guard_accepted_snapshot()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF OLD.status = 'accepted' THEN
        RAISE EXCEPTION 'Accepted COA snapshots are immutable'
            USING ERRCODE = '55000';
    END IF;
    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
    RETURN NEW;
END;
$$;
CREATE TRIGGER trg_ap_proof_accepted_snapshot_immutable
BEFORE UPDATE OR DELETE ON ap_proof.coa_snapshots
FOR EACH ROW EXECUTE FUNCTION ap_proof.guard_accepted_snapshot();

CREATE FUNCTION ap_proof.guard_accepted_snapshot_truncate()
RETURNS trigger
LANGUAGE plpgsql
AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM ap_proof.coa_snapshots WHERE status = 'accepted') THEN
        RAISE EXCEPTION 'Cannot truncate accepted COA snapshots'
            USING ERRCODE = '55000';
    END IF;
    RETURN NULL;
END;
$$;
CREATE TRIGGER trg_ap_proof_snapshot_truncate
BEFORE TRUNCATE ON ap_proof.coa_snapshots
FOR EACH STATEMENT EXECUTE FUNCTION ap_proof.guard_accepted_snapshot_truncate();

GRANT USAGE ON SCHEMA ap_proof TO mdgsm_ap_proof_runtime;
GRANT SELECT, INSERT ON ap_proof.journals TO mdgsm_ap_proof_runtime;
GRANT UPDATE (status) ON ap_proof.journals TO mdgsm_ap_proof_runtime;
GRANT SELECT, INSERT ON ap_proof.journal_lines TO mdgsm_ap_proof_runtime;
GRANT SELECT, INSERT ON ap_proof.audit_events TO mdgsm_ap_proof_runtime;
GRANT SELECT, INSERT ON ap_proof.period_transition_events TO mdgsm_ap_proof_runtime;
GRANT SELECT ON ap_proof.coa_snapshots TO mdgsm_ap_proof_runtime;
