# MDG-WP06-7B-POSTING-RULE-CLOSURE-001
## رجیستر تکمیل قواعد ثبت رویدادهای مالی FE-01 تا FE-38

| مشخصه | مقدار |
|---|---|
| Document ID | MDG-WP06-7B-POSTING-RULE-CLOSURE-001 |
| Version | 0.1 |
| Classification | CONTROLLED RECONSTRUCTION — DESIGN CLOSURE WORKPAPER |
| Baseline | MDG-ARCH-MASTER-001 v2.5 |
| Inputs | Integrated 7B/7C/7D v0.3; 7C Account Disposition v0.1; OWN-7C-043; ADR-0017 |
| Design status | EVENT COVERAGE RECORDED; POSTING RULES PARTIALLY OPEN |
| Implementation | NOT VERIFIED |
| Tests | NOT RUN / NOT VERIFIED |
| Evidence | NOT ACCEPTED |
| Posting Release | NOT GRANTED |

## 1. هدف و قاعده حاکم

این workpaper، مرحله بعد از ماتریس تعیین تکلیف حساب‌های 7C است: تعیین وضعیت قاعده ثبت برای هر رویداد FE-01 تا FE-38 و مشخص‌کردن داده/تصمیم/آزمون لازم برای بستن آن.

این سند به‌جای تکمیل حدسی Debit/Credit، برای هر رویداد سه وضعیت را جدا می‌کند:
- **DECISION-DEFINED:** اصل سیاست یا محدودیت از تصمیم مالک/ADR روشن است.
- **PARTIALLY MAPPED:** بخشی از حساب‌ها یا رفتارها قابل اشاره است، اما قرارداد کامل ثبت احراز نشده.
- **BLOCKED / OPEN:** تا تکمیل منبع، ویژگی حساب، Posting Matrix یا کنترل لازم هیچ ثبت مجازی نیست.

قاعده سخت: وجود یک FE، نام حساب یا جهت اقتصادی ظاهری، به‌تنهایی مجوز انتخاب حساب مقابل، شناسایی درآمد/هزینه، ایجاد Journal یا فعال‌سازی حساب نیست. Debit/Credit فقط وقتی canonical می‌شود که هر دو طرف، Recognition Time، Amount Basis، زیرمعین، ابعاد، محدودیت‌ها و تست مربوط از منبع معتبر/تصمیم پذیرفته‌شده قابل ردیابی باشند.

## 2. قرارداد الزامی هر Posting Rule

برای هر FE، رکورد canonical باید حداقل فیلدهای زیر را داشته باشد:

`FEId, EventName, SourceEvent, Trigger, Preconditions, RecognitionTime, SourceDocument, DebitAccountCode, DebitCondition, CreditAccountCode, CreditCondition, AmountBasis, CostBasis, TaxMode, SubledgerUpdates, Dimensions, InventoryEffect, PartyEffect, PostingIdentity, IdempotencyKey, ReversalRule, PeriodRule, AtomicityBoundary, RetryBehavior, Authorization, AuditEvents, TestCaseIDs, EvidenceRequirements, SourceRefs, DesignStatus, OpenIssueIDs`.

قواعد تکمیلی:
1. حساب‌های تجمیعی/کنترلی و حساب‌های زیرمعین نباید به‌جای هم استفاده شوند.
2. رویداد دریافت وجه، به‌تنهایی درآمد نیست؛ رویداد تخصیص وجه نیز نباید درآمد تکراری بسازد.
3. برگشت سند مالی نهایی‌شده با حذف/ویرایش خاموش انجام نمی‌شود؛ اصلاح از مسیر reversal/correction قابل ردیابی است.
4. Posting مالی و اثر موجودی/زیرمعین باید در مرز اتمیک واحد یا قرارداد اتمیک مستند و آزموده شوند.
5. Retry باید idempotent باشد؛ SourceEvent/SourceVersion/PostingIdentity یکتا و قابل ردیابی باشند.
6. دوره بسته، حساب غیرفعال، حساب NOT VERIFIED، TaxMode=0 و رویداد ناشناخته باید fail closed شوند.
7. هیچ حساب یا Test ID تاریخی فاقد متن منبع، با حدس تکمیل نمی‌شود.

## 3. ماتریس FE-01 تا FE-38

