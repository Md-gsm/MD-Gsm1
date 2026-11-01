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
10. نوشتن چندماژولی PostgreSQL از orchestrator واحد و participantهای هم‌زمان در connection/transaction مشترک ADR-0005 استفاده می‌کند. staging/eventual consistency/compensation برای DB ممنوع است.
11. Outbox فقط برای اثر بیرونی خارج PostgreSQL و پس از commit است.
12. Merge اجرایی خارج V1 است؛ participant ناشناخته پیش از write عملیات را متوقف می‌کند و شکست DB همه writeها را rollback می‌کند؛ Recovery فقط اثر بیرونی.
13. شناسه رسمی، نشانی مرجع‌دار، روابط نمایندگی، legal hold و merge اجرایی خارج V1 هستند. ذخیره شناسه رسمی تا تصویب ADR مدیریت کلید و تأیید مالک ممنوع است.
14. snapshot اسناد قطعی با تغییر Party بازنویسی نمی‌شود.

## فازبندی
V1: Party حقیقی/حقوقی، موبایل/تماس، FindOrCreate، نقش تجاری، WorkClassification، وضعیت، Access Scope، audit، concurrency و جست‌وجوی پذیرش.
فاز بعد: شناسه رسمی پس از ADR کلید؛ نشانی پیش از نیاز صورتحساب/ارسال؛ نمایندگی پیش از سناریوی حقوقی؛ legal hold پیش از حذف؛ merge پس از تکمیل participantها و ADR مستقل. ورود به هر فاز نیازمند تصمیم جداگانه مالک است.

## مسئولیت‌ها
گروه مشاور مسئول بازبینی معماری است. مالک پروژه هم‌زمان مسئول امنیت/حریم خصوصی و تصمیم‌های قانونی/نگهداری است؛ مسئول امنیت مستقل دیگری تعریف نشده است.

این ADR هیچ کلاس، جدول، migration، endpoint، worker یا UI ایجاد نمی‌کند. g1Authorized=false و Persons=NOT_IMPLEMENTED حفظ می‌شوند. پذیرش نهایی منوط به بازبینی مجدد گروه مشاور روی SHA نهایی، تأیید مالک و CI سبز است.
