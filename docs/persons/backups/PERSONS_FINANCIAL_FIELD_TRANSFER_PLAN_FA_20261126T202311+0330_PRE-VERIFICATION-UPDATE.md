# طرح انتقال و تعیین تکلیف چهار فیلد مالی/پرداختی قدیمی Persons

- شناسه: ISSUE-98 / WG-0
- نسخه: 1.0
- تاریخ: 2026-11-26
- مبنای بررسی: `main` پس از ادغام رزرو 0016 در `5d9d4d360e2f54d2c03449f0db31d54fa7e98e1a`
- وضعیت: Migration 0016 نوشته شده است؛ PR #113 برای رزرو رسمی ادغام شده، ولی CI مهاجرت/آزمون داده‌دار و بازبینی مستقل پیاده‌سازی هنوز در انتظار است.

## 1. نتیجه موجودی مصرف‌کنندگان

جست‌وجوی نمادهای دقیق در فایل‌های ردیابی‌شده main برای هر چهار نام زیر انجام شد: `credit_limit_rial`، `default_discount_percent`، `default_payment_method` و `bank_card_last_four`.

- هیچ مصرف‌کننده اجرایی C#، محاسبه دامنه، DTO قرارداد عمومی Persons، API یا جدول عملیاتی Finance/Pricing/Treasury که این نام‌ها را مصرف کند پیدا نشد.
- تنها منبع فیزیکی این ستون‌ها Migration تاریخی `database/migrations/persons/0002_persons_core.sql` است.
- Migration `0015_persons_lifecycle_history.sql` عمداً هر چهار کلید را از snapshotهای JSON تاریخچه حذف می‌کند.
- قرارداد عمومی Persons 2.0 و OpenAPI فیلدهای اعتبار، تخفیف و ابزار پرداخت متعلق به حوزه‌های دیگر را منتشر نمی‌کنند.
- `finance_party_accounts` فقط هویت حساب زیرمعین و PartyId را نگهداری می‌کند؛ مقصدی برای سقف اعتبار یا پرداخت نیست.
- در baseline بررسی‌شده، ماژول/جدول عملیاتی Pricing و Treasury و مقصد فیزیکی Finance-AR/AP برای این چهار مقدار وجود ندارد.

بنابراین consumer فعلی صفر است؛ داده‌های موجود فقط ستون‌های باقی‌مانده از schema تاریخی‌اند و نباید در عملیات جاری خوانده یا اعمال شوند.

## 2. مالک canonical، معنا و تعیین تکلیف

| ستون تاریخی | مالک canonical | محدودیت معنایی | تصمیم مهاجرت |
|---|---|---|---|
| `credit_limit_rial` | Finance-AR/AP | سقف اعتباری معتبر باید با کلید PartyId × Product/Item باشد. Master جاری مقدار Party-only را رد می‌کند؛ ستون قدیمی ItemId، بازه اثر، actor، reason یا تاریخچه تغییر limit ندارد. | مقدار خام حفظ شود، اما با disposition ثابت `PARTY_ONLY_SCOPE_REJECTED`؛ هرگز به سقف مؤثر یا مجوز فروش اعتباری تبدیل نشود. مقدار غیرصفر نیازمند بررسی دستی و تصمیم/داده Item-scoped جداگانه است. |
| `default_discount_percent` | Pricing | تخفیف اعمال‌شده باید در سند فروش snapshot تغییرناپذیر شود. مقدار قدیمی فاقد PriceList/Rule/Version/Scope/Validity و محدودیت مصرف است. | مقدار خام با disposition `PRICING_RULE_UNMAPPED` حفظ شود؛ خودکار به مشتری/همکار یا سند فروش اعمال نشود. |
| `default_payment_method` | Treasury | کدهای عددی 1 تا 5 در baseline مقصد، نگاشت نسخه‌دار و تأییدشده ندارند. | مقدار عددی خام با disposition `TREASURY_ENUM_UNMAPPED` حفظ شود؛ هیچ تبدیل حدسی به Cash/Bank/PSP/Cheque انجام نشود. |
| `bank_card_last_four` | Treasury | چهار رقم آخر به‌تنهایی ابزار پرداخت، توکن، مالکیت یا قابلیت برداشت را اثبات نمی‌کند. PAN کامل نباید وجود داشته باشد یا ساخته شود. | مقدار موجود عیناً با disposition `TREASURY_LAST4_REFERENCE_ONLY` حفظ شود؛ غیرقابل استفاده برای پرداخت و بدون API/runtime read. |

تصمیم مالک در Issue #98 مالکیت‌های Finance، Pricing و Treasury و روش forward-only را تأیید کرده است. این جدول معنای جدید کسب‌وکاری ایجاد نمی‌کند؛ محدودیت‌های Master جاری را اعمال می‌کند.

## 3. مقصد و راهبرد داده

چون مقصدهای canonical اجرایی Finance-AR/AP، Pricing و Treasury هنوز در مخزن وجود ندارند، ایجاد جدول عملیاتی ساختگی برای آن‌ها مجاز نیست. Migration شماره 0016 یک پل نگهداری داده است، نه جایگزین ماژول‌های مالک.

