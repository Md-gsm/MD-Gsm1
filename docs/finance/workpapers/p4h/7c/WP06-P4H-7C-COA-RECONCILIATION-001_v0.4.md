# WP06-P4H-7C-COA-RECONCILIATION-001
## Chart of Accounts — Canonical Account Reconciliation — Controlled Replacement

| مشخصه | مقدار |
|---|---|
| Version | 0.4 |
| Classification | CONTROLLED RECONSTRUCTION / CONTROLLED REPLACEMENT |
| Parent | WP06-P4H-7C-COA-RECONCILIATION-001 v0.3 |
| Architecture | MDG-ARCH-MASTER-001 v3.0.4 (current); v3.0.3 historical |
| Owner decision | OWN-7C-043 and prior effective owner decisions |
| COA authority | v1.2 master baseline; v1.5 controlled supplementary reconstruction/crosswalk; v1.4 historical/superseded; v1.3 intermediate only |
| Source register | COA v1.2 master + COA v1.5 supplementary register + accepted owner decisions |
| COA register scope | 78 identifiers: 76 RECON codes + owner-added 2114 and 5106; 110809 prohibited |
| Design status | OWNER SOURCE HIERARCHY VERIFIED; CANONICAL ATTRIBUTE RECONCILIATION PARTIAL / OPEN |
| Implementation / Tests | NOT VERIFIED / NOT RUN |
| Evidence / Posting Release | NOT ACCEPTED / NOT GRANTED |

## 1. هدف و مرجعیت

این سند Controlled Replacement برای تکمیل 7C است. سلسله‌مراتب معتبر:

1. Architecture v3.0.4 و قواعد هنجاری آن؛
2. تصمیمات قطعی مالک و ADRهای پذیرفته‌شده؛
3. COA v1.2 به‌عنوان سند مادر و مرجع اصلی کدینگ و قواعد دفترکل؛
4. COA v1.5 به‌عنوان رجیستر تکمیلی جاری و مبنای تطبیق حساب‌به‌حساب بر پایه v1.2؛
5. COA v1.3 فقط مرحله میانی مشتق‌شده است و مرجع مستقل جاری نیست.

هیچ AccountId، Parent، Level، Nature، NormalBalance، Postable، DirectPostingAllowed، تاریخ اثر یا FE eligibility بدون منبع/تصمیم صریح ساخته نمی‌شود. هر مقدار تعیین‌نشده OPEN / NOT VERIFIED و برای Posting غیرمجاز باقی می‌ماند.

## 2. رجیستر دامنه 78 شناسه

COA v1.2 رجیستر 76 کد پایه را در بر دارد. COA v1.5 دو شناسه تکمیلی 2114 و 5106 را بر مبنای تصمیمات معتبر مالک به دامنه تطبیق اضافه می‌کند. فهرست 76 کد پایه:

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

دامنه تطبیق 78 شناسه است: 76 کد پایه + 2114 + 5106. شناسه 110809 در فهرست پایه وجود دارد اما ممنوع است و حساب قابل استفاده یا ثبت نیست.

## 3. وضعیت صفات Canonical بر مبنای v1.2 و v1.4

وجود شناسه در رجیستر به معنی تکمیل صفات Canonical یا مجوز Posting نیست. وضعیت فیلدها فقط به میزان تصریح‌شدن در COA v1.2، رجیستر تکمیلی v1.4 و تصمیمات مالک پذیرفته می‌شود.

| فیلد | آنچه صریحاً تثبیت شده | وضعیت تکمیل |
|---|---|---|
| AccountId | الزام شناسه پایدار و یکتا در مدل داده | تخصیص شناسه برای همه 78 مورد هنوز اثبات نشده؛ OPEN |
| ParentAccountId | فقط سه رابطه صریح در بخش 4 | سایر والدها بدون سند؛ NOT VERIFIED |
| Level | باید با ParentAccountId و درخت معتبر سازگار باشد | برای موارد فاقد رابطه صریح باز؛ NOT VERIFIED |
| Nature / NormalBalance | برای هر حساب باید مقدار مصوب و منبع داشته باشد | هر مورد فاقد مقدار صریح BLOCKED |
| Postable / DirectPostingAllowed | دو کنترل مستقل؛ مجوز باید سمت سرور enforce شود | وضعیت حساب‌به‌حساب هنوز کامل نیست؛ BLOCKED |
| Status / EffectiveFrom / EffectiveTo | تاریخ اثر و وضعیت نسخه‌دار الزامی است | موارد فاقد تاریخ/تصمیم صریح OPEN |
| SubledgerType / Dimensions | GL محدود؛ جزئیات در زیرمعین | قرارداد همه حساب‌ها تکمیل نشده؛ OPEN |
| FinancialEventEligibility | رویدادهای مجاز/ممنوع باید صریح باشند | crosswalk دوطرفه 7B↔7C ناقص؛ OPEN |
| TaxMode / TaxTreatment | TaxMode=0 تمام Tax Line/Posting را می‌بندد؛ حالت مثبت نیازمند Tax Matrix معتبر است | برای حساب‌های فاقد تصمیم/ماتریس معتبر BLOCKED |
| Recognition / Reconciliation / Aging / Statement Mapping | باید در رکورد Canonical و منبع‌دار باشد | هر مقدار نامشخص OPEN / NOT VERIFIED |

