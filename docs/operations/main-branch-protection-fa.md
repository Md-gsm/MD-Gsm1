# راهنمای حفاظت شاخه main در MD-Gsm

## وضعیت فعلی

این راهنما برای مخزن `Md-gsm/MD-Gsm1` و شاخه `main` است. خواندن تازهٔ metadata شاخه نشان می‌دهد `main` محافظت‌شده است، اما فقط ۱۲ مورد از ۱۳ check الزامی را ثبت کرده است. مورد جاافتاده:

`persons-postgres-windows-migrations`

بنابراین گیت حفاظت کامل هنوز بسته نیست و در manifest مقدار `requiredStatusChecksConfiguredInScm=false` باقی می‌ماند. endpoint جزئیات کامل Branch Protection از اتصال فعلی با پاسخ 403 قابل خواندن نبود؛ در نتیجه مقدار review approval و تمام جزئیات دیگر تا read-back ادمین نباید قطعی فرض شوند.

## پیش‌نیازها

- Windows PowerShell 5.1 یا PowerShell جدیدتر
- GitHub CLI نصب‌شده و احراز هویت‌شده با حساب دارای Admin و دسترسی Administration: write
- دریافت نسخه به‌روزشدهٔ `scripts/Configure-MainBranchProtection.ps1` از شاخهٔ اصلاحات این PR
- اجرای اسکریپت از ریشهٔ مخزن

## اجرای گزارش بدون تغییر

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
gh auth status
.\scripts\Configure-MainBranchProtection.ps1
```

اسکریپت SHA فعلی `main` را می‌خواند، وضعیت آخرین ۱۳ check را بررسی می‌کند و سپس جزئیات حفاظت فعلی را می‌خواند. در حالت پیش‌فرض هیچ تنظیمی در GitHub تغییر نمی‌کند.

## اعمال پس از بازبینی گزارش

```powershell
.\scripts\Configure-MainBranchProtection.ps1 -Apply
```

اسکریپت تنظیمات زیر را اعمال و از GitHub دوباره می‌خواند:

- الزام هر ۱۳ check واقعی workflow، شامل آزمون PostgreSQL روی Windows؛
- الزام به‌روز بودن شاخه با مقصد؛
- الزام PR؛
- حداقل یک تأیید مستقل؛
- رد تأییدهای قدیمی پس از تغییرات و الزام تأیید آخرین push؛
- enforce برای مدیران، بدون bypass؛
- حفظ linear history، conversation resolution و محدودیت حذف/force-push.

اسکریپت در صورت عدم وجود هر check روی HEAD فعلی، یا سبز نبودن آن، پیش از هر تغییر متوقف می‌شود. پس از Apply نیز اگر count، نام دقیق checkها یا سیاست review مطابق انتظار نباشد، شکست اعلام می‌کند.

## سیاست بازبینی مستقل

حداقل یک تأیید GitHub Review از فردی غیر از آخرین push کننده لازم است. بازبینی گروه مشاور باید روی SHA دقیق همان PR انجام شود. ثبت comment یا اعلام شفاهی جایگزین GitHub Review مورد نیاز برای حفاظت شاخه نیست. تأیید آخرین push باید پس از آخرین تغییر کد ثبت شود؛ هر push جدید، تأیید قبلی را از اعتبار می‌اندازد.

## Signed commits و روش ادغام

سیاست signed commits برای `main` در شواهد قبلی ثبت شده است. commitهای feature branch در PR #12 امضای معتبر ندارند. در زمان ادغام، فقط از **Squash and merge در رابط وب GitHub** استفاده شود؛ از rebase merge یا روشی که commitهای unsigned را مستقیماً وارد `main` کند استفاده نشود. پس از ادغام مجاز، امضای commit نهایی `main` باید در GitHub با وضعیت **Verified** بررسی شود. این دستورالعمل مجوز ادغام PR فعلی نیست.

## آزمون منفی پس از اصلاح حفاظت

PR #6 آزمون منفی معماری پیشین بود، اما قبل از تکمیل rule فعلی اجرا شده است. پس از read-back موفق ۱۳ check و الزام یک approval، یک PR آزمایشی جداگانه با check اجباری ناموفق بسازید و از رابط GitHub ثابت کنید Merge مسدود می‌شود. PR آزمایشی نباید merge شود و پس از ثبت شواهد بسته شود.

## محدودیت دسترسی اتصال

اتصال GitHub در این نشست فقط امکان خواندن metadata شاخه و تغییر فایل‌های مخزن را دارد؛ endpoint کامل حفاظت را 403 می‌دهد و عملیات نوشتن Branch Protection در دسترس نیست. بنابراین Apply باید با GitHub CLI و حساب Admin مالک مخزن اجرا شود. تا read-back واقعی بعد از Apply، مقدار manifest نباید `true` شود.
