# MDG-WP06-7C-ACCOUNT-DISPOSITION-001
## ماتریس تعیین تکلیف حساب‌به‌حساب — Controlled Reconstruction

| مشخصه | مقدار |
|---|---|
| Document ID | MDG-WP06-7C-ACCOUNT-DISPOSITION-001 |
| Version | 0.1 |
| Parent baseline | MDG-ARCH-MASTER-001 v2.5 |
| Related work | WP06-P4H-7B / 7C / 7D |
| Source workbook | MDG-WP06-COA-RECON-001_v0.1.xlsx |
| Classification | CONTROLLED RECONCILIATION — NOT CANONICAL COA |
| Design | ACCOUNT DISPOSITION RECORDED; MASTER ATTRIBUTES OPEN |
| Implementation / Tests | NOT VERIFIED / NOT RUN |
| Posting Release | NOT GRANTED |

## 1. هدف و محدودیت

این سند مرحله تعیین تکلیف حساب‌به‌حساب 7C را از روی snapshot واقعی workbook تطبیق ثبت می‌کند. 76 کد زیر از شیت «منبع اصلی گروه» استخراج شده‌اند؛ این شیت snapshot پیشنهادی گروه است، نه فایل مادر canonical و نه تصمیم/مجوز اجرا. عنوان‌ها و ویژگی‌های آن نباید بدون hash-check منبع اصلی به عنوان Source Verified پذیرفته شوند.

دامنه تطبیق 78 کد است: 76 کد snapshot به‌علاوه 2114 و 5106. کد 4107 نیز در snapshot وجود دارد و از ADR/تصمیم مرتبط با rounding نیازمند کنترل collision و Posting Matrix است. هیچ‌یک از این موارد به‌تنهایی مجوز ثبت ایجاد نمی‌کند.

## 2. فهرست کامل 76 کد snapshot

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

تعداد ردیف‌های فهرست بالا: 76. شناسه حساب، والد، تاریخ مؤثر و مجوزهای Postable/DirectPosting برای این فهرست به‌صورت canonical تخصیص/تأیید نشده‌اند.

## 3. روابط والد صریح

تنها سه رابطه کد والد که از شواهد فعلی صریحاً قابل احرازند:
- 110302 → 1103
- 610801 → 6108
- 620201 → 6202

برای بقیه کدها ParentAccountCode را از پیشوند یا سلسله‌مراتب ظاهری حدس نزنید. ParentAccountId برای 76 ردیف snapshot تخصیص canonical نشده است.

## 4. تصمیمات حاکم و disposition حساب‌های خاص

