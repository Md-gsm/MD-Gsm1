# WP06-P4H-7D-TEST-REGISTRY-001
## Test Registry & FE↔COA↔Test Crosswalk

| مشخصه | مقدار |
|---|---|
| Version | 0.1 |
| Classification | CONTROLLED RECONSTRUCTION / CONTROLLED REPLACEMENT — DRAFT |
| Architecture | MDG-ARCH-MASTER-001 v2.5 |
| Owner criterion | CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 v0.1 — OWNER APPROVED |
| Technical status | PARTIAL / OPEN |
| Implementation | NOT VERIFIED |
| Tests | NOT RUN / NOT VERIFIED |
| Evidence | NOT ACCEPTED |
| Posting Release | NOT GRANTED |

این نسخه، تحویل‌دادنی کنترل‌شده جایگزین برای 7D است؛ متن تاریخی مستقل 7D را ادعا نمی‌کند. برای آزمون‌های تاریخی فاقد متن، شناسه‌های SOURCE-ID حفظ و آزمون‌های جدید با شناسه مستقل RECONSTRUCTED تعریف می‌شوند.

---

# 8. خط مبنای 7D — رجیستری آزمون‌ها

## 8.1 طبقه‌بندی منشأ آزمون

| وضعیت | معنی |
|---|---|
| SOURCE_VERIFIED | متن آزمون در منبع اصلی احراز شده |
| SOURCE_ID_ONLY | شناسه دیده شده، متن کامل در دسترس نیست |
| RECONSTRUCTED | متن یا دامنه آزمون بازسازی شده |
| PROPOSED | آزمون جدید پیشنهادی |
| IMPLEMENTED | آزمون در کد پیاده‌سازی شده |
| EXECUTED | آزمون واقعاً اجرا شده |
| ACCEPTED | نتیجه و شواهد پذیرفته شده |

این وضعیت‌ها مستقل‌اند. RECONSTRUCTED به معنی EXECUTED نیست.

## 8.2 شناسه‌های قابل مشاهده

| گروه | شناسه | وضعیت |
|---|---|---|
| FIN-TST | 001–034 | شناسه‌های ثبت‌شده؛ متن باید با منبع تفصیلی تطبیق شود |
| FIN-TST | 035–042 | ID ONLY |
| FIN-TST | 043–053 | در رجیستر بازسازی‌شده دیده شده‌اند؛ منشأ و متن هر مورد باید جداگانه احراز شود |
| COA-TST | 001–034 | متن مستقل اصلی احراز نشده |
| ATOMIC | 001–010 | متن کامل نیازمند احراز |
| INV-CAN | 001–038 | متن کامل نیازمند احراز |
| INV-NEG-TST | 001–012 | متن کامل نیازمند احراز |

## 8.3 موضوعات ثبت‌شده در بازسازی

| ID | موضوع ثبت‌شده |
|---|---|
| FIN-TST-046 | چرخه عمر چک |
| FIN-TST-047 | هزینه تعهدی و پیش‌پرداخت |
| FIN-TST-048 | تسویه موجودی منفی |
| FIN-TST-049 | برگشت کمیسیون در برابر خسارت تکنسین |
| FIN-TST-050 | ضمانت و تعمیر مجدد |
| FIN-TST-051 | حقوق مالکانه، افتتاحیه و بستن سال |
| FIN-TST-052 | Posting Identity، SourceVersion، Idempotency و Reversal |
| FIN-TST-053 | مغایرت نقدی و موجودی |

این جدول اثبات‌کننده متن تاریخی کامل آزمون‌ها نیست. متن هر آزمون باید در Test Registry مستقل، همراه با Source Class و Acceptance Status ثبت شود.

## 8.4 آزمون‌های پیشنهادی

شناسه‌های زیر پیشنهادهای همین بازسازی‌اند و شناسه تاریخی محسوب نمی‌شوند:

| Proposed ID | موضوع |
|---|---|
| PROPOSED-7B-001 | جلوگیری از ثبت دوباره رویداد |
| PROPOSED-7B-002 | کنترل یکتایی Posting Identity |
| PROPOSED-7B-003 | Fail Closed برای رویداد ناشناخته |
| PROPOSED-7B-004 | کنترل TaxMode=0 در تمام مسیرها |
| PROPOSED-7B-005 | تفکیک دریافت وجه و شناسایی درآمد |
| PROPOSED-7B-006 | تفکیک 2101، 2103 و 2114 |
| PROPOSED-7B-007 | جلوگیری از استفاده از حساب‌های غیرفعال و ممنوع |
| PROPOSED-7B-008 | تفکیک 2107 و 110703 |
| PROPOSED-7B-009 | کنترل سقف اثر گرد کردن با مرجع rational دقیق |
| PROPOSED-7B-010 | کنترل Reversal و حفظ سابقه سند |

