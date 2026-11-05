# بسته پنج موضوع باز IdentityAccess
تاریخ 2026-11-04 | وضعیت: آماده‌سازی مستند تکمیل؛ تصویب‌ها و review انسانی هنوز بازند.

## 1. ماتریس granular مجوز
قاعده: deny-by-default؛ فقط permission صریح؛ اجتماع مجوزهای چندنقشی؛ scope مستقل سمت API؛ wildcard ممنوع.
ترتیب نقش‌ها در جدول: super_admin, manager, receptionist, technician, warehouse, salesperson, purchaser, cashier, accountant, viewer.
| مجوز | super_admin | manager | receptionist | technician | warehouse | salesperson | purchaser | cashier | accountant | viewer |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| persons.read | ✓ | ✓ | ✓ | ✓* | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| persons.create | ✓ | ✓ | ✓ | | | ✓ | ✓ | | ✓ | |
| persons.update | ✓ | ✓ | ✓ | | | | | | ✓ | |
| persons.delete | ✓ | | | | | | | | | |
| repairs.read | ✓ | ✓ | ✓ | ✓* | | | | | ✓ | ✓ |
| repairs.create/update | ✓ | ✓ | ✓ | | | | | | | |
| repairs.assign | ✓ | ✓ | | | | | | | | |
| repairs.complete/worklog.write/parts.consume | ✓ | ✓ | | ✓* | | | | | | |
| repairs.deliver | ✓ | ✓ | ✓ | | | | | | | |
| inventory.read | ✓ | ✓ | | ✓* | ✓ | ✓ | ✓ | | ✓ | ✓ |
| inventory.receipt.create | ✓ | ✓ | | | ✓ | | ✓ | | | |
| inventory.issue.create/count.execute | ✓ | ✓ | | | ✓ | | | | | |
| sales.read | ✓ | ✓ | | | | ✓ | | ✓ | ✓ | ✓ |
| sales.create | ✓ | ✓ | | | | ✓ | | | | |
| sales.price.override | ✓ | | | | | | | | | |
| purchasing.read | ✓ | ✓ | | | ✓ | | ✓ | | ✓ | ✓ |
| purchasing.create | ✓ | ✓ | | | | | ✓ | | | |
| cash.read | ✓ | ✓ | | | | | | ✓ | ✓ | ✓ |
| cash.receipt.create/payment.create | ✓ | ✓ | | | | | | ✓ | | |
| finance.read | ✓ | ✓ | | | | | | | ✓ | ✓ |
| finance.voucher.create | ✓ | ✓ | | | | | | | ✓ | |
| finance.voucher.delete | ✓ | | | | | | | | | |
| finance.period.close | ✓ | | | | | | | | ✓ | |
| identity.accounts.read | ✓ | ✓ | | | | | | | | |
| identity.accounts.create/update | ✓ | | | | | | | | | |
| identity.credentials.reset | ✓ | | | | | | | | | |
| identity.roles.assign/permissions.manage | ✓ | | | | | | | | | |
| identity.sessions.revoke/settings.manage/recovery.execute | ✓ | | | | | | | | | |
| system.settings.read | ✓ | ✓ | | | | | | | | |
| system.settings.manage | ✓ | | | | | | | | | |

*تکنسین فقط رکوردهای تخصیص‌یافته. کاتالوگ granular: persons.read/create/update/delete؛ repairs.read/create/update/assign/complete/deliver/worklog.write/parts.consume؛ inventory.read/receipt.create/issue.create/count.execute؛ sales.read/create/price.override؛ purchasing.read/create؛ cash.read/receipt.create/payment.create؛ finance.read/voucher.create/voucher.delete/period.close؛ identity.accounts.read/create/update، identity.credentials.reset، identity.roles.assign، identity.permissions.manage، identity.sessions.revoke، identity.settings.manage، identity.recovery.execute؛ system.settings.read/manage.
manager مجوز مدیریت حساب/نقش/امنیت ندارد. حذف سند نهایی ممنوع و فقط reversal/correction؛ accountant می‌تواند دوره را ببندد ولی بازگشایی در ماتریس نیست. این ماتریس پیشنهاد است؛ تصویب مالک قبل از seed لازم است.

