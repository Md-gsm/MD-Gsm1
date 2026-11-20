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
source_version="$(psql -d "$MDGSM_TEST_DB" -At -v ON_ERROR_STOP=1 -c 'SHOW server_version;')"
drills=()

record() {
  local id="$1" target="$2" detail="$3"
  drills+=("{\"id\":\"$id\",\"status\":\"PASS\",\"targetDatabase\":\"$target\",\"detail\":\"$detail\"}")
  echo "PASS $id — $detail"
}

# DR-01: PostgreSQL custom-format full logical backup and restore.
pg_dump --format=custom --file "$tmp/full.custom" "$MDGSM_TEST_DB"
db="$(new_target dr01)"
pg_restore --exit-on-error --dbname "$db" "$tmp/full.custom"
[[ "$(snapshot "$db")" == "$source_snapshot" ]]
record DR-01 "$db" "custom-format full database restore; schema/data snapshot matches"

# DR-02: Plain SQL full backup and restore.
pg_dump --format=plain --file "$tmp/full.sql" "$MDGSM_TEST_DB"
db="$(new_target dr02)"
psql -d "$db" -v ON_ERROR_STOP=1 -f "$tmp/full.sql" >/dev/null
[[ "$(snapshot "$db")" == "$source_snapshot" ]]
record DR-02 "$db" "plain SQL full database restore; schema/data snapshot matches"

# DR-03: Directory archive with parallel dump and restore.
pg_dump --format=directory --jobs=2 --file "$tmp/full.directory" "$MDGSM_TEST_DB"
db="$(new_target dr03)"
pg_restore --jobs=2 --exit-on-error --dbname "$db" "$tmp/full.directory"
[[ "$(snapshot "$db")" == "$source_snapshot" ]]
record DR-03 "$db" "directory archive parallel restore; schema/data snapshot matches"

# DR-04: Schema-only restore creates the same IdentityAccess table set without row data.
pg_dump --schema-only --format=custom --file "$tmp/schema.custom" "$MDGSM_TEST_DB"
db="$(new_target dr04)"
pg_restore --exit-on-error --dbname "$db" "$tmp/schema.custom"
schema_tables="$(psql -d "$db" -At -v ON_ERROR_STOP=1 -c "SELECT count(*) FROM information_schema.tables WHERE table_schema='identity' AND table_type='BASE TABLE';")"
settings_rows="$(psql -d "$db" -At -v ON_ERROR_STOP=1 -c 'SELECT count(*) FROM identity.system_settings;')"
[[ "$schema_tables" == "11" && "$settings_rows" == "0" ]]
record DR-04 "$db" "schema-only restore; 11 IdentityAccess tables present and data absent"

# DR-05: Data-only restore into a schema-only target.
pg_dump --data-only --format=custom --file "$tmp/data.custom" "$MDGSM_TEST_DB"
db="$(new_target dr05)"
pg_restore --exit-on-error --dbname "$db" "$tmp/schema.custom"
pg_restore --data-only --exit-on-error --dbname "$db" "$tmp/data.custom"
[[ "$(snapshot "$db")" == "$source_snapshot" ]]
record DR-05 "$db" "data-only restore after schema restore; schema/data snapshot matches"

# DR-06: Selective restore of the session policy table into a schema-only target.
db="$(new_target dr06)"
pg_restore --exit-on-error --dbname "$db" "$tmp/schema.custom"
pg_restore --exit-on-error --dbname "$db" --table='identity.system_settings' "$tmp/data.custom"
settings="$(psql -d "$db" -At -v ON_ERROR_STOP=1 -c "SELECT max_active_users::text || '|' || heartbeat_interval_seconds::text || '|' || session_lease_seconds::text FROM identity.system_settings WHERE settings_id=1;")"
[[ "$settings" == "5|30|180" ]]
record DR-06 "$db" "selective system settings restore; session policy values preserved"

# DR-07: Restore without source ownership/ACL dependencies.
db="$(new_target dr07)"
pg_restore --no-owner --no-privileges --exit-on-error --dbname "$db" "$tmp/full.custom"
[[ "$(snapshot "$db")" == "$source_snapshot" ]]
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

script_hash="$(sha256sum "$0" | awk '{print $1}')"
dump_hash="$(sha256sum "$tmp/full.custom" | awk '{print $1}')"
joined="$(IFS=,; echo "${drills[*]}")"
printf '{\n  "evidenceVersion": "1.0",\n  "evidenceId": "EVD-GATED-RESTORE-POC",\n  "repository": "Md-gsm/MD-Gsm1",\n  "commit": "%s",\n  "workflowRunId": "%s",\n  "workflowRunAttempt": "%s",\n  "postgresServerVersion": "%s",\n  "sourceDatabase": "%s",\n  "sourceDatabaseMarker": "MDGSM_DISPOSABLE_TEST_DB",\n  "sourceSnapshot": "%s",\n  "restoreScriptSha256": "%s",\n  "fullCustomDumpSha256": "%s",\n  "scope": "PostgreSQL logical restore PoC against current IdentityAccess/infrastructure schema only; not full Gate D recovery.",\n  "drills": [\n    %s\n  ]\n}\n' "$GITHUB_SHA" "$GITHUB_RUN_ID" "$GITHUB_RUN_ATTEMPT" "$source_version" "$MDGSM_TEST_DB" "$source_snapshot" "$script_hash" "$dump_hash" "$joined" > artifacts/restore-poc-evidence.json

echo "PASS: all eight PostgreSQL logical restore PoCs completed on PostgreSQL $source_version."
