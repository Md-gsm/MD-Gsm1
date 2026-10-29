# ثبت اجرای اعتبارسنجی Windows — 2026-10-28

این یادداشت فقط خروجی‌ای را ثبت می‌کند که کاربر از ماشین Windows ارسال کرده است.

## نتایج گزارش‌شده

- `dotnet restore`: موفق.
- `dotnet build .\MDGsm.sln --configuration Release --no-restore`: موفق.
- پروژه‌های Domain، Client، Desktop، Application، Infrastructure، Domain.Tests و API در خروجی Build موفق بودند.
- `dotnet test .\MDGsm.sln --configuration Release --no-build`: ۱۱ تست موفق، ۰ شکست، ۰ ردشده.
- گیت‌های Python کامل نشدند، زیرا فرمان `python` پیام Windows Store «Python was not found» را داد. بنابراین این اجرا **قبولی کامل سورس‌گیت‌ها نیست**؛ در عین حال، خروجی ارائه‌شده نیز شکست خود گیت‌های Python را اثبات نمی‌کند، چون مفسر اجرا نشده است.

## اصلاح در بسته v0.1.1

`Validate-Stage9-Baseline.ps1` اکنون مفسر واقعی Python 3.12+ را با اجرای `--version` اعتبارسنجی می‌کند، در صورت نیاز `py -3` را امتحان می‌کند و alias فروشگاه Microsoft را به‌اشتباه Python واقعی نمی‌شمارد. در نبود مفسر، پیام توقف مشخص می‌کند که Restore/Build/Test دات‌نت قبلاً موفق شده‌اند اما گیت‌های Python اجرا نشده‌اند.

گیت‌های Python و ۱۰ آزمون fail-closed در محیط آماده‌سازی این بسته با Python 3.13.5 دوباره اجرا شده و موفق بوده‌اند. این نتیجه، اجرای اسکریپت PowerShell اصلاح‌شده روی Windows یا آزمون محافظت شاخه روی مخزن Remote را جایگزین نمی‌کند.
