#!/usr/bin/env bash
set -euo pipefail

: "${MDGSM_TEST_DB:?MDGSM_TEST_DB must name the run-scoped disposable database}"
: "${GITHUB_RUN_ID:?GITHUB_RUN_ID is required}"
: "${GITHUB_RUN_ATTEMPT:?GITHUB_RUN_ATTEMPT is required}"
: "${GITHUB_SHA:?GITHUB_SHA is required}"

if [[ ! "$MDGSM_TEST_DB" =~ ^mdgsm_ci_disposable_[0-9]+_[0-9]+$ ]]; then
  echo "Refusing non-disposable source database: $MDGSM_TEST_DB" >&2
  exit 2
fi

marker="$(psql -d "$MDGSM_TEST_DB" -At -v ON_ERROR_STOP=1 -c "SELECT shobj_description(oid, 'pg_database') FROM pg_database WHERE datname = current_database();")"
if [[ "$marker" != MDGSM_DISPOSABLE_TEST_DB* ]]; then
  echo "Source database marker is missing; refusing restore PoCs." >&2
  exit 2
fi

run_tag="${GITHUB_RUN_ID}_${GITHUB_RUN_ATTEMPT}"
tmp="${RUNNER_TEMP:-/tmp}/mdgsm-restore-poc-$run_tag"
mkdir -p "$tmp" artifacts

new_target() {
  local suffix="$1"
  local db="mdgsm_restore_poc_disposable_${run_tag}_${suffix}"
  if [[ ! "$db" =~ ^mdgsm_restore_poc_disposable_[0-9]+_[0-9]+_[a-z0-9]+$ ]]; then
    echo "Invalid restore target name: $db" >&2
    exit 2
  fi
  createdb "$db"
  psql -d postgres -v ON_ERROR_STOP=1 -c "COMMENT ON DATABASE \"$db\" IS 'MDGSM_DISPOSABLE_TEST_DB; Gate D restore PoC; run $run_tag';" >/dev/null
  printf '%s' "$db"
}

