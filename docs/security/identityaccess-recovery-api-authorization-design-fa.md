# طرح امنیتی Recovery اضطراری و API Authorization

- تاریخ: 2026-11-05
- وضعیت: طراحی و معیار پذیرش؛ غیرعملیاتی و بدون مجوز G1
- بازنگری وضعیت تصمیم‌ها: 2026-11-17
- مبنا: ADR-0006، ADR-0012، ADR-0013 و پنج موضوع باز IdentityAccess

## 0. مرز امنیتی

> **تصمیم جاری مالک:** Recovery ایمیل‌تنها با OTP ده‌دقیقه‌ای پذیرفته شده و MFA نیست؛ SMS و offline recovery code عامل اجباری این فاز نیستند. نقطه شروع Recovery (شبکه‌ای یا کنسول محلی مورد اعتماد) و ثبت/اثبات ایمیل super_admin هنوز تعیین نشده است. هر مدل قدیمی سه‌عاملی در این سند تاریخی و superseded است. Session جاری نیز بدون absolute TTL و بدون سقف per-account است؛ فقط heartbeat=30s، lease=180s و سقف 5 حساب فعال متمایز.

این سند به‌تنهایی مجوز ایجاد login/recovery endpoint، middleware فعال، migration IdentityAccess، network listener یا تغییر `g1Authorized=false` نیست. PostgreSQL مرجع authoritative است. Desktop غیرقابل‌اعتماد است. هر خطای DB، provider، پیکربندی یا policy در مسیر حساس باید fail-closed باشد. raw password، bearer token، recovery code، OTP و PII غیرضروری نباید در DB/log/trace/telemetry/CI artifact ثبت شوند. هیچ backdoor، master password یا مسیر bypass مجاز نیست.

## 1. Recovery اضطراری Super Admin

### 1.1 هدف و مرز

مالک محصول Recovery ایمیل‌تنها را برای فاز فعلی پذیرفته است. اثبات ایمیل با OTP یک‌بارمصرف 10 دقیقه‌ای انجام می‌شود؛ این روش MFA یا دو عامل مستقل نیست و ریسک وابستگی به امنیت mailbox باید در threat model و بازبینی مستقل ارزیابی شود.

**هنوز تصمیم/طراحی نشده:** نقطه شروع flow (endpoint شبکه‌ای یا کنسول محلی مورد اعتماد)، مرز اعتماد کنسول در صورت انتخاب، و فرایند ثبت و اثبات ایمیل حساب super_admin. این ابهام‌ها مانع ساخت یا فعال‌سازی Recovery runtime هستند تا تصمیم و کنترل‌های لازم ثبت شوند.

SMS در این فاز غیرفعال و خارج از دامنه است. Offline recovery code عامل اجباری یا bypass نیست. هیچ fallback دستی، backdoor یا master password مجاز نیست.

### 1.2 دارایی‌ها و اهداف امنیتی

| دارایی | الزام |
|---|---|
| حساب Super Admin | فقط از طریق flow مصوب ایمیل‌تنها؛ نقطه شروع هنوز تعیین نشده |
| OTP ایمیل | CSPRNG، یک‌بارمصرف، TTL=10 دقیقه، فقط digest در DB |
| challenge | کوتاه‌عمر، account-bound، یک‌بارمصرف و غیرقابل replay |
| نشست‌ها | revoke کامل و فوری پس از Recovery |
| credential | افزایش credential_version و must_change_password=true |
| audit | metadata allow-list؛ بدون secret و PII غیرضروری |
| SMS/offline code | خارج از دامنه/غیراجباری در این فاز |

### 1.3 تهدیدها و مرز اعتماد

- compromise حساب OS یا process محلی؛
- compromise ایمیل، SIM-swap، forwarding یا تأخیر provider؛
- brute force، flood و DoS کانال ارسال؛
- replay و race بین challengeها یا مصرف هم‌زمان code؛
- قطع PostgreSQL، failover و نتیجه commit نامعلوم؛
- افشای secret از log، trace، crash dump، telemetry یا CI artifact؛
- سوءاستفاده اپراتور داخلی برای bypass یا recovery بدون audit.

### 1.4 State machine

حالت‌ها: Created، EmailVerified، Completed، Expired، Locked، Cancelled، FailedClosed.