- برای هر Party موجود، یک ردیف با همان PartyId در schema خصوصی `mdgsm_migration_archive` ثبت می‌شود.
- هر چهار مقدار خام، source CreatedAt/UpdatedAt/CreatedBy/UpdatedBy/RowVersion و نسخه Migration مبدأ نگهداری می‌شوند.
- برای تمام Partyها ردیف ثبت می‌شود، حتی وقتی مقادیر برابر پیش‌فرض‌اند؛ بنابراین نبود ردیف یا صفر بودن count ابهام ایجاد نمی‌کند.
- Migration در همان تراکنش count و برابری کامل مقادیر/PartyIdها را کنترل می‌کند. هر اختلاف باعث شکست و rollback کامل می‌شود.
- پس از اثبات parity، چهار ستون و قید قالب کارت از جدول Persons حذف می‌شوند.
- آرشیو append-only، بدون دسترسی PUBLIC و بدون grant برای نقش runtime است. هیچ API، DTO، Search یا history آن را نمی‌خواند.
- Migrationهای تاریخی 0002 و 0015 بازنویسی نمی‌شوند. archive نگهداری دائمی خودکار یا مجوز پاک‌سازی داده نیست؛ هر حذف/انتقال بعدی نیازمند تصمیم نگهداری/قانونی و Migration مستقل است.

## 4. مسیر تحویل به مالکان آینده

هنگام ایجاد مقصد canonical هر حوزه، فقط migration همان حوزه می‌تواند ردیف‌های archive را مصرف کند و باید موارد زیر را ثابت کند:

1. Finance-AR/AP: فقط پس از تعیین ItemId و مجوز/دلیل/زمان اثر؛ Party-only legacy credit هیچ‌گاه بی‌واسطه به limit مؤثر تبدیل نمی‌شود.
2. Pricing: تعریف Rule/PriceList/Version/Scope/Validity و تصمیم مالک درباره پذیرش یا رد مقدار قدیمی پیش از انتشار.
3. Treasury: قرارداد mapping کد روش پرداخت، تعریف دقیق ابزار/توکن و کنترل‌های دسترسی؛ last-four به‌تنهایی مدرک ابزار معتبر نیست.
4. هر مقصد باید idempotent، قابل reconcile و بدون حذف/تغییر archive باشد. هیچ مقصدی نباید legacy value را به‌طور ضمنی فعال کند.

این تحویل آینده، پیاده‌سازی runtime جدیدی در Issue #98 نیست و تا آماده‌شدن ماژول مالک همچنان غیرفعال می‌ماند.

## 5. معیارهای پذیرش Migration 0016

- رزرو رسمی شماره 0016 پیش از ایجاد فایل SQL؛ PR #113 با تأیید ناظر در `main` ادغام شده است.
- Migration forward-only و اتمیک؛ عدم ویرایش 0002/0015.
- حفظ دقیق همه مقادیر و metadata و assert برابری source/archive پیش از حذف ستون‌ها.
- حذف هر چهار ستون و constraint کارت از Persons.
- عدم دسترسی PUBLIC/runtime و جلوگیری از UPDATE/DELETE/TRUNCATE archive.
- آزمون داده‌دار `PersonsFinancialFieldArchiveMigrationTests` روی PostgreSQL 18 در Linux و Windows برای مقدارهای غیرپیش‌فرض، null/default، provenance، حذف ستون‌ها، ACL و immutability؛ اجرای Linux در گیت یکپارچه و اجرای مستقل همان آزمون در job ویندوز.
- اجرای دوم migration runner بدون اعمال مجدد و با checksum history یکسان؛ کنترل history و backup/restore در هر دو سیستم‌عامل.
- ثبت SHA-256 SQL، کامیت ادغام و شواهد disposable در رجیستری.
- بدون اجرای دیتابیس واقعی، تولیدی، G1، API activation یا انتشار.

## 6. مرز تکمیل

با ادغام و اعتبارسنجی Migration 0016، نشت schema و وابستگی Persons به چهار فیلد حذف می‌شود و داده قدیمی بدون مصرف عملیاتی حفظ خواهد شد. ساخت مقصد نهایی و مصرف داده در Finance-AR/AP، Pricing و Treasury باید در Issue/مرحله مالک همان قابلیت انجام شود؛ این طرح هیچ‌کدام از آن‌ها را به‌صورت کاذب تکمیل‌شده اعلام نمی‌کند.


## 7. وضعیت پیاده‌سازی 0016

فایل SQL ثبت‌شده:
`database/migrations/0016_persons_financial_field_archive.sql`

آرشیو مقصد:
`mdgsm_migration_archive.persons_financial_field_archive`

چهار disposition ثابت و غیرعملیاتی:
- `PARTY_ONLY_SCOPE_REJECTED`
- `PRICING_RULE_UNMAPPED`
- `TREASURY_ENUM_UNMAPPED`
- `TREASURY_LAST4_REFERENCE_ONLY`

آزمون داده‌دار واقعی با سه Party ساختگی اجرا می‌شود: یک مجموعه مقادیر غیرپیش‌فرض، یک ردیف پیش‌فرض با Last Four تهی، و یک ردیف غیرپیش‌فرض دیگر. آزمون، هر مقدار و provenance را با Fixture اصلی مقایسه می‌کند؛ نبود چهار ستون در Persons را می‌سنجد؛ ACL عمومی schema/table/function را رد می‌کند؛ و اثبات می‌کند UPDATE، DELETE و TRUNCATE در آرشیو با SQLSTATE `55000` شکست می‌خورند. اجرای موفق jobهای دقیق Linux و Windows و بازبینی مستقل پیاده‌سازی، شرط تبدیل وضعیت رجیستری به `VERIFIED_DISPOSABLE` است.
