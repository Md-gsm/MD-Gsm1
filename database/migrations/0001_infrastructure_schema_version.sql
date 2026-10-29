-- MD-Gsm Stage 7 disposable smoke-test migration.
-- Creates only infrastructure metadata; no business/accounting tables.
BEGIN;
CREATE TABLE IF NOT EXISTS mdgsm_schema_version (
    version integer PRIMARY KEY,
    migration_name varchar(160) NOT NULL,
    applied_at_utc timestamptz NOT NULL DEFAULT now()
);
INSERT INTO mdgsm_schema_version (version, migration_name)
VALUES (1, '0001_infrastructure_schema_version')
ON CONFLICT (version) DO NOTHING;
COMMIT;
