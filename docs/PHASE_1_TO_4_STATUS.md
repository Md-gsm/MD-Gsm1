# وضعیت پیاده‌سازی IdentityAccess — مراحل ۱ تا ۴
تاریخ: 2026-11-04

این تغییر صرفاً پیاده‌سازی لایه persistence روی شاخه feature است؛ مجوز G1 یا فعال‌سازی عملیاتی نیست.

## انجام‌شده در سورس
1. مدل‌های persistence و mappingهای EF Core برای accounts، roles، permissions، account_roles، role_permissions، sessions، login_attempts، recovery_codes، recovery_challenges، security_audit_events و system_settings اضافه شدند.
2. DbSetها به MdGsmDbContext افزوده شدند؛ قیود کلیدی، unique/partial indexes، FKهای RESTRICT، check constraintهای digest و singleton و JSONB/bytea mapping مشخص شدند.
3. Adapter مشترک PostgreSQL برای IAccountStore، ISessionStore، ILoginAttemptStore، IRolePermissionStore، IAuditWriter، ISystemPolicyStore و IRecoveryStore پیاده شد و در DI ثبت شد.
4. IIdentityAccessUnitOfWork تراکنش Serializable و قفل SELECT ... FOR UPDATE روی ردیف singleton تنظیمات را اعمال می‌کند؛ lookup حساب نیز در تراکنش، ردیف حساب را FOR UPDATE قفل می‌کند. در retry، ChangeTracker پاک می‌شود تا state تلاش قبلی وارد تلاش بعدی نشود.

## محدودیت‌ها و گیت‌های باز
- هیچ Migration ایجاد یا اجرا نشده و هیچ دیتابیسی متصل نشده است. مدل EF به‌تنهایی جدول ایجاد نمی‌کند.
- جدول recovery_challenges افزودهٔ منطقی لازم برای قرارداد دوکاناله موجود در IRecoveryStore است و باید در بازبینی schema و Migration آینده بررسی شود.
- rate limit مبدأ در این نسخه ۲۰ تلاش ناموفق در ۵ دقیقه است؛ مقدار اولیه اجرایی است و باید در threat-model و آزمون بار تأیید شود.
- ثبت رویداد ممیزی در API فقط append انجام می‌دهد؛ منع UPDATE/DELETE باید در DB role/grants در Migration و استقرار اعمال شود.
- محافظت آخرین super_admin، تأیید دوکاناله در workflow کامل، ثبت ایمیل/موبایل و مدیریت پروفایل، و endpointها در این مرحله پیاده‌سازی نشده‌اند.
- Build و تست PostgreSQL 18 disposable تا اجرای CI تأیید نشده‌اند. هیچ ادعای آماده‌بودن production یا G1 مطرح نیست.
- g1Authorized=false؛ هیچ endpoint ورود، listener شبکه، credential bootstrap یا Migration اضافه نشده است.
