# ADR-0007 — مرز دامنه و مدل مفهومی Party در Persons

- وضعیت: **Proposed — نیازمند بازبینی مجدد گروه مشاور؛ Accepted نیست.**
- مرتبط: ADR-0004، ADR-0005، ADR-0006 و ADR-0008 پیشنهادی.
- این ADR مجوز پیاده‌سازی یا فعال‌سازی G1 نیست.

## تصمیم پیشنهادی
1. شناسه عمومی بین‌ماژولی فقط PartyId از نوع UUIDv7 است؛ Party حقیقی و حقوقی variant انحصاری دارد.
2. نقش‌های تجاری Customer/Supplier/Colleague/Representative از WorkClassification و IAM جدا هستند.
3. Persons طبقه‌بندی کاری Staff/Employee/Technician را نگه می‌دارد و از قرارداد قابل پرس‌وجو می‌کند؛ مطابق ADR-0004. مهارت/صلاحیت/زمان‌بندی/تخصیص تکنسین در RepairOperations می‌ماند.
4. موبایل اولویت V1 است؛ ارقام فارسی/عربی normalize و موبایل معتبر داخلی ۰۹ به +989 تبدیل می‌شود. شماره مبهم/نامعتبر رد و ذخیره نمی‌شود.
5. canonical mobile درون Party یکتا؛ بین Partyها soft duplicate signal؛ حداکثر یک موبایل اصلی فعال برای هر Party.
6. پذیرش سریع از FindOrCreatePartyByMobile با idempotency استفاده می‌کند؛ ambiguity نیازمند انتخاب انسانی است. suffix4 حداکثر ۱۰ نتیجه ماسک‌شده؛ unmask فقط با persons.contact.read.full و audit.
7. وضعیت Party: Active/Inactive/Restricted/Archived/Merged؛ وضعیت هر role و WorkClassification مستقل Active/Inactive است.
8. V1 تک‌شعبه و بدون tenant؛ Access Scope از permission actor، هدف عملیات و وضعیت رکورد مشتق می‌شود.
9. concurrency tokenهای opaque در سطح profile/contact/role مستقل‌اند.
10. نوشتن چندماژولی PostgreSQL از orchestrator واحد در Application و participantهای نسخه‌دار استفاده می‌کند؛ هر ماژول DbContext مستقل خود را دارد و فقط DbConnection/DbTransaction مشترک ADR-0005 را enlist می‌کند. DbContext سراسری ممنوع است؛ staging/eventual consistency/compensation برای DB ممنوع است.
11. Outbox فقط برای اثر بیرونی خارج PostgreSQL و پس از commit است.
12. Merge اجرایی خارج V1 است؛ participant ناشناخته پیش از write عملیات را متوقف می‌کند و شکست DB همه writeها را rollback می‌کند؛ Recovery فقط اثر بیرونی.
13. شناسه رسمی، نشانی مرجع‌دار، روابط نمایندگی، legal hold و merge اجرایی خارج V1 هستند. ذخیره شناسه رسمی تا تصویب ADR مدیریت کلید و تأیید مالک ممنوع است. وضعیت صدور صورتحساب رسمی مالیاتی برای اشخاص حقوقی تا تصمیم حقوقی صریح مالک (با منبع و تاریخ اثر) در HOLD می‌ماند؛ این HOLD ادعای حقوقی نیست.
14. snapshot اسناد قطعی با تغییر Party بازنویسی نمی‌شود.

## فازبندی
V1: Party حقیقی/حقوقی، موبایل/تماس، FindOrCreate، نقش تجاری، WorkClassification، وضعیت، Access Scope، audit، concurrency و جست‌وجوی پذیرش.
فاز بعد: شناسه رسمی پس از ADR کلید؛ نشانی پیش از نیاز صورتحساب/ارسال؛ نمایندگی پیش از سناریوی حقوقی؛ legal hold پیش از حذف؛ merge پس از تکمیل participantها و ADR مستقل. ورود به هر فاز نیازمند تصمیم جداگانه مالک است.

