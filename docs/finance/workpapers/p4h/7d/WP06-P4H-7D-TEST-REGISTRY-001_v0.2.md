# WP06-P4H-7D-TEST-REGISTRY-001
## Test Registry & FE↔COA↔Test Crosswalk — Controlled Replacement

| مشخصه | مقدار |
|---|---|
| Version | 0.2 |
| Classification | CONTROLLED RECONSTRUCTION / CONTROLLED REPLACEMENT |
| Parent | WP06-P4H-7D-TEST-REGISTRY-001 v0.1 |
| Architecture | MDG-ARCH-MASTER-001 v2.5 |
| O-01 Change Request | CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 v0.1 — OWNER APPROVED |
| Design | R1 FAIL-CLOSED CONTROL CASES SPECIFIED FOR 39 FE EVENTS + 16 COA OPEN ITEMS + 12 SHARED CONTROLS |
| Positive accounting journal cases | BLOCKED UNTIL 7B/7C CANONICAL |
| Execution | NOT RUN / NOT VERIFIED |
| Evidence | NOT ACCEPTED |
| Posting Release | NOT GRANTED |

## 1. هدف و اصل طراحی

این نسخه بدون ادعای بازیابی متن تاریخی، مجموعه آزمون‌های کنترل‌شده جدید را تعریف می‌کند. شناسه‌های تاریخی FIN-TST/COA-TST/ATOMIC/INV-CAN/INV-NEG فقط به‌عنوان SOURCE-ID حفظ می‌شوند. شناسه‌های این نسخه مستقل‌اند: `MDG-FIN-GATE` برای رویدادهای FE، `MDG-COA-GATE` برای شکاف‌های canonical COA و `MDG-FIN-*` برای کنترل‌های مشترک/سراسری.

اصل آزمون: تا وقتی 7B برای یک FE قاعده کامل و 7C برای حساب‌های آن ویژگی‌های canonical و مجوزها را تعیین نکرده‌اند، آزمون نباید Journal مالی حدسی بسازد. نتیجه مورد انتظار در این حالت `POSTING_BLOCKED`، صفر Journal جدید، صفر mutation در GL/Subledger و ثبت Audit Event قابل ردیابی است. برای FEهای R2 نتیجه مورد انتظار در R1 برابر `SCOPE_BLOCKED` است.

## 2. قرارداد محیط و اجرای مشترک

تمام آزمون‌ها باید در disposable PostgreSQL test database با fixture نسخه‌دار، زمان ثابت، داده مستقل از production و cleanup اتمیک اجرا شوند. هر اجرا باید ثبت کند: commit SHA، schema/migration version، database engine/version، test runner/version، fixture hash، input hash، output, event/audit logs، GL journal snapshot، Subledger snapshot، row counts قبل/بعد و نتیجه PASS/FAIL/BLOCKED.

### Assertions مشترک
- هیچ Journal یا JournalLine بدون Posting Rule معتبر ایجاد نشود.
- هیچ حساب INACTIVE / PROHIBITED / NOT VERIFIED / NOT POSTABLE مقصد ثبت جدید نشود.
- هیچ تغییر Subledger بدون سند مرجع و transaction boundary معتبر رخ ندهد.
- Retry یک SourceEvent/SourceVersion/PostingIdentity نباید اثر مالی دوم ایجاد کند.
- شکست میانی باید تمام تغییرات مالی و زیرمعین را rollback یا از مسیر recovery مستند و idempotent تکمیل کند.
- سند Finalized immutable باشد؛ اصلاح فقط با reversal/adjustment مرتبط با سند اصلی انجام شود.
- TaxMode=0 در تمام کانال‌ها صفر TaxLine و صفر TaxJournal تولید کند.
- خطای مجوز، دوره بسته، حساب نامعتبر، mapping ناقص و event ناشناخته باید fail closed باشند.

