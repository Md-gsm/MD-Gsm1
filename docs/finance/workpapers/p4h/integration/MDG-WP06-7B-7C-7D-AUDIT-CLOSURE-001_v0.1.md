# MDG-WP06-7B-7C-7D-AUDIT-CLOSURE-001
## ممیزی متقابل و برنامه آماده‌سازی بستن موارد باز

| مشخصه | مقدار |
|---|---|
| Document ID | MDG-WP06-7B-7C-7D-AUDIT-CLOSURE-001 |
| Version | 0.1 |
| Project | MD-Gsm1 |
| Repository | Md-gsm/MD-Gsm1 |
| Pull Request | #66 |
| Audited baseline | MDG-WP06-7B-7C-7D-INTEGRATED-001 v0.2 و منابع قابل احراز تا 2026-11-17 |
| New integrated baseline | MDG-WP06-7B-7C-7D-INTEGRATED-001 v0.3 |
| Classification | CONTROLLED AUDIT — CLOSURE PREPARATION |
| Design result | PARTIALLY RECONCILED — BLOCKING GAPS REMAIN |
| Implementation | NOT VERIFIED |
| Tests | NOT RUN / NOT VERIFIED |
| Evidence | NOT ACCEPTED |
| Gate 3 / Gate 4 | OPEN / OPEN |
| Posting Release | NOT GRANTED |

---

# 1. حکم ممیزی

ممیزی انجام شد؛ اما بسته 7B/7C/7D هنوز برای اعلام «تکمیل نهایی طراحی» یا بستن O-01 آماده نیست. نسخه v0.2 از نظر تفکیک تصمیم مالک، قواعد پایه، وضعیت‌های مستقل و منع Posting حدسی مسیر درستی داشت، ولی چهار مانع اصلی داشت:

1. شناسه‌های OPEN نسخه یکپارچه با شناسه‌های تاریخی 7D v0.3 برخورد معنایی داشتند.
2. 7C هنوز ماتریس canonical کامل 76 کد را ندارد؛ فایل موجود یک workbook تطبیق و snapshot پیشنهادی گروه است، نه COA مادر تأییدشده.
3. 7B در سطح FE-01..FE-38 خلاصه رویداد و اشاره به حساب‌هاست؛ برای بیشتر FEها قواعد Debit/Credit، recognition، amount basis، subledger و reversal کامل نیست.
4. 7D رجیستر آزمون و شناسه‌ها را گردآوری می‌کند، اما برای چند گروه، Test Case کامل و crosswalk حساب‌به‌آزمون موجود نیست؛ همچنین دو عبارت تاریخی با تصمیمات متأخر تعارض دارند.

نسخه یکپارچه v0.3 این ممیزی را به سند منتقل می‌کند، namespace شناسه‌های جدید را اصلاح می‌کند و شکاف‌های بازیابی Legacy را اضافه می‌کند. این تغییرات موارد را «قابل پیگیری و آماده تکمیل» می‌کند؛ هیچ موردی صرفاً با درج در رجیستر بسته تلقی نمی‌شود.

# 2. دامنه و شواهد بررسی‌شده

| منبع | وضعیت و محدودیت |
|---|---|
| MDG-ARCH-MASTER-001 v2.5 | مرجع معماری؛ O-01 در صفحه 108 همچنان باز است و تغییر آن نیازمند CR است. |
| OWN-7C-043 | پاسخ نهایی مالک؛ تصمیمات در دامنه ثبت‌شده نهایی‌اند و نباید دوباره پرسیده شوند. |
| ADR-0017 | در main ادغام شده؛ integer-only IRR، AwayFromZero، largest remainder و کنترل خطای سندی ±1 IRR را تثبیت می‌کند. |
| MDG-WP06-COA-RECON-001 v0.1.xlsx | Workbook تطبیق؛ 6 شیت، 76 ردیف snapshot در شیت «منبع اصلی گروه»، 22 یافته. این فایل canonical COA یا فایل مادر اصلی گروه نیست. SHA-256 فایل بررسی‌شده: `af3d02cab3c2f5a430508356680b42146320efb9122b1e5fd32d62ebb8c5c0fd`. |
| WP06-P4H-7D v0.3.docx | فایل مستقل موجود؛ وضعیت Source Recovery در خود سند PARTIAL است. SHA-256 فایل بررسی‌شده: `4bea759a5d0a3b7d944a7c43fe85d6da1883554d84830a765875a0a1acfd78fb`. |
| WP06-P4H-7B v0.5 Controlled Reconstruction | محتوای بازسازی‌شده و خلاصه ماتریس FE-01..FE-38 در سابقه کاری موجود است؛ اصل مستقل تاریخی 7B v0.4 احراز نشده است. |
| WP06-P4H-7C v0.3 / account-level reconciliation | نسخه‌های کاری بازسازی‌شده و خلاصه نتایج در سابقه موجود است؛ اصل مستقل تاریخی 7C v0.3 احراز نشده است. |
| MDG-WP06-7B-7C-7D-INTEGRATED-001 v0.2 | فایل موجود در PR #66؛ 970 خط محتوای طراحی/کنترل. |
| CI مربوط به PR #66 head قبلی | پنج workflow موفق بودند، اما هیچ‌کدام تست صحت معنایی 7B/7C/7D یا تست مالی production نیستند. |

