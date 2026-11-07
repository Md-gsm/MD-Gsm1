-- IdentityAccess persistence schema for MD-Gsm.
-- No credentials, role assignments, permission grants, or permission catalog are seeded.
-- The singleton policy row is required by IdentityAccessUnitOfWork.
CREATE SCHEMA IF NOT EXISTS identity;
REVOKE CREATE ON SCHEMA identity FROM PUBLIC;
CREATE TABLE identity.accounts (
 account_id uuid PRIMARY KEY, login_name text NOT NULL, login_name_normalized text NOT NULL,
 password_hash text NOT NULL, password_hash_version smallint NOT NULL CHECK(password_hash_version>=1),
 must_change_password boolean NOT NULL DEFAULT false, is_enabled boolean NOT NULL DEFAULT true,
 is_bootstrap_account boolean NOT NULL DEFAULT false, credential_version bigint NOT NULL DEFAULT 1,
 failed_attempt_count integer NOT NULL DEFAULT 0, failure_window_started_at timestamptz, locked_until timestamptz,
 email text, phone_e164 text, created_at_utc timestamptz NOT NULL DEFAULT now(),
 updated_at_utc timestamptz NOT NULL DEFAULT now(), created_by_account_id uuid,
 CONSTRAINT ck_identity_accounts_failed_attempt_count CHECK(failed_attempt_count>=0),
 CONSTRAINT ck_identity_accounts_credential_version CHECK(credential_version>=1),
 CONSTRAINT fk_identity_accounts_created_by FOREIGN KEY(created_by_account_id) REFERENCES identity.accounts(account_id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX ux_identity_accounts_login_name_normalized ON identity.accounts(login_name_normalized);
CREATE UNIQUE INDEX ux_identity_accounts_single_bootstrap ON identity.accounts(is_bootstrap_account) WHERE is_bootstrap_account=TRUE;
CREATE TABLE identity.roles (
 role_id uuid PRIMARY KEY, role_key text NOT NULL UNIQUE, display_name text NOT NULL, description text,
 is_system_role boolean NOT NULL DEFAULT false, is_assignable boolean NOT NULL DEFAULT true,
 created_at_utc timestamptz NOT NULL DEFAULT now(), updated_at_utc timestamptz NOT NULL DEFAULT now()
);
CREATE TABLE identity.permissions (
 permission_id uuid PRIMARY KEY, permission_key text NOT NULL UNIQUE, display_name text NOT NULL,
 description text, risk_level text NOT NULL DEFAULT 'standard', is_active boolean NOT NULL DEFAULT true,
 CONSTRAINT ck_identity_permissions_risk_level CHECK(risk_level IN('standard','sensitive','critical'))
);
CREATE TABLE identity.account_roles (
 account_id uuid NOT NULL, role_id uuid NOT NULL, assigned_at_utc timestamptz NOT NULL DEFAULT now(),
 assigned_by_account_id uuid, assignment_reason text NOT NULL DEFAULT '', PRIMARY KEY(account_id,role_id),
 CONSTRAINT fk_identity_account_roles_account FOREIGN KEY(account_id) REFERENCES identity.accounts(account_id) ON DELETE RESTRICT,
 CONSTRAINT fk_identity_account_roles_role FOREIGN KEY(role_id) REFERENCES identity.roles(role_id) ON DELETE RESTRICT,
 CONSTRAINT fk_identity_account_roles_assigned_by FOREIGN KEY(assigned_by_account_id) REFERENCES identity.accounts(account_id) ON DELETE RESTRICT
);
CREATE INDEX ix_identity_account_roles_role_account ON identity.account_roles(role_id,account_id);
CREATE TABLE identity.role_permissions (
 role_id uuid NOT NULL, permission_id uuid NOT NULL, PRIMARY KEY(role_id,permission_id),
 CONSTRAINT fk_identity_role_permissions_role FOREIGN KEY(role_id) REFERENCES identity.roles(role_id) ON DELETE RESTRICT,
 CONSTRAINT fk_identity_role_permissions_permission FOREIGN KEY(permission_id) REFERENCES identity.permissions(permission_id) ON DELETE RESTRICT
);
CREATE INDEX ix_identity_role_permissions_permission_role ON identity.role_permissions(permission_id,role_id);
CREATE TABLE identity.sessions (
 session_id uuid PRIMARY KEY, account_id uuid NOT NULL, token_digest bytea NOT NULL,
 created_at_utc timestamptz NOT NULL, last_seen_at_utc timestamptz NOT NULL, lease_expires_at_utc timestamptz NOT NULL,
 revoked_at_utc timestamptz, revoke_reason text, credential_version_at_issue bigint NOT NULL CHECK(credential_version_at_issue>=1),
 client_instance_id uuid NOT NULL, CONSTRAINT ck_identity_sessions_token_digest_length CHECK(octet_length(token_digest)=32),
 CONSTRAINT fk_identity_sessions_account FOREIGN KEY(account_id) REFERENCES identity.accounts(account_id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX ux_identity_sessions_token_digest ON identity.sessions(token_digest);
CREATE INDEX ix_identity_sessions_account_lease ON identity.sessions(account_id,lease_expires_at_utc);
CREATE INDEX ix_identity_sessions_active_lease ON identity.sessions(lease_expires_at_utc,account_id) WHERE revoked_at_utc IS NULL;
CREATE TABLE identity.login_attempts (
 attempt_id uuid PRIMARY KEY, account_id uuid, login_name_digest bytea NOT NULL, source_key_digest bytea NOT NULL,
 outcome text NOT NULL, occurred_at_utc timestamptz NOT NULL,
 CONSTRAINT ck_identity_login_attempts_digest_lengths CHECK(octet_length(login_name_digest)=32 AND octet_length(source_key_digest)=32),
 CONSTRAINT fk_identity_login_attempts_account FOREIGN KEY(account_id) REFERENCES identity.accounts(account_id) ON DELETE RESTRICT
);
CREATE INDEX ix_identity_login_attempts_account_time ON identity.login_attempts(account_id,occurred_at_utc);
CREATE INDEX ix_identity_login_attempts_source_time ON identity.login_attempts(source_key_digest,occurred_at_utc);
CREATE TABLE identity.recovery_codes (
 recovery_code_id uuid PRIMARY KEY, account_id uuid NOT NULL, code_digest bytea NOT NULL, created_at_utc timestamptz NOT NULL,
 used_at_utc timestamptz, revoked_at_utc timestamptz,
 CONSTRAINT ck_identity_recovery_codes_digest_length CHECK(octet_length(code_digest)=32),
 CONSTRAINT fk_identity_recovery_codes_account FOREIGN KEY(account_id) REFERENCES identity.accounts(account_id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX ux_identity_recovery_codes_digest ON identity.recovery_codes(code_digest);
CREATE TABLE identity.recovery_challenges (
 challenge_id uuid PRIMARY KEY, account_id uuid NOT NULL, challenge_digest bytea NOT NULL, channel text NOT NULL,
 created_at_utc timestamptz NOT NULL, expires_at_utc timestamptz NOT NULL, consumed_at_utc timestamptz, revoked_at_utc timestamptz,
 CONSTRAINT ck_identity_recovery_challenges_digest_length CHECK(octet_length(challenge_digest)=32),
 CONSTRAINT ck_identity_recovery_challenges_channel CHECK(channel='email'),
 CONSTRAINT fk_identity_recovery_challenges_account FOREIGN KEY(account_id) REFERENCES identity.accounts(account_id) ON DELETE RESTRICT
);
CREATE UNIQUE INDEX ux_identity_recovery_challenges_digest ON identity.recovery_challenges(challenge_digest);
CREATE INDEX ix_identity_recovery_challenges_account_channel_expiry ON identity.recovery_challenges(account_id,channel,expires_at_utc);
CREATE TABLE identity.security_audit_events (
 event_id uuid PRIMARY KEY, occurred_at_utc timestamptz NOT NULL, actor_account_id uuid, subject_account_id uuid,
 event_type text NOT NULL, outcome text NOT NULL, correlation_id text NOT NULL, details jsonb NOT NULL DEFAULT '{}'::jsonb,
 CONSTRAINT fk_identity_audit_actor FOREIGN KEY(actor_account_id) REFERENCES identity.accounts(account_id) ON DELETE RESTRICT,
 CONSTRAINT fk_identity_audit_subject FOREIGN KEY(subject_account_id) REFERENCES identity.accounts(account_id) ON DELETE RESTRICT
);
CREATE INDEX ix_identity_audit_events_occurred ON identity.security_audit_events(occurred_at_utc);
CREATE INDEX ix_identity_audit_events_actor_occurred ON identity.security_audit_events(actor_account_id,occurred_at_utc);
CREATE INDEX ix_identity_audit_events_subject_occurred ON identity.security_audit_events(subject_account_id,occurred_at_utc);
CREATE FUNCTION identity.reject_security_audit_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'IdentityAccess security audit events are append-only'; END;
$$;
CREATE TRIGGER trg_identity_security_audit_no_update_delete BEFORE UPDATE OR DELETE ON identity.security_audit_events
 FOR EACH ROW EXECUTE FUNCTION identity.reject_security_audit_mutation();
REVOKE UPDATE,DELETE,TRUNCATE ON identity.security_audit_events FROM PUBLIC;
CREATE TABLE identity.system_settings (
 settings_id smallint PRIMARY KEY, max_active_users integer NOT NULL, heartbeat_interval_seconds integer NOT NULL,
 session_lease_seconds integer NOT NULL, bootstrap_completed_at_utc timestamptz, updated_at_utc timestamptz NOT NULL DEFAULT now(),
 updated_by_account_id uuid, CONSTRAINT ck_identity_system_settings_singleton CHECK(settings_id=1),
 CONSTRAINT ck_identity_system_settings_max_users CHECK(max_active_users>=1),
 CONSTRAINT ck_identity_system_settings_heartbeat CHECK(heartbeat_interval_seconds>0),
 CONSTRAINT ck_identity_system_settings_lease CHECK(session_lease_seconds>heartbeat_interval_seconds),
 CONSTRAINT fk_identity_system_settings_updated_by FOREIGN KEY(updated_by_account_id) REFERENCES identity.accounts(account_id) ON DELETE RESTRICT
);
INSERT INTO identity.system_settings(settings_id,max_active_users,heartbeat_interval_seconds,session_lease_seconds)
VALUES(1,5,30,180) ON CONFLICT(settings_id) DO NOTHING;
COMMENT ON SCHEMA identity IS 'IdentityAccess authentication, authorization, sessions, recovery and security audit data.';
COMMENT ON TABLE identity.security_audit_events IS 'Append-only security audit evidence; updates and deletes are rejected by trigger.';
COMMENT ON TABLE identity.system_settings IS 'Exactly one row (settings_id=1) serializes session-capacity decisions.';
