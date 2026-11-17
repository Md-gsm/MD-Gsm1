# MDG-WP06-COA-SOURCE-RECOVERY-001
## گزارش اجرای پنج مرحله Source Recovery و Account-to-FE Reconciliation

| مشخصه | مقدار |
|---|---|
| Document ID | MDG-WP06-COA-SOURCE-RECOVERY-001 |
| Version | 0.1 |
| Classification | CONTROLLED SOURCE-RECOVERY REPORT — PARTIAL RECONCILIATION; NO POSTING RELEASE |
| Baseline | MDG-ARCH-MASTER-001 v2.5 |
| Related workpapers | 7B Posting Rule Closure v0.1; 7C Account Disposition v0.1; 7D Test Closure v0.1; Cross-Audit v0.2; COA v1.3 |
| PR | Md-gsm/MD-Gsm1 #66 |
| Implementation / Tests | NOT VERIFIED / NOT RUN |
| Gate 3 / Gate 4 | OPEN / OPEN |
| Posting Release | NOT GRANTED |

## 1. نتیجه اجرایی

هر پنج مرحله در حد شواهد موجود بررسی و تعیین تکلیف شد. یک منبع متنی مادر با Hash قابل تطبیق پیدا شد؛ فایل اصلی Excel گروه در Library در دسترس نبود، اما snapshot دقیق 76 ردیفی و 24 ستونی آن در workbook تطبیق موجود است. صفات 76 حساب به‌صورت row-level بررسی شدند و کمبودهای canonical به‌صورت آماری احراز شد. برای 2114 و 5106 منبع طراحی v1.3 پیدا شد، اما هیچ‌کدام به‌عنوان حساب آماده Posting تأیید نشده‌اند. 4107/5106 از نظر تکرار کد در مجموعه فعلی 78تایی برخورد ندارند، اما No-Collision در COA canonical کامل هنوز اثبات نشده است. Crosswalk رویدادهای FE-01..FE-38 از 7B مرور شد؛ حساب‌های کاندید از قواعد و تصمیمات معتبر جدا شده‌اند و موارد فاقد دوطرف ثبت، Recognition و آزمون کامل مسدود باقی مانده‌اند.

**حکم:** پنج مرحله Source Recovery و reconciliation اولیه تکمیل شده‌اند؛ مرحله 7C canonical close، تکمیل Posting Matrix 7B، اجرای 7D و O-01 هنوز بسته نیستند.

## 2. مرحله اول — بازیابی منبع مادر و Provenance

### 2.1 منبع متنی COA v1.0
- نام: # فهرست جامع و مرجع کدینگ حساب‌های MD-Gsm.txt
- شناسه: MDG-COA-MASTER-001
- نسخه اعلام‌شده: 1.0 — Proposed Master Reference
- وضعیت خود سند: PROPOSED؛ نه Owner Accepted و نه Posting Released.
- SHA-256 محاسبه‌شده از فایل موجود: 777328fdc9f42b1884ba8cf5884308f840893534ba86e51212328d4a7e174d79.
- این Hash با SRC-01 در workbook تطبیق برابر است: MATCH.
- نتیجه: فایل TXT از نظر integrity قابل شناسایی است، اما محتوای آن پیشنهاد است و در تعارض با تصمیم صریح مالک حاکم نیست.

### 2.2 فایل Excel مادر گروه
- نام مورد انتظار در Source Register: فهرست جامع و مرجع کدینگ حساب‌های MD-Gsm v1.0.xlsx.
- Hash ثبت‌شده در Source Register: a6ca3377fde5863455a3b4bccf9a000d0dbf23f44bd66eeae51c44d0f3697866.
- فایل اصلی با همین نام/بایت در منابع Library قابل بازیابی نشد؛ بنابراین Hash اصلی NOT VERIFIED است.
- فایل قابل بررسی: MDG-WP06-COA-RECON-001_v0.1.xlsx، SHA-256:
  af3d02cab3c2f5a430508356680b42146320efb9122b1e5fd32d62ebb8c5c0fd.
