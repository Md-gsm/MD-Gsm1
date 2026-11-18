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
- challenge_id UUID PK؛ account_id FK؛ challenge_digest BYTEA دقیقاً 32 بایت و UNIQUE؛ channel فقط email؛ created_at_utc؛ expires_at_utc؛ consumed_at_utc NULL؛ revoked_at_utc NULL.
- صدور challenge جدید، challengeهای باز قبلی همان حساب را revoke می‌کند؛ مصرف با UPDATE شرطی و فقط یک‌بار انجام می‌شود. در فاز فعلی تنها کانال مصوب email است؛ هیچ تأیید دوکاناله‌ای وجود ندارد.

### identity.recovery_codes
recovery_code_id UUID PK؛ account_id FK؛ code_digest BYTEA UNIQUE؛ created_at_utc؛ used_at_utc NULL؛ revoked_at_utc NULL. جدول در migration وجود دارد، اما runtime role مجوز INSERT/صدور کد ندارد؛ provisioning آن در فاز فعلی فعال یا عامل اجباری Recovery نیست.

### identity.security_audit_events
event_id UUID PK؛ occurred_at_utc؛ actor_account_id/subject_account_id nullable FK؛ event_type؛ outcome؛ correlation_id؛ details JSONB allow-listed فقط metadata غیرمحرمانه. قرارداد append-only؛ runtime DB role اجازه UPDATE/DELETE ندارد. retention/archival هنوز مصوب نیست.

### identity.system_settings
singleton settings_id SMALLINT PK CHECK=1؛ max_active_users INTEGER CHECK>=1 (مقدار آغازین مصوب 5)؛ heartbeat_interval_seconds (مقدار مصوب 30)؛ session_lease_seconds (مقدار مصوب 180)؛ bootstrap_completed_at_utc NULL؛ updated_at_utc؛ updated_by_account_id. تغییرات فقط با permission اختصاصی super_admin و audit. عمر مطلق و سقف نشست/account در schema وجود ندارد و نباید اضافه شود.

## قواعد تراکنش و چرخه عمر
1. Login تراکنشی است؛ ردیف تنظیمات singleton و حساب مرتبط lock می‌شوند. تعداد حساب‌های متمایز فعال با حداقل یک نشست unrevoked و lease معتبر محاسبه می‌شود. اگر حساب از قبل فعال است، login دوم آن را کاربر جدید حساب نمی‌کند. در سقف پر، حساب جدید رد و نشست موجود اخراج نمی‌شود.
   برای جلوگیری از عبور چند API process از سقف در بار هم‌زمان، Unit of Work در تراکنش SERIALIZABLE ردیف singleton را با UPDATE کنترل‌شده تغییرنسخه می‌دهد (حتی اگر مقدار business آن ثابت بماند). تراکنش منتظر با snapshot قدیمی باید با SQLSTATE 40001 retry شود؛ صرف SELECT FOR UPDATE بدون تغییر نسخه کافی تلقی نمی‌شود.
2. توکن opaque تصادفی >=256 بیت؛ raw token فقط یک‌بار روی TLS معتبر برمی‌گردد؛ فقط SHA-256 digest ذخیره می‌شود.
3. heartbeat با فاصله پیشنهادی 30s و lease پیشنهادی 180s تمدید می‌شود؛ قطع کوتاه شبکه بلافاصله نشست را منقضی نمی‌کند.
4. logout نشست جاری را revoke می‌کند. تغییر/reset رمز credential_version را افزایش داده و تمام نشست‌های حساب را در همان تراکنش revoke می‌کند.
5. 5 شکست در پنجره 15 دقیقه‌ای، قفل 15 دقیقه‌ای حساب؛ rate-limit مبدأ مستقل و تکمیلی؛ ورود موفق شمارنده حساب را پاک می‌کند.
6. مجوزها در هر درخواست سمت سرور resolve می‌شوند. scope تکنسین از رابطه تخصیص در ماژول مربوطه بررسی می‌شود، نه از شناسه ارسالی کلاینت.
7. bootstrap یک‌باره و local-only است؛ تا commit تکمیل رمز/پروفایل، network binding و business endpoints بسته می‌مانند.