- فقط تأیید OTP ایمیل معتبر و bind‌شده به همان account/challenge اجازه انتقال به EmailVerified می‌دهد.
- تکمیل فقط با مصرف اتمیک challenge و ثبت تغییر credential در تراکنش نهایی مجاز است.
- challenge منقضی، مصرف‌شده، لغوشده یا متعلق به حساب دیگر رد می‌شود.
- هر خطای DB/provider یا نتیجه commit نامعلوم موفقیت محسوب نمی‌شود؛ وضعیت از PostgreSQL authoritative reconcile می‌شود.

### 1.5 داده و TTL

مدل منطقی challenge شامل شناسه تصادفی، account_id، زمان ایجاد/انقضا، state، شمارنده تلاش و correlation غیرحساس است. OTP/nonce با CSPRNG تولید و فقط digest آن ذخیره شود. challenge باید به عملیات محلی و account bind شود؛ ID ارسالی کلاینت به‌تنهایی اثبات نیست.

تصمیم‌های مالک درباره TTL/تلاش/ارسال در الحاقیه 2026-11-05 ثبت شده‌اند؛ بااین‌حال هیچ Recovery runtime در این مرحله مجاز نیست. SMS صراحتاً به فاز آینده موکول شده و provider آن اکنون در دامنه این PR نیست. SMTP واقعی و الگوریتم digest/pepper و مدیریت کلید تا طراحی/بازبینی/استقرار امن باز می‌مانند.

### 1.6 تراکنش نهایی

در یک تراکنش PostgreSQL و با row lock روی challenge، code و account:
1. مالکیت، کامل‌بودن و عدم انقضای challenge بررسی شود.
2. code معتبر، استفاده‌نشده و revoke‌نشده بررسی شود.
3. code یک‌بارمصرف علامت used شود.
4. credential_version افزایش یابد.
5. تمام نشست‌های حساب revoke شوند.
6. must_change_password=true شود.
7. audit event بدون secret ثبت شود.
8. challenge به Completed برسد.

هر شکست باید کل تراکنش را rollback کند. پاسخ موفق فقط پس از commit قطعی. در نتیجه commit نامعلوم، success اعلام نشود و وضعیت بعدی فقط از DB authoritative reconcile شود.

### 1.7 Audit

ثبت مجاز: آغاز، ارسال/عدم ارسال عامل، تأیید/رد، rate-limit، lock، تکمیل، لغو، خطای وابستگی و revoke. فقط شناسه داخلی actor/subject، زمان UTC، نوع رخداد، outcome، correlation ID و reason code غیرحساس. ایمیل، شماره موبایل، OTP، recovery code، digest secret، token و request body ممنوع.

### 1.8 ماتریس آزمون Recovery

| ID | سناریو | انتظار |
|---|---|---|
| REC-01 | شروع از route شبکه‌ای پیش از تصویب entrypoint | رد/غیرفعال؛ بدون mutation |
| REC-02 | شروع از کنسول محلی بدون مرز اعتماد مصوب | رد/غیرفعال؛ بدون mutation |
| REC-03 | OTP ایمیل نامعتبر یا منقضی | رد |
| REC-04 | OTP متعلق به account/challenge دیگر | رد |
| REC-05 | replay یا مصرف هم‌زمان یک OTP | دقیقاً یک winner |
| REC-06 | بیش از 5 تلاش ناموفق | قفل 24 ساعته |
| REC-07 | resend در cooldown یا عبور از سقف rolling | رد و اعمال قفل مصوب |
| REC-08 | resend موفق | challenge قبلی revoke شود |
| REC-09 | raw OTP/token/PII غیرضروری در DB/log/artifact | صفر occurrence |
| REC-10 | خطا پیش از commit | rollback کامل |
| REC-11 | نتیجه commit نامعلوم | عدم اعلام موفقیت کاذب؛ reconcile از DB |
| REC-12 | Recovery موفق | revoke همه sessionها، credential_version++، must_change_password |
| REC-13 | DB/provider unavailable | fail-closed |
| REC-14 | brute force/flood | rate-limit و audit بدون account enumeration |
| REC-15 | حساب disabled یا غیرمجاز | رد طبق policy مصوب |

### 1.9 تصمیم‌های باز

SMTP relay واقعی، digest/pepper و key rotation، تعداد/تحویل امن codeهای آفلاین، تعریف کنسول محلی مورد اعتماد، فرایند دو نفره و incident response باید تعیین و review شوند. SMS provider عمداً به فاز آینده موکول شده است. تا تکمیل شروط امنیتی هیچ مسیر recovery عملیاتی ساخته نشود.

