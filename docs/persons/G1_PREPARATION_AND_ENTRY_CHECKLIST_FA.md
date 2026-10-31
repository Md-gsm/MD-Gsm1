# آماده‌سازی G1 — ماژول اشخاص MD-Gsm

**وضعیت فعلی:** آماده‌سازی سورس؛ G1 هنوز مجاز نشده است.
**مخزن مرجع یگانه:** `Md-gsm/MD-Gsm1`؛ مخزن موازی `md-gsm` دیگر وجود ندارد و مرجع فعال پروژه نیست.
**وضعیت معماری:** طرح هدف ماژولار در `docs/architecture/modular-target.md` ثبت شده؛ پیاده‌سازی کامل ساختار ماژول‌ها هنوز انجام نشده است.
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
| نگاشت AC-31..AC-49 | ناقص | فقط AC-46..AC-49 تست اجرایی دارند؛ AC-31..AC-45 باید UNMAPPED بمانند تا تست واقعی نوشته شود |
| Build/Test واقعی روی Windows | باز | خروجی `dotnet restore/build/test` از ماشین Windows یا CI Windows |

**G1 نباید شروع شود** تا شروط بالا با شواهد قابل ردیابی بسته شوند. ساختار هدف ماژولار، ADR-0004/0005/0006 و PostgreSQL 18.6 نیز باید در بازبینی معماری/امنیت بررسی شوند. CI فعلی آزمون PostgreSQL روی Windows یا migration runner داخل برنامه را اثبات نمی‌کند. اجرای موفق lint در این بسته به‌تنهایی هیچ کدام از تأییدهای خارجی/سازمانی را ادعا نمی‌کند.

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

این نام‌ها در Workflow تعریف می‌شوند، ولی Required بودن آن‌ها فقط با تنظیم مخزن remote قابل اثبات است. اتصال GitHub موجود است، اما API متصل جزئیات Branch Protection را با 403 رد کرده است؛ بنابراین تنظیمات دقیق و Required بودن checkها هنوز تأیید نشده‌اند.
