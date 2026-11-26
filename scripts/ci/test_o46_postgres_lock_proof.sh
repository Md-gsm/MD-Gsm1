#!/usr/bin/env bash
# O-46 PostgreSQL 18.6 adversarial proof.
# All objects/roles exist only in a uniquely marked disposable CI database.
# This proves PostgreSQL lock/snapshot/ACL mechanics and a test-only SECURITY DEFINER
# pattern. It does not prove production routine bodies, deployment grants, or a real Reader.
set -Eeuo pipefail

: "${PGHOST:?PGHOST must point to the disposable PostgreSQL service}"
: "${PGPORT:=5432}"
: "${PGUSER:=postgres}"
: "${GITHUB_RUN_ID:=1}"
: "${GITHUB_RUN_ATTEMPT:=1}"

DB="mdgsm_o46_lock_disposable_${GITHUB_RUN_ID}_${GITHUB_RUN_ATTEMPT}"
[[ "$DB" =~ ^mdgsm_o46_lock_disposable_[0-9]+_[0-9]+$ ]] || { echo "FAIL: disposable DB name rejected" >&2; exit 1; }

TMP="$(mktemp -d)"
SUMMARY="${RUNNER_TEMP:-/tmp}/o46-lock-proof-summary.txt"
mkdir -p "$(dirname "$SUMMARY")"
: > "$SUMMARY"
ADMIN=(psql -X -v ON_ERROR_STOP=1 -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d postgres)

cleanup() {
  set +e
  local pids
  pids="$(PGAPPNAME=o46-cleanup "${ADMIN[@]}" -Atqc \
    "SELECT pid FROM pg_stat_activity WHERE datname = '$DB' AND pid <> pg_backend_pid();")"
  if [[ -n "$pids" ]]; then
    while IFS= read -r pid; do
      [[ "$pid" =~ ^[0-9]+$ ]] && "${ADMIN[@]}" -c "SELECT pg_terminate_backend($pid);" >/dev/null 2>&1
    done <<< "$pids"
  fi
  "${ADMIN[@]}" -c "DROP DATABASE IF EXISTS \"$DB\";" >/dev/null 2>&1
  "${ADMIN[@]}" -c "DROP ROLE IF EXISTS o46_test_executor, o46_test_fn_owner, o46_test_reader;" >/dev/null 2>&1
  rm -rf "$TMP"
}
trap cleanup EXIT

"${ADMIN[@]}" -c "CREATE DATABASE \"$DB\";" >/dev/null
"${ADMIN[@]}" -c "COMMENT ON DATABASE \"$DB\" IS 'MDGSM_DISPOSABLE_TEST_DB; O-46 PostgreSQL 18.6 adversarial proof only';" >/dev/null
export PGDATABASE="$DB"
PSQL=(psql -X -v ON_ERROR_STOP=1 -h "$PGHOST" -p "$PGPORT" -U "$PGUSER" -d "$DB")

