# Security model — design baseline, not yet implemented authentication

## Trust boundaries

1. Desktop is an untrusted client. Hiding a menu or disabling a button is not authorization.
2. API is the enforcement boundary. Every protected operation must authenticate the caller and authorize the requested action on the server.
3. Domain/Application code must not trust user IDs, roles, totals, account IDs, or permission claims supplied by the client without server-side validation.
4. Infrastructure owns external credential and persistence integrations; secrets must not be embedded in the executable or repository.

## Planned identity and access controls

- Define named users, enabled/disabled status, and explicit role/permission assignments before implementing login.
- Use a maintained authentication framework and established password hashing; never implement custom cryptography or store plaintext/reversible passwords.
- Apply least privilege and deny by default. Check authorization on every protected API operation.
- Keep authentication secrets out of logs and error messages. Use TLS for network deployments; do not expose the API to a network until transport and host access are configured.
- Session/token lifetime, revocation, password reset, lockout/rate limiting, audit events, and initial administrator provisioning require explicit design and tests before production use.
- Never rely on client-side UI state as a security control.

## Phase 1 status

Authentication, authorization middleware, users, roles, password storage, token issuance, and protected endpoints are **not implemented**. The liveness endpoint is intentionally public and reveals no operational detail beyond process liveness. This document records constraints for later implementation; it is not a security certification.