| FE | موضوع رویداد | وضعیت قاعده | حساب/سیاست قابل اتکا | مانع اصلی بستن |
|---|---|---|---|---|
| FE-01 | فروش کالا | PARTIALLY MAPPED | مطابق integrated baseline v0.3، ارتباط اولیه با 4101؛ حساب مقابل و شرایط کامل ثبت احراز نشده | سند فروش، تحویل، Recognition Time، مبلغ، مالیات، حساب مقابل و زیرمعین |
| FE-02 | ارائه خدمت/تعمیر | PARTIALLY MAPPED | مطابق integrated baseline v0.3، ارتباط اولیه با 4102؛ زمان شناخت تابع تحقق/تحویل معتبر | سند خدمت، تکمیل/تحویل، Recognition Time، مبلغ، مالیات و حساب مقابل |
| FE-03 | دریافت وجه | PARTIALLY MAPPED | دریافت وجه به‌تنهایی درآمد نیست؛ مسیر نقد/بانک/PSP باید تفکیک شود | روش دریافت، حساب مقصد، تسویه، کارمزد، تخصیص و Posting Identity |
| FE-04 | پیش‌دریافت مشتری | PARTIALLY MAPPED | 2105 برای پیش‌دریافت مطرح است | زمان ایجاد/مصرف تعهد، بازپرداخت، تخصیص به سند نهایی و حساب وجه |
| FE-05 | تخصیص دریافت به بدهی/فاکتور | PARTIALLY MAPPED | تخصیص مستقل از دریافت؛ درآمد تکراری ممنوع | قواعد مانده، تخصیص جزئی/چندگانه، مازاد، reversal و تطبیق |
| FE-06 | خرید/فاکتور تأمین‌کننده | PARTIALLY MAPPED | 2101 بدهی فاکتورشده؛ 110701 مالیات خرید قابل بازیافت مشروط؛ 2103 برای GRNI کالا | زمان فاکتور در برابر دریافت کالا، TaxMode/Tax Matrix، هزینه/موجودی و حساب‌های مقابل |
| FE-07 | دریافت فیزیکی کالا | PARTIALLY MAPPED | 110801 تنها GL inventory control؛ 110802–110806 زیرمعین | سند دریافت، مالکیت/کنترل، مقدار/بهای ورودی، تطبیق با 2103 و atomicity |
| FE-08 | فروش کالا و بهای تمام‌شده | PARTIALLY MAPPED | 5101؛ WAC در ProductVariant×Warehouse و Specific Identification برای سریال‌دارها | قاعده کامل مقدار/هزینه، حساب موجودی کنترل، همزمانی فروش و خروج و تست ارزش‌گذاری |
| FE-09 | خروج قطعه برای تعمیر | BLOCKED | 110807 در R1 غیرفعال؛ COGS پیش از تحویل/شرط شناسایی ممنوع | مالکیت قطعه، مصرف/برگشت/ضایعات، حساب‌های مجاز و قرارداد تعمیر؛ تا تکمیل مسدود |
| FE-10 | برگشت فروش | PARTIALLY MAPPED | برگشت مالی از فروش اصلی قابل ردیابی؛ FIN-TST-010 ارجاع موجود | تفکیک credit/refund از برگشت فیزیکی، قیمت/مالیات/COGS و سند reversal |
| FE-10b | برگشت فیزیکی موجودی | PARTIALLY MAPPED | اثر موجودی مستقل از refund وجه | کنترل سلامت/قابلیت فروش، بهای برگشتی، warehouse و لینک به اصل |
| FE-11 | برگشت خرید | PARTIALLY MAPPED | برگشت به خرید/تأمین‌کننده اصلی متصل باشد | برگشت فیزیکی در برابر credit note، مالیات، موجودی و مانده 2101/2103 |
| FE-12 | بازپرداخت وجه مشتری | BLOCKED | 2107 حساب Refund Payable نیست؛ بدهی بازپرداخت مجزا هنوز احراز نشده | تعیین حساب بدهی/وجه، مجوز، refund lifecycle، برگشت/شکست پرداخت و تست |
| FE-13 | تخفیف | PARTIALLY MAPPED | ماهیت Contra Revenue؛ 4201–4203 mapping باز | کد/علت تخفیف، NormalBalance، زمان اعمال، سقف مجاز و اتصال به سند |
| FE-14 | چرخه چک | PARTIALLY MAPPED | تصمیم مالک OWN-7C-004-R1: 110401 نزد صندوق، 110402 واگذارشده به بانک، 110403 در جریان وصول، 110404 برگشتی در انتظار تعیین تکلیف؛ برگشت واقعی و تأییدشده طلب را بازسازی می‌کند | تطبیق source-title/attributes workbook با نقش‌های OWN-7C-004-R1، چرخه وضعیت/انتقال، تاریخ‌ها، عدم دوباره‌شماری و Test Case کامل |
| FE-15 | هزینه‌های جانبی خرید / Landed Cost | BLOCKED | 6104 حساب عمومی Landed Cost نیست | تفکیک ماهیت اقتصادی، سرمایه‌ای‌سازی/هزینه، مبنای تخصیص و سیاست مصوب |
| FE-16 | مغایرت شمارش موجودی | PARTIALLY MAPPED | 110808 برای کسری تعلیقی؛ 2113 برای مازاد شمارش | شمارش مستقل، تأیید، تحقیق، رفع تعلیق، جلوگیری از درآمد/هزینه خودکار |
| FE-17 | مغایرت صندوق | PARTIALLY MAPPED | 2112 حساب تعلیقی؛ 610801 فقط کسری نقدی قطعی و تأییدشده | شمارش، انتساب علت، سطح تأیید، آستانه، زمان انتقال از تعلیق |
| FE-18 | انتقال وجه/تسویه | PARTIALLY MAPPED | 1110 فقط وجه نقد فیزیکی در راه؛ 110302 PSP | تفکیک انتقال داخلی، clearing، PSP و بانک؛ عدم دوباره‌شماری کارمزد/مانده |
| FE-19 | خسارت/ضایعات | BLOCKED | 6201 نقش نیازمند احراز؛ هزینه بر اساس ماهیت واقعی | تمایز خرابی، سرقت، کسری، خسارت تکنسین و کسورات؛ سند و مسئولیت |
| FE-20 | خدمت برون‌سپاری | BLOCKED | 2114 فقط خدمت دریافت/تأییدشده با تعهد اثبات‌پذیر؛ 2102 غیرفعال؛ 5103 مسدود تا تکمیل | اثبات خدمت، تأیید، Recognition، حساب هزینه، invoice settlement و reversal |
| FE-21 | Trade-In | PARTIALLY MAPPED | خرید و فروش دو سند مستقل با TradeInId مشترک؛ عدم تهاتر؛ 110803 پس از پذیرش مالکیت و شناسایی موجودی | valuation، پذیرش مالکیت، سندهای مستقل، IMEI duplicate guard و تست انتهابه‌انتها |
| FE-22 | کارمزد بانک/PSP | PARTIALLY MAPPED | 6106/6107 مطرح؛ 110302 PSP؛ جلوگیری از دوباره‌شماری | تفکیک fee، net settlement، gross receivable، bank statement و reconciliation |
| FE-23 | کمیسیون عملیاتی | PARTIALLY MAPPED | 5104 و 2108؛ پس از تأیید تکمیل فنی معتبر | eligibility، محاسبه، سقف/نرخ مصوب، کسورات، reversal و جدایی از payroll |
| FE-24 | دارایی ثابت | BLOCKED FOR R1 / R2 SCOPE | 1201 بهای تمام‌شده؛ 1202 استهلاک انباشته؛ 6105 هزینه استهلاک | Fixed Asset Subledger، شناخت/رسیدگی/استهلاک/واگذاری و پذیرش R2 |
| FE-25 | اقساط | DEFERRED R2 | قاعده R1 تثبیت نشده | قرارداد، schedule، بهره/کارمزد، سررسید، تسویه و تست‌های R2 |
| FE-26 | ارز | DEFERRED R2 | قاعده R1 تثبیت نشده | نرخ مرجع، تاریخ، تسعیر، realized/unrealized و rounding R2 |
| FE-27 | مالیات | PARTIALLY MAPPED / CONDITIONAL | 110701 مالیات خرید قابل بازیافت؛ 1109/2109 فقط TaxMode و Tax Matrix معتبر؛ TaxMode=0 هیچ Tax Line/Posting ندارد | Tax Matrix، effective dates، مسیرهای UI/API/manual/import/integration و تست منفی |
| FE-28 | مانده افتتاحیه | PARTIALLY MAPPED | 3199 کنترل موقت افتتاحیه | شواهد مانده، منبع، تأیید مستقل، تطبیق و صفرکردن کنترل موقت |
| FE-29 | بستن دوره مالی | PARTIALLY MAPPED | قفل دوره؛ سند دستی معلق مانع بستن؛ بازگشایی با تأیید مستقل دوم | چک‌لیست reconciliation، pending items، مجوزها، دوره‌های باز و آزمون قفل |
| FE-30 | سند دستی | PARTIALLY MAPPED | نقش مجاز، مجوز حساب، audit trail و تفکیک وظایف لازم | ماتریس مجوز، محدودیت حساب، تأیید، پیوست/دلیل، reversal و آزمون امنیتی |
| FE-31 | انتقال بین انبارها | DEFERRED R2 | توسعه R2 | مالکیت، in-transit، دو مرحله ارسال/دریافت، مغایرت و atomicity R2 |
| FE-32 | WIP عمومی | DEFERRED R2 | 110807 حساب WIP نیست و در R1 غیرفعال است | تعریف دامنه WIP، هزینه‌های قابل جذب، تسویه و زیرمعین R2 |
| FE-33 | دریافت/پرداخت و تخصیص | PARTIALLY MAPPED | 2107 دریافت تخصیص‌نیافته؛ 110703 پرداخت خروجی تخصیص‌نیافته؛ مستقل از Refund Payable | چرخه تخصیص/عدم تخصیص، برگشت، تطبیق و جلوگیری از netting |
| FE-34 | بستن سال مالی | PARTIALLY MAPPED | idempotency، شناسه یکتا، atomicity و mapping حقوق مالکانه معتبر لازم | mapping حقوق مالکانه، carry-forward، کنترل دوباره اجرا و تأیید نهایی |
| FE-35 | هزینه عمومی | PARTIALLY MAPPED | 610x بر اساس ماهیت اقتصادی واقعی؛ 110702 فقط پیش‌پرداخت واقعی | taxonomy هزینه، مدرک، مالیات، تخصیص، پیش‌پرداخت در برابر هزینه و مجوز |
| FE-36 | رویداد ناشناخته | DECISION-DEFINED — BLOCK ON UNKNOWN | رویداد ناشناخته باید fail closed و بدون Posting بماند | ثبت رخداد، هشدار/صف بررسی، جلوگیری از fallback mapping و آزمون منفی |
| FE-37 | حقوق و کسورات | DEFERRED R2 (PAYROLL); COMMISSION SEPARATE | Payroll R2؛ کمیسیون عملیاتی از FE-23 است | جلوگیری از اختلاط payroll و commission؛ قرارداد R2 |
| FE-38 | حقوق مالکانه | BLOCKED | 3101–3105 mapping/NormalBalance باز؛ 3199 موقت | تطبیق منبع مادر/سیاست equity، opening evidence و تصویب mapping؛ بدون حدس |