- شیت «منبع اصلی گروه» یک snapshot با 76 ردیف حساب و 24 ستون است. تعداد کد یکتا 76 و duplicate code در این snapshot صفر است.
- این snapshot، جایگزین بایت‌های فایل مادر اصلی نیست؛ فقط شاهد row-level برای داده‌هایی است که در workbook تطبیق نگهداری شده‌اند.

### 2.3 سلسله‌مراتب اعمال‌شده
1. Architecture v2.5 در حوزه معماری و قواعد حاکم؛
2. تصمیم صریح و متأخر مالک، از جمله OWN-7C-043 و تصمیمات ثبت‌شده قبلی؛
3. ADRهای پذیرفته‌شده؛
4. COA v1.3 به‌عنوان Controlled Design Baseline، با رعایت status هر حساب؛
5. COA v1.0 و snapshot گروه به‌عنوان منابع پیشنهادی/شاهد، نه تصمیم مستقل.

## 3. مرحله دوم — ممیزی 76 حساب و صفات Canonical

### 3.1 پوشش داده
- 76 ردیف، 76 کد یکتا؛ هیچ کد تکراری در snapshot.
- 24 ستون منبع برای هر ردیف در workbook snapshot وجود دارد.
- AccountId: هر 76 مورد NOT ASSIGNED.
- ParentAccountId: هر 76 مورد NOT ASSIGNED.
- EffectiveFrom: هر 76 مورد NOT SET.
- ParentAccountCode: فقط سه رابطه صریح در داده snapshot وجود دارد:
  - 110302 → 1103
  - 610801 → 6108
  - 620201 → 6202
  سایر والدها نباید از پیشوند عددی استنتاج شوند.

### 3.2 وضعیت صفات کلیدی در snapshot
| فیلد | توزیع دقیق در 76 ردیف | نتیجه |
|---|---:|---|
| Postable | 56 UNVERIFIED؛ 8 NO IN R1؛ 6 CONDITIONAL؛ 5 NO RELEASE/source open؛ 1 UNRESOLVED | هیچ‌یک از مقادیر پیشنهادی، به‌تنهایی مجوز Posting نیست |
| DirectPostingAllowed | 72 UNVERIFIED؛ 4 NO | مجوز ثبت مستقیم برای اغلب حساب‌ها احراز نشده |
| SubledgerRequired | 35 YES/CONTROLLED SUBLEDGER؛ 35 NOT SPECIFIED؛ 6 TO BE CONFIRMED | قرارداد تفصیلی 41 ردیف هنوز تکمیل نشده |
| NormalBalance | 28 بدهکار پیشنهادی؛ 14 بستانکار پیشنهادی؛ 13 تعیین‌نشده؛ 6 وابسته به تصمیمات 041/042 که احراز نشده‌اند؛ 3 کنترلی/وابسته به وضعیت؛ بقیه دوطرفه/مشروط/سایر حالت‌ها | این‌ها snapshot/proposal هستند، نه NormalBalance canonical |
| Status | 47 DEFINED_IN_V2.5/POSTING RELEASE NOT ESTABLISHED؛ 5 OWNER/COA mapping open؛ سایر ردیف‌ها در وضعیت‌های source recovery، R2، reserved، conditional و غیره | Statusها باید با تصمیم و دامنه R1/R2 کنترل شوند |

### 3.3 حکم
- 76 حساب از نظر فهرست‌برداری و استخراج snapshot پوشش داده شدند.
- رجیستر canonical کامل نیست: شناسه پایدار، ParentAccountId، EffectiveFrom و مجوز ثبت کامل نشده‌اند.
- سه رابطه والد بالا تنها روابط صریح فعلی‌اند.
- هیچ‌یک از 76 حساب صرفاً با این گزارش Postable یا DirectPostingAllowed نمی‌شود.
- داده 24 ستونی snapshot باید به‌عنوان source evidence حفظ شود؛ برای canonicalization، منبع اصلی Excel یا تأیید row-level مستقل لازم است.

## 4. مرحله سوم — تعیین تکلیف 2114 و 5106