## 3. Test Cases برای FE-01..FE-38 به‌علاوه FE-10b

| Test ID | FE | سناریو / Fixture | اقدام | نتیجه مورد انتظار | ممنوعیت/Assertion منفی | شواهد |
|---|---|---|---|---|---|---|
| MDG-FIN-GATE-001 | FE-01 | فروش کالا؛ 4101 فقط candidate؛ debit/credit و recognition ناقص | ارسال فروش قبل از تکمیل rule و سپس با mapping ناقص | POSTING_BLOCKED؛ صفر Journal و صفر خروج موجودی | عدم شناسایی درآمد قبل از trigger تحویل معتبر؛ عدم حدس حساب مقابل | request، block reason، journal/stock snapshots |
| MDG-FIN-GATE-002 | FE-02 | خدمت/تعمیر؛ 4102 candidate؛ completion evidence ناقص | ارسال پرونده خدمت باز/تکمیل‌نشده | POSTING_BLOCKED تا تحقق/تحویل و rule کامل | خدمت باز درآمد نشود؛ Journal یا commission زودهنگام ممنوع | work-order state، audit، journal snapshot |
| MDG-FIN-GATE-003 | FE-03 | دریافت وجه با ماهیت تخصیص‌نیافته یا بدون source classification | ثبت receipt با روش cash/bank/PSP و mapping ناقص | دریافت در حالت کنترل‌شده باقی بماند؛ Posting مالی مسدود تا rule کامل | receipt به‌تنهایی revenue نسازد؛ عدم دوباره‌شماری PSP | receipt state، PSP reference، GL/subledger diff |
| MDG-FIN-GATE-004 | FE-04 | پیش‌دریافت مشتری؛ 2105 با subledger/recognition ناقص | دریافت پیش از تحویل و سپس تخصیص به سفارش | POSTING_BLOCKED تا mapping و liability lifecycle کامل؛ پس از آن تست مثبت با rule نهایی | پیش‌دریافت revenue نشود؛ 2105 با 2106/2107 مخلوط نشود | advance ledger، allocation trace، journal diff |
| MDG-FIN-GATE-005 | FE-05 | تخصیص partial/multiple receipt به یک invoice | ارسال دو allocation و retry همان allocation | تخصیص یکتا و مانده صحیح؛ تا rule نهایی، posting مسدود | درآمد دوباره‌شناسایی نشود؛ over-allocation رد شود | allocation ids، balance snapshots، idempotency key |
| MDG-FIN-GATE-006 | FE-06 | فاکتور تأمین‌کننده قبل/بعد از دریافت؛ 2101/2103/110701 | ثبت invoice، goods receipt و tax-mode variations | هر مرحله فقط با rule و Tax Matrix معتبر؛ در غیر این صورت fail closed | AP، GRNI و recoverable tax ادغام نشوند؛ TaxMode=0 هیچ tax line ندهد | invoice/receipt refs، tax trace، journal snapshot |
| MDG-FIN-GATE-007 | FE-07 | دریافت فیزیکی کالا؛ کنترل GL 110801 و subledger دسته‌ای | دریافت با SKU/warehouse/qty و سپس retry | inventory receipt فقط یک بار؛ GL effect تا rule کامل مسدود | GL posting مستقل به 110802..110806 ممنوع؛ receipt بدون سند ممنوع | inventory movement، GL/subledger diff، retry log |
| MDG-FIN-GATE-008 | FE-08 | فروش موجودی با WAC در ProductVariant×Warehouse و Specific Identification برای سریال‌دار | فروش یک کالای معمولی و یک serial item | مقدار/بهای تمام‌شده از valuation policy؛ تا canonical rule و valuation service، posting blocked | منفی شدن موجودی یا cost قبل از خروج معتبر ممنوع؛ روش valuation بین کالاها مخلوط نشود | valuation inputs, layers, movement, journal snapshot |
| MDG-FIN-GATE-009 | FE-09 | خروج قطعه برای تعمیر؛ 110807 inactive R1 | تلاش مصرف قطعه قبل از تحویل یا بدون ownership/consumption evidence | BLOCKED؛ صفر COGS و صفر posting به 110807 | استفاده جدید از 110807 ممنوع؛ COGS زودهنگام ممنوع | repair order، inventory state، denied journal |
| MDG-FIN-GATE-010 | FE-10 | برگشت فروش مالی بدون برگشت فیزیکی و بالعکس | ارسال credit/refund و physical return به‌صورت مستقل | دو اثر مستقل و linked؛ هرکدام تا rule کامل مسدود | refund خودکار physical stock نسازد؛ stock return خودکار cash refund نسازد | original doc link، return/refund states، snapshots |
| MDG-FIN-GATE-011 | FE-10b | برگشت فیزیکی کالا با وضعیت سالم/معیوب | ثبت return inspection و warehouse disposition | فقط movement مجاز پس از inspection؛ GL posting تابع FE-10/COGS rule | کالای معیوب به موجودی قابل فروش برنگردد؛ دوباره‌شماری ممنوع | inspection record، stock movement، audit |
| MDG-FIN-GATE-012 | FE-11 | برگشت خرید با/بدون credit note | ثبت return against original purchase | برگشت باید به اصل خرید و receipt وصل شود؛ posting تا rule کامل blocked | payable/tax/inventory به‌صورت مستقل و بی‌سند تغییر نکند | original purchase link، supplier credit, journal snapshot |
| MDG-FIN-GATE-013 | FE-12 | refund request، payment failed، payment succeeded؛ Refund Payable نامشخص | اجرای refund lifecycle با 2107 به‌عنوان candidate ممنوع | POSTING_BLOCKED؛ no refund payable journal تا تعیین حساب و rule | 2107 هرگز Refund Payable تلقی نشود؛ پرداخت ناموفق بدهی را تسویه نکند | refund state machine، payment callback، journal diff |
| MDG-FIN-GATE-014 | FE-13 | discount با reason codeهای مختلف؛ 4201..4203 unresolved | اعمال تخفیف مجاز/غیرمجاز | تخفیف بدون account mapping/approval معتبر blocked | discount به هزینه عمومی یا حساب درآمد تصادفی منتقل نشود؛ سقف و reason enforce شود | discount approval، reason, invoice totals |
| MDG-FIN-GATE-015 | FE-14 | چرخه چک مطابق OWN-7C-004-R1 | انتقال 110401→110402→110403 و حالت برگشتی 110404 | هر transition مجاز، یکتا و audit شدنی؛ journal تا mapping canonical blocked | برگشت چک بدون evidence و approval، یا انتقال دوباره، ممنوع | cheque lifecycle, bank evidence, audit trail |
| MDG-FIN-GATE-016 | FE-15 | هزینه حمل/بیمه/گمرک/هزینه خرید با ماهیت‌های متفاوت | ثبت هزینه تکمیلی و تخصیص به خرید | BLOCKED تا سیاست capitalization/expense و allocation تصویب شود | 6104 به‌عنوان Landed Cost عمومی استفاده نشود؛ تخصیص بی‌مبنا ممنوع | invoice, nature classification, allocation basis |
| MDG-FIN-GATE-017 | FE-16 | شمارش موجودی کسری/مازاد؛ 110808/2113 suspense | ثبت شمارش، بازشماری و تأیید مستقل | suspense state و investigation ثبت شود؛ transfer به P&L تا علت/approval blocked | مازاد 2113 درآمد خودکار نشود؛ کسری بدون شمارش قطعی هزینه نشود | count sheets, approvals, suspense aging |
| MDG-FIN-GATE-018 | FE-17 | مغایرت صندوق؛ 2112 suspense و 610801 برای کسری قطعی | ثبت اختلاف و تأیید مسئول مستقل | ابتدا suspense؛ 610801 فقط پس از اثبات و approval | اختلاف حل‌نشده مستقیم expense نشود؛ یک کسری دوبار ثبت نشود | cash count, variance case, approval, GL diff |
| MDG-FIN-GATE-019 | FE-18 | انتقال وجه نقد/بانک/PSP؛ 1110 فقط cash-in-transit فیزیکی | انتقال داخلی و PSP settlement | clearing legها مستقل و قابل reconciliation؛ posting تا rule کامل blocked | 1110 برای bank/PSP transit غیرنقدی استفاده نشود؛ 110302 دوباره‌شماری نشود | transfer refs، PSP settlement، reconciliation |
| MDG-FIN-GATE-020 | FE-19 | خسارت، ضایعات، سرقت، خرابی، مسئولیت تکنسین | ایجاد case با علت نامشخص و سپس evidence/approval | ابتدا investigation/suspense؛ expense/recovery تا nature و approval کامل blocked | 6201 یا دریافتنی تکنسین بر اساس حدس انتخاب نشود؛ payroll deduction خودکار ممنوع | incident file، cause, approvals، journal diff |
| MDG-FIN-GATE-021 | FE-20 | خدمت برون‌سپاری دریافت/تأییدشده یا نشده؛ 2114/5103 | ثبت invoice بدون service evidence و سپس service acceptance | 2114 فقط برای خدمت دریافت و تأییدشده با تعهد قابل اثبات؛ 5103 تا rule کامل blocked | 2102 inactive؛ 2103 فقط goods GRNI؛ invoice تنها evidence of service نیست | service acceptance، vendor doc، accrual trace |
| MDG-FIN-GATE-022 | FE-21 | Trade-In: خرید دستگاه دست‌دوم و فروش کالای جدید | ایجاد دو سند با TradeInId مشترک | دو سند مستقل و قابل تطبیق؛ 110803 فقط پس از ownership acceptance/recognition | netting ممنوع؛ IMEI به‌عنوان transaction id ممنوع؛ مالکیت نامشخص، موجودی نسازد | two document ids، TradeInId، ownership evidence |
| MDG-FIN-GATE-023 | FE-22 | کارمزد بانکی و PSP؛ 6106/6107 | ثبت statement fee و settlement | fee بر اساس statement/contract و بدون تکرار ثبت شود؛ mapping ناقص blocked | کارمزد از اختلاف مانده 110302 دوباره هزینه نشود | bank/PSP statement, fee id, reconciliation |
| MDG-FIN-GATE-024 | FE-23 | کمیسیون پس از تکمیل فنی معتبر | ثبت commission قبل و بعد از technician completion | قبل از تکمیل blocked؛ بعد از completion و policy کامل eligible | کمیسیون با payroll یا خسارت تکنسین خلط نشود؛ duplicate payout ممنوع | work completion, commission calc, approval |
| MDG-FIN-GATE-025 | FE-24 | دارایی ثابت در R1 | تلاش ایجاد دارایی/استهلاک در R1 | SCOPE_BLOCKED در R1؛ R2 فقط پس از فعال‌سازی مجاز | 1201/1202/6105 خارج از مجوز R2 ثبت نشوند | feature flag, scope rejection, no journal |
| MDG-FIN-GATE-026 | FE-25 | اقساط و مطالبات زمان‌دار در R1 | تلاش ایجاد schedule و posting اقساط | SCOPE_BLOCKED در R1 | هیچ receivable/payable یا interest journal ناخواسته ایجاد نشود | scope flag, API response, DB diff |
| MDG-FIN-GATE-027 | FE-26 | ارز و تسعیر در R1 | تلاش ثبت معامله ارزی/تسعیر | SCOPE_BLOCKED در R1 | نرخ ارز ثابت/حدسی و gain/loss posting ممنوع | scope response، no journal |
| MDG-FIN-GATE-028 | FE-27 | TaxMode=0، TaxMode نامعتبر و Tax Matrix منقضی | ارسال tax lines از UI/API/import/integration/manual journal | TaxMode=0: zero tax lines/postings در همه کانال‌ها؛ invalid matrix: fail closed | هیچ bypass channel مجاز نیست؛ نرخ hard-coded ممنوع | channel matrix، tax audit, journal diff |
| MDG-FIN-GATE-029 | FE-28 | opening balance بدون source evidence یا با evidence کامل | بارگذاری مانده افتتاحیه | بدون evidence/approval blocked؛ 3199 صرفاً temporary control طبق rule | مانده بی‌منبع به income/equity منتقل نشود | opening pack, source docs, approvals, journal |
| MDG-FIN-GATE-030 | FE-29 | close period با unreconciled accounts و pending journals | درخواست close و reopen | close با blocker رد شود؛ reopen با role/reason/second approval | دوره بسته ثبت جدید نپذیرد؛ pending journal نادیده گرفته نشود | close checklist، lock state، approval logs |
| MDG-FIN-GATE-031 | FE-30 | manual journal با roleهای مجاز/غیرمجاز | submit، approve، self-approve و modify finalized journal | SoD enforce؛ self-approval denied؛ final journal immutable | user بدون permission، inactive account یا period closed posting نکند | permission trace، audit events، journal history |
| MDG-FIN-GATE-032 | FE-31 | انتقال بین انبارها در R1 | ایجاد transfer در R1 | SCOPE_BLOCKED تا مجوز R2 | هیچ کاهش/افزایش موجودی یا GL movement نیمه‌کاره ایجاد نشود | scope response، stock diff |
| MDG-FIN-GATE-033 | FE-32 | WIP عمومی در R1 و 110807 | ایجاد WIP و تلاش استفاده از 110807 | SCOPE_BLOCKED؛ 110807 جایگزین WIP نیست | هیچ WIP/COGS یا journal به 110807 ساخته نشود | feature flag، account guard، journal diff |
| MDG-FIN-GATE-034 | FE-33 | دریافت ورودی تخصیص‌نیافته و پرداخت خروجی تخصیص‌نیافته | ایجاد receipt و payment unallocated | جهت و ماهیت جدا؛ 2107 و 110703 به‌جای هم استفاده نشوند | receipt با payment net نشود؛ تخصیص duplicate ممنوع | receipt/payment refs، subledger aging |
| MDG-FIN-GATE-035 | FE-34 | year close، retry و duplicate close request | اجرای close دوباره با همان identity و تغییر source version | idempotent; one close effect; changed source version rejected/reviewed | duplicate closing journal یا انتقال دوباره equity ممنوع | close identity، journal ids، audit |
| MDG-FIN-GATE-036 | FE-35 | هزینه تحقق‌یافته در برابر پیش‌پرداخت واقعی | ثبت هزینه با invoice/period evidence و پیش‌پرداخت | 110702 فقط برای prepayment واقعی؛ هزینه بر اساس ماهیت و دوره؛ تا taxonomy نهایی blocked | payment date به‌تنهایی expense recognition نسازد | invoice/service period, allocation schedule |
| MDG-FIN-GATE-037 | FE-36 | unknown event و unknown mapping | ارسال EventType و AccountCode ناشناخته | UNKNOWN_EVENT / UNKNOWN_MAPPING fail closed؛ zero journal | fallback account یا generic suspense ممنوع | request, rejection reason, zero journal proof |
| MDG-FIN-GATE-038 | FE-37 | payroll R2 و commission separation | ارسال payroll در R1 | SCOPE_BLOCKED؛ FE-23 commission مستقل باقی بماند | payroll journal یا deduction بدون قرارداد R2 ممنوع | scope response، no journal proof، commission crosswalk |
| MDG-FIN-GATE-039 | FE-38 | equity mapping unresolved | year close بدون mapping معتبر 3101..3105 | BLOCKED تا mapping/evidence و approval | equity account از حدس انتخاب نشود؛ 3199 فقط temporary opening control | equity mapping proof، close blocker، journal diff |

