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

## مرز Migration
این صرفاً مدل منطقی است. فعلاً SQL در database/migrations اضافه نشود، DbSet ثبت نشود، DbMigrator اجرا نشود و به DB غیرآزمایشی متصل نشویم. بعد از ADR-0006 review و مجوز رسمی G1، migration نسخه‌دار و checksumدار باید در PostgreSQL 18 disposable، با تست تراکنش، rollback/failure و idempotence بررسی شود.

 
## مدل تکمیلی Emergency Recovery — منطقی و غیر اجرایی

این بخش صرفاً قرارداد منطقی است؛ نام‌ها و قیود باید در review مستقل بررسی شوند و هیچ Migration یا DbSet ایجاد نمی‌کند.

### identity.recovery_challenges
- challenge_id UUID PK؛ account_id UUID NOT NULL FK identity.accounts ON DELETE RESTRICT؛ created_at_utc TIMESTAMPTZ NOT NULL؛ expires_at_utc TIMESTAMPTZ NOT NULL؛ state TEXT NOT NULL با CHECK روی Created/EmailVerified/MobileVerified/ProofsComplete/Completed/Expired/Locked/Cancelled/FailedClosed؛ attempt_count INTEGER NOT NULL CHECK >=0؛ locked_at_utc NULL؛ completed_at_utc NULL؛ cancelled_at_utc NULL؛ correlation_id UUID NOT NULL.
- UNIQUE(challenge_id,account_id) برای FKهای ترکیبی؛ CHECK(expires_at_utc > created_at_utc) و CHECK سازگاری state با completed/cancelled timestamp.
- index(account_id,state,expires_at_utc) و index(expires_at_utc) برای cleanup کنترل‌شده.
- یک Challenge فعال برای هر account در صورت تصویب سیاست تک‌Challenge فعال؛ partial unique index باید فقط روی stateهای فعال و با تعریف دقیق stateها ایجاد شود.
- Challenge ID به‌تنهایی مجوز نیست؛ هیچ endpoint عمومی یا شبکه‌ای برای ایجاد/تکمیل آن تعریف نمی‌شود.

### identity.recovery_channel_proofs
- proof_id UUID PK؛ challenge_id UUID NOT NULL FK recovery_challenges؛ account_id UUID NOT NULL FK accounts؛ channel TEXT NOT NULL CHECK email/mobile؛ proof_digest BYTEA NOT NULL؛ digest_key_version SMALLINT NOT NULL؛ created_at_utc، expires_at_utc؛ verified_at_utc NULL؛ consumed_at_utc NULL؛ attempt_count INTEGER NOT NULL CHECK >=0؛ revoked_at_utc NULL.
- UNIQUE(challenge_id,channel) برای جلوگیری از جایگزینی مبهم proof در یک Challenge؛ قید یکتا روی (challenge_id,proof_id) برای مرجع ترکیبی.
- FK ترکیبی (challenge_id,account_id) به recovery_challenges(challenge_id,account_id) تضمین می‌کند proof به همان account متصل است.
- raw OTP، مقصد کامل ایمیل/موبایل، provider payload و secret هرگز ذخیره نشوند. اگر برای ارسال مقصد لازم است، از contact reference محافظت‌شده و حداقل‌سازی PII استفاده شود؛ ذخیره snapshot خام مقصد نیازمند تصمیم حریم خصوصی مستقل است.
- proof تأییدشده باید فقط یک بار مصرف شود؛ replay هیچ transition یا side effect جدیدی ندارد.

### identity.recovery_operations
- operation_id UUID PK؛ challenge_id UUID NOT NULL UNIQUE FK؛ account_id UUID NOT NULL FK؛ state TEXT CHECK Pending/Committed/Failed؛ created_at_utc؛ committed_at_utc NULL؛ result_code غیرحساس؛
- UNIQUE(operation_id,account_id) و FK ترکیبی (challenge_id,account_id) به recovery_challenges(challenge_id,account_id)؛ هیچ request body یا secret ذخیره نشود.
- operation_id کلید idempotency است؛ retry با همان شناسه باید نتیجه durable را بخواند، نه عملیات دوم بسازد. نتیجه commit نامعلوم فقط از PostgreSQL reconcile می‌شود.

