# بسته بازبینی معماری مرحله دوم G1 — درخواست بازبینی مجدد

مخزن Md-gsm/MD-Gsm1 — PR #11 — شاخه design/g1-stage2-persons-architecture  
**وضعیت:** بسته آماده ارسال برای بازبینی مستقل؛ این سند نتیجه بازبینی نیست. SHA نهایی این بسته پس از پایان CI در سربرگ PR و همین سند ثبت می‌شود. SHA قدیمی یا CI روی commit قبلی، مبنای بازبینی نسخه جاری نیست. گروه مشاور باید دقیقاً SHA نهایی و run موفق همان SHA را بررسی کند.

## پاسخ طراحی به یافته‌های قبلی
| شناسه | پاسخ اصلاحی |
|---|---|
| B1 | WorkClassification شامل Staff/Employee/Technician در Persons؛ مهارت/تخصیص در RepairOperations |
| B2 | orchestrator واحد، participant هم‌زمان، shared transaction، rollback کامل DB؛ recovery فقط اثر بیرونی |
| B3 | normalize ارقام، ۰۹→+989، duplicate نرم بین Partyها، یک موبایل اصلی، FindOrCreate idempotent، suffix4 |
| B4 | رد و عدم ذخیره شماره نامعتبر |
| B5 | V1 تک‌شعبه بدون tenant؛ Scope بر اساس permission، هدف و وضعیت رکورد |
| M1 | outbox فقط اثر بیرونی خارج PostgreSQL |
| M2 | exact mobile و suffix4 با سقف ۱۰؛ unmask با permission مستقل و audit |
| M3 | وضعیت Party و role مستقل |
| M4 | token مستقل profile/contact/role |
| M5 | exclusion constraint دیتابیس برای overlap ممنوع |
| M6 | شناسه رسمی خارج V1؛ ذخیره ممنوع تا ADR-0008 و تأیید مالک |
| M7 | PartyId/party_id scan و PersonId/person_id reject؛ آزمون منفی CI واقعی اضافه شده و باید در هر تغییر بعدی سبز بماند |
| M8 | Contracts مستقل و PartyId opaque؛ بدون reference به Domain/Infrastructure/DbContext Persons |
| M9 | DbContext مستقل هر ماژول؛ ممنوعیت DbContext سراسری به‌عنوان اصلاحیه ADR-0005 |
| M10 | محدودیت suffix4 با سقف per-actor و global پیشنهادی، تعریف traversal، throttle موقت و unlock مدیریتی audit‌شده |
| M11 | V1 idempotency فقط OperationId scoped به actor/client و operation name و نتیجه؛ بدون payload/hash/HMAC و بدون نیاز به key manager |
| M12 | یکتایی موبایل در تماس‌های فعال هر Party و حفظ تاریخچه |
| M13 | purpose/permission نسخه‌دار و ماتریس lifecycle |
| M14 | trigger ورود به ADR-0008 هنگام نیاز به شناسه رسمی؛ تصمیم حقوقی با مالک |
| N1 | حذف ذخیره و مقایسهٔ payload؛ OperationId scoped و نتیجه، بدون payload/hash/HMAC |
| N2 | ثبت تصمیم Contracts و ممنوعیت DbContext سراسری در ADRها |
| N3 | ترتیب global migration و module ownership در ADR-0009 |
| N4 | تعریف traversal، throttle، unlock مدیر و تأیید حجم واقعی سقف‌ها |
| N5 | تصمیم صریح مالک درباره دامنه حقوقی صورتحساب رسمی |
| N6 | مسیرهای آینده تست‌های .NET به‌تفکیک unit/API/integration |
| N7 | نمایش دقیقاً دو رقم آخر موبایل در حالت ماسک |
| N8 | سقف تجمیعی global فقط telemetry/alert تا تأیید مالک؛ بدون رد سراسری |
| N9 | آزمون serialization/round-trip بین‌ماژولی PartyId |
| N10 | الزام ثبت نتیجه مستقل و بدون جهت‌دهی گروه مشاور |
| M8 | Contracts فقط مقدار opaque PartyId؛ ممنوعیت reference به Domain/Infrastructure/DbContext Persons |
| M9 | DbContext مستقل هر ماژول با DbConnection/DbTransaction مشترک در use case اتمیک؛ بدون DbContext سراسری |
| M10 | suffix4 با سقف ۱۰ نتیجه، ۵ درخواست/دقیقه و ۳۰ درخواست/روز برای هر actor/purpose، audit، alert و throttle موقت |
| M11 | FindOrCreate با OperationId کلاینت‌ساز در scope actor/client و operation؛ بدون ذخیره/مقایسه payload؛ retry همان نتیجه را برمی‌گرداند |
| M12 | یکتایی موبایل در تماس‌های فعال هر Party؛ تاریخچه حفظ و افزودن مجدد پس از غیرفعال‌سازی مجاز |
| M13 | purposeهای مجاز سمت سرور و permissionهای مستقل role/workclass؛ ماتریس گذار وضعیت Party |
| M14 | شروع ماژول فاکتور رسمی trigger اجباری ADR-0008 و تأیید/اجرای مدیریت کلید شناسه رسمی است |

