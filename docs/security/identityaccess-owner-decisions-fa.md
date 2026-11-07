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

عدم مجوز: تا review امنیتی/معماری و مجوز رسمی G1، `g1Authorized=false`؛ وجود مسیر login در کد به معنی فعال‌بودن آن نیست (feature flag به‌صورت پیش‌فرض خاموش است). هیچ production listener، migration روی دیتابیس واقعی یا Persons write مجاز نشده است.


## یادداشت وضعیت اجرا — 2026-11-07

این سند مرجع تصمیم‌های محصول 2026-11-04 است، نه فهرست وضعیت endpointها. در main مسیرهای HTTP login و password change در کد و تست یکپارچه وجود دارند، اما HTTP login به‌صورت پیش‌فرض خاموش است و production bootstrap/recovery/listener هنوز پذیرفته نشده‌اند. جزئیات جاری در `docs/security/identityaccess-five-open-items-fa.md` و `docs/persons/G1_PREPARATION_AND_ENTRY_CHECKLIST_FA.md` نگهداری می‌شود.
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


## تعیین دامنه فعلی: عدم پیاده‌سازی پیامک — 2026-11-05
- بنا بر تصمیم مالک، در این مرحله هیچ SMS provider، SDK، API client، credential، template، configuration، background job یا تست وابسته به سرویس پیامک اضافه نمی‌شود.
- قابلیت SMS به مرحله/نسخه آینده موکول است و در این مرحله صرفاً به‌عنوان توسعه آتی مستند می‌شود؛ انتخاب vendor نیز اکنون لازم نیست.
- نبود SMS به معنی مجاز شدن بازیابی با ایمیل تنها نیست. تا زمان تصویب و پیاده‌سازی یک ترکیب عامل بازیابی امن و آزموده‌شده، Recovery اضطراری در محیط عملیاتی غیرفعال/غیرقابل دسترس می‌ماند. عامل offline recovery code نیز به‌تنهایی جایگزین عامل مستقل دوم نمی‌شود مگر آنکه مدل تهدید و سیاست مالک جداگانه تصویب شود.
- مقادیر TTL پیامک و محدودیت ارسال پیامک که پیش‌تر در سند آمده‌اند، مشخصات هدف آینده‌اند و در این مرحله هیچ رفتار اجرایی یا تست runtime برای SMS ایجاد نمی‌کنند.
- ایمیل اختصاصی نیز در این مرحله فقط معماری هدف است؛ SMTP relay واقعی و secretها تا انتخاب/پیکربندی عملیاتی وارد برنامه نمی‌شوند.


## اصلاح و تکمیل تصمیم مالک درباره Recovery و SMS — 2026-11-05

این تصمیم جدید مالک، عبارت‌های متناقض قبلی همین سند درباره ممنوعیت Recovery ایمیل‌تنها را برای فاز فعلی جایگزین می‌کند:

- **ایمیل به‌تنهایی برای Recovery در فاز فعلی کافی و مورد تأیید مالک محصول است.** Recovery ایمیلی می‌تواند با OTP یک‌بارمصرف 10 دقیقه‌ای انجام شود؛ پس از اثبات ایمیل، تغییر رمز باید با password hasher استاندارد و versioned انجام شود، همه نشست‌های حساب در همان تراکنش revoke شوند، credential_version افزایش یابد و ورود بعدی تغییر رمز را اجباری کند.
- این انتخاب، پذیرش آگاهانه ریسک وابستگی Recovery به امنیت mailbox است؛ reviewer امنیتی باید این ریسک، راه‌های تصاحب mailbox، phishing، SIM-swap غیرمستقیم/بازیابی حساب ایمیل، و کنترل‌های کاهش ریسک را ارزیابی و ثبت کند. این ریسک نباید به‌عنوان «دو عامل مستقل» توصیف شود.
- SMS حذف نشده است؛ جایگاه/adapter/configuration آن برای توسعه آینده به‌صورت placeholder خالی و غیرفعال باقی می‌ماند. در این فاز هیچ provider، SDK، credential یا ارسال SMS فعال نمی‌شود. خالی‌بودن provider مانع Recovery ایمیلی فعلی نیست.
- فعال‌سازی آینده SMS باید بدون تغییر قرارداد Recovery ایمیلی و از طریق adapter/provider قابل‌تعویض انجام شود؛ انتخاب provider و secretها در فاز آینده.
- هیچ رمز ثابت، رمز مشترک، master password، backdoor یا رمز قابل بازیابی ذخیره‌شده مجاز نیست. OTP ایمیل تصادفی، کوتاه‌عمر، یک‌بارمصرف، digest-only در DB، rate-limited و secret-free در log است.
- bootstrap اولیه `admin/admin` فقط یک‌باره و local-only طبق تصمیم bootstrap قبلی باقی می‌ماند؛ این credential ثابت دائمی یا روش Recovery نیست و باید پیش از هر عملیات عادی/شبکه‌ای تغییر کند.

