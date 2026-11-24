# AuditSecurity Contracts v1.0

The published ingestion contract accepts one immutable event intent from a source module's committed Outbox delivery path. Delivery is at-least-once; consumers must use the stable EventId and retry the same canonical intent after an ambiguous response.

The contract carries identifiers, event classifications and a restricted metadata map only. Password material, credential hashes, session tokens, OTPs, recovery codes, connection strings, private keys and unnecessary raw PII are prohibited.

The contract assembly is DTO/interface-only and has no ProjectReference dependencies.