## 4. قیود حسابی که در تمام FEها لازم‌الاجرا هستند

| حساب/گروه | قید لازم‌الاجرا |
|---|---|
| 110801 | تنها GL inventory control؛ جزئیات در زیرمعین موجودی |
| 110802–110806 | دسته‌های زیرمعین؛ به‌عنوان GL مستقل استفاده نشوند |
| 110807 | غیرفعال R1؛ Posting جدید ممنوع؛ WIP عمومی نیست |
| 110809 | ممنوع؛ ایجاد/استفاده ممنوع |
| 110701 | مالیات خرید قابل بازیافت؛ TaxMode و Tax Matrix معتبر |
| 1109 / 2109 | فقط با TaxMode و Tax Matrix معتبر؛ TaxMode=0 در همه کانال‌ها no tax line/no posting |
| 2101 / 2103 / 2114 | به‌ترتیب بدهی فاکتورشده، کالای دریافت‌شده فاکتورنشده، خدمت دریافت/تأییدشده فاکتورنشده؛ جایگزین یکدیگر نیستند |
| 2107 / 110703 | دریافت ورودی تخصیص‌نیافته / پرداخت خروجی تخصیص‌نیافته؛ معادل یا Refund Payable نیستند |
| 2114 | تنها با خدمت دریافت/تأییدشده و تعهد قابل اثبات |
| 2102 / 2106 / 110602 / 620201 | غیرفعال تا احراز منبع/نقش مطابق تصمیمات مالک |
| 2104 | تسویه موقت موجودی منفی با event معتبر و aging/settlement control |
| 2113 | کنترل تعلیقی مازاد شمارش؛ درآمد خودکار نیست |
| 110302 | clearing/PSP طبق قرارداد؛ دوباره‌شماری fee/settlement ممنوع |
| 1201 / 1202 / 6105 | دارایی ثابت و استهلاک؛ دامنه کامل R2 |
| 6108 / 610801 | 6108 تجمیعی/non-postable؛ 610801 فقط کسری نقدی قطعی و تأییدشده |
| 3101–3105 / 3199 | mapping حقوق مالکانه باز؛ 3199 فقط کنترل موقت افتتاحیه |
| 4107 / 5106 | سود/زیان گردکردن؛ پس از collision check، NormalBalance، Posting Matrix و آزمون؛ ADR-0017 به‌تنهایی مجوز ثبت نیست |

