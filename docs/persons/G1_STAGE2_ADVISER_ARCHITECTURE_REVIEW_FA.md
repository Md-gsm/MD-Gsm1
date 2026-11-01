# بسته بازبینی معماری مرحله دوم G1 — درخواست بازبینی مجدد

مخزن Md-gsm/MD-Gsm1 — PR #11 — شاخه design/g1-stage2-persons-architecture  
**وضعیت:** بسته آماده ارسال برای بازبینی مستقل؛ این سند نتیجه بازبینی نیست. نسخه طراحی که CI آن با موفقیت تکمیل شد: `bab7c0d82d476b4527dfba58d2d84538980d64e4`؛ اجرای CI: [41283123467](https://github.com/Md-gsm/MD-Gsm1/actions/runs/41283123467)، نتیجه ۱۳/۱۳ job موفق. پس از commit این بسته، SHA نهایی PR از سربرگ PR ملاک دریافت/بازبینی است؛ نتیجه CI روی همان SHA نهایی باید موفق بماند.

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
| M8 | Contracts فقط مقدار opaque PartyId؛ ممنوعیت reference به Domain/Infrastructure/DbContext Persons |
| M9 | DbContext مستقل هر ماژول با DbConnection/DbTransaction مشترک در use case اتمیک؛ بدون DbContext سراسری |
| M10 | suffix4 با سقف ۱۰ نتیجه، ۵ درخواست/دقیقه و ۳۰ درخواست/روز برای هر actor/purpose، audit، alert و قفل موقت |
| M11 | FindOrCreate با idempotency key سروری، payload hash، TTL بیست‌وچهار ساعت و conflict برای payload متفاوت |
| M12 | یکتایی موبایل در تماس‌های فعال هر Party؛ تاریخچه حفظ و افزودن مجدد پس از غیرفعال‌سازی مجاز |
| M13 | purposeهای مجاز سمت سرور و permissionهای مستقل role/workclass؛ ماتریس گذار وضعیت Party |
| M14 | شروع ماژول فاکتور رسمی trigger اجباری ADR-0008 و تأیید/اجرای مدیریت کلید شناسه رسمی است |

این جدول پاسخ طراحی است؛ گروه مشاور باید مستقلانه نتیجه هر مورد را اعلام کند.

## چک‌لیست گروه مشاور
- [ ] PartyId تنها شناسه عمومی UUIDv7؛ حقیقی/حقوقی انحصاری.
- [ ] نقش تجاری، WorkClassification و IAM مستقل؛ Staff/Employee/Technician قابل پرس‌وجو.
- [ ] ارقام فارسی/عربی normalize؛ ۰۹ معتبر به +989؛ شماره نامعتبر رد.
- [ ] یکتایی موبایل در Party سخت و بین Partyها soft؛ یک موبایل اصلی فعال.
- [ ] FindOrCreate idempotent؛ ambiguity به انتخاب انسانی.
- [ ] suffix4 حداکثر ۱۰ نتیجه ماسک‌شده؛ ۵ درخواست/دقیقه و ۳۰ درخواست/روز برای actor/purpose، audit، alert و قفل موقت؛ unmask مجوز مستقل و purpose کنترل‌شده و audit.
- [ ] Active/Inactive/Restricted/Archived/Merged و وضعیت مستقل role.
- [ ] V1 تک‌شعبه بدون tenant؛ Scope permission + purpose + record state.
- [ ] orchestrator در Application؛ participant contract نسخه‌دار، OperationId و idempotency/payload hash؛ Prepare فقط read-only؛ تمام DB participants در shared connection/transaction؛ failure rollback کامل.
- [ ] participant ناشناخته/ناسازگار پیش از اولین write عملیات را متوقف می‌کند؛ DbContext هر ماژول مستقل اما enlisted در همان connection/transaction است.
- [ ] outbox فقط اثر بیرونی؛ Recovery برای DB staging/compensation ندارد.
- [ ] tokenهای profile/contact/role مستقل؛ overlap ممنوع با exclusion constraint.
- [ ] ذخیره شناسه رسمی تا ADR-0008 ممنوع.
- [ ] AC-50..66 مشخص اما UNMAPPED؛ هیچ ادعای PASS بدون تست؛ validator و تست سیاست AC-31..66 را پوشش می‌دهند.
- [ ] هیچ مجوز Runtime از این بازبینی استنباط نمی‌شود.

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

اجرای GitHub Actions شمارهٔ 41283123467 روی commit طراحی `bab7c0d82d476b4527dfba58d2d84538980d64e4` با ۱۳/۱۳ job موفق پایان یافته است؛ شامل `persons-spec-lint`، `persons-ci-enforcement-test`، `persons-ci-evidence` و آزمون‌های PostgreSQL 18 Linux و Windows. این نتیجه فقط صحت گیت‌ها و قراردادهای موجود را نشان می‌دهد؛ AC-50..AC-66 همچنان UNMAPPED هستند و هیچ ادعای اجرای تست‌های تجاری/Runtime برای آن‌ها وجود ندارد.

## درخواست مشخص از گروه مشاور

لطفاً نسخه نهایی PR #11 را با توجه به جدول B1–B5 و M1–M14، بخش‌های 10.2–10.8 سند اصلی، ADR-0007 و ADR-0008 بازبینی کنید. نتیجه باید مستقلانه برای هر بند ثبت شود؛ مواردی که صرفاً در سند تعریف شده‌اند ولی هنوز تست/Runtime ندارند باید به‌عنوان شرط/کار آینده باقی بمانند، نه PASS اجرایی. هیچ تأیید قبلی محدوده G1 جایگزین این بازبینی معماری مرحله دوم نیست.
