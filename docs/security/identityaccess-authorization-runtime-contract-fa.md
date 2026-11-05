# قرارداد اجرایی Authorization برای IdentityAccess

- وضعیت: قرارداد طراحی بر پایه ماتریس مصوب مالک محصول
- تاریخ: 2026-11-06
- مرجع نقش‌ها و grants: identityaccess-permission-matrix-approval-fa.md
- دامنه این تغییر: مستندات و قرارداد آزمون؛ هیچ runtime، middleware، endpoint، migration، seed یا تغییر DB ایجاد نمی‌کند.
- گیت اجرایی: g1Authorized=false؛ اجرای کد فقط پس از review مستقل G1 و مجوز اجرایی صریح مجاز است.

## 1. اصول الزام‌آور

1. Default Deny؛ فقط permission key دقیق و grant صریح می‌تواند وارد ارزیابی scope شود.
2. Role، permission، scope و مالکیت منبع چهار مفهوم مستقل‌اند.
3. Scope به ازای هر permission است؛ ارث‌بری و wildcard وجود ندارد.
4. چندنقشی فقط union grantهای صریح همان permission است.
5. all فقط scope صریح همان permission است و bypass قواعد دامنه، audit یا separation-of-duties نیست.
6. Scope یا resource ID ارسالی client، اثبات مجوز نیست.
7. خطای policy، metadata ناقص، permission ناشناخته، timeout یا خطای منبع authoritative هرگز Allow نمی‌شود.
8. Read، create، update، approve، post، reverse، export، print، assign و revoke مجوزهای مستقل‌اند.
9. حذف قطعی اسناد مالی/انبار ممنوع؛ اصلاح از مسیر reversal/correction است.
10. Super Admin نیز مشمول audit، کنترل جداسازی وظایف و invariantهای دامنه است.

## 2. کاتالوگ permissionهای ثابت

کلیدها case-sensitive، یکتا و version-controlled هستند. کلید ناشناخته یا غیرفعال Deny است.

**اشخاص:** persons.read, persons.create, persons.update, persons.archive, persons.merge, persons.export

**تعمیرات:** repairs.read, repairs.create, repairs.update, repairs.assign, repairs.complete, repairs.worklog.write, repairs.parts.consume, repairs.deliver, repairs.cancel, repairs.export

**انبار:** inventory.read, inventory.item.create, inventory.item.update, inventory.receipt.create, inventory.issue.create, inventory.transfer.create, inventory.count.execute, inventory.adjust.approve, inventory.export

**فروش و خرید:** sales.read, sales.create, sales.price.override, sales.discount.approve, sales.refund.approve, purchasing.read, purchasing.create, purchasing.approve

**صندوق و مالی:** cash.read, cash.receipt.create, cash.payment.create, cash.reconcile, finance.read, finance.voucher.create, finance.voucher.post, finance.voucher.reverse, finance.period.close, finance.period.reopen, finance.export

**داشبورد و گزارش:** dashboard.read, dashboard.configure.self, dashboard.assign, reports.read, reports.export, reports.print

**هویت و سیستم:** identity.accounts.read, identity.accounts.create, identity.accounts.update, identity.credentials.reset, identity.roles.assign, identity.permissions.manage, identity.sessions.revoke, identity.sessions.settings.manage, identity.recovery.execute, system.settings.read, system.settings.manage, audit.security.read

هر تغییر در کاتالوگ نیازمند نسخه جدید ماتریس، تغییر قرارداد، seed plan و آزمون‌های مثبت/منفی است. permission از نام route یا ورودی client به‌صورت حدسی ساخته نمی‌شود.

## 3. Scope و داده‌های ارزیابی

| Scope | معنی | منبع اثبات |
|---|---|---|
| self | رکورد متعلق به principal جاری | ماژول مالک |
| assigned | تخصیص فعال به principal | رابطه تخصیص authoritative |
| branch:<uuid> | شعبه مشخص | assignment سمت سرور |
| warehouse:<uuid> | انبار مشخص | assignment سمت سرور |
| all | کل دامنه همان permission | grant صریح همان permission |

