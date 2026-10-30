# آماده‌سازی G1 — ماژول اشخاص MD-Gsm

**وضعیت فعلی:** آماده‌سازی سورس؛ G1 هنوز مجاز نشده است.
**پایه مجاز:** `MD-Gsm_Phase1_CleanFoundation_Stage9_CSharpShell_MenuNavigation` با بسته آماده‌سازی v0.2.11؛ این نسخه هنوز G0 است و مجوز G1 محسوب نمی‌شود.
**مرحله 10:** صریحاً خارج از دامنه است.

## آنچه در این بسته انجام شده است

- سورس Stage9 از بایگانی مجاز وارد یک مخزن Git جدید شده؛ تاریخچه یا metadata مخزن دیگری وارد نشده است.
- مسیرهای مالکیت آینده برای Domain، Application، Infrastructure، API و Tests ایجاد شده‌اند؛ فایل‌های README صرفاً مرزبندی هستند و هیچ رفتار/جدول اشخاصی پیاده‌سازی نمی‌کنند.
- مرجع مالکیت داده‌ها با هویت مرکزی، تماس، نشانی، رابطه هویتی، تشخیص تکراری، پروتکل ادغام و staging موقت تکمیل شده است.
- Manifest ماژول، Registry مصرف‌کنندگان و نگاشت AC-31 تا AC-49 اضافه شده‌اند.
- Workflowهای CI برای Build/Test ویندوزی، کنترل‌های ایستای معماری، آزمون‌های PostgreSQL آزمایشی و سیاست‌های ماژول تعریف شده‌اند.
- اسکریپت API smoke test سخت‌گیرانه‌تر شده است: فقط loopback، دریافت امن رمز، و بازگردانی متغیرهای محیطی.

## شروط قفل‌شده قبل از G1

| شرط | وضعیت فعلی | شواهد لازم برای بستن |
|---|---|---|
| تصویب سیاست داده موقت | باز | تأیید مالک محصول و امنیت؛ مقادیر در manifest با سند معماری سازگار باشند |
| تصویب چک‌لیست قرارداد ادغام | باز | مسئول معماری و مالکان مصرف‌کنندگان؛ نگاشت ۱۰۰٪ ارجاع‌ها |
| Required Status Checks در SCM | باز | تنظیم واقعی branch protection / ruleset روی remote repo |
| آزمون منفی قفل CI | باز | PR آزمایشی با یک check شکست‌خورده واقعاً مسدود شود؛ نتیجه مستند شود |
| مالک هر CI job | باز | اختصاص نام/مسئول واقعی و ثبت در سند عملیات |
| نگاشت AC-31..AC-49 | انجام در سورس | `acceptance-gate-map.json` و lint خودکار |
| Build/Test واقعی روی Windows | انجام‌شده در CI Windows در 2026-10-30 | run `41087151790`: Release build با 0 Warning / 0 Error و 18 تست .NET موفق؛ suite پایتون 31 تست موفق؛ هر 12 job سبز |
| نصب و راه‌اندازی دستی Preview | تأیید کاربر در 2026-10-30 | نصب و اجرای برنامه موفق گزارش شد؛ تنها ایراد گزارش‌شده پیام Unknown Publisher برای installer unsigned بود |
| امضای ناشر برای نسخه انتشار | pipeline آماده؛ گواهی باز | گواهی معتبر Code Signing در GitHub Secrets و verification امضای EXE برنامه و installer |

**G1 نباید شروع شود** تا شروط بالا با شواهد قابل ردیابی بسته شوند. اجرای موفق lint در این بسته به‌تنهایی هیچ کدام از تأییدهای خارجی/سازمانی را ادعا نمی‌کند.


## ممیزی واقعی پیش‌نیازها — 2026-10-30