fail() { echo "FAIL: $*" >&2; echo "FAIL: $*" >> "$SUMMARY"; exit 1; }
pass() { echo "PASS: $*"; echo "PASS: $*" >> "$SUMMARY"; }
query() { "${PSQL[@]}" -Atq -c "$1"; }
assert_eq() {
  local actual="$1" expected="$2" label="$3"
  [[ "$actual" == "$expected" ]] || fail "$label (expected '$expected'; got '$actual')"
  pass "$label"
}
key_for() {
  query "SELECT pg_catalog.hashtextextended(
    'MDGSM:IDENTITYACCESS:O46:OWNER-BOOTSTRAP:V1:' ||
    pg_catalog.lower('$1'::uuid::text), 0::bigint);"
}
start_holder() {
  local app="$1" key="$2" mode="$3" seconds="$4"
  if [[ "$mode" == "exclusive" ]]; then
    PGAPPNAME="$app" "${PSQL[@]}" -q >"$TMP/$app.log" 2>&1 <<SQL &
BEGIN;
SELECT pg_catalog.pg_advisory_xact_lock($key);
SELECT pg_catalog.pg_sleep($seconds);
COMMIT;
SQL
  else
    PGAPPNAME="$app" "${PSQL[@]}" -q >"$TMP/$app.log" 2>&1 <<SQL &
BEGIN;
SELECT pg_catalog.pg_advisory_xact_lock_shared($key);
SELECT pg_catalog.pg_sleep($seconds);
COMMIT;
SQL
  fi
  HOLDER_PID=$!
}
wait_for_lock() {
  local app="$1"
  for _ in $(seq 1 80); do
    local count
    count="$(PGAPPNAME=o46-probe "${ADMIN[@]}" -Atqc \
      "SELECT count(*) FROM pg_locks l JOIN pg_stat_activity a USING(pid)
       WHERE a.application_name = '$app' AND l.locktype = 'advisory' AND l.granted;")"
    [[ "$count" == "1" ]] && return 0
    sleep 0.1
  done
  fail "timed out waiting for advisory lock held by $app"
}
wait_for_waiter() {
  local app="$1"
  for _ in $(seq 1 80); do
    local count
    count="$(PGAPPNAME=o46-probe "${ADMIN[@]}" -Atqc \
      "SELECT count(*) FROM pg_stat_activity
       WHERE application_name = '$app' AND wait_event_type = 'Lock';")"
    [[ "$count" -ge 1 ]] && return 0
    sleep 0.1
  done
  fail "timed out waiting for $app to block on a lock"
}
try_exclusive() { query "SELECT pg_catalog.pg_try_advisory_xact_lock($1);"; }
try_shared() { query "SELECT pg_catalog.pg_try_advisory_xact_lock_shared($1);"; }

SERVER_NUM="$(PGAPPNAME=o46-version "${ADMIN[@]}" -Atqc "SHOW server_version_num;")"
assert_eq "$SERVER_NUM" "180006" "Disposable target is PostgreSQL 18.6"

INSTALLATION="018f0000-0000-7000-8000-000000000046"
OTHER_INSTALLATION="018f0000-0000-7000-8000-000000000047"
OP_A="018f0000-0000-7000-8000-0000000000a1"
OP_B="018f0000-0000-7000-8000-0000000000b2"
REQUEST_A="018f0000-0000-7000-8000-0000000000c3"
REQUEST_B="018f0000-0000-7000-8000-0000000000d4"
KEY="$(key_for "$INSTALLATION")"
KEY_REPEAT="$(key_for "$INSTALLATION")"
OTHER_KEY="$(key_for "$OTHER_INSTALLATION")"
assert_eq "$KEY_REPEAT" "$KEY" "Same InstallationId yields same server-derived 64-bit lock key"
[[ "$OTHER_KEY" != "$KEY" ]] || fail "independent test InstallationIds unexpectedly produced the same key"
pass "Different InstallationId test vector derives a separate lock key"

# Executor is intentionally not expected to receive direct table or pg_catalog privileges.
"${PSQL[@]}" -q -c "CREATE ROLE o46_test_executor NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT"

# Exclusive lock excludes both lock modes; independent InstallationIds proceed separately.
start_holder "o46-exclusive-holder" "$KEY" "exclusive" 3
wait_for_lock "o46-exclusive-holder"
assert_eq "$(try_exclusive "$KEY")" "f" "Exclusive lock excludes a second exclusive transaction"
assert_eq "$(try_shared "$KEY")" "f" "Exclusive lock excludes a shared reconciliation transaction"
assert_eq "$(try_exclusive "$OTHER_KEY")" "t" "Different InstallationId can proceed independently"
wait "$HOLDER_PID"
assert_eq "$(try_exclusive "$KEY")" "t" "COMMIT releases transaction-scoped exclusive lock"

# Shared locks coexist, while an exclusive writer is excluded.
start_holder "o46-shared-holder" "$KEY" "shared" 3
wait_for_lock "o46-shared-holder"
assert_eq "$(try_shared "$KEY")" "t" "Two shared reconciliation locks coexist"
assert_eq "$(try_exclusive "$KEY")" "f" "Shared reconciliation lock excludes exclusive writer"
wait "$HOLDER_PID"

"${PSQL[@]}" -q <<SQL
BEGIN;
SELECT pg_catalog.pg_advisory_xact_lock($KEY);
ROLLBACK;
SQL
assert_eq "$(try_exclusive "$KEY")" "t" "ROLLBACK releases transaction-scoped lock"

