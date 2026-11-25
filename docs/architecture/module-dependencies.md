# نقشهٔ وابستگی حوزه‌ها و جهت مجاز ارتباط

این نقشه، وابستگی دامنه‌ای است و به‌معنی پیاده‌شدن همهٔ ماژول‌ها نیست. یک دیتابیس PostgreSQL مرکزی در شروع حفظ می‌شود؛ جداسازی منطقی ماژول‌ها جای microservice یا دیتابیس مجزا نیست.

| حوزهٔ مبدأ | می‌تواند از قرارداد کدام حوزه استفاده کند؟ | محدودیت |
|---|---|---|
| پوسته/داشبورد | APIهای مجاز و read modelهای گزارش | دسترسی مستقیم به دیتابیس یا محاسبهٔ مجدد قواعد دامنه ممنوع |
| هویت و امنیت | هویت اشخاص و قراردادهای مجوز | حساب ورود، هویت شخص را تکثیر نمی‌کند |
| اشخاص | هویت پایدار PartyId و نقش‌های تجاری | مالک ماندهٔ مالی یا سابقهٔ تعمیر نیست |
| Finance | PartyId و اعتبارسنجی ارجاع از قرارداد Persons؛ قیمت‌گذاری/خزانه‌داری/حسابداری از قراردادهای مربوط | مالک شناسه حساب زیرمعین Party و داده مالی Finance است؛ مالک هویت Party یا داده‌های Persons نیست؛ حساب موازی در Persons ممنوع |
| سازمان (Organization) | قراردادهای هویت/امنیت برای عضویت و محدوده دسترسی | مالک شعبه، تعریف انبار و ساختار سازمانی؛ موجودی و گردش کالا متعلق به Inventory است |
| کاتالوگ دستگاه | تعمیرات و جست‌وجو | نباید پروندهٔ تعمیر ایجاد کند |
| کاتالوگ کالا و خدمات | قیمت‌گذاری، خرید، فروش، انبار، تعمیرات | منطق ثبت حرکت یا حسابداری را اجرا نمی‌کند |
| انبارداری | کاتالوگ، بهای تمام‌شده و سندهای مبدأ مجاز | تنها مالک ثبت قطعی گردش موجودی است |
| قیمت‌گذاری | کاتالوگ و قواعد فهرست قیمت | از چرخهٔ خرید/فروش/تعمیرات وابسته نمی‌شود؛ فقط قیمت پیشنهاد می‌دهد |
| خرید | اشخاص، کاتالوگ، قیمت/مالیات، خزانه‌داری، انبار و حسابداری از قراردادها | موجودی یا دفترکل را مستقیم و موازی به‌روزرسانی نمی‌کند |
| فروش/صورتحساب | اشخاص، کاتالوگ، قیمت‌گذاری، خزانه‌داری، انبار و حسابداری | مسیر مالی موازی ایجاد نمی‌کند |
| تعمیرات | اشخاص، دستگاه، کاتالوگ، انبار، صورتحساب و خزانه‌داری از قراردادها | هویت و موجودی خصوصی برای خود ندارد |
| خزانه‌داری | اشخاص، صورتحساب‌ها/تعهدات، حسابداری | خرید یا فروش را مالک نمی‌شود |
| حسابداری | قراردادهای رخداد مالی از ماژول‌های مالک | منطق عملیاتی خرید/فروش/تعمیر را جایگزین نمی‌کند |
| حقوق و دستمزد | اشخاص/کارکنان، خزانه‌داری، حسابداری | محاسبهٔ حقوق در گزارش‌ها تکرار نمی‌شود |
| مالیات | صورتحساب اصلی، اشخاص و تنظیمات مالیاتی | سند فروش موازی نمی‌سازد |
| CRM | هویت اشخاص و رخدادهای قابل‌دسترسی | هویت یا پروندهٔ تعمیر مستقل نمی‌سازد |
| گزارش‌گیری | قراردادهای خواندن از حوزه‌های مالک | مالکیت نوشتن روی دادهٔ اصلی ندارد |

## گراف وابستگی دامنه‌ای (نه ترتیب فازها)

