# O-46 — تصمیم مالک دربارهٔ مسیر انحصاری ایجاد مالک اولیه

| فیلد | مقدار |
|---|---|
| شناسه | MDG-SECURITY-O46-BOOTSTRAP-OWNER-DECISION-001 |
| نسخه / تاریخ | 0.2 / 2026-11-24 |
| وضعیت | **OWNER-ACCEPTED CONSTRAINTS — TECHNICAL IMPLEMENTATION GATES OPEN** |
| تصمیم‌گیرنده | Product Owner و IdentityAccess Module Owner فعلی، همان شخص/حساب |
| بازبین مستقل | همان ناظر امنیت/معماری فعلی |
| مبنا | ADR-0019؛ O-46 Audit v0.4؛ Bootstrap Trust Proposal v0.1؛ IdentityAccess Baseline v1.0 |

## 1. تصمیم مالک

مالک، پیشنهادهای باقی‌ماندهٔ طرح O-46 Bootstrap Trust Boundary v0.1 را با اصلاح صریح بند تغییر اجباری رمز در اولین ورود، تأیید می‌کند. این سند مرجع ثبت تصمیم است و باید در خط‌مبنای IdentityAccess و طرح فنی Bootstrap منعکس شود.

### 1.1 تنها محل و مسیر مجاز ایجاد مالک اولیه

1. راه‌اندازی اولیه و ایجاد Product Owner فقط روی سیستم‌عامل میزبان سرور مرکزی انجام می‌شود.
2. تنها مسیر مجاز ایجاد مالک اولیه، Wizard اولیهٔ محلی روی همان سرور است.
3. هیچ مسیر جایگزین برای ایجاد، جایگزینی، انتقال یا ثبت مجدد Product Owner وجود ندارد؛ از جمله API/HTTP/TCP، کلاینت Windows، پنل مدیر، CLI، اسکریپت، migration، seed خودکار، ابزار پشتیبانی یا عملیات مستقیم و عادی دیتابیس.
4. نصب کلاینت روی رایانه‌های دیگر هرگز Wizard ایجاد مالک را اجرا نمی‌کند و تنها به سرور مرکزی از پیش راه‌اندازی‌شده متصل می‌شود.
5. قابلیت‌های عادی مدیریت حساب‌ها در آینده، در صورت تصویب و پیاده‌سازی، نمی‌توانند یک Account دیگر را به Product Owner تبدیل کنند یا binding مالک را بازنویسی کنند.
6. مالکیت Product Owner به همان AccountId اولیه و InstallationId همان استقرار متصل می‌شود؛ انتقال، جانشین، مالک دوم یا ثبت مجدد خودکار ممنوع است.
7. اگر حساب اصلی یا binding آن قابل بازیابی امن نباشد، عملیات ممتاز fail-closed می‌ماند. ایجاد مالک جایگزین، حتی توسط مدیر سیستم یا از طریق بازیابی عادی، مجاز نیست. بازیابی فقط همان AccountId است.

این تصمیم مانع ایجاد حساب‌های عادی دیگر در یک قابلیت مدیریت حساب مستقل و آتی نیست؛ اما آن قابلیت هیچ اختیار ایجاد یا تغییر Product Owner Binding ندارد.

### 1.2 اعتماد محلی و کانال ارتباطی

موارد زیر پذیرفته شدند:

- Wizard فقط در نشست محلی میزبان سرور اجرا شود.
- ارتباط Wizard با مؤلفهٔ راه‌اندازی سرور از طریق OS-local IPC باشد؛ برای میزبان Windows، Windows Named Pipe.
- Named Pipe باید اتصال راه دور را رد کند و ACL محدود به SID دقیق اپراتور محلیِ احراز‌شده و هویت‌های سرویس ضروری داشته باشد. SID از توکن معتبر سیستم‌عامل استخراج می‌شود، نه از ادعای caller.
- هیچ endpoint شبکه‌ای بدون احراز هویت، HTTP loopback setup route، remote pipe، انتقال secret از طریق فایل، command-line، environment variable یا لاگ مجاز نیست.
- مرز اعتماد این مدل شامل Windows OS Administrator/SYSTEM و PostgreSQL deployment/database-owner است. این طرح ادعا نمی‌کند که در برابر مهاجم دارای اختیار کامل روی این ریشه‌های اعتماد محافظت می‌کند.
- SID سیستم‌عامل فقط مجوز اجرای محلی Wizard است و هرگز هویت Product Owner یا منشأ OwnerAuthority محسوب نمی‌شود.

### 1.3 Challenge یک‌بارمصرف