Scope grant منطقی شامل role_id، permission_key، scope_kind، scope_id، granted_by، granted_at_utc و reason است. scope_id برای branch/warehouse اجباری و برای self/assigned/all تهی است. ترکیب نامعتبر یا شناسه خارج از assignment مجاز Deny است.

Principal هر درخواست protected باید از PostgreSQL وضعیت account enabled، credential_version، session revoked، lease، absolute expiry، credential_version_at_issue، roleهای فعال و grants جاری را resolve کند. Cache یا claim قدیمی بدون revalidation مجاز نیست.

Resource context از ماژول مالک می‌آید و در صورت کاربرد شامل resource_type/id، owner_account_id، branch_id، warehouse_id، assigned_account_ids، document_state و fiscal_period_state است. اطلاعات client مرجع این context نیست.

## 4. الگوریتم قطعی ارزیابی

ترتیب مراحل ثابت است:

1. Authentication/session: account فعال، session معتبر و revoke نشده، lease و absolute TTL معتبر و credential version برابر نسخه جاری. شکست: 401.
2. Bootstrap gate: تا تکمیل bootstrap فقط عملیات محدود تغییر رمز اولیه و تکمیل پروفایل مجاز؛ business API بسته.
3. Endpoint metadata: permission ثابت، resource type، audit class و scope resolver مشخص. metadata ناقص/ناشناخته Deny و خطای پیکربندی.
4. Grant: حداقل یک grant صریح برای همان permission از roleهای فعال لازم است؛ نبود آن 403.
5. Scope union: فقط scopeهای grantشده برای همان permission union می‌شوند.
6. Resource resolution: context از ماژول مالک resolve شود؛ خطا یا عدم قطعیت Deny.
7. Domain invariants: تخصیص، وضعیت سند، دوره مالی، سقف مالی، موجودی و separation-of-duties جداگانه بررسی شوند.
8. Query/write: scope در query اعمال شود؛ بارگیری همه داده و فیلتر در حافظه ممنوع. برای write، check و mutation تا حد امکان در یک transaction با concurrency guard باشند.
9. Audit: عملیات حساس با actor، subject، permission، outcome، reason code داخلی، correlation ID و timestamp ثبت شوند؛ بدون password، OTP، token، secret یا payload خام.
10. Response: وجود account/resource، مالک، شعبه و جزئیات policy افشا نشود.

### 4.1 چندنقشی

برای permission معین P، دامنه مجاز union تمام grantهای صریح P در roleهای فعال است. Scope سایر permissionها هرگز اضافه نمی‌شود. نقش inactive یا grant inactive بی‌اثر است. branch A در یک role و branch B در role دیگر برای همان permission، فقط A∪B می‌دهد. all برای P1 مجوز P2 را توسعه نمی‌دهد. در این نسخه deny override تعریف نشده؛ نبود grant در یک role، grant صریح role دیگر را حذف نمی‌کند.

### 4.2 پاسخ‌های استاندارد

- 401: session مفقود/نامعتبر/revoked/expired، account غیرفعال یا credential version mismatch.
- 403: principal معتبر اما grant یا شرط عمومی permission موجود نیست.
- 404 یکسان: resource وجود ندارد یا خارج از scope است، هرجا افشای وجود ممنوع باشد.
- 503 عمومی: DB یا منبع authoritative unavailable؛ fail-closed و بدون داده.
- 500: فقط خطای غیرمنتظره؛ بدون exception detail در response.

## 5. نقش‌های مصوب و guardهای scope

کاتالوگ نقش‌ها دقیقاً شامل super_admin، manager، receptionist، technician، warehouse، salesperson، purchaser، cashier، accountant و viewer است. Grants دقیق هر نقش فقط از ماتریس مصوب می‌آید؛ خانه خالی به معنی نبود grant است.

- technician: تعمیرات فقط assigned و داده شخصی فقط self؛ بدون all. repair ID ارسالی client مالکیت را ثابت نمی‌کند.
- manager: فقط branchهای صریح تخصیص‌یافته؛ manager بودن مجوز مدیریت IdentityAccess نیست.
- warehouse: فقط warehouse/branch تخصیص‌یافته؛ مشاهده فهرست انبارها مجوز دیدن همه موجودی نیست.
- receptionist: داده عملیاتی branch و grants صریح؛ archive/merge به‌صورت پیش‌فرض Deny.
- salesperson، purchaser، cashier و accountant: branch/warehouse/cashbox/fiscal-period فقط از assignment authoritative؛ نقش به‌تنهایی scope نمی‌سازد.
- viewer: فقط read grants صریح و scope محدود؛ هیچ write/approve ضمنی ندارد.
- super_admin: all فقط برای permissionهای صریح ماتریس؛ بدون bypass دامنه و audit.

