# IdentityAccess Privacy Digest Key Operations

The IdentityAccess privacy digest is HMAC-SHA256. It is deliberately separate from the opaque bearer-token digest: random 256-bit bearer tokens continue to use plain SHA-256, while login identifiers and source identifiers use a keyed HMAC.

## Required runtime secret

The service requires the configuration key `IdentityAccess:PrivacyHmacKey`. Supply it through the deployment secret store or the environment variable `IdentityAccess__PrivacyHmacKey`. The value is Base64 encoding of at least 32 cryptographically random bytes.

Generate a value in PowerShell without writing it into the repository:

```powershell
$bytes = [Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
[Convert]::ToBase64String($bytes)
```

Store the output in the host's protected secret store. Do not place it in appsettings files, source control, installer definitions, migration scripts, logs, telemetry, or support bundles. Do not reuse a password, token-signing key, database password, or another application's secret.

Startup fails closed if the key is missing, malformed, or shorter than 32 bytes. There is no development or production fallback. Automated integration tests inject a deterministic test-only key into in-memory configuration; it is not a deployable secret.

## Rotation and data compatibility

HMAC output is deterministic only for the same input and key. Changing the key changes all privacy digests and therefore affects lookup, throttling, and correlation of previously stored digests. Do not rotate this key in place until a separately reviewed versioned dual-key/rehash migration is implemented and approved. The current implementation intentionally has no implicit key rotation or fallback behavior.

## Initial account bootstrap boundary

No fixed initial username/password pair is shipped. No production bootstrap credential, login endpoint, authentication middleware, or network listener is activated by this change. A future first-run bootstrap must be a one-time local-only ceremony that accepts a user-supplied secret, stores only a password hash, and keeps network access fail-closed until the credential and recovery profile are completed. The separately approved emergency recovery path for the global administrator remains distinct from ordinary administrator password reset.
