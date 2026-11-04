# طراحی منطقی دیتابیس IdentityAccess — پیشنهاد، نه Migration
وضعیت: طراحی برای بازبینی؛ هیچ SQL یا جدول اجرایی در این مرحله ساخته/اعمال نمی‌شود.

## قرارداد PostgreSQL
PostgreSQL 18؛ schema برابر identity؛ کلیدهای UUID؛ زمان‌ها timestamptz به UTC؛ FKها به‌صورت پیش‌فرض RESTRICT؛ تغییر حساب/رمز/نشست/نقش و رویداد ممیزی در تراکنش اتمیک. رمز خام، raw bearer token و recovery code هرگز ذخیره نمی‌شوند.

## جداول و قیود

### identity.accounts
- account_id UUID PK
- login_name TEXT NOT NULL؛ مقدار نمایشی
- login_name_normalized TEXT NOT NULL؛ NFC و canonical case normalization
- password_hash TEXT NOT NULL؛ password_hash_version SMALLINT NOT NULL
- must_change_password BOOLEAN NOT NULL DEFAULT false
- is_enabled BOOLEAN NOT NULL DEFAULT true
- is_bootstrap_account BOOLEAN NOT NULL DEFAULT false
- credential_version BIGINT NOT NULL DEFAULT 1
- failed_attempt_count INTEGER NOT NULL DEFAULT 0 CHECK >= 0
- failure_window_started_at TIMESTAMPTZ NULL؛ locked_until TIMESTAMPTZ NULL
- email TEXT NULL؛ phone_e164 TEXT NULL (تماس/بازیابی، نه login)
- created_at_utc, updated_at_utc TIMESTAMPTZ؛ created_by_account_id UUID NULL
- UNIQUE(login_name_normalized)؛ login غیرخالی و 3..64 نویسه؛ اعتبارسنجی Unicode در Application.
- حداکثر یک حساب bootstrap؛ وضعیت تکمیل bootstrap در تنظیمات singleton ثبت می‌شود.

### identity.roles و identity.permissions
roles: role_id UUID PK، role_key TEXT UNIQUE، display_name، description، is_system_role، is_assignable، timestamps.
permissions: permission_id UUID PK، permission_key TEXT UNIQUE، display_name، description، risk_level CHECK standard/sensitive/critical، is_active.
catalog دقیقاً 10 نقش تصمیم‌نامه را seed می‌کند؛ برای نقش‌ها user ایجاد نمی‌شود. permissionها granular و بدون wildcard ضمنی هستند.

### identity.account_roles
account_id FK accounts RESTRICT، role_id FK roles RESTRICT، assigned_at_utc، assigned_by_account_id FK accounts، assignment_reason؛ PK(account_id,role_id)، index(role_id,account_id). هر تغییر audit می‌شود. آخرین super_admin فعال قابل حذف/غیرفعال‌سازی یا سلب آخرین نقش super_admin نیست.

### identity.role_permissions
role_id FK roles، permission_id FK permissions؛ PK(role_id,permission_id)، index(permission_id,role_id). مجوزها فقط صریح؛ deny-by-default.

### identity.sessions
session_id UUID PK؛ account_id FK accounts؛ token_digest BYTEA UNIQUE و دقیقاً 32 بایت (SHA-256 توکن CSPRNG با حداقل 256 بیت entropy)؛ created_at_utc، last_seen_at_utc، lease_expires_at_utc؛ revoked_at_utc NULL، revoke_reason NULL؛ credential_version_at_issue BIGINT؛ client_instance_id UUID (شناسه تصادفی runtime، نه hardware fingerprint).
Index(account_id,lease_expires_at_utc) و partial index برای نشست‌های فعال. raw token ذخیره نمی‌شود. هر درخواست protected وضعیت حساب، lease، revoke و credential version را از PostgreSQL بررسی می‌کند.

### identity.login_attempts
attempt_id UUID PK؛ account_id UUID NULL FK؛ login_name_digest BYTEA (برای نام ناشناخته، keyed digest)؛ source_key_digest BYTEA (digest شناسه مبدأ حداقلی)؛ outcome CHECK success/failure/locked/throttled؛ occurred_at_utc. Index(account_id,occurred_at_utc) و index(source_key_digest,occurred_at_utc). رمز ارسال‌شده هرگز ذخیره نمی‌شود؛ نگهداری تابع سیاست retention مصوب است.

