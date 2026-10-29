# Phase 7 status

## Source/package status
- PostgreSQL provider and EF Core 10 references are present in Infrastructure.
- SQL migration `0001_infrastructure_schema_version.sql` creates only the schema-version metadata table and records version 1.
- `scripts/Start-MDGsm-DisposableTest.ps1` automates local disposable database creation, migration, API connection/readiness checks, and Desktop launch for a smoke test.
- `scripts/Invoke-Stage7-PostgresSmokeTest.ps1` remains available for API-only smoke testing.

## Validation status
- The user previously confirmed `dotnet restore`, solution build, and all 9 tests succeeded on Stage 7 before this automation revision.
- This automation revision has not yet been executed on the user's Windows/PostgreSQL machine. Re-run restore/build/tests, then run `scripts/Start-MDGsm-DisposableTest.ps1` and verify the Desktop status.
- Do not claim the real PostgreSQL-to-API-to-Desktop test has passed until the user returns its output.

## Scope and safety
- Clean-start MD-Gsm only; no code or artifacts from any earlier application are used.
- No business/accounting schema is introduced.
- The runner only accepts a local PostgreSQL host, creates a fresh `mdgsm_stage7_disposable_...` database, and intentionally retains it for evidence.
- Admin credentials are used only for this local smoke test. This design is not production-ready; least-privilege runtime credentials and managed secrets are required before production use.