این جدول صرفاً پاسخ/توضیح تهیه‌کنندگان طراحی به یافته‌های قبلی است و نتیجه بازبینی محسوب نمی‌شود. گروه مشاور باید بدون اتکا به نتیجه‌گیری این جدول، برای هر بند نظر مستقل خود را ثبت کند؛ امکان رد پاسخ پیشنهادی و افزودن یافته جدید محفوظ است.

## چک‌لیست گروه مشاور
- [ ] PartyId تنها شناسه عمومی UUIDv7؛ حقیقی/حقوقی انحصاری.
- [ ] نقش تجاری، WorkClassification و IAM مستقل؛ Staff/Employee/Technician قابل پرس‌وجو.
- [ ] ارقام فارسی/عربی normalize؛ ۰۹ معتبر به +989؛ شماره نامعتبر رد.
- [ ] یکتایی موبایل در Party سخت و بین Partyها soft؛ یک موبایل اصلی فعال.
- [ ] FindOrCreate idempotent؛ ambiguity به انتخاب انسانی.
- [ ] suffix4 حداکثر ۱۰ نتیجه؛ ماسک فقط دو رقم آخر؛ سقف ۵/دقیقه و ۳۰/روز actor-purpose به‌علاوه سقف global پیشنهادی ۱۰۰۰/روز؛ تعریف دقیق traversal، throttle موقت ۱۵ دقیقه‌ای و unlock مدیریتی با reason/audit. اعداد تا سنجش حجم واقعی و تأیید مالک امنیت صرفاً پیشنهادی‌اند.
- [ ] Active/Inactive/Restricted/Archived/Merged و وضعیت مستقل role.
- [ ] V1 تک‌شعبه بدون tenant؛ Scope permission + purpose + record state.
- [ ] orchestrator در Application؛ participant contract نسخه‌دار، OperationId و idempotency/payload hash؛ Prepare فقط read-only؛ تمام DB participants در shared connection/transaction؛ failure rollback کامل.
- [ ] participant ناشناخته/ناسازگار پیش از اولین write عملیات را متوقف می‌کند؛ DbContext هر ماژول مستقل اما enlisted در همان connection/transaction است؛ DbContext سراسری ممنوع و اصلاحیه ADR-0005 است.
- [ ] ترتیب migration: تخصیص در registry مرکزی Git، بازه‌های رزرو ماژولی، CI برای duplicate/out-of-range و رد اجرای late lower version؛ ADR-0009 Proposed.
- [ ] PartyId در تمام consumer contracts wire-format UUIDv7 یکسان و round-trip دارد؛ بدون coupling به CLR type ماژول Persons.
- [ ] outbox فقط اثر بیرونی؛ Recovery برای DB staging/compensation ندارد.
- [ ] tokenهای profile/contact/role مستقل؛ overlap ممنوع با exclusion constraint.
- [ ] هیچ تفسیر حقوقی مفروض نیست؛ صدور صورتحساب رسمی اشخاص حقوقی تا تصمیم O مستند مالک و تأیید ADR-0008 در HOLD می‌ماند.
- [ ] AC-50..73 با مسیرهای آیندهٔ تست .NET و نام کلاس/متد مشخص اما UNMAPPED؛ هیچ ادعای PASS بدون تست؛ validator و تست سیاست AC-31..73 را پوشش می‌دهند.
- [ ] هیچ مجوز Runtime از این بازبینی استنباط نمی‌شود.

## یافته‌های جدید این دور — پاسخ تهیه‌کنندگان و داوری مستقل

پاسخ‌های زیر صرفاً اصلاحات انجام‌شده در طراحی هستند. گروه مشاور باید در ستون «داوری مستقل» هر مورد را جداگانه تأیید، رد یا مشروط کند و می‌تواند متن/معیار آزمون دیگری تعیین کند.

| شناسه | اصلاح پیشنهادی/ثبت‌شده در نسخه جاری | داوری مستقل گروه مشاور (A/R/X/O) | شدت/مرجع/شرط |
|---|---|---|---|
| N1 | payload خام و hash بدون کلید حذف؛ حذف مقایسهٔ payload؛ ثبت OperationId scoped و نتیجه، بدون payload/hash/HMAC؛ AC-67؛ AC-67 |  |  |
| N2 | Contracts و استقلال DbContext در ADR-0007؛ اصلاحیهٔ پیشنهادی جداگانه در ADR-0010؛ ADR-0005 Accepted دست‌نخورده می‌ماند |  |  |
| N3 | ADR-0009: sequence عددی سراسری، نام NNNN_module، history سراسری با module_key و dependency DAG |  |  |
| N4 | traversal دقیق، throttle ۱۵ دقیقه‌ای، unlock مدیریتی audit‌شده؛ سقف‌ها پیشنهادی و نیازمند سنجش حجم/تأیید مالک امنیت؛ AC-69 |  |  |
| N5 | هیچ حکم حقوقی مفروض نیست؛ صورتحساب رسمی اشخاص حقوقی تا تصمیم مستند O مالک در HOLD |  |  |
| N6 | مسیرهای آتی .NET و کلاس/متدهای مشخص برای AC-50..73؛ همه UNMAPPED |  |  |
| N7 | ماسک پیش‌فرض فقط دو رقم پایانی برای طول‌های بین‌المللی متغیر؛ AC-70/73 |  |  |
| N8 | سقف global پیشنهادی ۱۰۰۰ درخواست صرفاً telemetry/alert؛ عدم رد سراسری تا تأیید مالک؛ AC-68 |  |  |
| N9 | UUIDv7 wire serialization و round-trip بین producer/consumers؛ AC-72 |  |  |
| N10 | نتیجه مستقل باید در جدول نتیجه گروه ثبت شود؛ جدول پاسخ تهیه‌کننده به‌تنهایی تأیید نیست |  |  |