**خروجی مرحله ۱:** threat model و test acceptance مستند شد؛ runtime عمداً ساخته نشد.

## 2. API Authorization — قرارداد

ماتریس granular در 2026-11-06 توسط مالک تصویب شده است؛ خانه خالی Deny و deny-by-default. بااین‌حال seed و runtime policy تا gateهای پیاده‌سازی و G1 مجاز نیستند.

- permission granular، صریح، بدون wildcard و deny-by-default.
- اجتماع چندنقشی فقط union مجوزهای صریح.
- مجوز read/create/update/delete/approve/post/print/export مستقل.
- scope تکنسین از رابطه تخصیص authoritative در ماژول مالک، در query/service سمت API اعمال شود؛ شناسه client منبع scope نیست.
- کنترل رکوردی برای جلوگیری از IDOR و عدم افشای وجود رکورد غیرمجاز.
- dashboard/widget/report/search/print/export/API باید policy یکسان را اعمال کنند.
- خطای DB، identity، session، permission یا resource context => Deny.

Policy evaluator (قرارداد): ورودی principal/session معتبر، permission key دقیق و scope context؛ خروجی Allow/Deny با reason code داخلی غیرحساس. unknown permission، wildcard، نبود context و هر خطا Deny.

آزمون‌ها: unauthenticated=401؛ session expired/revoked یا account disabled=401؛ هویت معتبر بدون permission=403؛ scope نامعتبر=403 یا عدم افشای 404 طبق قرارداد؛ تکنسین فقط رکوردهای تخصیص‌یافته؛ دستکاری ID رد؛ چندنقشی فقط مجوز صریح؛ widget مخفی ولی API مستقیم Deny؛ print/export/search/dashboard کنترل شوند؛ DB/policy failure fail-closed؛ cross-account/cross-scope IDOR tests.

## 3. Authentication و Session — قرارداد

- username/password تنها روش ورود؛ email/mobile فقط contact/recovery.
- password hashing پشتیبانی‌شده و versioned؛ plaintext/reversible/custom crypto ممنوع.
- opaque token با CSPRNG حداقل 256 بیت؛ فقط SHA-256 digest ذخیره.
- هر درخواست protected وضعیت account/session/lease/revoke/credential_version را از PostgreSQL authoritative بررسی کند؛ cache محلی ممنوع.
- 5 شکست در 15 دقیقه => lockout 15 دقیقه؛ source rate limit مستقل مکمل؛ پاسخ عمومی بدون account enumeration.
- تغییر/reset رمز در همان تراکنش credential_version را افزایش و همه نشست‌ها را revoke کند.
- heartbeat=30s، lease=180s و سقف 5 حساب فعال متمایز، تصمیم نهایی مالک‌اند؛ runtime موجود فقط تا حدی پیاده‌سازی شده و پذیرش کامل نیازمند تست است.
- bootstrap حساب اولیه باید installer-created و first-login forced-change باشد؛ روش امن provisioning و تعارض عبارت تاریخی admin/admin هنوز باز است. تا رفع آن، listener و عملیات عادی بسته.
- raw token در DB/log/telemetry/artifact صفر؛ revoked token در درخواست بعدی رد؛ disabled account و credential version قدیمی رد؛ lockout و session cap در concurrency درست enforce شوند.

## 4. اتصال API و E2E — وضعیت واقعی main و معیار آینده

در main فعلی، routeهای login و password change وجود دارند؛ HTTP login با feature flag پیش‌فرض خاموش است. middleware فعلی فقط مسیرهای Persons و password change را با نشست PostgreSQL احراز هویت می‌کند. Recovery endpoint/orchestration، logout و heartbeat مستقل، و authorization metadata/evaluator سراسری هنوز تکمیل نشده‌اند؛ جزئیات در رجیستر WG-2 ثبت شده است.

