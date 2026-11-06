# ماتریس مصوب مجوزهای IdentityAccess — نسخه مصوب مالک

- تاریخ: 2026-11-05
- وضعیت: **تصویب‌شده توسط مالک محصول؛ اجرای runtime همچنان منوط به G1 مستقل و مجوز اجرایی جداگانه است**
- اصل حاکم: deny-by-default، grant صریح، scope مستقل برای هر permission، بدون wildcard.
- نقش‌ها: super_admin، manager، receptionist، technician، warehouse، salesperson، purchaser، cashier، accountant، viewer.
- علامت ✓ یعنی grant پیشنهادی؛ خانه خالی یعنی Deny. این ماتریس به‌تنهایی مجوز اجرا نیست.

## 1. قواعد تفسیر

1. هر ردیف یک permission مستقل است؛ داشتن permission خواندن، مجوز نوشتن/تأیید/حذف/چاپ/export ایجاد نمی‌کند.
2. scope باید کنار هر grant ثبت شود: `self`، `assigned`، `branch:<id>`، `warehouse:<id>` یا `all`. مقدار `all` فقط با grant صریح همان permission مجاز است؛ wildcard نیست.
3. تکنسین برای داده‌های تعمیر فقط `assigned` و برای داده شخصی خودش `self` دارد؛ client نمی‌تواند scope را تعیین کند.
4. چندنقشی فقط union مجوزهای صریح همان permission را می‌سازد؛ scopeها از permissionهای دیگر به ارث نمی‌رسند.
5. هر permission ناشناخته، scope نامعتبر، metadata ناقص، خطای policy یا خطای منبع authoritative برابر Deny است.
6. نقش‌ها به‌تنهایی کاربر ایجاد نمی‌کنند. super_admin نیز از ثبت audit، کنترل separation-of-duties و منع عملیات غیرقابل‌بازگشت مستثنا نیست.
7. حذف اسناد مالی/انبار قطعی ممنوع است؛ اصلاح از طریق reversal/correction و permission مستقل انجام می‌شود.
8. مشاهده فهرست یک منبع، مجوز مشاهده محتوای همه رکوردهای آن نیست.

## 2. ماتریس اشخاص و مشتریان

| Permission | super_admin | manager | receptionist | technician | warehouse | salesperson | purchaser | cashier | accountant | viewer |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| persons.read | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| persons.create | ✓ | ✓ | ✓ |  |  | ✓ | ✓ |  | ✓ |  |
| persons.update | ✓ | ✓ | ✓ |  |  |  |  |  | ✓ |  |
| persons.archive | ✓ |  |  |  |  |  |  |  |  |  |
| persons.merge | ✓ |  |  |  |  |  |  |  |  |  |
| persons.export | ✓ | ✓ |  |  |  |  |  |  | ✓ |  |

پیشنهاد scope: technician فقط `self/assigned` برای اطلاعات تماس لازم جهت کار؛ سایر نقش‌ها `branch` یا `all` بر اساس شعبه مجاز. export فقط با scope مستقل و audit.

## 3. تعمیرات و گردش کار

| Permission | super_admin | manager | receptionist | technician | warehouse | salesperson | purchaser | cashier | accountant | viewer |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| repairs.read | ✓ | ✓ | ✓ | ✓ |  |  |  |  | ✓ | ✓ |
| repairs.create | ✓ | ✓ | ✓ |  |  |  |  |  |  |  |
| repairs.update | ✓ | ✓ | ✓ |  |  |  |  |  |  |  |
| repairs.assign | ✓ | ✓ |  |  |  |  |  |  |  |  |
| repairs.complete | ✓ | ✓ |  | ✓ |  |  |  |  |  |  |
| repairs.worklog.write | ✓ | ✓ |  | ✓ |  |  |  |  |  |  |
| repairs.parts.consume | ✓ | ✓ |  | ✓ |  |  |  |  |  |  |
| repairs.deliver | ✓ | ✓ | ✓ |  |  |  |  |  |  |  |
| repairs.cancel | ✓ | ✓ |  |  |  |  |  |  |  |  |
| repairs.export | ✓ | ✓ |  |  |  |  |  |  | ✓ |  |

تکنسین فقط تعمیرات دارای assignment فعال و authoritative خودش را می‌خواند/تکمیل می‌کند؛ تغییر شناسه repair یا assignment در درخواست client مجوز ایجاد نمی‌کند.

## 4. انبار و کالا

