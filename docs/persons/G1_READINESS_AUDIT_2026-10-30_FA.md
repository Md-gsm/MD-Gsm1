# گزارش ممیزی پیش‌نیازهای G1 و وضعیت پروژه MD-Gsm

**تاریخ ممیزی:** 2026-10-30  
**مخزن:** `Md-gsm/MD-Gsm1` (Private)  
**نسخه آماده‌سازی:** `v0.2.11`  
**شاخه گزارش:** `work/g1-prerequisite-audit-2026-10-30`  
**وضعیت:** `G1_NOT_READY` — هیچ مجوزی برای پیاده‌سازی عملیاتی اشخاص صادر نشده است.

## 1. خلاصه مدیریتی

پیش‌نیازهای فنی و ماشینی در حد سورس و CI تکمیل یا راستی‌آزمایی شده‌اند؛ اما همه پیش‌نیازهای G1 بسته نشده‌اند. مالک پروژه سیاست داده موقت را هم در ظرفیت Product Owner و هم در ظرفیت Security/Privacy Owner تأیید کرده است؛ این تأیید دو‌نقشی توسط یک شخص ثبت شده و بازبینی مستقل شخص ثالث ادعا نمی‌شود. مالک معماری نیز چارچوب قرارداد ادغام را با الزام تأیید جداگانه برای مصرف‌کنندگان آینده تصویب کرده است. حفاظت شاخه `main`، اثبات واقعی مسدودشدن Merge و انتساب انسانی Primary/Alternate در CI همچنان باز هستند.

تأیید امنیت/حریم خصوصی توسط همان مالک پروژه و در ظرفیت جداگانه ثبت شده است؛ برای آن شخص دیگری وارد ساختار نشده و بازبینی مستقل شخص ثالث ادعا نمی‌شود. هیچ امضایی از طرف مالکان مصرف‌کننده‌ای که هنوز وجود/ثبت ندارند جعل نشده است. مخزن عمومی نشده، plan پولی خریداری نشده و هیچ تغییری به `main` داده نشده است.

**نتیجه:** Manifest همچنان باید `g1Authorized=false` و `repositoryStage=G0_PREPARATION_ONLY` باقی بماند.

## 2. کارهای تکمیل‌شده در سورس و مخزن

1. مخزن خصوصی مستقل `Md-gsm/MD-Gsm1` از پایه مجاز Phase 1 / Stage 9 ایجاد شده و از مخزن قدیمی `Md-gsm/MD-Gsm` مستقل نگه داشته شده است.
2. Baseline commit اصلی روی `main` برابر `2a1c925acb1b7bb99d8c6bf6c914c9ff4224af7d` است؛ `main` در این ممیزی تغییر نکرده است.
3. قوانین معماری، مالکیت داده‌ها، جهت وابستگی، قرارداد API، registry مصرف‌کنندگان و نگاشت AC-31 تا AC-49 در سورس وجود دارند.
4. workflow کنترل‌های معماری، قرارداد API، retention، merge contract، امنیت، PostgreSQL disposable، CI enforcement، build/test ویندوز و evidence را اجرا می‌کند.
5. آزمون منفی معماری واقعاً پیاده شده و اثبات می‌کند افزودن ProjectReference ممنوع یا dependency مستقیم database به Desktop باعث شکست gate می‌شود.
6. مرحله بازسازی/رفع CI evidence انجام شد؛ enforcement نهایی از context واقعی jobها استفاده می‌کند.
7. نسخه Desktop و نصب‌کننده به `v0.2.11` هم‌نسخه شدند. برچسب‌های UI قدیمی نیز با این نسخه هماهنگ شدند.
8. pipeline امضای Authenticode برای EXE برنامه و installer اضافه شده است؛ استفاده از Code Signing certificate، SHA-256، RFC 3161 timestamp، SignTool verification و دریافت PFX فقط از GitHub Actions secrets.
9. گزارش آزمون دستی کاربر از نسخه `v0.2.10` ثبت شد: نصب و اجرای برنامه موفق بوده و تنها پیام گزارش‌شده `Unknown Publisher` بوده است.
10. backup branch برای baseline و برای نسخه آماده‌سازی ساخته شد. شاخه پشتیبان آماده‌سازی با head نسخه `v0.2.11` یکسان است.
11. تأییدهای مالک محصول و مسئول امنیت/حریم خصوصی توسط @Md-gsm در دو ظرفیت، و تأیید مشروط مالک معماری در اسناد و Manifest ثبت شدند. همچنین گروه نرم‌افزاری مشاور مجموعه به‌عنوان مسئولیت تیمی CI تعیین شد؛ نام افراد Primary/Alternate همچنان منتظر اعلام مالک پروژه است.

