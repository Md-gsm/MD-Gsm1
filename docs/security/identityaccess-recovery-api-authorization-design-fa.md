# طرح امنیتی Recovery اضطراری و API Authorization

- تاریخ: 2026-11-05
- وضعیت: طراحی و معیار پذیرش؛ غیرعملیاتی و بدون مجوز G1
- مبنا: ADR-0006، ADR-0012، ADR-0013 و پنج موضوع باز IdentityAccess

## 0. مرز امنیتی

این سند به‌تنهایی مجوز ایجاد login/recovery endpoint، middleware فعال، migration IdentityAccess، network listener یا تغییر `g1Authorized=false` نیست. PostgreSQL مرجع authoritative است. Desktop غیرقابل‌اعتماد است. هر خطای DB، provider، پیکربندی یا policy در مسیر حساس باید fail-closed باشد. raw password، bearer token، recovery code، OTP و PII غیرضروری نباید در DB/log/trace/telemetry/CI artifact ثبت شوند. هیچ backdoor، master password یا مسیر bypass مجاز نیست.

## 1. Recovery اضطراری Super Admin

### 1.1 هدف و مرز

Recovery فقط برای حساب Super Admin و فقط از کنسول محلی میزبان مورد اعتماد آغاز می‌شود. Local-only به‌تنهایی اثبات هویت نیست. تکمیل recovery مستلزم هر سه عامل مستقل و متصل به یک account و challenge است:
1. اثبات کانال ایمیل ثبت‌شده؛
2. اثبات کانال موبایل ثبت‌شده؛
3. recovery code آفلاین یک‌بارمصرف با entropy حداقل 128 بیت.

هیچ عامل جایگزین دیگری نیست. فقدان هر عامل، recovery خودکار را متوقف می‌کند؛ هیچ fallback دستی/پشتیبانی‌محور تا طراحی ابزار امضاشده و تأیید دو نفره وجود ندارد.

### 1.2 دارایی‌ها و اهداف امنیتی

| دارایی | الزام |
|---|---|
| حساب Super Admin | فقط با سه اثبات مستقل بازیابی شود |
| recovery code | CSPRNG، حداقل 128 بیت entropy، نمایش یک‌باره، فقط digest در DB |
| challenge | کوتاه‌عمر، account-bound، request-bound و غیرقابل replay |
| OTP/nonce کانال | فقط digest، مقایسه امن، عدم افشا در پاسخ و log |
| نشست‌ها | revoke کامل و فوری پس از recovery |
| credential | افزایش credential_version و must_change_password=true |
| audit | metadata allow-list؛ بدون secret و PII غیرضروری |

### 1.3 تهدیدها و مرز اعتماد

- compromise حساب OS یا process محلی؛
- compromise ایمیل، SIM-swap، forwarding یا تأخیر provider؛
- brute force، flood و DoS کانال ارسال؛
- replay و race بین challengeها یا مصرف هم‌زمان code؛
- قطع PostgreSQL، failover و نتیجه commit نامعلوم؛
- افشای secret از log، trace، crash dump، telemetry یا CI artifact؛
- سوءاستفاده اپراتور داخلی برای bypass یا recovery بدون audit.

### 1.4 State machine

حالت‌ها: `Created`, `EmailVerified`, `MobileVerified`, `ProofsComplete`, `Completed`, `Expired`, `Locked`, `Cancelled`, `FailedClosed`.

- دو تأیید کانال باید به همان account/challenge bind باشند؛ ترتیب آن‌ها آزاد است.
- فقط پس از هر دو تأیید، `ProofsComplete` مجاز است.
- فقط `ProofsComplete -> Completed` با مصرف اتمیک recovery code مجاز است.
- حالت نهایی قابل برگشت نیست؛ تکرار proof نباید state را جلو ببرد.
- یک code نمی‌تواند در دو challenge موفق شود.
- هر خطای DB/provider یا وضعیت نامعلوم، موفقیت تلقی نمی‌شود.

### 1.5 داده و TTL

مدل منطقی challenge شامل شناسه تصادفی، account_id، زمان ایجاد/انقضا، state، شمارنده تلاش و correlation غیرحساس است. OTP/nonce با CSPRNG تولید و فقط digest آن ذخیره شود. challenge باید به عملیات محلی و account bind شود؛ ID ارسالی کلاینت به‌تنهایی اثبات نیست.

