#!/usr/bin/env bash
set -euo pipefail

: "${MDGSM_TEST_DB:?MDGSM_TEST_DB must point to the marked disposable PostgreSQL database}"
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
db="$MDGSM_TEST_DB"
psql -d "$db" -v ON_ERROR_STOP=1 -f "$root/tests/MDGsm.Domain.Tests/Accounting/PostgreSQL/ap03-ap04-proof.sql"

psql -d "$db" -v ON_ERROR_STOP=1 <<'SQL'
BEGIN;
INSERT INTO ap_proof.journals(journal_id, status, source_reference)
VALUES ('00000000-0000-7000-8000-000000000001', 'draft', 'AP03-VALID');
INSERT INTO ap_proof.journal_lines(line_id, journal_id, debit_irr, credit_irr) VALUES
('00000000-0000-7000-8000-000000000011', '00000000-0000-7000-8000-000000000001', 100, 0),
('00000000-0000-7000-8000-000000000012', '00000000-0000-7000-8000-000000000001', 0, 100);
COMMIT;
UPDATE ap_proof.journals SET status = 'finalized' WHERE journal_id = '00000000-0000-7000-8000-000000000001';
INSERT INTO ap_proof.audit_events(event_id, event_type) VALUES ('audit-1', 'journal.finalized');
INSERT INTO ap_proof.period_transition_events(transition_id, transition_type)
VALUES ('00000000-0000-7000-8000-000000000021', 'period.closed');
INSERT INTO ap_proof.coa_snapshots(snapshot_id, status)
VALUES ('00000000-0000-7000-8000-000000000031', 'draft');
UPDATE ap_proof.coa_snapshots SET status = 'accepted'
WHERE snapshot_id = '00000000-0000-7000-8000-000000000031';
SQL

expect_failure() {
    local label="$1"
    local statement="$2"
    if psql -d "$db" -v ON_ERROR_STOP=1 -c "$statement"; then
        echo "FAIL: $label unexpectedly succeeded" >&2
        exit 1
    else
        echo "PASS (expected rejection): $label"
    fi
}

expect_failure "AP03 one-line journal rejected at transaction end" \
"BEGIN;
 INSERT INTO ap_proof.journals VALUES ('00000000-0000-7000-8000-000000000002', 'draft', 'AP03-ONE-LINE');
 INSERT INTO ap_proof.journal_lines VALUES ('00000000-0000-7000-8000-000000000013', '00000000-0000-7000-8000-000000000002', 1, 0);
 COMMIT;"

expect_failure "AP03 unbalanced journal rejected at transaction end" \
"BEGIN;
 INSERT INTO ap_proof.journals VALUES ('00000000-0000-7000-8000-000000000003', 'draft', 'AP03-UNBALANCED');
 INSERT INTO ap_proof.journal_lines VALUES
 ('00000000-0000-7000-8000-000000000014', '00000000-0000-7000-8000-000000000003', 100, 0),
 ('00000000-0000-7000-8000-000000000015', '00000000-0000-7000-8000-000000000003', 0, 99);
 COMMIT;"

expect_failure "AP03 bigint total overflow rejected at transaction end" \
"BEGIN;
 INSERT INTO ap_proof.journals VALUES ('00000000-0000-7000-8000-000000000004', 'draft', 'AP03-OVERFLOW');
 INSERT INTO ap_proof.journal_lines VALUES
 ('00000000-0000-7000-8000-000000000016', '00000000-0000-7000-8000-000000000004', 5000000000000000000, 0),
 ('00000000-0000-7000-8000-000000000017', '00000000-0000-7000-8000-000000000004', 5000000000000000000, 0),
 ('00000000-0000-7000-8000-000000000018', '00000000-0000-7000-8000-000000000004', 0, 5000000000000000000),
 ('00000000-0000-7000-8000-000000000019', '00000000-0000-7000-8000-000000000004', 0, 5000000000000000000);
 COMMIT;"

