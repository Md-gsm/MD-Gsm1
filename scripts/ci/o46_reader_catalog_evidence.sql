\echo 'O-46 JRN-REC-03 evidence collection — READ ONLY; execute using the actual API/Setup Service DB connection identity.'
\set ON_ERROR_STOP on
BEGIN TRANSACTION READ ONLY;

SELECT
  current_database() AS database_name,
  current_user AS effective_current_user,
  session_user AS authenticated_session_user,
  current_setting('server_version') AS server_version,
  current_setting('server_version_num') AS server_version_num,
  current_setting('transaction_isolation') AS transaction_isolation;

SELECT
  r.rolname,
  r.rolcanlogin,
  r.rolsuper,
  r.rolcreatedb,
  r.rolcreaterole,
  r.rolinherit,
  r.rolbypassrls
FROM pg_catalog.pg_roles AS r
WHERE r.rolname IN (
  current_user,
  session_user,
  'mdgsm_runtime',
  'runtime_app',
  'reporting_ro',
  'backup_operator',
  'emergency_operator',
  'mdgsm_schema_owner',
  'mdgsm_bootstrap_executor',
  'mdgsm_identity_bootstrap_fn_owner',
  'migration_runner'
)
ORDER BY r.rolname;

SELECT
  member_role.rolname AS member_role,
  granted_role.rolname AS granted_role,
  m.admin_option,
  m.inherit_option,
  m.set_option
FROM pg_catalog.pg_auth_members AS m
JOIN pg_catalog.pg_roles AS member_role ON member_role.oid = m.member
JOIN pg_catalog.pg_roles AS granted_role ON granted_role.oid = m.roleid
WHERE member_role.rolname IN (current_user, session_user)
   OR pg_catalog.pg_has_role(current_user, member_role.oid, 'MEMBER')
ORDER BY member_role.rolname, granted_role.rolname;

SELECT
  n.nspname AS schema_name,
  c.relname AS relation_name,
  pg_catalog.pg_get_userbyid(c.relowner) AS relation_owner,
  c.relkind AS relation_kind,
  pg_catalog.has_schema_privilege(current_user, n.oid, 'USAGE') AS schema_usage,
  pg_catalog.has_table_privilege(current_user, c.oid, 'SELECT') AS effective_select,
  COALESCE(c.relacl::text, '<default ACL>') AS relation_acl
FROM pg_catalog.pg_class AS c
JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
WHERE c.relkind IN ('r', 'p', 'v', 'm', 'f')
  AND n.nspname NOT IN ('pg_catalog', 'information_schema')
  AND n.nspname NOT LIKE 'pg_toast%'
ORDER BY n.nspname, c.relname;

SELECT
  p.oid::pg_catalog.regprocedure AS function_signature,
  pg_catalog.has_function_privilege(current_user, p.oid, 'EXECUTE') AS effective_execute
FROM pg_catalog.pg_proc AS p
WHERE p.oid IN (
  'pg_catalog.hashtextextended(text,bigint)'::pg_catalog.regprocedure,
  'pg_catalog.pg_advisory_xact_lock(bigint)'::pg_catalog.regprocedure,
  'pg_catalog.pg_advisory_xact_lock_shared(bigint)'::pg_catalog.regprocedure
)
ORDER BY p.oid::pg_catalog.regprocedure::text;

ROLLBACK;
