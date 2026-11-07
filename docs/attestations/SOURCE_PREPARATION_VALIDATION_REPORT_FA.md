# گزارش اعتبارسنجی بسته آماده‌سازی سورس ماژول اشخاص MD-Gsm

**تاریخ اجرا:** ۲۰۲۶-۱۰-۲۸
**شاخه محلی:** `prepare/persons-module-g0-preparation`
**مرحله:** G0 — آماده‌سازی؛ G1 مجاز نشده است.

## منشأ و سلامت پایه

- پایه واردشده: `MD-Gsm_Phase1_CleanFoundation_Stage9_CSharpShell_MenuNavigation.zip`
- SHA-256 بسته پایه: `da79e32e42f37fa6116d7ed2ca7bcec60a9931e1b7806caa62f9e0b543322ad4`
- Commit ورود پایه پیش از تغییرات: `34b12fa62c8c367421ad0ce5cc1063fd266d429e`
- فهرست هش پایه Stage9 شامل ۶۳ ورودی است؛ همه ۶۳ ورودی روی snapshot پایه Git بررسی شدند و همگی `OK` بودند.
- یک Git repository جدید برای همین بسته ساخته شد؛ metadata مخزن دیگری وارد نشده است.
- Stage10 وارد نشده است.
- در کد اجرایی بررسی‌شده، رشته صریح `RepairDesk` پیدا نشد.

## تغییرات آماده‌سازی انجام‌شده

1. مرزهای پوشه‌ای رزروشده برای Domain، Application، Infrastructure، API و Tests ماژول اشخاص ایجاد شدند؛ این پوشه‌ها صرفاً README دارند و پیاده‌سازی تجاری نیستند.
2. Registry مالکیت داده با هویت مرکزی شخص، تماس، نشانی، روابط هویتی، تکراری‌یابی، پروتکل ادغام و staging موقت تکمیل شد.
3. Manifest مرحله، Registry مصرف‌کنندگان و نگاشت ماشینی AC-31 تا AC-49 اضافه شد.
4. Workflow CI با ۱۲ job نام‌گذاری‌شده اضافه شد؛ شامل Build/Test ویندوزی، کنترل مرز معماری، Registry، قرارداد API، داده موقت، ادغام، مهاجرت PostgreSQL آزمایشی، امنیت، E2E، آزمون سیاست قفل و تولید شواهد CI بدون PII.
5. ۱۰ آزمون سیاست fail-closed اضافه شد؛ آزمون‌ها بررسی می‌کنند که پیاده‌سازی بدون تست، مصرف‌کننده ثبت‌نشده، route بدون قرارداد یا G1 بدون شواهد بیرونی رد شود.
6. اسکریپت‌های تست Stage7 اکنون فقط loopback را قبول می‌کنند، دیتابیس تازه را با کامنت `MDGSM_DISPOSABLE_TEST_DB` علامت می‌زنند و اسکریپت API-only رمز را از ورودی مخفی می‌گیرد.
7. اسکریپت ویندوزی `scripts/Validate-Stage9-Baseline.ps1` برای اجرای Restore/Build/Test و گیت‌های منبع اضافه شد.

## نتایج اجراشده در این محیط

| کنترل | نتیجه |
|---|---|
| بررسی هش ۶۳ فایل پایه | PASS |
| اعتبارسنجی JSON | ۸ فایل — PASS |
| اعتبارسنجی XML پروژه/پیکربندی/XAML | ۱۰ فایل — PASS |
| کاتالوگ منو | ۱۹۶ ردیف — PASS |
| Registry مالکیت داده | ۳۳ مفهوم، شناسه و نام یکتا — PASS |
| YAML ساختاری Workflow | ۱۲ job مورد انتظار — PASS |
| گیت ایستای spec | PASS |
| گیت مرز معماری | PASS |
| تطبیق Registry مصرف‌کنندگان فعلی | صفر مصرف‌کننده فعال؛ صفر ناشناخته — PASS |
| گیت API، داده موقت، ادغام، امنیت و E2E | PASS-N/A؛ runtime مربوطه عمداً هنوز وجود ندارد و وضعیت آن به‌صورت ماشینی بررسی می‌شود |
| آزمون‌های سیاست fail-closed | ۱۰ از ۱۰ — PASS |
| `git diff --check` | PASS |

## مواردی که تأیید نشده‌اند