expect_failure "AP04 finalized journal header update blocked" \
"UPDATE ap_proof.journals SET source_reference = 'tampered' WHERE journal_id = '00000000-0000-7000-8000-000000000001';"
expect_failure "AP04 finalized journal delete blocked" \
"DELETE FROM ap_proof.journals WHERE journal_id = '00000000-0000-7000-8000-000000000001';"
expect_failure "AP04 insert line into finalized journal blocked" \
"INSERT INTO ap_proof.journal_lines VALUES ('00000000-0000-7000-8000-000000000022', '00000000-0000-7000-8000-000000000001', 1, 0);"
expect_failure "AP04 update finalized journal line blocked" \
"UPDATE ap_proof.journal_lines SET debit_irr = 101 WHERE line_id = '00000000-0000-7000-8000-000000000011';"
expect_failure "AP04 delete finalized journal line blocked" \
"DELETE FROM ap_proof.journal_lines WHERE line_id = '00000000-0000-7000-8000-000000000011';"
expect_failure "AP04 truncate journals containing finalized document blocked" \
"TRUNCATE ap_proof.journals;"
expect_failure "AP04 truncate lines belonging to finalized document blocked" \
"TRUNCATE ap_proof.journal_lines;"

expect_failure "AP04 audit update blocked" \
"UPDATE ap_proof.audit_events SET event_type = 'tampered' WHERE event_id = 'audit-1';"
expect_failure "AP04 audit delete blocked" \
"DELETE FROM ap_proof.audit_events WHERE event_id = 'audit-1';"
expect_failure "AP04 audit truncate blocked" \
"TRUNCATE ap_proof.audit_events;"
expect_failure "AP04 period transition update blocked" \
"UPDATE ap_proof.period_transition_events SET transition_type = 'tampered' WHERE transition_id = '00000000-0000-7000-8000-000000000021';"
expect_failure "AP04 period transition delete blocked" \
"DELETE FROM ap_proof.period_transition_events WHERE transition_id = '00000000-0000-7000-8000-000000000021';"
expect_failure "AP04 period transition truncate blocked" \
"TRUNCATE ap_proof.period_transition_events;"
expect_failure "AP04 accepted COA snapshot update blocked" \
"UPDATE ap_proof.coa_snapshots SET status = 'retired' WHERE snapshot_id = '00000000-0000-7000-8000-000000000031';"
expect_failure "AP04 accepted COA snapshot delete blocked" \
"DELETE FROM ap_proof.coa_snapshots WHERE snapshot_id = '00000000-0000-7000-8000-000000000031';"
expect_failure "AP04 accepted COA snapshot truncate blocked" \
"TRUNCATE ap_proof.coa_snapshots;"

psql -d "$db" -v ON_ERROR_STOP=1 <<'SQL'
DO $$
BEGIN
    IF NOT has_table_privilege('mdgsm_ap_proof_runtime', 'ap_proof.audit_events', 'INSERT')
       OR has_table_privilege('mdgsm_ap_proof_runtime', 'ap_proof.audit_events', 'UPDATE')
       OR has_table_privilege('mdgsm_ap_proof_runtime', 'ap_proof.audit_events', 'DELETE')
       OR has_table_privilege('mdgsm_ap_proof_runtime', 'ap_proof.period_transition_events', 'UPDATE')
       OR has_table_privilege('mdgsm_ap_proof_runtime', 'ap_proof.coa_snapshots', 'INSERT')
       OR has_schema_privilege('mdgsm_ap_proof_runtime', 'ap_proof', 'CREATE') THEN
        RAISE EXCEPTION 'AP04 least-privilege grant matrix check failed';
    END IF;
END $$;
SQL

expect_failure "AP04 runtime role cannot update append-only audit" \
"SET ROLE mdgsm_ap_proof_runtime; UPDATE ap_proof.audit_events SET event_type = 'runtime-tamper' WHERE event_id = 'audit-1';"
expect_failure "AP04 runtime role cannot update accepted COA snapshot" \
"SET ROLE mdgsm_ap_proof_runtime; UPDATE ap_proof.coa_snapshots SET status = 'retired' WHERE snapshot_id = '00000000-0000-7000-8000-000000000031';"
expect_failure "AP04 runtime role cannot update finalized journal despite table UPDATE grant" \
"SET ROLE mdgsm_ap_proof_runtime; UPDATE ap_proof.journals SET source_reference = 'runtime-tamper' WHERE journal_id = '00000000-0000-7000-8000-000000000001';"

echo "PASS: isolated PostgreSQL AP-03 deferred-balance and AP-04 immutability/least-privilege proof completed."