### 4.1 حساب 2114
منبع: MDG-COA-MASTER-001 v1.3، بخش‌های 4.4، 5 و 18.
- تعریف طراحی: بدهی خدمات واقعاً دریافت‌شده، تأییدشده و فاکتورنشده، با تعهد پرداخت قابل اثبات.
- تفکیک قطعی دامنه:
  - 2101: بدهی فاکتوردار تأمین‌کننده؛
  - 2103: فقط GRNI کالای دریافت‌شده و فاکتورنشده؛
  - 2114: خدمت دریافت‌شده/تأییدشده/دارای تعهد قابل اثبات و بدون فاکتور.
- صرف Purchase Order بدهی یا هزینه ایجاد نمی‌کند.
- وضعیت در v1.3: OWNER APPROVED — DESIGN ONLY.
- FE-20 mapping، NormalBalance، Parent، AccountId، Postable، DirectPostingAllowed، Subledger و posting conditions هنوز کامل/احراز نشده‌اند.
- نتیجه: 2114 از نظر تعریف سیاستی تعیین تکلیف شده، اما برای Posting مسدود می‌ماند. 2102 غیرفعال و 2110 جایگزین مصوب نیست.

### 4.2 حساب 5106
منبع: MDG-COA-MASTER-001 v1.3، بخش‌های 4.8، 5 و 18؛ در کنار ADR-0017.
- تعریف طراحی: حساب مستقل زیان گردکردن؛ 6205 جایگزین آن نیست.
- وضعیت در v1.3: OWNER APPROVED — DESIGN ONLY.
- فعال‌سازی مشروط به No-Collision، تکمیل Posting Matrix و آزمون است.
- AccountId، Parent، NormalBalance نهایی، Postable، DirectPostingAllowed و Subledger/eligibility canonical از منبع موجود کامل نشده‌اند.
- نتیجه: کد 5106 در رجیستر تجمیعی 78تایی افزوده شده، اما حساب canonical یا مجوز Posting نیست.

## 5. مرحله چهارم — کنترل عدم برخورد 4107 و 5106

- 4107 در snapshot 76 کدی وجود دارد و در COA v1.3 به‌عنوان rounding gain تعریف شده است.
- 5106 در 76 کد snapshot نیست و در COA v1.3 به‌عنوان rounding loss مستقل افزوده شده است.
- در مجموعه فعلی 78 شناسه، رشته کدهای 4107 و 5106 متفاوت‌اند و تکرار مستقیم کد بین این دو وجود ندارد.
- این کنترل فقط عدم تکرار در مجموعه اسناد فعلی را ثابت می‌کند؛ عدم برخورد با کل COA canonical، کدهای رزرو/تاریخی، Registry/Database constraints و migrationهای آینده را ثابت نمی‌کند.
- 4107 و 5106 هر دو DESIGN ONLY؛ No-Collision نهایی و Posting Matrix هنوز باز است.
- ADR-0017: مبالغ IRR صحیح، AwayFromZero، حداکثر اختلاف ±1 IRR در هر سند؛ این حد، tolerance عمومی یا مجوز posting نیست.
- قید 100,000 IRR به‌ازای هر ردیف، مسئله‌ای جدا و unresolved است؛ نباید با حد ±1 IRR سند یکی گرفته شود.
- 6205 یا 6204 نباید برای زیان گردکردن استفاده شوند.
- نتیجه: No duplicate in current 78-code list = checked; canonical No-Collision = NOT VERIFIED; automatic rounding posting = BLOCKED.

## 6. مرحله پنجم — Account-to-FE mapping

ماتریس 7B برای تمام FE-01..FE-38 مرور شد. این Crosswalk فهرست کاندیدها/قیود موجود را تثبیت می‌کند، نه اینکه برای هر رویداد Journal نهایی یا مجوز Posting صادر کند.