| کد | تعیین تکلیف حاکم | اقدام باقیمانده |
|---|---|---|
| 110701 | مالیات خرید قابل بازیافت؛ نه Supplier Advances | تطبیق عنوان snapshot با تصمیم مالک؛ TaxMode و Tax Matrix معتبر لازم |
| 110801 | تنها GL inventory control | تعریف دقیق اتصال به Inventory Subledger |
| 110802–110806 | دسته‌های Inventory Subledger؛ نه GL مستقل | ثبت نوع دسته/ابعاد و جلوگیری از GL posting مستقل |
| 110807 | غیرفعال در R1؛ حفظ تاریخچه؛ Posting جدید ممنوع | وضعیت تاریخی و migration guard؛ هیچ فعال‌سازی انجام نشود |
| 110809 | ممنوع؛ ایجاد یا استفاده نشود | ممنوعیت در کنترل COA و آزمون منفی |
| 110602 | غیرفعال تا احراز منبع و ماهیت | Source recovery؛ بدون Posting |
| 2102 | غیرفعال | اصلاح Crosswalk snapshot؛ بدون Posting |
| 2106 | غیرفعال تا احراز منبع | اصلاح عنوان/وضعیت snapshot؛ بدون Posting |
| 2107 | دریافت ورودی تخصیص‌نیافته؛ NOT Refund Payable | اصلاح تعارض عنوان snapshot و FE-12؛ حساب Refund Payable جداگانه unresolved |
| 2104 | تسویه موقت موجودی منفی | رویداد معتبر، عمر مانده، مالک، trigger تسویه و close blocker |
| 2113 | مازاد شمارش موجودی در حساب تعلیقی؛ نه درآمد خودکار | شواهد شمارش، بررسی مالک، رفع تعلیق و آزمون |
| 2114 | خدمت دریافت‌شده و تأییدشده با تعهد قابل اثبات، ولی فاکتورنشده | Parent، NormalBalance، Postable، DirectPosting و زیرمعین از منبع مادر مشخص نشده؛ از 2101 و 2103 مستقل |
| 1201 | بهای تمام‌شده دارایی ثابت | جزئیات Fixed Asset Subledger؛ فعال‌سازی R2 |
| 1202 | استهلاک انباشته | جزئیات Fixed Asset Subledger؛ فعال‌سازی R2 |
| 6105 | هزینه استهلاک؛ با 1202 اشتباه نشود | Mapping/فعال‌سازی و آزمون R2 |
| 3101–3105 | نگاشت و NormalBalance حقوق مالکانه باز | تطبیق منبع مادر/توافق ساختار حقوق مالکانه؛ بدون حدس |
| 3199 | کنترل موقت افتتاحیه | مستندات، منشأ مانده و تأییدهای مستقل |
| 4107 | سود گرد کردن | collision check، Normal Side، Posting Matrix، Posting Identity و آزمون |
| 5106 | زیان گرد کردن؛ خارج از 76 کد | همان کنترل‌های 4107؛ عدم برخورد کد باید اثبات شود |
| 110401 | نقش طبق OWN-7C-004-R1: چک دریافت‌شده نزد صندوق | تطبیق عنوان/صفات snapshot با نقش مصوب و ثبت source lineage؛ چرخه تست باز |
| 110402 | نقش طبق OWN-7C-004-R1: چک واگذارشده به بانک | تطبیق عنوان/صفات snapshot با نقش مصوب و ثبت source lineage؛ چرخه تست باز |
| 110403 | نقش طبق OWN-7C-004-R1: چک در جریان وصول | تطبیق عنوان/صفات snapshot با نقش مصوب و ثبت source lineage؛ چرخه تست باز |
| 110404 | نقش طبق OWN-7C-004-R1: چک برگشتی در انتظار تعیین تکلیف | عنوان پیشنهادی «ابزار چک دریافتی» در TXT v1.0 و snapshot با نقش مصوب تعارض دارد؛ تصمیم مالک مقدم است؛ source title/attributes باید اصلاح و lineage ثبت شود |
| 110302 | زیر 1103 | تطبیق کارمزد/تسویه PSP و جلوگیری از دوباره‌شماری |
| 6108 | حساب تجمیعی غیرقابل ثبت مستقیم | حفظ non-postable؛ فقط 610801 فرزند صریح |
| 610801 | فقط کسری نقدی قطعی و تأییدشده | شواهد علت، تأیید و قاعده ثبت |
| 620201 | غیرفعال تا تعیین منبع و نقش | حفظ والد صریح 6202؛ no posting |
| 6204–6205 | تاریخی/غیرفعال در R1 | حفظ تاریخچه؛ حذف/ادغام/Posting جدید ممنوع |
| 6103 | حقوق و دستمزد در دامنه R2 | خارج از Posting R1 |

## 5. وضعیت پیش‌فرض سایر حساب‌ها

برای هر کد snapshot که در جدول بخش 4 override نشده است، این وضعیت اعمال می‌شود:
- Source title و سایر 24 فیلد فقط به‌عنوان snapshot ثبت می‌شوند.
- AccountId و ParentAccountId تا زمان ایجاد registry رسمی تخصیص نمی‌یابند.
- ParentAccountCode، NormalBalance، Postable، DirectPostingAllowed، SubledgerRequired و EffectiveFrom فقط با منبع/تصمیم معتبر قابل تثبیت‌اند.
- هر مقدار «UNVERIFIED», «NOT SPECIFIED», «NOT SET» یا مشابه، مقدار نهایی نیست.
- هیچ حسابی صرفاً به دلیل وجود در snapshot مجاز به Posting نیست.
- FE links و Test links باید از ماتریس 7B/7D نهایی استخراج شوند؛ از نام حساب یا کد آن استنتاج نشوند.

