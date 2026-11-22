# اتصال مجوز داده‌های حساس Persons به IdentityAccess

وضعیت: زنجیرهٔ احراز هویت نشست تا مجوز دادهٔ حساس در کد API متصل شده است؛ فعال‌سازی عملیاتی کامل همچنان به گیت‌های انتشار و آزمون PostgreSQL نیاز دارد.

## قرارداد مجوز
- کلید دقیق مجوز: `persons.sensitive.read`.
- کلید در `PermissionCatalog` ثبت شده و طبق ماتریس فعلی فقط نقش `super_admin` آن را به‌صورت پیش‌فرض دارد.
- `IdentityAccessPartySensitiveDataAuthorizer` از `IRolePermissionStore.HasPermissionAsync` استفاده می‌کند؛ fallback یا wildcard ندارد.
- Query مجوز account را به `identity.accounts` join می‌کند و حساب غیرفعال و permission غیرفعال را رد می‌کند. نتیجه cache نمی‌شود.
- خطای permission store fail-closed است و هیچ دادهٔ حساسی برنمی‌گرداند.

## زنجیرهٔ اعتماد فعال در API
1. مسیرهای `/api/v1/persons/**` فقط Bearer token با قالب opaque صادرشده توسط token factory را می‌پذیرند؛ actor ID از body/query/header خوانده نمی‌شود.
2. API توکن خام را log یا persist نمی‌کند؛ SHA-256 digest را محاسبه می‌کند.
3. `PostgresSessionIdentityResolver` در هر درخواست digest را با session زنده تطبیق می‌دهد و هم‌زمان این شروط را enforce می‌کند: `revoked_at IS NULL`، `lease_expires_at > now`، حساب فعال، تکمیل تغییر اجباری رمز و تطابق `credential_version_at_issue` با نسخهٔ جاری حساب.
4. API lease را با `TryHeartbeatAsync` و همان digest و credential version تمدید می‌کند؛ اگر تمدید شکست بخورد درخواست 401 می‌گیرد.
5. فقط پس از موفقیت، principal با authentication type برابر `MDGsm.Session` و claimهای server-created شامل AccountId، SessionId، credential version و زمان احراز هویت ساخته می‌شود.
6. `IAuthenticatedActorContext` فقط همین principal را می‌پذیرد. قرارداد عمومی IdentityAccess principal/session metadata را از این accessor می‌خواند. ارزیابی permission از یک query واحد PostgreSQL استفاده می‌کند که نشست جاری، وضعیت account، credential version و grant فعال را هم‌زمان می‌سنجد. endpoint جزئیات Persons شناسهٔ actor را فقط از همین context می‌گیرد.
7. در صورت درخواست `includeSensitive=true`، repository دوباره permission زندهٔ `persons.sensitive.read` را بررسی می‌کند. تغییر یا لغو permission در درخواست بعدی اثر می‌گذارد.

## رفتار fail-closed
- توکن مفقود/بدقالب، ناشناخته، منقضی یا revoke شده: 401.
- نبود تنظیمات یا سرویس‌های نشست: 503؛ هیچ fallback به دسترسی ناشناس وجود ندارد.
- خطای DB یا permission store: خطای درخواست؛ هرگز allow نمی‌شود.
- endpointهای health و system-info خارج از محدودهٔ protected Persons باقی می‌مانند.

## مواردی که هنوز مجوز انتشار/فعال‌سازی می‌خواهند
- login HTTP endpoint و اتصال handler ورود به host هنوز اضافه نشده؛ بنابراین این تغییر توکن را از طریق HTTP صادر نمی‌کند. این مورد عمداً از scope این اصلاح خارج نگه داشته شده است.
- migration و permission seed تولیدی، bootstrap credential و network listener تولیدی فعال نشده‌اند.
- TLS/firewall و deployment topology باید قبل از هر اتصال شبکه‌ای production بازبینی شوند.
- آزمون middleware با fakeها اضافه شده است. آزمون PostgreSQL واقعی resolver نیز برای نشست معتبر، منقضی، لغوشده، حساب غیرفعال، credential version قدیمی و تغییر اجباری رمز اضافه شده و باید در CI disposable سبز شود؛ تست end-to-end HTTP با توکن واقعاً صادرشده و تست‌های PostgreSQL مجوزِ حذف‌شده/مفقود و خطای DB همچنان بازند.
- تست‌های صریح session منقضی/revoked، account disabled، credential version stale، permission revoked، missing permission و DB failure باید با PostgreSQL واقعی پوشش داده شوند.

این commit به‌تنهایی G1 را تأیید یا production را فعال نمی‌کند. `g1Authorized=false` باقی می‌ماند؛ PRها Draft و Unmerged می‌مانند و `main` تغییر نمی‌کند.


## تکمیل محدود قرارداد عمومی و اتصال نشست زنده — 2026-11-22

- Contracts نسخه 1.1 اکنون یک interface عمومی برای principal، permission و Product Owner decision دارد.
- Host فقط IValidatedRequestPrincipalAccessor را از claimهای server-created پیاده می‌کند.
- IdentityAccess Infrastructure مالک پیاده‌سازی IIdentityAccessAuthorizationContract است و permission را با IRequestAuthorizationStore بررسی می‌کند.
- query PostgreSQL grant را فقط زمانی Allow می‌کند که SessionId دقیق فعال و revoke نشده باشد، lease منقضی نشده باشد، account فعال باشد و credential version نشست با نسخه جاری account منطبق باشد.
- در نبود DB config، یک store صریح fail-closed ثبت می‌شود؛ dependency resolution به رفتار Allow نمی‌افتد.
- این کنترل یک تصمیم request-time است و هنوز جایگزین commit-time authorization linearization در تراکنش Organization نیست.
