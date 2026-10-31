# Required Status Checks — MD-Gsm Persons G1

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

The repository has a workflow definition and successful workflow runs, but the exact branch-protection/ruleset configuration has not been independently retrieved through the connected GitHub integration. Treat remote required-check enforcement as UNVERIFIED until the owner supplies the settings/API evidence. The prior negative architecture PR demonstrated a blocked merge state, but it is not a substitute for inspecting the configured ruleset and recording a dedicated current negative-enforcement proof.

The Linux PostgreSQL 18.6 job and a separate Windows PostgreSQL 18.6-5 job now execute the application-owned `MDGsm.DbMigrator`. The Windows job verifies the EDB installer using its published SHA-256, creates a marked disposable database, runs the migrator twice, and checks history and idempotence. These checks are configured in this branch; validation prerequisites must remain false until both CI jobs pass on the current commit. The Linux container image remains version-tagged but not digest-pinned.
