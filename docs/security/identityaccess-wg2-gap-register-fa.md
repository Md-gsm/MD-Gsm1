# رجیستر شکاف‌های WG-2 — IdentityAccess (بسته WG2-01)

| مشخصه | مقدار |
|---|---|
| گروه مسئول | WG-2 — IdentityAccess |
| بسته نقشه راه | WG2-01 در `docs/roadmap/PROJECT_ROADMAP_AND_ACCEPTANCE_PLAN_FA.md` |
| تاریخ بازنگری | 2026-11-17 |
| مبنای بررسی | `main` در SHA `17a0526b5c42ab1a28d8ba3731af5fb595b52692` |
| نوع سند | رجیستر وضعیت و شکاف‌های مستند؛ نه ADR، نه مجوز runtime یا G1 |
| `g1Authorized` | false (بدون تغییر) |

## ۱. دامنه و محدودیت‌های این سند

- این سند فقط وضعیت واقعی کد و اسناد را در محدوده WG-2 ثبت می‌کند: `src/Modules/IdentityAccess/**`، `src/BuildingBlocks/MDGsm.Infrastructure/IdentityAccess/**`، `src/Host/Api/Security/**`، `database/migrations/0003_identityaccess_core.sql`، `database/operations/**` و `docs/security/identityaccess-*.md`.
- هیچ سیاست، TTL، سقف، روش Recovery یا مقدار امنیتی جدیدی را مصوب نمی‌کند. هر مورد دارای برچسب «نیازمند تصمیم» باید پیش از پیاده‌سازی به‌صورت تصمیم/ADR ثبت شود.
- **روش بازنگری 2026-11-17:** ادعاهای این رجیستر با خواندن مستقیم کد، migration، grants و تست‌ها در main SHA فوق تطبیق داده شده‌اند. این بازنگری اجرای build، تست .NET، PostgreSQL یا تست امنیتی انجام نداده است؛ CI قبلی PR روی SHA قدیمی به‌تنهایی شواهد head جدید محسوب نمی‌شود.
- این سند جایگزین بازبینی مستقل WG-QA یا فرم G1 نیست.

برچسب‌ها: **[تصمیم]** نیازمند تصمیم مالک؛ **[قابل‌انجام]** با سیاست‌های مصوب فعلی قابل پیاده‌سازی؛ **[وابسته]** وابسته به گروه/مالک دیگر؛ **[سندی]** اصلاح مستندات.

## ۲. آنچه در `main` وجود دارد (با ارجاع)

| قابلیت | شاهد |
|---|---|
| ورود HTTP و تغییر رمز، پشت فلگ `IdentityAccess:HttpLoginEnabled` با پیش‌فرض خاموش | `src/Host/Api/Program.cs` |
| توکن opaque ۲۵۶ بیتی و ذخیره فقط SHA-256 | `IdentityAccessSecurityAdapters.cs`، migration 0003 |
| قفل حساب ۵ شکست/۱۵ دقیقه و قفل ۱۵ دقیقه؛ محدودیت مبدأ مکمل | `Account` در `IdentityAccessPrimitives.cs`، `LoginHandler` |
| سقف ۵ حساب فعال متمایز با قفل ردیف singleton | `IdentityAccessUnitOfWork.cs`، `LoginHandler` |
| تمدید lease در هر درخواست محافظت‌شده و رد نشست revoke/منقضی/credential_version قدیمی | `SessionAuthenticationMiddleware.cs`، `PostgresSessionIdentityResolver.cs` |
| revoke همه نشست‌ها در تغییر رمز و reset مدیر؛ افزایش credential_version | `ChangeOwnPasswordHandler`، `AdminResetPasswordHandler` |
| audit فقط‌افزودنی با trigger | migration 0003 |
| grantهای حداقلی نقش runtime | `database/operations/identityaccess-runtime-role-grants.sql` |
| کاتالوگ مجوز و ماتریس مصوب در Domain | `PermissionCatalog.cs` |

ماتریس permission در 2026-11-06 تصویب شده است. `PermissionCatalog.cs` کاتالوگ دامنه را نگه می‌دارد؛ seed دیتابیس و golden test کامل برای کل ماتریس هنوز وجود ندارد (P-02). مقایسه دستی قبلی به‌تنهایی اثبات برابری کامل نیست.

## ۳. رجیستر شکاف‌ها

### ۳.۱ Bootstrap مدیر اولیه

