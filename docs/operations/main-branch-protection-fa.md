# حفاظت شاخه main — وضعیت واقعی

مخزن Md-gsm/MD-Gsm1؛ SHA f011c94d52bc06c7e213abb6e480180e9ea73f40؛ تاریخ 2026-11-08.

Branch endpoint، protected=true و ۱۴ context الزامی را نشان می‌دهد. Endpoint جزئیات protection برای این اتصال HTTP 403 است؛ مقادیر زیر طبق REST readback مالک ثبت می‌شوند: حداقل یک تأیید؛ Code Owner review الزامی؛ approver غیر از آخرین pusher؛ dismiss stale approvals؛ enforce_admins=true؛ required_signatures=false.

۱۴ context: foundation-build-test، persons-spec-lint، persons-architecture-boundary، persons-consumer-registry، persons-api-contract، persons-temporary-data-retention، persons-merge-contract، persons-postgres-migrations، persons-postgres-windows-migrations، persons-security-tests، persons-e2e-disposable، persons-ci-enforcement-test، persons-ci-evidence، project-charter-owner-gate.

روی SHA دقیق ۱۵/۱۵ check-run موفق است: [Persons Gates 42411846550](https://github.com/Md-gsm/MD-Gsm1/actions/runs/42411846550) (13/13)، [CodeQL 42411846619](https://github.com/Md-gsm/MD-Gsm1/actions/runs/42411846619) (موفق ولی غیرالزامی)، [Charter Gate 42411846673](https://github.com/Md-gsm/MD-Gsm1/actions/runs/42411846673). Windows Preview Installer روی این SHA اجرا نشده؛ PostgreSQL فقط disposable CI.

طبق ممیزی مالک Secret Scanning، Push Protection و Dependabot Security Updates خاموش‌اند؛ commit signature enforcement نیز خاموش است. تنظیمات Security از این اتصال readback نشد؛ خروجی Admin لازم است.

بازبینی مستقل: reviewer واجد صلاحیت و مستقل از نویسنده/مالک تصمیم، بررسی Diff و شواهد روی آخرین SHA، گزارش file/line، severity، impact، reproduction، tests و resolution. approval خالی یا «ok/1» کافی نیست؛ بعد از Push review مجدد لازم است.

اسکریپت Configure-MainBranchProtection.ps1 هنوز ۱۲ check دارد، در حالی که live main چهارده context دارد. تا هم‌ترازی و dry-run/readback، گزینه Apply اجرا نشود.