## 2.1 محدودیت مهم Provenance

Workbook تطبیق شامل snapshot 76 کد از فایل گروه است و در شیت منابع، hash فایل TXT و XLSX گروه را ثبت می‌کند؛ اما فایل باینری اصلی گروه/ZIP در این ممیزی به‌عنوان منبع مستقل قابل بازبینی حاضر نبود. بنابراین:
- مقادیر snapshot را می‌توان دقیقاً از workbook فعلی استخراج کرد؛
- نمی‌توان آن‌ها را بدون hash-check روی فایل مادر، `SOURCE VERIFIED` نهایی اعلام کرد؛
- پیشنهادهای گروهی را نمی‌توان تصمیم مالک یا COA canonical تلقی کرد.

# 3. نتیجه ممیزی 7C — 76 کد منبعی + 2114 و 5106

## 3.1 آمار واقعی snapshot 76 ردیفی

| فیلد | نتیجه مشاهده‌شده | حکم |
|---|---:|---|
| AccountId | 76/76 NOT ASSIGNED | رجیستری شناسه canonical تکمیل نیست. |
| ParentAccountId | 76/76 NOT ASSIGNED | پیوند ساختاری داخلی تکمیل نیست. |
| ParentAccountCode | فقط 3 رابطه صریح؛ 67 «در منبع مشخص نشده»، 3 والد عددی دقیق نامشخص، 3 گروهی/غیرعددی | فقط 110302→1103، 610801→6108 و 620201→6202 را می‌توان صریح دانست. سایر والدها نباید از پیشوند کد حدس زده شوند. |
| NormalBalance | 28 بدهکارِ نیازمند تثبیت؛ 14 بستانکارِ نیازمند تثبیت؛ 13 تعیین‌نشده؛ 6 ارجاع به OWN-7C-041/042؛ 3 وابسته به وضعیت؛ سایر موارد خاص | برای 41 ردیف، جهت مانده هنوز به‌صورت نهایی احراز نشده؛ ارجاعات 041/042 مصوبه مالک محسوب نمی‌شوند. |
| Postable | 56 UNVERIFIED؛ 8 NO IN R1؛ 6 CONDITIONAL؛ 5 NO RELEASE؛ 1 UNRESOLVED | هیچ‌یک از این اعداد مجوز Posting صادر نمی‌کند؛ تصمیم‌های حساب و Gate باید جداگانه اعمال شوند. |
| DirectPostingAllowed | 72 UNVERIFIED؛ 4 NO | مجوز ثبت مستقیم برای اغلب حساب‌ها تعیین نشده است. |
| SubledgerRequired | 35 YES؛ 35 NOT SPECIFIED؛ 6 وابسته به Event Crosswalk | قرارداد زیرمعین برای 41 ردیف نیازمند تکمیل/تعیین است. |
| EffectiveFrom | 76/76 NOT SET | تاریخ مؤثر canonical موجود نیست. |

این اعداد از ستون‌های شیت «منبع اصلی گروه» در workbook تطبیق استخراج شده‌اند؛ آن شیت خود تصریح می‌کند که snapshot پیشنهادی گروه است و سند تصمیم/اجرا نیست.

## 3.2 کدهای افزوده‌شده خارج از 76 ردیف

- **2114:** نقش مالک مشخص است: خدمت دریافت‌شده و تأییدشده با تعهد قابل اثبات، ولی فاکتورنشده؛ از 2101 و 2103 مستقل. ویژگی‌های مادر شامل Parent، NormalBalance، Postable و DirectPostingAllowed از منبع مادر احراز نشده‌اند.
- **5106:** نقش آن به‌عنوان زیان گرد کردن از ADR-0017/تصمیم ثبت‌شده ناشی می‌شود؛ این کد در snapshot 76 ردیفی منبع مادر وجود ندارد. عدم برخورد کد، NormalBalance، posting eligibility و آزمون هنوز احراز نشده است.
- **4107:** حساب سود گرد کردن است؛ استفاده فقط بعد از بررسی عدم برخورد، Normal Side، Posting Matrix و تست مجاز می‌شود. ADR-0017 به‌تنهایی مجوز Journal نیست.

## 3.3 تصمیمات مالک که از نظر مفهومی تعیین تکلیف شده‌اند

موارد زیر نباید دوباره برای تصویب مطرح شوند؛ کار باقی‌مانده، تطبیق دقیق ردیف‌ها، پیوند منبع و آزمون است:

- 110701 = مالیات خرید قابل بازیافت، نه Supplier Advances؛ TaxMode/Tax Matrix شرط استفاده.
- 110801 تنها GL inventory control؛ 110802–110806 دسته‌های Inventory Subledger.
- 110807 در R1 غیرفعال؛ حفظ تاریخچه و منع Posting جدید.
- 110602 غیرفعال تا احراز ماهیت/منبع.
- 2102 و 2106 غیرفعال؛ 2107 دریافت ورودی تخصیص‌نیافته.
- 2114 خدمت دریافت‌شده و تأییدشده فاکتورنشده؛ مستقل از 2101 و 2103.
- 2104 تسویه فنی موقت موجودی منفی؛ 2113 کنترل مازاد شمارش.
- 1201 بهای تمام‌شده دارایی ثابت، 1202 استهلاک انباشته و 6105 هزینه استهلاک؛ فعال‌سازی کامل دارایی ثابت تابع R2/Gate است.
- 3101–3105 تا تطبیق منبع مادر و حقوق مالکانه، mapping/NormalBalance باز می‌مانند؛ OWN-7C-041/042 بدون تأیید صریح، Owner Approved نیستند.
- 620201 تا احراز منبع/نقش غیرفعال؛ 6204/6205 در R1 غیرفعال و تاریخچه حفظ شود.
- 110703 پرداخت خروجی تخصیص‌نیافته و 2107 دریافت ورودی تخصیص‌نیافته‌اند؛ معادل نیستند.
- Trade-In شامل اسناد خرید و فروش مستقل با TradeInId مشترک و بدون تهاتر است.

## 3.4 موارد 7C که هنوز قابل بستن نیستند

1. بازیابی و hash verification فایل مادر 9 شیتی گروه؛
2. تطبیق تمام 76 ردیف با source snapshot و تصمیمات مالک، بدون جایگزینی عنوان منبع؛
3. تکمیل درخت حساب و ParentAccountId بدون حدس؛
4. تعیین NormalBalance حساب‌های باقیمانده، بدون استفاده از OWN-7C-041/042 به‌عنوان مصوبه؛
5. تعیین Postable و DirectPostingAllowed به تفکیک حساب ساختاری، کنترل سیستمی، ثبت رویدادی و ثبت دستی؛
6. تکمیل SubledgerRequired و قرارداد ابعاد؛
7. تعیین وضعیت دقیق 4201–4203، 6106/6107، 620x و حساب‌های مالیاتی از منبع معتبر؛
8. بررسی عدم برخورد 4107/5106؛
9. تعریف صفات 2114 و 5106 با منشأ مستقل، بدون جعل Source Attributes.

# 4. نتیجه ممیزی 7B — FE-01 تا FE-38

ماتریس فعلی یکپارچه، پوشش موضوعی هر 38 رویداد را دارد؛ اما برای اکثر FEها هنوز Posting Matrix اجرایی/قابل پذیرش نیست. در بسیاری از ردیف‌ها فقط نام حساب یا وضعیت OPEN/CONDITIONAL/BLOCKED آمده است.

## 4.1 موانع اصلی

| حوزه | وضعیت ممیزی | اقدام لازم برای بستن طراحی |
|---|---|---|
| FE-01 / FE-02 | 4101/4102 به‌عنوان ارتباط اولیه؛ حساب مقابل و شرایط کامل باز | سند مبدأ، Recognition Time، Debit/Credit و تحویل/تحقق خدمت از منبع مصوب استخراج شود؛ هیچ حساب مقابل حدس زده نشود. |
| FE-03 / FE-04 / FE-05 | دریافت، پیش‌دریافت و تخصیص از هم تفکیک شده‌اند؛ سناریوها ناقص | ماتریس دریافت، ماهیت، تخصیص، مانده و جلوگیری از درآمد تکراری تکمیل شود. |
| FE-12 | Refund Payable تعیین نشده؛ 2107 ممنوع به‌عنوان Refund Payable | حساب و چرخه Refund فقط با منبع/تصمیم معتبر؛ تا آن زمان BLOCKED. |
| FE-13 | Contra Revenue پذیرفته؛ 4201–4203 mapping باز | نگاشت هر کد و ReasonCode، NormalBalance و آزمون مشخص شود. |
| FE-14 | تصمیم 24B چرخه چک را تعیین کرده؛ جزئیات Test Case/منبع هنوز ناقص | 110401 نزد صندوق، 110402 واگذارشده، 110403 در جریان وصول، 110404 برگشتی؛ دریافت چک طلب را می‌بندد و برگشت واقعی آن طلب را بازسازی می‌کند. Test Case کامل و شواهد لازم است. |
| FE-15 | 6104 حساب عمومی Landed Cost نیست | ماهیت هر هزینه و قاعده تخصیص/سرمایه‌ای‌سازی از سیاست مصوب استخراج شود. |
| FE-19 | خسارت/ضایعات و تفکیک خسارت تکنسین از کسورات نیازمند ماتریس | رویداد، علت، مسئولیت، سند و حساب بر اساس ماهیت واقعی تعیین شود. |
| FE-20 | 2114 فقط بدهی خدمت دریافت/تأییدشده؛ 5103 blocked | Recognition، اثبات خدمت، حساب هزینه، برگشت و تست تکمیل شود؛ 2102 و 2103 جایگزین نیستند. |
| FE-21 | قواعد مالکیت Trade-In روشن؛ نگاشت/تست تفصیلی باز | دو سند مستقل، TradeInId مشترک، عدم تهاتر، پذیرش مالکیت و IMEI duplicate guard آزموده شود. |
| FE-22 | 6106/6107 و تفکیک PSP/bank fee باز | سه‌طرفه reconciliation و جلوگیری از دوباره‌شماری 110302 آزموده شود. |
| FE-27 | TaxMode=0 ممنوعیت قطعی دارد؛ Tax Matrix کامل نیست | تمام مسیرهای UI/API/Manual/Import/Integration و effective tax rules آزموده شود. |
| FE-29 / FE-34 | قواعد بستن دوره/سال ناقص؛ equity mapping باز | چک‌لیست، blockerها، قفل/بازگشایی، تأیید مستقل و idempotent year close کامل شود. |
| FE-30 | Manual Journal نیازمند Permission/Audit | ماتریس مجوز حساب/نقش، تفکیک وظایف و audit assertions تکمیل شود. |
| FE-35 / FE-38 | ماهیت هزینه و equity mapping باز | 610x بر اساس ماهیت واقعی؛ 110702 فقط پیش‌پرداخت واقعی؛ 3101–3105 تا منبع/تصمیم باز. |

