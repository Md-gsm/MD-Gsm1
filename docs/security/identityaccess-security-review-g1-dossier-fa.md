# پرونده بازبینی امنیتی و مجوز G1
تاریخ 2026-11-04 | آماده بازبینی؛ نه تأییدشده.

مرجع: ADR-0006 opaque token؛ ADR-0012 deployment topology؛ ADR-0013 account/session/bootstrap. IdentityAccess هنوز runtime، login middleware، session store یا migration عملیاتی ندارد. تأیید امنیت/حریم خصوصی گزارش‌شده در 2026-10-31 در پرونده معماری، صرفاً baseline G0 را پوشش می‌دهد؛ این dossier بازبینی مستقل G1 برای Runtime و کنترل‌های IdentityAccess است و آن تأیید قدیمی به آن تعمیم داده نمی‌شود.

| حوزه | شواهد الزامی | وضعیت |
|---|---|---|
| token secrecy/hash/revoke | تست DB/log/artifact و revoke فوری | مسدود؛ runtime نیست |
| password hash/rehash | الگوریتم/پارامتر/version و تست | مسدود |
| lockout/source rate limit | تست پنجره، concurrency، عدم enumeration | مسدود |
| bootstrap admin/admin | local-only، یک‌باره، atomic، fail-closed، no embedded secret | مسدود |
| session cap/lease | تست چندکلاینت و stale lease | مسدود |
| permission | ماتریس مصوب و negative API tests | تصویب مالک لازم |
| recovery | threat model، replay/race tests | مسدود |
| audit/retention | append-only، archive integrity، retention مصوب | مسدود |
| TLS/topology | invalid cert rejection، binding/firewall | review لازم |
| architecture/privacy | review مستقل گروه مشاور و تصمیم مالک | باز |

CI سبز فقط jobهای همان SHA را تأیید می‌کند؛ امنیت runtime یا G1 را اثبات نمی‌کند. نویسنده کد نمی‌تواند review مستقل را به‌جای reviewer واقعی امضا کند.

فرم reviewer: نام/نقش ____؛ SHA ____؛ تاریخ ____؛ دامنه ____؛ یافته‌های Critical/High/Medium/Low ____؛ شروط ____؛ نتیجه ACCEPT/CONDITIONAL/REJECT ____؛ ثبت هویت/امضا ____.
تصمیم مالک: review امنیت/حریم خصوصی ____؛ review معماری گروه مشاور ____؛ Required Status Checks و branch protection ____؛ تصمیم AUTHORIZE G1 / KEEP BLOCKED ____؛ تاریخ/SHA ____.
تا تکمیل واقعی فرم و مجوز صریح: PR #30 Open+Draft+Unmerged، main بدون تغییر، `g1Authorized=false`.
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
