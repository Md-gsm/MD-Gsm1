# ADR-0009 — سراسری‌بودن ترتیب Migration و مالکیت ماژولی

- Status: Proposed — نیازمند بازبینی مستقل گروه مشاور و تأیید مالک؛ هنوز Accepted نیست.
- Scope: PostgreSQL shared database، migration coordinator و module-owned schema.
- This ADR does not authorize business migrations or G1 implementation.

## Context

مالکیت فایل و محتوای migration با ماژول است؛ شمارهٔ فایل در زمان توسعه و در Git تعیین می‌شود. advisory lock دیتابیس نمی‌تواند از برخورد شماره‌ها در دو branch جلوگیری کند و coordinator زمان اجرا نباید وانمود کند که نام فایل را تخصیص می‌دهد.

## Proposed decision

1. نام فایل `NNNN_<module>_<description>.sql` است. `NNNN` در زمان توسعه از registry مرکزی نسخه‌گذاری در مخزن تخصیص داده می‌شود؛ هیچ شماره‌ای با advisory lock زمان اجرا ساخته نمی‌شود.
2. بازه‌های رزرو اولیه:
   - Foundation/Infrastructure: `0001–0099`
   - Persons: `0100–0199`
   - IdentityAccess: `0200–0299`
   - RepairOperations: `0300–0399`
   - Finance: `0400–0499`
   - ماژول/دامنهٔ جدید یا بلوک بعدی پس از `0500` فقط با تغییر registry مرکزی و تصمیم مستند تخصیص می‌یابد.
3. CI برای هر فایل یکتایی شمارهٔ global، تطابق نام ماژول با مالک مسیر، قرارگرفتن شماره در بازهٔ رزروشده و عدم تکرار شماره در کل tree را کنترل می‌کند؛ duplicate و out-of-range fail می‌شوند.
4. جدول `mdgsm_migration_history` واحد است و `module_key`, `migration_version`, `migration_name`, checksum و زمان اجرا را ثبت می‌کند؛ version و name هر دو unique هستند.
5. dependencyهای بین‌ماژولی در metadata صریح و acyclic هستند؛ runner پیش از write وجود dependency و نبود cycle را بررسی می‌کند.
6. module runner فقط فایل‌های ماژول خود را discover/validate می‌کند؛ coordinator مرکزی اجرا و history را مدیریت می‌کند، اما شماره یا فایل تخصیص نمی‌دهد.
7. اجرا فقط به ترتیب صعودی global مجاز است. اگر `0200` اجرا شده باشد، افزودن/اجرای دیرهنگام `0105` رد می‌شود، حتی اگر dependency مستقیمی نداشته باشد؛ نسخه‌های اجراشده بازشماری یا عقب‌گرد نمی‌شوند. migration بعدی Persons باید از بلوک آیندهٔ global بالاتر از high-water mark استفاده کند؛ اگر بلوک فعلی تمام شده، توسعه تا تخصیص بلوک بعدی متوقف می‌شود.
8. پیش از اولین migration تجاری، registry/validator، runner و history باید در PR مستقل تکمیل و با PostgreSQL 18 Linux/Windows disposable آزموده شوند. runtime role حق DDL ندارد؛ اجرای migration با principal جداست.

## Required evidence

- CI duplicate version، out-of-range و module/path mismatch را رد کند.
- dependency مفقود و cycle پیش از write رد شوند.
- اجرای `0105` پس از ثبت `0200` رد شود؛ بلوک آیندهٔ بالاتر از high-water mark پذیرفته شود.
- checksum/name drift رد و اجرای مجدد idempotent باشد.
- history، module_key و ترتیب اجرا را ثبت کند؛ Linux/Windows disposable tests سبز باشند.

## Consequences

مالکیت migration در ماژول می‌ماند؛ شماره‌گذاری در Git/registry مرکزی انجام می‌شود و runtime coordinator فقط ترتیب/وابستگی/تاریخچه را enforce می‌کند. هیچ migration تجاری اضافه نمی‌شود.

## Status guard

Proposed؛ نیازمند بازبینی مستقل گروه مشاور و تأیید مالک. g1Authorized=false و Persons=NOT_IMPLEMENTED بدون تغییر می‌مانند.
