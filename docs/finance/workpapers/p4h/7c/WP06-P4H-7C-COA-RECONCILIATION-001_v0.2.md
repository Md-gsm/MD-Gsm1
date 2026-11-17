# WP06-P4H-7C-COA-RECONCILIATION-001
## Chart of Accounts — Canonical Account Reconciliation — Controlled Replacement

| مشخصه | مقدار |
|---|---|
| Version | 0.2 |
| Classification | CONTROLLED RECONSTRUCTION / CONTROLLED REPLACEMENT |
| Parent | WP06-P4H-7C-COA-RECONCILIATION-001 v0.1 |
| Architecture | MDG-ARCH-MASTER-001 v2.5 |
| Owner decision | OWN-7C-043 and prior effective owner decisions |
| COA design lineage | v1.2 prior baseline → v1.4 current owner-declared baseline; v1.3 intermediate |
| Source workbook | Group Excel v1.0 inside `فهرست جامع و مرجع کدینگ حساب‌ها.zip` |
| Source ZIP SHA-256 | `c17e3c16d05abb9d2ae57b8cfa0c8860e69b613ececb0db7758668cd1db04449` |
| Extracted Excel SHA-256 | `a6ca3377fde5863455a3b4bccf9a000d0dbf23f44bd66eeae51c44d0f3697866` |
| Source scope | 76 unique account codes × 24 columns; 9 workbook sheets |
| Design status | SOURCE VERIFIED; CANONICAL ATTRIBUTE RECONCILIATION PARTIAL / OPEN |
| Implementation / Tests | NOT VERIFIED / NOT RUN |
| Evidence / Posting Release | NOT ACCEPTED / NOT GRANTED |

## 1. هدف و مرجعیت

این سند تحویل‌دادنی کنترل‌شده جایگزین 7C است و اصل تاریخی 7C v0.3 را ادعا نمی‌کند. مرجعیت طراحی COA طبق این سلسله‌مراتب اعمال می‌شود:
1. Architecture v2.5 و قواعد هنجاری آن؛
2. تصمیمات قطعی مالک و ADRهای پذیرفته‌شده؛
3. COA v1.4 به‌عنوان نسخه جاری ساخته‌شده بر پایه v1.2؛
4. COA v1.2 به‌عنوان خط مبنای مستقل قبلی؛
5. COA v1.3 فقط به‌عنوان مرحله میانی؛
6. Excel/TXT v1.0 و workpaperها به‌عنوان منبع/شواهد، نه مرجع نهایی تصمیم.

هر تعارض با تصمیم صریح مالک از طریق lineage ثبت می‌شود؛ متن منبع خام بازنویسی یا حذف نمی‌شود. عدم دسترسی به Excel دیگر مانع نیست؛ فایل در ZIP بازیابی و hash آن احراز شده است.

## 2. رجیستر دامنه 76 حساب منبعی

76 کد یکتا در شیت «دفتر حساب‌ها»ی فایل Excel استخراج‌شده وجود دارد. فهرست کامل:

| گروه | کدها |
|---|---|
| دارایی و دریافتنی | 1101, 1102, 1103, 110302, 110401, 110402, 110403, 110404, 1105, 110601, 110602, 110701, 110702, 110703 |
| موجودی و مالیات/وجوه | 110801, 110802, 110803, 110804, 110805, 110806, 110807, 110808, 1109, 1110 |
| دارایی ثابت | 1201, 1202 |
| بدهی‌ها | 2101, 2102, 2103, 2104, 2105, 2106, 2107, 2108, 2109, 2110, 2111, 2112, 2113 |
| حقوق مالکانه و افتتاحیه | 3101, 3102, 3103, 3104, 3105, 3199 |
| درآمد و کاهنده درآمد | 4101, 4102, 4103, 4104, 4105, 4106, 4107, 4201, 4202, 4203 |
| بهای تمام‌شده | 5101, 5102, 5103, 5104, 5105 |
| هزینه‌ها | 6101, 6102, 6103, 6104, 6105, 6106, 6107, 6108, 610801 |
| زیان/تعدیلات و تاریخی | 6201, 6202, 620201, 6203, 6204, 6205, 110809 |

تعداد کل: 76. کدهای 2114 و 5106 در این فهرست منبعی نیستند و باید با منشأ جداگانه و مطابق تصمیم مالک/ADR در Registry افزوده شوند. 4107 از قبل در 76 کد منبعی وجود دارد.

## 3. نتیجه استخراج و وضعیت صفات

فایل Excel دارای 9 شیت است: دفتر حساب‌ها، راهنما، درخت و سطوح، کنترل‌های ویژه، زیرمعین و ابعاد، موارد باز، واژه‌نامه فیلدها، تصمیمات مالک، منابع و تصمیمات. شیت «دفتر حساب‌ها» 76 ردیف و 24 ستون دارد.

آمار زیر snapshot منبع است، نه مقدار canonical نهایی:

