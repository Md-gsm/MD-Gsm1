# MDG-WP06-7B-7C-7D-CROSS-AUDIT-001
## گزارش ممیزی متقابل نسخه 0.2 — تطبیق اصلاحات و وضعیت بستن

| مشخصه | مقدار |
|---|---|
| Document ID | MDG-WP06-7B-7C-7D-CROSS-AUDIT-001 |
| Version | 0.2 |
| Classification | CONTROLLED CROSS-AUDIT — RECONCILIATION RECORDED; CLOSURE NOT GRANTED |
| Baseline | MDG-ARCH-MASTER-001 v2.5 |
| Audited files | Integrated v0.4; 7B v0.2; 7C v0.2; 7D v0.2; CR O-01 v0.1; source workbook ZIP; row-level source/status CSV v0.2 |
| PR | Md-gsm/MD-Gsm1 #66 |
| Test execution | NOT RUN / NOT VERIFIED |
| Evidence | NOT ACCEPTED |
| Gate 3 / Gate 4 | OPEN / OPEN |
| Posting Release | NOT GRANTED |

## 1. حکم ممیزی

ممیزی متقابل اسناد 7B، 7C و 7D انجام شد. دو ناسازگاری مستند در نسخه‌های کاری شناسایی و اصلاح شدند. با وجود اصلاح این ناسازگاری‌ها، بسته هنوز برای بستن نهایی O-01 یا صدور Posting Release آماده نیست؛ زیرا صفات canonical حساب‌ها، قواعد کامل ثبت، بازیابی متن آزمون‌های تاریخی و اجرای آزمون‌ها ناقص است.

## 2. یافته‌های ممیزی و اصلاحات

| Finding | شدت | مشاهده | اصلاح/وضعیت |
|---|---|---|---|
| XA-001 — FE-01/FE-02 semantic inversion | HIGH | در 7B Closure v0.1، FE-01 به خدمت و FE-02 به فروش کالا نسبت داده شده بود؛ integrated baseline v0.3 برعکس آن را ثبت می‌کند. | 7B اصلاح شد تا FE-01 فروش کالا و FE-02 خدمت مطابق integrated v0.3 باشد. هنوز حساب‌های 4101/4102 صرفاً ارتباط اولیه‌اند و Posting Rule نهایی نیستند. |
| XA-002 — cheque policy vs source provenance | MEDIUM | 7B/7D به‌درستی نقش‌های 110401..110404 را به چرخه چک ارجاع داده بودند، اما authority reference باید OWN-7C-004-R1 باشد و تفاوت policy mapping با Source Title/Attributes روشن‌تر شود. | 7C نقش‌های چهارگانه را بر اساس OWN-7C-004-R1 ثبت کرد؛ عنوان پیشنهادی 110404 در TXT/snapshot با این تصمیم متعارض است و تطبیق title/attributes و source lineage همچنان OPEN است. |
| XA-003 — test identity provenance | HIGH | وجود شناسه‌های FIN-TST/COA-TST/ATOMIC/INV-CAN/INV-NEG ممکن بود با بازیابی متن کامل آزمون اشتباه شود. | 7D طبقه‌بندی SOURCE-ID، RECONSTRUCTED و PROPOSED را حفظ می‌کند. نگاشت شماره‌به‌شماره شناسه‌های تاریخی همچنان OPEN است. |
| XA-004 — account canonicalization | CRITICAL | 76 ردیف snapshot به‌تنهایی COA canonical نیستند؛ 2114 و 5106 نیز صفات کامل مادر ندارند. | هیچ AccountId، Parent، NormalBalance، eligibility یا Subledger حدس زده نشده؛ موارد OPEN باقی مانده‌اند. |
| XA-005 — posting completeness | CRITICAL | پوشش FE-01..FE-38 به‌معنی Posting Matrix کامل نیست. | 7B موانع هر رویداد را مشخص می‌کند؛ برای رویدادهای فاقد حساب/شرط معتبر، Debit/Credit NOT SPECIFIED و posting مسدود است. |
| XA-006 — test execution/evidence | CRITICAL | طراحی Test Case به‌معنی اجرای آزمون یا PASS نیست. | تمام تست‌های مالی NOT RUN / NOT VERIFIED و Evidence NOT ACCEPTED باقی می‌مانند. |

## 3. Crosswalk کنترل‌شده