## 6. کنترل‌های دامنه‌ای

### Persons
persons.read مجوز create/update/archive/merge/export نیست. party ID ورودی اثبات مالکیت نیست. export مجوز مستقل، scope مستقل و audit می‌خواهد. archive/merge باید duplicate و linked records را بررسی کند.

### Repairs
تکنسین فقط assignment فعال authoritative را می‌خواند/تغییر می‌دهد. repairs.complete، repairs.worklog.write و repairs.parts.consume مستقل‌اند. مصرف قطعه باید هم permission تعمیر و هم scope انبار را پاس کند. تغییر assignment فقط از مسیر repairs.assign و ماژول مالک است.

### Inventory
هر گردش کالا branch و warehouse مبدا/مقصد را server-side resolve می‌کند. transfer باید scope هر دو طرف را پاس کند. inventory.count.execute با inventory.adjust.approve یکی نیست؛ maker نباید همان تعدیل حساس را approve کند. موجودی، رزرو و concurrency invariantهای مستقل‌اند.

### Sales / Purchasing / Cash
sales.price.override، sales.discount.approve، sales.refund.approve و purchasing.approve فقط با permission و سقف مالی مصوب اجرا می‌شوند. تا زمان تعیین و نسخه‌گذاری threshold، هر عملیات با آستانه نامعلوم Deny است. ثبت‌کننده و تأییدکننده باید جدا باشند. cash.receipt.create، cash.payment.create و cash.reconcile مستقل‌اند.

### Finance
finance.voucher.create، finance.voucher.post، finance.voucher.reverse، finance.period.close و finance.period.reopen مستقل‌اند. post فقط در دوره باز و سند معتبر؛ reverse با ارجاع به سند اصلی و audit. period.reopen فقط grant صریح ماتریس. maker نباید voucher خودش را post کند. هیچ finance.write یا admin عمومی ساخته نشود.

### Dashboard / reports / export
dashboard.read مجوز داده widget نیست؛ هر widget مجوز و scope منبع را جداگانه اجرا می‌کند. dashboard.assign مجوز تغییر grants نیست. search/list/report/print/export باید predicate scope یکسان داشته باشند. export permission مستقل است.

### IdentityAccess / Audit
account، role assignment، permission catalog، session revoke و session settings هرکدام permission مستقل می‌خواهند. کاربر نمی‌تواند خودش را ارتقا دهد. تغییر دسترسی super_admin نیز audit می‌شود. Audit append-only و فاقد secrets/OTP/raw token است.

## 7. Endpoint metadata

هر endpoint protected باید metadata ثابت داشته باشد:
- PermissionKey دقیق از کاتالوگ
- ResourceType یا None
- AuditClass از enum ثابت
- ScopeResolverKey برای resource type
- RequiresOpenFiscalPeriod برای عملیات مالی مربوط
- RequiresMakerChecker برای عملیات حساس

Endpoint بدون metadata باید در startup/contract validation fail شود یا fail-closed غیرفعال بماند. صرف حضور در route group امنیت ایجاد نمی‌کند.

## 8. قرارداد آزمون پذیرش

این‌ها باید به تست‌های واقعی .NET/API/PostgreSQL تبدیل شوند؛ این سند اجرای تست را اثبات نمی‌کند.

### Catalog / grants
1. permission ناشناخته یا case/whitespace mismatch → Deny.
2. endpoint بدون metadata → fail-closed.
3. role بدون grant → 403.
4. persons.read بدون persons.update، درخواست update → 403.
5. role/grant غیرفعال → Deny.
6. seed دارای role/permission/grant خارج از ماتریس → fail.
7. نبود principal → Deny.