پیش از انتقال این پیشنهادها به Test Registry اجرایی، یکتایی شناسه‌ها و عدم تداخل با شناسه‌های موجود باید بررسی شود.

## 8.5 وضعیت اجرا

| حوزه | وضعیت |
|---|---|
| Financial Tests | NOT RUN |
| COA Tests | NOT RUN |
| Atomicity Tests | NOT RUN |
| Inventory Tests | NOT RUN |
| Negative Tests | NOT RUN |
| Evidence Acceptance | NOT ACCEPTED |
| Independent Review | NOT VERIFIED |

---

# 9. Crosswalk یکپارچه

## 9.1 قاعده

Crosswalk باید سه اتصال مستقل داشته باشد:
1. FE به COA؛
2. FE به Test Case؛
3. COA به Test Case.

نگاشت بر اساس شباهت عنوان یا حدس مجاز نیست.

## 9.2 FE-to-COA-to-Test Register

| FE | حساب‌های قابل احراز یا مطرح‌شده | آزمون مرتبط | وضعیت |
|---|---|---|---|
| FE-01 | 4101؛ حساب مقابل باز | آزمون فروش؛ شناسه دقیق نیازمند تطبیق | OPEN |
| FE-02 | 4102؛ حساب مقابل باز | آزمون شناسایی خدمت | OPEN |
| FE-03 | حساب دریافت و ماهیت مقابل نیازمند تعیین | آزمون دریافت بدون درآمد | OPEN |
| FE-04 | 2105 | آزمون پیش‌دریافت | CONDITIONAL |
| FE-05 | حساب‌های دریافت و درآمد مرتبط | آزمون تخصیص | OPEN |
| FE-06 | 2101، 2103، 110701 | آزمون خرید و مالیات | CONDITIONAL |
| FE-07 | 110801 و زیرمعین | آزمون دریافت فیزیکی | CONDITIONAL |
| FE-08 | 5101 و 110801 | آزمون بهای تمام‌شده | CONDITIONAL |
| FE-09 | 5102؛ 110807 غیرفعال | آزمون خروج قطعه | BLOCKED |
| FE-10 | حساب‌های برگشت فروش و موجودی | FIN-TST-010؛ متن باید احراز شود | CONDITIONAL |
| FE-10b | 110801 و زیرمعین | آزمون برگشت فیزیکی | OPEN |
| FE-11 | حساب‌های برگشت خرید | آزمون برگشت خرید | OPEN |
| FE-12 | حساب بازپرداخت تعیین نشده | آزمون Refund | BLOCKED |
| FE-13 | Contra Revenue و 420x | آزمون تخفیف | OPEN |
| FE-14 | حساب‌های چرخه چک؛ نگاشت باز | FIN-TST-046؛ متن بازسازی‌شده/نیازمند احراز | OPEN |
| FE-15 | 6104 حساب عمومی نیست | آزمون هزینه تکمیلی خرید | OPEN |
| FE-16 | حساب‌های کنترل کسری و 2113 برای اضافه | آزمون شمارش موجودی | CONDITIONAL |
| FE-17 | 2112 و 610801 با شرایط تأیید | FIN-TST-053 | CONDITIONAL |
| FE-18 | 1110 و 110302 با تفکیک ماهیت | آزمون انتقال وجه | OPEN |
| FE-19 | 6201 و حساب‌های مرتبط | آزمون خسارت | OPEN |
| FE-20 | 2114 و 5103؛ 2102 غیرفعال | آزمون خدمت و تعهد؛ شناسه نیازمند تثبیت | BLOCKED |
| FE-21 | 110803 پس از پذیرش مالکیت | آزمون Trade-In | OPEN |
| FE-22 | 6106، 6107 و 110302 | آزمون کارمزد | OPEN |
| FE-23 | 5104 و 2108 | آزمون کمیسیون | CONDITIONAL |
| FE-24 | 1201، 1202 و 6105 | آزمون دارایی ثابت | R2 / LIMITED |
| FE-25 | حساب‌های مرتبط با اقساط | آزمون R2 | R2 |
| FE-26 | حساب‌های مرتبط با تسعیر | آزمون R2 | R2 |
| FE-27 | 110701، 1109 و 2109 | آزمون TaxMode | CONDITIONAL |
| FE-28 | 3199 و حساب‌های افتتاحیه | FIN-TST-051 | CONDITIONAL |
| FE-29 | وضعیت دوره و اسناد | آزمون بستن دوره | OPEN |
| FE-30 | حساب‌های مجاز سند دستی | آزمون Permission و Audit | OPEN |
| FE-31 | انتقال بین انبارها | آزمون R2 | R2 |
| FE-32 | WIP؛ 110807 غیرمجاز | آزمون R2 | R2 |
| FE-33 | 110703 و 2107 | آزمون تخصیص دریافت و پرداخت | CONDITIONAL |
| FE-34 | حقوق مالکانه و Posting Identity | FIN-TST-051 و 052؛ منشأ متن نیازمند احراز | OPEN |
| FE-35 | حساب هزینه بر اساس ماهیت واقعی | آزمون هزینه تعهدی/پیش‌پرداخت؛ نگاشت نیازمند تکمیل | OPEN |
| FE-36 | بدون حساب مجاز برای رویداد ناشناخته | آزمون Fail Closed | BLOCKED |
| FE-37 | حقوق و دستمزد R2؛ کمیسیون FE-23 | آزمون تفکیک حقوق و کمیسیون | R2 / OPEN |
| FE-38 | 3101 تا 3105 و 3199 | FIN-TST-051؛ نگاشت نیازمند تکمیل | OPEN |

