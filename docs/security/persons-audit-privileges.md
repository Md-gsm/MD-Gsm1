# Persons access-audit deployment privileges

Each access event commits on a separate PostgreSQL connection and transaction. Audit failure aborts the read response (fail closed). The audit table intentionally has no foreign key to the business row so evidence survives caller rollback and business-row retention.

The migration revokes UPDATE, DELETE and TRUNCATE from PUBLIC and keeps database rules that discard UPDATE/DELETE. This is defense-in-depth, not protection against the table owner, superuser, BYPASSRLS, or an operator able to alter schema objects.

Before production, provision distinct non-login schema-owner and runtime roles. The runtime role must not own the audit table and must receive only INSERT and the minimum SELECT required for audit verification; it must not receive UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER, CREATE, or membership in the owner role. The owner credential must never be present in the application. Superuser/owner activity remains an explicitly monitored administrative trust boundary. Validate effective grants from the actual deployment role during production-topology rehearsal; this code change alone does not prove production grants.
