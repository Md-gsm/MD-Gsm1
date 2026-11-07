# آماده‌سازی G1 — ماژول اشخاص MD-Gsm

**وضعیت فعلی:** کار فنی G0 تکمیل و روی HEAD مشخص CI با موفقیت اعتبارسنجی شده است؛ سه مانع حاکمیتی/بازبینی انسانی پیش از Merge و ورود G1 باز است. G1 مجاز نشده است.  
**مخزن مرجع یگانه:** `Md-gsm/MD-Gsm1`؛ مخزن موازی `md-gsm` وجود ندارد و مرجع فعال پروژه نیست.  
**وضعیت معماری:** پروژه‌های Host و BuildingBlocks به مسیرهای نهایی منتقل شده‌اند؛ چهار اسمبلی Contracts نیز مستقل و در Solution ثبت شده‌اند. این کار به معنی پیاده‌سازی business moduleها نیست.  
**مرحله 10:** صریحاً خارج از دامنه است.

## کارهای فنی انجام‌شده در بسته G0

- مرجع مخزن فقط `Md-gsm/MD-Gsm1` است؛ تاریخچه یا metadata مخزن موازی وارد پروژه نشده است.
- پروژه‌های Domain، Application، Infrastructure و Client در `src/BuildingBlocks/*` قرار دارند.
- پروژه‌های API و Desktop در `src/Host/Api` و `src/Host/Desktop` قرار دارند.
- Solution، ProjectReferenceها، تست‌های معماری، اسکریپت‌های build/preview/smoke و path filterهای Windows workflow با مسیرهای جدید هماهنگ شده‌اند.
- چهار Contracts project برای Persons، IdentityAccess، RepairOperations و Finance در `src/Modules/*/Contracts` ساخته، نسخه‌گذاری و به Solution و API composition متصل شده‌اند. این اسمبلی‌ها فقط version marker دارند و حاوی business DTO یا رفتار نیستند.
- تست معماری موجودی دقیق projectها، جهت dependencyها، جداسازی Contracts و حذف ریشه‌های تخت قدیمی را کنترل می‌کند.
- سیاست داده موقت و چک‌لیست قرارداد ادغام توسط مالک پروژه پذیرفته شده‌اند؛ مسئولیت امنیت/حریم خصوصی نیز با مالک پروژه است.
- مالکیت jobهای CI به «گروه مشاور نرم‌افزار» واگذار شده و اعضای هر مرحله را مالک پروژه تعیین می‌کند.
- آزمون منفی معماری در PR #6 عمداً وابستگی ممنوع ایجاد کرد؛ check معماری شکست خورد و PR در وضعیت blocked باقی ماند و بدون merge بسته شد.
- Migration Runner متعلق به برنامه با PostgreSQL 18.6 در Linux و Windows آزمایش شده است. گزارش ثبت‌شده: `docs/operations/evidence/postgresql18-migration-validation-2026-10-31.json`.
- Docker image لینوکس PostgreSQL با digest ثابت pin شده است.

## وضعیت موانع و شروط G0/G1

| شرط | وضعیت فعلی | شواهد / باقی‌مانده |
|---|---|---|
| ساختار فیزیکی Host/BuildingBlocks | پیاده‌سازی شد | ۶ پروژه فنی به مسیرهای نهایی منتقل شده‌اند؛ Solution، مراجع پروژه، تست‌ها و اسکریپت‌ها هماهنگ شده‌اند. CI روی HEAD `7bcc423a87b44e19291f4f28d04858e06e74515c` در run `41185491539` با ۱۳/۱۳ job موفق شد. |
| مرز وابستگی و Contracts | بسته و روی HEAD نهایی تأیید شد | Architecture gate و تست C# موجودی دقیق projectها، dependency graph، Contracts isolation و نبود مسیرهای flat قدیمی را در run `41185491539` با موفقیت کنترل کردند. |
| تصویب سیاست داده موقت | بسته در سطح سیاست | پذیرش مالک پروژه در `TEMPORARY_DATA_RETENTION_POLICY_FA.md` ثبت شده؛ Runtime و تست‌های عملیاتی AC-31..38 مربوط به G1 هستند. |
| تصویب چک‌لیست قرارداد ادغام | بسته در سطح معیار طراحی | پذیرش مالک پروژه در `MERGE_CONTRACT_CHECKLIST_FA.md` ثبت شده؛ اجرای واقعی ادغام و تست‌های AC-39..45 مربوط به G1 هستند. |
| مالکیت CI jobها | بسته | گروه مشاور نرم‌افزار مسئول jobهاست؛ اعضای هر مرحله را مالک پروژه اعلام می‌کند. مسئول امنیت/حریم خصوصی شخص مالک پروژه است. |
| آزمون منفی کنترل معماری | انجام‌شده | PR #6؛ `persons-architecture-boundary` شکست خورد و PR به دلیل check شکست‌خورده blocked ماند؛ run `41177718250`. |
| PostgreSQL 18 Linux/Windows و Application Migration Runner | اعتبارسنجی‌شده روی commit آزمایش‌شده | run `41185491539` روی HEAD نهایی PR با ۱۳/۱۳ job موفق، شامل هر دو job PostgreSQL Linux/Windows و migration runner. گزارش جامع در `g0-ci-validation-2026-10-31.json` و جزئیات PostgreSQL در `postgresql18-migration-validation-2026-10-31.json`. |
| PostgreSQL image digest | بسته | Linux service در workflow با digest ثابت pin شده است. |
| Required Status Checks واقعی در SCM | باز / نیازمند اقدام Admin | شاخه `main` protected است، اما `contexts=[]` و `checks=[]` و enforcement level برابر `non_admins` است؛ بنابراین هیچ Required Status Check فعالی ثبت نشده. Admin باید ۱۳ نام check در `required-status-checks.md` را ثبت و bypass را محدود کند. |
| بازبینی مستقل معماری | باز | گروه مشاور نرم‌افزار باید topology و ADRها را روی commit مشخص بازبینی و نتیجه را در مخزن ثبت کند. نام یا امضای اشخاصی جعل نشده است. |
| بررسی امنیت/حریم خصوصی | مالکیت مشخص؛ ثبت نهایی بررسی لازم است | مسئولیت این حوزه با مالک پروژه است، نه گروه مشاور نرم‌افزار. پذیرش سیاست داده موقت در سند ثبت شده؛ بررسی نهایی امنیتی برای بسته G1 باید جداگانه و با مرجع commit مشخص ثبت شود. |
| AC-31..AC-45 | برنامه آزمون آماده؛ تست اجرایی وجود ندارد | سناریوها در `AC31-45-VERIFICATION-PLAN.md` شناسه‌گذاری شده‌اند؛ status در acceptance map باید `UNMAPPED` بماند تا رفتار واقعی پیاده‌سازی و تست شود. |