## 9.3 محدودیت Crosswalk

این جدول Crosswalk سطح رویداد است. برای پذیرش نهایی، ماتریس حساب‌به‌آزمون لازم است که برای هر حساب ویژگی مورد آزمون، FE مصرف‌کننده، مسیر ثبت، ورودی، خروجی مورد انتظار و شواهد پذیرش را مشخص کند.

تا زمان تکمیل ماتریس حساب‌به‌آزمون، Crosswalk کامل محسوب نمی‌شود.

---

# MDG-WP06-7D-TEST-CLOSURE-001
## رجیستر تکمیل آزمون‌ها و Crosswalk مالی FE ↔ COA ↔ Test

| مشخصه | مقدار |
|---|---|
| Document ID | MDG-WP06-7D-TEST-CLOSURE-001 |
| Version | 0.1 |
| Classification | CONTROLLED RECONSTRUCTION — TEST DESIGN WORKPAPER |
| Baseline | MDG-ARCH-MASTER-001 v2.5 |
| Inputs | 7B Posting Rule Closure v0.1; 7C Account Disposition v0.1; Integrated 7B/7C/7D v0.3; OWN-7C-043; ADR-0017 |
| Test design | PARTIALLY SPECIFIED — OPEN GAPS REGISTERED |
| Execution | NOT RUN / NOT VERIFIED |
| Evidence | NOT ACCEPTED |
| Gate 4 | OPEN |
| Posting Release | NOT GRANTED |

## 1. هدف و قواعد منبع

این workpaper آزمون‌ها را به FE و COA وصل می‌کند و برای هر آزمون پیش‌شرط، ورودی، رفتار مورد انتظار، کنترل منفی و شواهد لازم را مشخص می‌کند.

طبقه‌بندی شناسه‌ها:
- **SOURCE-ID:** شناسه‌ای که وجود آن در رجیستر/منبع قبلی قابل مشاهده است؛ وجود شناسه به‌تنهایی متن آزمون را بازیابی نمی‌کند.
- **RECONSTRUCTED:** متن آزمون از قواعد حاکم و workpaperهای قابل احراز بازسازی شده است.
- **PROPOSED:** آزمون جدید پیشنهادی است و نباید به‌عنوان شناسه تاریخی معرفی شود.
- **BLOCKED:** تا تکمیل قاعده 7B/صفات canonical 7C، Expected Result مالی قطعی ندارد.

هیچ آزمونی در این سند اجرا نشده است. عبارت Expected Result به معنای انتظار طراحی است، نه نتیجه PASS.

## 2. قرارداد الزامی Test Case

هر آزمون قابل پذیرش باید شامل این فیلدها باشد:
`TestId, ProvenanceClass, RequirementRefs, FEIds, AccountCodes, Purpose, Preconditions, Fixture, Input, Action, ExpectedBusinessState, ExpectedJournal, ExpectedSubledger, ExpectedAudit, NegativeAssertions, Cleanup, IsolationBoundary, EvidenceArtifacts, ExecutionStatus, Reviewer, SourceHash`.

برای آزمون مالی، Expected Journal فقط وقتی مقداردهی می‌شود که Debit/Credit در 7B قطعی و حساب‌های 7C canonical باشند. در غیر این صورت آزمون باید رفتار fail-closed یا بخش غیرمالی را بررسی کند و Expected Journal صریحاً NOT SPECIFIED بماند.

## 3. شناسه‌های موجود و وضعیت منبع