| Permission | super_admin | manager | receptionist | technician | warehouse | salesperson | purchaser | cashier | accountant | viewer |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| inventory.read | ✓ | ✓ |  | ✓ | ✓ | ✓ | ✓ |  | ✓ | ✓ |
| inventory.item.create | ✓ | ✓ |  |  | ✓ |  | ✓ |  |  |  |
| inventory.item.update | ✓ | ✓ |  |  | ✓ |  | ✓ |  |  |  |
| inventory.receipt.create | ✓ | ✓ |  |  | ✓ |  | ✓ |  |  |  |
| inventory.issue.create | ✓ | ✓ |  |  | ✓ |  |  |  |  |  |
| inventory.transfer.create | ✓ | ✓ |  |  | ✓ |  |  |  |  |  |
| inventory.count.execute | ✓ | ✓ |  |  | ✓ |  |  |  |  |  |
| inventory.adjust.approve | ✓ | ✓ |  |  |  |  |  |  |  |  |
| inventory.export | ✓ | ✓ |  |  | ✓ |  | ✓ |  | ✓ |  |

scope همه عملیات انبار باید `warehouse` و در صورت نیاز `branch` باشد. ثبت و تأیید تعدیل موجودی از هم جدا هستند؛ شخص ثبت‌کننده نباید همان تعدیل حساس را به‌تنهایی تأیید کند.

## 5. فروش، خرید، صندوق و مالی

| Permission | super_admin | manager | receptionist | technician | warehouse | salesperson | purchaser | cashier | accountant | viewer |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| sales.read | ✓ | ✓ |  |  |  | ✓ |  | ✓ | ✓ | ✓ |
| sales.create | ✓ | ✓ |  |  |  | ✓ |  |  |  |  |
| sales.price.override | ✓ |  |  |  |  |  |  |  |  |  |
| sales.discount.approve | ✓ | ✓ |  |  |  |  |  |  |  |  |
| sales.refund.approve | ✓ | ✓ |  |  |  |  |  |  |  |  |
| purchasing.read | ✓ | ✓ |  |  | ✓ |  | ✓ |  | ✓ | ✓ |
| purchasing.create | ✓ | ✓ |  |  |  |  | ✓ |  |  |  |
| purchasing.approve | ✓ | ✓ |  |  |  |  |  |  |  |  |
| cash.read | ✓ | ✓ |  |  |  |  |  | ✓ | ✓ | ✓ |
| cash.receipt.create | ✓ | ✓ |  |  |  |  |  | ✓ |  |  |
| cash.payment.create | ✓ | ✓ |  |  |  |  |  | ✓ |  |  |
| cash.reconcile | ✓ | ✓ |  |  |  |  |  |  | ✓ |  |
| finance.read | ✓ | ✓ |  |  |  |  |  |  | ✓ | ✓ |
| finance.voucher.create | ✓ |  |  |  |  |  |  |  | ✓ |  |
| finance.voucher.post | ✓ | ✓ |  |  |  |  |  |  | ✓ |  |
| finance.voucher.reverse | ✓ | ✓ |  |  |  |  |  |  | ✓ |  |
| finance.period.close | ✓ |  |  |  |  |  |  |  | ✓ |  |
| finance.period.reopen | ✓ |  |  |  |  |  |  |  |  |  |
| finance.export | ✓ | ✓ |  |  |  |  |  |  | ✓ |  |

مجوزهای approve/post/reverse/close/reopen حساس‌اند و باید audit شوند. جداسازی ثبت‌کننده از تأییدکننده و کنترل دوره مالی در لایه دامنه/سرویس و DB enforce شود. مدیر بدون grant مستقل، مجوز identity administration ندارد.

## 6. داشبورد، گزارش و خروجی

| Permission | super_admin | manager | receptionist | technician | warehouse | salesperson | purchaser | cashier | accountant | viewer |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| dashboard.read | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| dashboard.configure.self | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| dashboard.assign | ✓ | ✓ |  |  |  |  |  |  |  |  |
| reports.read | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| reports.export | ✓ | ✓ |  |  | ✓ |  | ✓ |  | ✓ |  |
| reports.print | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |

هر widget/report باید permission داده منبع را نیز enforce کند؛ مجوز dashboard.read مجوز خواندن داده‌های حساس نیست. تکنسین فقط گزارش‌های `self/assigned` را می‌بیند.

## 7. مدیریت هویت و تنظیمات

| Permission | super_admin | manager | receptionist | technician | warehouse | salesperson | purchaser | cashier | accountant | viewer |
|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| identity.accounts.read | ✓ | ✓ |  |  |  |  |  |  |  |  |
| identity.accounts.create | ✓ |  |  |  |  |  |  |  |  |  |
| identity.accounts.update | ✓ |  |  |  |  |  |  |  |  |  |
| identity.credentials.reset | ✓ |  |  |  |  |  |  |  |  |  |
| identity.roles.assign | ✓ |  |  |  |  |  |  |  |  |  |
| identity.permissions.manage | ✓ |  |  |  |  |  |  |  |  |  |
| identity.sessions.revoke | ✓ |  |  |  |  |  |  |  |  |  |
| identity.sessions.settings.manage | ✓ |  |  |  |  |  |  |  |  |  |
| identity.recovery.execute | ✓ |  |  |  |  |  |  |  |  |  |
| system.settings.read | ✓ | ✓ |  |  |  |  |  |  |  |  |
| system.settings.manage | ✓ |  |  |  |  |  |  |  |  |  |
| audit.security.read | ✓ |  |  |  |  |  |  |  |  |  |

