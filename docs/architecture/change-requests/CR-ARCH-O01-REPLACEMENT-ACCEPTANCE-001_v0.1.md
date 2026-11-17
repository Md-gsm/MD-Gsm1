# CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001
## جایگزینی معیار پذیرش O-01 با پذیرش و ثبت نسخه‌های کنترل‌شده جدید 7B/7C/7D

| مشخصه | مقدار |
|---|---|
| CR ID | CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 |
| Version | 0.1 |
| Status | OWNER DECISION RECORDED — INCORPORATED IN ARCHITECTURE v2.6 DRAFT — INDEPENDENT REVIEW PENDING |
| Authority | Product Owner decision recorded in conversation on 2026-11-18 |
| Affected baseline | MDG-ARCH-MASTER-001 v2.5 |
| Affected item | O-01 |
| Related work | Finance 7B / 7C / 7D controlled reconstruction and reconciliation |
| Posting Release | NOT GRANTED |

## 1. تصمیم مالک

مالک محصول تصمیم خود را با عبارت «پذیرش و ثبت نسخه جدید» اعلام کرد.

معنای رسمی تصمیم برای O-01:

**معیار تاریخی O-01 که بازیابی اسناد اصلی قدیمی 7B v0.4 و 7C v0.3 را الزام می‌کرد، با معیار «تکمیل، پذیرش و ثبت نسخه‌های کنترل‌شده جدید 7B، 7C و 7D» جایگزین می‌شود.**

بنابراین بازیابی نسخه‌های تاریخی اصلی شرط الزامی بسته‌شدن O-01 نیست. نسخه‌های جدید باید صریحاً با وضعیت Controlled Reconstruction / Controlled Replacement ثبت شوند و نباید به‌عنوان اسناد تاریخی بازیابی‌شده معرفی شوند.

این تصمیم صرفاً معیار پذیرش را تغییر می‌دهد؛ به‌خودی‌خود به معنای تکمیل اسناد، عبور Gateها، تأیید حرفه‌ای حسابداری/مالیاتی یا مجوز ثبت مالی نیست.

## 2. معیار پذیرش جایگزین O-01

O-01 تنها زمانی قابل بستن است که تمام موارد زیر مستند و پذیرفته شده باشند:

1. **ثبت نسخه‌ها:** نسخه مشخص، شناسه سند، وضعیت کنترل سند، تاریخ، مالک/تهیه‌کننده، وضعیت تأیید و checksum هر سه سند 7B، 7C و 7D ثبت شده باشد.
2. **7C — COA:** همه حساب‌های مشمول دامنه، کد، هویت پایدار، Parent/Level، ماهیت، NormalBalance، وضعیت فعال/غیرفعال، SubledgerType، Postable، DirectPostingAllowed، EffectiveFrom و lineage داشته باشند. هر مقدار حل‌نشده صریحاً OPEN/UNVERIFIED باشد؛ هیچ مقدار از روی پیشوند کد یا حدس پر نشود.
3. **7B — Posting Matrix:** برای هر FE-01..FE-38 تمام فیلدهای قرارداد Posting Matrix باید مقدار معتبر داشته باشند یا صریحاً NOT SPECIFIED / BLOCKED ثبت شوند؛ هر مورد مسدود باید دلیل، owner action، شرط رفع، Test ID، Evidence plan و منع قطعی Posting داشته باشد. هیچ فیلد اجباری نباید خالی یا با حدس پر شود.
4. **7B↔7C Crosswalk:** نگاشت رویدادها به حساب‌ها و حساب‌ها به رویدادها دوطرفه و بدون نگاشت یتیم یا حساب بی‌مصرفِ توضیح‌نداده‌شده باشد.
5. **7D — Test Matrix:** هر FE دارای آزمون مثبت/منفی/مرزی متناسب با وضعیت خود باشد؛ برای Posting Rule کامل، positive journal assertion دقیق الزامی است؛ برای FE ناقص یا خارج از R1، آزمون fail-closed/scope-block و positive case صریحاً BLOCKED/PENDING می‌ماند تا 7B/7C کامل شود. Atomicity، idempotency، reversal، concurrency، period lock، tax fail-closed و subledger controls حسب مورد پوشش داده شوند.
6. **Traceability:** هر تصمیم/قاعده به معماری، ADR، تصمیم مالک یا منبع ورودی با شناسه دقیق متصل باشد؛ تعارض‌ها با سلسله‌مراتب مصوب حل و ثبت شوند.
7. **Cross-Audit:** ممیزی مستقل روی SHA دقیق بسته انجام شود. هیچ C0/C1 حل‌نشده در تمامیت سند، provenance، صحت crosswalk، سازگاری کنترل‌ها یا وضعیت‌های گزارش‌شده باقی نماند. شکاف‌های واقعی سیاست/طراحی دامنه می‌توانند باز بمانند، مشروط بر اینکه برای هرکدام BLOCKED/DEFERRED، دلیل، owner action، شرط رفع، Test ID، Evidence plan و منع Posting صریح ثبت شده باشد؛ هیچ مانع بی‌مالک یا بی‌شرط رفع پذیرفته نیست.
8. **Evidence:** آزمون‌های قابل اجرا، به‌ویژه fail-closed و structural checks، باید واقعاً اجرا و خروجی، محیط، commit/SHA و checksum آن‌ها ثبت شود. آزمون‌های positive journal که به علت نقص 7B/7C هنوز قابل اجرا نیستند باید با دلیل و وضعیت BLOCKED/PENDING ثبت شوند؛ هرگز PASS تلقی نشوند.
9. **Professional review:** تأیید حسابدار ارشد و متخصص مالیاتی برای قواعد و صفات تخصصی‌ای که به‌عنوان FINAL/ACCEPTED اعلام می‌شوند ثبت شده باشد؛ مواردی که هنوز BLOCKED/UNRESOLVED هستند باید صریحاً بدون تأیید نهایی باقی بمانند. تأیید مالک جایگزین تأیید حرفه‌ای نیست.
10. **Document control:** نسخه‌های پذیرفته‌شده در رجیستر اسناد مرجع ثبت و نسخه‌های قبلی به‌عنوان Superseded/Historical نگهداری شوند؛ تاریخچه و lineage حذف نشود.

