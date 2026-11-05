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
