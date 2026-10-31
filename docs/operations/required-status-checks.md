# Required Status Checks — MD-Gsm G0/G1 control

Workflow: `.github/workflows/persons-module-gates.yml`

این jobها باید روی repository remote به‌صورت Required Status Checks / ruleset ثبت شوند:

- `foundation-build-test`
- `persons-spec-lint`
- `persons-architecture-boundary`
- `persons-consumer-registry`
- `persons-api-contract`
- `persons-temporary-data-retention`
- `persons-merge-contract`
- `persons-postgres-migrations`
- `persons-postgres-windows-migrations`
- `persons-security-tests`
- `persons-e2e-disposable`
- `persons-ci-enforcement-test`
- `persons-ci-evidence`

## نکته اجرایی مهم

تعریف workflow معادل فعال‌شدن قفل branch نیست. تا زمانی که remote repository مشخص نشده و تنظیمات branch protection/ruleset مشاهده و آزمون نشده‌اند، وضعیت `requiredStatusChecksConfiguredInScm` باید `false` بماند و G1 نباید شروع شود.

## آزمون منفی واقعی

1. یک branch آزمایشی روی remote بسازید.
2. تغییری ایجاد کنید که یکی از checkهای اجباری را عمداً FAIL کند.
3. PR بسازید و نشان دهید سیستم Merge را مسدود می‌کند.
4. شواهد PR، نام check شکست‌خورده، تنظیم branch protection و نتیجه مسدودشدن Merge را ثبت کنید.
5. تغییر آزمایشی را بدون دور زدن قفل خاتمه دهید.

Unit test مربوط به سیاست fail-closed، رفتار خود validator را می‌آزماید؛ **جایگزین آزمون branch protection واقعی نیست**.

## Evidence state (2026-10-31)

- The remote `main` branch API reports `protected=true`.
- The rulesets API returned an empty list. The dedicated branch-protection details endpoint returned HTTP 403 (`Resource not accessible by integration`), so the exact required-check names, strict/up-to-date policy, bypass exceptions and review requirements remain unverified.
- Negative enforcement test: PR #6 deliberately added the forbidden `MDGsm.Domain -> MDGsm.Infrastructure` project reference. The `persons-architecture-boundary` job failed in workflow run `41177718250`; GitHub reported the PR `mergeable_state=blocked`; the PR was closed without merge. This establishes that the negative test was blocked, but does not establish the complete exact Required Status Checks configuration.
- Consequently, `negativeCiEnforcementTestPassed` may be recorded as true, while `requiredStatusChecksConfiguredInScm` must remain false until a repository Admin can inspect and export the actual settings from GitHub. The connected GitHub integration has no write action for Branch Protection/Rulesets, so this setting cannot be changed from this session.

## Database migration evidence

The application-owned `tools/MDGsm.DbMigrator` was validated on both Linux and Windows against PostgreSQL 18.6 in workflow run `41181075233` (13/13 jobs passed). The Windows job verified the EDB installer SHA-256, created a marked disposable database, applied one migration on the first run and zero on the second. The Linux job performed the same first-run/second-run idempotence check. See `docs/operations/evidence/postgresql18-migration-validation-2026-10-31.json`.

The Linux service image is now digest-pinned as `postgres:18.6-bookworm@sha256:5c4783a8cc3f0074912164f94dfa1be4e043438f0f707cf968d7af9a0d31a2c8`.