## تفکیک بسته‌شدن G0 از مجوز G1

بسته‌شدن فنی G0 از نظر سورس، ساختار، Solution، scripts/tests، CI و شواهد فنی انجام شده است: run `41185491539` روی commit `7bcc423a87b44e19291f4f28d04858e06e74515c` با ۱۳/۱۳ job موفق شد و `architectureAligned=true` ثبت شده است. برای Merge امن PR و عبور رسمی از دروازه G1، هنوز پیکربندی ۱۳ Required Status Check در SCM، بازبینی مستقل معماری گروه مشاور نرم‌افزار، و بازبینی امنیت/حریم خصوصی توسط مالک پروژه لازم‌اند؛ این موارد با سبزشدن CI جایگزین نمی‌شوند.

موارد AC-31..AC-45 و Runtime اشخاص/ادغام، دامنه پیاده‌سازی و پذیرش G1 هستند؛ آن‌ها عمداً در این بسته به تست ساختگی تبدیل نمی‌شوند. بنابراین `acceptanceCriteriaAc31ToAc49Mapped=false` باقی می‌ماند و هیچ تغییر این سند مجوز شروع G1 نیست.

**`g1Authorized=false` باید باقی بماند** تا شرایط رسمی ورود G1 با شواهد مستقل و تنظیمات SCM تأیید شوند.

## ساختار فیزیکی فعلی

- `src/BuildingBlocks/MDGsm.Domain/`
- `src/BuildingBlocks/MDGsm.Application/`
- `src/BuildingBlocks/MDGsm.Infrastructure/`
- `src/BuildingBlocks/MDGsm.Client/`
- `src/Host/Api/`
- `src/Host/Desktop/`
- `src/Modules/Persons/{Domain,Application,Infrastructure,Contracts}/`
- `src/Modules/IdentityAccess/{Domain,Application,Infrastructure,Contracts}/`
- `src/Modules/RepairOperations/Contracts/`
- `src/Modules/Finance/Contracts/`
- `tests/MDGsm.Domain.Tests/`
- `docs/persons/`

هیچ Persons business entity، endpoint، DbSet، جدول اشخاص، migration اشخاص یا عملیات مالی به کد اضافه نشده است.

## کنترل‌های CI پیشنهادی برای Required Status Checks

`foundation-build-test`, `persons-spec-lint`, `persons-architecture-boundary`, `persons-consumer-registry`, `persons-api-contract`, `persons-temporary-data-retention`, `persons-merge-contract`, `persons-postgres-migrations`, `persons-postgres-windows-migrations`, `persons-security-tests`, `persons-e2e-disposable`, `persons-ci-enforcement-test`, `persons-ci-evidence`.

این ۱۳ job در Workflow تعریف و در run `41185491539` همگی موفق شده‌اند، اما Branch API روی `main` هنوز `contexts=[]` و `checks=[]` گزارش می‌کند؛ Required بودن آن‌ها فعال نیست. تا ثبت واقعی از سوی Admin، `requiredStatusChecksConfiguredInScm=false` باقی می‌ماند.