محیط اجرای حاضر `dotnet`، PowerShell و `psql/PostgreSQL` ندارد و دسترسی شبکه برای دریافت SDK نیز برقرار نبود؛ بنابراین موارد زیر **اجرا نشده‌اند**:

- `dotnet restore`, `dotnet build`, `dotnet test` واقعی؛
- اجرای PowerShell و تأیید نحوی/عملی اسکریپت‌ها؛
- اجرای مهاجرت روی PostgreSQL واقعی؛
- اجرای Workflow در مخزن remote؛
- فعال‌بودن Required Status Checks و اثبات مسدودشدن PR توسط branch protection.

این موارد به‌عنوان موفق گزارش نمی‌شوند. Workflow شامل ۱۲ job برای اجرای CI روی runner ویندوزی و PostgreSQL آزمایشی و تولید شواهد بدون PII است، اما تا زمان push به remote اجرا نشده است.

## قفل شروع G1

در `docs/persons/persons-module-manifest.json` مقدار `g1Authorized=false` باقی می‌ماند. موانع باز:

- تأیید سیاست داده موقت؛
- تأیید چک‌لیست قرارداد ادغام؛
- تعیین مسئول واقعی هر job؛
- تنظیم Required Status Checks روی مخزن remote؛
- PR آزمایشی که شکست یک check اجباری واقعاً Merge را مسدود کند؛
- Restore/Build/Test روی Windows و نتیجه CI/PostgreSQL پس از انتشار سورس به remote.

هیچ جدول، entity، DbSet، endpoint یا migration تجاری اشخاص ساخته نشده است. هیچ پایگاه داده‌ای توسط این ممیزی تغییر نکرده است.

## اجرای محلی روی Windows

از PowerShell در ریشه پوشه‌ای که `MDGsm.sln` در آن قرار دارد:

```powershell
Set-ExecutionPolicy -Scope Process Bypass -Force
.\scripts\Validate-Stage9-Baseline.ps1
```

پس از موفقیت Restore/Build/Test، تست انتهابه‌انتهای Stage7 فقط روی PostgreSQL محلی و آزمایشی تازه اجرا شود:

```powershell
.\scripts\Start-MDGsm-DisposableTest.ps1
```

اسکریپت پایگاه داده جدید و علامت‌گذاری‌شده ایجاد می‌کند و آن را خودکار حذف نمی‌کند. از اتصال به پایگاه داده دارای اطلاعات واقعی استفاده نشود.


## الحاقیه — اجرای کاربر روی Windows (2026-10-28)

بر اساس خروجی PowerShell ارائه‌شده پس از بسته v0.1:

- `dotnet restore`: موفق.
- `dotnet build --configuration Release --no-restore`: موفق؛ پروژه‌های Domain، Client، Desktop، Application، Infrastructure، Domain.Tests و API ساخته شدند.
- `dotnet test --configuration Release --no-build`: موفق؛ ۱۱ تست، صفر شکست، صفر ردشده.
- بخش گیت‌های Python کامل نشد؛ فرمان `python` پیام Windows Store «Python was not found» را نشان داد. بنابراین validator با خطای عمومی متوقف شد، اما این خروجی به‌تنهایی نشانه شکست قوانین/گیت‌های Python نیست؛ مفسر Python قابل اجرا پیدا نشده بود.

### اصلاح بسته v0.1.1

اسکریپت `Validate-Stage9-Baseline.ps1` اصلاح شد تا نسخه مفسر را قبل از اجرای گیت‌ها اعتبارسنجی کند، `py -3` را نیز جست‌وجو کند و alias فروشگاه Microsoft را با Python واقعی اشتباه نگیرد. اگر Python 3.12+ موجود نباشد، اسکریپت اکنون صریحاً می‌گوید آزمون‌های Python اجرا نشده‌اند؛ این وضعیت نه PASS و نه شکست خود گیت‌ها تلقی می‌شود. گزینه `-SkipPythonGates` نیز به‌وضوح نتیجه را `PARTIAL` گزارش می‌کند و هرگز ادعای قبولی کامل ندارد.

گیت‌ها و ۱۰ آزمون سیاستی در محیط آماده‌سازی با Python 3.13.5 اجرا شدند و همگی موفق بودند. این نتیجه جای اجرای مجدد اسکریپت اصلاح‌شده در Windows کاربر یا آزمون واقعی محافظت شاخه روی مخزن Remote را نمی‌گیرد.