## 2. Retention سوابق امنیتی
پیشنهاد، نه الزام قانونی یا استاندارد جهانی: رخدادهای مدیریتی/حساس 24 ماه online و حذف نهایی 36 ماه؛ ورود/نشست 12 ماه online و حذف 18 ماه؛ پرونده incident تا بسته‌شدن +12 ماه با بازبینی 90 روزه؛ telemetry aggregate برابر 90 روز. آرشیو append-only/immutable با manifest و کلید امضای خارج DB؛ IP خام پیش‌فرض ذخیره نشود؛ audit فقط allow-list metadata و بدون رمز/hash رمز/token/recovery code. legal hold بر purge مقدم است. تصویب مالک و بررسی حقوقی لازم؛ تا آن زمان purge/archival اجرایی نشود. **این سیاست retention مخصوص audit و رویدادهای امنیتی IdentityAccess است و با سیاست داده موقت Persons در `TEMPORARY_DATA_RETENTION_POLICY_FA.md` (TTL هفت‌روزه/حداکثر عمر ۳۰روزه) یکسان یا جایگزین آن نیست.**

## 3. Username normalization و آزمون
NFC نه NFKC؛ بدون trim خاموش؛ طول 3..64 Unicode scalar؛ فقط General Category L* و Nd؛ ارقام فارسی/عربی/لاتین حفظ؛ حروف فارسی/عربی ادغام نشوند؛ Latin default case-fold نسخه‌دار؛ collation قطعی؛ mixed-script letters رد و Script_Extensions معتبر الزامی. oracle تست Python در `scripts/ci/test_identityaccess_policy.py` افزوده شده و در spec gate اجرا می‌شود؛ جایگزین تست .NET production نیست. Unicode version pin، collision scan و .NET fuzz tests پیش از runtime لازم‌اند.

## 4. Emergency recovery مدیر کل
Break-glass فقط کنسول محلی؛ دو کانال مستقل ایمیل و موبایل به‌علاوه recovery code آفلاین یک‌بارمصرف با entropy حداقل 128 بیت و فقط digest در DB. challengeها کوتاه‌عمر و request-bound؛ rate limit، مصرف اتمیک code، revoke تمام نشست‌ها، افزایش credential_version، must_change_password و fail-closed شبکه. backdoor/رمز ثابت/endpoint عمومی ممنوع. فقدان کانال یا code هیچ bypass خودکاری ندارد؛ مسیر فیزیکی فقط پس از طراحی ابزار امضاشده و تأیید دو نفره. replay/race/expiry/DB-failure/secret-free-log tests و threat-model review لازم‌اند.

## 5. Security review و G1
پرونده review آماده است، اما review مستقل مخصوص Runtime و جریان‌های IdentityAccess و امضای تصمیم G1 هنوز انجام نشده است. تأییدهای گزارش‌شده در 2026-10-31 در `INDEPENDENT_REVIEW_CHECKLIST_FA.md` مربوط به baseline معماری/امنیتی G0 هستند و به این دامنه G1 تعمیم داده نمی‌شوند. reviewer باید token/digest/revoke، password hash/rehash، lockout/rate limit، bootstrap، session cap/lease، permission enforcement، recovery، audit/retention، TLS/firewall و dependency boundaries را بررسی کند. سبزشدن CI جایگزین review نیست. فرم reviewer باید SHA، یافته‌ها، نتیجه و امضا داشته باشد؛ مالک باید صریحاً G1 را authorize کند. تا آن زمان `g1Authorized=false` و PR #30 Open+Draft+Unmerged.
## 6. تصمیم‌های مالک محصول درباره Recovery و Session — 2026-11-05

بندهای قبلی که TTL/تلاش/ارسال را باز اعلام می‌کردند، با تصمیم‌های زیر جایگزین می‌شوند:
- بازار هدف: ایران؛ ایمیل: mailbox اختصاصی روی دامنه محصول و ارسال از طریق SMTP relay/Transactional Email؛ Mail Server مستقل در برنامه ساخته نمی‌شود.
- SMS عمداً به فاز آینده موکول شده و انتخاب vendor در این مرحله لازم نیست. پیش از فعال‌سازی Recovery باید عامل مستقل جایگزین SMS به تصویب برسد یا SMS در فاز بعدی اضافه و آزمون شود؛ بازیابی ایمیل‌تنها مجاز نیست.
- Email OTP TTL=10 دقیقه؛ SMS OTP TTL=5 دقیقه.
- حداکثر 5 تلاش ناموفق برای هر challenge؛ قفل Recovery به مدت 24 ساعت پس از رسیدن به حد.
- resend cooldown=1 ساعت؛ حداکثر 1 ارسال در rolling 1h و 5 ارسال در rolling 24h؛ عبور از هر سقف، قفل 24 ساعته ارسال/Recovery.
- محدودیت‌ها در سمت سرور و با عملیات اتمیک enforce شوند؛ per-account/channel و per-source. قفل مبدأ به‌تنهایی نباید ابزار DoS علیه قربانی باشد.
- resend proof قبلی همان کانال را باطل می‌کند؛ provider timeout/unknown result proof معتبر ایجاد نمی‌کند.
- قفل 24 ساعته Recovery جایگزین lockout ورود عادی 5 شکست/15 دقیقه و قفل 15 دقیقه نیست.