هر reset/role change/permission change/session revoke باید دلیل، actor، target، timestamp و correlation ID مجاز را audit کند؛ secret یا token در audit ممنوع است. کاربر نمی‌تواند مجوزهای خود را ارتقا دهد. تغییرات دسترسی super_admin نیز باید تحت کنترل مستقل و audit غیرقابل‌دستکاری باشد.

## 8. Scope پیشنهادی پیش‌فرض

| نقش | Scope پیشنهادی |
|---|---|
| super_admin | فقط برای هر permission دارای grant صریح، `all`؛ بدون wildcard |
| manager | branchهای مجاز؛ مجوز مدیریتی خارج از ماتریس ندارد |
| receptionist | branch؛ repair/personهای عملیاتی لازم |
| technician | `self` و `assigned`؛ بدون all |
| warehouse | warehouseهای مجاز و branch مربوط |
| salesperson | branch؛ قیمت/مشتری فقط در حوزه فروش |
| purchaser | branch و warehouseهای مرتبط با خرید |
| cashier | صندوق/branch تخصیص‌یافته |
| accountant | branchهای مجاز و دوره مالی مجاز |
| viewer | فقط read grantهای صریح و scope محدود؛ هیچ write |

## 9. مواردی که مالک باید صریحاً تصویب/اصلاح کند

1. آیا تمام grantهای علامت‌خورده ✓ مورد تأیید هستند؟
2. آیا manager باید مجوز approve برای تخفیف/مرجوعی/خرید/ثبت قطعی سند داشته باشد یا هر مورد نیازمند permission جداگانه است؟
3. آیا receptionist مجاز به archive/merge اشخاص نیست و فقط super_admin این اختیار را دارد؟
4. آیا accountant مجاز به voucher.post/reverse و period.close است، با جداسازی وظایف؟
5. آیا viewer به گزارش‌ها و داشبوردهای صرفاً read با scope محدود دسترسی داشته باشد؟
6. آیا super_admin برای همه grantهای صریح scope all داشته باشد یا بعضی حوزه‌ها نیز محدود به branch بمانند؟
7. تعیین حد آستانه مبلغی برای price override، discount approve، refund approve و purchase approve؛ تا تعیین آستانه، این عملیات باید Deny یا نیازمند تأیید super_admin باشند.
8. تعیین سیاست دسترسی چندشعبه‌ای و کاربرانی که بیش از یک branch/warehouse دارند.

## 10. ثبت تصمیم مالک

- نتیجه: APPROVE / APPROVE WITH CHANGES / REJECT
- نسخه ماتریس:
- SHA دقیق:
- موارد تغییرخواسته:
- نام و سمت تصمیم‌گیر:
- تاریخ:
- امضا/ثبت قابل راستی‌آزمایی:

تا ثبت تصمیم صریح، این ماتریس proposal است و نباید به seed، migration یا runtime policy تبدیل شود.


## 11. ثبت تصویب مالک محصول — 2026-11-06

- نتیجه: **APPROVE**
- تصمیم‌گیر: مالک محصول؛ تأیید صریح در گفت‌وگوی ChatGPT در تاریخ 2026-11-06؛ بدون ادعای امضای دیجیتال.
- موارد تغییرخواسته در grants و قواعد scope: هیچ‌کدام.
- دامنه تصویب: تمام grantهای علامت‌خورده، Deny خانه‌های خالی، deny-by-default، scope مستقل، محدودیت تکنسین به self/assigned، union صریح چندنقشی، separation-of-duties و audit.
- محدودیت‌ها: تا تعیین threshold مالی price override/discount/refund/purchase approval با آستانه نامشخص Deny است. branch/warehouse فقط با assignment صریح و authoritative مجاز است.
- این تأیید، تصویب سیاست محصول است؛ مجوز seed، migration، runtime یا عبور از G1 نیست.


## وضعیت نهایی تصویب — 2026-11-06
این ماتریس توسط مالک محصول در 2026-11-06 تصویب شده است. دامنه تصویب: grantهای علامت‌خورده، Deny خانه‌های خالی، deny-by-default، scope مستقل برای هر permission، union صریح چندنقشی، محدودیت technician به self/assigned، separation-of-duties و audit. امضای دیجیتال/گواهی بیرونی ادعا نمی‌شود. تغییر آینده grants/scopeها نیازمند نسخه و تصویب مجدد است.
این تصویب صرفاً سیاست محصول است؛ seed، migration، runtime، G1 یا اجازه merge ایجاد نمی‌کند. thresholdهای مالی نامشخص و scope شعبه/انبار بدون assignment authoritative همچنان Deny می‌مانند.
