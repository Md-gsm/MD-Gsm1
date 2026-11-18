# Required Status Checks — MD-Gsm1

**SHA:** f011c94d52bc06c7e213abb6e480180e9ea73f40 | **تاریخ:** 2026-11-08

۱۵ check-run روی main موفق‌اند؛ ۱۴ context زیر Required هستند:

| # | Context | Required |
|---:|---|---|
| 1 | foundation-build-test | بله |
| 2 | persons-spec-lint | بله |
| 3 | persons-architecture-boundary | بله |
| 4 | persons-consumer-registry | بله |
| 5 | persons-api-contract | بله |
| 6 | persons-temporary-data-retention | بله |
| 7 | persons-merge-contract | بله |
| 8 | persons-postgres-migrations | بله |
| 9 | persons-postgres-windows-migrations | بله |
| 10 | persons-security-tests | بله |
| 11 | persons-e2e-disposable | بله |
| 12 | persons-ci-enforcement-test | بله |
| 13 | persons-ci-evidence | بله |
| 14 | project-charter-owner-gate | بله |

[Persons Gates 42411846550](https://github.com/Md-gsm/MD-Gsm1/actions/runs/42411846550) 13/13 SUCCESS؛ [CodeQL 42411846619](https://github.com/Md-gsm/MD-Gsm1/actions/runs/42411846619) SUCCESS ولی non-required؛ [Charter 42411846673](https://github.com/Md-gsm/MD-Gsm1/actions/runs/42411846673) SUCCESS. Windows Installer روی این SHA اجرا نشده. PostgreSQL فقط disposable CI.

Branch endpoint protected=true و ۱۴ context را نشان می‌دهد. جزئیات protection برای این اتصال 403 است؛ مقادیر تکمیلی طبق گزارش مالک: یک approval، Code Owner review، approver غیر از آخرین pusher، dismiss stale، enforce_admins=true، required_signatures=false.

بازبینی مستقل باید واجد صلاحیت، مستقل، روی SHA نهایی و با گزارش یافته‌های واقعی باشد. اسکریپت Configure-MainBranchProtection.ps1 هنوز ۱۲ check دارد؛ تا اصلاح و dry-run/readback از Apply استفاده نشود.