| FE | حساب/مرجع قابل استناد در workpaper فعلی | حکم |
|---|---|---|
| FE-01 | 4101 اولیه | مقابل، recognition، tax و مبلغ باز |
| FE-02 | 4102 اولیه | trigger/مقابل و recognition باز |
| FE-03 | 1101/1102/110302 بر حسب روش دریافت | دریافت به‌تنهایی درآمد نیست؛ حساب مقصد/settlement کامل نیست |
| FE-04 | 2105 | شرایط ایجاد/مصرف تعهد و وجه مقابل باز |
| FE-05 | 2105، 2107، 110703 حسب نوع تخصیص | تخصیص مستقل؛ جلوگیری از درآمد تکراری؛ detail باز |
| FE-06 | 2101، 2103، 110701 مشروط | invoice/receipt/tax sequencing باز |
| FE-07 | 110801 GL control؛ 110802–110806 Subledger | بهای ورودی، مالکیت و atomicity باز |
| FE-08 | 5101 و 110801 | valuation/cost layers و trigger کامل نیست |
| FE-09 | 110807 غیرفعال R1 | BLOCKED؛ منبع/مالکیت قطعه و مسیر هزینه کامل نیست |
| FE-10 | سند فروش اصلی و FIN-TST-010 reference | refund/physical return تفکیک و mapping باز |
| FE-10b | Inventory Subledger و سند اصلی | حساب/ارزش برگشتی نهایی باز |
| FE-11 | 2101/2103 و سند خرید اصلی | credit note/receipt/tax mapping باز |
| FE-12 | 2107 فقط دریافت تخصیص‌نیافته؛ نه Refund Payable | حساب بدهی بازپرداخت هنوز تخصیص نیافته؛ BLOCKED |
| FE-13 | 4201–4203 فقط به‌عنوان کدهای unresolved؛ Contra Revenue policy | نگاشت کد/ReasonCode/FE باز؛ BLOCKED برای posting |
| FE-14 | OWN-7C-004-R1؛ 110401..110404 | policy mapping موجود؛ source-title/attributes، انتقال‌های وضعیت، 1102 و lifecycle test باز |
| FE-15 | 6104 به‌عنوان حساب عمومی Landed Cost مجاز نیست | مبنای سرمایه‌ای‌سازی/هزینه و روش تخصیص باز؛ BLOCKED |
| FE-16 | 110808 و 2113؛ 620201 مشروط/غیرفعال | suspense ownership و عدم دوباره‌شماری باید بسته شود |
| FE-17 | 2112؛ 610801 فقط کسری نقدی قطعی | علت، تأیید و trigger انتقال از suspense باز |
| FE-18 | 1110 فقط وجه نقد فیزیکی در راه؛ 110302 برای PSP | مسیرهای انتقال و clearing تفکیک شوند |
| FE-19 | 6201 نیازمند احراز؛ هزینه بر اساس ماهیت واقعی | خسارت/ضایعات/تکنسین تفکیک نشده؛ BLOCKED |
| FE-20 | 2114 فقط liability design؛ 2102 inactive؛ 2103 فقط کالا؛ 5103 blocked | حساب هزینه و Journal نهایی باز؛ BLOCKED |
| FE-21 | TradeInId؛ 110803 فقط پس از پذیرش مالکیت/شناسایی | 110803/2111 به‌عنوان GL مستقل نیازمند mapping جدا |
| FE-22 | 6106/6107؛ 110302 | gross/net settlement و عدم دوباره‌شماری باز |
| FE-23 | 5104/2108 پس از تکمیل فنی تأییدشده | entitlement، reversal و آزمون باز |
| FE-24 | 1201/1202/6105 در R2 | R1 blocked؛ Subledger و آزمون R2 |
| FE-25 | R2 | deferred |
| FE-26 | R2 | deferred |
| FE-27 | 110701؛ 1109/2109 فقط TaxMode و Tax Matrix معتبر | TaxMode=0 no tax posting؛ Tax Matrix و تست همه کانال‌ها باز |
| FE-28 | 3199 موقت | حقوق مالکانه/افتتاحیه و شواهد باز |
| FE-29 | دوره مالی و کنترل pending journals | checklist، قفل و reopen test باز |
| FE-30 | فقط حساب‌های DirectPostingAllowed پس از احراز مجوز | Permission/Audit/segregation test باز |
| FE-31 | R2 | deferred |
| FE-32 | R2؛ 110807 حساب WIP نیست و R1 inactive | deferred |
| FE-33 | 110703 خروجی تخصیص‌نیافته؛ 2107 ورودی تخصیص‌نیافته | دو مسیر جدا؛ تخصیص/برگشت باز |
| FE-34 | 3101–3105 mapping unresolved؛ 3199 temporary | year-close mapping و idempotency test باز |
| FE-35 | 610x فقط بر اساس ماهیت اقتصادی؛ 110702 فقط پیش‌پرداخت واقعی | taxonomy/approval و mapping دقیق باز |
| FE-36 | هیچ حسابی؛ unknown event fail-closed | ثبت/هشدار و test لازم |
| FE-37 | Payroll R2؛ کمیسیون عملیاتی در FE-23 | عدم ادغام commission/payroll |
| FE-38 | 3101–3105 فقط پس از mapping معتبر | BLOCKED |

