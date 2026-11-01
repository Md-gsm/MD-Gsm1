# ADR-0010 — Proposed amendments to ADR-0005

- Status: Proposed — requires independent Group of Software Advisers review and explicit Project Owner approval.
- ADR-0005 remains unchanged and Accepted.
- This proposal does not authorize G1 or runtime implementation.

## Proposed amendment A — independent module DbContexts

Each module owns its own DbContext and mappings. A module DbContext must not contain mappings for another module's tables. A single global DbContext aggregating all modules is prohibited. Atomic cross-module use cases may enlist independent module DbContexts in the same already-open DbConnection and DbTransaction. Transaction coordination does not transfer data ownership.

Required proof: architecture tests for context ownership and PostgreSQL integration tests for shared transaction enlistment and full rollback after injected participant failure.

## Proposed amendment B — V1 idempotency without payload comparison

V1 stores only an actor/client-scoped tuple of operation name and client-generated OperationId, plus operation status and result reference. It does not persist, hash, HMAC, log, or compare request payloads for idempotency. A repeated key in the same actor/client and operation scope returns the original stored result and performs no new write. A different user intent must use a fresh OperationId. Result retrieval remains authenticated and authorized; OperationId is not a bearer credential.

**Client contract (required):** a UI/client creates a new OperationId for every new user intent, including every actual edit/change to submitted data. It retains the same OperationId only while retrying the same intent after timeout, connection loss, or an ambiguous response. It must never reuse the previous OperationId for an edited payload: doing so silently returns the first result and does not apply the edit. A successful edit therefore requires a newly generated OperationId.

This avoids requiring a server-side HMAC key manager for Windows on-prem V1. Reusing an OperationId with changed payload returns the first result rather than detecting payload mismatch; the client contract and tests must make this behavior explicit. Any future payload equality requirement needs a separate approved ADR for key provisioning, protected storage, rotation, backup/restore and recovery.

## Required evidence before acceptance

- Independent DbContext ownership and forbidden global DbContext tests.
- Shared transaction rollback integration test.
- Idempotency tests for same key/same actor returning the same result, cross-actor isolation, changed-payload reuse returning the original result without a second write, and fresh key creating a distinct intent.
- Client contract tests: retrying an unchanged intent reuses its OperationId; editing any submitted field generates a fresh OperationId; a simulated changed-payload request with a reused key returns the first result and the client does not present it as the edit being applied.
- No request payload, raw mobile, digest or MAC in DB idempotency records, logs, traces or CI artifacts.

## Status guard

Proposed only. ADR-0005 remains unchanged and Accepted. ADR-0010 requires Group of Software Advisers review and Project Owner approval. g1Authorized=false; Persons=NOT_IMPLEMENTED.