## مسیر تصویب ماتریس مجوزها

نسخه پیشنهادی granular برای تصمیم مالک در `identityaccess-permission-matrix-approval-fa.md` آماده شده است. تا ثبت APPROVE یا APPROVE WITH CHANGES، هیچ seed یا runtime policy از آن ساخته نمی‌شود.


## ثبت تصمیم‌های نهایی مالک — 2026-11-06
1. ماتریس مجوزها تصویب شد: grantهای علامت‌خورده، Deny خانه‌های خالی، deny-by-default، scope مستقل، union صریح چندنقشی، محدودیت technician به self/assigned، separation-of-duties و audit. این تصمیم مجوز seed/runtime نیست.
2. Recovery ایمیل‌تنها تصویب شد: OTP ده‌دقیقه‌ای، یک‌بارمصرف و digest-only؛ پس از اثبات، تغییر رمز، revoke اتمیک همه نشست‌ها، افزایش credential_version و اجبار تغییر رمز در ورود بعدی. این روش MFA نیست و ریسک mailbox باید مستقل review شود.
3. SMS فاز آینده است؛ placeholder خالی و غیرفعال می‌ماند و انتخاب provider blocker فعلی نیست.
4. Retention امنیتی نیازمند تصویب/بررسی حقوقی؛ thresholdهای مالی باید تعیین شوند؛ مالک canonical branch/warehouse و assignment باید با شواهد کد/schema معرفی شود.
5. این تصمیم‌ها تأیید امنیتی مستقل یا مجوز G1 نیستند؛ reviewer، یافته‌ها، امضا و تصمیم AUTHORIZE G1 هنوز لازم‌اند.


## تصمیم‌های نهایی مالک — 2026-11-06/07
- ماتریس granular مجوز با deny-by-default، منع wildcard، اجتماع مجوزهای صریح چندنقشی، scope مستقل سمت API، محدودیت تکنسین به self/assigned و جداسازی وظایف تصویب شد. خانه خالی Deny است. تصویب policy به معنی seed یا فعال‌سازی runtime نیست.
- Recovery ایمیل‌تنها تصویب شد: OTP یک‌بارمصرف 10 دقیقه‌ای، digest-only، حداکثر 5 تلاش و قفل 24 ساعته؛ resend حداقل هر 1 ساعت، حداکثر 1 بار در rolling hour و 5 بار در rolling 24h؛ نقض سقف قفل 24 ساعته دارد. پس از اثبات OTP: تغییر رمز، افزایش credential_version، revoke اتمیک همه نشست‌ها و الزام تغییر رمز در ورود بعدی.
- ایمیل‌تنها MFA نیست؛ ریسک mailbox compromise باید مستقل review شود. SMS در این فاز placeholder غیرفعال است؛ هیچ provider/SDK/credential/send/test وجود ندارد. offline code عامل اجباری یا bypass نیست.
- سیاست نشست: برنامه باز + heartbeat/lease؛ سقف آغازین 5 حساب فعال متمایز قابل تنظیم؛ 30s/180s فقط liveness است؛ absolute TTL یا سقف 2 نشست/account تصویب نشده.
- retention امنیتی، مالک canonical داده‌های Branch/Warehouse/assignment، threshold مالی و review مستقل هنوز بازند. `g1Authorized=false`.