### قاعده نهایی Crosswalk
- جدول بالا یک account candidate/disposition map است، نه Debit/Credit matrix نهایی.
- هیچ FE بدون SourceEvent، Trigger، RecognitionTime، AmountBasis، هر دو سمت Journal، Subledger effects، PostingIdentity، Reversal، Atomicity و Test evidence بسته نمی‌شود.
- هر حسابی که UNVERIFIED, NOT SPECIFIED, DESIGN ONLY, INACTIVE, RESERVED یا BLOCKED است، باید در posting fail-closed شود.

## 7. اصلاح مرجع چرخه چک

مرجع سیاستی که در این workpapers باید استفاده شود OWN-7C-004-R1 است، نه ارجاع کلی/مبهم به «24B» به‌عنوان منبع مستقل.
- 110401: چک دریافت‌شده نزد صندوق؛
- 110402: چک واگذارشده به بانک؛
- 110403: چک در جریان وصول؛
- 110404: چک برگشتی در انتظار تعیین تکلیف.

در TXT پیشنهادی v1.0 و snapshot گروه، 110404 به‌صورت «ابزار چک دریافتی» آمده و 110401..110403 عنوان‌های باز دارند؛ این عناوین با OWN-7C-004-R1 ناسازگارند. تصمیم مالک مقدم است. تطبیق Source Title/attributes و قرارداد Subledger برای هر چهار کد هنوز باز است. برگشت واقعی و تأییدشده باید طلب مشتری را دقیقاً یک‌بار بازسازی کند؛ ثبت Journal نهایی تا تکمیل 7B/7D و attributes مسدود است.

## 8. رجیستر نهایی موارد باز

| ID | مورد | وضعیت پس از این مرحله | شرط بستن |
|---|---|---|---|
| SR-01 | بایت‌های اصلی Excel مادر گروه | OPEN | دریافت فایل اصلی و تأیید Hash ثبت‌شده |
| SR-02 | 76 ردیف source attributes | SNAPSHOT REVIEWED; CANONICAL OPEN | تطبیق با فایل مادر اصلی و تصویب row-level |
| SR-03 | AccountId/ParentAccountId/EffectiveFrom | OPEN | تصویب Canonical Registry و درخت |
| SR-04 | NormalBalance/Postable/DirectPostingAllowed/Subledger | OPEN | مقدار canonical و lineage برای هر حساب |
| SR-05 | 2114 | DESIGN DEFINED; POSTING BLOCKED | Parent/NormalBalance/eligibility/Subledger/FE-20 complete |
| SR-06 | 5106 | DESIGN DEFINED; POSTING BLOCKED | Account registry، NormalBalance، No-Collision و FE mapping |
| SR-07 | 4107/5106 canonical No-Collision | NOT VERIFIED | بررسی کامل namespace/registry و constraint |
| SR-08 | 110401..110404 source crosswalk | POLICY MAPPED; SOURCE CROSSWALK OPEN | اصلاح منبع/عنوان و قرارداد Subledger |
| SR-09 | FE-01..FE-38 Posting Matrix | PARTIAL / OPEN | تکمیل هر Posting Rule با هر دو طرف و شروط کامل |
| SR-10 | FE↔COA↔Test exact map | PARTIAL / OPEN | Test IDs و Expected Results دقیق برای هر account/event |
| SR-11 | اجرای آزمون‌ها و Evidence | NOT RUN / NOT ACCEPTED | اجرای ایزوله، log/snapshot، reviewer مستقل |
| SR-12 | O-01 معماری v2.5 | OPEN | CR رسمی برای تغییر معیار پذیرش و تأیید حاکمیتی |

