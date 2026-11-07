# ADR-0013 — سیاست حساب، اعتبارنامه، نشست و bootstrap در IdentityAccess
- وضعیت: تصمیم‌های مالک ثبت شد؛ جزئیات اجرایی نیازمند بازبینی امنیتی.
- تاریخ: 2026-11-04.
- مرتبط: ADR-0006 و ADR-0012.

## زمینه
MD-Gsm1 چندکاربره و چندکلاینت Windows با API مرکزی و PostgreSQL است. Desktop غیرقابل‌اعتماد و API مرز enforcement است. ثبت‌نام عمومی ممنوع؛ PostgreSQL مرجع authoritative حساب و نشست است.

## تصمیم‌ها
1. ورود فقط username/password؛ ایمیل و موبایل فقط تماس و بازیابی.
2. کاربر رمز خود را با رمز فعلی عوض می‌کند. مدیر مجاز می‌تواند رمز فراموش‌شده کاربر را reset کند؛ reset تغییر اجباری در ورود بعدی می‌خواهد.
3. تغییر یا reset رمز، credential_version را افزایش داده و تمام نشست‌های حساب را revoke می‌کند.
4. مدیر اولیه username=admin و bootstrap password=admin دارد، فقط در setup یک‌باره local-only. اولین ورود فقط تغییر اجباری رمز و تکمیل پروفایل را می‌پذیرد؛ تا تکمیل setup، network listener و عملیات تجاری fail-closed هستند. credential ثابت در source/config/installer/migration/log/telemetry ممنوع است.
5. 5 تلاش ناموفق در 15 دقیقه باعث قفل 15 دقیقه‌ای می‌شود؛ rate-limit مبدأ مستقل تکمیلی است؛ پاسخ ورود عمومی و بدون account enumeration است.
6. نشست تا بازبودن برنامه، با heartbeat/lease سمت سرور. پیشنهاد آغازین heartbeat=30s و lease=180s. سقف فعال کل سیستم قابل تنظیم، مقدار آغازین پیشنهادی 5 حساب فعال متمایز؛ محدودیت ثابت per-account وجود ندارد.
7. catalog اولیه: super_admin, manager, receptionist, technician, warehouse, salesperson, purchaser, cashier, accountant, viewer. هر حساب می‌تواند چند نقش داشته باشد؛ تعریف نقش به معنی ساخت user نیست؛ مجوزها granular و deny-by-default هستند.
8. فقط bootstrap super_admin در setup یک‌باره به‌طور خودکار انتساب می‌گیرد؛ سایر کاربران عملیاتی بعداً توسط مدیر ساخته می‌شوند.
9. بازیابی مدیر با تأیید ایمیل و موبایل؛ کدهای اضطراری یک‌بارمصرف و آفلاین؛ پروتکل نهایی نیازمند review امنیتی.
10. رویدادهای امنیتی audit می‌شوند؛ retention دقیق هنوز مصوب نیست.

## پیامدها و کنترل‌ها
- پیشنهاد username: Unicode NFC، حروف و ارقام Unicode، طول 3..64، case normalization برای لاتین؛ تست collision و سازگاری لازم.
- Password hasher پشتیبانی‌شده و versioned؛ plaintext/reversible/custom crypto ممنوع.
- توکن opaque با CSPRNG حداقل 256 بیت؛ فقط SHA-256 digest ذخیره؛ raw token یک‌بار روی TLS معتبر تحویل و هرگز log/persist نمی‌شود.
- هر درخواست protected، وضعیت حساب، نشست، lease، revoke، credential version و permission را از PostgreSQL می‌سنجد؛ cache محلی اعتبار نشست ممنوع.
- سقف هم‌زمانی باید تراکنشی enforce شود؛ تغییر/reset رمز و غیرفعال‌سازی حساب فوراً اثر کند.
- TLS، firewall و topology مطابق ADR-0012.

## عدم اعطای مجوز
این ADR گیت ADR-0006 را لغو نمی‌کند. اجرای runtime، SQL migration، credential تولید، network listener، فعال‌سازی Persons، تغییر production DB یا بستن G1 را مجاز نمی‌کند. `g1Authorized=false` تا بازبینی مستقل امنیت/معماری و مجوز رسمی باقی است.
