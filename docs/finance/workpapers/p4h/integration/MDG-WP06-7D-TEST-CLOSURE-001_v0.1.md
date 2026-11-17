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
| FE-14 | FIN-TST-046; ATOMIC family | نقش‌های 110401..110404 طبق 24B؛ source-title/attributes crosswalk باز | PARTIAL |
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
