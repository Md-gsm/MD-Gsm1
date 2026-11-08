# نقشه راه مرجع MD-Gsm1 — فازها و معیارهای پذیرش

**مبنای ممیزی:** f011c94d52bc06c7e213abb6e480180e9ea73f40 | **تاریخ:** 2026-11-08 | **منشور:** GOV-001 | **G1:** g1Authorized=false

## قواعد
شماره فازها دقیقاً ۰ تا ۱۲ مطابق منشور است. ترتیب ۷ تا ۱۱ فقط با دلیل وابستگی و تأیید صریح مالک تغییر می‌کند. مالک پاسخ‌گو مالک پروژه است؛ مالک دامنه‌ای در جدول آمده؛ نام فردی بدون انتصاب رسمی فرض نشده. زمان‌بندی TBD تا decomposition و تخمین ظرفیت. Desktop، عملیات، امنیت و data migration workstream عرضی‌اند.

## وضعیت مبنا
۱۵/۱۵ check-run موفق روی SHA دقیق؛ ۱۴ context Required. persons-spec-lint الزامی است؛ codeql-csharp موفق اما non-required. [Persons Gates 42411846550](https://github.com/Md-gsm/MD-Gsm1/actions/runs/42411846550) (13/13)، [CodeQL 42411846619](https://github.com/Md-gsm/MD-Gsm1/actions/runs/42411846619)، [Charter 42411846673](https://github.com/Md-gsm/MD-Gsm1/actions/runs/42411846673). Installer روی این SHA اجرا نشده؛ PostgreSQL فقط disposable. PR32 closed/unmerged؛ PR34/#47 superseded؛ PR35/#37 در side branch merged؛ PR13 در main merged؛ PR40/41/42/43/45/46/48/49/50/51 در main merged. این تاریخچه گزیده است نه PR1..51 کامل.

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
