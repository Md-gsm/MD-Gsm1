-- IdentityAccess runtime-role grants.
-- Run as the owner/deployment identity AFTER applying reviewed migrations.
-- The application login must be explicitly granted membership in mdgsm_runtime by the DBA.
-- This script creates no LOGIN role and stores no password/secret.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mdgsm_runtime') THEN
        EXECUTE 'CREATE ROLE mdgsm_runtime NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT';
    END IF;
END
$$;

REVOKE ALL ON SCHEMA identity FROM PUBLIC;
REVOKE ALL ON ALL TABLES IN SCHEMA identity FROM PUBLIC;
GRANT USAGE ON SCHEMA identity TO mdgsm_runtime;

-- Read-only authorization catalog and assignments; grants/role assignments remain owner-controlled.
GRANT SELECT ON identity.roles, identity.permissions,
    identity.account_roles, identity.role_permissions TO mdgsm_runtime;

-- Existing-account login and password/lockout updates only.
-- No account INSERT is granted: bootstrap/account provisioning is a separate reviewed operation.
GRANT SELECT ON identity.accounts TO mdgsm_runtime;
GRANT UPDATE (
    login_name, login_name_normalized, password_hash, password_hash_version,
    must_change_password, is_enabled, credential_version, failed_attempt_count,
    failure_window_started_at, locked_until, updated_at_utc
) ON identity.accounts TO mdgsm_runtime;

-- Session lifecycle; no delete/truncate or table ownership.
GRANT SELECT, INSERT ON identity.sessions TO mdgsm_runtime;
GRANT UPDATE (last_seen_at_utc, lease_expires_at_utc, revoked_at_utc, revoke_reason)
    ON identity.sessions TO mdgsm_runtime;

-- Rate-limit evidence is append-only from the application perspective.
GRANT SELECT, INSERT ON identity.login_attempts TO mdgsm_runtime;

-- Recovery challenge issuance/consumption. Recovery-code provisioning is withheld
-- until a separately reviewed bootstrap/recovery workflow exists.
GRANT SELECT, INSERT ON identity.recovery_challenges TO mdgsm_runtime;
GRANT UPDATE (consumed_at_utc, revoked_at_utc)
    ON identity.recovery_challenges TO mdgsm_runtime;
GRANT SELECT ON identity.recovery_codes TO mdgsm_runtime;
GRANT UPDATE (used_at_utc, revoked_at_utc)
    ON identity.recovery_codes TO mdgsm_runtime;

-- Append-only security evidence.
GRANT INSERT ON identity.security_audit_events TO mdgsm_runtime;

-- The UoW takes a row lock with a no-op update. Only that one column is writable.
GRANT SELECT ON identity.system_settings TO mdgsm_runtime;
GRANT UPDATE (updated_at_utc) ON identity.system_settings TO mdgsm_runtime;

-- Intentionally absent: CREATE/ALTER/DROP, table ownership, DELETE, TRUNCATE,
-- permission/role catalog writes, account inserts, schema CREATE, and sequence grants.
