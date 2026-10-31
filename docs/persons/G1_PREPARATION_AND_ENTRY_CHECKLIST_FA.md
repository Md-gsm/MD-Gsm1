# آماده‌سازی G1 — ماژول اشخاص MD-Gsm

**وضعیت فعلی:** آماده‌سازی سورس؛ G1 هنوز مجاز نشده است.
**مخزن مرجع یگانه:** `Md-gsm/MD-Gsm1`؛ مخزن موازی `md-gsm` دیگر وجود ندارد و مرجع فعال پروژه نیست.
**وضعیت معماری:** چهار پروژه مستقل Contracts در `src/Modules/*/Contracts` ایجاد، به `MDGsm.sln` اضافه و از API composition project ارجاع داده شده‌اند؛ اما انتقال فیزیکی Host/BuildingBlocks به مسیرهای نهایی هنوز انجام نشده و business moduleها پیاده‌سازی نشده‌اند.
**مرحله 10:** صریحاً خارج از دامنه است.

## آنچه در این بسته انجام شده است

- سورس Stage9 از بایگانی مجاز وارد یک مخزن Git جدید شده؛ تاریخچه یا metadata مخزن دیگری وارد نشده است.
- مسیرهای مالکیت آینده برای Domain، Application، Infrastructure، API و Tests ایجاد شده‌اند؛ فایل‌های README صرفاً مرزبندی هستند و هیچ رفتار/جدول اشخاصی پیاده‌سازی نمی‌کنند.
- مرجع مالکیت داده‌ها با هویت مرکزی، تماس، نشانی، رابطه هویتی، تشخیص تکراری، پروتکل ادغام و staging موقت تکمیل شده است.
- Manifest ماژول، Registry مصرف‌کنندگان و نگاشت AC-31 تا AC-49 اضافه شده‌اند.
- Workflowهای CI برای Build/Test ویندوزی، کنترل‌های ایستای معماری، آزمون‌های PostgreSQL آزمایشی و سیاست‌های ماژول تعریف شده‌اند.
- اسکریپت API smoke test سخت‌گیرانه‌تر شده است: فقط loopback، دریافت امن رمز، و بازگردانی متغیرهای محیطی.

## شروط قفل‌شده قبل از G1

| شرط | وضعیت فعلی | شواهد / باقی‌مانده |
|---|---|---|
| تصویب سیاست داده موقت | بسته در سطح سیاست | پذیرش مالک پروژه در سند `TEMPORARY_DATA_RETENTION_POLICY_FA.md` ثبت شده؛ پیاده‌سازی Runtime و تست AC-31..38 باقی است |
| تصویب چک‌لیست قرارداد ادغام | بسته در سطح معیار طراحی | پذیرش مالک پروژه در سند `MERGE_CONTRACT_CHECKLIST_FA.md` ثبت شده؛ اجرای واقعی ادغام و تست AC-39..45 باقی است |
| Required Status Checks در SCM | باز / تأییدنشده | `main protected=true`، اما جزئیات protection با 403 و Rulesets API با فهرست خالی پاسخ داده؛ exact Required Checks و bypass باید از GitHub Settings اثبات شود |
| آزمون منفی قفل CI | آزمون منفی انجام شد | PR #6؛ `persons-architecture-boundary` شکست خورد و PR `mergeable_state=blocked` گزارش شد؛ run `41177718250` |
| مالکیت CI jobها | بسته | گروه مشاور نرم‌افزار مسئول همهٔ jobهاست؛ اعضای هر مرحله را مالک پروژه اعلام می‌کند؛ مسئول امنیت/حریم خصوصی خود مالک پروژه است |
| AC-31..AC-45 | سناریوها برنامه‌ریزی شده، تست اجرایی ندارند | شناسه‌های سناریو در `AC31-45-VERIFICATION-PLAN.md` ثبت شده‌اند؛ status رسمی باید UNMAPPED بماند تا فایل و متد تست واقعی ایجاد و در CI اجرا شود |
| PostgreSQL 18 Linux/Windows و Application Migration Runner | بسته و اعتبارسنجی‌شده روی commit آزمایش‌شده | run `41181075233` با ۱۳/۱۳ job موفق؛ گزارش `postgresql18-migration-validation-2026-10-31.json` |
| PostgreSQL image digest | بسته | Linux service با digest ثابت در workflow pin شده است |
| انتقال کامل Host/BuildingBlocks | باز | پروژه‌های فعلی هنوز زیر `src/MDGsm.*` هستند؛ باید به مرزهای فیزیکی نهایی منتقل و با build/test تأیید شوند |
| Build/Test ویندوزی | موفق روی commit آزمایش‌شده | اجرای کامل foundation build/test در run `41181075233`؛ commit فعلی PR نیز نیازمند CI سبز روی HEAD جاری است |

**G1 نباید شروع شود** تا شروط بالا با شواهد قابل ردیابی بسته شوند. PostgreSQL 18 Linux/Windows و migration runner روی commit آزمایش‌شده اعتبارسنجی شده‌اند و فیلدهای متناظر در Manifest `true` هستند. module Contracts اکنون اسمبلی‌های جداگانه دارند و API به آن‌ها ارجاع می‌دهد. با این حال انتقال کامل Host/BuildingBlocks، تست‌های اجرایی AC-31..45، بررسی مستقل معماری/امنیت و اثبات تنظیم دقیق Required Status Checks باقی مانده‌اند. سایر تأییدهای خارجی/سازمانی با CI جایگزین نمی‌شوند.

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

`foundation-build-test`, `persons-spec-lint`, `persons-architecture-boundary`, `persons-consumer-registry`, `persons-api-contract`, `persons-temporary-data-retention`, `persons-merge-contract`, `persons-postgres-migrations`, `persons-postgres-windows-migrations`, `persons-security-tests`, `persons-e2e-disposable`, `persons-ci-enforcement-test`, `persons-ci-evidence`.

این نام‌ها در Workflow تعریف می‌شوند، ولی Required بودن آن‌ها فقط با تنظیم مخزن remote قابل اثبات است. اتصال GitHub موجود است، اما API متصل جزئیات Branch Protection را با 403 رد کرده است؛ بنابراین تنظیمات دقیق و Required بودن checkها هنوز تأیید نشده‌اند.
