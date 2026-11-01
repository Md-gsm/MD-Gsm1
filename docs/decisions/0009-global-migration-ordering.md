# ADR-0009 — سراسری‌بودن ترتیب Migration و مالکیت ماژولی

- Status: Proposed — نیازمند بازبینی مستقل گروه مشاور و تأیید مالک؛ هنوز Accepted نیست.
- Scope: PostgreSQL shared database، migration coordinator و module-owned schema.
- This ADR does not authorize business migrations or G1 implementation.

## Context

مالکیت فایل و محتوای migration با ماژول است؛ شمارهٔ فایل در زمان توسعه و در Git تعیین می‌شود. advisory lock دیتابیس نمی‌تواند از برخورد شماره‌ها در دو branch جلوگیری کند و coordinator زمان اجرا نباید وانمود کند که نام فایل را تخصیص می‌دهد.

## Proposed decision

1. نام فایل `NNNN_<module>_<description>.sql` است. `NNNN` در زمان توسعه از registry مرکزی نسخه‌گذاری در مخزن تخصیص داده می‌شود؛ هیچ شماره‌ای با advisory lock زمان اجرا ساخته نمی‌شود.
2. فایل مرجع registry در `database/migrations/registry.json` و version-controlled در Git است؛ حداقل `nextVersion` (شمارندهٔ یکنواخت سراسری) و فهرست تخصیص‌های قطعی شامل `version`, `moduleKey`, `migrationName`, `path` و dependencyهای صریح را نگه می‌دارد. مقدار فعلی `nextVersion=2` و تخصیص موجود فقط migration زیرساخت `0001` است. این فایل در این PR seed پیشنهادی است؛ validator اجرایی registry و runner هنوز پیاده‌سازی نشده‌اند. تخصیص شماره فقط با تغییر همین registry در PR انجام می‌شود؛ CI باید پیوستگی/افزایش یکنواخت شمارنده، یکتایی version و name و تطابق مسیر/مالک را بررسی کند. دو PR موازی که یک شماره را تخصیص دهند، با conflict یا validator رد می‌شوند و باید شمارهٔ بعدی آزاد را بگیرند.
3. بازه‌های اولیهٔ `0001–0099` برای Foundation/Infrastructure، `0100–0199` برای Persons، `0200–0299` برای IdentityAccess، `0300–0399` برای RepairOperations و `0400–0499` برای Finance صرفاً راهنمای تخصیص اولیه و برنامه‌ریزی‌اند؛ این بازه‌ها قید سخت CI یا شرط مالکیت دائمی نیستند. پس از مصرف/عبور از یک بازه، همان ماژول می‌تواند migration بعدی را با شمارهٔ سراسری بعدی از registry بگیرد. هر شمارهٔ تخصیص‌یافته global است و هیچ ماژولی sequence محلی ندارد.
4. جدول `mdgsm_migration_history` واحد است و `module_key`, `migration_version`, `migration_name`, checksum و زمان اجرا را ثبت می‌کند؛ version و name هر دو unique هستند.
5. dependencyهای بین‌ماژولی در metadata صریح و acyclic هستند؛ runner پیش از write وجود dependency و نبود cycle را بررسی می‌کند.
6. module runner فقط فایل‌های ماژول خود را discover/validate می‌کند؛ coordinator مرکزی اجرا و history را مدیریت می‌کند، اما شماره را در زمان اجرا تولید یا تخصیص نمی‌دهد. registry در زمان توسعه/PR شمارهٔ global را رزرو می‌کند.
7. اجرای migration همچنان فقط به ترتیب صعودی global و بالاتر از high-water mark دیتابیس مجاز است. برای نمونه، پس از اجرای `0200`، migration جدید `0105` رد می‌شود؛ این محدودیت با تخصیص `0201` یا شمارهٔ بعدی آزاد به Persons یا هر ماژول دیگر رفع می‌شود. high-water mark ترتیب اجرا را enforce می‌کند، نه بازهٔ ماژول را؛ بنابراین هیچ ماژولی به‌دلیل پایان بازهٔ راهنمای اولیه متوقف نمی‌شود. migration اجراشده هرگز بازشماری یا عقب‌گرد نمی‌شود.
8. پیش از اولین migration تجاری، registry/validator، runner و history باید در PR مستقل تکمیل و با PostgreSQL 18 Linux/Windows disposable آزموده شوند. runtime role حق DDL ندارد؛ اجرای migration با principal جداست.

## Required evidence

- CI تکرار version/name، شمارندهٔ عقب‌گرد/پرش غیرمجاز، ثبت تخصیص خارج از registry و module/path mismatch را رد کند؛ بازه‌های راهنما به‌تنهایی fail gate نیستند.
- dependency مفقود و cycle پیش از write رد شوند.
- اجرای `0105` پس از ثبت `0200` رد شود؛ تخصیص و اجرای شمارهٔ بعدی آزاد (مثلاً `0201`) برای Persons یا ماژول دیگر پذیرفته شود.
- checksum/name drift رد و اجرای مجدد idempotent باشد.
- history، module_key و ترتیب اجرا را ثبت کند؛ Linux/Windows disposable tests سبز باشند.

## Consequences

مالکیت migration در ماژول می‌ماند؛ یک شمارندهٔ یکنواخت سراسری در Git registry شماره‌ها را تخصیص می‌دهد و بازه‌های ماژولی فقط راهنمای اولیه‌اند. high-water mark ترتیب اجرای global را حفظ می‌کند، بدون آنکه توسعهٔ ماژول را به پایان بازهٔ رزروشده گره بزند. runtime coordinator فقط ترتیب/وابستگی/تاریخچه را enforce می‌کند. هیچ migration تجاری اضافه نمی‌شود.

## Status guard

Proposed؛ نیازمند بازبینی مستقل گروه مشاور و تأیید مالک. g1Authorized=false و Persons=NOT_IMPLEMENTED بدون تغییر می‌مانند.
