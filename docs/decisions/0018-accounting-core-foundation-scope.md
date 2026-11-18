# Accounting core foundation — owner-authorized execution scope

| Field | Value |
|---|---|
| Record | MDG-ADR-0018 |
| Date | 2026-11-18 |
| Status | OWNER-AUTHORIZED IMPLEMENTATION SCOPE — NOT A POSTING RELEASE |
| Data owners | ACC-01, ACC-02, FY-01 — Accounting |
| Architecture parent | MDG-ARCH-MASTER-001 v2.5 |
| COA authority | MDG-COA-MASTER-001 v1.2 master; v1.4 supplementary register |
| Migration | 0009 is RESERVED in main by PR #70; SQL NOT AUTHORIZED YET. Physical design: MDG-ACC-PDS-001 v0.1. |

## Owner direction

The product owner explicitly directed the team to begin real Accounting implementation on 2026-11-18. This record limits that authorization to the foundational kernel required to implement and test the approved accounting invariants.

## Authorized first slice

- Integer-only IRR amount type and ADR-0017 rounding/allocation primitives.
- Canonical account eligibility model that fails closed when required 7C attributes are absent or unverified.
- Fiscal-period posting, close and independently authorized reopen guards.
- Immutable finalized journal model, balanced journal-line invariants and separate reversal document.
- Stable posting identity and idempotency-key derivation.
- Read-only Posting Preflight that refuses absent, incomplete or unaccepted 7B rules and ineligible 7C accounts.
- Unit tests for positive invariants and negative/fail-closed behavior.

## Explicit boundaries

1. This work does not change the approved phase order or claim that Accounting Phase 8 is complete.
2. Inventory remains a prerequisite for Inventory-dependent accounting events and integrated posting. No Inventory, Treasury, Sales, Purchasing or RepairOperations event adapter is introduced in this slice.
3. No account is seeded as canonical. The 7C attributes not established by v1.2, v1.4 and accepted owner decisions remain unresolved; no AccountId, parent, Nature, NormalBalance, effective date or posting permission is invented.
4. No positive FE Posting Rule is seeded merely to make a test pass. 7B remains blocked until each rule is supported by a complete source/decision crosswalk.
5. No Journal is persisted or posted by this first slice. The preflight service is read-only; transactional PostgreSQL persistence, API composition and runtime posting require the reserved migration and subsequent acceptance.
6. TaxMode=0 continues to forbid all Tax Lines/Tax Posting. No Tax Matrix or legal rate is fabricated.
7. No production migration, data import, G1 activation or Posting Release is authorized.

## Acceptance of this slice

- Accounting Contracts, Domain and Application are separate projects with inward-only dependencies.
- Unit tests prove IRR, allocation, account eligibility, period controls, journal balance/immutability/reversal, idempotency identity and fail-closed preflight.
- Architecture and CI project inventories are updated.
- PostgreSQL persistence is a subsequent step after migration 0009 reservation is merged and the schema design is reviewed.
- Independent review is required on the exact final SHA.
