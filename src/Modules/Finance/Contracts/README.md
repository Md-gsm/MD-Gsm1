# Finance Contracts

Contract version: **1.1** (additive Party account identity reference).

Finance owns financial records and party subledger account identity. Persons remains the canonical owner of PartyId and personal identity.

The first runtime contract is `PartyAccountReference`: it identifies the Finance-owned subledger account and the stable PartyId it belongs to. It deliberately carries no balance, credit limit, or payment instrument data. Those values must be derived from Finance-owned ledger/policy records or the canonical owner module.

Account provisioning is lazy (first financial use), idempotent, and performed inside the caller-owned Finance transaction. No account is created as a side effect of Persons registration. Cross-module database atomicity follows ADR-0005.