## 6. فیلدهای canonical که هنوز باید برای هر حساب بسته شوند

برای هر 78 کد، رجیستر canonical باید حداقل شامل این فیلدها باشد:
AccountId, AccountCode, PersianTitle, EnglishTitle, ParentAccountId, ParentAccountCode, Level, AccountClass, Nature, NormalBalance, Postable, DirectPostingAllowed, SubledgerRequired, EffectiveFrom, EffectiveTo, Status, TaxModeConstraint, RecognitionRule, UsageRestrictions, ReconciliationPolicy, Subledger/Dimensions, OwnerDecisionRef, SourceRef, FEReferences, TestReferences, PostingRestrictions, OpenIssues.

هر فیلد باید یکی از این منشأها را داشته باشد: OWNER_DECISION, ADR, ARCHITECTURE_BASELINE, PRIMARY_SOURCE, RECONSTRUCTED, PROPOSED یا NOT_VERIFIED. مقدار نامعلوم نباید با مقدار فرضی جایگزین شود.

## 7. معیار بستن مرحله 7C

این مرحله فقط زمانی بسته می‌شود که:
1. فایل مادر اصلی 76 کد با hash و نسخه مستقل دریافت/تطبیق شود؛ در غیر این صورت Source Snapshot همین workbook به‌صراحت محدود باقی بماند.
2. برای 76 کد، تمام 24 ستون منبع بدون تغییر حفظ و هر اختلاف با تصمیمات مالک در ستون جداگانه ثبت شود.
3. برای 2114 و 5106 منشأ تصمیم، title، nature، NormalBalance، parent، postability، subledger، collision و محدودیت‌ها مستند شود؛ موارد فاقد منبع NOT SPECIFIED بمانند.
4. COA tree فقط با روابط مستند ساخته شود؛ شناسه‌های پایدار و effective dating با قرارداد registry تصویب‌شده تخصیص یابند.
5. تمام FE links، Posting eligibility و Test references به 7B/7D نهایی وصل شوند.
6. بررسی مستقل روی نسخه/hash دقیق انجام شود و تمام یافته‌های بحرانی تعیین تکلیف شوند.

## 8. وضعیت نهایی

- Account inventory: 76 snapshot codes + 2114 + 5106 = 78-account scope.
- Explicit parent links: 3.
- Canonical AccountId/ParentAccountId registry: OPEN.
- Canonical account attributes: PARTIALLY RECONCILED / OPEN.
- Posting eligibility: NOT GRANTED.
- Test execution: NOT RUN / NOT VERIFIED.
- Gate 3 / Gate 4: OPEN / OPEN.
- Posting Release: NOT GRANTED.

این سند یک ماتریس تعیین تکلیف کنترل‌شده است؛ جایگزین COA مادر، اسناد مستقل 7B/7C/7D یا مجوز اجرای مالی نیست.

---

## اصلاحیه مرجعیت منبع و معیار بستن 7C — 2026-11-17

این بخش بر هر عبارت متعارض درباره «snapshot غیرقابل اتکا به علت نبود Excel مادر»، «نیاز به دریافت مجدد Excel» یا «تطبیق با فایل مادر بازیابی‌نشده» مقدم است.

Excel نسخه 1.0 داخل ZIP «فهرست جامع و مرجع کدینگ حساب‌ها.zip» موجود و Hash آن با Source Register برابر است. Workbook دارای 9 شیت و شیت «دفتر حساب‌ها» شامل 76 کد یکتا و 24 ستون است. فایل، منبع ورودی/شاهد است؛ مرجع نهایی تصمیم یا Posting نیست.