| شناسه | شکاف | شاهد | نوع |
|---|---|---|---|
| B-01 | هیچ کد bootstrap وجود ندارد؛ نقش runtime اجازه INSERT روی `identity.accounts` یا `account_roles` ندارد. | grants: «No account INSERT is granted»؛ نبود هر handler مرتبط | [تصمیم] [قابل‌انجام پس از تصمیم] |
| B-02 | ADR-0013 عبارت تاریخی `admin/admin` و local-only را ثبت کرده، اما همان ADR هر credential ثابت در source/config/installer/migration/log را ممنوع می‌کند؛ تست `BootstrapInvariantTests.FixedPairIsAbsent` این جفت را در `src`، `installer`، `database`، `tools` و `scripts` رد می‌کند. تصمیم محصول ایجاد حساب اولیه در نصب و اجبار تغییر username/password/profile در اولین ورود است؛ روش امن تولید و تحویل credential هنوز تعیین نشده و نباید admin/admin را به‌عنوان پیاده‌سازی تأییدشده تلقی کرد. | ADR-0013؛ `identityaccess-recovery-api-authorization-design-fa.md` بخش ۵.۶؛ تست نام‌برده | [تصمیم] |
| B-03 | migration 0003 هیچ seed برای نقش‌ها/مجوزها/انتساب ندارد؛ محتویات هیچ دیتابیس زنده‌ای در این ممیزی خوانده نشده است. `HasPermissionAsync` از جدول‌های `roles`/`permissions` می‌خواند، بنابراین تا seed کنترل‌شده و تأییدشده، runtime نمی‌تواند grantهای مورد انتظار را resolve کند. مسیر انتساب super_admin به bootstrap تعریف نشده است. | migration 0003 (سرتیتر)؛ `PostgresIdentityAccessStores.HasPermissionAsync` | [تصمیم] روش و زمان seed |
| B-04 | state machine bootstrap (Uninitialized … Completed) فقط طراحی است؛ ستون `bootstrap_completed_at_utc` وجود دارد ولی خوانده/نوشته نمی‌شود و عملیات عادی تا تکمیل setup fail-closed نیست. | `identityaccess-schema-design-fa.md`؛ `MdGsmDbContext.cs` | [قابل‌انجام پس از B-02] |

### ۳.۲ بازیابی مدیر (Recovery)

تصمیم جاری مالک: Recovery ایمیل‌تنها (OTP یک‌بارمصرف ۱۰ دقیقه‌ای، digest-only، ۵ تلاش، قفل ۲۴ ساعته، resend حداقل هر ۱ ساعت، ۱ ارسال در ساعت و ۵ در ۲۴ ساعت). این روش MFA نیست و SMS فقط placeholder غیرفعال است.

| شناسه | شکاف | شاهد | نوع |
|---|---|---|---|
| R-01 | فقط primitiveهای ذخیره‌سازی (`IRecoveryStore`: ساخت challenge، مصرف challenge، مصرف recovery code) وجود دارد؛ orchestration، endpoint و handler وجود ندارد. | `IdentityAccessPorts.cs`، `PostgresIdentityAccessStores.cs`؛ ثبت DI در `PersistenceRegistration.cs` | [قابل‌انجام] |
| R-02 | شمارنده ۵ تلاش، قفل ۲۴ ساعته، cooldown و سقف ارسال (rolling 1h/24h)، محدودیت per-source و لاگ ارسال پیاده‌سازی نشده‌اند و schema ستون/جدول لازم را ندارد (`recovery_challenges` فقط: شناسه، account، digest، channel، created/expires/consumed/revoked). | migration 0003 | نیازمند Migration جدید؛ [وابسته] WG-0 برای رزرو شماره |
| R-03 | پورت و adapter ارسال ایمیل وجود ندارد؛ SMTP relay، دامنه و secretها هنوز انتخاب نشده‌اند. | `docs/security/identityaccess-owner-decisions-fa.md` | [وابسته] WG-4/عملیات؛ [تصمیم] |
| R-04 | تراکنش نهایی Recovery (مصرف OTP، تغییر رمز، افزایش credential_version، revoke اتمیک همه نشست‌ها، `must_change_password`، audit) پیاده‌سازی نشده؛ اجزای آن در `Account.ChangePassword` و `RevokeAllForAccountAsync` موجودند. | `IdentityAccessHandlers.cs` | [قابل‌انجام] |
| R-05 | idempotency و `operation_id` طراحی شده ولی نه در schema هست نه در کد. | `identityaccess-schema-design-fa.md` | Migration جدید |
| R-06 | **سطح شروع Recovery تعیین نشده است:** سندهای قدیمی «فقط کنسول محلی» می‌گویند و تصمیم نهایی ایمیل‌تنها است؛ اینکه شروع از endpoint شبکه‌ای (صفحه ورود) است یا فقط کنسول محلی، و تعریف «کنسول محلی مورد اعتماد» (G1-04) باز است. | recovery-design بخش ۱ و REC-01؛ g1-review-handoff G1-04 | [تصمیم] |
| R-07 | ایمیل حساب فقط ستون DB (`identity.accounts.email`) است؛ در `Account` دامنه نیست، اثبات مالکیت ایمیل وجود ندارد و مشخص نیست چه کسی و چه زمانی آن را برای super_admin ثبت می‌کند (تکمیل پروفایل در bootstrap). | migration 0003؛ `Account` | [تصمیم] |
| R-08 | `AdminResetPasswordHandler` حساب super_admin را رد می‌کند (`denied_super_admin_recovery_required`)؛ یعنی super_admin فقط از مسیر Recovery قابل بازیابی است و تا آماده‌شدن R-01..R-07 هیچ مسیر بازیابی ندارد. | `AdminResetPasswordHandler` | اطلاعات برای تصمیم |