این وضعیت، فهرست کمبودهای واقعی است؛ هیچ مقدار Canonical از روی عنوان یا پیش‌شماره حساب حدس زده نمی‌شود.

## 4. روابط والد مجاز

فقط این سه رابطه ParentAccountCode در COA v1.5 به‌صورت صریح قابل احرازند:
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

این تصمیم بر هر عنوان متعارض یا پیشنهاد گروهی مقدم است. عنوان مصوب و عنوان منبع در lineage نگهداری می‌شوند؛ بازنویسی خاموش عنوان یا attribute ممنوع است.

### 5.2 شناسه‌های تکمیلی v1.4

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
| 7C-OPEN-001 | Account-level canonical attribute reconciliation | Finance / COA Custodian | تطبیق 78 شناسه با v1.2 master، v1.5 supplementary register و تصمیمات مالک؛ تکمیل صفات فقط در صورت وجود منبع؛ unresolvedهای واقعی صریح بمانند | MDG-COA-GATE-001 | account crosswalk، source/decision lineage، unresolved register و diff report | OPEN — ATTRIBUTE GAPS; NO POSTING |
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

1. هر 78 شناسه با COA v1.2، رجیستر تکمیلی v1.4 و تصمیمات مالک تطبیق داده شده باشد.
2. برای هر فیلد Canonical مقدار VERIFIED همراه SourceRef/DecisionRef داشته باشد؛ یا به‌صورت صریح unresolved و BLOCKED ثبت شود.
3. 2114 و 5106 با منشأ تصمیم مالک و همه صفات لازم تعیین تکلیف شوند.
4. هیچ Parent از prefix یا عنوان حساب حدس زده نشده باشد.
5. No-Collision در کل namespace و DB registry اثبات شود.
6. هر حساب به FE و Test متصل باشد؛ orphan account/event تعیین تکلیف شود.
7. هر وضعیت inactive/prohibited/unverified/design-only در server/database fail closed باشد.
8. ممیزی مستقل روی SHA نهایی انجام شود؛ شکاف واقعی سیاست/صفات می‌تواند باز بماند، مشروط بر ثبت دلیل، owner action، شرط رفع، Test ID، Evidence plan و منع Posting.

## 9. وضعیت

- COA authority hierarchy: VERIFIED — v1.2 master; v1.5 supplementary; v1.3 intermediate.
- Identifier scope: 78 IDs VERIFIED; 110809 PROHIBITED.
- Canonical account attributes: PARTIAL / OPEN.
- AccountId and unspecified Parent/Level/Nature/NormalBalance/Postable/DirectPostingAllowed/Effective Dates/Subledger/FE eligibility: NOT VERIFIED where no accepted source exists.
- Implementation of the 7C canonical registry in production: NOT VERIFIED.
- Acceptance tests for full canonical registry: NOT RUN / NOT VERIFIED.
- Evidence: NOT ACCEPTED.
- Gate 3 / Gate 4: OPEN / OPEN.
- Posting Release: NOT GRANTED.

## 10. Canonical Account Evidence Register

رجیستر فعال 7C شامل 78 شناسه اعلام‌شده در COA v1.2/v1.4 و تصمیمات پذیرفته‌شده مالک است. هر حساب باید مجموعه کامل فیلدهای Canonical، شواهد فیلدبه‌فیلد، Source/Decision Reference، نسخه و Audit linkage داشته باشد.

هیچ فیلدی صرفاً به دلیل حضور در یک استخراج تاریخی تکمیل‌شده محسوب نمی‌شود. مقادیر فاقد پشتوانه صریح در v1.2، v1.4، تصمیم مالک یا ADR پذیرفته‌شده باید NOT VERIFIED / NOT SPECIFIED باقی بمانند و Posting را مسدود کنند.

## 11. Owner Decision Overlay — Controlled Reconstruction v0.1

### 11.1. مرز این لایه

این لایه، تصمیمات صریح مالک را از صفات مندرج در COA v1.2/v1.4 جدا می‌کند. مبنای آن OWN-7C-043 و برای چرخه چک OWN-7C-004-R1 است. این overlay جایگزین COA v1.2 یا رجیستر تکمیلی v1.4 نیست و به‌تنهایی صفات Canonical را تعیین نمی‌کند.

- Base codes: 76 کد در رجیستر COA v1.2.
- Supplementary identifiers: 2114 و 5106 در رجیستر v1.4 بر مبنای تصمیمات معتبر مالک.
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

