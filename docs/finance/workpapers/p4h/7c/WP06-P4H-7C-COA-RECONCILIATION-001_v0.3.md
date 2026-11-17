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

| ID | موضوع | Owner action / نقش مسئول | شرط رفع BLOCKED | Test ID | Evidence plan | وضعیت |
|---|---|---|---|---|---|---|
| 7C-OPEN-001 | COA v1.4 row-level crosswalk | Finance / COA Custodian | تطبیق 76 کد و تمام صفات با v1.4 و تصمیمات حاکم؛ ثبت lineage و اختلاف‌ها | MDG-COA-GATE-001 | crosswalk کامل، source refs، diff report و hash | BLOCKED — NO POSTING |
| 7C-OPEN-002 | AccountId registry | Finance + Data Architecture | تخصیص شناسه پایدار یکتا، immutable و constraint؛ بدون reuse | MDG-COA-GATE-002 | registry export، uniqueness test و migration review | BLOCKED — NO POSTING |
| 7C-OPEN-003 | Parent/Level hierarchy | Finance + Data Architecture | تکمیل درخت با روابط مستند، بدون cycle و بدون prefix inference | MDG-COA-GATE-003 | hierarchy graph، cycle test و source lineage | BLOCKED — NO POSTING |
| 7C-OPEN-004 | Nature/NormalBalance | Finance / Accounting Policy | تعیین مقدار canonical و lineage برای هر حساب؛ unresolved باقی‌مانده ممنوع برای posting | MDG-COA-GATE-004 | attribute matrix، policy approval و debit/credit validation | BLOCKED — NO POSTING |
| 7C-OPEN-005 | Postable/DirectPostingAllowed/Status | Finance + Security/Platform | eligibility هر حساب و enforce سمت سرور/DB؛ fail-closed برای unknown/inactive | MDG-COA-GATE-005 | eligibility matrix، negative tests و DB constraint evidence | BLOCKED — NO POSTING |
| 7C-OPEN-006 | Subledger/Dimensions | Finance + مالکان زیرمعین | مالکیت داده، dimensions، reconciliation و atomic update برای هر حساب مشخص شود | MDG-COA-GATE-006 | subledger contract، FK/constraint review و reconciliation tests | BLOCKED — NO POSTING |
| 7C-OPEN-007 | Effective dating | Finance + Data Architecture | EffectiveFrom/To، تغییر نسخه و جلوگیری از retroactive mutation تعریف شود | MDG-COA-GATE-007 | version history، temporal validation و audit log | BLOCKED — NO POSTING |
| 7C-OPEN-008 | 2114 attributes | Finance + Purchasing/RepairOperations | Parent، Nature، NormalBalance، eligibility، subledger و FE-20 نهایی شود؛ تا آن زمان design-only | MDG-COA-GATE-008 | approved account contract، FE-20 crosswalk و negative posting test | BLOCKED — NO POSTING |
| 7C-OPEN-009 | 5106/4107 collision | Finance + Data Architecture | namespace کامل، reserved/history codes، DB registry و eligibility کنترل شود | MDG-COA-GATE-009 | namespace scan، unique constraint proof و collision tests | BLOCKED — NO POSTING |
| 7C-OPEN-010 | 110401..110404 source crosswalk | Finance + Treasury | OWN-7C-004-R1 با source titles/attributes و hierarchy تطبیق؛ تفاوت‌ها lineage شوند | MDG-COA-GATE-010 | source-to-policy mapping، cheque lifecycle cases و audit trail | BLOCKED — NO POSTING |
| 7C-OPEN-011 | 3101..3105 equity | Finance + Product Owner | توافق حقوق مالکانه، source، NormalBalance و FE-28/34/38 پیش از فعال‌سازی | MDG-COA-GATE-011 | signed policy decision، equity crosswalk و negative posting tests | BLOCKED — NO POSTING |
| 7C-OPEN-012 | FE↔COA↔Test | Finance + Source Module Owners | نگاشت دوطرفه هر حساب/رویداد به Test ID؛ orphan account/event ممنوع | MDG-COA-GATE-012 | bidirectional traceability matrix، orphan scan و cross-audit report | BLOCKED — NO POSTING |

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


