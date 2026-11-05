-- Remove the unused optimistic-concurrency marker before Finance account provisioning is activated.
-- Migration 0004 is immutable; this forward-only migration preserves the migration history contract.
ALTER TABLE finance_party_accounts DROP COLUMN row_version;