### 3.1 رویدادهای درآمدی
- FE-01 = فروش کالا؛ ارتباط اولیه با 4101 در integrated baseline v0.3.
- FE-02 = ارائه خدمت/تعمیر؛ ارتباط اولیه با 4102 در integrated baseline v0.3.
- این نگاشت فقط برای همسان‌سازی نام رویدادها در workpapers استفاده شده است؛ Trigger، Recognition Time، حساب مقابل، Amount Basis و Expected Journal هنوز کامل/تأیید نشده‌اند.

### 3.2 چرخه چک
تصمیم مالک OWN-7C-004-R1:
- 110401: چک دریافت‌شده نزد صندوق؛
- 110402: چک واگذارشده به بانک؛
- 110403: چک در جریان وصول؛
- 110404: چک برگشتی در انتظار تعیین تکلیف.

این policy mapping از تطبیق عنوان و ویژگی‌های snapshot جداست. 7C باید source title، NormalBalance، Parent، Postable/DirectPostingAllowed و lineage هر حساب را با OWN-7C-004-R1 تطبیق دهد. Test Case کامل lifecycle نیز باز است.

### 3.3 دامنه حساب
- 76 کد snapshot شامل 4107 و 110809 است؛ ممنوعیت 110809 همچنان حاکم است.
- 2114 و 5106 خارج از 76 کد snapshot و با منشأ جدا هستند.
- 4107/5106 collision check و eligibility همچنان OPEN؛ ADR-0017 به‌تنهایی مجوز Posting نیست.

### 3.4 شناسه‌های آزمون
- FIN-TST-001..042 و COA-TST-001..034 و خانواده‌های ATOMIC/INV-CAN/INV-NEG تا بازیابی متن کامل، SOURCE-ID ONLY هستند.
- FIN-TST-043..053 در workpaper فعلی RECONSTRUCTED هستند؛ این برچسب ادعای original source نیست.
- هیچ Test ID جدیدی نباید به‌صورت ضمنی به شناسه تاریخی نسبت داده شود.

## 4. رجیستر مواردی که هنوز بسته نشده‌اند

| ID | موضوع | معیار بستن | وضعیت |
|---|---|---|---|
| XA-OPEN-01 | منبع مادر COA و provenance | فایل منبع مستقل، نسخه/hash، تطبیق 76 ردیف و اختلافات | OPEN |
| XA-OPEN-02 | AccountId/ParentAccountId | قرارداد registry و درخت مستند، بدون prefix inference | OPEN |
| XA-OPEN-03 | NormalBalance/Postable/DirectPostingAllowed | تکمیل حساب‌به‌حساب با lineage معتبر | OPEN |
| XA-OPEN-04 | Subledger/Dimensions/Effective dating | قرارداد هر حساب و تاریخ مؤثر | OPEN |
| XA-OPEN-05 | 2114/5106 attributes | منشأ، عنوان، Nature، NormalBalance، Parent، eligibility و collision | OPEN |
| XA-OPEN-06 | FE-01..FE-38 Posting Matrix | SourceEvent، trigger، recognition، هر دو سمت ثبت، amount/cost basis، reversal، atomicity و authorization | OPEN |
| XA-OPEN-07 | FIN-TST-001..042 source text | متن، نسخه، hash و Expected Result هر شناسه | OPEN |
| XA-OPEN-08 | COA-TST/ATOMIC/INV-CAN/INV-NEG | منبع تاریخی یا تصویب صریح نگاشت بازسازی‌شده جدید | OPEN |
| XA-OPEN-09 | FE↔COA↔Test exact mapping | هر رویداد و حساب مجاز به آزمون دقیق و Expected Result متصل شود | OPEN |
| XA-OPEN-10 | Tax Matrix | نرخ/نوع/تاریخ مؤثر و تست منفی همه کانال‌ها؛ TaxMode=0 no posting | OPEN |
| XA-OPEN-11 | R1/R2 boundaries | تست‌ها و guardهای فعال‌سازی/غیرفعال‌سازی نسخه‌ای | OPEN |
| XA-OPEN-12 | Production finance tests | محیط ایزوله، fixture قطعی، اجرا، log، snapshots و reviewer | NOT RUN |
| XA-OPEN-13 | Independent review | review substantive روی hash دقیق پس از CI | PENDING |
| XA-OPEN-14 | Architecture O-01 | CR اصلاح معیار پذیرش و تأیید رسمی مالک/معماری | OPEN |

## 5. وضعیت CI و حدود شواهد

CI عمومی مخزن برای build، CodeQL، Persons gates، installer و charter gates اجرا می‌شود. این workflowها جایگزین اعتبارسنجی محتوای مالی، COA semantic validation یا تست Posting نیستند. نتیجه workflow باید فقط برای SHA دقیق ثبت شود. هیچ نتیجه CI عمومی، Gate 3/4 مالی یا Posting Release را به‌تنهایی نمی‌بندد.

