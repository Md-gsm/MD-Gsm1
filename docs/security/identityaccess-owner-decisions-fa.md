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
