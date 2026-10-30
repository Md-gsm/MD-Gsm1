# آماده‌سازی G1 — ماژول اشخاص MD-Gsm

**وضعیت فعلی:** آماده‌سازی سورس؛ G1 هنوز مجاز نشده است.
**پایه مجاز:** `MD-Gsm_Phase1_CleanFoundation_Stage9_CSharpShell_MenuNavigation`
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
| Build/Test واقعی روی Windows | انجام‌شده در CI Windows در 2026-10-30 | run `41081078694`: Release build با 0 Warning / 0 Error و 16 تست .NET موفق |

**G1 نباید شروع شود** تا شروط بالا با شواهد قابل ردیابی بسته شوند. اجرای موفق lint در این بسته به‌تنهایی هیچ کدام از تأییدهای خارجی/سازمانی را ادعا نمی‌کند.

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

این نام‌ها در Workflow تعریف می‌شوند، ولی Required بودن آن‌ها فقط با تنظیم مخزن remote قابل اثبات است. این محیط به remote repository متصل نیست و ادعای فعال بودن branch protection نمی‌شود.