## 4. آزمون‌های مشترک اتمیک، هویت، برگشت و کنترل حساب

| Test ID | حوزه | روش | نتیجه مورد انتظار |
|---|---|---|---|
| MDG-FIN-ATOMIC-001 | FE-06/07 | تزریق شکست پس از هر write boundary | rollback کامل یا recovery idempotent؛ نه orphan journal و نه orphan inventory |
| MDG-FIN-ATOMIC-002 | FE-08 | شکست پس از خروج موجودی و قبل از Journal | کل عملیات rollback؛ هیچ stock/GL mismatch |
| MDG-FIN-ATOMIC-003 | FE-12 | شکست در پرداخت/ثبت refund liability | هیچ posting حدسی؛ وضعیت refund قابل retry و قابل ردیابی |
| MDG-FIN-IDEM-001 | تمام FE | اجرای هم‌زمان دو درخواست با PostingIdentity یکسان | فقط یک اثر مالی؛ درخواست دوم duplicate-safe |
| MDG-FIN-IDEM-002 | تمام FE | همان SourceEvent با SourceVersion متفاوت | conflict/review؛ بدون overwrite یا posting تکراری خودکار |
| MDG-FIN-REV-001 | finalized journal | تلاش edit/delete سپس reversal | edit/delete denied؛ reversal linked و balanced |
| MDG-FIN-PERIOD-001 | تمام FE | posting در دوره بسته و reopen با مجوز | posting denied؛ reopen نیازمند role، reason و approval دوم |
| MDG-FIN-COA-001 | COA | direct posting به summary/non-postable/inactive/unverified account | rejection server-side و DB-level |
| MDG-FIN-COA-002 | 4107/5106 | namespace/registry/DB unique constraint collision test | هر collision مانع activation؛ بدون collision نیز Posting فقط با rule و eligibility |
| MDG-FIN-TAX-001 | همه channels | TaxMode=0 در UI/API/import/integration/manual journal | صفر tax line و zero tax journal |
| MDG-FIN-SUBLEDGER-001 | 110801 و زیرمعین | GL control vs subledger reconciliation | mismatch، posting/close مرتبط را متوقف کند |
| MDG-FIN-IMEI-001 | FE-21 | IMEI فعال تکراری | عملیات متوقف و review؛ history حفظ شود |

