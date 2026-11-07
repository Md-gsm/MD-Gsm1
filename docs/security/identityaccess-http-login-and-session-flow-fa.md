# مسیر HTTP ورود و صدور نشست IdentityAccess

وضعیت: پیاده‌سازی روی شاخه آزمایشی PR #41؛ CI و بازبینی مستقل هنوز باید تأیید شوند.

## قرارداد HTTP

### ورود
`POST /api/v1/identity/login`

بدنه:
- `username`
- `password`
- `clientInstanceId` (UUID غیرخالی)

پاسخ موفق:
- `accessToken`: توکن تصادفی ۲۵۶ بیتی در قالب Base64Url با طول ۴۳ کاراکتر
- `expiresAtUtc`: زمان پایان lease
- `mustChangePassword`: الزام تغییر رمز پیش از استفاده از سایر APIهای محافظت‌شده

کدهای خطا:
- 401: اطلاعات ورود نامعتبر؛ بدون افشای وجود/وضعیت حساب
- 409: ظرفیت کاربران فعال تکمیل شده
- 429: محدودیت نرخ درخواست از مبدأ
- 503: اتصال پایگاه داده یا کلید HMAC پیکربندی نشده، یا قابلیت HTTP login خاموش است

کلید محدودسازی مبدأ با HMAC-SHA256 از IP اتصال مستقیم ساخته می‌شود. هدرهای Forwarded و X-Forwarded-For قابل اعتماد نیستند. مقدار خام IP و رمز عبور در audit یا log ثبت نمی‌شود. توکن خام فقط در پاسخ موفق داده می‌شود؛ DB فقط SHA-256 آن را نگه می‌دارد.

### تغییر رمز
`POST /api/v1/identity/password/change`

این endpoint نیازمند Bearer token معتبر است. اگر حساب `MustChangePassword=true` باشد، middleware فقط همین مسیر را مجاز می‌داند و سایر مسیرهای Persons را با 403 رد می‌کند. رمز جدید باید ۱۲ تا ۱۲۸ کاراکتر و با رمز فعلی متفاوت باشد.

تغییر رمز:
1. رمز فعلی را بررسی می‌کند.
2. رمز جدید را با PBKDF2/ASP.NET Core Identity ذخیره می‌کند.
3. `CredentialVersion` را افزایش می‌دهد.
4. تمام نشست‌های قبلی را revoke می‌کند.
5. کلاینت را ملزم می‌کند دوباره وارد شود.

## گیت غیرفعال‌سازی

قابلیت HTTP login فقط با تنظیم صریح `IdentityAccess:HttpLoginEnabled=true` فعال می‌شود؛ مقدار پیش‌فرض خاموش است. نبود اتصال PostgreSQL یا کلید HMAC نیز باعث 503 می‌شود. این feature gate جایگزین تأیید استقرار نیست.

هیچ migration تولیدی، seed مجوز، bootstrap credential، listener تولیدی/TLS، یا `g1Authorized=true` در این تغییر ایجاد نشده است.

## آزمون سرتاسری

`IdentityAccessHttpEndToEndTests` با API host واقعی و PostgreSQL موقت:
- درخواست بدون token را 401 می‌گیرد.
- از HTTP ورود می‌کند و token opaque واقعی دریافت می‌کند.
- محدودیت اجباری تغییر رمز را روی Persons با 403 می‌سنجد.
- رمز را از endpoint HTTP تغییر می‌دهد.
- با token قبلی 401 می‌گیرد.
- با رمز جدید دوباره وارد می‌شود.
- درخواست Persons را از session معتبر تا کنترل live permission می‌رساند.
- رمز اشتباه را با 401 عمومی رد می‌کند.

این آزمون فقط پس از اجرای CI روی head نهایی و بررسی logs، شاهد قبولی محسوب می‌شود.
