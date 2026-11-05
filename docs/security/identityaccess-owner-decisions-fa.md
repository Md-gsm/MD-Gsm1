# تصمیم‌های مالک محصول برای IdentityAccess
تاریخ 2026-11-04 | تصمیم‌های محصول ثبت‌شده؛ جزئیات فنی نیازمند تست و بازبینی.

- ورود فقط username/password؛ email/mobile فقط تماس/بازیابی.
- قرارداد پیشنهادی username: NFC، بدون trim خاموش، 3..64 scalar، Unicode L*/Nd، حفظ ارقام هر script، Latin case-fold نسخه‌دار، رد mixed scripts؛ oracle Python جایگزین تست .NET نیست.
- کاربر رمز خودش را با رمز فعلی تغییر می‌دهد؛ reset دیگران فقط مدیر؛ reset اجبار تغییر در ورود بعدی دارد. هر دو همه نشست‌های حساب را revoke می‌کنند.
- password hasher پشتیبانی‌شده و versioned؛ plaintext/reversible/custom crypto ممنوع.
- bootstrap admin/admin فقط یک‌باره و local-only؛ تا تغییر رمز و تکمیل پروفایل، network listener و عملیات عادی fail-closed؛ credential خام در source/installer/config/migration/log/telemetry ممنوع.
- نشست تا بازبودن برنامه؛ سقف قابل تنظیم (پیشنهاد آغازین 5 حساب متمایز)، heartbeat/lease پیشنهادی 30s/180s؛ PostgreSQL authoritative و بدون cache محلی.
- 5 شکست در 15 دقیقه => قفل 15 دقیقه؛ source rate limit مکمل؛ پاسخ login عمومی و بدون account enumeration.
- ده نقش: super_admin, manager, receptionist, technician, warehouse, salesperson, purchaser, cashier, accountant, viewer؛ چندنقشی؛ نقش‌ها user خودکار نمی‌سازند.
- ماتریس granular، retention و recovery در `identityaccess-five-open-items-fa.md` پیشنهاد شده‌اند و تا تصویب/review اجرایی نیستند.
- audit secret-free و append-only.

عدم مجوز: تا review امنیتی/معماری و مجوز رسمی G1، `g1Authorized=false`؛ بدون login endpoint، migration عملیاتی، listener شبکه یا Persons write.
## تصمیم‌های تکمیلی مالک محصول — 2026-11-05

### Recovery و ارائه‌دهندگان
- بازار هدف فعلی: ایران.
- ایمیل بازیابی از mailbox اختصاصی روی دامنه متعلق به محصول (برای نمونه no-reply@<product-domain>) ارسال می‌شود؛ Mail Server مستقل داخل برنامه ساخته نمی‌شود.
- ارسال ایمیل از طریق SMTP relay یا سرویس Transactional Email قابل تعویض انجام می‌شود؛ نام سرویس‌دهنده/میزبان SMTP، دامنه واقعی و DNSهای SPF/DKIM/DMARC در مرحله استقرار تعیین می‌شوند. هیچ credential واقعی در مخزن ثبت نمی‌شود.
- ارائه‌دهنده پیامک هنوز انتخاب نشده است؛ adapter باید provider-agnostic باشد و تا انتخاب و آزمون عملی ارائه‌دهنده، Recovery عملیاتی نمی‌شود.
- TTL کد ایمیل: 10 دقیقه؛ TTL کد پیامک: 5 دقیقه.
- برای هر challenge حداکثر 5 تلاش ناموفق مجاز است؛ پس از رسیدن به حد، Recovery به مدت 24 ساعت قفل می‌شود.
- ارسال مجدد برای هر کانال حداقل با فاصله 1 ساعت مجاز است؛ حداکثر 1 ارسال در هر بازه لغزان 1 ساعته و 5 ارسال در هر بازه لغزان 24 ساعته. عبور از هر سقف موجب قفل ارسال/Recovery به مدت 24 ساعت می‌شود.
- شمارنده‌ها باید اتمیک و سمت سرور باشند؛ محدودیت per-account/channel و per-source اعمال شود. قفل مبدأ به‌تنهایی نباید ابزار آسان DoS علیه قربانی باشد.
- ارسال مجدد، proof قبلی همان کانال را باطل می‌کند؛ timeout یا نتیجه مبهم provider proof معتبر تولید نمی‌کند.
- قفل 24 ساعته فقط برای Recovery/OTP است و lockout ورود عادی 5 شکست در 15 دقیقه و قفل 15 دقیقه‌ای را تغییر نمی‌دهد.

### سیاست نشست مصوب
- heartbeat: 30 ثانیه؛ lease: 180 ثانیه؛ عمر مطلق نشست: 12 ساعت از زمان ایجاد، بدون تمدید با heartbeat.
- حداکثر 2 نشست/دستگاه هم‌زمان برای هر حساب؛ شناسه client instance تصادفی است و hardware fingerprint محسوب نمی‌شود.
- حداکثر 5 حساب فعال متمایز در کل سیستم؛ این سقف توسط super_admin با permission اختصاصی و audit قابل تغییر است.
- تغییر رمز و غیرفعال‌سازی حساب باید در تراکنش اتمیک تمام نشست‌های حساب را فوراً revoke کند؛ در تغییر رمز credential_version نیز افزایش یابد.
- درخواست protected بعدی باید با مراجعه به PostgreSQL رد شود؛ cache محلی مجاز نیست.
- تعویض نشست در زمان پر بودن سقف باید صریح و audit شود؛ اخراج خاموش نشست قدیمی ممنوع.

این تصمیم‌ها قرارداد محصول/طراحی‌اند؛ پیاده‌سازی همچنان منوط به G1 مستقل و مجوز رسمی است.