## 5. سیاست مبلغ و گردکردن

مطابق ADR-0017:
- تمام مبالغ پولی از ابتدا تا انتهای محاسبه به عدد صحیح IRR نگهداری می‌شوند.
- گردکردن به روش AwayFromZero است.
- تخصیص باقیمانده با Largest Remainder و ترتیب deterministic انجام می‌شود.
- اختلاف مجاز rounding سند حداکثر ±1 IRR نسبت به مرجع rational دقیق است.
- 100,000 IRR per line موضوع جداگانه و OPEN است؛ این عدد نه tolerance و نه سقف خطای rounding است.
- وجود 4107/5106 بدون اثبات عدم برخورد کد و قواعد ثبت، مجوز ساخت journal line نیست.

## 6. کنترل‌های مشترک لازم برای هر رویداد

هر FE باید شواهد آزمون برای این کنترل‌ها را ارائه کند، در صورت کاربرد:
1. منبع و شناسه رویداد یکتا؛
2. تکرار درخواست و idempotency؛
3. جلوگیری از دوباره‌شماری درآمد، وجه، موجودی یا کارمزد؛
4. atomicity میان GL و subledger/inventory؛
5. retry پس از timeout یا شکست میانی؛
6. reversal/correction بدون تغییر خاموش سند نهایی؛
7. دوره بسته و تاریخ مؤثر؛
8. حساب غیرفعال/ممنوع/NOT VERIFIED؛
9. TaxMode=0 و مالیات نامعتبر؛
10. مجوز کاربر، تفکیک وظایف و Audit Trail؛
11. مبالغ صفر، منفی، مرزی و integer rounding؛
12. reconciliation و invariant مانده‌های مرتبط.

