# الحاقیه اصلاحی معماری مرحله دوم G1 — پاسخ به B1..B5 و M1..M7

**این فایل سابقهٔ اصلاحات و پاسخ به بازبینی قبلی است؛ مرجع نهایی و یکپارچه، سند اصلی `G1_STAGE2_ARCHITECTURE_DESIGN_FA.md` است که بندهای متعارض در آن ادغام شده‌اند.** این متن به‌تنهایی تأیید گروه مشاور یا مجوز پیاده‌سازی نیست.

## B1 — طبقه‌بندی کاری
Persons علاوه بر نقش‌های تجاری Customer/Supplier/Colleague/Representative، مفهوم مستقل WorkClassification با مقادیر Staff/Employee/Technician را نگه می‌دارد و از قرارداد قابل پرس‌وجو می‌کند؛ مطابق ADR-0004. مهارت، صلاحیت، زمان‌بندی و تخصیص تکنسین متعلق به RepairOperations است؛ استخدام و حقوق در حوزه کارکنان/Finance. طبقه‌بندی کاری، نقش تجاری و مجوز امنیتی مستقل‌اند.

## B2 و M1 — تراکنش/merge/outbox مطابق ADR-0005
یک orchestrator واحد در Application/composition infrastructure use case چندماژولی را هماهنگ می‌کند. هر مصرف‌کننده participant نسخه‌دار با قرارداد هم‌زمان Prepare/Validate/Apply/Verify ارائه می‌کند و همه participantها همان DbConnection باز و DbTransaction مشترک PostgreSQL را enlist می‌کنند. هر participant فقط از persistence contract خودش می‌نویسد. registry ناقص، consumer ناشناخته، participant غایب/ناسازگار یا تعارض مسدودکننده قبل از هر write عملیات را متوقف می‌کند. شکست هر participant باید rollback کامل تمام DB writes را ایجاد کند. staging commit، eventual consistency و compensation برای نوشتن دیتابیسی ممنوع‌اند. Recovery/Retry فقط برای اثر بیرونی خارج PostgreSQL است؛ outbox فقط برای همین اثرها و پس از commit است. Merge اجرایی خارج V1 می‌ماند.

## B3 و B4 — موبایل‌محوری و رد ورودی نامعتبر
ارقام فارسی/عربی به ASCII تبدیل می‌شوند؛ موبایل معتبر داخلی ۰۹ به +989… canonical می‌شود. شماره مبهم/ناقص/نامعتبر رد و ذخیره نمی‌شود؛ مسیر unverified وجود ندارد (ADR-0004). canonical mobile درون Party یکتا است؛ بین Partyها یکتایی نرم و فقط duplicate signal است. هر Party حداکثر یک موبایل اصلی فعال دارد که با partial unique index در PostgreSQL enforce می‌شود. FindOrCreatePartyByMobile(name,mobile,idempotencyKey) برای پذیرش سریع طراحی می‌شود؛ retry idempotent و ambiguity نیازمند انتخاب انسانی است. exact mobile و suffix4 برای پذیرش مجازند؛ suffix4 حداکثر ۱۰ نتیجه ماسک‌شده می‌دهد. نمایش کامل شماره فقط با persons.contact.read.full و audit مجاز است.

## B5 — Access Scope
V1 تک‌شعبه و بدون tenant/branch است. Access Scope مجموعه رکوردهایی است که actor بر اساس permission صریح، هدف عملیاتی و وضعیت رکورد مجاز به دسترسی است. نقش به‌تنهایی مجوز نیست. permissionهای پایه: persons.party.read/search/write، persons.search.mobile.exact، persons.search.mobile.suffix4، persons.contact.read.full/write، persons.party.archive/merge. query پیش از materialization و command در سمت سرور محدود می‌شود.

