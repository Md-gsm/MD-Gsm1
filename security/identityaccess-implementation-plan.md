# IdentityAccess — وضعیت مراحل و گیت‌ها
تاریخ 2026-11-04 | پیاده‌سازی مراحل 1 تا 7 روی شاخه feature انجام شده؛ تأیید نهایی منوط به CI و بازبینی‌های release است.

## مراحل 1 تا 4 — مدل و لایه ماندگاری
1. تصمیم‌های مالک درباره شناسه ورود، نشست‌ها، رمز، بازیابی، قفل حساب، bootstrap و نقش‌ها ثبت شده‌اند.
2. مدل منطقی PostgreSQL 18 و قیود IdentityAccess مستند شده‌اند.
3. Domain و Application شامل Username/Account، policy، permission catalog و handlerهای login، تغییر رمز، reset مدیریتی و heartbeat ایجاد شده‌اند.
4. EF Core entities/configurations، DbSetها، Repository adapterهای PostgreSQL و transactional Unit of Work اضافه شده‌اند. هنوز Migration عملیاتی یا endpoint ورود وجود ندارد؛ middleware نشست برای مسیرهای Persons به API متصل شده است.

## مراحل 5 تا 7 — هم‌زمانی و آزمون
5. سقف کاربران فعال در سطح چند process با تراکنش SERIALIZABLE و UPDATE نسخه‌ساز روی singleton policy row سریال می‌شود. تعداد حساب‌های متمایز با نشست معتبر ملاک است؛ نشست اضافه برای حساب فعال ظرفیت تازه مصرف نمی‌کند.
6. تست Integration روی PostgreSQL 18 disposable اضافه شده است: قفل 5 شکست/15 دقیقه، ورود پس از پایان قفل، heartbeat/تمدید lease و ابطال همه نشست‌ها پس از تغییر رمز.
7. آزمون ورود هم‌زمان چند حساب برای سقف 2 کاربر اضافه شده است؛ دو ورود مستقل باید پذیرفته و بقیه رد شوند و ورود دوم همان حساب نباید تعداد حساب‌های فعال را افزایش دهد.

## تصمیم‌های محصول ثبت‌شده در برابر پیشنهادهای نیازمند تصویب
- شناسه ورود، قواعد پایه نشست/قفل حساب، bootstrap local-only و فهرست ۱۰ نقش در `identityaccess-owner-decisions-fa.md` ثبت شده‌اند.
- ماتریس granular مجوز، دوره‌های retention امنیتی و جریان emergency recovery در `identityaccess-five-open-items-fa.md` پیشنهاد هستند؛ سند `identityaccess-owner-decisions-fa.md` صریحاً آن‌ها را تا تصویب/review غیرعملیاتی می‌داند. بنابراین عبارت «تأیید شده» برای این سه مورد صحیح نیست و هیچ seed/retention purge/recovery workflow نباید بر مبنای آن‌ها فعال شود.

## موارد باز و گیت‌های release
- اجرای موفق Build/Test و همه jobهای CI روی commit فعلی؛ نتایج commitهای قبلی به SHA جدید تعمیم داده نمی‌شوند.
- اجرای موفق PostgreSQL 18 disposable integration و تست هم‌زمانی روی همین commit.
- `Username.Parse` در Domain پیاده‌سازی شده و تست‌های .NET پایه وجود دارند؛ اما نسخه Unicode pin، Script_Extensions معتبر، CaseFolding نسخه‌دار، collision scan و fuzz coverage کامل نیستند. Oracle پایتون جایگزین این شواهد نیست.
- `IRecoveryStore` و adapterهای persistence برای challenge و recovery-code digest وجود دارند؛ اما orchestration/workflow، endpoint، سیاست expiry/rate-limit کامل و تست‌های end-to-end replay/race/DB-failure و threat-model review هنوز بازند.
- بازبینی معماری توسط گروه مشاور نرم‌افزار و تصمیم امنیت/حریم خصوصی مالک.
- اثبات Required Status Checks و جلوگیری از merge در SCM؛ دسترسی فعلی GitHub نتوانسته جزئیات protection را بخواند.
- review نهایی schema و ADR-0006؛ سپس فقط با مجوز رسمی G1 می‌توان Migration نسخه‌دار و checksumدار را در PostgreSQL 18 disposable ارزیابی کرد.

