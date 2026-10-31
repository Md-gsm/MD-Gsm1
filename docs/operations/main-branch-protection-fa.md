# راهنمای حفاظت شاخه main در MD-Gsm

## وضعیت این بسته

این ابزار برای مخزن Md-gsm/MD-Gsm1 و شاخه main تهیه شده است. اسکریپت ابتدا وضعیت شاخه، احراز هویت GitHub CLI، دوازده Check واقعی تعریف‌شده در workflow و نتیجه آخرین اجرای آن‌ها را بررسی می‌کند. در حالت پیش‌فرض هیچ تغییری در GitHub ایجاد نمی‌کند.

## پیش‌نیازها

- Windows PowerShell 5.1 یا PowerShell جدیدتر
- GitHub CLI نصب‌شده و احراز هویت‌شده با حساب دارای دسترسی Admin به مخزن
- دسترسی لازم برای خواندن و تغییر Branch Protection
- اجرای اسکریپت از پوشه ریشه مخزن

## اجرای گزارش بدون تغییر

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
gh auth status
.\scripts\Configure-MainBranchProtection.ps1
```

اسکریپت در این حالت:
1. شاخه و SHA فعلی main را از GitHub می‌خواند.
2. حضور و موفقیت هر ۱۲ check را روی همان SHA بررسی می‌کند.
3. تنظیمات فعلی حفاظت شاخه را می‌خواند.
4. سیاست پیشنهادی را چاپ می‌کند؛ هیچ تنظیمی را تغییر نمی‌دهد.

## اعمال پس از بازبینی گزارش

```powershell
.\scripts\Configure-MainBranchProtection.ps1 -Apply
```

سیاست درخواستی شامل الزام PR، دوازده check اجباری، به‌روز بودن شاخه با مقصد، اعمال محدودیت برای مدیران، الزام حل گفتگوهای باز PR (Require conversation resolution)، ممنوعیت Force Push و ممنوعیت حذف شاخه است. پس از اعمال، اسکریپت تنظیمات را دوباره از GitHub می‌خواند و تعداد و نام دقیق checkها و تمام گزینه‌های اصلی را راستی‌آزمایی می‌کند.

### نکته درباره تأیید بازبین

در نسخه فعلی، تعداد تأییدهای اجباری PR برابر صفر تنظیم شده است؛ زیرا هنوز حساب یا تیم مستقل بازبین برای الزام یک تأیید مشخص نشده است. این تنظیم، الزام PR و CI را فعال می‌کند اما جایگزین بازبینی انسانی نیست. پس از تعیین سازوکار بازبین مستقل، می‌توان تعداد تأییدها و الزام Code Owner را جداگانه افزایش داد.

### الزام حل گفتگوهای PR

گزینه `required_conversation_resolution.enabled` فعال می‌شود؛ بنابراین گفتگوهای ثبت‌شده در Review/PR باید پیش از Merge حل‌وفصل شوند. این الزام جایگزین بازبینی انسانی یا تأیید بازبین نیست.

## Checkهای واقعی مورد انتظار

نام‌ها از docs/operations/required-status-checks.md و jobهای workflow استخراج شده‌اند:

- foundation-build-test
- persons-spec-lint
- persons-architecture-boundary
- persons-consumer-registry
- persons-api-contract
- persons-temporary-data-retention
- persons-merge-contract
- persons-postgres-migrations
- persons-security-tests
- persons-e2e-disposable
- persons-ci-enforcement-test
- persons-ci-evidence

اسکریپت اگر حتی یکی از checkها روی آخرین SHA وجود نداشته باشد یا موفق نشده باشد، عملیات را متوقف می‌کند.

## محدودیت مهم

موفقیت اسکریپت و فعال‌شدن Branch Protection به‌تنهایی آزمون منفی واقعی را انجام نمی‌دهد. برای بستن کامل گیت، باید یک PR آزمایشی با check شکست‌خورده ایجاد شود و مسدودشدن Merge از سمت GitHub ثبت شود. این آزمون باید جداگانه و کنترل‌شده انجام شود.