### قیود مصرف و تراکنش
1. ترتیب lock ثابت: account سپس challenge سپس recovery_code و proofها؛ همه مسیرهای تغییر باید همین ترتیب را رعایت کنند.
2. recovery_code با `UPDATE ... WHERE used_at_utc IS NULL AND revoked_at_utc IS NULL RETURNING` یا معادل اتمیک مصرف شود؛ تعداد ردیف برگشتی باید دقیقاً یک باشد.
3. unique constraint روی operation_id و ارتباط challenge/account مانع استفاده از نتیجه برای حساب دیگر می‌شود.
4. در یک transaction واحد: اعتبارسنجی challenge/proofs/code، مصرف code، ثبت operation، افزایش credential_version، revoke همه sessionها، must_change_password، Cancel سایر Challengeهای باز، audit allow-list و Completed.
5. هر constraint violation یا خطای DB کل transaction را rollback می‌کند. عملیات با commit نامعلوم نباید با operation_id جدید تکرار شود.
6. proof و Challengeهای منقضی‌شده با job پاکسازی غیرحساس و طبق retention مصوب مدیریت می‌شوند؛ cleanup نباید state تکمیل‌شده یا audit را حذف کند.

## مدل پیشنهادی Scope برای Authorization

این بخش هیچ Permission یا نقش جدیدی seed نمی‌کند:
- permission grant فقط کلید دقیق permission و role را مشخص می‌کند؛ scope به‌صورت جداگانه با نوع scope و شناسه محدوده ثبت می‌شود.
- مدل پیشنهادی `identity.role_permission_scopes(role_id, permission_id, scope_kind, scope_id)` با PK مرکب (role_id,permission_id,scope_kind,scope_id) و FK به role_permissions؛ `scope_kind` محدود به self/assigned/branch/warehouse/all.
- `all` فقط grant صریح همان permission است و wildcard نیست. scope_id برای self/assigned می‌تواند null کنترل‌شده باشد؛ برای branch/warehouse باید شناسه واقعی و FK به ماژول مالک داشته باشد، اما IdentityAccess نباید مالک داده یا جدول شعبه/انبار شود.
- ترکیب چندنقشی فقط union grantهای صریح همان permission انجام می‌شود؛ نبود context یا خطای lookup در ماژول مالک Deny است.
- رابطه تخصیص تکنسین و رکوردهای Persons/Repair/Inventory در ماژول مالک باقی می‌ماند. IdentityAccess فقط principal و grant را ارائه می‌کند؛ query نهایی باید در ماژول مالک scope را enforce کند.
- این طرح تا تعیین data-owner contract و بررسی چرخه وابستگی، صرفاً پیشنهاد است؛ ایجاد FK بین schemaها بدون تأیید مالکیت ممنوع.

## Bootstrap state constraints

وضعیت Bootstrap باید state machine مستقل داشته باشد: Uninitialized -> Provisioning -> PasswordChangeRequired -> ProfileCompletionRequired -> Completed؛ حالت FailedClosed در خطا. فقط عملیات تغییر رمز اولیه و تکمیل پروفایل در حالت‌های میانی مجازند. هیچ business permission یا API route نباید از وضعیت bootstrap استنتاج Allow بگیرد. مقدار ثابت `admin/admin` در ADR-0013 نیازمند تصمیم تکمیلی مالک برای روش provisioning امن است؛ schema نباید default password یا credential ثابت تعریف کند.
## تصمیم‌های مصوب مالک درباره Recovery و نشست — 2026-11-05 (بدون Migration)