این فهرست، معیار طراحی آزمون است و به‌معنای وجود Test Case مصوب یا اجرای موفق آزمون‌ها نیست. Test ID جدید فقط با ثبت در 7D و برچسب PROPOSED ساخته می‌شود.

## 7. اقلام باز 7B برای بستن

| ID | موضوع | معیار بستن | وضعیت |
|---|---|---|---|
| 7B-CL-001 | تکمیل Source/Recognition برای FE-01/02 | منبع رویداد، زمان شناخت، سند، حساب‌ها و تست قابل ردیابی | OPEN |
| 7B-CL-002 | دریافت/پیش‌دریافت/تخصیص FE-03/04/05 | تفکیک وجوه، تعهد، تخصیص و عدم درآمد تکراری | OPEN |
| 7B-CL-003 | خرید و دریافت FE-06/07 | تفکیک invoice/AP، GRNI، receipt و tax | OPEN |
| 7B-CL-004 | فروش و COGS FE-08/09 | valuation، ownership، inventory movement، ممنوعیت FE-09 تا قرارداد | OPEN |
| 7B-CL-005 | برگشت/Refund FE-10/10b/11/12 | تفکیک سند مالی، کالای فیزیکی، refund payable و cash movement | OPEN |
| 7B-CL-006 | تخفیف FE-13 | نگاشت 420x، reason code، contra revenue و tests | OPEN |
| 7B-CL-007 | چک FE-14 | انطباق کامل با OWN-7C-004-R1 و چرخه وضعیت/حساب/تست | OPEN |
| 7B-CL-008 | Landed cost FE-15 | سیاست ماهیت و تخصیص مصوب | OPEN |
| 7B-CL-009 | مغایرت‌ها FE-16/17/19 | کنترل تعلیق، علت، تأیید، رفع تعلیق و آزمون | OPEN |
| 7B-CL-010 | انتقال/کارمزد FE-18/22 | PSP/bank/cash clearing و reconciliation بدون دوباره‌شماری | OPEN |
| 7B-CL-011 | برون‌سپاری FE-20 | اثبات خدمت، 2114 و تکمیل هزینه/تست | OPEN |
| 7B-CL-012 | Trade-In FE-21 | دو سند مستقل، ownership، valuation و IMEI controls | OPEN |
| 7B-CL-013 | کمیسیون FE-23 و Payroll FE-37 | تکمیل commission و جداسازی payroll R2 | OPEN |
| 7B-CL-014 | دارایی ثابت/اقساط/FX/WIP/warehouse FE-24/25/26/31/32 | تعریف R2 و کنترل منع فعال‌سازی زودهنگام | OPEN |
| 7B-CL-015 | Tax FE-27 | Tax Matrix، effective dating و no-tax مسیرهای همه کانال‌ها | OPEN |
| 7B-CL-016 | Opening/period/year close FE-28/29/34/38 | equity mapping، evidence، close/reopen و idempotency | OPEN |
| 7B-CL-017 | Manual journal FE-30 | permission matrix، SoD، audit، reversal و تست | OPEN |
| 7B-CL-018 | هزینه عمومی FE-35 | ماهیت واقعی، پیش‌پرداخت و مدارک | OPEN |
| 7B-CL-019 | Unknown event FE-36 | fail-closed و آزمون منفی end-to-end | OPEN |
| 7B-CL-020 | FE-to-COA-to-Test crosswalk | هر FE به حساب canonical و Test Case معتبر وصل شود | OPEN |