## 10. رجیستر سطر‌به‌سطر 76 حساب منبعی — v0.2

نسخه `MDG-WP06-COA-76-ROW-SOURCE-REGISTER_v0.2.csv` در Library ثبت و نسخه v0.1 آن superseded شد. این فایل 76 ردیف و 39 ستون دارد: 24 ستون عیناً از شیت «دفتر حساب‌ها» و 15 ستون کنترل canonical status.

- Library path: `/MDG-WP06-COA-76-ROW-SOURCE-REGISTER_v0.2.csv`
- SHA-256 CSV: `78c3793c075ff4aaebef4b86230411d3dca52233f0cca3c81278b2f4607acbb1`
- Source Excel SHA-256: `a6ca3377fde5863455a3b4bccf9a000d0dbf23f44bd66eeae51c44d0f3697866`
- برای هر 76 ردیف، وضعیت CanonicalAccountId، CanonicalParentAccountId، CanonicalLevel، CanonicalNature، CanonicalNormalBalance، CanonicalPostable، CanonicalDirectPostingAllowed، CanonicalSubledgerRequired، CanonicalStatus، CanonicalTaxModeConstraint، CanonicalEffectiveFrom و CanonicalEffectiveTo صریحاً NOT ASSIGNED / UNVERIFIED / NOT SET ثبت شده است.
- PostingStatus برای همه ردیف‌ها: `NO POSTING UNTIL CANONICAL COA + 7B RULE VERIFIED`.
- Owner decision overlay در همین سند و ارجاعات OWN ثبت می‌شود؛ وضعیت UNVERIFIED در CSV به معنی نادیده‌گرفتن تصمیمات مالک نیست، بلکه به معنی تکمیل‌نشدن تطبیق row-level با COA v1.4 است.

این فایل رجیستر کامل source snapshot و وضعیت canonicalization است؛ **COA canonical یا جایگزین v1.4 نیست**. مقادیر canonical فقط پس از تطبیق دقیق با v1.4، تصمیمات مالک و Architecture/ADR قابل ثبت‌اند.


---

## 11. Owner Decision Overlay — Controlled Reconstruction v0.1

### 11.1. مرز این لایه

این لایه، تصمیمات صریح مالک را از source snapshot جدا می‌کند. مبنای آن OWN-7C-043 و برای چرخه چک OWN-7C-004-R1 است. این لایه جایگزین COA v1.4 نیست و به‌تنهایی صفات canonical را تعیین نمی‌کند.

- Source Excel: 76 کد/ردیف، hash احرازشده.
- Owner-added: 2114 و 5106، خارج از 76 ردیف Excel و بر اساس OWN-7C-043.
- Registry decision scope: 78 شناسه؛ 110809 در شمارش شناسه‌ها وجود دارد اما PROHIBITED است و حساب قابل استفاده نیست.
- برای تمام ردیف‌ها، AccountId و ParentAccountId نهایی، Level/Nature/NormalBalance، Postable/DirectPostingAllowed، EffectiveFrom/To و canonical FE eligibility تا تطبیق با baseline جاری COA احراز نشده‌اند.
- هیچ ردیفی با این overlay به‌تنهایی مجوز Posting نمی‌گیرد.

### 11.2. تصمیمات حساب‌محور و guardهای قطعی

