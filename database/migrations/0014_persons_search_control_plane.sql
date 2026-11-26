-- Persons Search Control Plane (owner-approved Issue #97 / WG-0 PR #107).
-- Persistent quotas and abuse locks are additional controls; authorization remains per request.

CREATE TABLE persons_search_actor_purpose_counters (
    actor_id varchar(128) NOT NULL CHECK (btrim(actor_id) <> ''),
    purpose varchar(80) NOT NULL CHECK (btrim(purpose) <> ''),
    window_kind varchar(8) NOT NULL CHECK (window_kind IN ('minute', 'day')),
    window_start_utc timestamptz NOT NULL,
    request_count integer NOT NULL CHECK (request_count >= 0),
    updated_at_utc timestamptz NOT NULL,
    PRIMARY KEY (actor_id, purpose, window_kind, window_start_utc)
);
CREATE INDEX ix_persons_search_actor_counter_cleanup
    ON persons_search_actor_purpose_counters (window_start_utc);

CREATE TABLE persons_search_global_daily_counters (
    utc_day date PRIMARY KEY,
    request_count bigint NOT NULL CHECK (request_count >= 0),
    warning_emitted boolean NOT NULL DEFAULT false,
    updated_at_utc timestamptz NOT NULL
);

CREATE TABLE persons_search_abuse_window (
    actor_id varchar(128) NOT NULL,
    violation_id uuid NOT NULL,
    occurred_at_utc timestamptz NOT NULL,
    PRIMARY KEY (actor_id, violation_id)
);
CREATE INDEX ix_persons_search_abuse_window_actor_time
    ON persons_search_abuse_window (actor_id, occurred_at_utc);

CREATE TABLE persons_search_security_violation_audit (
    violation_id uuid PRIMARY KEY,
    actor_id varchar(128) NOT NULL CHECK (btrim(actor_id) <> ''),
    violation_code varchar(48) NOT NULL CHECK (violation_code IN
        ('permission_denied', 'actor_identity_tampering', 'purpose_tampering', 'authorization_bypass')),
    request_id varchar(128) NOT NULL CHECK (btrim(request_id) <> ''),
    occurred_at_utc timestamptz NOT NULL
);
CREATE INDEX ix_persons_search_violation_actor_time
    ON persons_search_security_violation_audit (actor_id, occurred_at_utc);

CREATE TABLE persons_search_actor_locks (
    actor_id varchar(128) PRIMARY KEY CHECK (btrim(actor_id) <> ''),
    lock_id uuid NOT NULL UNIQUE,
    locked_at_utc timestamptz NOT NULL,
    expires_at_utc timestamptz NOT NULL,
    qualifying_violation_count integer NOT NULL CHECK (qualifying_violation_count >= 5),
    CHECK (expires_at_utc > locked_at_utc)
);
CREATE INDEX ix_persons_search_actor_locks_expiry
    ON persons_search_actor_locks (expires_at_utc);

CREATE TABLE persons_search_lock_audit (
    audit_id uuid PRIMARY KEY,
    lock_id uuid NOT NULL,
    actor_id varchar(128) NOT NULL CHECK (btrim(actor_id) <> ''),
    event_type varchar(24) NOT NULL CHECK (event_type IN ('activated', 'expired', 'admin_unlocked', 'admin_unlock_noop')),
    occurred_at_utc timestamptz NOT NULL,
    admin_actor_id varchar(128),
    reason varchar(300) NOT NULL CHECK (btrim(reason) <> ''),
    request_id varchar(128) NOT NULL CHECK (btrim(request_id) <> '')
);
CREATE INDEX ix_persons_search_lock_audit_actor_time
    ON persons_search_lock_audit (actor_id, occurred_at_utc);

CREATE RULE persons_search_violation_audit_no_update AS
    ON UPDATE TO persons_search_security_violation_audit DO INSTEAD NOTHING;
CREATE RULE persons_search_violation_audit_no_delete AS
    ON DELETE TO persons_search_security_violation_audit DO INSTEAD NOTHING;
CREATE RULE persons_search_lock_audit_no_update AS
    ON UPDATE TO persons_search_lock_audit DO INSTEAD NOTHING;
CREATE RULE persons_search_lock_audit_no_delete AS
    ON DELETE TO persons_search_lock_audit DO INSTEAD NOTHING;

REVOKE UPDATE, DELETE, TRUNCATE
    ON persons_search_security_violation_audit, persons_search_lock_audit FROM PUBLIC;
REVOKE ALL
    ON persons_search_actor_purpose_counters, persons_search_global_daily_counters,
       persons_search_abuse_window, persons_search_security_violation_audit,
       persons_search_actor_locks, persons_search_lock_audit FROM PUBLIC;

COMMENT ON TABLE persons_search_actor_purpose_counters IS
    'Durable per-Actor/Purpose fixed-window request counters. Quota excess is rate-limited and never counted as abuse by itself.';
COMMENT ON TABLE persons_search_abuse_window IS
    'Ephemeral qualifying security violations for the rolling 10-minute lock threshold; audit evidence is retained separately.';
COMMENT ON TABLE persons_search_actor_locks IS
    'Persistent Actor-specific Abuse Lock; 15-minute duration, no global shutdown.';
COMMENT ON TABLE persons_search_lock_audit IS
    'Immutable activation, expiry and administrator-unlock evidence for Persons Search Abuse Locks.';
