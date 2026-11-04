# IdentityAccess — وضعیت مراحل و گیت‌ها
تاریخ 2026-11-04 | پیاده‌سازی مراحل 1 تا 7 روی شاخه feature انجام شده؛ تأیید نهایی منوط به CI و بازبینی‌های release است.

## مراحل 1 تا 4 — مدل و لایه ماندگاری
1. تصمیم‌های مالک درباره شناسه ورود، نشست‌ها، رمز، بازیابی، قفل حساب، bootstrap و نقش‌ها ثبت شده‌اند.
2. مدل منطقی PostgreSQL 18 و قیود IdentityAccess مستند شده‌اند.
3. Domain و Application شامل Username/Account، policy، permission catalog و handlerهای login، تغییر رمز، reset مدیریتی و heartbeat ایجاد شده‌اند.
4. EF Core entities/configurations، DbSetها، Repository adapterهای PostgreSQL و transactional Unit of Work اضافه شده‌اند. هنوز Migration عملیاتی یا endpoint ورود وجود ندارد.

## مراحل 5 تا 7 — هم‌زمانی و آزمون
5. سقف کاربران فعال در سطح چند process با تراکنش SERIALIZABLE و UPDATE نسخه‌ساز روی singleton policy row سریال می‌شود. تعداد حساب‌های متمایز با نشست معتبر ملاک است؛ نشست اضافه برای حساب فعال ظرفیت تازه مصرف نمی‌کند.
6. تست Integration روی PostgreSQL 18 disposable اضافه شده است: قفل 5 شکست/15 دقیقه، ورود پس از پایان قفل، heartbeat/تمدید lease و ابطال همه نشست‌ها پس از تغییر رمز.
7. آزمون ورود هم‌زمان چند حساب برای سقف 2 کاربر اضافه شده است؛ دو ورود مستقل باید پذیرفته و بقیه رد شوند و ورود دوم همان حساب نباید تعداد حساب‌های فعال را افزایش دهد.

## تصمیم‌های مالک که تأیید شده‌اند
- ماتریس نقش‌ها و مجوزهای پیشنهادی فعلی.
- نگهداری سوابق حساس 24 ماه و تلاش‌های ناموفق/نشست‌ها 12 ماه.
- بازیابی مدیر با تأیید دو کانال ایمیل و موبایل و کد اضطراری یک‌بارمصرف آفلاین.

## موارد باز و گیت‌های release
- اجرای موفق Build/Test و همه jobهای CI روی commit فعلی؛ نتایج commitهای قبلی به SHA جدید تعمیم داده نمی‌شوند.
- اجرای موفق PostgreSQL 18 disposable integration و تست هم‌زمانی روی همین commit.
- تست .NET کامل username normalization، Unicode version pin، Script_Extensions معتبر و collision scan.
- threat-model review برای recovery/bootstrap، انتخاب provider و TTL چالش.
- بازبینی معماری توسط گروه مشاور نرم‌افزار و تصمیم امنیت/حریم خصوصی مالک.
- اثبات Required Status Checks و جلوگیری از merge در SCM؛ دسترسی فعلی GitHub نتوانسته جزئیات protection را بخواند.
- review نهایی schema و ADR-0006؛ سپس فقط با مجوز رسمی G1 می‌توان Migration نسخه‌دار و checksumدار را در PostgreSQL 18 disposable ارزیابی کرد.

## مرزهای صریح
- g1Authorized=false باقی می‌ماند.
- هیچ IdentityAccess migration اضافه یا اجرا نشده و هیچ دیتابیس عملیاتی متصل نشده است.
- هیچ login endpoint، auth middleware عملیاتی، network listener یا credential bootstrap تولیدی فعال نشده است.
- PR #31 باید Open + Draft + Unmerged بماند؛ main بدون تغییر است. هیچ merge مجاز نیست.
- تست Integration فقط دیتابیس جداگانه‌ای را می‌پذیرد که نام و comment marker آن صریحاً disposable باشد و host آن loopback باشد.