### تنظیمات Recovery
- بازار هدف: ایران.
- ایمیل: mailbox اختصاصی روی دامنه محصول؛ ارسال با SMTP relay/Transactional Email. ایجاد Mail Server مستقل داخل برنامه انتخاب نشده است. نام میزبان SMTP و جزئیات DNS در زمان استقرار تعیین می‌شوند.
- ارائه‌دهنده SMS هنوز انتخاب نشده و باید پیش از فعال‌سازی Recovery از نظر OTP template، تحویل، SLA، خط خدماتی، API، وضعیت delivery و مدیریت خطا ارزیابی شود.
- email_otp_ttl_seconds = 600؛ sms_otp_ttl_seconds = 300.
- recovery_max_failed_attempts = 5؛ پس از رسیدن به حد، قفل Recovery برابر 86400 ثانیه.
- resend_cooldown_seconds = 3600؛ max_sends_per_rolling_hour = 1؛ max_sends_per_rolling_24h = 5؛ عبور از هر حد، قفل ارسال/Recovery برابر 86400 ثانیه.
- شمارنده‌ها و قفل با زمان PostgreSQL و transaction/atomic update اجرا شوند. شمارش حداقل per account و channel و نیز per source؛ محدودیت source نباید امکان قفل‌کردن قربانی از راه دور را به‌تنهایی ایجاد کند.
- resend proof قبلی را باطل می‌کند. provider timeout/unknown result هرگز به‌معنی ارسال/تأیید موفق نیست.
- این اعداد برای Recovery هستند و lockout ورود عادی را تغییر نمی‌دهند.

### تنظیمات Session
- heartbeat_interval_seconds = 30؛ session_lease_seconds = 180؛ absolute_session_ttl_seconds = 43200.
- max_concurrent_sessions_per_account = 2.
- max_active_distinct_accounts = 5؛ قابل تغییر توسط super_admin با permission اختصاصی و audit.
- password change و account disable باید revoke تمام نشست‌های حساب را اتمیک انجام دهند. برای password change، credential_version نیز افزایش یابد.
- absolute TTL از created_at_utc محاسبه می‌شود و heartbeat آن را تمدید نمی‌کند. نشست فعال باید revoked نشده، lease معتبر، absolute expiry معتبر، account enabled و credential version جاری داشته باشد.
- کنترل دو نشست در هر حساب و سقف کل پنج حساب باید transactionally serialized باشند.

این بخش مدل منطقی است؛ هیچ migration، DbSet یا runtime ایجاد/فعال نمی‌کند.


## تعیین دامنه فعلی: عدم پیاده‌سازی پیامک — 2026-11-05
- بنا بر تصمیم مالک، در این مرحله هیچ SMS provider، SDK، API client، credential، template، configuration، background job یا تست وابسته به سرویس پیامک اضافه نمی‌شود.
- قابلیت SMS به مرحله/نسخه آینده موکول است و در این مرحله صرفاً به‌عنوان توسعه آتی مستند می‌شود؛ انتخاب vendor نیز اکنون لازم نیست.
- نبود SMS به معنی مجاز شدن بازیابی با ایمیل تنها نیست. تا زمان تصویب و پیاده‌سازی یک ترکیب عامل بازیابی امن و آزموده‌شده، Recovery اضطراری در محیط عملیاتی غیرفعال/غیرقابل دسترس می‌ماند. عامل offline recovery code نیز به‌تنهایی جایگزین عامل مستقل دوم نمی‌شود مگر آنکه مدل تهدید و سیاست مالک جداگانه تصویب شود.
- مقادیر TTL پیامک و محدودیت ارسال پیامک که پیش‌تر در سند آمده‌اند، مشخصات هدف آینده‌اند و در این مرحله هیچ رفتار اجرایی یا تست runtime برای SMS ایجاد نمی‌کنند.
- ایمیل اختصاصی نیز در این مرحله فقط معماری هدف است؛ SMTP relay واقعی و secretها تا انتخاب/پیکربندی عملیاتی وارد برنامه نمی‌شوند.
