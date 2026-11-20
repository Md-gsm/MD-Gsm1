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

write_evidence() {
  local exit_code="$1"
  local overall="PASS"
  [[ "$exit_code" -eq 0 ]] || overall="FAIL"
  local script_hash=""
  local dump_hash=""
  [[ -f "$0" ]] && 