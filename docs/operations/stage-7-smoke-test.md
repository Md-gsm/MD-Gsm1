# Stage 7: one-command disposable PostgreSQL + Desktop test (Windows)

Run PowerShell from the project root (the folder containing `MDGsm.sln`). This script creates a **new, retained, disposable database** for this run, applies the project-owned SQL migration, starts the API configured for that database, checks API liveness/readiness/system-info, and launches the Desktop client. The API is stopped when the Desktop process exits.

## 1. Build and test first

```powershell
dotnet restore .\MDGsm.sln
if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
dotnet build .\MDGsm.sln --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
dotnet test .\MDGsm.sln --no-build
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
```

## 2. Start the integrated test

```powershell
.\scripts\Start-MDGsm-DisposableTest.ps1
```

The script prompts for the local PostgreSQL admin password with hidden input. It defaults to PostgreSQL 18 at `127.0.0.1:5432` and user `postgres`. You can override paths/host/port/user as parameters, but the safety guard permits only localhost addresses. Never paste the password into chat or save it in a source file.

## 3. Verify Desktop

When the Desktop window opens, click **بررسی اتصال به API**. Expected UI status: **ارتباط موفق است.** The script's console should already have reported PASS for migration version 1, API liveness, PostgreSQL readiness, and system-info.

## Safety and scope

- This script is strictly a local test convenience. It creates a new database with prefix `mdgsm_stage7_disposable_`, marks it with PostgreSQL comment `MDGSM_DISPOSABLE_TEST_DB`, and does not drop it, even on failure.
- It does not alter any existing database and has no drop/reset commands.
- It uses the admin login for the API only to keep this first local smoke test straightforward. This is **not production-safe**; before any production use, implement a separate least-privilege runtime role, secret management, TLS/network policy, and reviewed migration deployment.
- Migration 0001 creates only `mdgsm_schema_version`; no business or accounting tables are created.
- The script does not use `dotnet-ef`; it applies the explicit SQL migration in `database/migrations/0001_infrastructure_schema_version.sql`. The API-only runner `scripts/Invoke-Stage7-PostgresSmokeTest.ps1` has the same loopback-only and disposable-database marking guard; it asks for the admin password via hidden input rather than a command-line parameter.
- It must only be run against a local disposable test server. Do not use a real-data database.
