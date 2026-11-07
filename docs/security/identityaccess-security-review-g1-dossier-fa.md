# پرونده امنیتی IdentityAccess — وضعیت فنی برای بازبینی انسانی

تاریخ: 2026-11-04  
مخزن: Md-gsm/MD-Gsm1  
PR: #31 (Open + Draft + Unmerged)  
SHA مبنای این پرونده: 5b8669df31de283c8a83ac9fe1e74381ac1a5986

## هدف و محدودیت

این پرونده خلاصه شواهد فنی موجود و آزمون‌های لازم برای بازبین امنیتی را گردآوری می‌کند. این سند هیچ تأیید امنیتی/حریم خصوصی را جعل نمی‌کند و مجوز G1 صادر نمی‌کند.

## اجزای پیاده‌سازی‌شده در شاخه

- Domain و Application handlers/ports برای ورود، تغییر رمز، reset مدیریتی و heartbeat.
- EF Core entity configurations و DbSetهای IdentityAccess.
- PostgreSQL repositories و transactional Unit of Work.
- password hasher مبتنی بر ASP.NET Core Identity.
- opaque token factory با entropy تصادفی 256-bit و digest ذخیره‌سازی.
- تست Integration روی PostgreSQL disposable برای lockout، heartbeat، revoke و سقف ورود هم‌زمان.
- CI روی commit پیشین 18797d3f209c371adfd652cb767c1ddb7bf1b145 موفق بوده است؛ commit این پرونده تغییر مستنداتی دارد و باید CI همان SHA خودش را نیز سبز کند.

## قابلیت‌هایی که عمداً فعال نشده‌اند

- Login HTTP endpoint و token issuance از طریق HTTP هنوز اضافه نشده است؛ middleware احراز نشست برای مسیرهای Persons اکنون عملیاتی است.
- Listener شبکه و binding تولیدی.
- Migration عملیاتی IdentityAccess و اتصال به دیتابیس واقعی.
- bootstrap credential یا حساب عملیاتی admin/admin.
- recovery endpoint/flow اجرایی.
- فعال‌سازی G1 یا Persons.

## یافته‌ها و آزمون‌های لازم

| موضوع | وضعیت | معیار پذیرش بازبین |
|---|---|---|
| Username | NFC و uppercase invariant و heuristic script group؛ هنوز نسخه Unicode و Script_Extensions/CaseFolding pin نشده | داده رسمی نسخه‌دار، collision scan، تست‌های .NET و fuzz؛ تا آن زمان غیرفعال |
| Password hashing | primitive موجود | پارامترها، versioning، rehash، timing و عدم افشای وجود حساب |
| Opaque token | token factory و digest موجود | raw token در DB/log/artifact صفر؛ revoke فوری؛ disabled account رد |
| Login lockout | lockout و تست PostgreSQL موجود | 5/15/15، source rate limit مستقل، concurrency و پاسخ عمومی |
| Session | lease و سقف حساب‌های متمایز | چندپردازه، stale lease، heartbeat race، revoke و ظرفیت |
| Authorization | permission catalog و adapter زنده وصل است؛ API برای مسیر Persons توکن را به نشست معتبر PostgreSQL متصل می‌کند و actor فقط از principal سمت سرور می‌آید. ماتریس طبق تصمیم‌نامه هنوز proposal است | تصویب مالک، seed نسخه‌دار، تست PostgreSQL و negative API tests، row scope تکنسین با داده تخصیص معتبر |
| Recovery | `IRecoveryStore` و PostgreSQL adapter برای create/consume challenge و consume یک‌باره recovery code وجود دارند؛ orchestration/endpoint و اثبات دوکاناله end-to-end وجود ندارد | threat model و تست expiry/replay/race/DB-failure/atomic consumption و اثبات دو کانال مستقل |
| Bootstrap | local-only و fail-closed در تصمیم | آزمون setup یک‌باره، منع network تا تکمیل، عدم وجود secret ثابت |
| Audit/privacy | مدل audit و writer persistence وجود دارد؛ retention عددی هنوز proposal است و append-only DB privilege/purge عملیاتی اثبات نشده | تصویب مالک/بررسی حقوقی، append-only با role محدود، allow-list و تست حذف secret/PII و purge |
| Schema | EF model و قیود موجود؛ migration عملیاتی وجود ندارد | بازبینی FK/index/check/uniqueness و سازگاری mapping با PostgreSQL 18 |
| TLS/topology | ADR-0012 | رد certificate نامعتبر، binding صریح، firewall و عدم اتصال مستقیم Desktop به DB |