### ۳.۳ نشست‌ها

| شناسه | شکاف | شاهد | نوع |
|---|---|---|---|
| S-01 | **تناقض سندی درباره سیاست نشست** (جزئیات در بخش ۴). کد با اصلاحیه 2026-11-07 هماهنگ است: بدون عمر مطلق و بدون سقف ۲ نشست برای هر حساب. | بخش ۴ | [سندی] |
| S-02 | `SessionHeartbeatHandler` و `AdminResetPasswordHandler` پیاده‌سازی شده‌اند ولی در `Program.cs` ثبت DI نشده‌اند و endpoint ندارند (فقط `LoginHandler` و `ChangeOwnPasswordHandler` ثبت شده‌اند). | `Program.cs` | [قابل‌انجام] |
| S-03 | logout وجود ندارد: `RevokeByDigestAsync` هیچ فراخوان‌ندهای ندارد. | جست‌وجوی مصرف‌کننده‌ها در `src` | [قابل‌انجام] |
| S-04 | handler غیرفعال‌سازی حساب با revoke اتمیک تمام نشست‌ها وجود ندارد (`Account.SetEnabled` هست ولی مصرف‌کننده ندارد)؛ سیاست مصوب این revoke را الزامی کرده است. | `IdentityAccessPrimitives.cs` | [قابل‌انجام] |
| S-05 | middleware فقط مسیرهای `/api/v2/persons` و `/api/v1/identity/password/change` را احراز هویت می‌کند؛ بقیه مسیرها پیش‌فرض anonymous‌اند، در حالی که قرارداد پس از G1 «احراز هویت پیش‌فرض برای همه routeهای application» می‌خواهد. | `SessionAuthenticationMiddleware.cs`؛ recovery-design بخش ۴ | [قابل‌انجام] پشت فلگ؛ فعال‌سازی gated |
| S-06 | lease فقط با درخواست محافظت‌شده تمدید می‌شود و heartbeat مستقل ندارد؛ کلاینت بیکار بیش از ۱۸۰ ثانیه نشستش را از دست می‌دهد. | middleware؛ نبود endpoint heartbeat | [قابل‌انجام] |
| S-07 | سقف rate-limit مبدأ در کد ثابت است (۲۰ شکست غیرموفق در ۵ دقیقه) و در هیچ تصمیم مالک عدد ندارد. | `PostgresIdentityAccessStores.IsSourceRateLimitedAsync` | [تصمیم] |
| S-08 | تغییر سقف ۵ حساب توسط super_admin ممکن نیست: نقش runtime فقط ستون `updated_at_utc` را در `system_settings` می‌تواند به‌روز کند و permission `identity.settings.manage` مصرف‌کننده ندارد. | grants؛ `PermissionCatalog.cs` | [قابل‌انجام] با طراحی grant جدید؛ [وابسته] WG-4/DBA |
| S-09 | `PlatformPasswordHasher.Verify` نتیجه `SuccessRehashNeeded` را موفق می‌پذیرد ولی rehash انجام نمی‌شود؛ الگوریتم و پارامترهای نهایی هنوز مصوب نیستند. | `IdentityAccessSecurityAdapters.cs`؛ G1-05 | [تصمیم] |
| S-10 | بررسی mixed-script در `Username` ضعیف است: هر اسکریپتی جز لاتین و عربی/فارسی در یک گروه قرار می‌گیرد (مثلاً ترکیب سیریلیک و یونانی رد نمی‌شود)؛ سیاست مصوب Script_Extensions، Unicode version pin و collision scan می‌خواهد. | `Username.GetScriptGroup`؛ G1-06 | [قابل‌انجام] ؛ آزمون fuzz/collision لازم |