# Backend termination must release transaction-level locks.
start_holder "o46-terminated-holder" "$KEY" "exclusive" 60
wait_for_lock "o46-terminated-holder"
BACKEND_PID="$(PGAPPNAME=o46-probe "${ADMIN[@]}" -Atqc \
  "SELECT pid FROM pg_stat_activity WHERE application_name='o46-terminated-holder' LIMIT 1;")"
[[ "$BACKEND_PID" =~ ^[0-9]+$ ]] || fail "could not identify disposable holder backend"
TERMINATED="$(PGAPPNAME=o46-probe "${ADMIN[@]}" -Atqc "SELECT pg_terminate_backend($BACKEND_PID);")"
assert_eq "$TERMINATED" "t" "Test backend termination succeeded"
set +e
wait "$HOLDER_PID"
HOLDER_STATUS=$?
set -e
[[ "$HOLDER_STATUS" -ne 0 ]] || fail "terminated psql holder unexpectedly exited successfully"
assert_eq "$(try_exclusive "$KEY")" "t" "Backend termination releases transaction-scoped lock"

# All objects below are synthetic disposable proof objects, never application schema objects.
"${PSQL[@]}" -q <<'SQL'
CREATE SCHEMA o46_test;
CREATE TABLE o46_test.challenges (
  challenge_id uuid PRIMARY KEY,
  installation_id uuid NOT NULL,
  setup_operation_id uuid NOT NULL,
  purpose text NOT NULL,
  request_id uuid NOT NULL,
  fingerprint bytea NOT NULL CHECK (octet_length(fingerprint) = 32),
  expires_at timestamptz NOT NULL,
  revoked_at timestamptz,
  consumed_at timestamptz
);
CREATE UNIQUE INDEX ux_o46_test_one_active_per_operation
  ON o46_test.challenges(installation_id, setup_operation_id, purpose)
  WHERE revoked_at IS NULL AND consumed_at IS NULL;
CREATE TABLE o46_test.reconciliation_state (
  installation_id uuid NOT NULL,
  setup_operation_id uuid NOT NULL,
  account_id uuid NOT NULL,
  fingerprint_version smallint NOT NULL,
  fingerprint bytea NOT NULL,
  completed_at timestamptz NOT NULL,
  password_hash text NOT NULL,
  verifier_digest bytea NOT NULL
);
CREATE VIEW o46_test.reconciliation_view AS
  SELECT installation_id, setup_operation_id, account_id,
         fingerprint_version, fingerprint, completed_at
  FROM o46_test.reconciliation_state;

CREATE ROLE o46_test_fn_owner NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
CREATE ROLE o46_test_reader NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS NOINHERIT;
GRANT USAGE, CREATE ON SCHEMA o46_test TO o46_test_fn_owner;
GRANT USAGE ON SCHEMA o46_test TO o46_test_reader;
GRANT SELECT, INSERT ON o46_test.challenges TO o46_test_fn_owner;
GRANT SELECT ON o46_test.reconciliation_view TO o46_test_reader;

CREATE FUNCTION o46_test.issue_initial_owner_challenge_v1(
  p_installation_id uuid,
  p_setup_operation_id uuid,
  p_request_id uuid,
  p_fingerprint bytea
) RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, o46_test
AS $body$
DECLARE
  v_lock_key bigint;
  v_existing o46_test.challenges%ROWTYPE;
  v_challenge_id uuid;
