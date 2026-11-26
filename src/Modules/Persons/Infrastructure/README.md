# Persons Infrastructure

Owns PostgreSQL repositories, merge coordinator, temporary-workflow persistence/retention, DPAPI payload-key provider and migration resources. It depends only on Persons Application, Domain and Contracts. IdentityAccess-specific adapters remain outside this assembly.


## Persons source Outbox and delivery boundary

- Every newly inserted Party writes one immutable, PII-free `PartyRegistered` envelope and its initial `pending` delivery row in the same PostgreSQL transaction as the Party, contacts, address, and operation idempotency record. Direct registration, quick registration, and temporary-workflow promotion use this single repository-owned insertion path.
- Existing-party selection and OperationId replay do not create another event. `PartyMerged` is not emitted.
- Delivery state is separate from the event envelope. The PostgreSQL store leases due rows with `FOR UPDATE SKIP LOCKED`, reclaims expired leases without changing EventId, and fences acknowledgement/retry writes by current lease owner and lease expiry.
- Delivery is at-least-once. A publisher that succeeds but loses its acknowledgement can cause the same EventId to be published again; consumers must deduplicate by EventId. Retries retain the immutable event identity and use a closed, PII-free failure code with bounded exponential delay.
- The worker is deliberately **not registered** in `AddPersonsInfrastructure` or ApiHost. No concrete publisher/destination, broker URL, credentials, worker login, database grants, or runtime activation are included. Current O-20 security policy leaves the Outbox objects unavailable to the runtime application identity; any future delivery activation requires a separate Owner/security decision, an approved runtime identity and publisher adapter, fresh exact-head privilege inventory, and the required CI/review gates.
- Disposable PostgreSQL tests verify standard Party creation atomicity, rollback/retry on Outbox insertion failure, immutable event identity across retry/lease recovery, stale-worker fencing, and absence of PUBLIC/runtime application table privileges. They do not execute production migrations or enable delivery.