- Base-code coverage: 76 کد COA v1.2 در دامنه تطبیق.
- Supplementary coverage: 2114 و 5106 در دامنه v1.4؛ مجموع 78 شناسه.
- Decision registry: 78 identifiers, including prohibited 110809; 110809 is not an active account.
- Owner decisions: applied as a separate overlay; source titles/attributes are not silently overwritten.
- Canonical reconciliation to the user's current COA baseline: OPEN pending exact row-level mapping of canonical attributes.
- Test execution: NOT RUN / NOT VERIFIED.
- Evidence: NOT ACCEPTED.
- Gate 3 / Gate 4: OPEN / OPEN.
- Posting Release: NOT GRANTED.

Status: CONTROLLED RECONSTRUCTION — DRAFT v0.4; supersedes v0.3 as the current 7C replacement workpaper; not a posting release or final canonical COA.


## 12. اصلاحیه حاکم مرجعیت COA — 2026-11-18

- COA v1.2 سند مادر و مرجع اصلی کدینگ/قواعد دفترکل است.
- COA v1.5 رجیستر تکمیلی جاری برای تطبیق حساب‌به‌حساب 7C و مبتنی بر v1.2 است.
- COA v1.3 مرحله میانی است.
- رجیستر فعال شامل 78 شناسه است؛ 110809 ممنوع است.
- v1.4 در دسترس و بررسی شده است؛ عبارت «محتوای v1.4 در دسترس نیست» یا «SOURCE CONTENT REQUIRED» معتبر نیست.
- تکمیل Canonical attributes فقط در حدود منابع واقعی مجاز است. AccountId، Parent/Level، Nature/NormalBalance، Postable/DirectPostingAllowed، Effective Dates و Subledger هرجا تعیین نشده‌اند OPEN/NOT VERIFIED می‌مانند و Posting ممنوع است.
- 7C-OPEN-001 مانع دسترسی نیست؛ مانع واقعی آن تطبیق صفات Canonical است.

Status: COA AUTHORITY VERIFIED; ACCOUNT-LEVEL CANONICALIZATION PARTIAL / OPEN; IMPLEMENTATION NOT VERIFIED; TESTS NOT RUN; EVIDENCE NOT ACCEPTED; POSTING RELEASE NOT GRANTED.

## Controlling v3.0.3 / COA authority overlay — 2026-11-20

This overlay supersedes historical current-authority statements in this workpaper. The accepted architecture is MDG-ARCH-MASTER-001 v3.0.3. Active COA hierarchy: v1.2 master; v1.5 controlled supplementary reconstruction/crosswalk; v1.4 historical/superseded for current supplementary authority; v1.3 intermediate only. The original canonical nine-sheet COA and independent Part 4H-6 remain unrecovered. COA v1.5 is not represented as the recovered original source. Its SHA-256 is b21bb4a9999f6b503c9e3c215a3b5db6029d659a17587e8f7359280c9450e3f8.

The 78-ID scope remains 76 base codes plus 2114 and 5106; 110809 is prohibited. Explicit parent relations remain only 110302→1103, 610801→6108 and 620201→6202. Do not infer other parent/level values from prefixes or titles.

Owner-approved dispositions now controlling under v3.0.3 include: 110801 sole GL inventory control; 110802–110806 inventory subledger categories; 110807 inactive in R1; 110809 prohibited; 2114 service received/accepted but uninvoiced liability (DESIGN ONLY / NO POSTING pending canonical attributes and FE-20); 4107 rounding gain and 5106 rounding loss (both DESIGN ONLY pending canonical attributes, collision check, eligibility, FE/PostingRule and tests); 6204/6205 superseded/inactive; 620201 inactive; O-65/CHG-100 mixed-sign allocation as specified in P.4. Monetary unit is 1 IRR, document-level one-time AwayFromZero rounding, maximum 1 IRR per document; no independent per-row monetary threshold.

Do not mark an account canonical merely because its code or owner disposition is known. AccountId, ParentAccountId, Level, Nature, NormalBalance, Postable, DirectPostingAllowed, Effective dates, Subledger dimensions, Tax treatment and FE eligibility remain NOT VERIFIED/NOT SPECIFIED wherever the controlling sources do not explicitly determine them. Those unresolved fields remain fail-closed for Posting.

COA authority and owner-policy conflicts are reconciled; canonical row-level attributes, full namespace collision proof, FE↔COA crosswalk, 2114/5106 qualification, 7C gate tests and independent evidence remain open. Gate B-final is not closed by this overlay.


## Current Architecture Master authority — 2026-11-24

The Product Owner has designated **MDG-ARCH-MASTER-001 v3.0.4** as the sole current, accepted, official and governing Architecture/Accounting Master. v3.0.3 remains a historical snapshot and may be referenced only to describe prior source material or decisions. This notice does not change the COA source hierarchy, posting-rule status, test evidence, unresolved source gaps or release authorization recorded in this workpaper. Any detailed semantic dependency must be reconciled against v3.0.4 before implementation or Posting Release.
