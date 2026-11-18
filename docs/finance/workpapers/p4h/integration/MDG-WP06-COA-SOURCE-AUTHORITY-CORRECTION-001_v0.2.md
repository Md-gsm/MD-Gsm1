# MDG-WP06-COA-SOURCE-AUTHORITY-CORRECTION-001
## اصلاحیه قطعی سلسله‌مراتب مرجع COA و تعیین مبنای فعال 7C

| Field | Value |
|---|---|
| Document ID | MDG-WP06-COA-SOURCE-AUTHORITY-CORRECTION-001 |
| Version | 0.2 |
| Status | OWNER-DECLARED SOURCE HIERARCHY — CONTROLLED CORRECTION |
| Supersedes | v0.1 |
| Architecture | MDG-ARCH-MASTER-001 v2.5 |
| Owner decisions | OWN-7C-043 and previously accepted owner decisions |
| Posting Release | NOT GRANTED |

## 1. تصمیم مرجعیت

سلسله‌مراتب معتبر COA برای ادامه کار:

1. MDG-ARCH-MASTER-001 v2.5 و قواعد هنجاری آن؛
2. تصمیمات قطعی مالک و ADRهای پذیرفته‌شده؛
3. COA v1.2 — سند مادر و مرجع اصلی کدینگ حساب‌ها و قواعد دفترکل؛
4. COA v1.4 — رجیستر تکمیلی جاری و مبنای تطبیق حساب‌به‌حساب 7C بر پایه v1.2؛
5. COA v1.3 — مرحله میانی مشتق‌شده، نه مرجع مستقل جاری.

این سلسله‌مراتب، تصمیم صریح مالک برای کار جاری است و بر عبارت‌های متعارض نسخه‌های قبلی workpaperها مقدم است.

## 2. دامنه COA جاری

- COA v1.2 رجیستر 76 کد پایه را در بر دارد.
- COA v1.4 دو شناسه تکمیلی 2114 و 5106 را بر مبنای تصمیمات معتبر مالک به دامنه تطبیق اضافه می‌کند.
- دامنه فعال 7C برابر 78 شناسه است.
- 110809 شناسه ممنوع است و حساب قابل ایجاد، استفاده یا ثبت نیست.
- روابط والد صریح: 110302→1103، 610801→6108، 620201→6202؛ برای سایر کدها parent از prefix استنتاج نمی‌شود.
- OWN-7C-043 و تصمیمات معتبر قبلی برای dispositionها و محدودیت‌های حساب‌محور اعمال می‌شوند.

## 3. مبنای فعال تکمیل 7C

تطبیق فعال باید بر COA v1.2، رجیستر تکمیلی v1.4، Architecture v2.5 و تصمیمات پذیرفته‌شده مالک متکی باشد. هر AccountId، ParentAccountId، Level، Nature، NormalBalance، Postable، DirectPostingAllowed، EffectiveDate، Subledger، TaxTreatment یا FE eligibility که این منابع صریحاً تعیین نکرده‌اند، باید NOT VERIFIED / NOT SPECIFIED باقی بماند. تکمیل این فیلدها با حدس، prefix inference یا صرف وجود کد ممنوع است.

## 4. اثر بر workpaperهای قبلی

این نسخه، هر عبارت متعارض درباره «v1.4 به‌عنوان سند مادر»، «v1.2 به‌عنوان نسخه قبلی مستقل»، یا وابستگی 7C به منبع جانبی/استخراجی را برای تصمیم‌گیری جاری supersede می‌کند. اسناد قدیمی‌تر صرفاً سابقه ممیزی‌اند و منبع فعال یا شرط پذیرش نیستند.

- 7C v0.3 با 7C v0.4 جایگزین می‌شود.
- Integrated v0.5 با Integrated v0.6 جایگزین می‌شود.
- 5-Stage Report v0.1 با v0.2 جایگزین می‌شود.
- Source Recovery v0.1 و Account Disposition v0.1 برای تعیین مرجعیت جاری superseded هستند.
- Cross-Audit v0.3 و Audit Closure v0.2 فقط شواهد تاریخی‌اند و هر statement متعارض در آن‌ها تابع این اصلاحیه است.

## 5. وضعیت فنی

- COA authority hierarchy: VERIFIED.
- 78-ID scope: VERIFIED.
- Canonical account attributes: PARTIAL / OPEN.
- Positive 7B Posting Rules: 0 accepted.
- 7D: 67 Test IDs specified; end-to-end runtime execution NOT RUN.
- O-01: OPEN.
- Gate 3 / Gate 4: OPEN / OPEN.
- Evidence: NOT ACCEPTED.
- Posting Release: NOT GRANTED.

این اصلاحیه هیچ حساب یا Posting Rule را فعال نمی‌کند و هیچ مجوز ادغام یا ثبت مالی صادر نمی‌کند.