## یافته‌های جدید مالک/بازبین — پاسخ تهیه‌کننده و داوری مستقل

| شناسه | موضوع | پاسخ اصلاحی تهیه‌کننده | داوری مستقل گروه مشاور |
|---|---|---|---|
| O1 | D2-08 و 10.7 متعارض | D2-08 با 10.7 همسان شد؛ سقف‌ها تا تأیید مالک telemetry-only و throttle فقط actor-specific است. |  |
| O2 | کلید HMAC در on-prem V1 | مقایسهٔ payload حذف شد؛ فقط OperationId scoped و نتیجه ذخیره می‌شود؛ ADR-0010 Proposed. |  |
| O3 | تخصیص migration با advisory lock | شماره در Git registry تخصیص می‌یابد؛ بازه‌های رزرو، CI duplicate/out-of-range و قاعدهٔ late lower version تعریف شد. |  |
| O4 | ویرایش ADR-0005 Accepted | متن Accepted دست‌نخورده؛ اصلاحیهٔ مستقل ADR-0010 Proposed. |  |
| O5 | دامنهٔ global cap و mask | global cap telemetry-only؛ throttle actor-only؛ ماسک دو رقم آخر برای طول متغیر. |  |
| O6 | approval policy gate | تأیید مالک دربارهٔ حدود و enforcement پیش‌نیاز صریح G1 شد؛ هنوز false است. |  |

## نتیجه واقعی گروه مشاور — فقط توسط گروه تکمیل شود
| شناسه | نتیجه A/R/X/O | شدت | مرجع | دلیل/اثر | اقدام و معیار آزمون |
|---|---|---|---|---|---|
| B1 |  |  |  |  |  |
| B2 |  |  |  |  |  |
| B3 |  |  |  |  |  |
| B4 |  |  |  |  |  |
| B5 |  |  |  |  |  |
| M1 |  |  |  |  |  |
| M2 |  |  |  |  |  |
| M3 |  |  |  |  |  |
| M4 |  |  |  |  |  |
| M5 |  |  |  |  |  |
| M6 |  |  |  |  |  |
| M7 |  |  |  |  |  |

**نتیجه کلی:** APPROVE / APPROVE WITH CONDITIONS / REQUEST CHANGES / REJECT  
**SHA دقیقِ بررسی گروه مشاور:** (گروه مشاور هنگام بررسی، SHA نهایی PR را اینجا ثبت کند.)  
**تاریخ:**  
**Blocker/Major باقی‌مانده:**  
**موارد نیازمند تصمیم مالک:**

## تفکیک مسئولیت
گروه مشاور مسئول بازبینی معماری است. مالک پروژه هم‌زمان مسئول امنیت، حریم خصوصی و تصمیم‌های قانونی/نگهداری است؛ مسئول امنیت مستقل دیگری وجود ندارد. این سند درخواست بازبینی است و خانه‌های نتیجه را گروه مشاور باید پر کند. تا ثبت نتیجه واقعی، ADR-0007 Proposed، PR #11 Draft، g1Authorized=false و Persons=NOT_IMPLEMENTED باقی می‌مانند.


## وضعیت CI و محدودیت نتیجه

SHA نهایی: پس از آخرین تغییر و CI موفق همان SHA تکمیل شود. نتیجه CI قدیمی‌تر جایگزین CI نسخه نهایی نیست. AC-50..AC-73 همگی UNMAPPED هستند؛ مسیرها و نام متدها فقط نقشه تست‌های آتی .NET هستند و هیچ ادعای اجرای تست تجاری/Runtime برای آن‌ها وجود ندارد.

## درخواست مشخص از گروه مشاور

لطفاً نسخه نهایی PR #11 را با توجه به جدول B1–B5 و M1–M14، بخش‌های 10.2–10.9 سند اصلی، ADR-0005، ADR-0007، ADR-0008، ADR-0009 و ADR-0010 بازبینی کنید. نتیجه باید مستقلانه برای هر بند ثبت شود؛ مواردی که صرفاً در سند تعریف شده‌اند ولی هنوز تست/Runtime ندارند باید به‌عنوان شرط/کار آینده باقی بمانند، نه PASS اجرایی. هیچ تأیید قبلی محدوده G1 جایگزین این بازبینی معماری مرحله دوم نیست.