## 4.2 مواردی که در سطح تصمیم روشن‌اند اما تست/اثبات می‌خواهند

- FE-09: 110807 در R1 غیرفعال است؛ مسیر مالی خروج قطعه تا تکمیل قرارداد و تست BLOCKED می‌ماند.
- FE-14: چرخه چک مطابق تصمیم 24B؛ تست کامل هنوز لازم است.
- FE-20: 2114 فقط با خدمت دریافت و تأییدشده و تعهد اثبات‌پذیر؛ 5103 تا تکمیل قواعد مسدود.
- FE-21: دو سند مستقل خرید/فروش، TradeInId مشترک، بدون تهاتر.
- FE-27: TaxMode=0 در تمام کانال‌ها هیچ Tax Line/Posting تولید نمی‌کند.
- FE-37: حقوق و دستمزد R2؛ کمیسیون عملیاتی از FE-23 جدا.
- FE-24: حساب‌های 1201/1202/6105 از نظر نقش مشخص‌اند؛ توسعه کامل ماژول دارایی ثابت R2 است.

## 4.3 معیار تکمیل هر FE

برای هر FE باید یک ردیف canonical با این فیلدها ارائه شود:
`SourceEvent, Trigger, Preconditions, RecognitionTime, SourceDocument, DebitRule, CreditRule, ConditionalRules, AmountBasis, CostBasis, TaxMode, Subledger, Dimensions, InventoryEffect, PartyEffect, PostingIdentity, Idempotency, Reversal, PeriodRule, Atomicity, Retry, TestIDs, SourceRefs, OpenIssue`.

هر حساب یا مبلغ نامعلوم باید OPEN/BLOCKED بماند؛ هیچ حسابی از روی عنوان مشابه یا prefix حدس زده نشود.

# 5. نتیجه ممیزی 7D — رجیستر آزمون و شواهد

## 5.1 شکاف‌های منبع

| گروه | وضعیت |
|---|---|
| FIN-TST-001..034 | شناسه‌ها موجود؛ جزئیات باید متن‌به‌متن با منبع canonical تطبیق شود. |
| FIN-TST-035..042 | ID ONLY / SOURCE RECOVERY REQUIRED؛ نباید به Test Case کامل تبدیل فرضی شوند. |
| FIN-TST-043..053 | عنوان/موضوع سطح بالا قابل مشاهده است؛ جزئیات کامل، منشأ و expected result برای هر مورد لازم است. |
| COA-TST-001..034 | ارجاع وجود دارد؛ متن و ویژگی حساب مورد آزمون باید احراز شود. |
| ATOMIC-001..010 | شناسه و اصول کلی وجود دارد؛ سناریوی مستقل هر آزمون کامل نیست. |
| INV-CAN-001..038 | متن اصلی Part 4H-3 در دسترس فعلی احراز نشده؛ نام/فرمول نباید حدس زده شود. |
| INV-NEG-TST-001..012 | دامنه کلی قابل مشاهده است؛ Test Case کامل و Expected Result لازم است. |
| P4C/P4E/P4G Legacy | شناسه‌ها/موضوعات قابل مشاهده‌اند؛ نگاشت فقط با بررسی رفتار و Expected Result مجاز است. |
| Part 4D | منبع/شناسه‌های کامل بازیابی نشده؛ شماره‌سازی حدسی ممنوع. |

## 5.2 دو تعارض مهم در 7D v0.3

### تعارض A — 100,000 IRR per line