| مورد | نتیجه ممیزی | توضیح |
|---|---|---|
| Temporary Data Retention Policy | تصویب‌نشده | سند پیشنهادی موجود است؛ امضای مالک محصول و مسئول امنیت/حریم خصوصی ثبت نشده است. |
| Merge Contract Checklist | تصویب‌نشده | معیار فنی موجود است؛ امضای مالک معماری و مالکان مصرف‌کنندگان ثبت نشده است. |
| Required Status Checks در SCM | مسدود / انجام‌نشده | فهرست شاخه‌های GitHub مقدار `main.protected=false` را نشان داد. مخزن خصوصی است؛ GitHub برای protected branches و rulesets روی مخزن خصوصی Pro/Team/Enterprise می‌خواهد. API rulesets صریحاً upgrade to GitHub Pro یا public شدن repo را اعلام کرد. مخزن عمومی نشده و نباید بدون تصمیم مالک عمومی شود. |
| آزمون منفی Merge-blocking | انجام‌نشده | تا وقتی Required Status Checks واقعاً فعال نباشد، این آزمون معنا ندارد؛ فقط unit test سیاست fail-closed موفق است و جایگزین آن نیست. |
| CI job owners و alternates | انجام‌نشده | هر 12 ردیف مالک اصلی و جایگزین خالی است. هیچ فردی به‌صورت حدسی تعیین نشده است. |
| نگاشت AC-31..AC-49 | انجام‌شده | فایل `acceptance-gate-map.json` و gate lint در CI بررسی شده‌اند. |
| CI ماژول اشخاص | موفق | run `41087151790`، هر 12 job موفق؛ .NET: 18/18؛ Python policy/UI: 31/31؛ PostgreSQL 18 disposable موفق. |
| ساخت Installer v0.2.11 | موفق، ولی unsigned | run `41087151900` موفق و artifact ساخته شد. امضای Authenticode جداگانه به گواهی نیاز دارد؛ این موضوع به‌تنهایی G1 gate نیست. |

### نتیجه

**G1 هنوز مجاز نیست.** Manifest باید `g1Authorized=false` بماند و `g1-entry-evidence.json` در وضعیت `NOT_READY` باقی بماند. دو تصویب انسانی، حفاظت واقعی شاخه، آزمون Merge-blocking و انتساب مسئولان CI هنوز شواهد معتبر ندارند.
## ترتیب ساخت پیشنهادی پس از بسته‌شدن شروط

1. G1: قراردادهای مفهومی، API و مالکیت؛ بدون migration عملیاتی.
2. بازبینی و تصویب بسته طراحی داده و قرارداد API.
3. G2: طرحواره نو و migrationهای نسخه‌دار روی PostgreSQL disposable.
4. سپس پیاده‌سازی مرحله‌ای Use Case، endpoint و آزمون‌ها طبق دروازه‌های G3 تا G7 سند معماری.

## ساختار افزوده‌شده (صرفاً مرزبندی)

- `src/MDGsm.Domain/Persons/`
- `src/MDGsm.Application/Persons/`
- `src/MDGsm.Infrastructure/Persons/`
- `src/MDGsm.Api/Endpoints/Persons/`
- `tests/MDGsm.Domain.Tests/Persons/`
- `docs/persons/`

هیچ entity، endpoint، DbSet، جدول اشخاص، migration اشخاص یا عملیات مالی به کد اضافه نشده است.

## کنترل‌های CI پیشنهادی برای Required Status Checks

`foundation-build-test`, `persons-spec-lint`, `persons-architecture-boundary`, `persons-consumer-registry`, `persons-api-contract`, `persons-temporary-data-retention`, `persons-merge-contract`, `persons-postgres-migrations`, `persons-security-tests`, `persons-e2e-disposable`, `persons-ci-enforcement-test`, `persons-ci-evidence`.

این نام‌ها در Workflow تعریف می‌شوند، ولی Required بودن آن‌ها فقط با تنظیم و مشاهده تنظیمات مخزن remote و اجرای آزمون منفی Merge-blocking قابل اثبات است. در وضعیت فعلی، فعال بودن branch protection و Required Status Checks تأیید نشده و مقدار manifest باید `false` بماند.