پس از مجوز رسمی G1:
1. authentication به‌صورت پیش‌فرض برای تمام routeهای application؛ فقط health/liveness صریحاً anonymous.
2. هر endpoint دارای permission metadata دقیق و policy evaluator سمت سرور.
3. CI route فاقد authorization metadata را رد کند.
4. system/info طبق تصمیم مالک عمومی یا protected شود.
5. readiness/live اطلاعات حساس و جزئیات اتصال را افشا نکنند.
6. TLS و bind production fail-closed؛ wildcard و HTTP غیر-loopback ممنوع.
7. integration tests با PostgreSQL disposable؛ fake auth فقط در تست.
8. E2E برای 401/403/404، IDOR، revoke، disabled account، expired lease، invalid permission، DB outage، invalid TLS و چند client.
9. هیچ secret واقعی یا raw token در fixture/artifact.

## Definition of Done و G1

- threat model و REC-01..20 توسط reviewer بررسی شود.
- سیاست ایمیل‌تنها/TTL/تلاش/resend تصویب شده است؛ نقطه شروع Recovery، SMTP، rate-limit runtime و provisioning امن هنوز باید تعیین/تکمیل شوند.
- permission matrix توسط مالک تصویب شده است؛ seed، scope runtime و golden tests هنوز باید تکمیل شوند.
- session/lockout قراردادها با ADRها تطبیق داده شوند.
- reviewer مستقل روی SHA مشخص نتیجه/یافته/امضا ثبت کند.
- مالک تصمیم رسمی G1 را ثبت کند.

تا آن زمان `g1Authorized=false`؛ migration/runtime فعال نیست؛ recovery/login/auth middleware به production host وصل نمی‌شود؛ PRهای قبلی و main تغییر نمی‌کنند. این سند تأیید امنیتی یا بستن G1 نیست.

 
## 5. تکمیل الزامات بازبینی سخت‌گیرانه (Revision 2)

این بخش ابهام‌های بازبینی را به قرارداد قابل ارزیابی تبدیل می‌کند؛ هیچ مقدار پیشنهادی را به تصمیم مالک یا مقدار runtime تبدیل نمی‌کند.

### 5.1 مدل Challenge و اثبات کانال‌ها

مدل منطقی Challenge در schema-design تعریف می‌شود. هر Challenge به یک account فعال و یک درخواست Recovery مشخص bind است. شناسه Challenge تصادفی است و به‌تنهایی capability یا مدرک اثبات نیست. در فاز فعلی فقط کانال ایمیل مصوب است؛ proof شامل challenge_id، account_id، channel=email، digest، زمان ایجاد/انقضا، زمان مصرف و شمارنده تلاش است. قید یکتا برای digest و مصرف یک‌باره اعمال می‌شود.

State transitionها باید با compare-and-swap یا row lock و شرط state قبلی انجام شوند. transition تکراری نباید mutation دوم ایجاد کند. Challenge جدید باید challenge قبلی همان حساب را به‌صورت اتمیک باطل کند. پیاده‌سازی و migration جدید تا رزرو رسمی WG-0 و مجوز لازم ممنوع است.

### 5.2 ارزیابی کانال ایمیل

Recovery ایمیل‌تنها تصمیم مصوب مالک برای فاز فعلی است؛ این روش MFA یا دو عامل مستقل نیست. threat review باید mailbox compromise، phishing، forwarding، بازیابی حساب ایمیل و تصاحب کانال تماس را ارزیابی و کنترل‌های کاهش ریسک را ثبت کند.

provider ایمیل باید TLS معتبر، عدم ثبت OTP در لاگ، محدودیت ارسال، گزارش delivery غیرحساس و مدیریت امن credential داشته باشد. هیچ provider یا secret عملیاتی در این سند انتخاب نشده است. SMS و offline recovery code عامل اجباری یا fallback این فاز نیستند.

### 5.3 تراکنش، concurrency و idempotency

درخواست نهایی باید دارای operation_id تصادفی و یکتا باشد. در تراکنش نهایی:
- account، Challenge و recovery code با ترتیب قفل ثابت و یکسان lock شوند تا deadlock قابل پیش‌بینی باشد؛
- state و expiry با زمان DB بررسی شوند، نه ساعت کلاینت؛
- unique constraint روی code مصرف‌شده و operation_id از دوبار اجرا جلوگیری کند؛
- همه Challengeهای باز همان حساب Cancel شوند؛
- code مصرف‌شده، credential_version، revoke نشست‌ها، must_change_password، audit و نتیجه operation در یک commit واحد ثبت شوند؛
- retry با همان operation_id نتیجه ذخیره‌شده را برگرداند و mutation دوم نسازد؛ operation_id جدید پس از نتیجه نامعلوم، بدون reconcile مجاز نیست.

