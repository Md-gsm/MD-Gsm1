# ADR-0014: مالکیت و محل کد ماژول اشخاص

- وضعیت: پذیرفته‌شده به‌عنوان تصمیم موقت معماری
- تصمیم: تا انتقال کنترل‌شده، کد داخلی Domain/Application/Infrastructure در مسیر فعلی BuildingBlocks باقی می‌ماند؛ قراردادهای عمومی فقط در src/Modules/Persons/Contracts هستند.
- ممنوعیت: هیچ ماژول دیگری نباید به namespace یا پروژه داخلی Persons در BuildingBlocks ارجاع مستقیم دهد.
- شرط پایان: انتقال کامل به src/Modules/Persons/{Domain,Application,Infrastructure}، اصلاح solution/DI/CI و تست معماری بدون وابستگی مستقیم.
- دروازه: انتقال باید پیش از G1 authorization و merge نهایی انجام شود. این ADR مجوز فعال‌سازی یا merge نیست.
- دلیل: حفظ ترتیب PRهای stacked و اجتناب از جابه‌جایی بزرگ هم‌زمان با اصلاحات امنیتی/داده‌ای.