## خروجی مورد انتظار از بازبینی

بازبین باید روی همین SHA ثبت کند:
- هویت/نقش بازبین و دامنه بازبینی؛
- یافته‌ها با severity و مسیر بازتولید؛
- ACCEPT / CONDITIONAL / REJECT؛
- مواردی که به علت نبود runtime قابل آزمون نیستند؛
- تاریخ و شناسه گزارش.

مالک پروژه باید بررسی امنیت و حریم خصوصی را جداگانه ثبت کند. گروه مشاور باید بازبینی معماری را جداگانه ثبت کند. نویسنده کد یا CI نمی‌تواند جای این تأییدها را بگیرد.

## حفاظت

PR #31 باز و Draft و ادغام‌نشده می‌ماند؛ main بدون تغییر؛ `g1Authorized=false`. هرگونه migration تولیدی، endpoint ورود، listener شبکه و credential bootstrap تا مجوز مستقل ممنوع است.
## وضعیت تصمیم‌های مالک و برنامه بازبینی تیم مستقل — 2026-11-05

مالک محصول پارامترهای Recovery و Session را تأیید کرده است: بازار ایران؛ mailbox اختصاصی دامنه محصول و SMTP relay قابل تعویض؛ Email OTP=10 دقیقه؛ SMS OTP=5 دقیقه؛ پنج تلاش ناموفق و قفل 24 ساعته؛ resend هر یک ساعت، سقف یک ارسال در ساعت و پنج ارسال در 24 ساعت، قفل 24 ساعته پس از عبور؛ heartbeat=30s، lease=180s، absolute TTL=12h، دو نشست/account، پنج حساب فعال متمایز سراسری با امکان تغییر توسط super_admin؛ تغییر رمز و غیرفعال‌سازی موجب revoke فوری تمام نشست‌ها.

SMS provider و SMTP vendor/host واقعی هنوز انتخاب/پیکربندی نشده‌اند. این موضوعات باید پیش از آزمون end-to-end Recovery بسته شوند.

مالک اعلام کرده است پس از بررسی نهایی بسته توسط مالک، یک تیم مستقل آن را بررسی و تأیید خواهد کرد. تا زمان معرفی تیم/اعضای reviewer، بررسی روی SHA مشخص، ثبت یافته‌ها، نتیجه رسمی و امضای واقعی، هیچ تأیید مستقلی ثبت نشده است. این سند جای امضا را پر نمی‌کند و g1Authorized=false باقی می‌ماند.

### چک‌لیست تحویل به تیم مستقل
- SHA دقیق commit و نسخه تمام اسناد/کد تحت بررسی؛
- threat model و معماری اعتماد، topology/TLS و مرزهای ماژول؛
- آزمون‌های Recovery شامل replay/race/expiry/rate-limit/provider failure/DB unknown commit؛
- آزمون‌های Session شامل absolute expiry، دو نشست/account، سقف کل، disable/password revoke و concurrency؛
- آزمون‌های API authorization، IDOR، scope، fail-closed و audit؛
- گزارش یافته‌ها با severity، مالک اصلاح و شواهد retest؛
- نتیجه ACCEPT / CONDITIONAL / REJECT، نام و سمت reviewerها، تاریخ و امضای واقعی.


