# بسته پنج موضوع باز IdentityAccess
آخرین بازنگری: 2026-11-17 | تصمیم‌های مالک درباره ماتریس مجوز، Recovery ایمیل‌تنها و سیاست نشست ثبت شده‌اند؛ پیاده‌سازی، آزمون runtime، بازبینی مستقل و مجوز G1 همچنان بازند.

> **وضعیت جاری:** ماتریس granular در 2026-11-06 تصویب شده است؛ این تصویب به معنی seed یا فعال‌سازی runtime نیست. Recovery ایمیل‌تنها نیز برای فاز فعلی پذیرفته شده و MFA نیست. سیاست نشست جاری: برنامه باز + heartbeat/lease با سقف آغازین 5 حساب فعال متمایز؛ عمر مطلق و سقف نشست به‌ازای هر حساب تصویب نشده‌اند. موارد قدیمی‌تر در این سند که خلاف این خلاصه‌اند، تاریخی و superseded هستند. g1Authorized=false.

## 1. ماتریس granular مجوز
قاعده: deny-by-default؛ فقط permission صریح؛ اجتماع مجوزهای چندنقشی؛ scope مستقل سمت API؛ wildcard ممنوع.
ترتیب نقش‌ها در جدول: super_admin, manager, receptionist, technician, warehouse, salesperson, purchaser, cashier, accountant, viewer.
| مجوز | super_admin | manager | receptionist | technician | warehouse | salesperson | purchaser | cashier | accountant | viewer |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| persons.read | ✓ | ✓ | ✓ | ✓* | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| persons.create | ✓ | ✓ | ✓ | | | ✓ | ✓ | | ✓ | |
| persons.update | ✓ | ✓ | ✓ | | | | | | ✓ | |
| persons.delete | ✓ | | | | | | | | | |
| persons.sensitive.read | ✓ | | | | | | | | | |
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

*تکنسین فقط رکوردهای تخصیص‌یافته. کاتالوگ granular: persons.read/create/update/delete؛ persons.sensitive.read؛ repairs.read/create/update/assign/complete/deliver/worklog.write/parts.consume؛ inventory.read/receipt.create/issue.create/count.execute؛ sales.read/create/price.override؛ purchasing.read/create؛ cash.read/receipt.create/payment.create؛ finance.read/voucher.create/voucher.delete/period.close؛ identity.accounts.read/create/update، identity.credentials.reset، identity.roles.assign، identity.permissions.manage، identity.sessions.revoke، identity.settings.manage، identity.recovery.execute؛ system.settings.read/manage.
manager مجوز مدیریت حساب/نقش/امنیت ندارد. حذف سند نهایی ممنوع و فقط reversal/correction؛ accountant می‌تواند دوره را ببندد ولی بازگشایی در ماتریس نیست. این ماتریس پیشنهاد است؛ تصویب مالک قبل از seed لازم است.

## 2. Retention سوابق امنیتی
پیشنهاد، نه الزام قانونی یا استاندارد جهانی: رخدادهای مدیریتی/حساس 24 ماه online و حذف نهایی 36 ماه؛ ورود/نشست 12 ماه online و حذف 18 ماه؛ پرونده incident تا بسته‌شدن +12 ماه با بازبینی 90 روزه؛ telemetry aggregate برابر 90 روز. آرشیو append-only/immutable با manifest و کلید امضای خارج DB؛ IP خام پیش‌فرض ذخیره نشود؛ audit فقط allow-list metadata و بدون رمز/hash رمز/token/recovery code. legal hold بر purge مقدم است. تصویب مالک و بررسی حقوقی لازم؛ تا آن زمان purge/archival اجرایی نشود.