| فیلد | وضعیت مشاهده‌شده در snapshot | تفسیر کنترل‌شده |
|---|---|---|
| AccountId | هر 76 ردیف NOT ASSIGNED | تخصیص فقط در COA Registry مصوب |
| ParentAccountId | هر 76 ردیف NOT ASSIGNED | نباید از prefix تولید شود |
| EffectiveFrom | هر 76 ردیف NOT SET | تاریخ مؤثر نیازمند تصمیم/ثبت نسخه‌ای |
| ParentAccountCode | فقط 3 رابطه صریح | 110302→1103؛ 610801→6108؛ 620201→6202 |
| Postable | 56 UNVERIFIED؛ 8 NO IN R1؛ 6 CONDITIONAL؛ 5 NO RELEASE/source open؛ 1 UNRESOLVED | هیچ‌کدام به‌تنهایی مجوز ثبت نیست |
| DirectPostingAllowed | 72 UNVERIFIED؛ 4 NO | مجوز باید در Registry و سرور اعمال شود |
| SubledgerRequired | 35 YES/CONTROLLED SUBLEDGER؛ 35 NOT SPECIFIED؛ 6 TO BE CONFIRMED | قرارداد هر حساب باید مشخص شود |
| NormalBalance | 28 proposed debit؛ 14 proposed credit؛ 13 undetermined؛ 6 مرتبط با OWN-7C-041/042؛ 3 control/status-dependent؛ 2 two-sided/conditional/other | مقادیر پیشنهادی بدون lineage، canonical نیستند |

## 4. روابط والد مجاز

فقط این سه رابطه ParentAccountCode از فایل منبع به‌صورت صریح قابل احرازند:
- 110302 → 1103
- 610801 → 6108
- 620201 → 6202

برای 110401..110404 و 4201..4203 والد عددی قطعی از متن موجود تعیین نشده است. هرگونه استنتاج از پیشوند کد ممنوع است.

## 5. Dispositionهای حاکم

| کد/گروه | تصمیم حاکم | وضعیت استفاده |
|---|---|---|
| 110801 | تنها GL Inventory Control | کنترل دفترکل؛ جزئیات در Inventory Subledger |
| 110802–110806 | دسته‌های Inventory Subledger | GL مستقل نیستند |
| 110807 | غیرفعال R1؛ حفظ تاریخچه | ثبت جدید ممنوع |
| 110809 | ممنوع | ایجاد/استفاده ممنوع |
| 110602 | غیرفعال تا احراز منبع/ماهیت | No Posting |
| 110701 | Recoverable Purchase Tax فقط با TaxMode/Tax Matrix معتبر؛ Supplier Advance نیست | Conditional |
| 1109/2109 | مالیات فقط با TaxMode و Tax Matrix معتبر؛ TaxMode=0 همه مسیرها را مسدود می‌کند | Conditional / fail closed |
| 2102/2106 | غیرفعال | No Posting |
| 2107 | دریافت ورودی تخصیص‌نیافته؛ Refund Payable نیست | فقط با رویداد معتبر |
| 110703 | پرداخت خروجی تخصیص‌نیافته | از 2107 مستقل |
| 2104 | تسویه موقت موجودی منفی | فقط با source، aging، owner، clearing trigger و evidence |
| 2113 | مازاد شمارش در suspense | درآمد خودکار نیست |
| 2114 | بدهی خدمت دریافت‌شده و تأییدشده ولی فاکتورنشده با تعهد قابل اثبات | OWNER APPROVED — DESIGN ONLY؛ posting blocked تا صفات canonical و FE-20 |
| 1201/1202/6105 | بهای تمام‌شده دارایی/استهلاک انباشته/هزینه استهلاک | دامنه کامل R2 |
| 3199 | کنترل موقت افتتاحیه | نه سرمایه/درآمد و نه حساب ساختاری |
| 3101–3105 | equity mapping و NormalBalance unresolved | No Posting تا توافق حقوق مالکانه |
| 4107/5106 | سود/زیان گردکردن مستقل | DESIGN ONLY؛ collision/eligibility/Posting Matrix و tests لازم |
| 6204/6205 | غیرفعال R1 | No Posting |
| 620201 | غیرفعال R1 | No Posting |
| 110401–110404 | نقش‌های چرخه چک طبق OWN-7C-004-R1 | policy mapping قطعی؛ source title/attributes و hierarchy هنوز crosswalk می‌خواهد |

### 5.1 چرخه چک

OWN-7C-004-R1 مرجع نقش حساب‌هاست:
- 110401: چک نزد صندوق؛
- 110402: چک واگذارشده به بانک؛
- 110403: چک در جریان وصول؛
- 110404: چک برگشتی در انتظار تعیین تکلیف.

این تصمیم بر عنوان پیشنهادی فایل Excel/TXT مقدم است. منبع خام حفظ می‌شود و تفاوت عنوان/attribute به‌عنوان conflict lineage ثبت می‌شود؛ عنوان منبع به‌صورت خاموش بازنویسی نمی‌شود.

### 5.2 حساب‌های افزوده خارج از 76 کد