## 9. اقدام‌های مجاز بعدی و ممنوعیت‌ها

مجاز:
1. دریافت اصل Excel مادر یا جایگزین رسمی آن و تطبیق Hash؛
2. ساخت Canonical Registry پیشنهادی با همه 78 شناسه، با فیلدهای unresolved به‌صورت صریح؛
3. تکمیل FE-14 source crosswalk و FE-20/rounding mapping پس از شواهد؛
4. افزودن semantic lint برای ممنوعیت posting به حساب‌های unresolved/inactive؛
5. طراحی تست‌های account-to-event و اجرای آن‌ها پس از مجوز Gate 3.

ممنوع:
- ساخت AccountId، Parent، NormalBalance یا Postability با حدس؛
- تلقی COA v1.0 یا snapshot گروه به‌عنوان Owner Accepted؛
- فعال‌سازی 2114 یا 5106 صرفاً با وجود کد؛
- ادعای canonical No-Collision برای 4107/5106 بر اساس تفاوت دو عدد؛
- انتشار Journal نهایی برای FEهایی که حساب/شرط مقابل آن‌ها باز است؛
- اعلام PASS، Gate 3/4 closure یا Posting Release.

## 10. وضعیت نهایی

- Source TXT integrity: MATCH.
- Original group XLSX bytes/hash: NOT RECOVERED / NOT VERIFIED.
- 76-row snapshot: 76 unique codes; 24 columns reviewed.
- Canonical account registry: OPEN.
- 2114 / 5106: design definitions found; posting eligibility incomplete.
- 4107/5106 duplicate in current 78-code list: NOT FOUND; canonical No-Collision: NOT VERIFIED.
- FE-01..FE-38 account candidate crosswalk: REVIEWED; PARTIAL / OPEN.
- Test execution: NOT RUN / NOT VERIFIED.
- Evidence: NOT ACCEPTED.
- Gate 3 / Gate 4: OPEN / OPEN.
- Posting Release: NOT GRANTED.

این گزارش نتیجه Source Recovery و reconciliation طراحی است؛ سند اصلی تاریخی را بازیابی‌شده اعلام نمی‌کند، COA canonical نمی‌سازد و مجوز پیاده‌سازی یا ثبت مالی صادر نمی‌کند.

---

## اصلاحیه الزام‌آور provenance — 2026-11-17

این بخش، بندهای 2.2، 3.3، SR-01، بندهای مربوط به نبود Excel و نتیجه‌گیری‌های متکی بر آن‌ها را supersede می‌کند.

فایل ZIP «فهرست جامع و مرجع کدینگ حساب‌ها.zip» موجود و بررسی شده است. ZIP شامل Excel v1.0 و TXT v1.0 است؛ Hash آرشیو، Excel و TXT به‌ترتیب:
- ZIP: `c17e3c16d05abb9d2ae57b8cfa0c8860e69b613ececb0db7758668cd1db04449`
- Excel: `a6ca3377fde5863455a3b4bccf9a000d0dbf23f44bd66eeae51c44d0f3697866` — MATCH با Source Register.
- TXT: `777328fdc9f42b1884ba8cf5884308f840893534ba86e51212328d4a7e174d79` — MATCH با SRC-01.

Excel شامل 9 شیت است؛ شیت «دفتر حساب‌ها» دارای 76 حساب یکتا و 24 ستون است. بنابراین «Original group XLSX bytes/hash: NOT RECOVERED / NOT VERIFIED» و شرط دریافت مجدد Excel از این گزارش حذف و پس گرفته می‌شود. نبود Excel مانع یا نقص 7C نیست.