اگر اتصال هنگام commit قطع شود، پاسخ «نامشخص/در حال بررسی» است، نه موفق یا ناموفق قطعی. بازیابی نتیجه فقط با خواندن operation_id از PostgreSQL انجام می‌شود. اگر DB در دسترس نیست، هیچ retry با operation_id جدید و هیچ تغییر اعتبارنامه انجام نمی‌شود.

### 5.4 قرارداد 401/403/404 و Scope

- 401 فقط برای نبود/نامعتبر بودن هویت یا نشست، expiry/revoke، account disabled یا credential_version ناسازگار.
- 403 برای principal معتبر که permission لازم را ندارد.
- برای شناسه رکوردی که وجود یا عدم وجود آن نباید افشا شود، پاسخ بیرونی یکسان 404 برای «وجود ندارد» و «خارج از scope»؛ این رفتار باید در همه endpointهای هم‌خانواده ثابت باشد.
- 404 عدم وجود مجوز عمومی را پنهان نمی‌کند؛ مجوز عملیات عمومی همچنان 403 است.
- reason code دقیق فقط در audit امن داخلی ثبت می‌شود و نباید جزئیات رکورد/مالک را به caller افشا کند.

### 5.5 مدل Scope و ترکیب چندنقشی

Scope یک فیلتر مستقل از permission است. ترتیب ارزیابی: هویت/نشست، permission دقیق، context معتبر منبع، scope، سپس قواعد وضعیت/مالکیت دامنه. هیچ مرحله‌ای با مرحله بعدی جایگزین نمی‌شود.

Scopeهای قابل مدل‌سازی (بدون اعطای خودکار):
- `self`: داده متعلق به principal؛
- `assigned`: رکورد دارای تخصیص فعال به principal از منبع authoritative؛
- `branch`: شعبه‌های صریحاً تخصیص‌یافته؛
- `warehouse`: انبارهای صریحاً تخصیص‌یافته؛
- `all`: فقط با grant صریح و تأییدشده برای همان permission؛ wildcard یا پیش‌فرض نیست.

برای چند نقش، union فقط روی grantهای صریح همان permission و همان نوع scope انجام می‌شود. Scope وسیع‌تر از نقش دیگر نباید از یک permission متفاوت به ارث برسد. نبود یا ابهام context مساوی Deny است. محدودیت دوره مالی، وضعیت سند و مالکیت aggregate، policy دامنه‌ای جداگانه و اجباری‌اند.

### 5.6 Bootstrap و تعارض credential اولیه

ADR-0013 عبارت تاریخی admin/admin را برای setup اولیه ثبت کرده و هم‌زمان credential ثابت در source/config/installer/migration/log/telemetry را ممنوع کرده است. این دو عبارت بدون پروتکل provisioning امن تعارض اجرایی دارند. تصمیم محصول می‌گوید نصب باید حساب مدیر اولیه ایجاد کند و اولین ورود تغییر username/password/profile را اجباری کند؛ اما روش امن تولید و تحویل credential اولیه هنوز مشخص نشده است.

پیش از runtime باید روش provisioning یکتا و خارج از source تعریف شود (مانند secret موقت تولیدشده در setup محلی، نمایش یک‌باره و الزام تغییر فوری) و توسط مالک/امنیت تأیید شود. تا آن زمان هیچ مقدار ثابت، default password قابل حدس یا credential مشترک نباید در محصول ساخته شود. شبکه و عملیات عادی تا تکمیل bootstrap باید fail-closed بمانند.

### 5.7 Session، Lockout و concurrency

زمان‌ها فقط از ساعت PostgreSQL محاسبه می‌شوند. افزایش شمارنده شکست و تعیین locked_until باید اتمیک باشد؛ درخواست‌های هم‌زمان نباید پنجره یا تعداد تلاش را دور بزنند. rate limit مبدأ مکمل lockout حساب است و نباید جایگزین آن شود. مقدار فعلی source rate-limit در کد 20 شکست ناموفق در 5 دقیقه است؛ این مقدار هنوز تصمیم مالک نیست و باید تعیین تکلیف شود.