BEGIN
  v_lock_key := pg_catalog.hashtextextended(
    'MDGSM:IDENTITYACCESS:O46:OWNER-BOOTSTRAP:V1:' ||
    pg_catalog.lower(p_installation_id::text), 0::bigint);
  PERFORM pg_catalog.pg_advisory_xact_lock(v_lock_key);

  SELECT * INTO v_existing
  FROM o46_test.challenges
  WHERE installation_id = p_installation_id
    AND setup_operation_id = p_setup_operation_id
    AND purpose = 'initial_product_owner'
    AND request_id = p_request_id;

  IF FOUND THEN
    IF v_existing.fingerprint = p_fingerprint
       AND v_existing.revoked_at IS NULL
       AND v_existing.consumed_at IS NULL
       AND v_existing.expires_at > pg_catalog.clock_timestamp() THEN
      RETURN v_existing.challenge_id;
    END IF;
    RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'challenge_request_id_conflict';
  END IF;

  IF EXISTS (
    SELECT 1 FROM o46_test.challenges
    WHERE installation_id = p_installation_id
      AND setup_operation_id <> p_setup_operation_id
      AND purpose = 'initial_product_owner'
      AND revoked_at IS NULL
      AND consumed_at IS NULL
      AND expires_at > pg_catalog.clock_timestamp()
  ) THEN
    RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'setup_operation_in_progress';
  END IF;

  INSERT INTO o46_test.challenges(
    challenge_id, installation_id, setup_operation_id, purpose,
    request_id, fingerprint, expires_at)
  VALUES (
    pg_catalog.gen_random_uuid(), p_installation_id, p_setup_operation_id,
    'initial_product_owner', p_request_id, p_fingerprint,
    pg_catalog.clock_timestamp() + interval '20 minutes')
  RETURNING challenge_id INTO v_challenge_id;
  RETURN v_challenge_id;
END;
$body$;

ALTER FUNCTION o46_test.issue_initial_owner_challenge_v1(uuid,uuid,uuid,bytea)
  OWNER TO o46_test_fn_owner;
REVOKE CREATE ON SCHEMA o46_test FROM o46_test_fn_owner;
REVOKE ALL ON FUNCTION o46_test.issue_initial_owner_challenge_v1(uuid,uuid,uuid,bytea) FROM PUBLIC;
GRANT USAGE ON SCHEMA o46_test TO o46_test_executor;
GRANT EXECUTE ON FUNCTION o46_test.issue_initial_owner_challenge_v1(uuid,uuid,uuid,bytea)
  TO o46_test_executor;
SQL

# Verify the effective built-in privileges on the actual SECURITY DEFINER owner and Reader roles.
# No system-function grant is added by this test; missing privilege is a hard failure/evidence.
OWNER_LOCK_PRIVS="$(query "SELECT
  has_function_privilege('o46_test_fn_owner','pg_catalog.hashtextextended(text,bigint)','EXECUTE')::text || '|' ||
  has_function_privilege('o46_test_fn_owner','pg_catalog.pg_advisory_xact_lock(bigint)','EXECUTE')::text;")"
assert_eq "$OWNER_LOCK_PRIVS" "true|true" "NOLOGIN SECURITY DEFINER owner can derive and acquire the exclusive lock"
READER_LOCK_PRIVS="$(query "SELECT
  has_function_privilege('o46_test_reader','pg_catalog.hashtextextended(text,bigint)','EXECUTE')::text || '|' ||
  has_function_privilege('o46_test_reader','pg_catalog.pg_advisory_xact_lock_shared(bigint)','EXECUTE')::text;")"
assert_eq "$READER_LOCK_PRIVS" "true|true" "View-only Reader can derive key and acquire the shared lock"

ACL_RESULT="$(query "SELECT
  has_function_privilege('o46_test_executor',
    'o46_test.issue_initial_owner_challenge_v1(uuid,uuid,uuid,bytea)', 'EXECUTE')::text || '|' ||
  has_table_privilege('o46_test_executor','o46_test.challenges','SELECT')::text || '|' ||
  has_table_privilege('o46_test_executor','o46_test.challenges','INSERT')::text || '|' ||
  has_table_privilege('o46_test_reader','o46_test.reconciliation_view','SELECT')::text || '|' ||
  has_table_privilege('o46_test_reader','o46_test.reconciliation_state','SELECT')::text || '|' ||
  has_function_privilege('o46_test_reader','pg_catalog.pg_advisory_xact_lock_shared(bigint)','EXECUTE')::text;")"
assert_eq "$ACL_RESULT" "true|false|false|true|false|true" "Synthetic Executor EXECUTE-only and Reader view-only ACL boundary"