در زمان این گزارش، workflowهای آخرین SHA شامل برخی runهای موفق و برخی runهای در حال اجرا بودند؛ تا تکمیل همه runها، وضعیت CI نهایی اعلام نمی‌شود.

## 6. معیار تصمیم برای O-01

O-01 در Architecture v2.5 با این گزارش بسته نمی‌شود. پیش‌نیازهای CR:
1. منبع و provenance حساب‌ها تعیین تکلیف شود.
2. 7B برای تمام رویدادهای R1 قواعد قابل پذیرش داشته باشد یا به‌طور مستند BLOCKED/DEFERRED بماند.
3. 7D crosswalk دقیق و تست‌های لازم را با منبع یا برچسب بازسازی‌شده داشته باشد.
4. تناقض‌های بین اسناد رفع و روی hash نهایی ممیزی شود.
5. اجرای تست‌ها و شواهد Gate 4 جداگانه انجام شود.
6. معیار O-01 از «بازیابی اصل تاریخی» به «تکمیل و پذیرش خط مبنای کنترل‌شده جدید» فقط از مسیر Change Request و تصویب حاکمیتی تغییر کند.

## 7. وضعیت نهایی

- Cross-document contradictions identified: 2; corrected in workpapers.
- FE semantic alignment: aligned to integrated v0.3; financial mapping remains partial.
- Cheque policy/source distinction: clarified; source crosswalk remains open.
- Account canonicalization: OPEN.
- Posting rules: PARTIAL / OPEN.
- Test design: PARTIAL / OPEN.
- Test execution: NOT RUN / NOT VERIFIED.
- Evidence: NOT ACCEPTED.
- Gate 3 / Gate 4: OPEN / OPEN.
- O-01: OPEN.
- Posting Release: NOT GRANTED.

این گزارش یک گزارش ممیزی کنترل‌شده است، نه تأیید صحت مالی نهایی، نه بستن O-01 و نه مجوز پیاده‌سازی/ثبت.


## اصلاحیه پس از تصمیم مالک و احراز منبع — 2026-11-18

این اصلاحیه بر وضعیت‌های قدیمی متعارض در این گزارش مقدم است.

### XA-OPEN-01 — تفکیک منشأ از canonicalization
فایل مادر Excel گروه از ZIP بازیابی و hash آن احراز شده است: ZIP SHA-256 `c17e3c16d05abb9d2ae57b8cfa0c8860e69b613ececb0db7758668cd1db04449`؛ Excel استخراج‌شده SHA-256 `a6ca3377fde5863455a3b4bccf9a000d0dbf23f44bd66eeae51c44d0f3697866`. فایل 9 شیت و 76 ردیف حساب دارد. بنابراین بخش «عدم دسترسی/عدم احراز بایت‌های فایل» بسته شد. اما canonicalization حساب‌ها، ویژگی‌های هر ردیف، lineage و تطبیق با COA v1.4 همچنان OPEN است. XA-OPEN-01 به دو موضوع تفکیک می‌شود:
- XA-OPEN-01A — فایل و provenance منبع Excel: CLOSED.
- XA-OPEN-01B — تطبیق محتوای 76 حساب با COA v1.4 و تصمیمات مالک: OPEN.

### مرجعیت COA
COA v1.2 خط مبنای پیشین و مبنای ایجاد v1.4 است؛ v1.4 نسخه جاری اعلام‌شده از سوی مالک است؛ v1.3 مرحله میانی است. Excel/TXT v1.0 منبع شاهد است، نه مرجع نهایی تصمیم. هر ردیف باید با v1.4، OWN decisions و ADRهای پذیرفته‌شده تطبیق داده شود.

### XA-OPEN-14 / O-01
مالک در 2026-11-18 معیار جایگزین O-01 را تأیید کرده است. CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 v0.1 در PR #66 ثبت شده است. در نتیجه نیاز به تأیید مالک برای خودِ تغییر معیار بسته شد؛ اما O-01 به‌دلیل تکمیل‌نشدن تحویل‌دادنی‌های 7B/7C/7D و شواهد، هنوز OPEN است. وضعیت XA-OPEN-14: OWNER DECISION CLOSED / DELIVERABLE ACCEPTANCE OPEN.