در بخش 13 سند 7D v0.3، عبارت «اختلاف گردکردن 100,000 ریال به‌ازای هر ردیف» آمده است. این عبارت با ADR-0017 و وضعیت نهایی ثبت‌شده ناسازگار است:
- سقف اثر گرد کردن در سطح سند، مقایسه خروجی integer-only با مرجع rational دقیق همان فرمول است و حداکثر ±1 IRR است.
- 100,000 IRR per line موضوعی مستقل با معنای نامشخص و OPEN است.
- این عدد نه تلورانس rounding است، نه مجوز اختلاف قیمت و نه ورودی تست rounding.
- FIN-TST-043 تا احراز متن/منشأ نباید به‌عنوان تست معتبر 100,000-per-line یا rounding معرفی شود.

### تعارض B — 110807

در بخش 13 سند 7D v0.3، فعال‌سازی خودکار 110807 بر اساس معیارهای شناخت دارایی و انتقال به هزینه در صورت از دست رفتن شرایط آمده است. تصمیم نهایی OWN-7C-043 برای R1 روشن است:
- 110807 در R1 غیرفعال؛
- تاریخچه حفظ می‌شود؛
- Posting جدید ممنوع است.
بنابراین هر تست R1 باید عدم ایجاد Posting جدید در 110807 را اثبات کند. هر سناریوی توسعه‌یافته دارایی/انتقال، در صورت ارتباط، خارج از فعال‌سازی R1 و تابع دامنه R2/Gate است.

## 5.3 کیفیت Crosswalk

Crosswalk سطح FE در سند یکپارچه برای هر FE ردیف دارد، اما:
- «FIN-TST core» شناسه آزمون دقیق نیست؛
- اشاره به FIN-TST-051/052 بدون متن کامل منبع و Expected Result، پوشش قابل پذیرش محسوب نمی‌شود؛
- برای هر حساب باید آزمون ماهیت، NormalBalance، Postable/No-posting، Subledger و مسیرهای مجاز/ممنوع مشخص شود؛
- حساب‌های غیرفعال، ممنوع، تعلیقی و R2 باید negative/scope-guard tests داشته باشند؛
- COA-to-Test matrix هنوز ساخته نشده است.

# 6. رجیستر تعارض‌های 22گانه COA — تعیین وضعیت کنونی

این جدول وضعیت یافته‌های ISS-001..022 در workbook تطبیق را با تصمیمات متأخر OWN-7C-043، ADR-0017 و خط مبنای v2.5 تطبیق می‌دهد. «Resolved at policy level» به معنی تکمیل ردیف canonical یا اجرای تست نیست.

| Finding | وضعیت فعلی | اقدام باقی‌مانده |
|---|---|---|
| ISS-001 OWN-7C-038 / FE-09 | PARTIAL | 6105/1202 مصوب‌اند؛ شناسه مستقل FE-09 و منبع اصلی آن هنوز احراز شود. |
| ISS-002 110701 | POLICY RESOLVED | ردیف 7C و Tax Matrix با همین نقش تطبیق شوند. |
| ISS-003 110801–110806 | POLICY RESOLVED | ساختار GL/Subledger در 7C canonical و تست شود. |
| ISS-004 2102 | POLICY RESOLVED | INACTIVE/NO POSTING در رجیستر و scope tests اعمال شود. |
| ISS-005 2106/2107 | POLICY RESOLVED | 2106 inactive؛ 2107 unallocated incoming؛ 110703 outgoing جدا. |
| ISS-006 چرخه چک | POLICY RESOLVED / TEST OPEN | نگاشت 24B و Test Case کامل. |
| ISS-007 2103/2114/2110 | PARTIAL | 2103 کالا، 2114 خدمت؛ 2110 نباید FE-20 mapping تلقی شود؛ صفات مادر/تست کامل شود. |
| ISS-008 2104 | POLICY RESOLVED / CONTROL OPEN | negative-inventory source, aging, owner, clearing trigger و test. |
| ISS-009 2112/2113 | PARTIAL | 2113 positive count; 2112 suspense; هر دو نیازمند علت، مالک، عمر و تسویه و تست. |
| ISS-010 OWN-7C-041/042 | OPEN | بدون سند/تأیید صریح، OWNER APPROVED ممنوع؛ 3101–3105 باز. |
| ISS-011 6105/1202 | POLICY RESOLVED / R2 OPEN | نقش حساب‌ها ثابت؛ فعال‌سازی دارایی ثابت تابع Gate/R2. |
| ISS-012 3101–3104 | OPEN | Equity source, NormalBalance و FE-28/34 mapping. |
| ISS-013 6204/6205 | POLICY RESOLVED | INACTIVE_R1/HISTORICAL_PRESERVED؛ انتقال/حذف خودکار ممنوع. |
| ISS-014 110808/620201 | PARTIAL | suspense path، عدم دوباره‌شماری، 620201 inactive تا احراز نقش. |
| ISS-015 TXT/XLSX status mismatch | OPEN — PROVENANCE | metadata و source classification در بسته منبع یکسان شود. |
| ISS-016 AccountId/ParentAccountId | OPEN | قرارداد شناسه و درخت canonical؛ فقط والدهای صریح ثبت شوند. |
| ISS-017 NormalBalance/Postable/DirectPosting | OPEN | تعیین ردیف‌به‌ردیف و جداسازی قابلیت طراحی از مجوز Posting. |
| ISS-018 درخت 1103/1104/1108 | OPEN | Parent/summary/leaf با منبع و ساختار معتبر، بدون prefix inference. |
| ISS-019 4201–4203 و rounding loss | OPEN | Contra Revenue mapping؛ 4107/5106 collision and eligibility checks. |
| ISS-020 GL در برابر Subledger | POLICY/ARCHITECTURE RESOLVED | کنترل دوطرفه در 7C و آزمون عدم ایجاد GL accounts جعلی. |
| ISS-021 ارجاع OWN-7C-039 | PARTIAL | 110702/110703/2102/2110 در ردیف‌های 7C با تصمیم واقعی تطبیق شوند. |
| ISS-022 ارجاع OWN-7C-040 به مالیات | POLICY RESOLVED / CROSSWALK OPEN | مالیات به TaxMode/Tax Matrix متصل؛ OWN-7C-040 فقط Trade-In. |