## مرزهای صریح
- g1Authorized=false باقی می‌ماند.
- هیچ IdentityAccess migration اضافه یا اجرا نشده و هیچ دیتابیس عملیاتی متصل نشده است.
- هیچ login HTTP endpoint، network listener یا credential bootstrap تولیدی فعال نشده است. Middleware اعتبارسنجی نشست فقط روی مسیرهای Persons فعال است؛ تا زمان صدور توکن از مسیر ورود و تکمیل گیت‌های انتشار، استفادهٔ production مجاز نیست.
- PR #31 باید Open + Draft + Unmerged بماند؛ main بدون تغییر است. هیچ merge مجاز نیست.
- تست Integration فقط دیتابیس جداگانه‌ای را می‌پذیرد که نام و comment marker آن صریحاً disposable باشد و host آن loopback باشد.


## وضعیت جاری و ارجاع مرجع — 2026-11-05

مرجع جاری وضعیت G1، PR #37 و بسته تحویل مستقل در `identityaccess-g1-review-handoff-fa.md` است. هر ارجاع قدیمی‌تر به PR #30 در این سند، وضعیت جاری شاخه/PR را نشان نمی‌دهد و برای تصمیم فعلی ملاک نیست.

روی SHA `406b0d9103ada7177d4e34582ebd8e1c172e18e0` شواهد CI در حال اجرا/انتظار هستند؛ نتایج موفق SHA قبلی به این SHA تعمیم داده نمی‌شوند. IdentityAccess هنوز runtime، middleware، session store یا migration عملیاتی ندارد. ماتریس permission و retention همچنان تصویب نشده‌اند. SMS به فاز آینده موکول شده و Recovery به دلیل نبود عامل جایگزین مستقلِ مصوب غیرفعال می‌ماند. `g1Authorized=false` تا review مستقل و تصمیم صریح مالک برقرار است.


## تصویب ماتریس و قرارداد اجرایی — 2026-11-06

- مالک محصول ماتریس را بدون تغییر grants و scope rules تصویب کرده است.
- قرارداد اجرایی در `identityaccess-authorization-runtime-contract-fa.md` ثبت شده است.
- این اسناد policy design و acceptance contract هستند؛ runtime، middleware، endpoint، migration یا seed ایجاد نشده است.
- سقف‌های مالی هنوز مشخص نیستند؛ عملیات با threshold نامعلوم Deny باقی می‌ماند.
- قرارداد مرزی branch/warehouse در `identityaccess-scope-data-owner-contract-fa.md` ثبت شده؛ اما canonical data owner و شواهد entity/schema/migration/assignment هنوز احراز نشده و این بخش بسته نیست. retention audit و تعریف دقیق self برای Persons نیز بازند.
- G1 مستقل و مجوز اجرایی صریح لازم‌اند؛ `g1Authorized=false` باقی است.


## وضعیت جاری پیاده‌سازی — 2026-11-07
- PR #41 کد login/password change/session validation و Migration IdentityAccess دارد؛ متن‌های قدیمی این سند درباره نبود کامل runtime/login endpoint تاریخی‌اند.
- Session policy فعلی 5 active accounts / 30s heartbeat / 180s lease است؛ absolute TTL=12h یا سقف 2 نشست/account در سیاست جاری وجود ندارد.
- Recovery endpoint/workflow و email delivery هنوز پیاده‌سازی نشده‌اند؛ Store و schema primitive به‌تنهایی Recovery را کامل نمی‌کنند.
- Permission matrix تصویب شده اما seed نشده است. Technician Persons detail تا تعیین منبع assignment، fail-closed است؛ سایر scopeهای row/branch/warehouse آماده نیستند.
- retention، canonical data owner برای Branch/Warehouse/assignment، threshold مالی، SMTP deployment، TLS/topology و review مستقل بازند. `g1Authorized=false`.