FIRST_CHALLENGE="$(query "SET ROLE o46_test_executor; SELECT o46_test.issue_initial_owner_challenge_v1(
  '$INSTALLATION'::uuid, '$OP_A'::uuid, '$REQUEST_A'::uuid, decode(repeat('ab',32),'hex'));")"
SECOND_CHALLENGE="$(query "SET ROLE o46_test_executor; SELECT o46_test.issue_initial_owner_challenge_v1(
  '$INSTALLATION'::uuid, '$OP_A'::uuid, '$REQUEST_A'::uuid, decode(repeat('ab',32),'hex'));")"
assert_eq "$SECOND_CHALLENGE" "$FIRST_CHALLENGE" "Same operation/request/fingerprint retry returns the same challenge"

# A second SetupOperationId must block behind the first and then fail closed.
PGAPPNAME=o46-issue-a "${PSQL[@]}" -q >"$TMP/o46-issue-a.log" 2>&1 <<SQL &
BEGIN;
SET ROLE o46_test_executor;
SELECT o46_test.issue_initial_owner_challenge_v1(
  '$INSTALLATION'::uuid, '$OP_A'::uuid, '$REQUEST_A'::uuid, decode(repeat('ab',32),'hex'));
SELECT pg_catalog.pg_sleep(3);
COMMIT;
SQL
ISSUE_A_PID=$!
wait_for_lock "o46-issue-a"
PGAPPNAME=o46-issue-b "${PSQL[@]}" -q >"$TMP/o46-issue-b.log" 2>&1 <<SQL &
SET ROLE o46_test_executor;
SELECT o46_test.issue_initial_owner_challenge_v1(
  '$INSTALLATION'::uuid, '$OP_B'::uuid, '$REQUEST_B'::uuid, decode(repeat('cd',32),'hex'));
SQL
ISSUE_B_PID=$!
wait_for_waiter "o46-issue-b"
wait "$ISSUE_A_PID"
set +e
wait "$ISSUE_B_PID"
ISSUE_B_STATUS=$?
set -e
[[ "$ISSUE_B_STATUS" -ne 0 ]] || fail "second SetupOperationId unexpectedly opened a concurrent live challenge"
grep -Fq "setup_operation_in_progress" "$TMP/o46-issue-b.log" || fail "second SetupOperationId did not fail with setup_operation_in_progress"
ACTIVE_CHALLENGES="$(query "SELECT count(*) FROM o46_test.challenges
  WHERE installation_id='$INSTALLATION'::uuid AND revoked_at IS NULL
    AND consumed_at IS NULL AND expires_at > pg_catalog.clock_timestamp();")"
assert_eq "$ACTIVE_CHALLENGES" "1" "Concurrent distinct SetupOperationIds leave exactly one live challenge"
pass "Test-only SECURITY DEFINER issuer serializes cross-operation challenge issue"

# READ COMMITTED: a second SQL command sees a commit made while its shared lock request waited.
SNAPSHOT_OP="018f0000-0000-7000-8000-0000000000f6"
PGAPPNAME=o46-rc-writer "${PSQL[@]}" -q >"$TMP/o46-rc-writer.log" 2>&1 <<SQL &
BEGIN;
SELECT pg_catalog.pg_advisory_xact_lock($KEY);
INSERT INTO o46_test.reconciliation_state(
  installation_id, setup_operation_id, account_id, fingerprint_version,
  fingerprint, completed_at, password_hash, verifier_digest)
VALUES ('$INSTALLATION'::uuid, '$SNAPSHOT_OP'::uuid, pg_catalog.gen_random_uuid(),
        1, decode(repeat('ef',32),'hex'), pg_catalog.clock_timestamp(),
        'secret-sentinel-password-hash', decode(repeat('11',32),'hex'));
SELECT pg_catalog.pg_sleep(3);
COMMIT;
SQL
RC_WRITER_PID=$!
wait_for_lock "o46-rc-writer"
PGAPPNAME=o46-rc-reader "${PSQL[@]}" -Atq >"$TMP/o46-rc-reader.log" 2>&1 <<SQL &
BEGIN ISOLATION LEVEL READ COMMITTED;
SELECT pg_catalog.pg_advisory_xact_lock_shared($KEY);
SELECT count(*) FROM o46_test.reconciliation_view
 WHERE installation_id='$INSTALLATION'::uuid AND setup_operation_id='$SNAPSHOT_OP'::uuid;