مرجعیت نسخه‌ها برای تطبیق: Architecture v2.5 و تصمیمات مالک/ADRهای پذیرفته‌شده؛ سپس COA v1.4 به‌عنوان خط مبنای فعلی؛ v1.2 خط مبنای مستقل قبلی و مبنای ساخت v1.4؛ v1.3 مرحله میانی؛ Excel/TXT v1.0 منابع ورودی و شواهد هستند، نه مرجع نهایی تصمیم یا Posting.

سه رابطه ParentAccountCode با کد عددی صریح در فایل: 110302→1103، 610801→6108، 620201→6202. یادداشت‌های 110401..110403 و 4201..4203 کد والد عددی قطعی تعیین نمی‌کنند.

SR-01 «بایت‌های اصلی Excel مادر گروه» از رجیستر موانع حذف می‌شود. موارد باز AccountId، ParentAccountId، EffectiveFrom، صفات canonical، FE mapping، آزمون و شواهد همچنان بازند؛ این موارد به نبود فایل ارتباط ندارند. برای شرح کامل provenance به `MDG-WP06-COA-SOURCE-AUTHORITY-CORRECTION-001_v0.1.md` مراجعه شود.

 
## اصلاحیه حاکمیتی و منبعی — 2026-11-18

این اصلاحیه بر هر عبارت متعارض قبلی در همین گزارش مقدم است.

1. **SR-01 — بایت‌های فایل Excel مادر گروه: CLOSED.** فایل Excel از ZIP کتابخانه بازیابی و استخراج شد. ZIP با SHA-256 برابر `c17e3c16d05abb9d2ae57b8cfa0c8860e69b613ececb0db7758668cd1db04449` و فایل Excel داخل آن با SHA-256 برابر `a6ca3377fde5863455a3b4bccf9a000d0dbf23f44bd66eeae51c44d0f3697866` راستی‌آزمایی شد. فایل دارای 9 شیت است؛ شیت «دفتر حساب‌ها» 76 ردیف و 24 ستون دارد. ادعای قبلی «Original group XLSX bytes/hash: NOT RECOVERED / NOT VERIFIED» پس‌گرفته می‌شود.
2. **مرجعیت COA:** Excel/TXT v1.0 شواهد منبع‌اند، نه مرجع نهایی تصمیم. COA v1.2 خط مبنای قبلی است؛ v1.4 نسخه بعدی ساخته‌شده بر پایه v1.2 و مرجع طراحی جاری اعلام‌شده از سوی مالک است؛ v1.3 فقط مرحله میانی است. معماری v2.5، تصمیمات قطعی مالک و ADRهای پذیرفته‌شده همچنان در سلسله‌مراتب حاکم‌اند. این گزارش نباید v1.3 را جایگزین v1.4 معرفی کند.
3. **SR-02 — تطبیق 76 ردیف:** وجود و تمامیت فایل و تعداد ردیف‌ها احراز شده است؛ این امر به‌تنهایی canonical شدن همه ویژگی‌های حساب را ثابت نمی‌کند. تطبیق حساب‌به‌حساب با خط مبنای v1.4 و تصمیمات مالک همچنان باید مستند شود.
4. **O-01:** مالک معیار تاریخی بازیابی اصل 7B v0.4/7C v0.3 را با تکمیل، پذیرش و ثبت نسخه‌های کنترل‌شده جدید 7B/7C/7D جایگزین کرده است. این تصمیم در CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 v0.1 ثبت شده است. بنابراین SR-12 دیگر «دریافت CR از مالک» نیست؛ معیار جدید از نظر تصمیم مالک پذیرفته شده، اما بسته‌شدن O-01 همچنان منوط به پذیرش اسناد و شواهد است.

وضعیت اصلاح‌شده: SR-01 CLOSED؛ SR-02 SOURCE SNAPSHOT VERIFIED / CANONICAL RECONCILIATION OPEN؛ SR-03..SR-11 مطابق وضعیت فنی خود باز می‌مانند؛ O-01 REPLACEMENT CR OWNER-APPROVED / DELIVERABLE ACCEPTANCE PENDING.