| شناسه | طبقه | وضعیت متن/منبع | تصمیم این workpaper |
|---|---|---|---|
| FIN-TST-001..034 | SOURCE-ID | شناسه‌ها در رجیستر قابل مشاهده؛ متن کامل هر مورد در این بسته احراز نشده | حفظ شناسه؛ از نسبت‌دادن Expected Result حدسی خودداری |
| FIN-TST-035..042 | SOURCE-ID ONLY | شناسه‌محور؛ متن اصلی احراز نشده | باز؛ متن/منبع لازم |
| FIN-TST-043..053 | RECONSTRUCTED REFERENCE | در workpaper 7D بازسازی‌شده تعریف موضوعی دارند | جزئیات آزمون در این سند تکمیل می‌شود؛ Original ادعا نمی‌شود |
| COA-TST-001..034 | SOURCE-ID | شناسه‌ها ثبت شده؛ متن اصلی/Expected Result احراز نشده | حفظ شناسه؛ متن بازسازی فقط با برچسب RECONSTRUCTED جداگانه |
| ATOMIC-001..010 | SOURCE-ID | شناسه‌ها ثبت شده؛ متن کامل احراز نشده | باز؛ این سند فقط خانواده کنترل را مشخص می‌کند |
| INV-CAN-001..038 | SOURCE-ID | شناسه‌ها ثبت شده؛ متن کامل احراز نشده | باز؛ انتظار canonical از متن اصلی لازم است |
| INV-NEG-TST-001..012 | SOURCE-ID | شناسه‌ها ثبت شده؛ متن کامل احراز نشده | باز؛ انتظار canonical از متن اصلی لازم است |

## 4. بازسازی آزمون‌های FIN-TST-043..053

این بخش متن اجرایی پیشنهادی برای موضوعات ثبت‌شده در workpaper بازسازی‌شده است؛ نه ادعای بازیابی متن تاریخی.

| ID | FE / COA | هدف و پیش‌شرط | رفتار مورد انتظار | وضعیت |
|---|---|---|---|---|
| FIN-TST-043 | FE-01/02; 4101/4102 | آزمون Recognition دو رویداد درآمدی مطابق integrated baseline v0.3: FE-01 فروش کالا و FE-02 ارائه خدمت؛ شروط نهایی 7B ناقص | هیچ درآمدی پیش از trigger معتبر شناسایی نشود؛ Expected Journal تا تکمیل قواعد و حساب مقابل NOT SPECIFIED بماند | RECONSTRUCTED / BLOCKED |
| FIN-TST-044 | FE-03/05; 110302/110703/2107 | دریافت وجه و سپس تخصیص جداگانه؛ شناسه منبع یکتا | دریافت و تخصیص دو اثر مجزا؛ تخصیص نباید درآمد دوم بسازد؛ duplicate request نباید posting تکراری بسازد | RECONSTRUCTED |
| FIN-TST-045 | FE-06/07; 2101/2103/110701/110801 | فاکتور خرید و دریافت کالا در ترتیب‌های مختلف؛ TaxMode معتبر/نامعتبر | تفکیک AP، GRNI، receipt و tax؛ TaxMode=0 هیچ tax line/posting تولید نکند؛ Journalهای نهایی تا تکمیل حساب‌ها BLOCKED | RECONSTRUCTED / PARTIAL |
| FIN-TST-046 | FE-14; cheque lifecycle | گردش چک با نقش‌های OWN-7C-004-R1: 110401 نزد صندوق، 110402 واگذارشده به بانک، 110403 در جریان وصول، 110404 برگشتی در انتظار تعیین تکلیف؛ شناسه چک یکتا | هر transition فقط یک‌بار اعمال شود؛ برگشت واقعی و تأییدشده طلب را طبق OWN-7C-004-R1 بازسازی کند؛ source-title crosswalk و Test Case کامل هنوز باز است | RECONSTRUCTED; OWN-7C-004-R1 policy / source-title crosswalk open |
| FIN-TST-047 | FE-35; 610x/110702 | تمایز هزینه تحقق‌یافته از پیش‌پرداخت واقعی | طبقه‌بندی صرفاً با ماهیت و مدرک معتبر؛ 110702 فقط پیش‌پرداخت واقعی؛ حساب هزینه دقیق تا taxonomy باز، NOT SPECIFIED | RECONSTRUCTED / BLOCKED |
| FIN-TST-048 | FE-08/09/16; 110801/110807/110808/2113/2104 | کسری/مازاد شمارش، negative inventory و خروج قطعه تعمیر | 110807 در R1 غیرفعال؛ منفی موجودی بدون settlement مجاز نباید پنهانی نادیده گرفته شود؛ 2104 فقط مسیر settlement معتبر؛ 110808/2113 در نقش تعلیقی | RECONSTRUCTED / PARTIAL |
| FIN-TST-049 | FE-19/23; 5104/2108/6201 | تفکیک کمیسیون از خسارت تکنسین | commission فقط بعد از تکمیل فنی تأییدشده؛ خسارت و کسر از تکنسین بدون قاعده/مجوز/مدرک ثبت نشود؛ payroll با commission ادغام نشود | RECONSTRUCTED / PARTIAL |
| FIN-TST-050 | FE-20; 2114/5103/2102 | خدمت برون‌سپاری دریافت و تأیید نشده/شده | 2114 فقط در صورت دریافت و تأیید خدمت و تعهد قابل اثبات؛ 2102 inactive؛ ثبت هزینه/5103 تا تکمیل قاعده مسدود | RECONSTRUCTED / BLOCKED |
| FIN-TST-051 | FE-28/29/34/38; 3199/3101..3105 | افتتاحیه، بستن دوره و سال با pending journal | مانده افتتاحیه نیازمند مدرک/تأیید؛ pending manual journal مانع close؛ reopen نیازمند تأیید مستقل دوم؛ equity mapping باز، year close مسدود | RECONSTRUCTED / PARTIAL |
| FIN-TST-052 | FE-30/34/36 | Posting Identity، SourceVersion، retry، reversal و unknown event | تکرار همان منبع نباید Journal دوم بسازد؛ SourceVersion تغییر یافته باید قابل تشخیص باشد؛ unknown event fail-closed؛ reversal به‌صورت سند قابل ردیابی | RECONSTRUCTED |
| FIN-TST-053 | FE-16/17; 110808/2112/2113/610801 | مغایرت نقد و موجودی | کسری/مازاد ابتدا در مسیر تعلیقی معتبر؛ 610801 فقط کسری نقدی قطعی و تأییدشده؛ مازاد 2113 درآمد خودکار نیست | RECONSTRUCTED / PARTIAL |