### وضعیت ممیزی به‌روز
- منبع Excel و hash: VERIFIED.
- معیار جایگزین O-01: OWNER APPROVED.
- 7B Posting Rule completeness: PARTIAL / OPEN.
- 7C canonical account reconciliation against v1.4: OPEN.
- 7D test design completeness: PARTIAL / OPEN.
- Test execution: NOT RUN / NOT VERIFIED.
- Evidence: NOT ACCEPTED.
- Independent review on final SHA: PENDING.
- Gate 3 / Gate 4: OPEN / OPEN.
- Posting Release: NOT GRANTED.

 
## Cross-Audit Addendum — 7B/7C/7D v0.2 — 2026-11-18

### ساختار تحویل‌دادنی‌های جایگزین
- 7B v0.2: 39 ردیف رویدادی شامل FE-01..FE-38 و FE-10b؛ disposition هر رویداد و Test ID مشخص است.
- 7C v0.2: 76 کد منبعی و 24 ستون، provenance احرازشده و canonicalization register؛ صفات نهایی هنوز با COA v1.4 تطبیق کامل نشده‌اند.
- 7D v0.2: 39 Test ID مستقل MDG-FIN-GATE-001..039؛ تمام 39 ارجاع 7B در 7D متناظر دارند.

### نتیجه تطبیق ساختاری
تعداد Test IDهای یکتا در 7B v0.2 برابر 39 و در 7D v0.2 برابر 39 است؛ مجموعه شناسه‌ها از 001 تا 039 پیوسته است. CSV وضعیت منبع 7C نیز 76 ردیف × 39 ستون دارد و hash آن به‌صورت مستقل بازتولید شد. FE coverage شامل FE-01..FE-38 به‌علاوه FE-10b است. این کنترل، صحت طراحی حسابداری یا اجرای آزمون را اثبات نمی‌کند.

### موارد همچنان باز
- 7B positive posting rule completeness و canonical debit/credit: OPEN.
- 7C row-level reconciliation با v1.4 و canonical attributes: OPEN.
- 7D positive journal assertions وابسته به 7B/7C: BLOCKED.
- Test execution: NOT RUN / NOT VERIFIED.
- Evidence: NOT ACCEPTED.
- Independent review روی SHA نهایی: PENDING.
- O-01: CR OWNER-APPROVED / DELIVERABLE ACCEPTANCE OPEN.


## Automated Structural Consistency Check — 2026-11-18

این کنترل ساختاری روی محتوای شاخه PR انجام شد؛ جایگزین ممیزی مستقل تخصصی یا آزمون runtime نیست.

| Check | Result | Scope / Limitation |
|---|---|---|
| 7B event rows | PASS — 39 rows | FE-01..FE-38 plus FE-10b; posting rule completeness not implied |
| 7B unique Test ID references | PASS — 39 IDs | MDG-FIN-GATE-001..039 |
| 7D unique Test IDs | PASS — 39 IDs | Continuous sequence 001..039 |
| 7B↔7D ID set equality | PASS | Every 7B test reference exists in 7D; does not prove assertion correctness |
| FE coverage | PASS — FE-01..FE-38 plus FE-10b | FE-37 and FE-38 are separate cases |
| Source workbook | PASS — 76 unique account codes, 24 columns, 9 sheets | Source workbook/hash verified; canonical COA values remain separate |
| Manifest | REFRESHED | SHA256SUMS updated after the current document revisions; final independent verification pending |

### نتیجه
ساختار شناسه‌ها و پوشش رویداد/آزمون بین 7B و 7D سازگار است. 7B posting rules، 7C canonical account values و 7D positive expected journals همچنان به تکمیل محتوای canonical و تأییدهای مربوط وابسته‌اند. هیچ آزمون مالی اجرا نشده و هیچ Evidence اجرایی پذیرفته نشده است.


### 7C Row-Level Source Snapshot Addendum
فایل `MDG-WP06-COA-76-ROW-SOURCE-REGISTER_v0.2.csv` در Library ثبت شد: 76 ردیف × 39 ستون (24 ستون منبع + 15 ستون وضعیت canonical). SHA-256 فایل CSV برابر `78c3793c075ff4aaebef4b86230411d3dca52233f0cca3c81278b2f4607acbb1` است. این hash با materialization و محاسبه مستقل SHA-256 تأیید شد؛ 76 ردیف داده و 39 ستون نیز با خواندن CSV تأیید شد. در تمام 76 ردیف، CanonicalAccountId و CanonicalParentAccountId برابر NOT ASSIGNED، EffectiveFrom/To برابر NOT SET و صفات canonical برابر UNVERIFIED است. این فایل source snapshot/status register است، نه COA canonical. بنابراین پوشش سطر‌به‌سطر source status تکمیل شده؛ canonicalization با v1.4 همچنان OPEN است.