## 3. Username normalization و آزمون
NFC نه NFKC؛ بدون trim خاموش؛ طول 3..64 Unicode scalar؛ فقط General Category L* و Nd؛ ارقام فارسی/عربی/لاتین حفظ؛ حروف فارسی/عربی ادغام نشوند؛ Latin default case-fold نسخه‌دار؛ collation قطعی؛ mixed-script letters رد و Script_Extensions معتبر الزامی. oracle تست Python در `scripts/ci/test_identityaccess_policy.py` افزوده شده و در spec gate اجرا می‌شود؛ جایگزین تست .NET production نیست. Unicode version pin، collision scan و .NET fuzz tests پیش از runtime لازم‌اند.

## 4. Recovery مدیر کل — سیاست جاری و مرزهای باز

- **تصمیم مصوب مالک:** Recovery ایمیل‌تنها برای فاز فعلی پذیرفته شده است؛ OTP یک‌بارمصرف 10 دقیقه‌ای، digest-only، حداکثر 5 تلاش ناموفق و قفل 24 ساعته. resend حداقل هر 1 ساعت؛ حداکثر 1 ارسال در rolling hour و 5 ارسال در rolling 24h؛ عبور از سقف موجب قفل 24 ساعته می‌شود.
- این روش MFA یا دو عامل مستقل نیست؛ ریسک mailbox compromise، phishing، forwarding و فرایند بازیابی خود mailbox باید در threat model و بازبینی مستقل بررسی شود.
- SMS در این فاز غیرفعال و خارج از دامنه است؛ offline recovery code عامل اجباری یا مسیر bypass نیست.
- **هنوز تعیین‌نشده:** نقطه شروع Recovery (endpoint شبکه‌ای یا کنسول محلی مورد اعتماد)، تعریف مرز اعتماد کنسول در صورت انتخاب، نحوه ثبت/اثبات ایمیل super_admin، SMTP relay واقعی و secret/key management.
- تا تکمیل orchestration، ارسال ایمیل، rate-limit اتمیک، مصرف یک‌باره، تراکنش تغییر رمز + افزایش credential_version + revoke همه نشست‌ها + must_change_password، تست‌های replay/race/expiry/failure و threat-model review، Recovery runtime عملیاتی نیست.
- هیچ backdoor، master password، رمز ثابت دائمی یا bypass پشتیبانی‌محور مجاز نیست. تصمیم bootstrap اولیه از Recovery جداست و provisioning امن آن همچنان باز است.

## 5. Security review و G1
پرونده review آماده است، اما review مستقل روی commit جاری و امضای پذیرش نهایی هنوز ثبت نشده‌اند. reviewer باید token/digest/revoke، password hash/rehash، lockout/rate limit، bootstrap، session cap/lease، permission enforcement، recovery، audit/retention، TLS/firewall و dependency boundaries را بررسی کند. سبزشدن CI جایگزین review نیست. فرم reviewer باید SHA، یافته‌ها، نتیجه و امضا داشته باشد؛ مالک باید صریحاً G1 را authorize کند. تا آن زمان `g1Authorized=false` باقی می‌ماند. PR #31 اکنون Closed و Unmerged است؛ بسته‌شدن PR به معنی حذف کدهای پذیرفته‌شده در شاخه‌های دیگر یا تکمیل IdentityAccess نیست.


## به‌روزرسانی 2026-11-06 — اتصال HTTP ورود

- مسیر `POST /api/v1/identity/login` اکنون به `LoginHandler` متصل شده و توکن opaque را فقط پس از ثبت digest نشست در PostgreSQL بازمی‌گرداند.
- مسیر `POST /api/v1/identity/password/change` اضافه شده است؛ نشست اجباری فقط همین مسیر را می‌تواند فراخوانی کند و پس از تغییر رمز تمام نشست‌ها revoke و credential version افزایش می‌یابد.
- تست سرتاسری با API host واقعی و PostgreSQL موقت اضافه شده است: login HTTP، دریافت token، محدودیت must-change، تغییر رمز، رد token قبلی، ورود مجدد و عبور تا مجوز Persons.
- برای جلوگیری از فعال‌شدن ناخواسته، HTTP login فقط با `IdentityAccess:HttpLoginEnabled=true` فعال می‌شود؛ مقدار پیش‌فرض خاموش است.
- CI فعلی main روی commit `194720455d01a5f88b1996ed9b100b7bc7ec363b` موفق شده است (Persons Module Gates run `42381030785` با ۱۳/۱۳ job، CodeQL run `42381030784` و Project Charter Owner Gate run `42381030735`). این موفقیت جایگزین بازبینی امنیتی مستقل روی commit جاری نیست. هیچ migration تولیدی، bootstrap credential، listener/TLS تولیدی یا G1 authorization فعال نشده است.


