# RepairOperations Contracts

Reserved for versioned repair workflow contracts. This module owns technician skills, capabilities, scheduling, assignment, and the repair-case lifecycle. Technician identity/work role is owned by Persons and referenced through PartyId contracts.

No runtime repair source-event contract is published yet. The owner-review proposal for FE-02 is:
- `docs/modules/repairoperations/REPAIROPERATIONS-SOURCE-EVENT-CONTRACT-PROPOSAL-001_v0.1.md`

The proposal distinguishes technical completion, optional quality acceptance, and customer handover. It is not an accepted contract, does not add runtime event types, and does not select Accounting RecognitionTime. Owner review is required before any runtime publication.