## 5. ماتریس خانواده ATOMIC

این ماتریس معیار طراحی است، نه متن تاریخی شناسه‌های ATOMIC-001..010. نگاشت شماره‌به‌شماره به شناسه تاریخی تا بازیابی منبع باز می‌ماند.

| خانواده آزمون | کنترل لازم | FEهای نمونه | Expected Result |
|---|---|---|---|
| اتمیک بودن GL و Subledger | شکست هر سمت نباید commit ناقص باقی بگذارد | FE-07/08/21 | یا کل عملیات مرتبط commit شود یا هیچ اثر مالی/موجودی ناقص باقی نماند |
| atomic reversal | برگشت و آثار مرتبط در یک boundary | FE-10/11/14 | هیچ وضعیت نیمه‌برگشت‌شده یا مانده ناسازگار باقی نماند |
| idempotent retry | تکرار درخواست با همان identity | FE-03/05/22 | یک اثر مؤثر؛ پاسخ retry قابل ردیابی |
| concurrency | دو عملیات همزمان روی منبع/موجودی/مانده یکسان | FE-05/08/21 | invariantها حفظ؛ lost update یا oversell غیرمجاز رخ ندهد |
| period boundary | ثبت همزمان با close/lock | FE-29/34 | عملیات مطابق ترتیب قطعی مجاز/رد شود؛ ثبت پس از lock ممنوع |
| failure injection | شکست پس از هر مرحله داخلی | FE-06/07/08 | rollback یا recovery اتمیک؛ نه orphan journal و نه orphan subledger |
| tax atomicity | شکست Tax Matrix یا TaxMode | FE-06/27 | بدون tax line ناقص؛ TaxMode=0 بدون tax posting |
| cheque transition | شکست بین transition و accounting effect | FE-14 | state و journal ناسازگار نشوند |
| refund atomicity | شکست در پرداخت/ثبت بدهی | FE-12 | چون حساب مقابل هنوز OPEN است، هیچ posting حدسی؛ صرفاً fail closed |
| audit atomicity | شکست ثبت audit در عملیات حساس | FE-29/30 | عملیات حساس بدون audit لازم commit نشود |

## 6. ماتریس خانواده Inventory Canonical و Negative

### 6.1 INV-CAN-001..038

شناسه‌های 001..038 حفظ می‌شوند، اما شماره‌به‌شماره متن تاریخی آن‌ها در دسترس نیست. تا زمان بازیابی منبع، این پوشش canonical در سطح requirement ثبت می‌شود و به شناسه خاص نسبت داده نمی‌شود:
- موجودی در سطح ProductVariant × Warehouse؛
- سریال/IMEI و جلوگیری از شناسه فعال تکراری؛ IMEI شناسه تراکنش نیست؛
- WAC برای اقلام غیرسریالی و Specific Identification برای اقلام سریالی، مطابق قاعده موجود؛
- 110801 تنها GL control؛ 110802..110806 subledger categories؛
- 110807 inactive R1؛ 110809 prohibited؛
- انتقال/رسید/برگشت با provenance و warehouse dimensions؛
- عدم موجودی منفی بدون مسیر settlement مجاز؛
- Trade-In تا قبل از پذیرش مالکیت و شناسایی موجودی وارد inventory control نشود؛
- reconciliation بین quantity subledger و GL control؛
- inventory count shortage/overage به مسیرهای تعلیقی مصوب؛
- هزینه موجودی/COGS فقط در trigger مجاز و با cost layer قابل ردیابی.

### 6.2 INV-NEG-TST-001..012

