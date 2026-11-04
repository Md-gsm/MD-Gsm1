# پرونده بازبینی امنیتی و مجوز G1
تاریخ 2026-11-04 | آماده بازبینی؛ نه تأییدشده.

مرجع: ADR-0006 opaque token؛ ADR-0012 deployment topology؛ ADR-0013 account/session/bootstrap. IdentityAccess هنوز runtime، login middleware، session store یا migration عملیاتی ندارد.

| حوزه | شواهد الزامی | وضعیت |
|---|---|---|
| token secrecy/hash/revoke | تست DB/log/artifact و revoke فوری | مسدود؛ runtime نیست |
| password hash/rehash | الگوریتم/پارامتر/version و تست | مسدود |
| lockout/source rate limit | تست پنجره، concurrency، عدم enumeration | مسدود |
| bootstrap admin/admin | local-only، یک‌باره، atomic، fail-closed، no embedded secret | مسدود |
| session cap/lease | تست چندکلاینت و stale lease | مسدود |
| permission | ماتریس مصوب و negative API tests | تصویب مالک لازم |
| recovery | threat model، replay/race tests | مسدود |
| audit/retention | append-only، archive integrity، retention مصوب | مسدود |
| TLS/topology | invalid cert rejection، binding/firewall | review لازم |
| architecture/privacy | review مستقل گروه مشاور و تصمیم مالک | باز |

CI سبز فقط jobهای همان SHA را تأیید می‌کند؛ امنیت runtime یا G1 را اثبات نمی‌کند. نویسنده کد نمی‌تواند review مستقل را به‌جای reviewer واقعی امضا کند.

فرم reviewer: نام/نقش ____؛ SHA ____؛ تاریخ ____؛ دامنه ____؛ یافته‌های Critical/High/Medium/Low ____؛ شروط ____؛ نتیجه ACCEPT/CONDITIONAL/REJECT ____؛ ثبت هویت/امضا ____.
تصمیم مالک: review امنیت/حریم خصوصی ____؛ review معماری گروه مشاور ____؛ Required Status Checks و branch protection ____؛ تصمیم AUTHORIZE G1 / KEEP BLOCKED ____؛ تاریخ/SHA ____.
تا تکمیل واقعی فرم و مجوز صریح: PR #30 Open+Draft+Unmerged، main بدون تغییر، `g1Authorized=false`.
