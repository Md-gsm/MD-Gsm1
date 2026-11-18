# Finance Contracts

Contract version: **1.1** (additive Party account identity and trusted actor context).

Finance owns financial records and party subledger account identity. Persons remains the canonical owner of PartyId and personal identity.

The first runtime contract is `PartyAccountReference`: it identifies the Finance-owned subledger account and the stable PartyId it belongs to. It carries no balance, credit limit, or payment instrument data.

`IFinanceActorContext` is a server-side boundary. Implementations must obtain ActorId from the authenticated principal, never from request payloads or client-controlled headers. Provisioning rejects a missing, blank, padded, control-character, or over-128-character actor identifier.

Account provisioning is lazy (first financial use), idempotent, and performed inside the caller-owned Finance transaction. Persons validation is performed in that same transaction and locks the active Party row. Replays use INSERT ... ON CONFLICT DO NOTHING followed by a SELECT under READ COMMITTED; existing creation metadata is preserved. No account is created as a side effect of Persons registration. Cross-module database atomicity follows ADR-0005.
