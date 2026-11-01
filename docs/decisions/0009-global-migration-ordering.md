# ADR-0009 — سراسری‌بودن ترتیب Migration و مالکیت ماژولی

- Status: Proposed — نیازمند بازبینی گروه مشاور و تأیید مالک؛ هنوز Accepted نیست.
- Scope: PostgreSQL shared database، migration coordinator و module-owned schema.
- This ADR does not authorize business migrations or G1 implementation.

## Context

زیرساخت فعلی از جدول history واحد با ستون عددی سراسری migration_version و نام‌گذاری ترتیبی استفاده می‌کند. در عین حال هر ماژول مالک schema و migrationهای خود است. sequence مستقل در هر ماژول با یک کلید عددی سراسری برخورد می‌کند و ترتیب dependencyها را مبهم می‌سازد.

## Proposed decision

1. مالکیت فایل و محتوای migration با ماژول است؛ تخصیص version و ترتیب اجرا سراسری است.
2. نام فایل: NNNN_<module>_<description>.sql. بخش NNNN یک sequence عددی یکتای global است که فقط coordinator مرکزی، زیر PostgreSQL advisory transaction lock، تخصیص می‌دهد.
3. جدول mdgsm_migration_history واحد باقی می‌ماند و باید module_key، migration_version، migration_name، checksum و زمان اجرا را ثبت کند. قید unique روی migration_version و migration_name اعمال می‌شود.
4. نسخه‌ها در ترتیب عددی global اجرا می‌شوند. dependencyهای بین‌ماژولی باید صریح و acyclic باشند و پیش از اجرای batch اعتبارسنجی شوند؛ ترتیب شماره به‌تنهایی dependency یا مجوز نوشتن در schema دیگر نیست.
5. module runner فقط فایل‌های متعلق به ماژول را discover/validate می‌کند، اما اجرای نهایی و درج history از coordinator مرکزی انجام می‌شود. ماژول نمی‌تواند version محلی یا تکراری تعیین کند.
6. تا پیش از اولین migration تجاری، تغییر runner/history و backward compatibility migration 0001 باید در PR مستقل طراحی و با DB disposable واقعی روی PostgreSQL 18 Linux و Windows آزمایش شود. runtime role حق DDL ندارد؛ اجرای migration با principal جدا انجام می‌شود.

## Required evidence

- دو migration از دو ماژول با versionهای global متفاوت و dependency مشخص به ترتیب صحیح اجرا شوند.
- duplicate version/name، dependency مفقود و dependency cycle پیش از write رد شوند.
- checksum/name drift رد شود؛ اجرای مجدد idempotent باشد.
- history، module_key و ترتیب اجرا را ثبت کند.
- Linux و Windows disposable integration tests سبز باشند.

## Consequences

ماژول‌ها مالک migration خود باقی می‌مانند، اما sequence و orchestration در سطح دیتابیس مرکزی واحد است. هیچ migration تجاری در این ADR اضافه نمی‌شود.

## Status guard

Proposed؛ نیازمند بازبینی مستقل گروه مشاور و تأیید مالک. g1Authorized=false و Persons=NOT_IMPLEMENTED بدون تغییر می‌مانند.