## وضعیت PR و Runtime — 2026-11-07

- PR #31 در GitHub با وضعیت Closed و Unmerged ثبت شده است؛ آن را Open+Draft معرفی نکنید.
- مسیرهای HTTP login و password change در main وجود دارند و در تست API/PostgreSQL موقت پوشش داده شده‌اند؛ feature flag ورود HTTP به‌صورت پیش‌فرض خاموش است.
- production bootstrap، recovery نهایی مدیر، deployment listener/TLS، بازبینی مستقل امنیت/حریم خصوصی و مجوز G1 همچنان باز هستند.
## 6. تصمیم‌های جاری مالک — Recovery، Session و Authorization

### Recovery ایمیل‌تنها
- کانال مصوب فعلی: ایمیل؛ OTP یک‌بارمصرف 600 ثانیه، فقط digest در DB.
- حداکثر 5 تلاش ناموفق در هر challenge؛ پس از حد، قفل 24 ساعته.
- حداقل فاصله resend یک ساعت؛ حداکثر 1 ارسال در rolling hour و 5 ارسال در rolling 24h؛ نقض سقف موجب قفل 24 ساعته می‌شود.
- محدودیت‌ها باید سمت سرور، اتمیک و متکی به ساعت PostgreSQL باشند؛ قفل source نباید به‌تنهایی ابزار DoS علیه حساب قربانی شود.
- resend، challenge قبلی همان کانال را باطل می‌کند؛ timeout/نتیجه نامعلوم provider هرگز اثبات موفق محسوب نمی‌شود.
- SMS و offline code در این فاز عامل اجباری نیستند و provider پیامک خارج از دامنه است.

### Session
- نشست تا وقتی برنامه باز است و heartbeat/lease معتبر تمدید می‌شود؛ عمر مطلق ثابت برای نشست تصویب نشده است.
- سقف آغازین 5 حساب فعال متمایز در کل سیستم است؛ تغییر آن فقط با permission اختصاصی و audit.
- heartbeat=30s و lease=180s فقط برای liveness هستند؛ lease با heartbeat معتبر تمدید می‌شود.
- سقف 2 نشست هم‌زمان برای هر حساب تصویب نشده است؛ هیچ absolute_session_ttl یا max_concurrent_sessions_per_account فعالی نباید از متن‌های تاریخی استخراج شود.
- تغییر رمز باید همه نشست‌های همان حساب را revoke کند و credential_version را افزایش دهد؛ غیرفعال‌سازی حساب نیز باید revoke اتمیک نشست‌ها داشته باشد.
- ظرفیت باید در PostgreSQL و در transaction/serialization enforce شود؛ cache محلی مرجع نیست.

### Authorization و وضعیت اجرا
- ماتریس granular در 2026-11-06 تصویب شده؛ خانه خالی Deny، deny-by-default، بدون wildcard و scope مستقل.
- تصویب ماتریس مجوز seed، نقش‌دهی، runtime policy یا مجوز G1 نیست.
- g1Authorized=false؛ runtime/Recovery/seed و migration عملیاتی تا gateهای مربوط مجاز نیستند.

## 7. وضعیت تصویب ماتریس و Reviewer

