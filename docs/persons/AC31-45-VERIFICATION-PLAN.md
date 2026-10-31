# AC-31..AC-45 verification plan

The G0 traceability plan assigns every requirement to its gate and future executable scenario. These entries are plans, not passing tests or implemented behavior.

| AC | Future test scenario | Required proof |
|---|---|---|
| AC-31 | RET-31-UTC-EXPIRY-BLOCK | Expired data is blocked immediately |
| AC-32 | RET-32-PHYSICAL-PURGE | PII and derivatives purged within 30 minutes |
| AC-33 | RET-33-ABSOLUTE-MAX-AGE | 30-day absolute age cannot be extended |
| AC-34 | RET-34-SINGLE-AUTHORIZED-RENEWAL | One authorized renewal only |
| AC-35 | RET-35-WORKER-IDEMPOTENT-RETRY | Failure, retry, idempotency and alert behavior |
| AC-36 | RET-36-NO-GLOBAL-DISCOVERY | No global search/autocomplete exposure |
| AC-37 | RET-37-EXPLICIT-ATOMIC-PROMOTION | Explicit party selection and atomic promotion |
| AC-38 | RET-38-NO-RAW-PII-TELEMETRY | No raw PII in logs, audit or CI artifacts |
| AC-39 | MRG-39-CONSUMER-REGISTRY-COMPLETE | All PartyId consumers discovered |
| AC-40 | MRG-40-REFERENCE-CLASSIFICATION | Every reference classified exactly once |
| AC-41 | MRG-41-VERSIONED-PARTICIPANT-CONTRACT | Prepare/Validate/Apply/Verify/Recovery contract |
| AC-42 | MRG-42-READONLY-PREVIEW | Preview is read-only and counts are accurate |
| AC-43 | MRG-43-BLOCKING-CONFLICT-STOP | Blocking conflicts prevent execution |
| AC-44 | MRG-44-ATOMIC-CONCURRENT-IDEMPOTENT | Shared transaction, concurrency and idempotency |
| AC-45 | MRG-45-HISTORICAL-SNAPSHOT-PRESERVATION | Historical snapshots remain unchanged |

Each criterion remains UNMAPPED in the executable acceptance map until a real test file and test method exist. G1 remains unauthorized until those tests are implemented and pass.