| کد | مرجع تصمیم | Disposition مالک | شرط/محدودیت قطعی |
|---|---|---|---|
| 110302 | OWN-7C-043 | کنترل وجوه/مطالبات PSP | تطبیق سه‌طرفه سیستم، PSP و بانک؛ مستقل از وجوه فیزیکی در راه و حساب‌های تخصیص‌نیافته |
| 110401 | OWN-7C-004-R1 | چک دریافت‌شده و نگهداری‌شده در صندوق | Cheque Subledger و lifecycle؛ تصمیم مالک بر عنوان منبع متعارض مقدم است |
| 110402 | OWN-7C-004-R1 | چک ظهرنویسی/منتقل‌شده به بانک | انتقال مستند از 110401 و حفظ lifecycle |
| 110403 | OWN-7C-004-R1 | چک در جریان وصول | وصول بانکی واقعی و قابل تطبیق |
| 110404 | OWN-7C-004-R1 | چک برگشتی در انتظار تعیین تکلیف | بازسازی دریافتنی فقط پس از برگشت واقعی و تأییدشده و فقط یک‌بار |
| 110602 | OWN-7C-043 | غیرفعال تا بازیابی منبع و تعیین ماهیت | حفظ تاریخچه؛ ثبت جدید ممنوع |
| 110701 | OWN-7C-043 | مالیات خرید قابل بازیافت؛ نه پیش‌پرداخت تأمین‌کننده | فقط با شرایط قانونی، TaxMode و Tax Matrix معتبر |
| 110702 | OWN-7C-043 | پیش‌پرداخت فقط با منفعت اقتصادی آتی و معیار شناسایی | سند معتبر و ماهیت اقتصادی واقعی الزامی |
| 110703 | OWN-7C-043 | پرداخت انجام‌شده در انتظار تخصیص؛ مسیر خروجی | با 2107 دریافت تخصیص‌نیافته اشتباه نشود |
| 110801 | OWN-7C-043 | تنها حساب کنترل موجودی در GL | Inventory Subledger باید با GL control تطبیق شود |
| 110802 | OWN-7C-043 | طبقه‌بندی Inventory Subledger، نه حساب مستقل GL | ایجاد حساب کنترل مستقل ممنوع |
| 110803 | OWN-7C-043 | طبقه‌بندی موجودی دستگاه دست‌دوم/Trade-In در Subledger | فقط پس از احراز مالکیت، پذیرش و شناسایی موجودی |
| 110804 | OWN-7C-043 | طبقه‌بندی موجودی قطعات تعمیر در Subledger | تخصیص/نصب به‌تنهایی مصرف مالی نیست |
| 110805 | OWN-7C-043 | طبقه‌بندی وضعیت کالای در راه در Subledger | مالکیت و زمان انتقال مستند باشد |
| 110806 | OWN-7C-043 | طبقه‌بندی کالای معیوب/قرنطینه در Subledger | قرنطینه به‌تنهایی دارایی را حذف نمی‌کند |
| 110807 | OWN-7C-043 | غیرفعال R1 | تاریخچه حفظ؛ ثبت جدید ممنوع |
| 110808 | OWN-7C-043 | حساب کنترلی/تعلیقی کسری موجودی | Direct Posting ممنوع؛ Source، Owner، Reason، Age و Clearing الزامی |
| 1109 | OWN-7C-043 | حساب مالیات خرید، کانال TaxMode-controlled | فقط با TaxMode و Tax Matrix معتبر |
| 1110 | OWN-7C-043 | فقط وجوه نقد فیزیکی در راه | مستقل از PSP/110302 و حساب‌های تخصیص‌نیافته |
| 1201 | OWN-7C-043 | بهای تمام‌شده دارایی ثابت | Fixed Asset Subledger؛ چرخه عملیاتی R2 و تابع Gate |
| 1202 | OWN-7C-043 | استهلاک انباشته دارایی ثابت | Contra-asset؛ هزینه استهلاک نیست؛ چرخه عملیاتی R2 |
| 2101 | OWN-7C-043 | بدهی فاکتوردار تأمین‌کننده | Supplier Subledger؛ از 2103 و 2114 مستقل |
| 2102 | OWN-7C-043 | غیرفعال | حفظ تاریخچه؛ ثبت جدید ممنوع |
| 2103 | OWN-7C-043 | GRNI کالای فیزیکی دریافت‌شده و فاکتورنشده | برای خدمات استفاده نشود؛ از 2114 مستقل |
| 2104 | OWN-7C-043 | تسویه موقت فنی موجودی منفی | بدهی خارجی، دارایی یا درآمد نیست؛ موقت/سن‌بندی‌شده؛ Normal Side باز |
| 2105 | OWN-7C-043 | پیش‌دریافت مرتبط با سفارش/تعهد مشخص | درآمد فقط پس از تحقق؛ از 2107 مستقل |
| 2106 | OWN-7C-043 | غیرفعال تا بازیابی منبع | حفظ تاریخچه؛ ثبت جدید ممنوع |
| 2107 | OWN-7C-043 | وجوه دریافت‌شده ناشناس/تخصیص‌نیافته | Refund Payable نیست؛ پس از شناسایی به تعهد درست تخصیص یابد |
| 2109 | OWN-7C-043 | حساب مالیات پرداختنی، کانال TaxMode-controlled | فقط با TaxMode و Tax Matrix معتبر |
| 2112 | OWN-7C-043 | کنترل اضافه صندوق مثبت در انتظار بررسی | درآمد خودکار نیست؛ اختلاف مستند و clearing لازم |
| 2113 | OWN-7C-043 | کنترل مازاد مثبت شمارش موجودی | درآمد/بدهی خودکار نیست؛ از 110808، 2104 و 620201 تفکیک شود |
| 3101–3105 | OWN-7C-043 | مسدود تا تطبیق COA و قرارداد حقوق مالکانه | هیچ Posting تا تصویب mapping و NormalBalance |
| 3199 | OWN-7C-043 | کنترل موقت افتتاحیه | نه گره ساختاری، نه سرمایه و نه درآمد؛ انتقال فقط با مدرک و تأیید |
| 4107 | OWN-7C-043 | حساب مستقل سود گردکردن | فعال‌سازی پس از No-Collision و Posting Matrix؛ ±1 IRR/document |
| 620201 | OWN-7C-043 | غیرفعال تا بازیابی منبع و تعیین نقش | حفظ تاریخچه؛ با 110808 دوباره‌شماری نشود |
| 6204–6205 | OWN-7C-043 | غیرفعال R1؛ تاریخچه حفظ | حذف/ادغام/انتقال خودکار/Posting جدید ممنوع؛ 6205 زیان گردکردن نیست |
| 110809 | OWN-7C-043 | PROHIBITED | ایجاد در COA، Seed، Migration، Import یا Posting ممنوع |
| 2114 | OWN-7C-043 | OWNER-ADDED: بدهی خدمت دریافت‌شده و تأییدشده ولی فاکتورنشده | تعهد پرداخت قابل اثبات لازم؛ از 2101 و 2103 مستقل؛ PO به‌تنهایی بدهی/هزینه نیست |
| 5106 | OWN-7C-043 | OWNER-ADDED: زیان گردکردن مستقل از 4107 | No-Collision در کل namespace و Posting Matrix؛ ±1 IRR/document؛ 6205 جایگزین نیست |

