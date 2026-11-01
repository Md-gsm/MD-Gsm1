# بسته بازبینی معماری مرحله دوم G1 — درخواست بازبینی مجدد

مخزن Md-gsm/MD-Gsm1 — PR #11 — شاخه design/g1-stage2-persons-architecture  
**وضعیت:** درخواست بازبینی مستقل؛ این سند نتیجه بازبینی نیست. SHA و تاریخ باید توسط گروه مشاور هنگام بررسی نسخه نهایی درج شود.

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
| M7 | PartyId/party_id scan و PersonId/person_id reject؛ تست واقعی CI لازم |

این جدول پاسخ طراحی است؛ گروه مشاور باید مستقلانه نتیجه هر مورد را اعلام کند.

## چک‌لیست گروه مشاور
- [ ] PartyId تنها شناسه عمومی UUIDv7؛ حقیقی/حقوقی انحصاری.
- [ ] نقش تجاری، WorkClassification و IAM مستقل؛ Staff/Employee/Technician قابل پرس‌وجو.
- [ ] ارقام فارسی/عربی normalize؛ ۰۹ معتبر به +989؛ شماره نامعتبر رد.
- [ ] یکتایی موبایل در Party سخت و بین Partyها soft؛ یک موبایل اصلی فعال.
- [ ] FindOrCreate idempotent؛ ambiguity به انتخاب انسانی.
- [ ] suffix4 حداکثر ۱۰ نتیجه ماسک‌شده؛ unmask مجوز مستقل و audit.
- [ ] Active/Inactive/Restricted/Archived/Merged و وضعیت مستقل role.
- [ ] V1 تک‌شعبه بدون tenant؛ Scope permission + purpose + record state.
- [ ] تمام DB participants در shared connection/transaction؛ failure rollback کامل.
- [ ] participant ناشناخته پیش از write عملیات را متوقف می‌کند.
- [ ] outbox فقط اثر بیرونی؛ Recovery برای DB staging/compensation ندارد.
- [ ] tokenهای profile/contact/role مستقل؛ overlap ممنوع با exclusion constraint.
- [ ] ذخیره شناسه رسمی تا ADR-0008 ممنوع.
- [ ] AC-50..66 مشخص اما UNMAPPED؛ هیچ ادعای PASS بدون تست.
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
**SHA دقیق:**  
**تاریخ:**  
**Blocker/Major باقی‌مانده:**  
**موارد نیازمند تصمیم مالک:**

## تفکیک مسئولیت
گروه مشاور مسئول بازبینی معماری است. مالک پروژه هم‌زمان مسئول امنیت، حریم خصوصی و تصمیم‌های قانونی/نگهداری است؛ مسئول امنیت مستقل دیگری وجود ندارد. این سند درخواست بازبینی است و خانه‌های نتیجه را گروه مشاور باید پر کند. تا ثبت نتیجه واقعی، ADR-0007 Proposed، PR #11 Draft، g1Authorized=false و Persons=NOT_IMPLEMENTED باقی می‌مانند.