### ۳.۴ مجوزها

| شناسه | شکاف | شاهد | نوع |
|---|---|---|---|
| P-01 | فقط یک مسیر مجوز واقعی می‌گیرد (`GET /api/v2/persons/{partyId}`) و بررسی داخل خود endpoint انجام می‌شود؛ policy evaluator مرکزی و metadata مجوز endpoint وجود ندارد. | `Program.cs` | [قابل‌انجام] |
| P-02 | تست golden که ماتریس مصوب را قفل کند وجود ندارد؛ قرارداد اجرایی می‌خواهد تغییر ماتریس بدون تغییر golden باعث fail شدن CI شود. | `tests/**/IdentityAccess/` (فقط `SensitivePersonsPermissionCatalogTests` یک مجوز را بررسی می‌کند) | [قابل‌انجام] |
| P-03 | scopeهای self/assigned/branch/warehouse منتظر مالک canonical داده شعبه/انبار/تخصیص هستند؛ تا آن زمان تکنسین برای مسیر Persons fail-closed و 403 است. | `Program.cs`؛ runtime-contract بخش ۹ | [وابسته] WG-1 و مالک دامنه |
| P-04 | سقف‌های مالی (price override، تخفیف، استرداد، تأیید خرید) تعریف نشده‌اند؛ تا آن زمان Deny. | runtime-contract بخش ۹ | [تصمیم] |
| P-05 | handlerهای مدیریت هویت وجود ندارند: ساخت/ویرایش حساب، انتساب نقش، مدیریت مجوز، لغو نشست توسط مدیر. نقش runtime روی جدول‌های نقش/انتساب فقط SELECT دارد. | `PermissionCatalog.cs` (کلیدها)؛ grants | [قابل‌انجام] با طراحی grant؛ [وابسته] WG-4/DBA |
| P-06 | اتصال حساب ورود به Party از طریق قرارداد Persons در محدوده WG-2 تعریف شده، ولی schema حساب‌ها ستون یا ارجاع Party ندارد. | Playbook بخش WG-2؛ migration 0003 | [وابسته] WG-1: تعیین نسخه قرارداد و نیاز در این فاز |

## ۴. تطبیق تناقض‌های سندی نشست و Recovery

اصلاحیه 2026-11-07 در ADR-0013 و تصمیم‌های نهایی مالک می‌گویند عمر مطلق ثابت و سقف ۲ نشست/حساب مصوب نشده‌اند؛ سقف ۵ حساب فعال و 30s/180s سیاست جاری است. این PR متن اسناد WG-2 زیر را **در محل اصلی** بازنویسی کرده و عبارت‌های متناقض را صریحاً تاریخی/superseded علامت زده است:

| سند | بخش | وضعیت |
|---|---|---|
| `identityaccess-five-open-items-fa.md` | خلاصه جاری و بخش‌های ۴، ۶ و ۷ | در محل اصلی بازنویسی شد |
| `identityaccess-recovery-api-authorization-design-fa.md` | مدل Recovery، session، API و بخش‌های تاریخی | در محل اصلی بازنویسی شد |
| `identityaccess-g1-review-handoff-fa.md` | وضعیت جاری، بخش ۴.۱/۴.۲ و blockerها | snapshot تاریخی مشخص و سیاست جاری اصلاح شد |
| `identityaccess-schema-design-fa.md` | session schema، bootstrap و Recovery schema | مدل قدیمی با وضعیت Migration 0003 تطبیق داده شد |

متن ADR-0013 در `docs/decisions/` متعلق به WG-0 است و این PR آن را تغییر نمی‌دهد؛ اصلاحیه خود ADR و اسناد تصمیم مالک، مبنای سیاست جاری هستند. در اسناد WG-2، مدل سه‌عاملی ایمیل+موبایل+کد آفلاین و جدول پیشنهادی `recovery_channel_proofs` دیگر به‌عنوان طراحی جاری معرفی نمی‌شوند. Migration 0003 فقط `recovery_challenges` با `CHECK(channel='email')` و جدول `recovery_codes` را دارد؛ جدول دوم به معنی فعال بودن provisioning یا الزام offline code نیست.

## ۵. درخواست تصمیم و اطلاعات