### identity.recovery_challenges (تکمیل قرارداد بازیابی)
- challenge_id UUID PK؛ account_id FK؛ challenge_digest BYTEA دقیقاً 32 بایت و UNIQUE؛ channel فقط email یا mobile؛ created_at_utc؛ expires_at_utc؛ consumed_at_utc NULL؛ revoked_at_utc NULL.
- صدور challenge جدید در همان کانال، challengeهای مصرف‌نشده قبلی همان حساب/کانال را revoke می‌کند؛ مصرف با UPDATE شرطی و فقط یک‌بار انجام می‌شود.
- این جدول برای پیاده‌سازی قرارداد IRecoveryStore افزوده شده و تأیید دوکاناله همچنان باید در workflow کاربردی واحد و اتمیک enforce شود.

### identity.recovery_codes
recovery_code_id UUID PK؛ account_id FK؛ code_digest BYTEA UNIQUE؛ created_at_utc؛ used_at_utc NULL؛ revoked_at_utc NULL. کد با entropy بالا، فقط یک‌بار نمایش، فقط digest ذخیره، مصرف یک‌باره و صدور/تعویض audit می‌شود. روش دقیق تولید/تحویل نیازمند بازبینی امنیتی است.

### identity.security_audit_events
event_id UUID PK؛ occurred_at_utc؛ actor_account_id/subject_account_id nullable FK؛ event_type؛ outcome؛ correlation_id؛ details JSONB allow-listed فقط metadata غیرمحرمانه. قرارداد append-only؛ runtime DB role اجازه UPDATE/DELETE ندارد. retention/archival هنوز مصوب نیست.

### identity.system_settings
singleton settings_id SMALLINT PK CHECK=1؛ max_active_users INTEGER CHECK>=1 (مقدار آغازین پیشنهادی 5)؛ heartbeat_interval_seconds (پیشنهاد 30)؛ session_lease_seconds (پیشنهاد 180)؛ bootstrap_completed_at_utc NULL؛ updated_at_utc؛ updated_by_account_id. تغییرات فقط با permission super_admin و audit.

## قواعد تراکنش و چرخه عمر
1. Login تراکنشی است؛ ردیف تنظیمات singleton و حساب مرتبط lock می‌شوند. تعداد حساب‌های متمایز فعال با حداقل یک نشست unrevoked و lease معتبر محاسبه می‌شود. اگر حساب از قبل فعال است، login دوم آن را کاربر جدید حساب نمی‌کند. در سقف پر، حساب جدید رد و نشست موجود اخراج نمی‌شود.
2. توکن opaque تصادفی >=256 بیت؛ raw token فقط یک‌بار روی TLS معتبر برمی‌گردد؛ فقط SHA-256 digest ذخیره می‌شود.
3. heartbeat با فاصله پیشنهادی 30s و lease پیشنهادی 180s تمدید می‌شود؛ قطع کوتاه شبکه بلافاصله نشست را منقضی نمی‌کند.
4. logout نشست جاری را revoke می‌کند. تغییر/reset رمز credential_version را افزایش داده و تمام نشست‌های حساب را در همان تراکنش revoke می‌کند.
5. 5 شکست در پنجره 15 دقیقه‌ای، قفل 15 دقیقه‌ای حساب؛ rate-limit مبدأ مستقل و تکمیلی؛ ورود موفق شمارنده حساب را پاک می‌کند.
6. مجوزها در هر درخواست سمت سرور resolve می‌شوند. scope تکنسین از رابطه تخصیص در ماژول مربوطه بررسی می‌شود، نه از شناسه ارسالی کلاینت.
7. bootstrap یک‌باره و local-only است؛ تا commit تکمیل رمز/پروفایل، network binding و business endpoints بسته می‌مانند.

## وضعیت پیاده‌سازی و مرز Migration
با مجوز صریح مالک در 2026-11-04، فقط برای شاخه feature مربوط به PR #31، مدل‌های EF Core، DbSetها، mappingها، repository adapterها و Unit of Work پیاده‌سازی شده‌اند. این مجوز محدود، ممنوعیت ایجاد/اجرای Migration، اتصال به دیتابیس، فعال‌سازی endpoint یا listener شبکه و فعال‌سازی G1 را تغییر نمی‌دهد.
هیچ SQL در database/migrations اضافه نشده، DbMigrator اجرا نشده و به هیچ دیتابیسی متصل نشده‌ایم. پیش از هر Migration، schema review و ADR-0006 review، تأیید رسمی G1، سپس تست نسخه‌دار و checksumدار در PostgreSQL 18 disposable با تراکنش، rollback/failure و idempotence الزامی است.