سیاست نشست مصوب:
- heartbeat=30s؛ lease=180s؛ absolute session lifetime=12h از ایجاد.
- حداکثر 2 نشست فعال برای هر حساب؛ حداکثر 5 حساب فعال متمایز در کل سیستم.
- سقف کل 5 حساب با permission اختصاصی super_admin قابل تغییر و هر تغییر audit می‌شود.
- تغییر رمز و غیرفعال‌سازی حساب، revoke فوری تمام نشست‌های آن حساب را در همان تراکنش تضمین می‌کند؛ درخواست بعدی با بررسی PostgreSQL رد می‌شود.

## 7. وضعیت تصویب ماتریس و Reviewer
تصویب این پارامترها به معنی تصویب ماتریس granular مجوزها یا retention نیست؛ آن موارد همچنان نیازمند تصمیم جداگانه مالک‌اند. تیم مستقلی که مالک پس از بررسی نهایی معرفی/مأمور می‌کند، می‌تواند Reviewer سازمانی G1 باشد؛ اما نام تیم، اعضای واجد صلاحیت، تعارض منافع، reviewer مسئول، SHA تحت بررسی، نتیجه و امضای واقعی باید در پرونده G1 ثبت شود. تا آن زمان review مستقل انجام‌شده محسوب نمی‌شود.


## تعیین دامنه فعلی: عدم پیاده‌سازی پیامک — 2026-11-05
- بنا بر تصمیم مالک، در این مرحله هیچ SMS provider، SDK، API client، credential، template، configuration، background job یا تست وابسته به سرویس پیامک اضافه نمی‌شود.
- قابلیت SMS به مرحله/نسخه آینده موکول است و در این مرحله صرفاً به‌عنوان توسعه آتی مستند می‌شود؛ انتخاب vendor نیز اکنون لازم نیست.
- نبود SMS به معنی مجاز شدن بازیابی با ایمیل تنها نیست. تا زمان تصویب و پیاده‌سازی یک ترکیب عامل بازیابی امن و آزموده‌شده، Recovery اضطراری در محیط عملیاتی غیرفعال/غیرقابل دسترس می‌ماند. عامل offline recovery code نیز به‌تنهایی جایگزین عامل مستقل دوم نمی‌شود مگر آنکه مدل تهدید و سیاست مالک جداگانه تصویب شود.
- مقادیر TTL پیامک و محدودیت ارسال پیامک که پیش‌تر در سند آمده‌اند، مشخصات هدف آینده‌اند و در این مرحله هیچ رفتار اجرایی یا تست runtime برای SMS ایجاد نمی‌کنند.
- ایمیل اختصاصی نیز در این مرحله فقط معماری هدف است؛ SMTP relay واقعی و secretها تا انتخاب/پیکربندی عملیاتی وارد برنامه نمی‌شوند.


## اصلاح وضعیت جاری G1 و Recovery — 2026-11-05

بسته تحویل جاری reviewer در `identityaccess-g1-review-handoff-fa.md` قرار دارد و مرجع وضعیت PR جاری، PR #37 است؛ ارجاع قدیمی PR #30 در متن‌های پیشین وضعیت جاری را نشان نمی‌دهد.

- ماتریس granular permission هنوز پیشنهاد است و تصویب مالک ندارد.
- retention امنیتی هنوز پیشنهاد است و تصویب مالک/بررسی حقوقی ندارد.
- SMS عمداً به فاز آینده موکول شده است؛ انتخاب provider یا تست SMS در این فاز لازم نیست.
- با تعویق SMS، روش اجرایی عامل مستقل جایگزین برای Recovery هنوز تصویب نشده است. بنابراین Recovery ایمیل‌تنها مجاز نیست و Recovery اضطراری غیرفعال باقی می‌ماند.
- وجود طرح/جدول تست Recovery به معنی وجود runtime یا اجرای تست runtime نیست.
- CI موفق فقط به SHA و jobهای همان اجرا مربوط است و جایگزین review مستقل G1 نیست.
- reviewer مستقل، SHA مورد تأیید، یافته‌ها، امضا و تصمیم رسمی مالک هنوز ثبت نشده‌اند؛ `g1Authorized=false`.
