# پرونده بازبینی امنیتی و مجوز G1 — نسخه آماده تحویل به ناظر

تاریخ ثبت: 2026-11-04  
مخزن: `Md-gsm/MD-Gsm1`  
PR مرجع: [#31](https://github.com/Md-gsm/MD-Gsm1/pull/31)  
شاخه: `feature/identityaccess-runtime-20261104`  
Commit مبنای این بسته: `5b8669df31de283c8a83ac9fe1e74381ac1a5986`  
Commit مبنای CI قبلی: `18797d3f209c371adfd652cb767c1ddb7bf1b145`

## حکم

این پرونده برای تحویل به ناظر تست انسانی آماده‌سازی شده است؛ **این سند تأیید امنیتی، تأیید معماری، پذیرش محصول یا مجوز G1 نیست**. هیچ امضا یا نتیجه‌ای به نام مالک یا گروه مشاور ثبت نشده است.

PR #31 باید Open + Draft + Unmerged باقی بماند. شاخه `main` نباید تغییر کند و `g1Authorized=false` است.

## وضعیت پیاده‌سازی قابل مشاهده

- Domain/Application، مدل‌های EF Core، DbSetها، PostgreSQL adapters و Unit of Work در شاخه feature وجود دارند.
- password hasher مبتنی بر ASP.NET Core Identity، opaque token factory با CSPRNG و digest و privacy digest وجود دارند.
- تست PostgreSQL disposable برای lockout، heartbeat، revoke پس از تغییر رمز و سقف ورود هم‌زمان اضافه شده است.
- در commit مبنا، سه workflow ثبت‌شده برای PR با نتیجه موفق گزارش شده‌اند: Persons Module Gates، Windows Preview Installer و CodeQL. runهای دقیق در GitHub Actions مرجع هستند.
- هنوز login HTTP endpoint، auth middleware عملیاتی، listener شبکه، migration عملیاتی IdentityAccess یا credential bootstrap وجود ندارد.
- تست‌های فعلی، اثبات امنیت کامل runtime یا پذیرش انسانی نیستند.

## ماتریس بازبینی امنیتی

| حوزه | شواهد/وضعیت فعلی | اقدام لازم پیش از تأیید |
|---|---|---|
| Token secrecy/hash/revoke | token factory و digest adapter موجود؛ تست کامل عدم نشت در log/artifact و revoke در HTTP runtime موجود نیست | آزمون ناظر و بازبینی سورس/لاگ |
| Password hash/rehash | hasher پلتفرمی versioned موجود؛ سیاست rehash و تست migration hash کامل باید تأیید شود | تست الگوریتم/پارامتر/version و rehash |
| Lockout/source rate limit | account lockout و handler و تست PostgreSQL حساب وجود دارد؛ source throttling مستقل باید اثبات شود | آزمون abuse/concurrency و عدم enumeration |
| Bootstrap مدیر | تصمیم local-only و fail-closed مستند است؛ bootstrap اجرایی فعال نشده | بازبینی threat model؛ هیچ رمز ثابت/credential تعبیه نشود |
| Session cap/lease | UoW و تست PostgreSQL چندحسابی وجود دارند | آزمون چندپردازه‌ای، stale lease و شکست شبکه |
| Permissions | catalog و ماتریس نقش‌ها موجود؛ enforce شدن کامل scope در API هنوز اثبات نشده | negative authorization tests و تأیید ماتریس |
| Recovery | طرح دوکاناله و recovery code مستند؛ challenge runtime و آزمون replay/race هنوز پذیرش نشده | threat model، آزمون expiry/replay/race/atomic consume |
| Audit/retention | مدل و سیاست retention مستند؛ append-only و purge اجرایی هنوز اثبات نشده | آزمون tamper/retention و منع ثبت secret/PII |
| TLS/topology | ADR-0012 الزام‌ها را تعریف کرده؛ listener عملیاتی فعال نیست | بررسی cert validation، binding و firewall در مرحله پیاده‌سازی |
| Unicode username | نسخه فعلی از NFC و `ToUpperInvariant` و heuristic script groups استفاده می‌کند | تا pin رسمی Unicode و Script_Extensions/CaseFolding و collision scan، غیرعملیاتی بماند |
| Schema/ADR-0006 | مدل EF و قرارداد opaque token موجود؛ migration IdentityAccess اضافه/اجرا نشده | بازبینی mapping، قیود، FK/index و اثبات‌های امنیتی |
| Privacy | اسکن/بررسی اولیه جایگزین مالک نیست | مالک باید یافته‌ها، استثناها و تصمیم خود را روی همین SHA ثبت کند |

## تحویل به ناظر تست انسانی — ترتیب پیشنهادی

1. ابتدا commit و نتیجه تمام workflowهای همان SHA را در GitHub Actions تثبیت کند.
2. نسخه Preview Installer ویندوز را فقط در محیط آزمایشی نصب کند؛ هیچ دیتابیس عملیاتی استفاده نشود.
3. رفتار فعلی UI و مرزهای غیرفعال بودن IdentityAccess را بررسی کند؛ نبود صفحه ورود/endpoint عملیاتی باید به‌عنوان محدودیت فعلی ثبت شود، نه خطای پنهان.
4. تست‌های خودکار PostgreSQL disposable را از artifact/log همان run بازبینی کند؛ دیتابیس باید نام و comment marker آزمایشی داشته باشد.
5. برای قابلیت‌هایی که هنوز runtime ندارند (recovery، bootstrap، login UI، token validation، API permission enforcement)، نتیجه را «قابل تست انسانی نیست — پیاده‌سازی/مجوز جداگانه لازم است» ثبت کند؛ شبیه‌سازی دستی را به‌عنوان قبولی ثبت نکند.
6. تمام یافته‌ها را با severity، گام بازتولید، نتیجه مورد انتظار/واقعی و SHA ثبت کند. داده حساس، رمز، توکن یا PII در گزارش وارد نشود.
7. پس از رفع یافته‌ها، CI باید دوباره روی commit جدید اجرا شود؛ ناظر فقط همان SHA را تأیید کند.

## فرم ثبت ناظر (خالی عمداً)

- ناظر/نقش:
- SHA بررسی‌شده:
- سیستم‌عامل و نسخه نصب:
- Workflow run IDs:
- سناریوها و نتیجه:
- یافته‌های Critical / High / Medium / Low:
- محدودیت‌های غیرقابل‌آزمون:
- نتیجه: ACCEPT / CONDITIONAL / REJECT
- تاریخ و مرجع گزارش:

## گیت‌های انسانی/مدیریتی که خودکار بسته نمی‌شوند

- تأیید معماری توسط گروه مشاور نرم‌افزار روی SHA دقیق.
- تأیید امنیت و حریم خصوصی توسط مالک پروژه روی SHA دقیق.
- تعیین/تأیید بازبین انسانی و الزام Code Owner در SCM؛ هیچ هویت یا تأییدی جعل نمی‌شود.
- تأیید نهایی Schema و ADR-0006.
- اثبات تنظیمات واقعی Required Status Checks و حفاظت `main` از طریق دسترسی Admin.
- مجوز صریح مالک برای G1، جدا از review و تست.

## حفاظت‌های لازم

- `g1Authorized=false`
- PR #31: Open + Draft + Unmerged
- `main`: بدون تغییر
- بدون IdentityAccess production migration، login endpoint، middleware، network listener یا bootstrap credential
- بدون ادغام خودکار یا آماده‌کردن Merge

این پرونده فقط مرز آماده‌سازی و تحویل را روشن می‌کند؛ مرحله تست انسانی پس از تأیید آمادگی فنی و دسترسی‌های لازم آغاز می‌شود و به‌خودی‌خود G1 را مجاز نمی‌کند.
