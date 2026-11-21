-- Fail-closed O20-G assertions; only the marked disposable CI database is eligible.
DO $o20g$
DECLARE runtime_oid oid; migration_oid oid; owner_oid oid;
        db_oid oid := (SELECT oid FROM pg_database WHERE datname = current_database());
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_database d WHERE d.oid = db_oid
       AND shobj_description(d.oid, 'pg_database') LIKE 'MDGSM_DISPOSABLE_TEST_DB;%') THEN
        RAISE EXCEPTION 'O20-G refused: current database is not marked disposable';
    END IF;
    IF NOT (SELECT rolsuper FROM pg_roles WHERE rolname = current_user) THEN
        RAISE EXCEPTION 'O20-G requires catalog-audit authority in disposable CI';
    END IF;
    SELECT oid INTO runtime_oid FROM pg_roles WHERE rolname = 'mdgsm_runtime';
    SELECT oid INTO migration_oid FROM pg_roles WHERE rolname = 'migration_runner';
    SELECT oid INTO owner_oid FROM pg_roles WHERE rolname = 'mdgsm_schema_owner';
    IF runtime_oid IS NULL OR migration_oid IS NULL OR owner_oid IS NULL THEN
        RAISE EXCEPTION 'O20-G expected runtime, migration LOGIN and NOLOGIN schema owner roles';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname NOT LIKE 'pg\_%' ESCAPE '\'
       AND rolname <> 'postgres'
       AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)) THEN
        RAISE EXCEPTION 'O20-G found an unexpected elevated non-system role';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles
       WHERE rolname IN ('mdgsm_runtime', 'mdgsm_schema_owner')
       AND (rolcanlogin OR rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls OR rolinherit)) THEN
        RAISE EXCEPTION 'O20-G runtime or schema-owner role attributes violate least privilege';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'migration_runner'
       AND (NOT rolcanlogin OR rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls OR rolinherit)) THEN
        RAISE EXCEPTION 'O20-G migration_runner attributes violate least privilege';
    END IF;
    IF (SELECT count(*) FROM pg_auth_members WHERE member = migration_oid) <> 1
       OR NOT EXISTS (SELECT 1 FROM pg_auth_members WHERE member = migration_oid AND roleid = owner_oid
             AND NOT admin_option AND NOT inherit_option AND set_option) THEN
        RAISE EXCEPTION 'O20-G migration_runner must have exactly one non-admin, non-inheriting SET ROLE edge to mdgsm_schema_owner';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_auth_members WHERE member = runtime_oid OR roleid = runtime_oid) THEN
        RAISE EXCEPTION 'O20-G mdgsm_runtime has unexpected role membership edges';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_shdepend d WHERE d.refclassid = 'pg_authid'::regclass
       AND d.refobjid IN (runtime_oid, migration_oid) AND d.deptype = 'o'
       AND (d.dbid = db_oid OR d.dbid = 0)) THEN
        RAISE EXCEPTION 'O20-G runtime or migration LOGIN unexpectedly owns database objects';
    END IF;
    IF has_database_privilege('migration_runner', current_database(), 'CREATE')
       OR has_schema_privilege('migration_runner', 'public', 'CREATE')
       OR NOT pg_has_role('migration_runner', 'mdgsm_schema_owner', 'SET') THEN
        RAISE EXCEPTION 'O20-G migration effective privilege/SET ROLE boundary is invalid';
    END IF;
    IF has_database_privilege('mdgsm_runtime', current_database(), 'CREATE')
       OR EXISTS (SELECT 1 FROM pg_namespace n WHERE has_schema_privilege('mdgsm_runtime', n.oid, 'CREATE')) THEN
        RAISE EXCEPTION 'O20-G runtime has unexpected database or schema CREATE privilege';
    END IF;
    IF has_table_privilege('mdgsm_runtime', 'identity.accounts', 'INSERT')
       OR has_table_privilege('mdgsm_runtime', 'identity.security_audit_events', 'UPDATE')
       OR has_table_privilege('mdgsm_runtime', 'identity.security_audit_events', 'DELETE')
       OR has_table_privilege('mdgsm_runtime', 'identity.security_audit_events', 'TRUNCATE')
       OR has_table_privilege('mdgsm_runtime', 'identity.recovery_codes', 'SELECT')
       OR has_table_privilege('mdgsm_runtime', 'identity.recovery_codes', 'INSERT')
       OR has_table_privilege('mdgsm_runtime', 'identity.recovery_codes', 'UPDATE')
       OR has_table_privilege('mdgsm_runtime', 'identity.recovery_codes', 'DELETE')
       OR has_table_privilege('mdgsm_runtime', 'identity.recovery_codes', 'TRUNCATE') THEN
        RAISE EXCEPTION 'O20-G runtime has an unauthorized IdentityAccess table privilege';
    END IF;
    IF NOT has_table_privilege('mdgsm_runtime', 'identity.sessions', 'SELECT')
       OR NOT has_table_privilege('mdgsm_runtime', 'identity.sessions', 'INSERT')
       OR NOT has_table_privilege('mdgsm_runtime', 'identity.security_audit_events', 'INSERT')
       OR has_table_privilege('mdgsm_runtime', 'identity.roles', 'INSERT')
       OR has_table_privilege('mdgsm_runtime', 'identity.permissions', 'INSERT')
       OR has_table_privilege('mdgsm_runtime', 'identity.account_roles', 'INSERT')
       OR has_table_privilege('mdgsm_runtime', 'identity.role_permissions', 'INSERT') THEN
        RAISE EXCEPTION 'O20-G runtime grant contract differs from reviewed IdentityAccess grants';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_proc p WHERE p.prosecdef AND p.proowner = runtime_oid) THEN
        RAISE EXCEPTION 'O20-G runtime unexpectedly owns SECURITY DEFINER routines';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_default_acl d WHERE d.defaclrole IN (runtime_oid, migration_oid)) THEN
        RAISE EXCEPTION 'O20-G runtime or migration LOGIN has unexpected default ACL ownership';
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname IN ('backup_operator', 'reporting_ro', 'emergency_operator')) THEN
        RAISE EXCEPTION 'O20-G found a support LOGIN that should not persist in disposable CI';
    END IF;
END
$o20g$;