- 2114: فقط service liability برای خدمت دریافت/تأییدشده و تعهد قابل اثبات؛ ویژگی‌های canonical مادر و FE-20 کامل نشده‌اند.
- 5106: rounding-loss مستقل؛ canonical collision check، NormalBalance، eligibility و FE mapping کامل نشده‌اند.
- 4107/5106 در فهرست فعلی 78 کد تکرار مستقیم ندارند؛ این به‌تنهایی No-Collision کل namespace/registry را اثبات نمی‌کند.

## 6. رجیستر Canonical Account

برای هر حساب، رکورد نهایی باید تمام این فیلدها را تعیین یا با وضعیت صریح unresolved نگه دارد:
`AccountCode, AccountId, PersianTitle, EnglishTitle, ParentAccountCode, ParentAccountId, Level, AccountClass, Nature, NormalBalance, Postable, DirectPostingAllowed, Status, SubledgerRequired, TaxModeConstraint, RecognitionRule, UsageRestrictions, ReconciliationPolicy, SubledgerDimensions, EffectiveFrom, EffectiveTo, OwnerDecisionRef, SourceRef, FEReferences, TestReferences, OpenCondition`.

هیچ مقدار نامعلوم نباید با حدس پر شود. هر حساب NOT VERIFIED/NOT SPECIFIED/INACTIVE/PROHIBITED/RESERVED/DESIGN ONLY برای posting جدید fail closed است.

## 7. موارد باز برای تکمیل 7C

| ID | موضوع | معیار بستن | وضعیت |
|---|---|---|---|
| 7C-OPEN-001 | COA v1.4 row-level crosswalk | تمام 76 کد و فیلدهای v1.4 با Excel/decisions تطبیق داده شوند | OPEN |
| 7C-OPEN-002 | AccountId registry | شناسه پایدار یکتا و immutable با uniqueness constraint | OPEN |
| 7C-OPEN-003 | Parent/Level hierarchy | درخت بدون cycle، با منبع صریح و بدون prefix inference | OPEN |
| 7C-OPEN-004 | Nature/NormalBalance | مقدار canonical و lineage برای هر حساب | OPEN |
| 7C-OPEN-005 | Postable/DirectPostingAllowed/Status | eligibility هر حساب و enforce سمت سرور/DB | OPEN |
| 7C-OPEN-006 | Subledger/Dimensions | قرارداد مالک داده و ابعاد برای هر حساب | OPEN |
| 7C-OPEN-007 | Effective dating | تاریخ اثر و تاریخ پایان/جایگزینی | OPEN |
| 7C-OPEN-008 | 2114 attributes | Parent, Nature, NormalBalance, eligibility, subledger, FE-20 | OPEN |
| 7C-OPEN-009 | 5106/4107 collision | namespace/registry/database uniqueness و eligibility | OPEN |
| 7C-OPEN-010 | 110401..110404 source crosswalk | policy vs source title/attributes و test lifecycle | OPEN |
| 7C-OPEN-011 | 3101..3105 equity | agreement، source، NormalBalance و FE-28/34/38 | OPEN |
| 7C-OPEN-012 | FE↔COA↔Test | نگاشت دوطرفه هر حساب به رویداد و Test ID | OPEN |

## 8. معیار پذیرش

1. 76 source rows و 24 columns با checksum احراز شده و در source lineage ثبت باشند.
2. تمام فیلدهای canonical برای همه کدهای مشمول با v1.4 و تصمیمات حاکم تطبیق داده شده باشند.
3. همه حساب‌های افزوده 2114/5106 با منشأ مستقل و صفات کامل ثبت شوند.
4. هیچ Parent از prefix حدس زده نشده باشد.
5. No-Collision در کل namespace و DB registry اثبات شود.
6. هر حساب به FE و Test متصل باشد؛ حساب بی‌مصرف یا رویداد بی‌حساب تعیین تکلیف شود.
7. هر وضعیت inactive/prohibited/unverified/design-only در server/database fail closed باشد.
8. ممیزی مستقل روی SHA نهایی انجام شود؛ هیچ C0/C1 حل‌نشده در تمامیت سند، provenance، lineage، crosswalk یا سازگاری کنترل‌ها باقی نماند. شکاف واقعی صفات/سیاست حساب می‌تواند باز بماند، مشروط بر ثبت صریح BLOCKED/UNVERIFIED، دلیل، owner action، شرط رفع، Test ID، Evidence plan و منع Posting.

## 9. وضعیت

- Source Excel bytes/hash: VERIFIED.
- 76 source codes / 24 columns: VERIFIED.
- COA design hierarchy: v1.2 prior → v1.4 current; v1.3 intermediate.
- Canonical account attributes: PARTIAL / OPEN.
- AccountId/ParentAccountId/EffectiveFrom: NOT ASSIGNED / NOT SET.
- Implementation: NOT VERIFIED.
- Tests: NOT RUN / NOT VERIFIED.
- Evidence: NOT ACCEPTED.
- Gate 3 / Gate 4: OPEN / OPEN.
- Posting Release: NOT GRANTED.