## وضعیت پیاده‌سازی و مرز Migration — بازنگری 2026-11-17

در main فعلی، migration شماره 0003 برای IdentityAccess وجود دارد و شامل accounts، roles، permissions، account_roles، role_permissions، sessions، login_attempts، recovery_codes، recovery_challenges، security_audit_events و system_settings است. کد Domain/Application/Infrastructure، middleware محدود، login/password-change route و تست‌های IdentityAccess نیز در main موجودند.

این واقعیت به معنی تکمیل همه workflowها، مجوز اجرای migration روی دیتابیس واقعی، فعال‌سازی HTTP login، production bootstrap یا G1 نیست. HTTP login با feature flag پیش‌فرض خاموش است. recovery orchestration/endpoint، provisioning bootstrap، seed نقش/مجوز، SMTP adapter و مدیریت کامل حساب/نشست هنوز کامل نیستند. هر تغییر schema جدید باید ابتدا در رجیستری WG-0 رزرو شود؛ شماره 0009 فقط AVAILABLE است و هنوز RESERVED نیست. تست و پذیرش باید روی PostgreSQL 18 disposable با rollback/failure و idempotence اثبات شود.



 
## وضعیت جاری مدل Recovery و تطبیق با Migration 0003 — 2026-11-17

### آنچه واقعاً در main وجود دارد
- `identity.recovery_challenges`: challenge_id، account_id، challenge_digest، channel با CHECK فقط email، created_at_utc، expires_at_utc، consumed_at_utc و revoked_at_utc؛ digest یکتا و indexهای حساب/کانال/انقضا.
- `identity.recovery_codes`: digest کد و وضعیت used/revoked؛ اما runtime role مجوز INSERT برای provisioning ندارد و این جدول به معنی فعال بودن offline recovery code نیست.
- `IRecoveryStore` و adapter PostgreSQL فقط primitiveهای ایجاد/مصرف challenge و مصرف code را دارند؛ orchestration، endpoint، SMTP sender، شمارنده تلاش، قفل 24 ساعته، cooldown، سقف resend، operation idempotency و تراکنش نهایی Recovery در main کامل نشده‌اند.
- حساب دارای email و phone_e164 است، اما اثبات/تأیید مالکیت ایمیل در schema فعلی ثبت نمی‌شود.
- جدول جداگانه `recovery_channel_proofs` و مدل سه‌عاملی ایمیل+موبایل+offline code در این فاز مصوب نیستند و نباید بدون CR و Migration رزروشده ساخته شوند.

### قیود طراحی برای ادامه کار
1. فقط ایمیل کانال مصوب فعلی است؛ OTP ده‌دقیقه‌ای، digest-only، 5 تلاش، قفل 24 ساعته، resend حداقل هر ساعت، سقف 1 ارسال در rolling hour و 5 ارسال در rolling 24h.
2. SMS و offline recovery code عامل اجباری یا fallback این فاز نیستند.
3. هر challenge باید account-bound، یک‌بارمصرف، کوتاه‌عمر و غیرقابل replay باشد؛ ارسال مجدد challenge قبلی را باطل کند.
4. شمارنده‌ها و قفل‌ها باید اتمیک و متکی به PostgreSQL باشند؛ محدودیت source نباید ابزار DoS علیه قربانی باشد.
5. عملیات نهایی باید تغییر رمز، افزایش credential_version، revoke تمام نشست‌ها، must_change_password و audit را اتمیک ثبت کند.
6. هیچ Recovery runtime تا تعیین نقطه شروع (network endpoint یا local console)، اثبات ایمیل، SMTP/secret management، threat review، تست concurrency/failure و مجوز G1 فعال نشود.

این بخش وضعیت و قیود طراحی را ثبت می‌کند؛ مجوز migration، runtime یا G1 ایجاد نمی‌کند.