## 5. Crosswalk و Provenance

- MDG-FIN-GATE-001..039 هرکدام یک FE را پوشش می‌دهند؛ مورد 038 دو مسیر FE-37/38 را جداگانه با دو assertion مستقل پوشش می‌دهد.
- هر Test ID جدید از نوع RECONSTRUCTED است؛ SOURCE-IDهای تاریخی FIN-TST-001..042، COA-TST-001..034، ATOMIC-001..010، INV-CAN-001..038 و INV-NEG-TST-001..012 بدون متن اصلی صرفاً شناسه تاریخی‌اند.
- هر ردیف باید به PostingRuleVersion و AccountRegistryVersion نهایی bind شود. تا آن زمان ExpectedJournal برای مسیرهای غیرمجاز/ناقص `NONE — POSTING BLOCKED` و برای مسیرهای مثبت مالی `NOT SPECIFIED — BLOCKED PENDING 7B/7C` است.
- 7B و 7C باید در نسخه‌های بعدی دقیقاً به این Test IDها ارجاع دهند.

## 6. معیار پذیرش 7D

1. همه FE-01..FE-38 به حداقل یک test case مشخص و قابل اجرا متصل‌اند.
2. آزمون‌های مشترک atomicity, idempotency, reversal, period lock, account eligibility, tax, subledger reconciliation و IMEI ثبت شده‌اند.
3. هر test case دارای fixture, input, action, expected outcome, negative assertions و evidence requirements است.
4. هیچ آزمون تاریخی فاقد متن به‌عنوان آزمون کامل معرفی نشده است.
5. پس از تثبیت 7B/7C، برای هر posting rule مجاز، positive journal assertion دقیق با expected debit/credit و amount basis تکمیل می‌شود.
6. اجرای واقعی و evidence acceptance جدا از design approval باقی می‌ماند.

