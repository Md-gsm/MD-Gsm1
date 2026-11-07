# آماده‌سازی و دروازه ورود G1 — ماژول اشخاص MD-Gsm

**آخرین تطبیق وضعیت:** 2026-11-08  
**Main baseline پس از PR #51:** `f011c94d52bc06c7e213abb6e480180e9ea73f40`  
**وضعیت:** پیاده‌سازی جزئی؛ G1 مجاز نیست؛ `g1Authorized=false`.  
**مخزن یگانه:** `Md-gsm/MD-Gsm1`.

این سند وضعیت فعلی را از ادعاهای تاریخی G0 جدا می‌کند. سبزشدن CI، وجود Migration یا تکمیل ساختار پروژه به معنی تکمیل ماژول، پذیرش مستقل یا مجوز G1 نیست.

## ۱. ساختار و مرزهای معماری

- پروژه‌های فنی Host و BuildingBlocks در مسیرهای نهایی خود قرار دارند.
- چهار پروژه Contracts برای Persons، IdentityAccess، RepairOperations و Finance به‌صورت اسمبلی‌های مستقل در Solution حضور دارند و API composition آن‌ها را ارجاع می‌دهد.
- Contracts فعلی به‌تنهایی پیاده‌سازی کامل ماژول‌های کسب‌وکاری نیستند.
- مالکیت داده‌ها و مرزها در `docs/architecture/data-ownership.md` و `docs/architecture/modular-target.md` ثبت شده‌اند.
- کد داخلی Persons در حال حاضر در مسیر انتقالی BuildingBlocks و مطابق ADR-0014 قرار دارد؛ این وضعیت نباید به‌عنوان تغییر مالکیت داده تعبیر شود.

## ۲. قابلیت‌های واقعی موجود در main

| حوزه | وضعیت واقعی | مرز ادعا |
|---|---|---|
| هویت Party و مدل دامنه | بخشی پیاده‌سازی شده | به معنی تکمیل همه use caseها نیست |
| Persistence اشخاص | PostgreSQL repository و migration موجود | Migration فقط در دیتابیس disposable اعتبارسنجی شده |
| API جزئیات شخص | `GET /api/v2/persons/{partyId}` پیاده‌سازی شده | endpoint محافظت‌شده؛ فیلد حساس نیازمند مجوز است |
| API ایجاد شخص | ناقص/پیاده‌سازی‌نشده | ثبت عملیاتی از API قابل اعلام نیست |
| جست‌وجوی اشخاص | SearchAsync در Repository وجود دارد؛ API exposed نیست | suffix-4/name، حداکثر ۱۰ نتیجه؛ SQL مقدار موبایل اصلی را برمی‌گرداند و masking/authorization باید پیش از API تکمیل شود |
| Contact Point | بخشی از مدل/قرارداد موجود، چرخه کامل ناقص | افزودن/ویرایش/تأیید/غیرفعال‌سازی و تاریخچه نیازمند تکمیل و تست است |
| UI اشخاص | پیش‌نمایش نمایشی | داده‌ها ساختگی‌اند؛ فرم عملیاتی متصل به API نیست |
| IdentityAccess session/security runtime | بخشی پیاده‌سازی شده | نیازمند تکمیل و بازبینی امنیتی مستقل |
| HTTP login/password change | مسیرهای کد و تست یکپارچه موجود | ورود HTTP feature-gated و به‌صورت پیش‌فرض خاموش است؛ production rollout مجاز نیست |
| Bootstrap مدیر و بازیابی اضطراری | طراحی/تصمیم‌ها ثبت شده، پذیرش اجرایی نهایی باز | credential تولیدی یا recovery عملیاتی تأیید نشده |
| داده موقت | store رمزنگاری‌شده و retention worker به‌صورت partial وجود دارد | alert delivery و AC-31..38 criterion-level acceptance هنوز باز است |
| اجرای Merge اشخاص | coordinator تراکنشی و Finance participant به‌صورت partial وجود دارد | consumer coverage و AC-39..45 کامل/نقشه‌برداری نشده‌اند |
| Migration روی دیتابیس واقعی | انجام نشده | CI فقط disposable PostgreSQL را تغییر داده است |

## ۳. شواهد تاریخی CI پیش از ادغام PR #40

این Run IDها مربوط به main تاریخی پیش از PR #40 با commit `194720455d01a5f88b1996ed9b100b7bc7ec363b` هستند و برای main فعلی شواهد محسوب نمی‌شوند:

| کنترل | Run | نتیجه |
|---|---:|---|
| Persons Module Gates | `42381030785` | ۱۳ از ۱۳ Job موفق؛ شامل PostgreSQL 18 Linux و Windows |
| CodeQL | `42381030784` | موفق |
| Windows Preview Installer | `42381030763` | موفق |
| Project Charter Owner Gate | `42381030735` | موفق |

این شواهد صرفاً تاریخی‌اند. PR #50 و PR #51 پس از آن ادغام شده‌اند. روی main فعلی `f011c94d52bc06c7e213abb6e480180e9ea73f40`، ۱۵/۱۵ check-run موفق است: Persons Gates 13/13، CodeQL و Charter Gate. ۱۴ context از این ۱۵ مورد Required هستند؛ CodeQL موفق ولی غیرالزامی است. Windows Preview Installer روی این SHA اجرا نشده و ساخت Installer به معنی پذیرش تصویری/عملیاتی نیست. PostgreSQL فقط روی دیتابیس disposable اعتبارسنجی شده است.