TTL دقیق challenge/OTP، قالب کد، تعداد تلاش، resend cooldown، rate limit، provider ایمیل/SMS، الگوریتم digest/pepper و روش مدیریت کلید هنوز تصمیم اجرایی ندارند و نیازمند تصویب و review امنیتی‌اند. هیچ مقدار حدسی در runtime قرار نگیرد.

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
| REC-01 | شروع از route شبکه‌ای | وجود نداشته باشد/رد شود |
| REC-02 | کنسول محلی بدون سه اثبات | رد، بدون mutation |
| REC-03/04 | فقط ایمیل یا فقط موبایل | تکمیل ممنوع |
| REC-05 | دو کانال بدون code | تکمیل ممنوع |
| REC-06 | entropy کمتر از 128 بیت | provisioning رد |
| REC-07 | raw code/OTP/token در DB/log/artifact | صفر occurrence |
| REC-08 | challenge منقضی | رد |
| REC-09 | challenge متعلق به account دیگر | رد |
| REC-10 | replay proof | بدون پیشروی state |
| REC-11 | دو درخواست هم‌زمان با یک code | دقیقاً یک winner |
| REC-12 | دو challenge با یک code | حداکثر یک موفقیت |
| REC-13 | خطا پیش از commit | rollback کامل |
| REC-14 | نتیجه commit نامعلوم | عدم اعلام موفقیت کاذب؛ reconcile از DB |
| REC-15 | recovery موفق | revoke همه sessionها، credential_version++، must_change_password |
| REC-16 | DB/provider unavailable | fail-closed |
| REC-17 | brute force/flood | rate-limit و audit بدون account enumeration |
| REC-18 | log/trace/crash | بدون secret و PII غیرضروری |
| REC-19 | code استفاده‌شده یا revoke‌شده | رد |
| REC-20 | account نامعتبر/disabled یا non-bootstrap | رد طبق policy مصوب |

### 1.9 تصمیم‌های باز

Provider ایمیل/SMS، TTL، تعداد تلاش، resend cooldown، rate-limit، digest/pepper و key rotation، تعداد/تحویل امن codeهای آفلاین، تعریف کنسول محلی مورد اعتماد، فرایند دو نفره و incident response باید تعیین و review شوند. تا آن زمان هیچ مسیر recovery عملیاتی ساخته نشود.

**خروجی مرحله ۱:** threat model و test acceptance مستند شد؛ runtime عمداً ساخته نشد.

## 2. API Authorization — قرارداد

ماتریس فعلی در `identityaccess-five-open-items-fa.md` پیشنهاد است و تصویب نشده؛ بنابراین permissionها نباید seed یا فعال شوند.

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
- heartbeat=30s، lease=180s و سقف 5 حساب فعال، مقادیر پیشنهادی‌اند؛ تا تصویب نهایی runtime نشوند.
- bootstrap admin/admin فقط یک‌باره و local-only؛ تا تغییر رمز و تکمیل پروفایل، listener و عملیات عادی بسته.
- raw token در DB/log/telemetry/artifact صفر؛ revoked token در درخواست بعدی رد؛ disabled account و credential version قدیمی رد؛ lockout و session cap در concurrency درست enforce شوند.

## 4. اتصال API و E2E

تا مجوز رسمی G1، فقط قرارداد و test plan؛ middleware یا endpoint فعال ایجاد نمی‌شود. پس از G1:
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
- provider/TTL/rate-limit/provisioning تصویب شوند.
- permission matrix و scopeها صریحاً توسط مالک تصویب شوند.
- session/lockout قراردادها با ADRها تطبیق داده شوند.
- reviewer مستقل روی SHA مشخص نتیجه/یافته/امضا ثبت کند.
- مالک تصمیم رسمی G1 را ثبت کند.

تا آن زمان `g1Authorized=false`؛ migration/runtime فعال نیست؛ recovery/login/auth middleware به production host وصل نمی‌شود؛ PRهای قبلی و main تغییر نمی‌کنند. این سند تأیید امنیتی یا بستن G1 نیست.

 
## 5. تکمیل الزامات بازبینی سخت‌گیرانه (Revision 2)

این بخش ابهام‌های بازبینی را به قرارداد قابل ارزیابی تبدیل می‌کند؛ هیچ مقدار پیشنهادی را به تصمیم مالک یا مقدار runtime تبدیل نمی‌کند.