snapshot() {
  local db="$1"
  psql -d "$db" -At -v ON_ERROR_STOP=1 -c "
    SELECT
      (SELECT count(*) FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE')::text || '|' ||
      (SELECT count(*) FROM identity.system_settings)::text || '|' ||
      (SELECT count(*) FROM identity.accounts)::text || '|' ||
      (SELECT count(*) FROM identity.sessions)::text || '|' ||
      (SELECT count(*) FROM identity.security_audit_events)::text || '|' ||
      (SELECT count(*) FROM mdgsm_migration_history)::text || '|' ||
      (SELECT max(migration_version)::text FROM mdgsm_migration_history);
  "
}

source_snapshot="$(snapshot "$MDGSM_TEST_DB")"
# Structural parity: normalized pg_dump schema covers columns/types, constraints,
# indexes, triggers, routines, sequences, extensions and schema-owned objects.
# Ownership and ACL/default-ACL are fingerprinted separately (except DR-07).
schema_fingerprint() {
  local db="$1"
  # PostgreSQL 18 pg_dump adds a per-invocation \\restrict token; remove that
  # transport guard line before hashing so equivalent schemas compare identically.
  pg_dump --schema-only --no-owner --no-privileges "$db" |
    sed -E '/^--/d; /^$/d; /^\\\\(un)?restrict /d' |
    sha256sum | awk '{print $1}'
}

security_catalog_fingerprint() {
  local db="$1"
  psql -d "$db" -At -v ON_ERROR_STOP=1 -c "
    SELECT md5(COALESCE(jsonb_agg(item ORDER BY item::text)::text, '[]'))
    FROM (
      SELECT jsonb_build_array('namespace', n.nspname, pg_get_userbyid(n.nspowner), COALESCE(n.nspacl::text,'')) AS item
      FROM pg_namespace n WHERE n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname NOT LIKE 'pg_toast%'
      UNION ALL
      SELECT jsonb_build_array('relation', n.nspname, c.relname, c.relkind, pg_get_userbyid(c.relowner), COALESCE(c.relacl::text,'')) AS item
      FROM pg_class c JOIN pg_namespace n ON n.oid=c.relnamespace
      WHERE n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname NOT LIKE 'pg_toast%'
      UNION ALL
      SELECT jsonb_build_array('routine', n.nspname, p.proname, pg_get_userbyid(p.proowner), COALESCE(p.proacl::text,'')) AS item
      FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
      WHERE n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname NOT LIKE 'pg_toast%'
      UNION ALL
      SELECT jsonb_build_array('type', n.nspname, t.typname, pg_get_userbyid(t.typowner), COALESCE(t.typacl::text,'')) AS item
      FROM pg_type t JOIN pg_namespace n ON n.oid=t.typnamespace
      WHERE n.nspname NOT IN ('pg_catalog','information_schema') AND n.nspname NOT LIKE 'pg_toast%'
      UNION ALL
      SELECT jsonb_build_array('default_acl', COALESCE(n.nspname,''), d.defaclobjtype, pg_get_userbyid(d.defaclrole), COALESCE(d.defaclacl::text,'')) AS item
      FROM pg_default_acl d LEFT JOIN pg_namespace n ON n.oid=d.defaclnamespace
      UNION ALL
      SELECT jsonb_build_array('role', r.rolname, r.rolsuper, r.rolinherit, r.rolcreaterole,
        r.rolcreatedb, r.rolcanlogin, r.rolreplication, r.rolbypassrls, r.rolconnlimit) AS item
      FROM pg_roles r WHERE r.rolname !~ '^pg_'
      UNION ALL
      SELECT jsonb_build_array('role_membership', pg_get_userbyid(m.roleid), pg_get_userbyid(m.member),
        pg_get_userbyid(m.grantor), m.admin_option) AS item
      FROM pg_auth_members m
      JOIN pg_roles member_role ON member_role.oid=m.member
      WHERE member_role.rolname !~ '^pg_'
    ) objects;
  "
}

source_schema_fingerprint="$(schema_fingerprint "$MDGSM_TEST_DB")"
source_security_fingerprint="$(security_catalog_fingerprint "$MDGSM_TEST_DB")"
check_equal() {
  local label="$1" expected="$2" actual="$3"
  if [[ "$expected" != "$actual" ]]; then
    echo "FAIL $label expected=$expected actual=$actual" >&2
    if [[ "$label" == schema\ catalog* ]]; then
      pg_dump --schema-only --no-owner --no-privileges "$MDGSM_TEST_DB" |
        sed -E '/^--/d; /^$/d; /^\\\\(un)?restrict /d' > "$tmp/source.schema.sql"
      target_db="${label#schema catalog }"
      pg_dump --schema-only --no-owner --no-privileges "$target_db" |
        sed -E '/^--/d; /^$/d; /^\\\\(un)?restrict /d' > "$tmp/target.schema.sql"
      echo "Schema parity diff (first 120 lines):" >&2
      diff -u "$tmp/source.schema.sql" "$tmp/target.schema.sql" | head -120 >&2 || true
    fi
    return 1
  fi
  echo "PASS $label"
}
source_version="$(psql -d "$MDGSM_TEST_DB" -At -v ON_ERROR_STOP=1 -c 'SHOW server_version;')"
drills=()

write_evidence() {
  local exit_code="$1"
  local overall="PASS"
  [[ "$exit_code" -eq 0 ]] || overall="FAIL"
  local script_hash=""
  local dump_hash=""
  if [[ -f "$0" ]]; then script_hash="$(sha256sum "$0" | awk '{print $1}')"; fi
  if [[ -f "$tmp/full.custom" ]]; then dump_hash="$(sha256sum "$tmp/full.custom" | awk '{print $1}')"; fi
  local joined=""
  if (( ${#drills[@]} > 0 )); then joined="$(IFS=,; echo "${drills[*]}")"; fi
  printf '{\n  "evidenceVersion": "1.0",\n  "evidenceId": "EVD-GATED-RESTORE-POC",\n  "overallStatus": "%s",\n  "exitCode": %s,\n  "repository": "Md-gsm/MD-Gsm1",\n  "commit": "%s",\n  "workflowRunId": "%s",\n  "workflowRunAttempt": "%s",\n  "postgresServerVersion": "%s",\n  "sourceDatabase": "%s",\n  "sourceDatabaseMarker": "MDGSM_DISPOSABLE_TEST_DB",\n  "sourceSnapshot": "%s",\n  "sourceSchemaFingerprint": "%s",\n  "sourceSecurityCatalogFingerprint": "%s",\n  "restoreScriptSha256": "%s",\n  "fullCustomDumpSha256": "%s",\n  "scope": "PostgreSQL logical restore PoC against current IdentityAccess/infrastructure schema only; not full Gate D recovery.",\n  "drills": [\n    %s\n  ]\n}\n' "$overall" "$exit_code" "$GITHUB_SHA" "$GITHUB_RUN_ID" "$GITHUB_RUN_ATTEMPT" "$source_version" "$MDGSM_TEST_DB" "$source_snapshot" "$source_schema_fingerprint" "$source_security_fingerprint" "$script_hash" "$dump_hash" "$joined" > artifacts/restore-poc-evidence.json
}

finish() {
  local exit_code="$?"
  trap - EXIT
  write_evidence "$exit_code"
  if [[ "$exit_code" -eq 0 ]]; then
    echo "PASS: all eight PostgreSQL logical restore PoCs completed on PostgreSQL $source_version."
  else
    echo "FAIL: restore PoC stopped after ${#drills[@]} successful scenario(s); partial EVD written."
  fi
  exit "$exit_code"
}
trap finish EXIT

record() {
  local id="$1" target="$2" detail="$3"
  drills+=("{\"id\":\"$id\",\"status\":\"PASS\",\"targetDatabase\":\"$target\",\"detail\":\"$detail\"}")
  echo "PASS $id — $detail"
}

# DR-01: PostgreSQL custom-format full logical backup and restore.
pg_dump --format=custom --file "$tmp/full.custom" "$MDGSM_TEST_DB"
db="$(new_target dr01)"
pg_restore --exit-on-error --dbname "$db" "$tmp/full.custom"
check_equal "snapshot $db" "$source_snapshot" "$(snapshot "$db")"
check_equal "schema catalog $db" "$source_schema_fingerprint" "$(schema_fingerprint "$db")"
check_equal "security catalog $db" "$source_security_fingerprint" "$(security_catalog_fingerprint "$db")"
record DR-01 "$db" "custom-format full database restore; schema/data snapshot matches"

# DR-02: Plain SQL full backup and restore.
pg_dump --format=plain --file "$tmp/full.sql" "$MDGSM_TEST_DB"
db="$(new_target dr02)"
psql -d "$db" -v ON_ERROR_STOP=1 -f "$tmp/full.sql" >/dev/null
check_equal "snapshot $db" "$source_snapshot" "$(snapshot "$db")"
check_equal "schema catalog $db" "$source_schema_fingerprint" "$(schema_fingerprint "$db")"
check_equal "security catalog $db" "$source_security_fingerprint" "$(security_catalog_fingerprint "$db")"
record DR-02 "$db" "plain SQL full database restore; schema/data snapshot matches"

# DR-03: Directory archive with parallel dump and restore.
pg_dump --format=directory --jobs=2 --file "$tmp/full.directory" "$MDGSM_TEST_DB"
db="$(new_target dr03)"
pg_restore --jobs=2 --exit-on-error --dbname "$db" "$tmp/full.directory"
check_equal "snapshot $db" "$source_snapshot" "$(snapshot "$db")"
check_equal "schema catalog $db" "$source_schema_fingerprint" "$(schema_fingerprint "$db")"
check_equal "security catalog $db" "$source_security_fingerprint" "$(security_catalog_fingerprint "$db")"
record DR-03 "$db" "directory archive parallel restore; schema/data snapshot matches"

# DR-04: Schema-only restore creates the same IdentityAccess table set without row data.
pg_dump --schema-only --format=custom --file "$tmp/schema.custom" "$MDGSM_TEST_DB"
db="$(new_target dr04)"
pg_restore --exit-on-error --dbname "$db" "$tmp/schema.custom"
schema_tables="$(psql -d "$db" -At -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE';")"
settings_rows="$(psql -d "$db" -At -v ON_ERROR_STOP=1 -c 'SELECT count(*) FROM identity.system_settings;')"
[[ "$schema_tables" == "11" && "$settings_rows" == "0" ]]
check_equal "schema catalog $db" "$source_schema_fingerprint" "$(schema_fingerprint "$db")"
check_equal "security catalog $db" "$source_security_fingerprint" "$(security_catalog_fingerprint "$db")"
record DR-04 "$db" "schema-only restore; 11 IdentityAccess tables present and data absent"

# DR-05: Data-only restore into a schema-only target.
pg_dump --data-only --format=custom --file "$tmp/data.custom" "$MDGSM_TEST_DB"
db="$(new_target dr05)"
pg_restore --exit-on-error --dbname "$db" "$tmp/schema.custom"
pg_restore --data-only --exit-on-error --dbname "$db" "$tmp/data.custom"
check_equal "snapshot $db" "$source_snapshot" "$(snapshot "$db")"
check_equal "schema catalog $db" "$source_schema_fingerprint" "$(schema_fingerprint "$db")"
check_equal "security catalog $db" "$source_security_fingerprint" "$(security_catalog_fingerprint "$db")"
record DR-05 "$db" "data-only restore after schema restore; schema/data snapshot matches"

# DR-06: Selective restore of the session policy table into a schema-only target.
db="$(new_target dr06)"
pg_restore --exit-on-error --dbname "$db" "$tmp/schema.custom"
echo "START DR-06 selective system settings restore"
pg_restore --data-only --schema=identity --table=system_settings --exit-on-error --dbname "$db" "$tmp/data.custom"
settings="$(psql -d "$db" -At -v ON_ERROR_STOP=1 -c "SELECT max_active_users::text || '|' || heartbeat_interval_seconds::text || '|' || session_lease_seconds::text FROM identity.system_settings WHERE settings_id=1;")"
[[ "$settings" == "5|30|180" ]]
check_equal "schema catalog $db" "$source_schema_fingerprint" "$(schema_fingerprint "$db")"
record DR-06 "$db" "selective system settings restore; session policy values preserved"

# DR-07: Restore without source ownership/ACL dependencies.
db="$(new_target dr07)"
pg_restore --no-owner --no-privileges --exit-on-error --dbname "$db" "$tmp/full.custom"
check_equal "snapshot $db" "$source_snapshot" "$(snapshot "$db")"
# DR-07 intentionally omits owner/ACL; structural schema must still match.
check_equal "schema catalog $db" "$source_schema_fingerprint" "$(schema_fingerprint "$db")"
record DR-07 "$db" "ownership-independent restore into a fresh target; schema/data snapshot matches"

# DR-08: Corrupt archive must fail closed and must not be accepted as a successful restore.
cp "$tmp/full.custom" "$tmp/full.corrupt"
size="$(stat -c '%s' "$tmp/full.corrupt")"
if (( size < 256 )); then echo "Archive unexpectedly small." >&2; exit 1; fi
truncate -s "$((size - 128))" "$tmp/full.corrupt"
db="$(new_target dr08)"
if pg_restore --single-transaction --exit-on-error --dbname "$db" "$tmp/full.corrupt" >/dev/null 2>&1; then
  echo "Corrupted archive was accepted; DR-08 failed." >&2
  exit 1
fi
record DR-08 "$db" "truncated archive rejected by pg_restore; target remains disposable/quarantined"
