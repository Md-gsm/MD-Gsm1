# CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001
## جایگزینی معیار پذیرش O-01 با پذیرش و ثبت نسخه‌های کنترل‌شده جدید 7B/7C/7D

| مشخصه | مقدار |
|---|---|
| CR ID | CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 |
| Version | 0.1 |
| Status | OWNER DECISION RECORDED — IMPLEMENTATION IN CONTROLLED DOCUMENT REVISION PENDING |
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
3. **7B — Posting Matrix:** رویدادهای FE-01..FE-38 دارای trigger/recognition، شرایط مجاز و ممنوع، debit/credit، حساب‌های مجاز، مبنای مبلغ/بهای تمام‌شده، مالیات، ابعاد/subledger، reversal، idempotency و کنترل atomicity باشند؛ هر مورد خارج از دامنه R1 صریحاً مسدود/مستثنا باشد.
4. **7B↔7C Crosswalk:** نگاشت رویدادها به حساب‌ها و حساب‌ها به رویدادها دوطرفه و بدون نگاشت یتیم یا حساب بی‌مصرفِ توضیح‌نداده‌شده باشد.
5. **7D — Test Matrix:** هر الزام و هر FE به آزمون مثبت، منفی، مرزی و کنترل‌های مرتبط نگاشت شده باشد؛ تست‌های اتمیک، تکرارپذیری، reversal، concurrency و fail-closed حسب مورد پوشش داده شوند.
6. **Traceability:** هر تصمیم/قاعده به معماری، ADR، تصمیم مالک یا منبع ورودی با شناسه دقیق متصل باشد؛ تعارض‌ها با سلسله‌مراتب مصوب حل و ثبت شوند.
7. **Cross-Audit:** ممیزی مستقل روی SHA دقیق بسته اسناد انجام شود و هیچ C0/C1 باز یا تناقض مسدودکننده باقی نماند. یافته‌های غیرمسدودکننده باید مالک و مهلت داشته باشند.
8. **Evidence:** اجرای آزمون‌ها، خروجی‌ها، محیط، commit/SHA، checksum و نتیجه پذیرش قابل بازتولید ثبت شده باشد.
9. **Professional review:** تأیید حسابدار ارشد و متخصص مالیاتی برای بخش‌های مربوط به تخصص آن‌ها ثبت شده باشد؛ تأیید مالک جایگزین این تأییدهای حرفه‌ای نیست.
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