ماتریس granular در 2026-11-06 تصویب شده است؛ بنابراین هر عبارت قدیمی همین سند که آن را «proposal» یا «منتظر تصویب مالک» معرفی می‌کند، superseded است. این تصمیم به معنی seed یا فعال‌سازی runtime نیست.

بازبینی مستقل امنیتی/معماری روی SHA نهایی، ثبت یافته‌ها و امضای reviewer، بررسی حقوقی retention، تعیین مالک canonical Branch/Warehouse/assignment و تصمیم رسمی مالک برای AUTHORIZE G1 همچنان بازند. CI موفق جایگزین هیچ‌یک از این موارد نیست؛ تا ثبت شواهد، g1Authorized=false باقی می‌ماند.


## سابقه دامنه پیامک — 2026-11-05 (تاریخی)

SMS در این فاز پیاده‌سازی نمی‌شود و provider/SDK/credential/job/test پیامکی اضافه نمی‌شود. عبارت قدیمی این بخش که «Recovery ایمیل‌تنها مجاز نیست» با تصمیم نهایی مالک در 2026-11-06/07 superseded شده است؛ Recovery ایمیل‌تنها برای فاز فعلی پذیرفته شده، اما تا تکمیل کنترل‌های عملیاتی و بازبینی مستقل فعال نمی‌شود.


## وضعیت G1 و Recovery — بازنگری 2026-11-17

تصمیم‌های مالک درباره ماتریس، Recovery ایمیل‌تنها و نشست نهایی‌اند؛ اجرای آن‌ها هنوز اثبات نشده است. runtime موجود در main شامل login و تغییر رمز با feature flag خاموش برای HTTP login است؛ Recovery orchestration/endpoint و bootstrap provisioning کامل وجود ندارد. جزئیات شکاف‌ها در identityaccess-wg2-gap-register-fa.md ثبت شده است. تا review مستقل، تست‌های لازم و AUTHORIZE رسمی، g1Authorized=false باقی می‌ماند.


## تصمیم اصلاحی مالک درباره ایمیل و placeholder پیامک — 2026-11-05 (تاریخی)

این ثبت تاریخی، پذیرش Recovery ایمیل‌تنها و خارج‌بودن SMS از دامنه را ثبت می‌کند. تصمیم نهایی و دقیق‌تر در بخش «تصمیم‌های نهایی مالک — 2026-11-06/07» و خلاصه جاری ابتدای سند است. هر عبارت قدیمی درباره نیاز اجباری به موبایل/offline code یا ممنوعیت ایمیل‌تنها، superseded است.


## اصلاح نهایی وضعیت تصمیم‌ها — 2026-11-06
این الحاقیه بر وضعیت‌های قدیمی‌تر مقدم است:
1. ماتریس مجوزها در 2026-11-06 تصویب شده؛ مرجع `identityaccess-permission-matrix-approval-fa.md` است. seed/runtime همچنان gated هستند.
2. Recovery ایمیل‌تنها برای فاز فعلی پذیرفته شده؛ OTP یک‌بارمصرف ده‌دقیقه‌ای و digest-only. این روش MFA نیست و ریسک mailbox compromise باید مستقل review شود. SMS placeholder آینده و غیرفعال است؛ انتخاب provider blocker این فاز نیست.
3. Retention امنیتی هنوز تصویب مالک/بررسی حقوقی ندارد؛ purge یا archival اجرایی ممنوع است.
4. مالک canonical Branch/Warehouse و assignment با شواهد schema/code احراز نشده؛ scope fail-closed باقی می‌ماند.
5. reviewer واقعی، SHA مورد بررسی، یافته‌ها، نتیجه و امضای قابل راستی‌آزمایی ثبت نشده؛ `g1Authorized=false`.
PR #37 بسته طراحی مستنداتی است. PR #41 شاخه یکپارچه‌سازی جداگانه‌ای است که کد و migration IdentityAccess را پیشنهاد می‌کند؛ هیچ‌یک merge یا مجوز عملیاتی نیستند.