| سناریوی منفی | انتظار |
|---|---|
| تلاش برای Posting به 110807 در R1 | رد کامل |
| ایجاد/استفاده از 110809 | رد کامل |
| ثبت GL مستقل به 110802..110806 | رد؛ اینها دسته زیرمعین‌اند |
| فروش بیش از موجودی قابل تخصیص | رد یا مسیر settlement صریح و مجاز؛ بدون negative stock خاموش |
| IMEI فعال تکراری | توقف و نیازمند بررسی؛ duplicate active IMEI مجاز نیست |
| استفاده از IMEI به‌عنوان TransactionId | رد قرارداد |
| ثبت Trade-In قبل از پذیرش مالکیت | بدون ورود به inventory control |
| ثبت COGS پیش از trigger معتبر | رد |
| retry دریافت/خروج موجودی | بدون اثر تکراری |
| برگشت کالا بدون ارجاع به سند اصلی | رد/صف بررسی |
| مغایرت count بدون تأیید | در وضعیت تعلیقی؛ بدون income/expense خودکار |
| اختلاف GL control و subledger | invariant failure؛ close/posting مرتبط متوقف شود |

این 12 سناریو بازسازی/پیشنهاد طراحی‌اند و تا تأیید رجیستر اصلی نباید به عنوان متن تاریخی INV-NEG-TST-001..012 معرفی شوند.

## 7. COA-TST-001..034 — معیارهای تکمیل

برای هر حساب canonical، آزمون‌های COA باید این کنترل‌ها را پوشش دهند:
1. یکتایی AccountCode و AccountId؛
2. عدم Parent cycle؛
3. Parent معتبر و سازگاری Level؛
4. منع direct posting به حساب تجمیعی؛
5. تطبیق NormalBalance با ماهیت مصوب؛
6. رعایت Postable و DirectPostingAllowed؛
7. ممنوعیت posting به inactive/prohibited/not verified؛
8. الزام Subledger برای حساب‌های کنترل؛
9. effective dating و جلوگیری از استفاده خارج از بازه؛
10. تغییر حساس فقط با CR/مجوز/Audit؛
11. عدم برخورد 4107/5106؛
12. عدم ثبت به 110809؛
13. قواعد TaxMode برای 110701/1109/2109؛
14. حفظ روابط صریح والد: 110302→1103، 610801→6108، 620201→6202؛
15. تفکیک 2101، 2103 و 2114؛
16. تفکیک 2107 از Refund Payable و 110703؛
17. کنترل 110801 در برابر 110802..110806؛
18. 6108 تجمیعی و 610801 فقط کسری نقدی قطعی؛
19. 1201/1202/6105 در دامنه دارایی ثابت R2؛
20. حفظ source lineage و OwnerDecisionRef.

این 20 معیار، پوشش پیشنهادی برای COA-TST است؛ mapping دقیق معیارها به شماره 001..034 تا بازیابی متن اصلی باز است. هیچ‌یک از شناسه‌ها با ترتیب فرضی پر نشده‌اند.

## 8. Crosswalk رویداد به آزمون

| FE | آزمون‌های قابل ارجاع | COA/سیاست | پوشش |
|---|---|---|---|
| FE-01/02 | FIN-TST-001..034 (ID-only); FIN-TST-043 | 4101/4102؛ recognition و مقابل باز | PARTIAL |
| FE-03/04/05 | FIN-TST-044; FIN-TST-052 | 2105، 2107، 110703؛ عدم درآمد تکراری | PARTIAL |
| FE-06/07 | FIN-TST-045; ATOMIC family | 2101/2103/110701/110801 | PARTIAL |
| FE-08/09 | FIN-TST-048; INV-CAN family; INV-NEG family | 5101/110801؛ FE-09 blocked | PARTIAL/BLOCKED |
| FE-10/10b/11/12 | FIN-TST-010 (ID-only); FIN-TST-052 | refund payable unresolved | BLOCKED/PARTIAL |
| FE-13 | FIN-TST-001..034 (ID-only) | 4201..4203 mapping open | BLOCKED |
| FE-14 | FIN-TST-046; ATOMIC family | نقش‌های 110401..110404 طبق OWN-7C-004-R1؛ source-title/attributes crosswalk باز | PARTIAL |
| FE-15 | FIN-TST-047 (expense nature overlap only) | 6104 not generic landed cost | BLOCKED |
| FE-16/17/19 | FIN-TST-048/049/053 | 110808/2113/2112/610801/6201 | PARTIAL/BLOCKED |
| FE-18/22 | FIN-TST-044/052 | 110302, 6106/6107 | PARTIAL |
| FE-20 | FIN-TST-050 | 2114; 5103 blocked | BLOCKED |
| FE-21 | FIN-TST-052; INV-CAN family | TradeInId, 110803 after ownership | PARTIAL |
| FE-23/37 | FIN-TST-049 | 5104/2108; payroll R2 | PARTIAL |
| FE-24/25/26/31/32 | R2 test suite NOT DEFINED | R2 scope | DEFERRED |
| FE-27 | FIN-TST-045; COA tax criteria | TaxMode/Tax Matrix | PARTIAL |
| FE-28/29/34/38 | FIN-TST-051/052 | 3199; equity mapping open | PARTIAL/BLOCKED |
| FE-30/36 | FIN-TST-052; negative controls | permission/audit; fail closed | PARTIAL |