### Scope / IDOR
8. technician با assignment فعال repair را می‌خواند → Allow.
9. همان technician repair خارج از assignment را با ID دستکاری‌شده می‌خواند → 404 یکسان.
10. assignment قبل از commit لغو شود → mutation Deny.
11. branch A به branch B دسترسی ندارد.
12. warehouse A به موجودی/transfer warehouse B دسترسی ندارد.
13. client مقدار scope=all بفرستد بدون grant all → Deny.
14. branch/warehouse ID جعلی → Deny.
15. دو role با branch A و B برای همان permission → فقط A∪B.
16. all برای P1، P2 را توسعه ندهد.
17. resource missing و out-of-scope در endpointهای محافظت‌شده پاسخ غیرقابل‌تمایز داشته باشند.
18. خطای resolver/DB → 503 fail-closed.

### Domain invariants
19. creator همان voucher را post کند → Deny.
20. maker همان inventory adjustment را approve کند → Deny.
21. approve مالی بدون threshold مصوب → Deny.
22. مبلغ بالاتر از threshold → checker مجاز لازم؛ نبود approver → Deny.
23. finance.voucher.post در دوره بسته → Deny.
24. period.reopen بدون grant صریح → Deny.
25. hard delete سند قطعی مالی/انبار → Deny.
26. super_admin بدون permission دقیق → Deny.

### Data channels
27. widget بدون مجوز منبع هیچ داده‌ای برنگرداند.
28. search/list/report/print/export برای principal یکسان predicate scope داشته باشند.
29. export بدون permission مستقل → Deny.
30. PII خارج scope در CSV/PDF/print ظاهر نشود.
31. query پیش از materialization به scope محدود باشد.

### Session / concurrency / audit
32. session revoked/expired، account disabled یا credential version mismatch → 401.
33. تغییر grant بین دو درخواست در درخواست بعدی دیده شود.
34. revoke هم‌زمان نباید پس از نقطه linearization درخواست جدیدی را Allow کند.
35. تغییر assignment هم‌زمان با write در transaction دوباره بررسی شود.
36. DB outage/timeout/exception در هر مرحله → Allow نشود.
37. audit رویداد حساس دقیق و بدون secret/OTP/token باشد.

### Exhaustive matrix
38. برای هر 10 role × هر permission، assertion خودکار مطابق ماتریس اجرا شود.
39. برای هر grant، fixture مثبت و fixture خارج scope منفی اجرا شود.
40. هیچ grant خارج از ماتریس seed نشود.
41. تغییر ماتریس بدون تغییر golden tests باعث fail شدن CI شود.

## 9. ابهام‌ها و شروط باقی‌مانده

1. سقف مالی price override/discount/refund/purchase approval هنوز مشخص نشده؛ تا آن زمان Deny.
2. branch/warehouse assignment و قرارداد resolver باید توسط data owner تعریف شود؛ IdentityAccess مالک داده عملیاتی branch/warehouse نیست.
3. retention و دسترسی audit باید جداگانه تصویب شود.
4. تعریف فیلدهای self در Persons و گزارش تکنسین باید با data owner تأیید شود.
5. مرز transaction و linearization بین API و ماژول‌های مالک باید با PostgreSQL test اثبات شود.
6. هر permission/role/scope تازه یا استثناء maker-checker نیازمند ماتریس مصوب جدید است.

## 10. معیار خروج از طراحی به اجرا

- [ ] ماتریس و این قرارداد روی SHA مشخص ثبت و checksum معتبر داشته باشند.
- [ ] معماری مستقل، data-owner contract و scope resolverها review شوند.
- [ ] reviewer مستقل G1 با نام/نقش، SHA بررسی‌شده، یافته‌ها، تصمیم و امضای واقعی ثبت کند.
- [ ] مالک مجوز صریح اجرای G1 را برای همان SHA بدهد.
- [ ] CI required checks و branch protection همان SHA تأیید شوند.
- [ ] 41 آزمون فوق و آزمون‌های concurrency/failure در PostgreSQL disposable اجرا شوند.
- [ ] هیچ Critical/High باز نماند مگر پذیرش ریسک مستند و امضاشده.

**نتیجه:** ماتریس از نظر سیاست محصول مصوب است؛ قرارداد اجرایی در این سند تدوین شده؛ runtime implementation و تست runtime هنوز انجام نشده و G1 بسته است.