## ۴. وضعیت گیت‌های G0/G1

| شرط | وضعیت | توضیح |
|---|---|---|
| ساختار Host/BuildingBlocks و Contracts | انجام‌شده | کنترل معماری در CI جاری موفق |
| جهت وابستگی و جداسازی Contracts | تست‌شده | فقط مرز فنی؛ نه تکمیل همه ماژول‌ها |
| سیاست داده موقت | تصویب در سطح سیاست | Runtime و آزمون اجرایی پاک‌سازی باز است |
| چک‌لیست قرارداد Merge | تصویب در سطح معیار | اجرای واقعی Merge و تست مشارکت‌کنندگان باز است |
| کنترل‌های CI روی main | ۱۵/۱۵ check-run موفق؛ ۱۴ context Required | main protected؛ جزئیات کامل endpoint برای این اتصال 403 است. مقادیر review/signature طبق REST readback مالک در `docs/operations/required-status-checks.md` ثبت شده‌اند |
| آزمون منفی معماری | انجام‌شده در PR #6 | شواهد تاریخی شکست کنترل معماری؛ جایگزین کنترل‌های جاری نیست |
| PostgreSQL 18 Linux/Windows و Migration Runner | موفق در CI جاری | فقط دیتابیس‌های disposable؛ بدون تغییر دیتابیس واقعی |
| بازبینی مستقل معماری روی commit جاری | باز/نیازمند مدرک | گزارش قدیمی روی commit قدیمی به‌تنهایی پذیرش commit فعلی نیست |
| بازبینی امنیت و حریم خصوصی روی commit جاری | باز/نیازمند مدرک | سبزشدن CodeQL جایگزین threat-model/security review نیست |
| AC-31..AC-45 و پذیرش runtime | باز/UNMAPPED | تست‌های متمرکز جزئی وجود دارند؛ هیچ criterion-level mapping کامل پذیرفته نشده است |
| پذیرش نهایی UI و نصب | باز | Installer CI موفق است؛ بررسی بصری/کاربری نهایی باید ثبت شود |
| مجوز صریح G1 | صادر نشده | `g1Authorized=false` |

## ۵. IdentityAccess — تصمیم‌ها در برابر اجرا

- ورود فقط username/password است؛ ایمیل و موبایل کانال تماس/بازیابی‌اند.
- سیاست ۵ تلاش ناموفق و قفل ۱۵ دقیقه‌ای برای ورود در ADR-0013 ثبت شده است.
- نشست، lease/heartbeat، سقف حساب‌های فعال و revoke باید سمت سرور و با مرجع PostgreSQL enforce شوند.
- مسیر HTTP login و password change در کد و تست یکپارچه وجود دارد؛ feature flag ورود HTTP به‌صورت پیش‌فرض خاموش است.
- bootstrap مدیر، بازیابی اضطراری، ماتریس مجوز نهایی، retention و production hardening هنوز نیازمند شواهد و بازبینی هستند.
- سیاست بازیابی و ماتریس مجوزهای PR #37 نباید بدون تطبیق با ADR-0013 و تصمیم‌های ثبت‌شده مالک به Runtime منتقل شوند.

## ۶. Migration و داده واقعی

- Migration اشخاص در مخزن موجود است و از Migration Runner متعلق به برنامه اجرا می‌شود.
- تست‌های CI، اجرای اول و اجرای تکراری را روی دیتابیس‌های موقت PostgreSQL 18 در Linux و Windows اعتبارسنجی می‌کنند.
- هیچ Migration اشخاص یا IdentityAccess روی دیتابیس واقعی/تولیدی اجرا نشده است.
- وجود فایل Migration، به‌تنهایی مجوز اجرا یا انتشار نیست.

## ۷. شروط باقی‌مانده پیش از درخواست G1

1. تکمیل و تست APIهای create/search و چرخه کامل Contact Point.
2. تکمیل UI واقعی و اتصال آن به API از مسیر مجاز؛ حذف وابستگی عملیاتی به داده نمایشی.
3. تکمیل IdentityAccess، bootstrap، recovery، revoke و کنترل هم‌زمانی؛ اجرای تست‌های امنیتی منفی و مثبت.
4. پیاده‌سازی runtime پاک‌سازی داده موقت و تست deadline/failure/retry.
5. پیاده‌سازی Merge اشخاص با مشارکت‌کنندگان، atomicity، rollback و idempotency.
6. تکمیل نقشه AC-31..AC-45 با تست‌های واقعی و شواهد commit-bound.
7. بازبینی مستقل معماری و امنیت روی commit مشخص فعلی.
8. ثبت پذیرش Windows UI/installer و آزمون backup/restore در دامنه مربوط.
9. بررسی مجدد Branch Protection و Required Checks با مدرک مستقیم و تازه.
10. تأیید صریح مالک پروژه برای G1؛ هیچ یک از موارد بالا به‌تنهایی این مجوز را ایجاد نمی‌کند.

**قانون توقف:** تا صدور مجوز رسمی، `g1Authorized=false` باقی می‌ماند. این سند هیچ مجوزی برای فعال‌سازی ماژول، اجرای Migration روی دیتابیس واقعی، ساخت credential تولیدی یا انتشار نرم‌افزار صادر نمی‌کند.