ظرفیت session به‌صورت transactionally serialized روی ردیف تنظیمات singleton enforce می‌شود. نشست فعال یعنی revoked_at IS NULL، lease_expires_at > DB now، credential_version_at_issue برابر credential_version حساب و account enabled. heartbeat پس از revoke یا credential version mismatch هرگز lease را تمدید نمی‌کند. سیاست جاری: heartbeat=30s، lease=180s و حداکثر 5 حساب فعال متمایز؛ absolute TTL و سقف 2 نشست/account تصویب نشده‌اند.

### 5.8 TLS، میزبان محلی و خطاهای Provider

«Local-only» باید با کنترل سیستم‌عامل و bind صریح loopback تعریف شود؛ headerهای forwarded یا آدرس اعلام‌شده از client مبنای اعتماد نیستند. bind wildcard، IPv6/IPv4 عمومی، HTTP غیر-loopback و fallback به TLS نامعتبر ممنوع است. certificate validation خاموش نمی‌شود و خطای trust/hostname/expiry fail-closed است. اگر TLS برای loopback محلی متفاوت است، روش آن باید در ADR topology ثبت شود؛ loopback بودن به‌تنهایی مجوز نادیده‌گرفتن اعتبارسنجی TLS نیست.

در خطای provider، timeout، پاسخ مبهم یا delivery نامعلوم، proof تأییدشده محسوب نمی‌شود. OTP فقط پس از درخواست معتبر ایجاد و digest آن ذخیره می‌شود؛ پاسخ provider شامل secret یا متن کد در log ممنوع است. کلید pepper در secret store خارج DB نگهداری، نسخه‌گذاری و rotation آن با امکان اعتبارسنجی محدود digestهای نسخه قدیم طراحی می‌شود؛ الگوریتم و چرخه عمر تا review تصویب نشده‌اند.

### 5.9 جدول پیگیری یافته‌ها

| Finding | اصلاح مستندشده | وضعیت باقی‌مانده |
|---|---|---|
| IA-REV-01 Challenge schema | مدل و قیود در Schema Design تکمیل شد | Migration فقط پس از G1 |
| IA-REV-02 استقلال عوامل | threat criteria و شرط توقف افزوده شد | اثبات provider/کانال و review مستقل |
| IA-REV-03 replay/race/unknown commit | operation_id، locking، idempotency و reconcile افزوده شد | تست runtime پس از G1 |
| IA-REV-04 API scope | مدل scope و قرارداد 401/403/404 افزوده شد | تصویب ماتریس و تست endpoint |
| IA-REV-05 Bootstrap | تعارض admin/admin ثبت و عملیات محدود تعریف شد | تصمیم مالک درباره provisioning |
| IA-REV-06 Session | قواعد concurrency و lease افزوده شد | تصمیم TTL مطلق/چندکلاینت |
| IA-REV-07 Lockout | atomic counter و ضد DoS افزوده شد | پارامترهای rate limit و تست runtime |
| IA-REV-08 Provider/Audit | قواعد secret, pepper, failure و audit تکمیل شد | انتخاب provider/retention/کلید |
| IA-REV-09 TLS | اعتبارسنجی certificate و bind محلی صریح شد | ثبت جزئیات topology در ADR |

**وضعیت:** این Revision نقص‌های مستندسازی را می‌بندد، نه گیت‌های تصمیم/پیاده‌سازی را. G1 همچنان بسته و `g1Authorized=false` است.
## 6. تصمیم‌های مالک محصول — Recovery و Session (بازنگری 2026-11-17)

### 6.1 بازار و Provider
- بازار هدف فعلی ایران است.
- Email از mailbox اختصاصی روی دامنه محصول ارسال می‌شود؛ برنامه Mail Server مستقل اجرا نمی‌کند. ارسال از SMTP relay یا Transactional Email provider قابل تعویض استفاده می‌کند. vendor/SMTP host و DNS واقعی در زمان استقرار تعیین می‌شود.
- SMS در این فاز خارج از دامنه است؛ انتخاب vendor blocker فعلی نیست. نبود SMS مانع Recovery ایمیل‌تنها نیست.
- هیچ API key، SMTP password یا OTP template secret در Git، appsettings، installer، migration یا log قرار نگیرد.

