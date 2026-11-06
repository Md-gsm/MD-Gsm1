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

## 4. Emergency recovery مدیر کل
Break-glass فقط کنسول محلی؛ دو کانال مستقل ایمیل و موبایل به‌علاوه recovery code آفلاین یک‌بارمصرف با entropy حداقل 128 بیت و فقط digest در DB. challengeها کوتاه‌عمر و request-bound؛ rate limit، مصرف اتمیک code، revoke تمام نشست‌ها، افزایش credential_version، must_change_password و fail-closed شبکه. backdoor/رمز ثابت/endpoint عمومی ممنوع. فقدان کانال یا code هیچ bypass خودکاری ندارد؛ مسیر فیزیکی فقط پس از طراحی ابزار امضاشده و تأیید دو نفره. replay/race/expiry/DB-failure/secret-free-log tests و threat-model review لازم‌اند.

## 5. Security review و G1
پرونده review آماده است، اما review مستقل و امضای مالک انجام نشده. reviewer باید token/digest/revoke، password hash/rehash، lockout/rate limit، bootstrap، session cap/lease، permission enforcement، recovery، audit/retention، TLS/firewall و dependency boundaries را بررسی کند. سبزشدن CI جایگزین review نیست. فرم reviewer باید SHA، یافته‌ها، نتیجه و امضا داشته باشد؛ مالک باید صریحاً G1 را authorize کند. تا آن زمان `g1Authorized=false` و PR #31 Open+Draft+Unmerged.