### 5.1 مدل Challenge و اثبات کانال‌ها

مدل منطقی و قیود پیشنهادی در `identityaccess-schema-design-fa.md` تعریف می‌شود. هر Challenge به یک account فعال و یک درخواست محلی مشخص bind است. شناسه Challenge تصادفی است و به‌تنهایی capability یا مدرک اثبات نیست. هر proof دارای `challenge_id`، `account_id`، `channel`، digest، زمان ایجاد/انقضا، زمان تأیید و شمارنده تلاش مستقل است. قید یکتا برای هر (challenge, channel) و هر nonce اعمال می‌شود؛ تأیید یک کانال نمی‌تواند برای Challenge دیگر مصرف شود.

State transitionها باید با compare-and-swap یا row lock و شرط state قبلی انجام شوند. transition تکراری idempotent است و state نهایی را تغییر نمی‌دهد. Challenge فعال هم‌زمان برای یک حساب طبق سیاست تک‌Challenge فعال پیشنهاد می‌شود؛ صدور Challenge جدید باید Challengeهای قبلی را اتمیک Cancel کند. این سیاست تا تصویب مالک، تصمیم اجرایی محسوب نمی‌شود.

### 5.2 استقلال عوامل و ارزیابی کانال

سه عامل در سه دسته‌اند: کنترل ایمیل ثبت‌شده، کنترل شماره موبایل ثبت‌شده، و possession کد آفلاین. پیش از پیاده‌سازی، threat review باید استقلال عملی دو کانال دیجیتال را ارزیابی کند؛ ایمیل و موبایل روی یک دستگاه یا با یک روش بازیابی مشترک، استقلال قوی ایجاد نمی‌کنند. اگر وابستگی مشترک شناخته شود، آن دو کانال نباید به‌عنوان دو عامل مستقل شمرده شوند و Recovery باید متوقف بماند تا روش جایگزین مورد تصویب قرار گیرد.

provider باید قابلیت جلوگیری از افشای وجود حساب، TLS معتبر، عدم ثبت OTP در لاگ، محدودیت ارسال، گزارش delivery غیرحساس و مدیریت امن credential داشته باشد. SMS در برابر SIM-swap مصون فرض نمی‌شود. هیچ provider یا کانالی در این سند انتخاب نشده است.

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

ADR-0013 عبارت `admin/admin` را برای setup اولیه ثبت کرده و هم‌زمان credential ثابت در source/config/installer/migration/log/telemetry را ممنوع کرده است. این دو عبارت بدون پروتکل provisioning امن، تعارض اجرایی دارند. این سند آن را خودسرانه حل یا رمز ثابت را تأیید نمی‌کند.

پیش از runtime باید مالک تصمیم بگیرد credential اولیه چگونه به‌صورت یکتا و خارج از source توزیع/تحویل می‌شود (برای مثال secret یک‌بارمصرف تولیدشده در setup محلی با نمایش فقط یک‌باره و الزام تغییر فوری). تا ثبت تصمیم و threat review، هیچ مقدار ثابت، default password قابل حدس یا credential مشترک نباید در محصول ساخته شود. حالت Bootstrap فقط اجازه تغییر credential و تکمیل پروفایل را دارد؛ تمام routeها و عملیات دیگر، از جمله endpointهای مدیریتی و گزارش‌ها، باید Deny شوند. شبکه تا ثبت commit نهایی bootstrap غیرفعال می‌ماند.

### 5.7 Session، Lockout و concurrency

زمان‌ها فقط از ساعت PostgreSQL محاسبه می‌شوند. افزایش شمارنده شکست و تعیین locked_until باید اتمیک باشد؛ درخواست‌های هم‌زمان نباید پنجره یا تعداد تلاش را دور بزنند. rate limit مبدأ مکمل lockout حساب است و نباید جایگزین آن شود. برای جلوگیری از DoS ناشی از قفل‌کردن حساب قربانی، محدودیت مبدأ، پاسخ عمومی، و سیاست reset شمارنده باید مستقل و تست شوند.

Session cap به‌صورت transactionally serialized روی ردیف تنظیمات singleton یا advisory lock ثابت enforce می‌شود؛ درخواست‌های هم‌زمان برای آخرین slot فقط یک winner دارند. session فعال یعنی revoked_at IS NULL و lease_expires_at > DB now و credential_version_at_issue = account.credential_version و account enabled. heartbeat پس از revoke یا credential version mismatch هرگز lease را تمدید نمی‌کند. TTL مطلق نشست، رفتار قطع طولانی شبکه و سیاست چندکلاینت باید قبل از runtime تصویب شود.

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
## 6. تصمیم‌های مالک محصول — Recovery و Session (2026-11-05)