## 6.1 آزمون‌های تکمیلی canonical COA — 7C-OPEN-001..016

این 16 آزمون مکمل برای کنترل fail-closed و پذیرش تدریجی canonical COA تعریف شده‌اند. این‌ها به 39 آزمون FE در جدول اصلی اضافه می‌شوند؛ در کنار 12 آزمون مشترک بخش 4، 7D در مجموع 67 Test ID مستقل دارد: 39 مورد FE، 16 مورد COA و 12 مورد مشترک/سراسری. SOURCE-IDهای تاریخی در این عدد شمرده نمی‌شوند. هیچ‌یک اجرا نشده است.

| Test ID | 7C Open | سناریو | اقدام | نتیجه مورد انتظار | Evidence plan |
|---|---|---|---|---|---|
| MDG-COA-GATE-001 | 7C-OPEN-001 | حسابی در 76 ردیف بدون crosswalk کامل v1.4 | تلاش برای تأیید canonical release | UNVERIFIED باقی بماند؛ POSTING_BLOCKED؛ هیچ attribute حدس زده نشود | crosswalk diff، source refs، block log |
| MDG-COA-GATE-002 | 7C-OPEN-002 | AccountId تکراری یا قابل reuse | ثبت/تغییر registry با شناسه تکراری | unique constraint رد کند؛ هیچ حسابی با شناسه مبهم فعال نشود | DB constraint output، duplicate fixture، audit |
| MDG-COA-GATE-003 | 7C-OPEN-003 | Parent cycle یا رابطه استنباط‌شده از prefix | بارگذاری hierarchy نامعتبر | cycle/prefix-only relation رد شود؛ hierarchy تأیید نشود | graph fixture، cycle detector output، lineage |
| MDG-COA-GATE-004 | 7C-OPEN-004 | Nature/NormalBalance نامشخص یا متناقض | درخواست Postable شدن حساب | status UNVERIFIED و POSTING_BLOCKED؛ هیچ سمت ثبت از روی نام استنباط نشود | attribute validation report، rejected command |
| MDG-COA-GATE-005 | 7C-OPEN-005 | حساب INACTIVE/PROHIBITED/NOT VERIFIED/NOT POSTABLE | تلاش ثبت مستقیم و غیرمستقیم | server و DB هر دو رد کنند؛ صفر Journal و صفر Subledger mutation | API response، DB logs، before/after snapshots |
| MDG-COA-GATE-006 | 7C-OPEN-006 | Subledger owner/dimensions ناقص یا نامعتبر | ثبت حساب نیازمند subledger بدون مرجع معتبر | transaction fail-closed؛ هیچ GL/Subledger mismatch باقی نماند | contract validation، transaction log، reconciliation |
| MDG-COA-GATE-007 | 7C-OPEN-007 | EffectiveFrom/To نامعتبر یا retroactive overwrite | ایجاد نسخه حساب با تاریخ اثر متعارض | نسخه قبلی immutable؛ تغییر نامعتبر رد و audit شود | temporal fixtures، version diff، audit trail |
| MDG-COA-GATE-008 | 7C-OPEN-008 | استفاده از 2114 قبل از تکمیل صفات و FE-20 | ارسال posting برای 2114 | POSTING_BLOCKED؛ design-only حفظ شود | account snapshot، FE-20 block reason، zero-journal proof |
| MDG-COA-GATE-009 | 7C-OPEN-009 | collision در namespace/reserved/history registry برای 4107 یا 5106 | تلاش ثبت یا فعال‌سازی کد متعارض | collision رد؛ هیچ کد جدید فعال نشود | full namespace scan، unique index proof، audit |
| MDG-COA-GATE-010 | 7C-OPEN-010 | source title/attribute با OWN-7C-004-R1 تعارض دارد | ساخت crosswalk چرخه چک | تصمیم مالک مقدم؛ اختلاف خام حفظ و lineage ثبت؛ نقش‌ها بدون crosswalk کامل قابل posting نیستند | source/policy diff، cheque lifecycle fixture، audit |
| MDG-COA-GATE-011 | 7C-OPEN-011 | 3101..3105 بدون توافق equity و NormalBalance | تلاش برای posting opening/equity | POSTING_BLOCKED؛ هیچ equity mapping حدسی ساخته نشود | approval-state fixture، rejected journal، audit |
| MDG-COA-GATE-012 | 7C-OPEN-012 | orphan account، FE یا Test در crosswalk | اجرای traceability validation | orphan گزارش شود؛ بسته تا رفع/ثبت disposition تأیید نشود | bidirectional matrix، orphan scan output، review record |