## 3. شواهد تست نهایی پیشین

### Persons Module Gates

- Workflow run: `41087151790` (run number 70)
- نتیجه: **SUCCESS — هر 12 job موفق**
- .NET Release build: `Build succeeded`, صفر Warning و صفر Error.
- .NET test suite: 18 passed، 0 failed، 0 skipped.
- Python UI/policy suite: 31 تست موفق.
- PostgreSQL 18 migration gate: موفق روی دیتابیس disposable.
- evidence artifact: `12991907715`.
- لینک: https://github.com/Md-gsm/MD-Gsm1/actions/runs/41087151790

### Windows Preview Installer

- Workflow run: `41087151900` (run number 33)
- نتیجه: **SUCCESS**
- PowerShell parse-check: موفق.
- Release build: صفر Warning و Error؛ .NET tests: 18/18.
- Inno Setup 6: نصب‌کننده نسخه `v0.2.11` با موفقیت تولید و upload شد.
- Artifact ID: `12991339172`.
- لینک: https://github.com/Md-gsm/MD-Gsm1/actions/runs/41087151900
- ZIP SHA-256: `03b05e50365918652a1f105ffd23be35dbd37870cffc448e1a47076aff990f60`.
- Installer EXE SHA-256: `56ea1577f027cd5526f6a005add22ad96e706d5ce422d8aa8c62ad78c917964f`.

**محدودیت امضا:** artifact نسخه v0.2.11 صریحاً `UNSIGNED` است، چون PFX معتبر در GitHub Secrets تنظیم نشده است. بنابراین پیام `Unknown Publisher` هنوز رفع نشده.

## 4. وضعیت دقیق پیش‌نیازهای G1 و تأییدهای انسانی

| پیش‌نیاز | وضعیت | شواهد / مانع |
|---|---|---|
| تصویب Temporary Data Retention Policy | تأیید شد در دو ظرفیت | @Md-gsm به‌عنوان Product Owner و Security/Privacy Owner تأیید کرده است. این self-approval دو‌نقشی است و بازبینی مستقل شخص ثالث محسوب نمی‌شود. دوره دقیق Backup retention پیش از پیاده‌سازی Backup باید تعیین شود. |
| تصویب Merge Contract Checklist | تأیید مشروط مالک معماری ثبت شد | @Md-gsm چارچوب را تصویب کرد؛ consumer registry در زمان ثبت خالی است. هر مصرف‌کننده آینده پیش از استفاده از PersonId به ثبت و تأیید خودش نیاز دارد. |
| Required Status Checks روی `main` | باز و مسدود | API فهرست شاخه‌ها `main.protected=false` را نشان داد. |
| آزمون واقعی negative merge enforcement | باز | بدون protection واقعی، اثبات block شدن Merge ممکن نیست. واحدتست fail-closed جایگزین آن نیست. |
| مسئولیت و مالکان CI برای 12 job | مسئولیت تیمی تعیین شد؛ افراد باز | گروه نرم‌افزاری مشاور مجموعه مسئولیت تیمی هر 12 job را پذیرفته است؛ Primary و Alternate انسانی به اعلام مالک پروژه نیاز دارند. |
| AC-31 تا AC-49 mapping | انجام‌شده | registry و acceptance-gate-map در CI بررسی شدند. |

### وضعیت Manifest

- `mergeChecklistApproved=true` فقط به معنی تصویب چارچوب معماری برای وضعیت فعلی بدون مصرف‌کننده ثبت‌شده است؛ مصرف‌کننده‌های آینده تأیید جداگانه می‌خواهند.
- `temporaryDataPolicyApproved=true` است؛ مالک پروژه در دو ظرفیت Product Owner و Security/Privacy Owner تأیید کرده است. این self-approval دو‌نقشی است، نه تأیید مستقل شخص ثالث.
- `g1Authorized=false` و `repositoryStage=G0_PREPARATION_ONLY` بدون تغییر باقی می‌مانند.

## 5. محدودیت حفاظت شاخه در GitHub