## مسئولیت‌ها
گروه مشاور مسئول بازبینی معماری است. مالک پروژه هم‌زمان مسئول امنیت/حریم خصوصی و تصمیم‌های قانونی/نگهداری است؛ مسئول امنیت مستقل دیگری تعریف نشده است.

این ADR هیچ کلاس، جدول، migration، endpoint، worker یا UI ایجاد نمی‌کند. g1Authorized=false و Persons=NOT_IMPLEMENTED حفظ می‌شوند. پذیرش نهایی منوط به بازبینی مجدد گروه مشاور روی SHA نهایی، تأیید مالک و CI سبز است.


## تصمیم‌های تکمیلی این بازنگری — هنوز Proposed

15. Contracts هر ماژول نوع PartyId مستقل و opaque دارد و فقط UUID wire-format استاندارد را serialize می‌کند؛ هیچ reference به Domain/Infrastructure/DbContext ماژول Persons مجاز نیست. آزمون contract بین‌ماژولی باید JSON/OpenAPI serialization، UUIDv7 shape و round-trip را برای producer و هر consumer اثبات کند.
16. استقلال DbContext هر ماژول و enlist در connection/transaction مشترک، اصلاحیهٔ پیشنهادی ADR-0005 است و تا تصویب ADR-0010 Proposed محسوب می‌شود.
17. V1 برای idempotency فقط OperationId scoped به actor/client و operation name و نتیجه را نگه می‌دارد؛ payload، hash و HMAC ذخیره یا مقایسه نمی‌شوند. تکرار همان کلید نتیجهٔ قبلی را می‌دهد؛ intent متفاوت باید OperationId تازه داشته باشد.
18. migrationها دارای sequence عددی سراسری و نام NNNN_<module>_<description>.sql هستند؛ registry مرکزی Git با شمارندهٔ یکنواخت `nextVersion` تخصیص را ثبت می‌کند. بازه‌های ماژولی فقط راهنمای اولیه‌اند و پس از عبور high-water mark، هر ماژول می‌تواند شمارهٔ سراسری بعدی را بگیرد. جدول history سراسری module key را نیز ثبت می‌کند و dependencyها پیش از اجرا بررسی می‌شوند. جزئیات در ADR-0009.
19. جست‌وجوی suffix4 فقط دو رقم پایانی را در نتایج نشان می‌دهد. سقف پیشنهادی global روزانه ۱۰۰۰ درخواست علاوه بر سقف actor/purpose است و تا تأیید مالک امنیت و سنجش بار واقعی صرفاً پیشنهادی می‌ماند. قفل ۲۴ساعته حساب ممنوع است؛ throttle ۱۵دقیقه‌ای فقط برای actor دارای الگوی پیمایش اعمال می‌شود و unlock مدیر فقط همان actor را با reason/audit باز می‌کند. سقف global تا تأیید مالک telemetry-only است و نباید موجب رد سراسری کاربران شود. تأیید صریح مالک درباره اعداد، دامنه و enforcement پیش‌نیاز ورود به G1 است.
20. هیچ قاعده حقوقی درباره نیاز/عدم نیاز شناسه رسمی مفروض نیست؛ صدور صورتحساب رسمی اشخاص حقوقی تا تصمیم مستند مالک در HOLD می‌ماند.
21. UUIDv7 در بخش ۴۸بیتی آغازین خود زمان تقریبی تولید را با دقت میلی‌ثانیه حمل می‌کند؛ بنابراین مشاهدهٔ PartyId می‌تواند زمان تقریبی ایجاد شناسه/پرونده را افشا کند. این مقدار PII مستقیم نیست، اما metadata زمانی محسوب می‌شود: PartyId محرمانه یا credential نیست و نباید به‌تنهایی مجوز دسترسی تلقی شود؛ انتشار آن باید به حداقل نیاز قرارداد محدود باشد و log/telemetry نباید آن را بی‌دلیل افشا کند.
