# اتصال مجوز داده‌های حساس Persons به IdentityAccess

وضعیت: اتصال adapter و ثبت DI در شاخه integration انجام شده؛ فعال‌سازی مسیر درخواست production هنوز مسدود است.

## تصمیم و قرارداد
- کلید دقیق مجوز: `persons.sensitive.read`.
- کلید در `PermissionCatalog` ثبت شده و طبق ماتریس فعلی فقط نقش `super_admin` آن را به‌صورت پیش‌فرض دارد. نقش‌های دیگر باید با تصمیم صریح مالک و تغییر ماتریس مجوز بگیرند.
- `IdentityAccessPartySensitiveDataAuthorizer` از `IRolePermissionStore.HasPermissionAsync` استفاده می‌کند؛ fallback یا wildcard ندارد.
- ثبت DI در `AddMdGsmPersistence` انجام شده است. وقتی اتصال DB تنظیم نشده باشد، registration زود برمی‌گردد و repository همچنان deny-by-default باقی می‌ماند.
- Query مجوز account را به `identity.accounts` join می‌کند و حساب غیرفعال را رد می‌کند. Permission باید فعال باشد. نتیجه cache نمی‌شود؛ تغییر role/permission در query بعدی دیده می‌شود.
- شناسه actor باید UUID غیرخالی با قالب D باشد. شناسه نامعتبر بدون query رد می‌شود. خطای store به‌صورت exception منتشر می‌شود و هیچ داده حساسی برگردانده نمی‌شود.

## مرز امنیتی که هنوز باید تکمیل شود
`IPartyRepository.GetAsync` یک `actorId` دریافت می‌کند. این مقدار فقط وقتی قابل اعتماد است که لایه API آن را از نشست معتبر سمت سرور استخراج کند؛ هرگز از body، query string، header دلخواه یا مقدار ارسالی Desktop پذیرفته نشود.

در شاخه فعلی هنوز login HTTP endpoint، authentication middleware، token/session validation و API actor-context عملیاتی وجود ندارد. بنابراین adapter به permission store وصل شده، اما مسیر درخواست production هنوز به هویت نشست معتبر متصل نشده است. صرف معتبر بودن UUID اثبات هویت نیست.

پیش از فعال‌سازی runtime باید:
1. API فقط opaque access token را از محل مورد توافق دریافت کند.
2. digest توکن با SHA-256 محاسبه و session را در DB با `revoked_at IS NULL`، `lease_expires_at > now`، حساب فعال و `credential_version` جاری اعتبارسنجی کند.
3. account ID فقط از رکورد session معتبر استخراج و به `GetAsync` داده شود.
4. هیچ actor ID کلاینت‌ساخت به repository نرسد.
5. تست end-to-end برای token نامعتبر/منقضی/لغوشده، account غیرفعال، credential version قدیمی، مجوز حذف‌شده، نبود مجوز و خطای DB اجرا شود.

هیچ migration یا permission seed production در این تغییر اضافه نشده است. `g1Authorized=false` باقی می‌ماند و هیچ endpoint، listener، credential bootstrap یا production migration فعال نمی‌شود.