| MDG-COA-GATE-013 | 7C-OPEN-013 | 4201–4203 بدون ReasonCode/FE یا با نگاشت تکراری | ثبت تخفیف/کاهنده درآمد با mapping ناقص | POSTING_BLOCKED؛ تخفیف به هزینه عملیاتی یا حساب نامرتبط منتقل نشود؛ 4107/5106 با 42xx خلط نشوند | account-to-FE map، reason-code fixture، rejected journal |
| MDG-COA-GATE-014 | 7C-OPEN-014 | OWN-7C-040 TradeIn scope approval بدون GL account mapping | تلاش ثبت مستقل 110803/2111 پیش از crosswalk | scope approval به‌تنهایی مجوز GL نیست؛ POSTING_BLOCKED تا Nature/NormalBalance/GL-Subledger role تأیید شود | TradeIn two-document fixture، account mapping diff، zero-journal proof |
| MDG-COA-GATE-015 | 7C-OPEN-015 | قرارداد صندوق/بانک/Party/تکنسین ناقص یا ناسازگار | ثبت cash/bank/AR/technician receivable با subledger ناقص | POSTING_BLOCKED؛ هیچ Party/Statement/Shift/Recovery status جعلی یا ناقص پذیرفته نشود | subledger contract fixtures، reconciliation outputs، rejected requests |
| MDG-COA-GATE-016 | 7C-OPEN-016 | PSP settlement با cash-in-transit مخلوط یا بدون TransferId/three-way tie-out | تلاش تسویه 110302/1110 با مرجع ناقص | POSTING_BLOCKED؛ PSP principal و physical cash transfer دوباره‌شماری نشوند | PSP/bank/cash transfer fixtures، tie-out report، zero-duplicate proof |

### وضعیت اجرای آزمون‌های COA
- Test design: SPECIFIED — FAIL-CLOSED ONLY.
- Execution: NOT RUN / NOT VERIFIED.
- Evidence: NOT ACCEPTED.
- Positive canonical account/journal assertions: BLOCKED until the corresponding 7C row and 7B rule are accepted.
- هیچ PASS اجرایی از تعریف این Test IDها استنتاج نمی‌شود.

## 7. وضعیت

- Test design: CONTROLLED R1 GATE-CONTROL SUITE SPECIFIED — 39 FE tests + 16 COA tests + 12 shared control tests (67 total).
- Positive accounting journal assertions: BLOCKED PENDING FINAL 7B/7C.
- Test execution: NOT RUN / NOT VERIFIED.
- Evidence: NOT ACCEPTED.
- Gate 4: OPEN.
- Posting Release: NOT GRANTED.
