#!/usr/bin/env bash
set -euo pipefail
: "$MDGSM_TEST_DB"
: "$RUNNER_TEMP"
[[ "$MDGSM_TEST_DB" =~ ^mdgsm_ci_disposable_[0-9]+_[0-9]+$ ]]
backup_file="$RUNNER_TEMP/mdgsm-o20d-$GITHUB_RUN_ID-$GITHUB_RUN_ATTEMPT.dump"
restore_db="mdgsm_o20d_restore_${GITHUB_RUN_ID}_${GITHUB_RUN_ATTEMPT}"
[[ "$restore_db" =~ ^mdgsm_o20d_restore_[0-9]+_[0-9]+$ ]]
role_created=0
restore_created=0
cleanup() {
  if [[ "$restore_created" -eq 1 ]]; then psql -X -v ON_ERROR_STOP=1 -d postgres -c "DROP DATABASE IF EXISTS \"$restore_db\";" >/dev/null 2>&1 || true; fi
  if [[ "$role_created" -eq 1 ]]; then
    psql -X -v ON_ERROR_STOP=1 -d "$MDGSM_TEST_DB" -c "DROP OWNED BY backup_operator;" >/dev/null 2>&1 || true
    psql -X -v ON_ERROR_STOP=1 -d postgres -c "DROP OWNED BY backup_operator;" >/dev/null 2>&1 || true
    psql -X -v ON_ERROR_STOP=1 -d postgres -c "DROP ROLE backup_operator;" >/dev/null 2>&1 || true
  fi
  rm -f "$backup_file"
}
trap cleanup EXIT
marker="$(psql -X -d postgres -Atqc "SELECT shobj_description(oid,'pg_database') FROM pg_database WHERE datname='$MDGSM_TEST_DB'")"
[[ "$marker" == *"MDGSM_DISPOSABLE_TEST_DB"* ]] || { echo "Source database is not marked disposable." >&2; exit 1; }
if psql -X -d postgres -Atqc "SELECT 1 FROM pg_roles WHERE rolname='backup_operator'" | grep -q 1; then
  echo "Refusing to reuse an existing backup_operator principal." >&2; exit 1
fi
psql -X -v ON_ERROR_STOP=1 -d postgres -c "CREATE ROLE backup_operator LOGIN NOINHERIT NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS;"
role_created=1
psql -X -v ON_ERROR_STOP=1 -d postgres -c "GRANT CONNECT ON DATABASE \"$MDGSM_TEST_DB\" TO backup_operator;"
psql -X -v ON_ERROR_STOP=1 -d "$MDGSM_TEST_DB" <<'SQL'
DO $grant$
DECLARE s record;
BEGIN
  FOR s IN SELECT n.nspname FROM pg_namespace n
           WHERE n.nspname <> 'information_schema' AND n.nspname NOT LIKE 'pg_%'
  LOOP
    EXECUTE format('GRANT USAGE ON SCHEMA %I TO backup_operator', s.nspname);
    EXECUTE format('GRANT SELECT ON ALL TABLES IN SCHEMA %I TO backup_operator', s.nspname);
    EXECUTE format('GRANT SELECT ON ALL SEQUENCES IN SCHEMA %I TO backup_operator', s.nspname);
  END LOOP;
END
$grant$;
SQL
role_state="$(psql -X -d "$MDGSM_TEST_DB" -Atqc "SELECT (rolcanlogin AND NOT rolsuper AND NOT rolcreatedb AND NOT rolcreaterole AND NOT rolreplication AND NOT rolbypassrls AND NOT rolinherit)::text || '|' || (SELECT count(*) FROM pg_auth_members WHERE member=oid OR roleid=oid)::text || '|' || (SELECT count(*) FROM pg_class c JOIN pg_roles rr ON rr.oid=c.relowner WHERE rr.rolname='backup_operator')::text FROM pg_roles WHERE rolname='backup_operator'")"
test "$role_state" = "true|0|0"
priv_state="$(psql -X -d "$MDGSM_TEST_DB" -Atqc "SELECT has_database_privilege('backup_operator',current_database(),'CONNECT')::text || '|' || has_database_privilege('backup_operator',current_database(),'CREATE')::text || '|' || has_database_privilege('backup_operator',current_database(),'TEMP')::text || '|' || has_table_privilege('backup_operator','identity.accounts','SELECT')::text || '|' || has_table_privilege('backup_operator','identity.accounts','INSERT')::text || '|' || has_table_privilege('backup_operator','identity.accounts','UPDATE')::text || '|' || has_table_privilege('backup_operator','identity.accounts','DELETE')::text || '|' || has_table_privilege('backup_operator','identity.accounts','TRUNCATE')::text || '|' || has_schema_privilege('backup_operator','identity','CREATE')::text")"
test "$priv_state" = "true|false|false|true|false|false|false|false|false"
pg_dump --username=backup_operator --format=custom --no-owner --no-acl --file "$backup_file" "$MDGSM_TEST_DB"
test -s "$backup_file"
pg_restore --list "$backup_file" >/dev/null
if psql -X -d postgres -Atqc "SELECT 1 FROM pg_database WHERE datname='$restore_db'" | grep -q 1; then
  echo "Restore target already exists; refusing to reuse it." >&2; exit 1
fi
createdb "$restore_db"
restore_created=1
psql -X -v ON_ERROR_STOP=1 -d postgres -c "COMMENT ON DATABASE \"$restore_db\" IS 'MDGSM_DISPOSABLE_TEST_DB; O20-D isolated restore';"
psql -X -v ON_ERROR_STOP=1 -d postgres -c "REVOKE CONNECT ON DATABASE \"$restore_db\" FROM PUBLIC;"
pg_restore --exit-on-error --no-owner --no-acl --dbname "$restore_db" "$backup_file"
source_state="$(psql -X -d "$MDGSM_TEST_DB" -Atqc "SELECT (SELECT count(*)::text FROM mdgsm_migration_history) || '|' || (SELECT max(migration_version)::text FROM mdgsm_migration_history) || '|' || (SELECT count(*)::text FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE') || '|' || (SELECT count(*)::text FROM identity.accounts)")"
restored_state="$(psql -X -d "$restore_db" -Atqc "SELECT (SELECT count(*)::text FROM mdgsm_migration_history) || '|' || (SELECT max(migration_version)::text FROM mdgsm_migration_history) || '|' || (SELECT count(*)::text FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE') || '|' || (SELECT count(*)::text FROM identity.accounts)")"
test "$source_state" = "$restored_state"
if psql -X -v ON_ERROR_STOP=1 -U backup_operator -d "$restore_db" -Atqc "SELECT 1" >/dev/null 2>&1; then
  echo "backup_operator unexpectedly connected to restore target." >&2; exit 1
fi
if createdb -U backup_operator "mdgsm_o20d_forbidden_$GITHUB_RUN_ID" >/dev/null 2>&1; then
  echo "backup_operator unexpectedly created a database." >&2; exit 1
fi
echo "PASS: backup_operator performed logical pg_dump with read-only source privileges; separate DBA restored into a marked isolated database; migration and IdentityAccess invariants matched; backup identity cannot write, create databases, or access restore target."