- حداقل آنتروپی: ۲۵۶ بیت با CSPRNG معتبر.
- عمر challenge: حداکثر ۳۰ دقیقه از زمان صدور.
- challenge فقط یک بار و به همان Wizard محلی احراز‌شده تحویل می‌شود.
- فقط verifier/digest و metadata غیرمحرمانه در PostgreSQL ذخیره می‌شود؛ مقدار خام قابل بازیابی نیست.
- challenge به InstallationId، SetupOperationId، purpose و SID اپراتور محلی متصل است.
- claim با قفل/تراکنش پایگاه داده سریال می‌شود؛ در رقابت هم‌زمان دقیقاً یک درخواست موفق خواهد بود.
- replay، expiry، mismatch، مصرف مجدد، خطای DB و نتیجهٔ commit نامعلوم fail-closed هستند. نتیجهٔ نامعلوم با همان SetupOperationId reconcile می‌شود؛ حساب یا مالک جدید ایجاد نمی‌شود.
- challenge موفق، منقضی یا لغوشده قابل بازیابی یا استفادهٔ مجدد نیست.

### 1.4 InstallationId و حفظ پیوستگی

- InstallationId از نوع UUIDv7 و یکتا برای هر استقرار مرکزی است.
- شناسه در پایگاه مرکزی و تنظیمات پایدار و حفاظت‌شدهٔ میزبان سرور ثبت/قابل تطبیق است.
- نام رایانه، SID، نام دیتابیس، مسیر فایل، username یا نسخهٔ برنامه منشأ شناسه نیستند.
- reinstall، Repair، update، restore و تعویض فایل‌های اجرایی نباید InstallationId، AccountId یا Product Owner Binding را تغییر دهند.
- ناسازگاری DB و metadata محلی موجب توقف کنترل‌شده و بررسی/بازیابی می‌شود؛ سیستم نباید یکی را خودکار انتخاب کند یا Wizard مالک جدید نشان دهد.

### 1.5 اعتبار نهایی حساب و عدم اجبار تغییر رمز بعد از ورود

1. Wizard محلی اطلاعات نهایی پروفایل، username و password مالک را دریافت می‌کند. رمز نهایی باید همان‌جا طبق سیاست PasswordPolicy اعتبارسنجی و با password hasher پشتیبانی‌شده و نسخه‌دار ذخیره شود.
2. تکمیل موفق Wizard، مرحلهٔ نهایی‌سازی credential/profile اولیه است.
3. **مالک پس از اولین Login مجبور به تغییر username، password یا profile نیست.** اجبار تغییر رمز مالک پس از Login اول، به‌طور کامل برای این مسیر حذف می‌شود.
4. bootstrap برای مالک نباید صرفاً به‌دلیل اولیه‌بودن حساب، `must_change_password=true` تنظیم کند. ورود عادی پس از تکمیل و راستی‌آزمایی کامل Setup مجاز خواهد شد، مشروط به عبور از gateهای مستقل فعال‌سازی.
5. این تصمیم قابلیت عادی و اختیاری مالک برای تغییر رمز خود در آینده را حذف نمی‌کند؛ قواعد احراز رمز فعلی، password policy، افزایش credential_version و ابطال نشست‌ها همچنان برقرارند.
6. این استثنا فقط مربوط به حساب اولیهٔ Product Owner و روش Wizard است؛ قواعد reset/تغییر رمز حساب‌های عادی دیگر و الزام‌های امنیتی مربوط به آن‌ها بدون تغییر باقی می‌ماند.
7. هیچ رمز موقت، پیش‌فرض یا مشترکی ایجاد نمی‌شود.

### 1.6 ایجاد اتمیک و مالکیت داده

- IdentityAccess مالک انحصاری Account، credential و Product Owner Binding است.
- ایجاد AccountId، credential نهایی، binding غیرقابل‌انتقال به InstallationId و مصرف challenge در تراکنش محلی اتمیک IdentityAccess انجام می‌شود.
- Platform Setup Orchestrator فقط state فنی و SetupOperationId را نگه می‌دارد؛ دادهٔ Account یا binding را مالک نمی‌شود.
- Organization اطلاعات کارگاه را در تراکنش خودش می‌سازد.
- Accounting دورهٔ مالی اولیه را در تراکنش خودش می‌سازد.
- عملیات هر ماژول idempotent و قابل reconcile است؛ تراکنش توزیع‌شده و rollback مخرب دادهٔ قبلاً commit‌شده فرض نمی‌شود.
- role/permission catalog، bootstrap role grant، runtime database grants و seed همچنان gate جداگانه O46-G04 دارند؛ این تصمیم به‌تنهایی هیچ‌کدام را فعال نمی‌کند.

## 2. مواردی که هنوز باز هستند

این تصمیم، قیود محصول و مرز اعتماد را می‌بندد اما خودِ پیاده‌سازی را تأیید نمی‌کند. این موارد باز می‌مانند:

1. طراحی نهایی threat model برای Named Pipe/ACL، استخراج و ثبت SID اپراتور و مقاومت در برابر دسترسی محلی غیرمجاز.
2. طراحی schema/state persistence و رزرو رسمی migration از WG-0؛ هیچ شمارهٔ migration جدیدی با این سند رزرو نمی‌شود.
3. طراحی/بازبینی مرز deployment principal برای ایجاد Account و binding بدون اعطای INSERT به runtime role.
4. پیاده‌سازی و آزمون concurrency، challenge replay/expiry، unknown commit، secret hygiene و fail-closed listener gating.
5. هماهنگ‌سازی موفق/ناموفق مراحل IdentityAccess، Organization و Accounting با SetupOperationId.
6. آزمون‌های Gate D برای نصب تمیز، قطع Wizard، reinstall، update، کلاینت دوم، restore و uninstall.
7. بازبینی مستقل امنیتی/معماری روی SHA دقیق و release gateها.

