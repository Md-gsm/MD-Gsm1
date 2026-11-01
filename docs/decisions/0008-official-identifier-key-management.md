# ADR-0008 — Key management gate for official identifiers

- Status: **Proposed — explicit security/privacy owner decision required.**
- Official identifiers are excluded from V1. No runtime field, table, API, log, backup export or import path may persist them until the project owner, who is also security/privacy authority, approves the key-management design.

The design must specify envelope encryption with purpose-scoped data keys; master/key-encryption key held outside DB and source; least-privilege access; rotation/re-encryption/revocation and compromise response; backup/restore proving ciphertext recoverability without plaintext keys beside backups; audit of decrypt/read operations; masking and prohibition of plaintext in logs/telemetry/exports/CI; executable tests for unauthorized decrypt, missing key, rotation, restore and redaction.

No appsettings secret, committed environment secret or DB-stored master key is acceptable. Until owner approval and test evidence, official identifier persistence is forbidden. Adviser review cannot replace owner approval.