## 9. شواهد لازم برای اجرای واقعی

هر run قابل پذیرش باید حداقل این موارد را نگه دارد:
- commit SHA و source hash؛
- نسخه دقیق 7B/7C/7D و شناسه CR/ADR مرتبط؛
- DB engine/version و schema migration version؛
- fixture IDs و seed hash؛
- request/event IDs و PostingIdentity؛
- journal/subledger/inventory snapshots قبل و بعد؛
- audit event IDs؛
- assertion output و test log؛
- failure/rollback evidence در آزمون‌های منفی/اتمیک؛
- محیط اجرا و زمان؛
- نام اجراکننده و بازبین مستقل؛
- نتیجه واقعی PASS/FAIL/BLOCKED و دلیل.

داده حساس باید redacted شود؛ شواهد باید قابل بازتولید و متصل به commit باشند.

## 10. موارد باز برای بستن 7D

| ID | مورد باز | معیار بستن | وضعیت |
|---|---|---|---|
| 7D-CL-001 | متن اصلی FIN-TST-001..042 | منبع/نسخه/hash و Test Case کامل هر ID | OPEN |
| 7D-CL-002 | COA-TST-001..034 | متن اصلی یا بازسازی مستقل با traceability و reviewer | OPEN |
| 7D-CL-003 | ATOMIC-001..010 | بازیابی mapping شماره‌ای یا تصویب mapping جدید | OPEN |
| 7D-CL-004 | INV-CAN-001..038 | منبع کامل و mapping دقیق 38 شناسه | OPEN |
| 7D-CL-005 | INV-NEG-TST-001..012 | منبع یا تصویب 12 سناریوی بازسازی‌شده با شناسه جدید | OPEN |
| 7D-CL-006 | Expected Journal | تکمیل قواعد 7B و صفات canonical 7C | BLOCKED |
| 7D-CL-007 | FE↔COA↔Test full crosswalk | اتصال هر FE و حساب مجاز به Test IDs دقیق | OPEN |
| 7D-CL-008 | R2 test scope | تعریف آزمون‌های R2 برای FE-24/25/26/31/32/37 | DEFERRED |
| 7D-CL-009 | Tax negative coverage | آزمون همه کانال‌ها برای TaxMode=0 و invalid Tax Matrix | OPEN |
| 7D-CL-010 | CI test harness / DB isolation | disposable DB، deterministic fixtures، rollback و evidence capture | NOT VERIFIED |
| 7D-CL-011 | Source provenance & hashes | تثبیت source inventory و hashها | OPEN |
| 7D-CL-012 | Independent review | review مستدل روی hash دقیق و closure evidence | OPEN |

## 11. وضعیت

- Test inventory: IDs indexed; source texts partly unavailable.
- Reconstructed design cases: FIN-TST-043..053 detailed at workpaper level.
- Exact legacy ID-to-case mapping: NOT VERIFIED.
- FE coverage: PARTIAL crosswalk recorded.
- COA coverage: design criteria recorded; account-specific test mapping open.
- Test execution: NOT RUN / NOT VERIFIED.
- Evidence: NOT ACCEPTED.
- Gate 4: OPEN.
- Posting Release: NOT GRANTED.

این سند Test Design Workpaper بازسازی‌شده است، نه منبع تاریخی بازیابی‌شده، نه گزارش اجرای آزمون و نه مجوز ثبت مالی.


## اصلاحیه معیار تحویل و تکمیل 7D — 2026-11-18

با تأیید معیار جدید O-01، بازیابی متن آزمون‌های تاریخی شرط لازم پذیرش نسخه جدید 7D نیست. شناسه‌های تاریخی FIN-TST/COA-TST/ATOMIC/INV-CAN/INV-NEG باید به‌عنوان SOURCE-ID با provenance حفظ شوند؛ برای مواردی که متن اصلی در دسترس نیست، نسخه جدید باید Test ID مستقل و جدید با برچسب CONTROLLED RECONSTRUCTION بسازد. هیچ شناسه جدیدی نباید به‌عنوان متن تاریخی معرفی شود.

7D جدید فقط زمانی قابل پذیرش است که برای همه FE-01..FE-38، حساب‌های مجاز/ممنوع و قواعد حساس، آزمون‌های مثبت، منفی، مرزی، idempotency، reversal، atomicity، period lock، tax fail-closed و R1/R2 guard به‌صورت Test Case کامل (precondition, fixture, input, action, expected business state, expected journal یا BLOCKED, assertions, cleanup, evidence) تعریف و به 7B/7C نگاشت شوند. آزمون‌های مالی تا زمانی که Posting Rule و صفات canonical حساب‌ها کامل نیستند باید Expected Journal=NOT SPECIFIED و وضعیت BLOCKED داشته باشند.