### 6.2 OTP و محدودیت‌ها
| پارامتر | مقدار مصوب |
|---|---:|
| Email OTP TTL | 600 ثانیه (10 دقیقه) |
| حداکثر تلاش ناموفق در challenge | 5 |
| قفل پس از رسیدن به حد تلاش | 86400 ثانیه (24 ساعت) |
| فاصله حداقل ارسال مجدد | 3600 ثانیه (1 ساعت) |
| سقف ارسال در rolling 1h | 1 |
| سقف ارسال در rolling 24h | 5 |
| قفل پس از عبور از سقف ارسال | 86400 ثانیه (24 ساعت) |

- محدودیت‌ها per account و per source enforce شوند؛ شمارنده‌ها اتمیک و متکی به ساعت DB باشند. قفل منبع به‌تنهایی نباید ابزار آسان DoS علیه حساب قربانی باشد.
- resend، challenge قبلی را revoke می‌کند؛ هر OTP به همان account/challenge bind است. ارسال ناموفق/نامشخص provider proof معتبر تولید نمی‌کند.
- این lockout صرفاً برای Recovery است؛ lockout ورود عادی 5 شکست در 15 دقیقه/قفل 15 دقیقه‌ای را تغییر نمی‌دهد.

### 6.3 Session
| پارامتر | مقدار جاری |
|---|---:|
| Heartbeat | 30 ثانیه |
| Lease | 180 ثانیه |
| حساب فعال متمایز کل سیستم | 5، قابل تغییر با permission اختصاصی و audit |
| عمر مطلق نشست | تصویب نشده؛ TTL مطلق وجود ندارد |
| سقف نشست هم‌زمان هر حساب | تصویب نشده؛ سقف per-account وجود ندارد |
| تغییر رمز | افزایش credential_version و revoke فوری همه نشست‌های حساب |
| غیرفعال‌سازی حساب | revoke فوری همه نشست‌های حساب |

heartbeat معتبر lease را تمدید می‌کند؛ PostgreSQL مرجع authoritative است. کنترل ظرفیت باید در تراکنش/serialization سمت DB انجام شود. هیچ قاعده 12h absolute TTL یا 2 sessions/account از متن‌های قدیمی نباید پیاده‌سازی شود.

### 6.4 تعارض‌های قبلی و مرز تصمیم
- بندهای تاریخی absolute TTL=12h و سقف 2 نشست/account superseded هستند؛ سیاست جاری در اصلاحیه ADR-0013 مورخ 2026-11-07 و تصمیم‌های نهایی مالک 2026-11-06/07 ثبت شده است.
- Recovery ایمیل‌تنها پذیرفته شده و MFA نیست؛ هر الزام قدیمی به سه عامل ایمیل+موبایل+offline code superseded است.
- تصمیم سیاستی مجوز runtime، migration، endpoint، middleware، SMTP credential یا G1 ایجاد نمی‌کند.


## تعیین دامنه فعلی: عدم پیاده‌سازی پیامک — 2026-11-05
- بنا بر تصمیم مالک، در این مرحله هیچ SMS provider، SDK، API client، credential، template، configuration، background job یا تست وابسته به سرویس پیامک اضافه نمی‌شود.
- قابلیت SMS به مرحله/نسخه آینده موکول است و در این مرحله صرفاً به‌عنوان توسعه آتی مستند می‌شود؛ انتخاب vendor نیز اکنون لازم نیست.
- تصمیم نهایی مالک، Recovery ایمیل‌تنها را برای این فاز پذیرفته است؛ بااین‌حال تا تکمیل workflow، SMTP، تست و review مستقل، Recovery runtime غیرفعال می‌ماند. عامل offline recovery code نیز به‌تنهایی جایگزین عامل مستقل دوم نمی‌شود مگر آنکه مدل تهدید و سیاست مالک جداگانه تصویب شود.
- مقادیر TTL پیامک و محدودیت ارسال پیامک که پیش‌تر در سند آمده‌اند، مشخصات هدف آینده‌اند و در این مرحله هیچ رفتار اجرایی یا تست runtime برای SMS ایجاد نمی‌کنند.
- ایمیل اختصاصی نیز در این مرحله فقط معماری هدف است؛ SMTP relay واقعی و secretها تا انتخاب/پیکربندی عملیاتی وارد برنامه نمی‌شوند.


## 6. شفاف‌سازی اجباری پس از تعویق SMS — 2026-11-05

تصمیم مالک این است که SMS در این فاز پیاده‌سازی نشود و به آینده موکول گردد. بنابراین عبارت‌های قدیمی‌تر این سند درباره «اثبات کانال موبایل» به معنی وجود SMS runtime یا مجوز انتخاب خودکار یک روش جایگزین نیستند. روش اجرایی اثبات عامل دوم مستقل از ایمیل هنوز تعیین و تصویب نشده است.