این بخش تصمیم‌های مالک را جایگزین عبارت‌های قبلی «نامشخص/پیشنهادی» می‌کند؛ مرز غیرعملیاتی و g1Authorized=false بدون تغییر باقی می‌ماند.

### 6.1 بازار و Provider
- بازار هدف فعلی ایران است.
- Email از mailbox اختصاصی روی دامنه محصول ارسال می‌شود؛ برنامه Mail Server مستقل اجرا نمی‌کند. ارسال از SMTP relay یا Transactional Email provider قابل تعویض استفاده می‌کند. نام vendor/SMTP host و DNS واقعی در مرحله استقرار تعیین می‌شود.
- SMS vendor هنوز انتخاب نشده است. تا ارزیابی و آزمون واقعی API/OTP template/delivery/status/SLA/خط خدماتی/قوانین ارسال، Recovery عملیاتی نشود.
- هیچ API key، SMTP password یا OTP template secret در Git، appsettings، installer، migration یا log قرار نگیرد.

### 6.2 OTP و محدودیت‌ها
| پارامتر | مقدار مصوب |
|---|---:|
| Email OTP TTL | 600 ثانیه (10 دقیقه) |
| SMS OTP TTL | 300 ثانیه (5 دقیقه) |
| حداکثر تلاش ناموفق در challenge | 5 |
| قفل پس از رسیدن به حد تلاش | 86400 ثانیه (24 ساعت) |
| فاصله حداقل ارسال مجدد | 3600 ثانیه (1 ساعت) |
| سقف ارسال در rolling 1h | 1 |
| سقف ارسال در rolling 24h | 5 |
| قفل پس از عبور از سقف ارسال | 86400 ثانیه (24 ساعت) |

- محدودیت‌ها per account + channel و per source enforce شوند؛ شمارنده‌ها اتمیک و متکی به ساعت DB باشند. قفل منبع به‌تنهایی نباید ابزار آسان DoS علیه حساب قربانی باشد.
- resend، proof قبلی همان کانال را revoke می‌کند؛ هر OTP به همان account/challenge bind است. ارسال ناموفق/نامشخص provider proof معتبر تولید نمی‌کند.
- این lockout صرفاً برای Recovery است؛ lockout ورود عادی 5 شکست در 15 دقیقه/قفل 15 دقیقه‌ای را تغییر نمی‌دهد.

### 6.3 Session
| پارامتر | مقدار مصوب |
|---|---:|
| Heartbeat | 30 ثانیه |
| Lease | 180 ثانیه |
| عمر مطلق نشست از زمان ایجاد | 12 ساعت |
| نشست/دستگاه هم‌زمان هر حساب | 2 |
| حساب فعال متمایز کل سیستم | 5، قابل تغییر توسط super_admin |
| تغییر رمز | revoke فوری همه نشست‌های حساب |
| غیرفعال‌سازی حساب | revoke فوری همه نشست‌های حساب |

Absolute TTL با heartbeat تمدید نمی‌شود. سقف دو نشست و سقف حساب فعال باید با transaction/advisory lock enforce شود. تغییر سقف کل فقط با permission اختصاصی، audit و اعتبارسنجی بازه مجاز انجام شود. تعویض نشست در زمان پر بودن سقف per-account باید صریح، قابل مشاهده و audit شود؛ اخراج خودکار/خاموش ممنوع. «دستگاه» با client_instance_id تصادفی تعریف می‌شود، نه fingerprint سخت‌افزاری.

### 6.4 تعارض‌های قبلی و مرز تصمیم
- مقادیر 30s/180s و 5 حساب که پیش‌تر پیشنهادی بودند، اکنون همراه 12h و سقف 2 نشست/account تصمیم مالک‌اند.
- تصمیم فعلی فقط Recovery OTP و Session را پوشش می‌دهد؛ ماتریس permission، retention و جزئیات bootstrap provisioning جداگانه باقی می‌مانند.
- Runtime، migration، endpoint، middleware و provider credential تا تکمیل G1 و مجوز رسمی ایجاد/فعال نشوند.