## 8. ترتیب اجرای کار برای تکمیل

1. تثبیت صفات حساب 7C و تفکیک حساب‌های R1/R2/Inactive/Control.
2. تکمیل FEهای R1 با source document و Recognition Rule.
3. تعیین Debit/Credit فقط برای FEهایی که هر دو حساب و شرطشان قابل احراز است.
4. تکمیل Subledger/Dimensions و Posting Identity/Idempotency/Reversal/Atomicity.
5. تکمیل Test Case و Expected Result در 7D و اتصال هر Test ID به FE و حساب.
6. ممیزی مستقل crosswalk و تثبیت hash.
7. پس از شواهد لازم، درخواست CR برای اصلاح O-01 در Architecture v2.5؛ این سند به‌تنهایی O-01 را نمی‌بندد.

## 9. وضعیت نهایی

- FE coverage: 38/38 indexed.
- Fully specified posting rules: NOT ESTABLISHED.
- Debit/Credit completeness: PARTIAL / NOT VERIFIED.
- Account eligibility: NOT GRANTED.
- Tests: NOT RUN / NOT VERIFIED.
- Evidence: NOT ACCEPTED.
- Gate 3 / Gate 4: OPEN / OPEN.
- Posting Release: NOT GRANTED.

این سند یک workpaper بازسازی‌شده برای تکمیل طراحی است؛ نه سند تاریخی بازیابی‌شده، نه مجوز پیاده‌سازی، و نه مجوز ثبت مالی.


## اصلاحیه معیار تحویل 7B — 2026-11-18

با تأیید معیار جدید O-01، متن تاریخی مستقل 7B v0.4 دیگر شرط الزامی پذیرش نیست. نسخه جایگزین جدید باید Controlled Reconstruction / Controlled Replacement باشد. تمام 38 رویداد FE-01..FE-38 باید یا Posting Rule کامل با منبع و دو سمت ثبت و تمام فیلدهای قرارداد §2 داشته باشند، یا به‌صورت صریح BLOCKED/DEFERRED با دلیل، شرط رفع، مالک اقدام، Test ID و ممنوعیت Posting ثبت شوند. پوشش اسمی FE یا نامزد حساب به‌تنهایی تکمیل محسوب نمی‌شود.

در نسخه فعلی، موارد FE-09، FE-12، FE-15، FE-19، FE-20، FE-24 و FE-38 صریحاً BLOCKED هستند و سایر رویدادها نیز تا تکمیل فیلدهای اجباری باید PARTIAL/OPEN باقی بمانند. این اصلاحیه وضعیت طراحی را به PASS تغییر نمی‌دهد.

 
## ارجاع نسخه جایگزین 7B v0.2 — 2026-11-18

نسخه مستقل جایگزین 7B در مسیر `docs/finance/workpapers/p4h/7b/WP06-P4H-7B-POSTING-MATRIX-001_v0.2.md` ثبت شد. این نسخه برای تمام 38 رویداد FE-01..FE-38 و FE-10b (39 ردیف رویدادی) disposition ثبت جدید را مشخص می‌کند؛ هر مورد ناقص در وضعیت BLOCKED/DEFERRED با شرط رفع و Test ID مستقل قرار دارد. شناسه‌های آزمون MDG-FIN-GATE-001..039 در 7D v0.2 تعریف شده‌اند.

این تکمیل، پوشش disposition را در سطح طراحی gate به 39/39 می‌رساند؛ اما Posting Ruleهای مثبت، debit/credit canonical و اجرای مالی همچنان بازند. 7B v0.2 هنوز DRAFT / NOT ACCEPTED است.
