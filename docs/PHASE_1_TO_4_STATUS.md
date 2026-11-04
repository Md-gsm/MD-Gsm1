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


## مراحل ۵ تا ۷ — کنترل هم‌زمانی و آزمون PostgreSQL

5. **کنترل هم‌زمانی ورود و سقف کاربران فعال:** نقطهٔ سریال‌سازی سراسری روی ردیف singleton تنظیمات تقویت شد. تراکنش SERIALIZABLE با UPDATE نسخه‌ساز روی همان ردیف اجرا می‌شود تا تراکنش‌های منتظر با snapshot قدیمی به serialization failure برسند و با snapshot تازه retry شوند. سقف بر اساس تعداد حساب‌های متمایز دارای نشست معتبر اعمال می‌شود؛ نشست دوم همان حساب ظرفیت جدید مصرف نمی‌کند.
6. **آزمون PostgreSQL:** تست Integration روی دیتابیس جداگانهٔ CI با marker صریح disposable و loopback-only اضافه شد. تست چرخهٔ قفل ۵ تلاش/۱۵ دقیقه، ورود موفق پس از پایان قفل، تمدید lease با heartbeat، و ابطال نشست‌ها پس از تغییر رمز را از طریق Repository و Handlerهای واقعی بررسی می‌کند. مدل دیتابیس تست با EnsureCreated فقط در دیتابیس تازهٔ disposable ایجاد می‌شود؛ Migration عملیاتی جدیدی اضافه یا اجرا نمی‌شود.
7. **آزمون ورود هم‌زمان:** چند حساب مستقل به‌طور هم‌زمان برای سقف ۲ کاربر وارد می‌شوند؛ انتظار دقیقاً دو حساب فعال است. ورود دوم حساب فعال باید مجاز باشد، بدون افزایش تعداد حساب‌های متمایز فعال.

## مرزهای اجرا و شواهد
- تست PostgreSQL فقط از `IdentityAccessTestConnectionString` استفاده می‌کند و پیش از EnsureCreated، loopback host، نام دیتابیس دارای الگوی disposable و database comment marker را بررسی می‌کند.
- این تست به دیتابیس Persons یا دیتابیس عملیاتی متصل نمی‌شود.
- هنوز Migration اجرایی IdentityAccess، endpoint ورود، network listener، credential bootstrap یا تغییر `g1Authorized` ایجاد نشده است.
- نتیجهٔ نهایی مراحل ۵ تا ۷ منوط به اجرای موفق CI روی commit جدید است؛ تا آن زمان وضعیت «پیاده‌سازی‌شده، آزمون CI در انتظار» است.