Recovery ایمیل‌تنها همچنان مسیر جداگانه، غیرفعال تا تحقق gateهای خودش و بدون جایگزینی Product Owner است.

## 3. معیارهای پذیرش

| شناسه | معیار |
|---|---|
| O46-OWNER-01 | هیچ API، endpoint، CLI، seed، migration یا ابزار مدیریتی نمی‌تواند Product Owner جدید ایجاد یا binding را تغییر دهد. |
| O46-OWNER-02 | اجرای Wizard فقط روی میزبان سرور و از کانال local IPC مجاز است؛ remote/client invocation رد می‌شود. |
| O46-OWNER-03 | فقط SID محلی احراز‌شده در ACL مجاز است؛ SID ارسالی caller پذیرفته نمی‌شود. |
| O46-OWNER-04 | challenge حداقل ۲۵۶ بیت، عمر حداکثر ۳۰ دقیقه، verifier-only و single-use است. |
| O46-OWNER-05 | InstallationId از نوع UUIDv7 و در reinstall/update/restore پایدار است. |
| O46-OWNER-06 | Account، credential نهایی، binding، challenge consumption و audit intent در تراکنش IdentityAccess اتمیک‌اند. |
| O46-OWNER-07 | Wizard رمز نهایی مالک را اعتبارسنجی و ذخیره می‌کند؛ اولین Login مالک به اجبار تغییر رمز نیاز ندارد. |
| O46-OWNER-08 | reset/seed/role path نمی‌تواند Owner دیگری بسازد یا owner binding را منتقل کند. |
| O46-OWNER-09 | unknown commit، restart یا شکست مراحل setup موجب Account/Binding/Organization/FiscalPeriod تکراری یا Wizard مالک جدید نمی‌شود. |
| O46-OWNER-10 | هیچ secret خام در log، trace، crash dump، telemetry، support bundle یا CI artifact وجود ندارد. |

تمام معیارهای بالا در این سند **NOT RUN / NOT VERIFIED** هستند تا شواهد آزمون مستقل ارائه شود.

## 4. وضعیت Gate

- O46-G01: **OWNER DECISION RECORDED — DESIGN/THREAT REVIEW AND IMPLEMENTATION EVIDENCE OPEN**.
- O46-G02: Recovery entrypoint و اثبات ایمیل همچنان باز.
- O46-G03: SMTP، secret manager و کلید Recovery فقط برای Recovery مطرح‌اند و همچنان باز.
- O46-G04: schema/migration، bootstrap seed، deployment principal و role/permission boundary همچنان باز.
- O46-G05: آزمون PostgreSQL و race/idempotency شروع نشده.
- O46-G06: secret-sentinel و independent threat review انجام نشده.
- O46-G07: release authorization صادر نشده.

این تصمیم هیچ کد اجرایی، credential، حساب، role، permission، migration، database grant، endpoint، listener، Recovery یا feature activation را مجاز نمی‌کند.

**END — O-46 Bootstrap Owner Decision v0.1**


## Product Owner authority and recovery amendment — 2026-11-24

The Product Owner has designated **MDG-ARCH-MASTER-001 v3.0.4** as the sole current, accepted, official and governing Architecture/Accounting Master. Earlier v3.0.3-only authority statements in prior records are historical and superseded by baseline registration v1.3 and project authority control v1.0.

The Product Owner's recovery decision is now explicit:
1. The initial Product Owner identity and binding are immutable. Recovery may restore only the original AccountId and may not create a second owner, replacement, transfer or successor.
2. Owner recovery has two alternative methods: (a) a recovery code chosen by the Product Owner in the initial Setup Wizard, with only a securely salted/versioned verifier/hash persisted and bounded attempts/audit; or (b) a single-use Email OTP sent to the verified owner email.
3. The Product Owner's recovery does not require approval by the Product Owner account itself. Recovery of ordinary user accounts remains under Product Owner authority.
4. The product uses one centrally managed Google/Gmail sender account owned by MD-Gsm. Store customers enter only the recipient email address and do not configure SMTP, sender mailbox credentials or OAuth. The central sender account and authorization are created/configured during final release preparation, not customer setup.
5. Sender credentials/tokens must not be embedded in distributed binaries, source control or store databases. A protected central sending component/service, secure transport, OAuth/API authorization, token custody, delivery monitoring, abuse controls and privacy/retention rules remain design, implementation and test gates. The owner-defined recovery code remains independent of email-service availability.
6. This policy decision does not authorize a live Gmail account, OAuth client/secret, production sender service, runtime endpoint, database migration, grant, activation or Go-Live.

This amendment changes owner-policy and architecture authority only. All implementation, schema reservation, technical threat review, exact-head tests, independent review and release gates remain independently open unless separately evidenced.

**END — MDG-SECURITY-O46-BOOTSTRAP-OWNER-DECISION-001_v0.2.md**
