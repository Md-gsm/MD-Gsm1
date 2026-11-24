# AuditSecurity module boundary

## Ownership

AuditSecurity is the sole owner of the system-wide canonical AuditEvent/SecurityEvent ledger, stream sequence, predecessor/hash-chain state, signed checkpoint envelope, independent verification result and durable canonical acknowledgement.

Source modules remain owners of their business facts and local transactional Outbox. A source module must commit its immutable audit intent in the same database transaction as its own fact. Delivery to AuditSecurity is at-least-once and idempotent; it is not a cross-database transaction. AuditSecurity must not read or mutate source-module business tables.

Platform owns provider selection and operations for non-exportable signing keys, trusted public-key history, independent immutable checkpoint storage, custody separation, rotation/revocation, recovery and alert routing.

## Data ownership

AuditSecurity-owned data is limited to canonical event envelopes and per-stream ordering/hash fields, stream-head state, checkpoint references, and trusted public-key identifiers/verification history. Private signing keys never belong in this module or PostgreSQL.

The initial metadata policy is fail-closed: the supplied policy accepts empty metadata only. Event-specific allow-lists require review before any additional metadata is accepted. Password material, credentials, bearer tokens, OTPs, recovery codes, connection strings, private keys and unnecessary raw PII are prohibited.

## Dependencies and contracts

- Domain is independent of Infrastructure, Host and other business modules.
- Application depends only on AuditSecurity Domain and its published Contracts.
- Infrastructure adapters implement transactional append/read and Platform signing/anchor ports.
- Source modules may depend only on AuditSecurity Contracts when their own Outbox adapters are authorized.
- AuditSecurity does not own accounts, roles, permissions, sessions, Organization assignments, accounting documents or business data.

## Implementation status and activation boundary

The Domain canonicalization/hash/verifier and Application append/checkpoint orchestration core are provider-neutral code. They are not yet a running service: no PostgreSQL adapter/schema/migration, source Outbox adapters, worker, HSM/KMS signer, external Object Lock anchor, API route, scheduler or production registration is included.

No SQL, DDL, migration number, PostgreSQL principal, credential, grant, key, provider resource or production activation is created or authorized. The database append adapter must enforce one PostgreSQL transaction, stream-head FOR UPDATE, database-owned clock_timestamp(), global unique EventId and (AuditStreamId, StreamSequence), append-only event rows and atomic head advancement.

Independent exact-SHA Security/Architecture review remains required before any production activation.