# 7. برنامه بستن — ترتیب اجرایی و معیار خروج

ترتیب زیر از وابستگی واقعی پیروی می‌کند؛ 7D نباید قبل از تثبیت 7B/7C نهایی شود.

## بسته A — Provenance و تثبیت Source Snapshot
**مسئول:** Finance Documentation + Architecture  
**خروجی:** Source manifest و 76-row source snapshot کنترل‌شده  
**معیار بستن:**
- فایل اصلی 9 شیتی گروه و ZIP در دسترس؛
- SHA-256 واقعی فایل‌ها با مقادیر manifest تطبیق؛
- 76 کد و 24 ستون بدون تغییر استخراج شوند؛
- source proposal از owner decision جدا بماند؛
- تمام 22 ISS وضعیت و cross-reference داشته باشند.

## بسته B — 7C Canonical Account Disposition
**مسئول:** Accounting Design + Architecture  
**خروجی:** 78-row account-level disposition matrix (76 source rows + 2114 + 5106)  
**معیار بستن:**
- تمام 24 فیلد source برای 76 کد حفظ شوند؛
- ستون‌های disposition جداگانه برای تصمیم مالک، Parent، Nature، NormalBalance، Postable، DirectPostingAllowed، Subledger، Status، FE/Test refs، restrictions و source lineage تکمیل شوند؛
- 2114 و 5106 با منشأ جدا و صفات مادر نامشخص علامت‌گذاری شوند؛
- فقط سه Parent صریح فعلی بدون مدرک جدید استفاده شوند؛
- 4107/5106 collision check مستند شود؛
- 041/042 به‌عنوان مصوبه مالک معرفی نشوند.

## بسته C — 7B Event Posting Rules
**مسئول:** Accounting Design  
**خروجی:** 38 ردیف FE با قرارداد کامل  
**معیار بستن:**
- تمام فیلدهای Section 4.1 سند یکپارچه برای FE-01..FE-38 تکمیل؛
- حساب بدهکار/بستانکار فقط از منبع یا تصمیم معتبر؛
- حساب‌های BLOCKED/CONDITIONAL و R2 صریح باشند؛
- 2114/2101/2103، 2107/110703، 110801/Subledger، 6105/1202، 4107/5106 و tax accounts تفکیک شوند؛
- Posting Identity, Retry, Reversal, Period, Atomicity, Amount/Cost Basis تکمیل شوند؛
- هر FE دقیقاً یک یا چند Test ID واقعی یا GAP شناسه‌دار داشته باشد.

## بسته D — 7D Test Registry & Crosswalk
**مسئول:** QA + Accounting Design  
**خروجی:** FE-to-Test و COA-to-Test matrices + Test Case definitions  
**معیار بستن:**
- شناسه‌های تاریخی بدون حذف یا بازشماری حفظ؛
- Source/ID-only/Reconstructed/Proposed از هم جدا؛
- هر Test Case هدف، ورودی، precondition، مراحل، expected result، negative case، assertions، data set و evidence requirements داشته باشد؛
- 100,000 IRR per line از rounding tests حذف و OPEN بماند؛
- 110807 در R1 negative posting test داشته باشد؛
- TaxMode=0 در تمام کانال‌ها تست شود؛
- FE-14، FE-20، FE-21، FE-27، FE-29، FE-34، FE-35، FE-38 پوشش دقیق داشته باشند؛
- COA active/control/suspense/inactive accounts تست داشته باشند.

## بسته E — مستقل‌سازی Gate و شواهد
**مسئول:** WG-QA + Architecture  
**خروجی:** Test execution evidence و independent review  
**معیار بستن:**
- اجرای واقعی روی build/schema مشخص؛
- Test IDs، commit SHA، DB version، dataset، run ID و خروجی actual/expected ثبت شوند؛
- evidence قابل بازتولید باشد؛
- تست‌های ناموفق defect و retest داشته باشند؛
- reviewer مستقل روی همان SHA بازبینی substantive انجام دهد؛
- فقط پس از پذیرش شواهد، Gate 4 قابل بررسی است.

