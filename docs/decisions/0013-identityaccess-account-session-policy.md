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
## الحاقیه تصمیم‌های مالک — 2026-11-05

این الحاقیه بندهای Session و Recovery قبلی را در موارد تعارض به‌روزرسانی می‌کند:
1. بازار هدف فعلی ایران است. ایمیل از mailbox اختصاصی روی دامنه محصول ارسال می‌شود؛ Mail Server مستقل داخل برنامه ساخته نمی‌شود و SMTP relay/Transactional Email قابل تعویض خواهد بود. vendor/SMTP host و DNS در زمان استقرار انتخاب می‌شود.
2. SMS provider هنوز انتخاب نشده؛ انتخاب و آزمون vendor مانع بازمانده فعال‌سازی Recovery است.
3. Email OTP TTL=10 دقیقه؛ SMS OTP TTL=5 دقیقه.
4. Recovery: حداکثر 5 تلاش ناموفق در هر challenge؛ سپس قفل 24 ساعته. resend حداقل هر 1 ساعت؛ سقف 1 ارسال در rolling hour و 5 ارسال در rolling 24h؛ عبور از سقف، قفل 24 ساعته ارسال/Recovery. این مقادیر lockout ورود عادی (5/15m و 15m) را تغییر نمی‌دهند.
5. Session: heartbeat=30s، lease=180s، absolute TTL=12h، حداکثر 2 نشست هم‌زمان هر حساب، حداکثر 5 حساب فعال متمایز کل سیستم. سقف کل با permission اختصاصی super_admin قابل تغییر و audit است.
6. تغییر رمز و غیرفعال‌سازی حساب موجب revoke فوری تمام نشست‌های حساب می‌شود؛ تغییر رمز credential_version را نیز افزایش می‌دهد. revoke باید در همان تراکنش تغییر وضعیت ثبت شود و API درخواست بعدی را با مراجعه به PostgreSQL رد کند.
7. absolute TTL از زمان ایجاد محاسبه می‌شود و با heartbeat تمدید نمی‌شود. کنترل concurrency سقف‌ها transactionally serialized است.

این تصمیم‌ها مجوز runtime/migration نیستند؛ قید G1 و بازبینی مستقل همچنان برقرار است.


## تعیین دامنه فعلی: عدم پیاده‌سازی پیامک — 2026-11-05
- بنا بر تصمیم مالک، در این مرحله هیچ SMS provider، SDK، API client، credential، template، configuration، background job یا تست وابسته به سرویس پیامک اضافه نمی‌شود.
- قابلیت SMS به مرحله/نسخه آینده موکول است و در این مرحله صرفاً به‌عنوان توسعه آتی مستند می‌شود؛ انتخاب vendor نیز اکنون لازم نیست.
- نبود SMS به معنی مجاز شدن بازیابی با ایمیل تنها نیست. تا زمان تصویب و پیاده‌سازی یک ترکیب عامل بازیابی امن و آزموده‌شده، Recovery اضطراری در محیط عملیاتی غیرفعال/غیرقابل دسترس می‌ماند. عامل offline recovery code نیز به‌تنهایی جایگزین عامل مستقل دوم نمی‌شود مگر آنکه مدل تهدید و سیاست مالک جداگانه تصویب شود.
- مقادیر TTL پیامک و محدودیت ارسال پیامک که پیش‌تر در سند آمده‌اند، مشخصات هدف آینده‌اند و در این مرحله هیچ رفتار اجرایی یا تست runtime برای SMS ایجاد نمی‌کنند.
- ایمیل اختصاصی نیز در این مرحله فقط معماری هدف است؛ SMTP relay واقعی و secretها تا انتخاب/پیکربندی عملیاتی وارد برنامه نمی‌شوند.


## رفع تعارض دامنه SMS — الحاقیه 2026-11-05

این الحاقیه بر هر عبارت قدیمی‌تر در همین ADR که «انتخاب SMS provider» را blocker لازم برای این فاز معرفی می‌کند، مقدم است: SMS عمداً به فاز آینده موکول شده و انتخاب vendor، افزودن SDK/client/credential/configuration/job یا اجرای تست SMS در این فاز لازم یا مجاز نیست.

با این حال، تعویق SMS به معنی فعال‌سازی Recovery با ایمیل تنها نیست. چون روش اجرایی و مصوب عامل مستقل جایگزین SMS هنوز وجود ندارد، Recovery باید غیرفعال بماند تا مالک و reviewer امنیتی ترکیب عوامل جایگزین را تصویب و آزمون کنند. مقادیر TTL و محدودیت‌های SMS صرفاً هدف طراحی آینده‌اند. SMTP واقعی نیز تا تصمیم deployment انتخاب نشده است.

این الحاقیه هیچ مجوز runtime، migration یا G1 ایجاد نمی‌کند؛ `g1Authorized=false` باقی است.


## تصمیم نهایی مالک — 2026-11-06 (مقدم بر الحاقیه‌های قدیمی‌تر)
- Recovery ایمیل‌تنها برای فاز فعلی تصویب شده است؛ این روش MFA یا دو عامل مستقل نیست و ریسک تصاحب mailbox باید در threat model و بازبینی مستقل ارزیابی شود.
- OTP ایمیل یک‌بارمصرف، TTL ده دقیقه و digest-only در DB است؛ حداکثر پنج تلاش ناموفق برای هر challenge و قفل 24 ساعته پس از حد تلاش؛ resend حداقل هر یک ساعت، حداکثر یک ارسال در rolling hour و پنج ارسال در rolling 24 hours؛ عبور از سقف موجب قفل 24 ساعته Recovery/ارسال می‌شود.
- پس از اثبات OTP، تغییر رمز، افزایش credential_version، revoke اتمیک همه نشست‌های حساب و الزام تغییر رمز در ورود بعدی لازم است.
- SMS فقط placeholder آینده و غیرفعال است؛ provider/SDK/credential/job/test پیامک در این فاز وجود ندارد و انتخاب vendor blocker فعلی نیست.
- این تصمیم سیاست محصول است، نه مجوز اجرای runtime یا G1؛ `g1Authorized=false` باقی است.