وضعیت فعلی این نسخه همچنان PARTIAL DESIGN / NOT RUN / NOT VERIFIED است؛ این اصلاحیه مجوز PASS یا Evidence Acceptance نیست.


## 10.3 مسائل باز 7D

| ID | موضوع | معیار بسته‌شدن |
|---|---|---|
| INT-7D-OPEN-001 | COA-TST-001..034 | متن منبع یا بازسازی کنترل‌شده |
| INT-7D-OPEN-002 | FIN-TST-035..042 | متن آزمون و منشأ |
| INT-7D-OPEN-003 | ATOMIC-001..010 | Test Case کامل |
| INT-7D-OPEN-004 | INV-CAN-001..038 | Test Case کامل |
| INT-7D-OPEN-005 | INV-NEG-TST-001..012 | Test Case کامل |
| INT-7D-OPEN-006 | FE-01..FE-38 | Crosswalk کامل |
| INT-7D-OPEN-007 | COA-to-Test | ماتریس حساب‌به‌آزمون |
| INT-7D-OPEN-008 | FE-09 | آزمون خروج قطعه |
| INT-7D-OPEN-009 | FE-20 | آزمون برون‌سپاری |
| INT-7D-OPEN-010 | TaxMode | پوشش تمام مسیرها |
| INT-7D-OPEN-011 | 4107 و 5106 | عدم برخورد و صحت ثبت |
| INT-7D-OPEN-012 | سقف اثر گرد کردن | مقایسه با مرجع rational دقیق |
| INT-7D-OPEN-013 | 100,000 IRR per line | تعیین معنای مستقل؛ بدون فرض یا استفاده |
| INT-7D-OPEN-014 | Idempotency | جلوگیری از ثبت تکراری |
| INT-7D-OPEN-015 | Reversal | برگشت قابل ردیابی |
| INT-7D-OPEN-016 | Atomicity | عدم ثبت ناقص |
| INT-7D-OPEN-017 | Period Lock | جلوگیری از ثبت در دوره بسته |
| INT-7D-OPEN-018 | Trade-In | دو سند و کنترل مالکیت |
| INT-7D-OPEN-019 | Evidence Registry | شواهد قابل بازبینی |
| INT-7D-OPEN-020 | Legacy Crosswalk برای P4C/P4E/P4G | نگاشت بر اساس متن واقعی؛ در نبود متن، LEGACY UNMAPPED |
| INT-7D-OPEN-021 | Part 4D Legacy Test IDs | بازیابی منبع؛ شماره‌سازی حدسی ممنوع |
| INT-7D-OPEN-022 | FIN-TST-043..053 provenance/detail | تعیین Source/RECONSTRUCTED/PROPOSED و Test Case کامل برای هر شناسه |
| INT-7D-OPEN-023 | FE-14 cheque lifecycle test detail | Test Case کامل منطبق با OWN-7C-004-R1، بدون تغییر تصمیم مالک |
| INT-7D-OPEN-024 | 110807/110808/2113 control tests | آزمون عدم Posting جدید 110807 در R1 و کنترل suspense/clearing برای 110808 و 2113 |
| INT-7D-OPEN-025 | R1/R2 scope guards | اثبات عدم اجرای ناخواسته مسیرهای R2 در R1 |

---


#

## معیار پذیرش نسخه 7D
- برای هر FE-01..FE-38، حساب‌های مجاز/ممنوع، TaxMode، R1/R2، lifecycle و control invariants آزمون‌های مثبت، منفی، مرزی، retry/idempotency، reversal، atomicity و period lock تعریف شوند.
- هر Test Case شامل TestId، Provenance، RequirementRefs، FEIds، AccountCodes، Purpose، Preconditions، Fixture، Input، Action، ExpectedBusinessState، ExpectedJournal یا NOT SPECIFIED/BLOCKED، ExpectedSubledger، ExpectedAudit، NegativeAssertions، Cleanup، IsolationBoundary و EvidenceArtifacts باشد.
- تمام تست‌ها به 7B و 7C نگاشت دوطرفه داشته باشند.
- شناسه‌های تاریخی بدون متن بازیابی‌شده صرفاً SOURCE-ID باقی بمانند؛ آزمون‌های جدید شناسه مستقل داشته باشند.
- اجرای تست و پذیرش شواهد فقط با خروجی واقعی محیط آزمون انجام شود.

## وضعیت نسخه
7D DRAFT — TEST DESIGN PARTIAL; EXECUTION NOT RUN; EVIDENCE NOT ACCEPTED.