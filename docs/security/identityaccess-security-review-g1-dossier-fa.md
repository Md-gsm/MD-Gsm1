# پرونده امنیتی IdentityAccess — وضعیت فنی برای بازبینی انسانی

تاریخ: 2026-11-04  
مخزن: Md-gsm/MD-Gsm1  
PR: #31 (Open + Draft + Unmerged)  
SHA مبنای این پرونده: 5b8669df31de283c8a83ac9fe1e74381ac1a5986

## هدف و محدودیت

این پرونده خلاصه شواهد فنی موجود و آزمون‌های لازم برای بازبین امنیتی را گردآوری می‌کند. این سند هیچ تأیید امنیتی/حریم خصوصی را جعل نمی‌کند و مجوز G1 صادر نمی‌کند.

## اجزای پیاده‌سازی‌شده در شاخه

- Domain و Application handlers/ports برای ورود، تغییر رمز، reset مدیریتی و heartbeat.
- EF Core entity configurations و DbSetهای IdentityAccess.
- PostgreSQL repositories و transactional Unit of Work.
- password hasher مبتنی بر ASP.NET Core Identity.
- opaque token factory با entropy تصادفی 256-bit و digest ذخیره‌سازی.
- تست Integration روی PostgreSQL disposable برای lockout، heartbeat، revoke و سقف ورود هم‌زمان.
- CI روی commit پیشین 18797d3f209c371adfd652cb767c1ddb7bf1b145 موفق بوده است؛ commit این پرونده تغییر مستنداتی دارد و باید CI همان SHA خودش را نیز سبز کند.

## قابلیت‌هایی که عمداً فعال نشده‌اند

- Login HTTP endpoint و auth middleware عملیاتی.
- Listener شبکه و binding تولیدی.
- Migration عملیاتی IdentityAccess و اتصال به دیتابیس واقعی.
- bootstrap credential یا حساب عملیاتی admin/admin.
- recovery endpoint/flow اجرایی.
- فعال‌سازی G1 یا Persons.

## یافته‌ها و آزمون‌های لازم

| موضوع | وضعیت | معیار پذیرش بازبین |
|---|---|---|
| Username | NFC و uppercase invariant و heuristic script group؛ هنوز نسخه Unicode و Script_Extensions/CaseFolding pin نشده | داده رسمی نسخه‌دار، collision scan، تست‌های .NET و fuzz؛ تا آن زمان غیرفعال |
| Password hashing | primitive موجود | پارامترها، versioning، rehash، timing و عدم افشای وجود حساب |
| Opaque token | token factory و digest موجود | raw token در DB/log/artifact صفر؛ revoke فوری؛ disabled account رد |
| Login lockout | lockout و تست PostgreSQL موجود | 5/15/15، source rate limit مستقل، concurrency و پاسخ عمومی |
| Session | lease و سقف حساب‌های متمایز | چندپردازه، stale lease، heartbeat race، revoke و ظرفیت |
| Authorization | permission catalog و ماتریس اولیه | deny-by-default، negative tests، row scope تکنسین در API/DB |
| Recovery | design و recovery code digest model | threat model و تست expiry/replay/race/atomic consumption |
| Bootstrap | local-only و fail-closed در تصمیم | آزمون setup یک‌باره، منع network تا تکمیل، عدم وجود secret ثابت |
| Audit/privacy | مدل audit و retention policy | append-only، allow-list، حذف secret/PII از log و purge evidence |
| Schema | EF model و قیود موجود؛ migration عملیاتی وجود ندارد | بازبینی FK/index/check/uniqueness و سازگاری mapping با PostgreSQL 18 |
| TLS/topology | ADR-0012 | رد certificate نامعتبر، binding صریح، firewall و عدم اتصال مستقیم Desktop به DB |

## خروجی مورد انتظار از بازبینی

بازبین باید روی همین SHA ثبت کند:
- هویت/نقش بازبین و دامنه بازبینی؛
- یافته‌ها با severity و مسیر بازتولید؛
- ACCEPT / CONDITIONAL / REJECT؛
- مواردی که به علت نبود runtime قابل آزمون نیستند؛
- تاریخ و شناسه گزارش.

مالک پروژه باید بررسی امنیت و حریم خصوصی را جداگانه ثبت کند. گروه مشاور باید بازبینی معماری را جداگانه ثبت کند. نویسنده کد یا CI نمی‌تواند جای این تأییدها را بگیرد.

## حفاظت

PR #31 باز و Draft و ادغام‌نشده می‌ماند؛ main بدون تغییر؛ `g1Authorized=false`. هرگونه migration تولیدی، endpoint ورود، listener شبکه و credential bootstrap تا مجوز مستقل ممنوع است.
