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

## نتیجه مشاهده remote در 2026-10-30

- Repository: `Md-gsm/MD-Gsm1` (private).
- Branch list endpoint returned `main.protected=false`; بنابراین در لحظه بررسی هیچ branch protection فعالی روی `main` گزارش نشده است.
- Rulesets endpoint returned HTTP 403 with GitHub's message: `Upgrade to GitHub Pro or make this repository public to enable this feature.`
- GitHub documentation confirms protected branches and rulesets on private repositories require Pro/Team/Enterprise; on GitHub Free they are available for public repositories. See [Managing protected branches](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches) and [Managing rulesets](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets).
- Repository visibility has intentionally not been changed. No plan upgrade or billing action was made.
- The connected GitHub integration did not expose branch-protection write operations; branch-protection read returned `Resource not accessible by integration`. The branch setting therefore remains an owner-side action after plan/access is resolved.

## پیکربندی پیشنهادی پس از رفع محدودیت

1. مخزن را private نگه دارید و برای حساب مالک GitHub Pro یا بالاتر فعال کنید؛ یا با تصمیم آگاهانه مالک، گزینه public شدن repo را بررسی کنید. این پروژه به‌طور پیش‌فرض نباید public شود.
2. در `Settings → Branches` برای `main` یک branch protection rule بسازید.
3. Require a pull request before merging را فعال کنید؛ direct push را ببندید؛ force push و deletion را ممنوع کنید؛ bypass را فقط با استثنای محدود و مستند مجاز کنید.
4. Require status checks to pass before merging را فعال کنید و هر 12 نام job بالا را انتخاب کنید؛ گزینه strict / require branches to be up to date را روشن کنید.
5. پس از مشاهده ذخیره‌شدن rule، یک PR آزمایشی با check عمداً ناموفق ایجاد کنید و ثابت کنید GitHub Merge را مسدود می‌کند. سپس PR آزمایشی را ببندید و شواهد آن را در `g1-entry-evidence.json` درج کنید.

**تا زمان مشاهده واقعی rule و انجام آزمون منفی، `requiredStatusChecksConfiguredInScm=false` و `negativeCiEnforcementTestPassed=false` باقی می‌مانند.**