## M2 — پذیرش و unmask
جست‌وجوی دقیق موبایل و suffix4 با permissionهای مستقل. suffix4 حداکثر ۱۰ نتیجه ماسک‌شده. نمایش کامل شماره نیازمند persons.contact.read.full، هدف عملیاتی مجاز و audit است؛ مجوز جست‌وجو به‌تنهایی unmask نیست.

## M3 — وضعیت‌ها
Party دارای Active/Inactive/Restricted/Archived/Merged است. Inactive یعنی در عملیات جدید پیش‌فرض قابل انتخاب نیست ولی سوابق حفظ و فعال‌سازی مجدد مجاز است؛ Archived و Restricted معانی جدا دارند. هر نقش تجاری و WorkClassification وضعیت Active/Inactive مستقل دارد؛ غیرفعال‌کردن Customer نباید Supplier فعال را غیرفعال کند. IAM state مستقل است.

## M4 — concurrency
توکن‌های PartyProfileVersion، ContactSetVersion، RoleSetVersion و در فاز بعد AddressSetVersion مستقل و opaque هستند. تغییر تماس با تغییر هم‌زمان نام conflict کاذب ندارد؛ فرمان چندمجموعه‌ای همه tokenهای مربوط را بررسی می‌کند.

## M5 — بازه زمانی
بازه‌ها نیمه‌باز [ValidFrom,ValidTo) هستند. در موارد ممنوعیت هم‌پوشانی، PostgreSQL exclusion constraint روی range (با btree_gist در صورت نیاز) مرجع نهایی است؛ validation برنامه‌ای جایگزین قید DB نیست. migration/extension فقط در فاز پیاده‌سازی و پس از آزمون PostgreSQL 18.

## M6 — مدیریت کلید
شناسه رسمی خارج V1 است و ذخیره آن ممنوع می‌ماند تا ADR-0008 تصویب و مالک پروژه (مسئول امنیت/حریم خصوصی) envelope encryption، نگهداری کلید خارج DB و سورس، rotation، least privilege، backup/restore و آزمون بازیابی را تأیید کند. تا آن زمان هیچ فیلد/جدول/API شناسه رسمی مجاز نیست.

## M7 — consumer scan
registry باید PartyId/party_id را scan و PersonId/person_id را خارج از policy و fixture آزمون منفی reject کند. این پیکربندی به‌تنهایی PASS نیست؛ تست منفی CI باید واقعاً اجرا شود. consumer ساختگی ثبت نشود.

## فازبندی
V1: Party حقیقی/حقوقی، موبایل و تماس، FindOrCreate، نقش تجاری، WorkClassification، وضعیت، Access Scope، audit، concurrency و جست‌وجوی پذیرش.
فاز بعد: شناسه رسمی پس از ADR-0008 و تأیید مالک؛ نشانی ساخت‌یافته پیش از نیاز صورتحساب/ارسال؛ روابط نمایندگی پیش از سناریوی حقوقی؛ legal hold پیش از قابلیت حذف؛ merge پس از تکمیل همه participantها و ADR مستقل. هیچ قابلیت deferred خودکار فعال نمی‌شود؛ trigger و تصمیم مالک لازم است.
مالک پروژه هم‌زمان مسئول امنیت/حریم خصوصی و تصمیم قانونی/نگهداری است؛ گروه مشاور بازبینی مستقل معماری را انجام می‌دهد.
B1..B5/M1..M7 و D2-01..D2-10 به AC-50..AC-66 نگاشت شده‌اند؛ همه ACهای جدید UNMAPPED می‌مانند تا تست اجرایی واقعی وجود داشته باشد.


> جزئیات تکمیلی مربوط به participant versioning، OperationId/idempotency، محل orchestrator، مرز Contracts، DbContextهای مستقل با transaction مشترک، سقف‌های ضد-enumeration، گذارهای وضعیت و trigger شناسه رسمی/صورتحساب مالیاتی در سند اصلی یکپارچه شده‌اند؛ این فایل برای جلوگیری از دو baseline موازی، منبع تصمیم مستقل نیست.
