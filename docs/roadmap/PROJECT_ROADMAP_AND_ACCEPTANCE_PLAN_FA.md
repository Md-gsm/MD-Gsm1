# نقشه راه مرجع MD-Gsm1 — فازها و معیارهای پذیرش

**مبنای ممیزی:** 4933cc9abda03568a23979e3ac25a085981ff84e | **تاریخ:** 2026-11-08 | **منشور:** GOV-001 | **G1:** g1Authorized=false

## قواعد
شماره فازها دقیقاً ۰ تا ۱۲ مطابق منشور است. ترتیب ۷ تا ۱۱ فقط با دلیل وابستگی و تأیید صریح مالک تغییر می‌کند. مالک پاسخ‌گو مالک پروژه است؛ مالک دامنه‌ای در جدول آمده؛ نام فردی بدون انتصاب رسمی فرض نشده. زمان‌بندی TBD تا decomposition و تخمین ظرفیت. Desktop، عملیات، امنیت و data migration workstream عرضی‌اند.

## وضعیت مبنا
آخرین `main` در ممیزی فعلی: `4933cc9abda03568a23979e3ac25a085981ff84e` (ادغام PR #54). روی همین merge commit، ۱۳ از ۱۵ check-run موفق، صفر شکست و دو مورد در حال اجرا هستند: `persons-postgres-windows-migrations` و `codeql-csharp`. `windows-preview-installer` موفق شده است. این وضعیت تا تکمیل دو check باقی‌مانده، CI سبز نهایی محسوب نمی‌شود. خود PR #54 روی head SHA `83b551683b0dbd7fa8bc383ec4c21469e0e45149` با ۱۸/۱۸ check-run موفق ادغام شد؛ این شواهد head را نباید با check-runهای merge commit یکی دانست. PostgreSQL فقط روی دیتابیس‌های disposable آزموده شده است. PR #52، #53 و #54 روی main ادغام شده‌اند؛ PRهای بسته/شاخه‌های تاریخی دیگر به‌معنای کار فعال نیستند. در لحظه ممیزی هیچ PR بازی وجود نداشت. فهرست کامل تاریخچه PRها همچنان باید به‌صورت جداگانه با base/head/merge SHA/review/CI آرشیو شود.

## فازهای مصوب

| فاز | وضعیت | مالک دامنه‌ای | پیش‌نیاز | خروجی و معیار خروج |
|---|---|---|---|---|
| ۰ حاکمیت | ✅ منشور، CODEOWNERS، PR template، Charter Gate | Owner/Governance | — | منشور مصوب، کنترل مالک‌محور، registry و branch protection evidence |
| ۱ ممیزی اشخاص | 🟡 ممیزی انجام شده؛ PR archive کامل نیست | Architecture/QA | ۰ | PR/branch/base/SHA، کد در برابر docs، وابستگی و ریسک؛ side merge به‌عنوان main ثبت نشود |
| ۲ قرارداد و مالکیت | 🟡 PartyId UUIDv7، natural/legal، Contracts 2.0؛ ADR7..10 مفقود | Persons/Architecture | ۱ | Party/ContactPoint/Address/roles، mobile-first، قراردادها و consumers؛ تصمیم مفقود فقط پس از تصویب مجدد |
| ۳ تکمیل Persons | 🟡 repository ثبت/QuickRegister/SearchAsync دارد؛ API search و UI عملیاتی ناقص | Persons/Desktop | ۲ و IdentityAccess | CRUD/lifecycle، search/merge، ContactPoint/Address، API، authorization، SQL/DTO masking، audit و تست منفی/هم‌زمانی |
| ۴ آزمون و بازبینی | 🟡 CI سبز، پذیرش نهایی نیست | QA/Security/Independent Reviewer | ۳ | Unit/DB/API/Contract/Security/E2E، migration/restore، multi-user، review گزارش‌دار روی SHA دقیق و بدون مانع بحرانی/بالا |
| ۵ پذیرش اشخاص | ⬜ NOT_READY | Owner/QA/Security | ۴ | bootstrap/recovery، grants، backup/restore، Windows install/visual test، پذیرش و دستور صریح مالک برای G1 |
| ۶ Platform Core/Organization | ⬜ | Platform/Organization | ۵ یا scope مستقل مصوب | config/lifecycle، Branch/Warehouse، membership/scope؛ Catalog/UOM/Pricing قبل از ۷/۱۰ مالک و قرارداد بگیرند |
| ۷ Inventory | ⬜ | Inventory | ۶، Catalog/UOM و قرارداد مالی | دفتر گردش، رسید/حواله/انتقال/شمارش/رزرو، valuation، اتمیک، concurrency، restore، Organization |
| ۸ Accounting | ⬜ | Accounting | ۷ و قرارداد اثر مالی/دوره | کدینگ، Fiscal Period، Journal/Lines، توازن، immutable docs، reversal/correction، idempotency و reconciliation |
| ۹ Treasury | ⬜ | Treasury | ۸ | صندوق/بانک/چک، دریافت/پرداخت، تخصیص/استرداد، reconciliation و دفترکل؛ بدون منطق موازی |
| ۱۰ Sales/Purchasing | ⬜ | Sales/Purchasing | ۶..۹، Persons، Inventory، Accounting، Treasury، Pricing | سفارش/فاکتور/رسید خرید/Returns، snapshot، اثر اتمیک و idempotency؛ Tax integration با مالک/scope مصوب |
| ۱۱ RepairOperations | 🟡 Contracts تنها | RepairOperations | Persons/Inventory/Treasury/Accounting/Catalog/Pricing و canonical assignment | پذیرش/پرونده/IMEI، lifecycle، skill/assignment، مصرف قطعه، دریافت، تحویل/گارانتی/صورتحساب؛ مالک assignment پیش‌شرط |
| ۱۲ ERP Integration | ⬜ | Architecture/QA/all module owners | ۶..۱۱ | E2E خرید→موجودی→بدهی/پرداخت/دفترکل؛ فروش→خروج/مالیات/دریافت؛ تعمیر→قطعه/تسویه/تحویل؛ Returns، دوره، چندکاربره و DR |

Catalog/Pricing فاز مستقل در منشور نیست؛ جایگاه آن پیش از ۷/۱۰ باید تصویب شود. Payroll/CRM/Reporting خارج از فازهای مصوب‌اند تا تصمیم مالک برسد. Returns به سند مبدأ متصل است. Tax integration نیاز قانونی است اما scope/owner باید تصویب شود.

## Workstreamهای عرضی
- **Desktop/فارسی — Desktop/Product:** Shell/navigation/login/first-run، API wiring، RTL، اعداد/ورودی فارسی، تقویم جلالی، ریال، خطا و keyboard flow. خروج: UI عملیاتی متصل به API، بدون synthetic data، تست UI و اجرای بصری Windows. پوسته تأییدشده redesign نشود.
- **Operations — Platform/Release + DBA + Security:** config، secret sourcing/rotation، TLS، redacted logs، monitoring/alerts، health، installer/upgrade/uninstall، Authenticode، rollback/runbook. پیش از داده واقعی backup/restore روی DB مجزا و نصب/ارتقای Windows روی SHA دقیق.
- **Data migration/Cut-over — Data Migration + Persons/Inventory/Accounting + Owner:** فقط export داده‌ای مجاز؛ هیچ کد/schema/migration/script/binary قدیمی وارد نمی‌شود. profiling، mapping، پاکسازی، dry-run ناشناس، reconciliation تعداد/جمع/مانده/IMEI، freeze، delta export، cut-over، rollback و تأیید کتبی.
- **Supply chain — Security/Admin:** Secret Scanning، Push Protection، Dependabot security updates، CodeQL، dependency review/SBOM، package licensing، provenance و امضای installer. طبق audit مالک سه کنترل اول خاموش‌اند؛ commit signature enforcement نیز خاموش؛ Admin readback لازم.
- **Independent review:** reviewer واجد صلاحیت و مستقل از نویسنده و owner تصمیم؛ review آخرین SHA؛ گزارش file/line، severity، impact، reproduction، test و resolution. «ok/1» یا approval خالی کافی نیست؛ بدون reviewer واقعی گیت باز.

## یافته‌های ممیزی
| ID | شدت | شاهد/ریسک | اقدام |
|---|---|---|---|
| F-01 | 🔴 | PartyReference constructor فقط Guid.Empty را رد می‌کند؛ existence را ثابت نمی‌کند. Finance provisioner فعلی قبل side effect validator را در تراکنش صدا می‌زند. | OPEN: تمام call-siteها validator قبل side effect و تست منفی. |
| F-02..04 | 🟠 | در PR35 متن یافته‌ها بازیابی نشد؛ review thread/comment قابل دسترس نبود. | BLOCKED: گزارش ممیز لازم؛ حدس ممنوع. |
| FIN-02 | 🔴 | AC39..41 UNMAPPED؛ Migration0006 فقط شناسه حساب Party، نه ledger/balance. | OPEN: mapping criterion-level؛ Finance=PARTIAL. |
| PER-01 | 🟠 | SearchAsync suffix-4/max10؛ SQL مقدار موبایل اصلی را برمی‌گرداند؛ API exposed نیست. | OPEN: SQL/DTO mask، auth، rate-limit، audit و عدم افشای PII. |
| DB-01 | 🟠 | فقط IdentityAccess runtime grants؛ Persons/Finance grant matrix ندارند. | OPEN: least privilege و تست مثبت/منفی. |
| ARC-01 | 🟠 | Persons internals در BuildingBlocks؛ Module folders README-only؛ IdentityAccess persistence نیز BuildingBlocks. | OPEN: relocation و architecture tests. |
| API-01 | 🟠 | identity/system info v1، Persons v2؛ docs قدیمی v1/persons. | OPEN: version policy/compatibility/OpenAPI؛ route فعلی v2. |
| ADR-01 | 🟠 | ADR7..10 در main نیست؛ PR11 closed/unmerged. | OPEN: بازیابی و تصویب مجدد suffix-4/rate-limit/idempotency/concurrency. |
| MIG-01 | 🟡 | Migration0002 در subfolder؛ logical resource filename-based. | OPEN: globally unique filenames و collision test؛ applied migration جابه‌جا نشود. |
| SEC-01 | 🟡 | IPrivacyDigest در Program و helper ثبت می‌شود؛ Program helper را صدا نمی‌زند؛ HMAC key برای Login لازم. | WATCH: composition root واحد، DI test و secret rotation. |
| GOV-01 | 🟠 | branch protection script فهرست ۱۲ check؛ live main چهارده context. | OPEN: script/tests sync؛ Apply تا dry-run ممنوع. |
| GOV-02 | 🟠 | approvals کم‌محتوا سابقه دارند. | معیار review تعریف شد؛ اجرای واقعی باید ثابت شود. |
| SCM-01 | 🟠 | Secret scanning/Push Protection/Dependabot updates طبق audit خاموش؛ signatures خاموش. | OPEN: Admin readback و مجوز مالک. |
| ROAD-01 | 🔴 | ترتیب/شماره roadmap قبلی با منشور مغایر. | اصلاح به ۰..۱۲؛ تغییر ترتیب فقط تصمیم مالک. |
| ROAD-02 | 🟠 | Desktop/Operations/migration بدون مالک/معیار. | workstream و معیار خروج اینجا تعریف شد. |
| ROAD-03 | 🟡 | AC46..49 ممکن بود runtime تلقی شوند. | CI policy/evidence فقط؛ نه AC31..45. |
| PR-01 | 🟠 | PR32 unmerged؛ PR35/#37 side merge؛ PR34/#47 superseded. | base branch کنار merged state ثبت شود. |

## PRهای کلیدی و مقصد
| PR | وضعیت | Base | اثر |
|---|---|---|---|
| #13 | merged | main | governance docs |
| #32 | closed/unmerged | feature/persons-domain-core-c23bd326 | وارد main نشد |
| #35 | merged | fix/persons-review-findings-20261105 | side، نه main |
| #37 | merged | feature/persons-ac31-45-runtime | side، نه main |
| #34 | closed/unmerged | Finance side | superseded |
| #40/#41/#42/#43 | merged | main | Persons/IdentityAccess hardening |
| #45/#46 | merged | main | docs/evidence |
| #47 | closed/unmerged | main | superseded by #50 |
| #48/#49 | merged | main | policy/evidence |
| #50 | merged | main | partial runtime + migrations 0006–0008 |
| #51 | merged | main | docs reconcile؛ evidence JSONها هنوز drift داشتند |

این تاریخچه گزیده است؛ PR1..51 باید با base/head/merge SHA/review/CI جداگانه export شود.

## تصمیم‌های باز
Organization مالک Branch/Warehouse و membership/scope؛ Inventory مالک quantity/movement/transfer. RepairOperations مالک skill/scheduling/technician assignment؛ assignment از security role یا Persons استنتاج نشود. IdentityAccess: bootstrap/recovery، SMTP/OTP، revoke، legal retention، high-risk financial thresholds و secret rotation باز. 12h TTL و دو نشست/account را مصوب فرض نکنید. Mobile اولویت جست‌وجوی مالک؛ کد ملی کلید اصلی UX نیست و باید ADR شود. Staff/Employee/Technician هویت، استخدام و skill/assignment جدا هستند. Tax rates باید versioned/configurable باشند.

## برگه پیش از هر فاز
مالک پاسخ‌گو/داده؛ scope؛ dependencies/contracts؛ acceptance criteria؛ positive/negative/concurrency/idempotency/recovery؛ تست Unit/DB/API/UI/Security/E2E؛ migration/forward recovery؛ permissions/secrets/privacy/retention؛ installer/monitoring/backup/restore؛ reviewer؛ PR/SHA/CI؛ تخمین effort/confidence پس از decomposition؛ risk و تصمیم مالک؛ شواهد و امضای پذیرش.

## ممنوعیت‌ها
عدم استفاده از artifacts پروژه قدیمی؛ عدم migration واقعی/Production؛ عدم تغییر مستقیم main/force push/delete branch؛ عدم تغییر ترتیب منشور؛ عدم COMPLETE با کد/CI تنها؛ عدم G1 بدون دستور صریح مالک.


## اجرای WG-001 پس از ادغام — ثبت وضعیت 2026-11-08

**مبنای اجرا:** main در SHA `4933cc9abda03568a23979e3ac25a085981ff84e`؛ WG-001 در PR #54 ادغام شده است. این بخش برنامه اجرایی را از نقشه راه فازهای مصوب جدا نمی‌کند و هیچ ترتیب یا مالکیت منشور را تغییر نمی‌دهد.

### وضعیت فعال‌سازی گروه‌ها
- شناسه‌های WG نقش‌های کاری و مرزهای مسئولیت هستند؛ این ثبت به‌تنهایی انتصاب فردی ایجاد نمی‌کند. نام فردی برای WG-0 تا WG-5 یا WG-QA فرض/اضافه نشده است.
- در زمان ممیزی PR بازی وجود نداشت. شاخه‌های قدیمی/بسته حذف نمی‌شوند؛ قبل از هر استفاده باید base، head، آخرین commit، اختلاف با main و PR مقصد بررسی شود.
- WG-0 هماهنگ‌کننده ترتیب و ثبت وضعیت است؛ مالک منطق دامنه گروه‌ها نمی‌شود.
- اجرای فازهای بعدی ERP تا عبور Persons از پذیرش و صدور مجوز صریح مالک، جلو نمی‌رود. `g1Authorized=false` بدون تغییر باقی می‌ماند.

### بسته‌های کاری اولویت‌دار

| شناسه | گروه پاسخ‌گو | محدوده و خروجی قابل پذیرش | وابستگی/گیت توقف |
|---|---|---|---|
| WG0-01 | WG-0 | ثبت وضعیت واحد main/PR/branch/checkها و نگهداری همین سند با SHA و شواهد تازه؛ آرشیو کامل PRها با base/head/merge SHA، review و CI | هیچ branch حذف یا force-push نمی‌شود؛ تغییر main فقط از PR و با مجوز صریح |
| WG1-01 | WG-1 با هماهنگی WG-0 | تکمیل traceability معیارهای AC-31..AC-49: برای هر معیار، وضعیت واقعی، فایل/کلاس، تست مثبت/منفی، شواهد اجرا و SHA؛ معیار فاقد پیاده‌سازی صریحاً OPEN بماند | نقشه فعلی هنوز تکمیل criterion-level را تأیید نمی‌کند؛ تست‌های متمرکز جایگزین پوشش کامل نیستند |
| WG1-02 | WG-1 | تکمیل چرخه create/search اشخاص و ContactPoint/Address، قرارداد API، authorization، حداقل‌سازی DTO و masking داده تماس، rate-limit/audit و تست‌های منفی/هم‌زمانی | مالک Party و داده تماس فقط Persons؛ مجوز و نشست از IdentityAccess؛ هیچ کپی منطق یا جدول موازی |
| WG1-03 | WG-1 + WG-0 | تعیین و اجرای برنامه انتقال internals اشخاص از BuildingBlocks به مسیر ماژولی مطابق ADR-0014، همراه با تست‌های مرزی و بدون تغییر قرارداد مصرف‌کننده مگر با تصمیم ثبت‌شده | مسیر فعلی طبق معماری، transitional است؛ جابه‌جایی migrationهای اعمال‌شده یا تغییر قرارداد بدون تأیید ممنوع |
| WG2-01 | WG-2 | بستن شکاف‌های IdentityAccess شامل bootstrap مدیر، recovery، revoke، ماتریس مجوز، نشست/هم‌زمانی و سخت‌سازی production؛ هر سیاست جدید ابتدا ADR/تصمیم مالک | HTTP login فعلاً feature-gated است؛ سیاست‌های TTL/تعداد نشست/Recovery را حدس نزنید |
| WG3-01 | WG-3 پس از آماده‌شدن API | اتصال UI واقعی Persons به APIهای پذیرفته‌شده، حذف synthetic data از مسیر عملیاتی، حفظ پوسته تأییدشده، تست UI و مشاهده بصری Windows | تا تثبیت قرارداد API و مجوز سمت سرور، UI فقط مصرف‌کننده است و منطق دامنه/DB نمی‌سازد |
| WG4-01 | WG-4 | تکمیل شواهد نصب/ارتقا/حذف Windows، TLS/secrets/logging، backup/restore روی DB جدا و runbook؛ ثبت artifact و SHA | Installer CI موفق به‌تنهایی آزمون بصری/کاربری یا backup/restore نیست؛ هیچ DB واقعی یا تنظیم زنده GitHub بدون مجوز |
| WGQA-01 | WG-QA در نقش کنترل؛ مالک پروژه برای پذیرش | بازبینی معماری/امنیت/قرارداد روی آخرین SHA و گزارش مستدل شامل scope، فایل/خط، شدت، اثر، بازتولید، تست و resolution | approval کوتاه یا CI سبز به‌تنهایی گزارش بازبینی مستقل یا پذیرش نیست |
| G1-01 | مالک پروژه + QA + مالکان دامنه | ارزیابی یکپارچه همه شروط G1، پذیرش Windows و backup/restore، و تصمیم صریح درباره فعال‌سازی | تا دستور صریح مالک، `g1Authorized=false`؛ هیچ Migration روی دیتابیس واقعی اجرا نشود |

### ترتیب اجرای مجاز و مسیرهای موازی
1. **ابتدا WG-0** وضعیت دقیق main و پیش‌نیازها را تثبیت می‌کند؛ در کنار آن WG-1 و WG-2 می‌توانند فقط روی محدوده‌های مستقل و قراردادهای تثبیت‌شده کار کنند.
2. **WG-1 و WG-2** مسیرهای ضروری برای Persons را جلو می‌برند؛ تغییرات مشترک Host، Persistence، Contracts و build با هماهنگی WG-0 و مالکان متأثر انجام می‌شود.
3. **WG-3** پس از تثبیت قراردادهای create/search و ContactPoint وارد اتصال عملیاتی می‌شود؛ پوسته تصویب‌شده redesign نمی‌شود.
4. **WG-4** می‌تواند آزمون‌های عملیاتی مستقل را جلو ببرد، اما هیچ تغییر تولیدی/DB واقعی/Branch Protection را بدون مجوز اجرا نمی‌کند.
5. **WG-QA** از ابتدا معیار و تست را تعریف می‌کند و هر نتیجه را فقط روی SHA دقیق می‌سنجد؛ بازبینی نهایی پس از تغییرات جدید باید دوباره انجام شود.
6. **WG-5** فقط تحلیل مالکیت و قرارداد Catalog/UOM/Pricing را انجام می‌دهد؛ runtime و فازهای وابسته تا تصمیم رسمی مالک `BLOCKED` می‌مانند.
7. فازهای ۶ تا ۱۲ طبق پیش‌نیازهای جدول فازها و منشور جلو می‌روند؛ این ثبت مجوز شروع Inventory، Accounting، Treasury، Sales/Purchasing یا RepairOperations نیست.

### معیار پایان هر بسته
هر بسته باید مالک داده و قرارداد، دامنه فایل، تست‌های واقعی، شواهد commit-bound، وضعیت مثبت/منفی و محدودیت‌های باقی‌مانده را در PR ثبت کند. پایان بسته فقط با build/CI اعلام نمی‌شود. PR تا دستور صریح مالک Draft می‌ماند و ادغام تنها با CI کامل سبز روی آخرین SHA، بازبینی معتبر و مجوز صریح انجام می‌شود.