| شناسه | درخواست / وضعیت | مخاطب |
|---|---|---|
| Q-01 | provisioning امن حساب bootstrap: نصب باید حساب اولیه بسازد و اولین ورود را به تغییر username/password/profile مجبور کند؛ اما عبارت تاریخی `admin/admin` با منع credential ثابت در source/config/installer تعارض دارد. روش امن تولید و تحویل credential هنوز مشخص نیست. | مالک محصول + WG-0/WG-2 |
| Q-02 | نقطه شروع Recovery (endpoint شبکه‌ای یا کنسول محلی مورد اعتماد)، مرز اعتماد کنسول در صورت انتخاب، و ثبت/اثبات ایمیل super_admin (R-06، R-07). سیاست ایمیل‌تنها قبلاً تصویب شده و موضوع این پرسش نیست. | مالک محصول + امنیت |
| Q-03 | روش/زمان seed کنترل‌شده نقش‌ها و grantهای ماتریس مصوب، مالک seed و مسیر migration/rollback؛ تصویب ماتریس انجام شده اما seed مجاز نشده است. | WG-0/WG-2 |
| Q-04A | تعیین تکلیف حد source rate-limit فعلی (20 شکست ناموفق در 5 دقیقه) و اینکه این threshold policy یا مقدار قابل تنظیم باشد؛ کنترل ضد DoS نیز باید مشخص شود. | مالک محصول + امنیت |
| Q-04B | تعیین پارامترهای password hash، سیاست version/rehash و رفتار `SuccessRehashNeeded`؛ کد فعلی IdentityV3 PBKDF2 را استفاده می‌کند اما نتیجه rehash را صرفاً موفق تلقی می‌کند. | معماری/امنیت + مالک برای policy |
| Q-05 | **حل‌شده:** سیاست نشست 2026-11-07 نهایی است: برنامه باز + heartbeat/lease، سقف 5 حساب فعال متمایز، heartbeat=30s و lease=180s؛ absolute TTL و سقف 2 نشست/account تصویب نشده‌اند. | تصمیم مالک ثبت شده؛ سؤال جدید لازم نیست |
| Q-06 | رزرو شماره Migration برای ستون‌ها/جدول‌های Recovery (R-02، R-05)؛ شماره 0009 در رجیستری AVAILABLE است، نه RESERVED؛ رزرو رسمی پیش از هر SQL الزامی است. | WG-0 |
| Q-07 | تعیین نیاز/نسخه قرارداد اتصال حساب به Party (P-06) و احراز مالک canonical شعبه/انبار/تخصیص (P-03) با شواهد schema/code. | WG-1 و مالکان دامنه |
| Q-08 | مدیریت secret کلید HMAC، پیکربندی SMTP، TLS و طراحی grantهای ادمین (S-08، P-05، R-03)؛ این‌ها وابستگی عملیاتی/DBA هستند و نباید با تصمیم محصول جایگزین شوند. | WG-4 و DBA |
| Q-09 | تعیین reviewer مستقل واجد صلاحیت، تعارض منافع، SHA و ثبت یافته/امضا برای WG-2 و G1. | WG-QA + مالک |

## ۶. ترتیب کار پیشنهادی

1. **این PR:** تکمیل رجیستر با شواهد main، رفع تناقض‌های سیاستی اسناد و به‌روزرسانی checksum؛ بدون تغییر runtime/schema/ADR و بدون تغییر `g1Authorized`.
2. **پس از Q-01 و Q-02 و بازبینی امنیتی:** طراحی و پیاده‌سازی bootstrap و Recovery ایمیل‌تنها، با endpoint/console تصمیم‌گرفته‌شده، SMTP، rate-limit و تراکنش اتمیک.
3. **پس از Q-03 و رزرو رسمی 0009 توسط WG-0:** migration/seed لازم با rollback و تست disposable؛ هیچ شماره‌ای پیش از رزرو مصرف نشود.
4. **بسته‌های قابل‌انجام مستقل:** logout/heartbeat registration و endpoint، revoke هنگام disable، golden permission tests، اصلاح Username طبق Script_Extensions، rehash طبق سیاست مصوب؛ هرکدام پشت gate و با تست مثبت/منفی/concurrency.
5. **پس از Q-07:** قرارداد scope/Party و مالکیت canonical در ماژول‌های مربوط؛ تا آن زمان scope نامعلوم fail-closed.

هیچ‌یک از مراحل بالا مجوز اجرای Migration روی دیتابیس واقعی، فعال‌سازی ورود HTTP یا تغییر `g1Authorized` نیست.
