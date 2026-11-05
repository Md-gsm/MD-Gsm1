# IdentityAccess — وضعیت مراحل و گیت‌ها
تاریخ 2026-11-04 | مراحل 1 تا 3 ثبت شده؛ پنج موضوع باقیمانده مستند/آماده‌سازی شده‌اند.

1. ماتریس مجوز: `identityaccess-five-open-items-fa.md`؛ تصویب مالک لازم.
2. retention: همان سند؛ این retention مربوط به audit/security IdentityAccess است و از retention داده موقت Persons جداست؛ زمان‌ها پیشنهادند و بررسی حقوقی/تصویب مالک لازم.
3. username: قرارداد و oracle تست Python در CI؛ تست .NET production، Unicode version pin، Script_Extensions معتبر و collision scan هنوز لازم.
4. recovery: runbook در همان سند؛ threat-model review و تصمیم provider/TTL challenge لازم.
5. security/G1: پرونده reviewer آماده؛ review مستقل Runtime/IdentityAccess و تصمیم صریح مالک برای G1 انجام نشده است. تأییدهای ثبت‌شده در 2026-10-31 فقط baseline G0 را پوشش می‌دهند و جایگزین review G1 نیستند؛ G1 مسدود است.

روی SHA قبلی `0f6e32d893e84b2ac7319bbe639e74dcd67fe0a6`: CodeQL run 41794022182 موفق؛ installer run 41794021941 موفق؛ Persons Gates run 41794021936 در آخرین poll همه jobها جز Windows PostgreSQL که in_progress بود موفق بودند. این نتایج به SHA جدید تعمیم ندارند.

پیش از runtime: تصویب ماتریس و retention؛ تست .NET؛ review recovery/bootstrap؛ review معماری گروه مشاور؛ تصمیم امنیت/حریم خصوصی مالک؛ اثبات Required Status Checks در SCM؛ تست migration/concurrency/failure/rollback/lockout/lease/revoke/audit در disposable PostgreSQL 18.
هیچ login endpoint، middleware، session store، IdentityAccess migration، network listener یا credential عملیاتی ساخته نشده است. PR #30 Open+Draft+Unmerged؛ main بدون تغییر؛ `g1Authorized=false`.