در 2026-10-30 وضعیت branch list remote بررسی شد و `main.protected=false` بود. تلاش برای خواندن classic branch protection از طریق GitHub App با `403 Resource not accessible by integration` برگشت. درخواست rulesets نیز `403` داد و پیام GitHub این بود که برای این قابلیت باید GitHub Pro تهیه شود یا مخزن public باشد.

مخزن در وضعیت Private باقی مانده است. آن را برای دور زدن محدودیت public نکرده‌ام. مستندات رسمی GitHub نیز می‌گوید protected branches و rulesets روی مخزن خصوصی به GitHub Pro/Team/Enterprise نیاز دارند و در Free برای مخازن عمومی قابل دسترس‌اند:

- https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches
- https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets

برای تکمیل این شرط، مالک مخزن باید تصمیم بگیرد مخزن خصوصی با Pro/Team حفظ شود یا گزینه دیگری را آگاهانه انتخاب کند. هیچ خرید plan، تغییر visibility یا تغییر تنظیمات حساب انجام نشده است. GitHub connector متصل نیز endpoint نوشتن branch protection ارائه نمی‌دهد؛ بنابراین تغییر باید توسط مالک در Settings انجام و سپس از remote بررسی شود.

## 6. کارهای انسانی باقی‌مانده

1. پیش از پیاده‌سازی عملیاتی Backup، مالک پروژه مقدار دقیق دوره نگهداری Backup و روش پایان‌عمر رسانه‌ها را در مشخصات اجرایی تعیین کند؛ سیاست کلی در دو ظرفیت مالک محصول و مسئول امنیت/حریم خصوصی تأیید شده است.
2. مصرف‌کنندگان آینده هنگام ورود به `consumer-registry.json`، قرارداد اختصاصی و تأیید مالک همان مصرف‌کننده را ارائه کنند؛ در زمان فعلی هیچ مصرف‌کننده‌ای ثبت نشده است.
3. مخزن private را با plan سازگار نگه دارند و در Settings → Branches protection برای `main` با 12 required checks تنظیم کنند.
4. یک PR آزمایشی با check عمداً ناموفق باز شود و GitHub واقعاً Merge را مسدود کند؛ سپس PR آزمایشی بسته شود و evidence ثبت شود.
5. برای هر 12 job، مالک اصلی و جایگزین انسانی از میان اعضای گروه نرم‌افزاری مشاور مجموعه توسط مالک پروژه معرفی شود و پذیرش مسئولیت آنان ثبت گردد.

Issue پیگیری این موارد: https://github.com/Md-gsm/MD-Gsm1/issues/4

## 7. وضعیت Git و نسخه‌ها

- PR شماره 1: Stage 0 architecture negative test؛ باز و ادغام‌نشده.
- PR شماره 3: آماده‌سازی نسخه `v0.2.11`؛ باز، به شاخه `stage0/architecture-negative-test` متصل و ادغام‌نشده.
- PR شماره 5: [G1 readiness audit](https://github.com/Md-gsm/MD-Gsm1/pull/5)؛ باز، بر پایه شاخه آماده‌سازی v0.2.11 و هنوز ادغام‌نشده است.
- `main` همچنان روی baseline commit `2a1c925...` است.
- شاخه پشتیبان `backup/persons-activation-prep-v0.2.11-2026-10-30` با head آماده‌سازی برابر است.

## 8. مرز فنی حفظ‌شده

هیچ Persons business entity، endpoint، DbSet، جدول عملیاتی اشخاص، migration اشخاص یا عملیات ثبت/ادغام پایدار اضافه نشده است. جست‌وجو و افزودن اشخاص همچنان preview و in-memory است. هیچ دیتابیس عملیاتی یا اطلاعات واقعی برای این مرحله استفاده نشده است.

## 9. حکم پایان ممیزی

**تأیید مالک محصول و مسئول امنیت/حریم خصوصی (در دو ظرفیت توسط یک شخص) و تأیید مشروط مالک معماری ثبت شده‌اند؛ اما G1 هنوز آماده نیست.** self-approval دو‌نقشی جایگزین بازبینی مستقل شخص ثالث نیست؛ چنین بازبینی‌ای در ساختار پروژه پیش‌بینی نشده است. حفاظت شاخه، آزمون واقعی Merge-blocking و ثبت نام Primary/Alternate برای گروه نرم‌افزاری مشاور مجموعه همچنان باید تکمیل شوند. هر مصرف‌کننده‌ای که بعداً به registry افزوده می‌شود نیز باید قرارداد و تأیید مالک خود را قبل از استفاده از PersonId ارائه کند.