## تعیین دامنه فعلی: عدم پیاده‌سازی پیامک — 2026-11-05
- بنا بر تصمیم مالک، در این مرحله هیچ SMS provider، SDK، API client، credential، template، configuration، background job یا تست وابسته به سرویس پیامک اضافه نمی‌شود.
- قابلیت SMS به مرحله/نسخه آینده موکول است و در این مرحله صرفاً به‌عنوان توسعه آتی مستند می‌شود؛ انتخاب vendor نیز اکنون لازم نیست.
- نبود SMS به معنی مجاز شدن بازیابی با ایمیل تنها نیست. تا زمان تصویب و پیاده‌سازی یک ترکیب عامل بازیابی امن و آزموده‌شده، Recovery اضطراری در محیط عملیاتی غیرفعال/غیرقابل دسترس می‌ماند. عامل offline recovery code نیز به‌تنهایی جایگزین عامل مستقل دوم نمی‌شود مگر آنکه مدل تهدید و سیاست مالک جداگانه تصویب شود.
- مقادیر TTL پیامک و محدودیت ارسال پیامک که پیش‌تر در سند آمده‌اند، مشخصات هدف آینده‌اند و در این مرحله هیچ رفتار اجرایی یا تست runtime برای SMS ایجاد نمی‌کنند.
- ایمیل اختصاصی نیز در این مرحله فقط معماری هدف است؛ SMTP relay واقعی و secretها تا انتخاب/پیکربندی عملیاتی وارد برنامه نمی‌شوند.


## الحاقیه شواهد جاری و اصلاح ارجاع PR — 2026-11-05

پرونده تحویل جاری در `docs/security/identityaccess-g1-review-handoff-fa.md` ثبت شده است. مرجع جاری این پرونده PR #37 با SHA `26562482b09e55f0750fb2264574a701916e1c74` است؛ ارجاع‌های قدیمی به PR #30 در متن‌های قبلی، وضعیت فعلی شاخه/PR را نشان نمی‌دهند و نباید برای تصمیم جاری استفاده شوند.

روی همین SHA سه workflow زیر completed/success شده‌اند: Persons Module Gates (Run 42098140813؛ 13/13 jobs)، CodeQL (Run 42098140790؛ 1/1 job) و Windows Preview Installer (Run 42098140826؛ 1/1 job). این شواهد فقط CI همان SHA را تأیید می‌کنند و تأیید runtime یا G1 نیستند.

PR #37 باز، Draft و Unmerged است و base آن `feature/persons-ac31-45-runtime` است. main توسط این PR تغییر نکرده است. reviewer مستقل، امضا و تصمیم رسمی AUTHORIZE G1 هنوز ثبت نشده‌اند؛ `g1Authorized=false` باقی می‌ماند.


## وضعیت شواهد و تصمیم‌ها روی head فعلی PR #37 — 2026-11-06
- head پیش از این اصلاح مستندات: `8efefe28772a850ae3ff54dfa539ee8c3ca735f6`.
- CI روی همین SHA موفق بوده است: Persons Module Gates Run 42107521556 (13/13)، CodeQL Run 42107521572 (1/1)، Windows Preview Installer Run 42107521451 (1/1). این نتایج به commit اصلاحی بعدی منتقل نمی‌شوند و باید روی head جدید دوباره اجرا شوند.
- ماتریس granular توسط مالک تصویب شده؛ runtime/seed همچنان gated است.
- Recovery ایمیل‌تنها برای فاز فعلی تصویب شده؛ MFA نیست و mailbox compromise باید مستقل review شود. SMS placeholder آینده و غیرفعال است.
- PR #41 جدا از PR #37، کد و migration IdentityAccess را به‌عنوان integration candidate پیشنهاد می‌کند؛ Open/Draft/Unmerged است. وجود کد در آن، merge یا G1 approval نیست.
- retention امنیتی، مالک canonical branch/warehouse، review مستقل روی SHA دقیق، یافته‌ها، امضا و تصمیم AUTHORIZE G1 بازند؛ `g1Authorized=false` باقی است.