## 3. سلسله‌مراتب منابع

این CR هیچ تصمیم مالک یا ADR پذیرفته‌شده‌ای را بازگشایی نمی‌کند. مرجعیت محتوا طبق Architecture v2.5، تصمیمات قطعی مالک و ADRهای پذیرفته‌شده باقی می‌ماند. منابع اولیه و workpaperها فقط در حد سازگاری با مراجع بالاتر پذیرفته می‌شوند.

برای COA، نسخه 1.2 خط مبنای مستقل قبلی است و نسخه 1.4 طبق اعلام مالک نسخه بعدی ساخته‌شده بر پایه آن است. نسخه 1.3 در این زنجیره صرفاً نسخه/مرحله میانی محسوب می‌شود، نه اینکه به‌جای 1.4 خط مبنای جاری معرفی شود. ثبت یا پذیرش نسخه 1.4 باید بر اساس متن کامل و قابل ارجاع همان نسخه انجام شود؛ این CR محتوای COA v1.4 را بازنویسی یا حدس نمی‌زند.

## 4. دامنه اثر و موارد خارج از اثر

این تغییر:
- الزام بازیابی تاریخی 7B v0.4 و 7C v0.3 را از معیار O-01 حذف می‌کند؛
- پذیرش و ثبت نسخه‌های کنترل‌شده جدید 7B/7C/7D را معیار جایگزین می‌کند؛
- نیاز به crosswalk، ممیزی، آزمون و شواهد را حفظ می‌کند؛
- تصمیمات قطعی OWN-7C-043 را دوباره برای تأیید به مالک ارجاع نمی‌دهد.

این تغییر:
- O-01 را در لحظه ثبت این تصمیم بسته اعلام نمی‌کند؛
- Gate 3 یا Gate 4 را نمی‌بندد؛
- پیاده‌سازی Accounting را مجاز نمی‌کند؛
- Posting Release صادر نمی‌کند؛
- تأیید حسابدار ارشد یا متخصص مالیاتی را حذف نمی‌کند؛
- ادعا نمی‌کند نسخه‌های بازسازی‌شده همان اسناد تاریخی اصلی‌اند.

## 5. وضعیت اجرایی

- Owner decision: RECORDED.
- CR document: REGISTERED IN PR #66; owner approval recorded.
- Architecture v2.5: preserved unchanged as the prior accepted baseline. Architecture v2.6 controlled amendment draft has been created and registered in the Library; it is pending independent review and document acceptance. Until acceptance, v2.5 remains the last accepted architecture baseline.
- O-01: OPEN — replacement criterion OWNER APPROVED; controlled 7B/7C/7D deliverables and acceptance evidence pending.
- Gate 3 / Gate 4: OPEN / OPEN.
- Posting Release: NOT GRANTED.


## 6. ثبت نسخه معماری پیش‌نویس 2.6

پیش‌نویس کنترل‌شده Architecture v2.6 با اعمال همین CR در Library ثبت شد. نسخه 2.5 بدون تغییر نگهداری می‌شود و تا پذیرش مستقل v2.6 آخرین baseline پذیرفته‌شده است.

| Artifact | Library path | SHA-256 |
|---|---|---|
| Markdown | /MDGsm_Unified_Architecture_Blueprint_v2.6_Draft.md | 771ba853fe402832f379199dc72aad14b232375d2e62e417ac71084018f7d60a |
| Word | /MDGsm_Unified_Architecture_Blueprint_v2.6_Draft.docx | 2c26d4863d314d2de438f9a72239698b4c8df814bbf59057c68a5978b1b7ee82 |

Status: DRAFT — PENDING INDEPENDENT REVIEW AND DOCUMENT ACCEPTANCE.