```mermaid
flowchart TD
  IA[IdentityAccess] --> P[Persons]
  O[Organization: Branch/Warehouse/Scope] --> I[Inventory]
  C[Catalog + UOM] --> I
  C --> PR[Pricing]
  P --> S[Sales]
  P --> B[Purchasing]
  P --> R[RepairOperations]
  C --> S
  C --> B
  C --> R
  PR --> S
  PR --> B
  PR --> R
  I --> A[Accounting]
  I --> S
  I --> B
  I --> R
  A --> T[Treasury]
  A --> S
  A --> B
  T --> S
  T --> B
  T --> R
  S --> TX[Tax Integration]
  B --> TX
  S --> RT[Returns linked to source]
  B --> RT
  R --> E[ERP E2E Integration]
  S --> E
  B --> E
  I --> E
  A --> E
  T --> E
```

Persons زیرمجموعه Organization نیست؛ Persons مالک هویت Party است و Organization مالک ساختار شعبه/انبار و membership/scope. RepairOperations برای مصرف قطعه به Inventory وابسته است و برای دریافت/تسویه به Treasury؛ بنابراین نباید در نمودار قبل از Accounting/Treasury به‌صورت ماژول مستقل هم‌تراز قرار گیرد. Catalog/UOM/Pricing زیرساخت مصرفی‌اند و به‌تنهایی فاز رسمی منشور نیستند.

این گراف روابط قراردادی مورد انتظار را نشان می‌دهد، نه دستور تغییر ترتیب رسمی. ترتیب فازها همچنان دقیقاً طبق GOV-001 است: ۶ Platform Core/Organization، ۷ Inventory، ۸ Accounting، ۹ Treasury، ۱۰ Sales/Purchasing، ۱۱ RepairOperations، ۱۲ ERP Integration. هر تعارض واقعی بین وابستگی و ترتیب فازها باید با تحلیل dependency و تأیید مالک حل شود؛ این نمودار به‌تنهایی مجوز جابه‌جایی فاز نیست.

## مرز فیزیکی پروژه‌های Persons بر اساس ADR-0016 — پیاده‌سازی در شاخهٔ Issue #99

کد اجرایی Persons به سه پروژهٔ مستقل منتقل شده است:

- `src/Modules/Persons/Domain/MDGsm.Persons.Domain.csproj`: مدل دامنه و value objectها؛ بدون ProjectReference.
- `src/Modules/Persons/Application/MDGsm.Persons.Application.csproj`: use caseها و portها؛ وابسته فقط به Domain و Contracts خود Persons.
- `src/Modules/Persons/Infrastructure/MDGsm.Persons.Infrastructure.csproj`: persistence، DPAPI، retention و adapterهای PostgreSQL؛ وابسته فقط به Application، Domain و Contracts خود Persons.

مصرف‌کنندگان بیرونی همچنان فقط قراردادهای `src/Modules/Persons/Contracts` را مصرف می‌کنند. API Host مستقیماً به Application و Contracts ارجاع می‌دهد و به Domain/Infrastructure داخلی Persons ارجاع مستقیم ندارد. Shared Infrastructure صرفاً composition root و محل اجرای DbMigrator است و از طریق Extension رسمی Persons را ثبت می‌کند؛ آداپتورهای متصل به IdentityAccess در `MDGsm.Infrastructure.IdentityAccess` می‌مانند تا جهت وابستگی معکوس نشود.

SQLهای Persons از Shared Infrastructure به اسمبلی `MDGsm.Persons.Infrastructure` منتقل و با LogicalNameهای قبلی `MDGsm.Migrations.NNNN_name.sql` منتشر می‌شوند. DbMigrator منابع هر دو اسمبلی را می‌خواند؛ نام فایل، محتوای SQL، شماره‌ها و checksumهای migration تغییر نمی‌کنند. این تغییر فقط جابه‌جایی کد و resource assembly است؛ هیچ Migration روی دیتابیس واقعی اجرا نمی‌شود.

وضعیت این تغییر در شاخهٔ stacked Issue #99 در انتظار restore/build/test، PostgreSQL 18 Linux/Windows migration replay، بازبینی مستقل و پذیرش نهایی است. `g1Authorized=false` باقی می‌ماند و این تغییر به‌تنهایی مجوز G1، feature flag، production deployment یا merge نیست.

