# بسته تحویل رسمی بازبینی مستقل G1 — IdentityAccess

- تاریخ تهیه: 2026-11-05
- وضعیت: **آماده تحویل برای بررسی مستقل؛ G1 تأیید نشده و مسدود است**
- مخزن: `Md-gsm/MD-Gsm1`
- PR جاری: [#37](https://github.com/Md-gsm/MD-Gsm1/pull/37)
- شاخه: `security/identityaccess-recovery-authz-design-20261105`
- شاخه مقصد PR: `feature/persons-ac31-45-runtime`
- SHA مبنای شواهد CI: `26562482b09e55f0750fb2264574a701916e1c74`
- وضعیت PR هنگام تهیه بسته: Open، Draft، Unmerged
- وضعیت main: بدون تغییر توسط این PR

> این سند فهرست شواهد و موارد لازم برای تصمیم مستقل است؛ نه گزارش تأیید امنیتی، نه امضای reviewer و نه مجوز اجرای runtime.

## 1. خلاصه تصمیم برای reviewer

در SHA فوق، اسناد طراحی و مرزبندی IdentityAccess و طرح Recovery/API Authorization آماده بررسی مستنداتی هستند. اما IdentityAccess هنوز runtime احراز هویت/مجوزدهی، endpoint ورود یا بازیابی، middleware، session store، migration اختصاصی یا listener عملیاتی ندارد. بنابراین reviewer در این مرحله می‌تواند صحت معماری، تصمیم‌ها، تناقض‌ها، threat model و آمادگی گیت‌ها را بررسی کند؛ نمی‌تواند اثربخشی runtimeهایی را که هنوز وجود ندارند تأیید کند.

**تصمیم پیشنهادی فعلی:** KEEP BLOCKED / NO-GO برای فعال‌سازی G1 و هرگونه runtime یا migration تا رفع پیش‌نیازهای بخش 5 و امضای مستقل.

## 2. شواهد CI متصل به SHA

تمام اجراهای زیر دقیقاً روی SHA بالا تکمیل شده‌اند:

| کنترل | Run ID | نتیجه | جزئیات |
|---|---:|---|---|
| Persons Module Gates | 42098140813 | SUCCESS | 13 از 13 job موفق |
| CodeQL C# | 42098140790 | SUCCESS | 1 از 1 job موفق |
| Windows Preview Installer | 42098140826 | SUCCESS | 1 از 1 job موفق |

### 2.1 فهرست jobهای Persons Module Gates

همه موارد زیر `completed/success` بوده‌اند:

1. persons-merge-contract
2. persons-architecture-boundary
3. persons-security-tests
4. persons-e2e-disposable
5. persons-temporary-data-retention
6. persons-postgres-windows-migrations
7. persons-consumer-registry
8. persons-ci-enforcement-test
9. persons-spec-lint
10. foundation-build-test
11. persons-api-contract
12. persons-postgres-migrations
13. persons-ci-evidence

لینک اجرای workflow:
- https://github.com/Md-gsm/MD-Gsm1/actions/runs/42098140813
- https://github.com/Md-gsm/MD-Gsm1/actions/runs/42098140790
- https://github.com/Md-gsm/MD-Gsm1/actions/runs/42098140826

### 2.2 حدود اعتبار شواهد CI

- این نتایج فقط همان SHA و jobهای اجراشده را پوشش می‌دهند.
- سبز بودن CI، وجود یا صحت runtime احراز هویت، Authorization، Recovery یا Session را اثبات نمی‌کند.
- CodeQL موفق، معادل penetration test یا review مستقل معماری/امنیت نیست.
- Windows Preview Installer موفق، معادل نصب و آزمون کامل IdentityAccess نیست.
- jobهای Persons بخشی از کنترل‌های همان مخزن/شاخه‌اند و نباید به‌عنوان تست runtime پیامک یا Recovery معرفی شوند.
- در این فاز هیچ تست وابسته به SMS یا سرویس‌دهنده SMS اضافه نشده است.

## 3. شواهد معماری و وضعیت واقعی پیاده‌سازی

### 3.1 شواهد قابل مشاهده در سورس

- `src/Host/Api/Program.cs`: سرویس‌های Persistence و endpointهای health و `/api/v1/system/info` وجود دارند؛ فراخوانی ثبت Authentication/Authorization و middleware مربوطه در این فایل وجود ندارد. پیام startup صریحاً اعلام می‌کند Database، Authentication و accounting workflows پیکربندی نشده‌اند.
- `src/Modules/IdentityAccess/Contracts/README.md`: قرارداد runtime هنوز منتشر نشده و پیاده‌سازی به review امنیتی ADR-0006 و مجوز صریح G1 وابسته است.
- `src/Modules/IdentityAccess/Domain/README.md`: مرز مالکیت تعریف شده، اما entity/invariant/behavior اجرایی هنوز پیاده نشده است.
- `docs/decisions/0006-opaque-access-tokens.md`: فقط hash توکن در سمت سرور، اعتبارسنجی authoritative و revoke فوری الزام شده؛ ADR به‌تنهایی مجوز شروع runtime نیست.
- `docs/security/identityaccess-recovery-api-authorization-design-fa.md`: threat model، state machine و test acceptance برای Recovery و API Authorization را تعریف می‌کند؛ خودش endpoint یا middleware فعال نمی‌سازد.

### 3.2 وضعیت قابلیت‌ها

| حوزه | وضعیت واقعی | مدرک لازم برای بستن |
|---|---|---|
| Authentication/password | طراحی؛ runtime موجود نیست | پیاده‌سازی، تست .NET و تست DB |
| Opaque token/session | ADR و طراحی؛ store/runtime موجود نیست | تست hash-only، revoke، expiry، concurrency |
| API Authorization | ماتریس پیشنهادی؛ evaluator/runtime موجود نیست | تصویب ماتریس، policy evaluator و تست منفی API |
| Bootstrap | تصمیم ثبت شده؛ runtime موجود نیست | تست local-only، یک‌باره، atomic و fail-closed |
| Recovery | طراحی و test plan؛ runtime عمداً غیرفعال | تصویب عوامل، threat review و REC-01..20 |
| Audit/retention امنیتی | پیشنهاد مستند؛ policy نهایی مصوب نیست | تصویب مالک/حقوقی و تست integrity/retention |
| TLS/topology | قیود معماری؛ آزمون عملی G1 کامل نیست | تست TLS، binding، firewall و deployment |
| G1 | مستقل بررسی/امضا نشده | reviewer واقعی، SHA، یافته‌ها و تصمیم مالک |

## 4. تصمیم‌های مالک ثبت‌شده و مرزهای دامنه

### 4.1 Session

تصمیم‌های ثبت‌شده مالک:
- heartbeat: 30 ثانیه
- lease: 180 ثانیه
- absolute lifetime: 12 ساعت از زمان ایجاد؛ heartbeat آن را تمدید نمی‌کند
- حداکثر 2 نشست هم‌زمان برای هر حساب
- حداکثر 5 حساب فعال متمایز در کل سیستم
- تغییر سقف کل فقط با permission اختصاصی super_admin و ثبت audit
- تغییر رمز و غیرفعال‌سازی حساب: revoke فوری همه نشست‌های همان حساب؛ تغییر رمز همچنین credential_version را افزایش می‌دهد
- enforce شدن سقف‌ها باید transactional/serialized باشد

این‌ها تصمیم‌های سیاستی‌اند؛ تا زمان G1 و پیاده‌سازی/تست، رفتار اجرایی محسوب نمی‌شوند.

### 4.2 Recovery و SMS

- بازار هدف فعلی ایران است.
- ایمیل هدف از mailbox اختصاصی دامنه محصول با SMTP relay/Transactional Email قابل‌تعویض ارسال می‌شود؛ vendor/host/DNS و secretها هنوز برای deployment انتخاب نشده‌اند.
- SMS صراحتاً به فاز آینده موکول شده است؛ در این فاز هیچ provider، SDK، client، credential، template، configuration، job یا تست وابسته به SMS وجود ندارد.
- TTL پیامک و محدودیت‌های مرتبط، فقط یادداشت طراحی آینده‌اند و نباید به‌عنوان قابلیت موجود یا آزموده‌شده تلقی شوند.
- نبود SMS مجوز Recovery ایمیل‌تنها نیست.
- تا تصویب و آزمون ترکیب مستقل عوامل بازیابی، Recovery اضطراری باید غیرفعال و غیرقابل‌دسترسی باقی بماند.
- recovery code آفلاین به‌تنهایی جایگزین عامل مستقل دوم تلقی نمی‌شود مگر با threat model و تصویب جداگانه.

**ابهام مسدودکننده:** سند طراحی قبلی از ایمیل + موبایل + کد آفلاین نام می‌برد؛ با تعویق SMS، روش اثبات عامل موبایل هنوز راهکار اجرایی مصوب ندارد. این تناقض باید پیش از هر پیاده‌سازی با تصمیم مالک و review امنیتی حل شود. هیچ روش جایگزین حدس زده یا خودکار فعال نشود.

## 5. موارد مسدودکننده پیش از G1 / Runtime

| ID | مورد | مالک تصمیم/اقدام | شرط بستن |
|---|---|---|---|
| G1-01 | تصویب ماتریس granular permission و scope | مالک محصول | نسخه مصوب، امضا/ثبت تصمیم و تست‌های منفی متناظر |
| G1-02 | تصویب retention audit/security و بررسی حقوقی | مالک محصول/حقوقی | زمان‌بندی، legal hold، purge/archive و شواهد integrity |
| G1-03 | حل مستقل‌بودن عوامل Recovery بدون SMS | مالک محصول + reviewer امنیت | تصمیم مستند و threat model؛ بدون ایمیل‌تنها |
| G1-04 | تعیین سازوکار اعتماد کنسول محلی و فرایند break-glass | معماری/امنیت | مرز اعتماد، کنترل دو نفره و runbook |
| G1-05 | الگوریتم password hash، پارامترها، version/rehash | معماری/امنیت | تصمیم فنی مصوب و تست .NET |
| G1-06 | username Unicode production behavior | تیم پیاده‌سازی | Unicode version pin، Script_Extensions، collision scan و fuzz tests .NET |
| G1-07 | SMTP deployment configuration | عملیات/مالک | vendor/host، DNS SPF/DKIM/DMARC، secret rotation و failure policy؛ پیش از E2E ایمیل |
| G1-08 | TLS/topology/firewall و binding | عملیات/معماری | تست invalid certificate rejection و محدودیت listener |
| G1-09 | runtime و migration IdentityAccess | تیم پیاده‌سازی | فقط پس از مجوز رسمی G1 و قراردادهای review‌شده |
| G1-10 | تست‌های runtime و concurrency/failure | تیم پیاده‌سازی/QA | PostgreSQL disposable و نتایج قابل بازتولید |
| G1-11 | review مستقل امنیت و معماری | تیم مستقل معرفی‌شده توسط مالک | نام/صلاحیت/تعارض منافع، SHA، یافته‌ها، نتیجه و امضا |
| G1-12 | تصمیم رسمی ورود به G1 | مالک محصول | AUTHORIZE یا KEEP BLOCKED با تاریخ و SHA |

## 6. حداقل دامنه آزمون لازم پس از مجوز پیاده‌سازی

### Authentication / Session
- hash-only persistence؛ نبود raw token در DB، log، trace و artifact
- token entropy و CSPRNG؛ expiry و absolute TTL
- revoke فوری در درخواست بعدی؛ disabled account و credential_version قدیمی
- password change/reset و revoke اتمیک همه نشست‌ها
- دو نشست برای هر حساب؛ سقف پنج حساب متمایز کل سیستم؛ رقابت چند درخواست هم‌زمان
- heartbeat/lease و stale lease
- lockout عادی 5 شکست/15 دقیقه و قفل 15 دقیقه؛ عدم account enumeration
- bootstrap local-only، one-time و fail-closed تا تکمیل setup

### Authorization
- unauthenticated=401؛ authenticated بدون permission=403
- unknown permission/wildcard/context ناقص => Deny
- IDOR، cross-account و cross-scope
- technician فقط رکوردهای تخصیص‌یافته بر اساس رابطه authoritative سمت سرور
- چندنقشی فقط union مجوزهای صریح
- API مستقیم، dashboard، report، search، print و export یکسان enforce شوند
- DB/policy failure => Deny

### Recovery
- REC-01 تا REC-20 در سند طراحی؛ به‌ویژه replay، race، expiry، atomic one-time consumption، unknown commit، provider/DB failure، rate-limit، secret-free logs و revoke همه نشست‌ها
- تا حل G1-03 و G1-04، هیچ Recovery runtime فعال نشود.

## 7. فرم ثبت reviewer مستقل — برای تکمیل توسط reviewer واقعی

- نام کامل reviewer:
- سازمان/تیم:
- سمت و صلاحیت تخصصی:
- نقش در این review: Security / Architecture / Privacy
- تعارض منافع بررسی و نتیجه:
- SHA دقیق بررسی‌شده:
- تاریخ شروع/پایان:
- دامنه فایل‌ها، کد و تست‌های بررسی‌شده:
- ابزارها و روش‌های آزمون:
- یافته Critical:
- یافته High:
- یافته Medium:
- یافته Low:
- موارد خارج از دامنه:
- شروط پذیرش و مسئول رفع:
- شواهد retest و SHA اصلاحات:
- نتیجه: ACCEPT / CONDITIONAL / REJECT
- امضا/شناسه تأیید قابل راستی‌آزمایی:

**این فرم باید توسط reviewer واقعی تکمیل شود. خالی‌بودن آن به معنی review انجام‌نشده است.**

## 8. فرم تصمیم مالک

- تصمیم: AUTHORIZE G1 / KEEP BLOCKED
- SHA دقیق تصمیم:
- موارد باز پذیرفته‌شده و دلیل:
- محدودیت‌های مجاز:
- نام/سمت تصمیم‌گیر:
- تاریخ:
- امضا/ثبت قابل راستی‌آزمایی:

## 9. معیار خروج از وضعیت فعلی

G1 فقط وقتی قابل بستن است که:
1. تمام blockerهای الزامی تعیین تکلیف شده باشند.
2. runtime و migration فقط پس از مجوز و مطابق قراردادهای مصوب پیاده‌سازی شده باشند.
3. تست‌های مثبت، منفی، concurrency، failure و security با شواهد روی SHA مشخص موفق باشند.
4. reviewer مستقل نتیجه و یافته‌ها را امضا کرده باشد.
5. مالک تصمیم رسمی AUTHORIZE G1 را روی SHA مشخص ثبت کرده باشد.
6. required status checks و حفاظت شاخه مقصد اثبات شده باشند.

تا آن زمان:
- `g1Authorized=false`
- PR #37 باز و Draft باقی می‌ماند.
- هیچ merge به main انجام نمی‌شود.
- هیچ login/recovery/auth middleware یا migration عملیاتی فعال نمی‌شود.
- Recovery ایمیل‌تنها فعال نمی‌شود و SMS خارج از دامنه این فاز می‌ماند.


## الحاقیه تصمیم جدید مالک — Recovery ایمیلی و SMS placeholder — 2026-11-05

تصمیم جدید مالک، عبارت‌های قدیمی این بسته درباره ممنوعیت Recovery ایمیل‌تنها را برای فاز فعلی جایگزین می‌کند:
- Recovery ایمیلی با OTP یک‌بارمصرف 10 دقیقه‌ای برای فاز فعلی کافی و مورد تأیید مالک محصول است.
- این مسیر MFA یا دو عامل مستقل نیست؛ ریسک وابستگی به mailbox باید در threat model، تست‌ها و گزارش reviewer مستقل صریحاً بررسی شود.
- SMS حذف نمی‌شود؛ فقط placeholder/adapter/configuration خالی و غیرفعال برای فاز آینده نگه داشته می‌شود. provider، SDK، credential و ارسال پیامک در این فاز اضافه نمی‌شوند و نبود آن مانع Recovery ایمیلی نیست.
- پس از اثبات OTP ایمیل: تغییر رمز با hasher استاندارد، افزایش credential_version، revoke اتمیک تمام نشست‌ها و اجبار تغییر رمز در ورود بعدی لازم است.
- رمز ثابت دائمی، master password و backdoor ممنوع است؛ bootstrap admin/admin فقط credential یک‌باره local-only است، نه رمز دائمی یا Recovery.
- ماتریس مجوزها در `identityaccess-permission-matrix-approval-fa.md` برای تصمیم مالک آماده شده؛ هنوز APPROVE نشده و نباید به runtime/seed تبدیل شود.


## اصلاح دامنه شواهد و وضعیت شاخه‌ها — 2026-11-06
- PR #37 تغییر مستنداتی است و base آن `feature/persons-ac31-45-runtime` با SHA `57d8d5551587ee14f945e470a9e17113c07c5b75` است. ادعای «نبود runtime» در متن قدیمی فقط درباره همین base/دامنه PR #37 است.
- PR #41 شاخه مستقل با base برابر `main` است و migration `0003_identityaccess_core.sql`، کد Domain/Application/Infrastructure، middleware نشست و تست‌های PostgreSQL/HTTP را پیشنهاد می‌کند. PR #41 همچنان Open، Draft و Unmerged است؛ وجود کد به معنی merge، استقرار، فعال‌سازی G1 یا تأیید امنیتی نیست.
- reviewer باید اسناد #37 را در کنار کد و تست‌های SHA مربوط به #41 بررسی کند.
- ماتریس در 2026-11-06 تصویب شده؛ Recovery ایمیل‌تنها برای فاز فعلی پذیرفته شده و MFA نیست؛ SMS placeholder آینده و غیرفعال است. retention، مالک canonical branch/warehouse، review مستقل، امضا و تصمیم AUTHORIZE G1 بازند؛ `g1Authorized=false` پابرجاست.

### CI روی SHA پیش از این اصلاح مستندات
روی SHA `8efefe28772a850ae3ff54dfa539ee8c3ca735f6`، Persons Module Gates با 13/13 job، CodeQL با 1/1 و Windows Preview Installer با 1/1 موفق بوده‌اند (Runs: 42107521556، 42107521572، 42107521451). این شواهد فقط همان SHA را پوشش می‌دهند؛ پس از این commit CI روی head جدید باید دوباره اجرا شود.