خط مبنای مستقل v1.2 و نسخه جاری COA v1.4 (که بر پایه v1.2 ساخته شده) به‌همراه Architecture v2.5، OWN-7C-043 و ADRهای پذیرفته‌شده، مبنای تصمیم و تطبیق‌اند. v1.3 صرفاً مرحله میانی است. هر ارجاع v1.3 در این workpaper که به‌عنوان خط مبنای فعلی برداشت شود، باید v1.4 خوانده شود؛ ارجاع تاریخی به v1.3 فقط برای شرح تغییرات محفوظ است.

شرط پذیرش 7C این نیست که Excel دوباره دریافت شود. شرط پذیرش، تکمیل تطبیق حساب‌به‌حساب با منابع حاکم، تعیین وضعیت هر ویژگی canonical با lineage، حفظ تعارض‌های واقعی و اعمال fail-closed برای مقادیر نامشخص است. AccountId، ParentAccountId و EffectiveFrom که در هر 76 ردیف فایل تخصیص/تعیین نشده‌اند، شکاف‌های واقعی Registry هستند و باید در طراحی canonical تعیین تکلیف شوند؛ از پیشوند عددی Parent استنتاج نشود.

این اصلاحیه، بندهای پذیرش یا اقدام‌های قبلی را که دریافت مجدد Excel یا تأیید Hash آن را شرط بستن 7C قرار می‌دادند، باطل می‌کند. جزئیات Hashها و 9 شیت در `MDG-WP06-COA-SOURCE-AUTHORITY-CORRECTION-001_v0.1.md` ثبت شده است.


## اصلاحیه مرجعیت و منبع — 2026-11-18

این اصلاحیه بر عبارت‌های متعارض بخش 1 و هر ارجاع قدیمی مقدم است.

فایل Excel منبع گروه در ZIP بازیابی شده است؛ SHA-256 فایل استخراج‌شده: `a6ca3377fde5863455a3b4bccf9a000d0dbf23f44bd66eeae51c44d0f3697866`. بنابراین 76 ردیف و 24 ستون شیت «دفتر حساب‌ها» منبع واقعی استخراج‌شده‌اند؛ دیگر نباید این فایل «صرفاً snapshot فاقد فایل مادر» یا «hash تأییدنشده» توصیف شود.

سلسله‌مراتب جاری: Architecture v2.5 و تصمیمات مالک/ADRهای پذیرفته‌شده؛ COA v1.4 به‌عنوان خط مبنای جاری که بر پایه v1.2 ایجاد شده؛ v1.2 به‌عنوان خط مبنای قبلی؛ v1.3 به‌عنوان مرحله میانی؛ Excel/TXT v1.0 به‌عنوان شواهد منبع. این workpaper هنوز Canonical COA نهایی نیست؛ علت آن فقدان فایل نیست، بلکه نیاز به ثبت تطبیق کامل و قابل ردیابی همه صفات هر حساب با v1.4 و تصمیمات حاکم است.

وضعیت 7C: Source bytes VERIFIED؛ 76 source rows VERIFIED؛ account-level canonical disposition PARTIAL؛ AccountId/Parent/NormalBalance/eligibility/Subledger/EffectiveFrom باید بر اساس v1.4 و منابع معتبر نهایی شوند؛ حدس از روی prefix ممنوع.

 
## ارجاع نسخه جایگزین 7C v0.2 — 2026-11-18

نسخه مستقل جایگزین 7C در مسیر `docs/finance/workpapers/p4h/7c/WP06-P4H-7C-COA-RECONCILIATION-001_v0.2.md` ثبت شد. این نسخه provenance فایل Excel و 76 ردیف/24 ستون، سلسله‌مراتب v1.2→v1.4 و جایگاه میانی v1.3، آمار صفات snapshot، تصمیمات حاکم و رجیستر canonical attributes را یکپارچه می‌کند.

Source Excel و hash آن VERIFIED است. کار باز، تطبیق ردیف‌به‌ردیف با COA v1.4 و تکمیل صفات canonical است؛ این موضوع به معنی نبود یا عدم احراز فایل Excel نیست. 7C v0.2 هنوز DRAFT / NOT ACCEPTED است.