تا زمانی که مالک و reviewer امنیتی روش مستقل جایگزین را با threat model و معیارهای آزمون تصویب نکرده‌اند:
- هیچ Recovery ایمیل‌تنها فعال یا قابل دسترس نشود.
- هیچ عامل جایگزین، مسیر دستی، پشتیبانی‌محور یا bypass به‌صورت ضمنی فرض نشود.
- کد آفلاین به‌تنهایی عامل مستقل دوم فرض نشود.
- Recovery runtime و migration آن ساخته/فعال نشود.
- TTL پیامک و محدودیت‌های ارسال فقط هدف طراحی آینده باقی بمانند.

بسته تحویل reviewer و فهرست blockerهای فعلی در `identityaccess-g1-review-handoff-fa.md` مرجع جاری هستند. این الحاقیه مجوز G1 نیست و `g1Authorized=false` باقی می‌ماند.


## الحاقیه نهایی دامنه Recovery — تصمیم مالک 2026-11-06 (مقدم بر طراحی سه‌عاملی قدیمی)
- مالک محصول Recovery ایمیل‌تنها را برای فاز فعلی تصویب کرده است؛ الزام سه عامل ایمیل+موبایل+کد آفلاین در بخش‌های قدیمی، برای فاز فعلی الزام اجرایی/پذیرش نیست و گزینه طراحی قبلی/آینده تلقی می‌شود.
- جریان فعلی فقط از OTP ایمیل ثبت‌شده استفاده می‌کند: CSPRNG، یک‌بارمصرف، TTL ده دقیقه، digest-only، bind به account/challenge، حداکثر پنج تلاش و قفل 24 ساعته، resend حداقل یک ساعت، سقف یک ارسال در ساعت و پنج ارسال در 24 ساعت، و قفل 24 ساعته پس از عبور از سقف.
- ایمیل‌تنها MFA نیست. تصاحب mailbox، phishing، account recovery ایمیل و forwarding باید در threat model و review مستقل بررسی شوند.
- پس از proof موفق، تغییر رمز با hasher استاندارد/versioned، افزایش credential_version، revoke اتمیک همه نشست‌ها، audit بدون secret و الزام تغییر رمز در ورود بعدی باید در یک تراکنش واحد انجام شود.
- SMS فقط placeholder آینده و غیرفعال است؛ هیچ provider، SDK، credential، ارسال یا تست وابسته به SMS در این فاز وجود ندارد. کد آفلاین نیز عامل اجباری یا مسیر bypass نیست.
- OTP معتبر و منقضی‌نشده برای همان account/challenge مجاز است؛ OTP تکراری/منقضی/حدس‌زده‌شده/متعلق به حساب دیگر رد می‌شود؛ مصرف هم‌زمان دقیقاً یک commit موفق دارد؛ خطای provider/DB یا نتیجه commit نامعلوم proof موفق محسوب نمی‌شود.
- این تصمیم فقط قرارداد طراحی است. Recovery runtime، migration یا endpoint تا review مستقل، تست روی SHA مشخص و مجوز رسمی G1 ممنوع است؛ `g1Authorized=false` باقی می‌ماند.


## تصمیم اجرایی جاری — 2026-11-07
- Recovery ایمیل‌تنها برای این فاز تصویب شده است؛ OTP یک‌بارمصرف 10 دقیقه‌ای و digest-only، حداکثر 5 تلاش، قفل 24 ساعته، resend حداقل هر 1 ساعت، حداکثر 1 ارسال در rolling hour و 5 ارسال در rolling 24h؛ نقض سقف قفل 24 ساعته دارد.
- پس از اثبات OTP، تغییر رمز + افزایش credential_version + revoke اتمیک همه نشست‌های حساب + اجبار تغییر رمز در ورود بعدی الزامی است.
- این flow MFA نیست و تا threat-model review، SMTP configuration، endpoint/orchestration، rate-limit اتمیک و تست replay/race/expiry/failure آماده نشوند، Recovery runtime فعال نیست.
- SMS فقط placeholder آینده و غیرفعال است؛ schema و Store این فاز نباید کانال mobile را بپذیرند. offline recovery code عامل اجباری یا bypass نیست.
