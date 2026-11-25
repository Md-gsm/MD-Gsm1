# Persons Temporary Workflow Payload Contract v1

Status: AC-37 implementation contract. This describes the encrypted payload format consumed by Persons promotion; it does not select a production key-custody provider.

## Encryption envelope

- Algorithm: AES-256-GCM.
- Wire bytes: 12-byte nonce || 16-byte authentication tag || ciphertext.
- Associated data (UTF-8): MD-Gsm/Persons/TemporaryWorkflow/v1/{workflowRecordId:D}/{keyVersion}.
- Key material is resolved by keyVersion through the injected ITemporaryWorkflowEncryptionKeyProvider. A 32-byte key is required.
- A ciphertext copied to another WorkflowId or paired with a different keyVersion fails authentication.
- The key provider is intentionally not implemented or registered by this change. No key, credential, or secret is committed to source control.

## Plaintext JSON envelope

The decrypted payload is UTF-8 JSON, maximum 64 KiB, with case-sensitive camelCase property names, maximum nesting depth 16, no comments/trailing commas, and unknown properties rejected.

Required fields:
- schemaVersion: integer, exactly 1.
- partyType: NaturalPerson or LegalEntity.
- roles: non-empty distinct array containing Customer and/or Colleague.

NaturalPerson fields:
- givenName and familyName are required.
- nationalId is optional.
- legalName, legalNationalId, and economicCode are forbidden.
- workClassification may be Staff, Employee, or Technician.

LegalEntity fields:
- legalName is required.
- legalNationalId, economicCode, and responsiblePersonName are optional.
- givenName, familyName, nationalId, and workClassification are forbidden.

Common optional fields:
- primaryMobile, alternateMobile, landline, email, internalNote.
- acquaintanceSource must be a defined PartyAcquaintanceSource value; omitted means Unknown.
- communication contains repairStatusSmsEnabled, readyForDeliverySmsEnabled, and promotionsSmsEnabled. Defaults are true, true, and false respectively.
- address contains countryCode, province, city, postalCode, and addressLine. If any address component is supplied, all four address components are required. Omitted countryCode defaults to IR.

Domain registration validation remains authoritative for identifier, contact, address, role/work-classification, and duplicate constraints. The parser never logs or echoes plaintext. Preview exposes only the workflow owner's display name and a masked primary mobile; it does not return identifiers, full contact values, address, notes, or ciphertext.

## Lifecycle and deletion

On successful promotion, the Party, workflow closure, encrypted payload deletion, immutable PII-free tombstone, and (for a newly created Party only) PartyRegistered Outbox event plus initial delivery record commit in one PostgreSQL transaction. Failed promotion leaves the encrypted payload and safe retry state intact. The tombstone stores WorkflowId, OperationId, ActorId, resulting PartyId, UTC completion time, Existing/Created outcome, and schemaVersion only.
