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
| 110401 | نقش طبق تصمیم مالک 24B: چک نزد صندوق | تطبیق عنوان/صفات snapshot با نقش مصوب و ثبت source lineage؛ چرخه تست باز |
| 110402 | نقش طبق تصمیم مالک 24B: چک واگذارشده | تطبیق عنوان/صفات snapshot با نقش مصوب و ثبت source lineage؛ چرخه تست باز |
| 110403 | نقش طبق تصمیم مالک 24B: چک در جریان وصول | تطبیق عنوان/صفات snapshot با نقش مصوب و ثبت source lineage؛ چرخه تست باز |
| 110404 | نقش طبق تصمیم مالک 24B: چک برگشتی | تطبیق عنوان/صفات snapshot با نقش مصوب و ثبت source lineage؛ چرخه تست باز |
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