# 8. آماده‌بودن موارد برای بستن

| وضعیت | موارد |
|---|---|
| **قابل بستن در سطح تصمیم/سیاست، نه تست** | نقش 110701؛ 110801–110806؛ غیرفعالی 110807/110602/2102/2106؛ نقش 2107/110703؛ تفکیک 2114/2101/2103؛ نقش 6105/1202؛ R1 غیرفعالی 6204/6205؛ Trade-In بدون تهاتر؛ TaxMode=0؛ سیاست integer-only ADR-0017. |
| **آماده تکمیل مستند با منبع موجود** | نگاشت 24B برای 110401–110404؛ نقش 2114؛ 2104/2113؛ 1201/1202/6105؛ FE-21 Trade-In؛ تفکیک FE-23 از FE-37؛ ممنوعیت 110809. |
| **مسدود به منبع/تطبیق فنی** | 76-row source provenance؛ Parent tree؛ AccountId/ParentAccountId؛ NormalBalance؛ Postable/DirectPostingAllowed؛ Subledger؛ 2114/5106 attributes؛ 4201–4203؛ 6106/6107؛ 620x؛ Tax Matrix؛ Equity mapping؛ 4107/5106 collision. |
| **مسدود به تعریف/طراحی Posting** | FE-01/02 counterpart؛ FE-12 refund payable؛ FE-13 discount mapping؛ FE-15 landed cost; FE-19 damage; FE-20 expense counterpart; FE-22 fees; FE-29/34 close; FE-30 manual journal; FE-35 expense nature; FE-38 equity. |
| **مسدود به طراحی تست/بازیابی Legacy** | FIN-TST-035..042؛ Part 4D؛ P4C/P4E/P4G crosswalk؛ COA-TST-001..034؛ ATOMIC-001..010؛ INV-CAN-001..038؛ INV-NEG-TST-001..012؛ FE-to-Test؛ COA-to-Test. |
| **باز مستقل و بدون امکان حدس** | معنای 100,000 IRR per line؛ اصل تاریخی مستقل 7B v0.4 و 7C v0.3؛ Part 4H-6؛ OWN-7C-041/042 تا زمان سند معتبر. |

# 9. وضعیت CI و بازبینی PR #66

در head قبلی `0821a443fce80fd1df2d782e69a4137a12f825a4` پنج workflow ثبت‌شده موفق بودند:
- Project Charter Owner Gate ×2
- CodeQL
- MD-Gsm Persons Module Gates
- MD-Gsm Windows Persons Preview Installer

این‌ها workflowهای عمومی مخزن/Foundation/Persons هستند؛ تست معنایی سند مالی، تست حسابداری production یا اثبات صحت Posting Matrix نیستند.

بازبینی `mrdeljoe4` در همان وضعیت یک `APPROVED` با body خالی بوده و هیچ inline review thread ثبت نشده است. بنابراین این approval به‌تنهایی بازبینی فنی substantive محسوب نمی‌شود. پس از commit نسخه v0.3 و گزارش ممیزی، بازبینی مستقل باید روی SHA جدید انجام شود.

# 10. وضعیت نهایی ممیزی

- Owner Decisions در دامنه OWN-7C-043 بازگشایی نمی‌شوند.
- برخی تعارض‌ها در سطح سیاست حل شده‌اند؛ ردیف‌های canonical، crosswalk و تست‌ها هنوز باید تکمیل شوند.
- 7B از نظر شرح FEها موجود، اما از نظر Posting Rule کامل نیست.
- 7C snapshot 76 ردیفی دارد، اما canonical account registry کامل نیست.
- 7D رجیستر دارد، اما test cases و crosswalk کامل نیستند.
- شناسه‌های جدید در v0.3 با پیشوند INT از شناسه‌های تاریخی جدا شده‌اند.
- هیچ تست مالی اجرا نشده است.
- CI عمومی موفق، Gate 3/4 را نمی‌بندد.
- O-01 در Architecture v2.5 فقط از طریق CR قابل بازتعریف است.
- Gate 1/2 در سطح تصمیم/طراحی بسته؛ Gate 3/4 باز.
- Implementation NOT VERIFIED.
- Evidence NOT ACCEPTED.
- Posting Release NOT GRANTED.

**Audit Verdict: READY FOR CONTROLLED CLOSURE WORK — NOT READY FOR FINAL ACCEPTANCE OR MERGE.**

---
End of Audit


## اصلاحیه اجرای برنامه بستن پس از تأیید مالک — 2026-11-18

این اصلاحیه بر هر بند متعارض قبلی مقدم است.

