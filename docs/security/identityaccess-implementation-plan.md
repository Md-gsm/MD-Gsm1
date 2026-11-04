# IdentityAccess — مرز پیاده‌سازی و مراحل آماده‌سازی
- وضعیت: تصمیم‌های مالک برای مراحل آماده‌سازی 1 تا 3 ثبت شد (2026-11-04).
- مرتبط: ADR-0006، ADR-0012، ADR-0013.
- این سند مجوز G1، runtime authentication، network exposure، فعال‌سازی Persons یا migration production نیست.

## وضعیت واقعی در زمان شروع
مخزن Md-gsm/MD-Gsm1؛ شاخه feature/persons-domain-core-c23bd326؛ PR #30 باید Open + Draft + Unmerged بماند. Head بررسی‌شده b3ed1ca37e15788e490c4c4fa904a61cae2f5579 بود و سه workflow مربوط به Persons Gates، CodeQL و Windows Preview Installer روی همان SHA موفق بودند. این نتیجه به commitهای بعدی تعمیم داده نمی‌شود. در کد فعلی سرویس اجرایی حساب/نشست، middleware احراز هویت، endpoint ورود و migration IdentityAccess وجود ندارد.

## مرحله 1 — سیاست مالک و موارد کاربرد (مستند شد)
مرجع: docs/security/identityaccess-owner-decisions-fa.md و ADR-0013.
- ورود username/password؛ email/mobile فقط تماس/بازیابی؛ self-registration ممنوع.
- تغییر رمز خود کاربر با رمز فعلی؛ reset مدیر با اجبار تغییر رمز در ورود بعدی؛ هر دو همه نشست‌های حساب را باطل می‌کنند.
- bootstrap admin/admin فقط local-only و یک‌باره؛ اولین ورود اجبار تغییر رمز و تکمیل پروفایل؛ تا آن زمان network listener و عملیات عادی/تجاری بسته.
- 5 شکست در 15 دقیقه => قفل 15 دقیقه؛ rate-limit مبدأ مکمل.
- نشست تا بازبودن برنامه با heartbeat/lease؛ پیشنهاد 30s/180s.
- سقف فعال کل سیستم قابل تنظیم؛ مقدار پیشنهادی 5 حساب متمایز؛ محدودیت ثابت per-account وجود ندارد.
- ده نقش مصوب، امکان چند نقش برای هر حساب، بدون ساخت کاربر خودکار برای نقش‌ها.
- بازیابی مدیر با email+mobile و کد اضطراری آفلاین یک‌بارمصرف.
- retention رویدادها هنوز مصوب نیست.
- سیاست فنی پیشنهادی username: NFC، حروف/ارقام Unicode، 3..64، case normalization لاتین؛ نیازمند تست.

## مرحله 2 — قرارداد امنیتی و عملیاتی (مستند شد)
- IdentityAccess مالک account/credential/lockout/session/role/permission/audit است.
- API enforcement boundary؛ Desktop untrusted؛ PostgreSQL authoritative؛ عدم اتصال مستقیم DB از کلاینت.
- opaque token با CSPRNG حداقل 256 بیت؛ فقط SHA-256 digest؛ raw token یک‌بار روی TLS معتبر و بدون log/persist.
- password hasher پشتیبانی‌شده، پارامتر نسخه‌دار و rehash؛ plaintext/reversible/custom crypto ممنوع.
- هر درخواست protected حساب فعال، session lease/revoke، credential version و permission را از PostgreSQL بررسی می‌کند؛ بدون cache اعتبار نشست.
- پاسخ ورود عمومی؛ rate limit حساب و مبدأ.
- bootstrap یک‌باره، local-only، اتمیک و fail-closed؛ تا تکمیل رمز/پروفایل network binding و عملیات تجاری غیرفعال.
- تغییر/reset رمز credential_version را افزایش و همه نشست‌ها را در همان تراکنش revoke می‌کند.
- audit append-only و بدون secret؛ retention/archival pending؛ TLS/firewall مطابق ADR-0012.

## مرحله 3 — طراحی منطقی دیتابیس (مستند شد، پیاده‌سازی نشد)
مرجع: docs/security/identityaccess-schema-design-fa.md.
جداول پیشنهادی: accounts, roles, permissions, account_roles, role_permissions, sessions, login_attempts, recovery_codes, security_audit_events, system_settings. طراحی شامل کلیدها، یکتایی login، FK/index، قفل، lease، credential version، digest بازیابی، audit و کنترل تراکنشی سقف کاربران است. هیچ SQL migration، DbSet، جدول live یا اجرای DbMigrator در این مرحله نیست.

## catalog نقش
super_admin، manager، receptionist، technician، warehouse، salesperson، purchaser، cashier، accountant، viewer. حدود اولیه در تصمیم‌نامه مالک آمده. نقش‌ها با permissionهای مستقل و بدون wildcard به عملیات نگاشت می‌شوند؛ scope تکنسین سمت API بر اساس تخصیص واقعی enforce می‌شود.

## موارد باز پیش از runtime
1. بازبینی امنیتی/معماری و مجوز رسمی G1 طبق ADR-0006.
2. ماتریس granular permission و نگاشت نهایی role-to-permission.
3. تست collision و پذیرش normalization نام کاربری.
4. پروتکل دقیق صدور/تحویل/چرخش recovery code و emergency recovery.
5. تصویب retention، archive integrity و سطح دسترسی audit.
6. طراحی استقرار bootstrap محلی، TLS، firewall و network fail-closed.
7. migration و تست concurrency/failure/rollback/idempotence روی PostgreSQL 18 disposable.
8. UI ورود، تغییر اجباری رمز/پروفایل، تغییر رمز خودکاربر، reset مدیر و recovery.
9. تست lockout/rate limit، سقف هم‌زمانی، lease، revoke و عدم افشای secret در log.

## عدم هدف
هیچ runtime endpoint، live account/session table، SQL migration، network listener، Persons integration، credential تولید یا G1 authorization با این اسناد فعال نمی‌شود. PR #30 باز + Draft + Unmerged؛ main بدون تغییر؛ `g1Authorized=false`.
