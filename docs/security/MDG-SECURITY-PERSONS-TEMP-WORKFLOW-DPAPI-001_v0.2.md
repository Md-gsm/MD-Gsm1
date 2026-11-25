# Persons Temporary Workflow Payload Key Custody
**Document ID:** MDG-SECURITY-PERSONS-TEMP-WORKFLOW-DPAPI-001  
**Version:** 0.2  
**Status:** Owner-approved deployment identity and installer design; operational verification pending  
**Approval:** Product Owner approval recorded in Issue #100 on 2026-11-25

## 1. Scope and purpose
This decision applies only to the AES-256-GCM encryption key used for sensitive temporary Persons workflow payloads before promotion. It does not define custody for IdentityAccess credentials, owner recovery, AuditSecurity/Hash Chain signing, database encryption, or any other secret.

## 2. Approved custody model

- Windows API service name: `MDGsmApi`.
- Windows service logon account: `NT AUTHORITY\LocalService`.
- Service-specific SID: `NT SERVICE\MDGsmApi`, with SCM service SID type set to `unrestricted` so the SID is present in the service token.
- The installer runs elevated, creates/configures the service first, enables its service SID, resolves that SID explicitly, and provisions the DPAPI key for that SID. It never grants runtime key access to the setup user's SID.
- Use Windows DPAPI with `DataProtectionScope.LocalMachine`.
- Generate one independent cryptographically random 256-bit key per installation using the Windows cryptographic random-number generator.
- Protect the key with DPAPI before persistence. Persist only the DPAPI-protected blob and a non-secret format header; never persist raw key bytes in PostgreSQL, application configuration, source control, logs, installer assets, or ordinary backup manifests.
- Store the protected key below `%ProgramData%\MD-Gsm\Keys\Persons\MDGsmApi\`. This is data/key storage only; no application executables, DLLs, or installer files are placed there.
- Apply protected Windows DACLs. The directory grants Read/Execute only to `NT SERVICE\MDGsmApi` and Full Control only to LocalSystem. The key file grants Read only to `NT SERVICE\MDGsmApi` and Full Control only to LocalSystem. No interactive user, Administrators group, LocalService group, Authenticated Users, or Everyone entry is added.
- Provisioning is explicit and one-time. Runtime key lookup never creates, replaces, or silently rotates a missing key. Repair skips provisioning when the file exists and verifies its ACL; an existing key with invalid ACL or unreadable contents fails closed rather than being rewritten.
- The key purpose is isolated from all other application keys. Owner recovery codes, email OTPs, IdentityAccess secrets, and AuditSecurity signing keys must never be reused.

## 3. Cryptographic contract
- Algorithm: AES-256-GCM for payload protection.
- Key size: exactly 32 bytes.
- DPAPI scope: LocalMachine.
- DPAPI optional entropy: fixed, purpose-specific application context string; this is domain separation, not a substitute for key secrecy.
- Key version: `dpapi-localmachine-v1`.
- Ciphertext format remains nonce (12 bytes) || authentication tag (16 bytes) || ciphertext.
- Associated data remains bound to the workflow record identifier and key version.
- Decrypted key buffers are caller-owned byte arrays and must be zeroed after use. Plaintext buffers must be zeroed after parsing/use, including failure paths.

## 4. Installation identity and ACL boundary
The approved API Windows Service runs as `NT AUTHORITY\LocalService` with service name `MDGsmApi` and SCM service SID type `unrestricted`. The effective runtime key reader is the service-specific SID `NT SERVICE\MDGsmApi`, not the shared LocalService account. The elevated installer creates/configures the service, enables its service SID, resolves the SID from Windows, and passes that SID explicitly to the provisioning operation. Setup's interactive/admin SID is not included in the final key or directory DACL. The API validates the key at process startup under the actual service token and refuses to start when key access or DPAPI unprotection fails.

DPAPI LocalMachine protects the key blob at rest from ordinary offline file disclosure, but it does not protect against a local administrator, SYSTEM-level compromise, or malware executing as the API identity. This is an explicit trust boundary, not a claim of administrator-proof isolation.

## 5. Lifecycle, rotation, backup and loss
- No key rotation policy may be inferred from this decision. A versioned rotation procedure must preserve decryptability of every still-valid workflow before retiring an old key.
- Retain a prior key only while valid ciphertext still depends on it; deletion requires evidence that no such workflow remains.
- No cross-machine DPAPI key transfer or restoration is supported by this decision. A restore to another Windows installation must fail closed until a separately reviewed recovery design exists.
- Loss, corruption, ACL denial, DPAPI failure, or unknown key version makes the affected encrypted workflow unavailable. Do not generate a replacement key automatically and do not silently discard ciphertext.
- No raw key export or plaintext key backup is authorized.

## 6. Runtime activation gate
The infrastructure provider is registered only on Windows when an explicit key path is configured. The API reads and unprotects the key at startup under its actual service identity; missing, malformed, inaccessible, or DPAPI-invalid key material prevents API startup. Promotion remains additionally gated by the existing `Persons:WorkflowPromotionApiEnabled` feature flag and server-side authorization. The installer leaves that flag false and does not enable database writes, identity login, or production activation.

## 7. Required verification before AC-37 acceptance
1. Build and test on the supported Windows .NET 10 runner.
2. Verify effective directory and file DACLs under the final API service identity; confirm ordinary users and network clients cannot read the protected key file.
3. Verify separate installations generate independent keys.
4. Verify wrong key version, missing file, malformed blob, DPAPI failure, and tampering all fail closed.
5. Verify provisioning refuses overwrite and no runtime code silently provisions or rotates.
6. Verify AES-GCM key buffers and plaintext buffers are zeroed on success and failure paths; run secret-sentinel/log-redaction scans.
7. Run the full disposable PostgreSQL 18 promotion/migration integration suite against the exact same commit.
8. Verify Setup creates the LocalService API service, enables the service SID, explicitly provisions for NT SERVICE\\MDGsmApi, starts the API, and validates key unprotection under the service token; verify repair never replaces key bytes.
9. Verify system-wide installation under `C:\\Program Files\\MD-Gsm\\`, separate key storage under `C:\\ProgramData\\MD-Gsm\\Keys\\Persons\\MDGsmApi\\`, and uninstall removes the service/application but preserves ProgramData key material.
10. Complete independent security review and owner release gate.

## 8. Explicit non-decisions
This document selects the built-in LocalService account and MDGsmApi service SID only for the Persons API Windows Service; no service-account password is created. It does not define cross-machine DPAPI backup/recovery, set a retention TTL, authorize database migration execution, activate Promotion, or grant Persons G1. Those remain separate gates.