1. منبع Excel مادر گروه از ZIP بازیابی شده و hash فایل Excel استخراج‌شده `a6ca3377fde5863455a3b4bccf9a000d0dbf23f44bd66eeae51c44d0f3697866` احراز شده است. بنابراین «نبود فایل Excel» مانع کار نیست و نباید در رجیستر نقص‌ها تکرار شود.
2. سلسله‌مراتب COA برای ادامه کار: Architecture v2.5 و تصمیمات مالک/ADRهای پذیرفته‌شده؛ سپس COA v1.4 به‌عنوان نسخه جاری ساخته‌شده بر پایه v1.2؛ COA v1.2 به‌عنوان خط مبنای قبلی؛ v1.3 فقط مرحله میانی؛ Excel/TXT v1.0 به‌عنوان شواهد منبع.
3. مالک در 2026-11-18 معیار جایگزین O-01 را پذیرفته است. بازیابی اصل تاریخی 7B v0.4 و 7C v0.3 دیگر شرط الزامی نیست. تحویل‌دادنی‌های جدید باید Controlled Reconstruction / Controlled Replacement باشند و به‌عنوان اصل تاریخی معرفی نشوند.
4. این تصمیم هیچ مورد فنی را خودکار نمی‌بندد. Account canonicalization، کامل‌بودن Posting Ruleها، Test Design، crosswalk، اجرای آزمون، Evidence، تأیید متخصص و Independent Review همچنان باید با شواهد بسته شوند.
5. اصلاح اولویت کار: (الف) استخراج و تطبیق COA v1.4 حساب‌به‌حساب؛ (ب) تکمیل 7C canonical attributes؛ (ج) تکمیل 7B FE-01..38 با هر دو سمت ثبت یا BLOCKED مستدل؛ (د) تکمیل 7D آزمون‌های جدید با شناسه‌های مستقل و crosswalk دقیق؛ (هـ) lint/consistency checks؛ (و) CI و review مستقل روی SHA نهایی.

وضعیت: O-01 CR OWNER-APPROVED; O-01 DELIVERABLES NOT ACCEPTED; Gate 3/4 OPEN; Posting Release NOT GRANTED.

 
## وضعیت پیشرفت واقعی O-01 پس از انتشار 7B/7C/7D v0.2 — 2026-11-18

تحویل‌دادنی‌های کنترل‌شده مستقل در PR #66 ثبت شده‌اند:
- 7B: `WP06-P4H-7B-POSTING-MATRIX-001 v0.2`
- 7C: `WP06-P4H-7C-COA-RECONCILIATION-001 v0.2`
- 7D: `WP06-P4H-7D-TEST-REGISTRY-001 v0.2`
- Integrated: `MDG-WP06-7B-7C-7D-INTEGRATED-001 v0.4`
- CR: `CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 v0.1`
- Architecture amendment: `MDG-ARCH-MASTER-001 v2.6 Draft` در Library.

### پیشرفت قابل اثبات
1. معیار جایگزین O-01 با تأیید مالک ثبت شده است.
2. فایل Excel منبع و hash آن احراز شده؛ دریافت مجدد آن مانع نیست.
3. 7B دارای disposition برای 39 ردیف رویدادی است؛ posting ruleهای کامل همچنان ناقص‌اند.
4. 7C دارای source register و canonicalization matrix است؛ تطبیق کامل ردیف‌به‌ردیف با v1.4 هنوز باز است.
5. 7D دارای 39 test case مستقل و cross-reference کامل 7B↔7D است؛ positive journal assertions وابسته به تکمیل 7B/7C هستند.

### شرایط باقی‌مانده برای بستن O-01
- 7B: تکمیل posting ruleهای مجاز یا تثبیت BLOCKED/DEFERRED با همه فیلدهای الزامی و معیار رفع.
- 7C: تطبیق کامل تمام صفات canonical با COA v1.4 و ثبت lineage/eligibility/effective dating.
- 7D: تکمیل positive journal assertions پس از تثبیت 7B/7C و تطبیق دوطرفه FE↔COA↔Test.
- ممیزی مستقل روی SHA دقیق، اجرای تست‌های قابل اجرا، پذیرش Evidence و تأییدهای حرفه‌ای.
- ثبت نسخه‌های نهایی در Document Registry.

O-01 هنوز OPEN است. Gate 3/4 OPEN؛ Implementation NOT VERIFIED؛ Test execution NOT RUN؛ Evidence NOT ACCEPTED؛ Posting Release NOT GRANTED.


### 7C سطر‌به‌سطر — تکمیل Source Status Register
فایل `MDG-WP06-COA-76-ROW-SOURCE-REGISTER_v0.1.csv` با 76 ردیف و 30 ستون در Library ثبت شد؛ hash آن `7e61eca4bc486bc8173ea59a7c3b48a6fa9e5d231e5507cba208a3a003b80014` است. این اقدام وضعیت هر ردیف منبع را صریح می‌کند و مانع ادعای «نبود Excel» می‌شود. این اقدام به معنی canonical شدن صفات نیست؛ تطبیق 76 ردیف با COA v1.4 و ثبت مقادیر canonical همچنان مانع فنی 7C است.