### 11.3. وضعیت canonical پس از اعمال owner overlay

این overlay فقط disposition/guardهای صریح مالک را تثبیت می‌کند. موارد زیر همچنان برای هر 78 شناسه نیازمند تطبیق و ثبت مستقل‌اند:

1. AccountId و ParentAccountId نهایی و درخت بدون استنتاج از prefix؛
2. Level، AccountClass و Nature؛
3. NormalBalance؛
4. Postable و DirectPostingAllowed به‌صورت دو کنترل مستقل؛
5. SubledgerRequired و مالک تفصیل؛
6. EffectiveFrom/EffectiveTo و version lineage؛
7. Tax treatment و FE eligibility؛
8. No-Collision برای 4107/5106 و کل namespace؛
9. crosswalk نهایی با 7B و expected journalهای مثبت 7D.

در نتیجه، این overlay سطح تصمیم مالک را از صفات canonical جدا می‌کند؛ هیچ UNVERIFIED را به مقدار قطعی تبدیل نمی‌کند.

### 11.4. کنترل پذیرش و وضعیت

- Source-row coverage: 76/76 rows from verified Excel source.
- Owner-added coverage: 2114 and 5106 explicitly registered outside source Excel.
- Decision registry: 78 identifiers, including prohibited 110809; 110809 is not an active account.
- Owner decisions: applied as a separate overlay; source titles/attributes are not silently overwritten.
- Canonical reconciliation to the user's current COA baseline: OPEN pending exact row-level mapping of canonical attributes.
- Test execution: NOT RUN / NOT VERIFIED.
- Evidence: NOT ACCEPTED.
- Gate 3 / Gate 4: OPEN / OPEN.
- Posting Release: NOT GRANTED.

Status: CONTROLLED RECONSTRUCTION — DRAFT v0.3; supersedes v0.2 as the latest 7C workpaper draft only after review; not a posting release or final canonical COA.