COMMIT;
SQL
RC_READER_PID=$!
wait_for_waiter "o46-rc-reader"
wait "$RC_WRITER_PID"
wait "$RC_READER_PID"
RC_COUNT="$(grep -E '^[0-9]+$' "$TMP/o46-rc-reader.log" | tail -n 1)"
assert_eq "$RC_COUNT" "1" "READ COMMITTED later SELECT observes writer commit after lock wait"

# REPEATABLE READ retains the earlier transaction snapshot; this is deliberately rejected.
RR_OP="018f0000-0000-7000-8000-0000000000f7"
PGAPPNAME=o46-rr-writer "${PSQL[@]}" -q >"$TMP/o46-rr-writer.log" 2>&1 <<SQL &
BEGIN;
SELECT pg_catalog.pg_advisory_xact_lock($KEY);
INSERT INTO o46_test.reconciliation_state(
  installation_id, setup_operation_id, account_id, fingerprint_version,
  fingerprint, completed_at, password_hash, verifier_digest)
VALUES ('$INSTALLATION'::uuid, '$RR_OP'::uuid, pg_catalog.gen_random_uuid(),
        1, decode(repeat('22',32),'hex'), pg_catalog.clock_timestamp(),
        'secret-sentinel-password-hash', decode(repeat('33',32),'hex'));
SELECT pg_catalog.pg_sleep(3);
COMMIT;
SQL
RR_WRITER_PID=$!
wait_for_lock "o46-rr-writer"
PGAPPNAME=o46-rr-reader "${PSQL[@]}" -Atq >"$TMP/o46-rr-reader.log" 2>&1 <<SQL &
BEGIN ISOLATION LEVEL REPEATABLE READ;
SELECT count(*) FROM o46_test.reconciliation_view
 WHERE installation_id='$INSTALLATION'::uuid AND setup_operation_id='$RR_OP'::uuid;
SELECT pg_catalog.pg_advisory_xact_lock_shared($KEY);
SELECT count(*) FROM o46_test.reconciliation_view
 WHERE installation_id='$INSTALLATION'::uuid AND setup_operation_id='$RR_OP'::uuid;
COMMIT;
SQL
RR_READER_PID=$!
wait_for_waiter "o46-rr-reader"
wait "$RR_WRITER_PID"
wait "$RR_READER_PID"
mapfile -t RR_COUNTS < <(grep -E '^[0-9]+$' "$TMP/o46-rr-reader.log")
[[ "${#RR_COUNTS[@]}" -eq 2 ]] || fail "could not read both REPEATABLE READ snapshot counts"
assert_eq "${RR_COUNTS[0]}" "0" "REPEATABLE READ establishes the pre-commit snapshot"
assert_eq "${RR_COUNTS[1]}" "0" "REPEATABLE READ preserves stale snapshot after lock wait"

# Synthetic Reader actually acquires shared lock and reads only the sanitized View.
READER_RESULT="$(PGAPPNAME=o46-reader-boundary "${PSQL[@]}" -Atq <<SQL
BEGIN;
SET ROLE o46_test_reader;
SELECT pg_catalog.pg_advisory_xact_lock_shared($KEY);
SELECT count(*) FROM o46_test.reconciliation_view WHERE installation_id='$INSTALLATION'::uuid;
SELECT has_table_privilege(current_user,'o46_test.reconciliation_state','SELECT')::text;
COMMIT;
RESET ROLE;
SQL
)"
mapfile -t READER_LINES < <(printf '%s\n' "$READER_RESULT" | sed '/^[[:space:]]*$/d')
[[ "${#READER_LINES[@]}" -eq 2 ]] || fail "unexpected synthetic Reader evidence output"
assert_eq "${READER_LINES[0]}" "2" "View-only Reader obtains shared lock and sees only projected rows"
assert_eq "${READER_LINES[1]}" "false" "View-only Reader cannot SELECT the underlying base table"

pass "O-46 PostgreSQL 18.6 adversarial lock/snapshot/ACL matrix completed"
