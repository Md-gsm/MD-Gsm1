# گزارش ممیزی مرحلهٔ اول ماژول اشخاص — 2026-11-06

## دامنه و محدودیت شواهد

این بازبینی روی زنجیرهٔ کاری فعلی Persons و فایل‌های Domain، Application، Infrastructure، Migration، Contracts، OpenAPI و تست‌های PostgreSQL انجام شد. این گزارش فقط یافته‌هایی را می‌بندد که از کد و تصمیم معماری موجود قابل اثبات بوده‌اند.

**محدودیت مهم:** در شاخهٔ کاری فعلی Acceptance Gate Map فقط AC-31 تا AC-49 را تعریف می‌کند. مرجع نیازمندی/ماتریس قابل ردیابی AC-01 تا AC-30 در فایل‌های موجود و جست‌وجوی مخزن پیدا نشد. بنابراین هیچ AC از این بازه در این گزارش به‌صورت ساختگی تعریف یا IMPLEMENTED اعلام نمی‌شود. برای ممیزی معیاربه‌معیار AC-01..30 باید مرجع مصوب آن‌ها وارد زنجیره شود.

## یافته‌های قطعی و اصلاحات این تغییر

### P1 — نشت مالکیت داده‌های مالی از مرز Persons

**شاهد:** ADR-0014 و ADR-0015 مالکیت سقف اعتبار را به Finance/Accounts Receivable، تخفیف و قواعد قیمت را به Pricing، و روش/ابزار پرداخت را به Treasury واگذار کرده‌اند. با این حال DTOهای Application و Contracts و OpenAPI قبلی، CreditLimit، DefaultDiscount، DefaultPaymentMethod و BankCardLastFour را در مدل ثبت/خواندن Persons قرار داده بودند.

**اصلاح:**
- فیلدهای مالی از قراردادهای ثبت Application و PartyDetails حذف شدند.
- PostgresPartyRepository دیگر این فیلدها را از درخواست نمی‌نویسد و در خواندن Party نیز انتخاب/بازگردانی نمی‌کند.
- فیلدهای مالی از Create DTOها و PartyResponse عمومی حذف شدند.
- schema مالی از OpenAPI حذف و نسخهٔ قرارداد Party به 2.0 ارتقا یافت.
- تست قراردادی اضافه شد که نبود این چهار فیلد در PartyDetails را اثبات می‌کند.

**باقی‌مانده:** ستون‌های موقت متناظر در Migration 0002 هنوز در schema وجود دارند؛ این تغییر آن‌ها را نه حذف می‌کند و نه دادهٔ قدیمی را منتقل می‌کند. پیش از G1 باید تصمیم و Migration سازگار برای حذف/انتقال این ستون‌ها ثبت شود. در این مرحله مسیر خواندن/نوشتن Persons برای این مقادیر بسته شده است.

### P1 — تناقض موبایل اجباری با تصمیم پذیرفته‌شدهٔ موبایل اختیاری

**شاهد:** ADR-0014 ثبت Party بدون موبایل را مجاز می‌داند و تست PostgreSQL موجود نیز ثبت Walk-in بدون موبایل را پوشش می‌دهد؛ اما DTOهای Contracts و OpenAPI و سند مرزهای اجرایی، موبایل اصلی را اجباری معرفی می‌کردند.

**اصلاح:**
- PrimaryMobile در درخواست‌های حقیقی و حقوقی Contracts اختیاری/nullable شد.
- RegisterLegalParty نیز مانند RegisterNaturalParty موبایل nullable می‌پذیرد.
- فیلد از فهرست required در OpenAPI خارج و schema آن nullable شد.
- مسیر OpenAPI به /api/v2 منتقل شد و نسخهٔ قرارداد به 2.0 تغییر کرد.
- سند مرزهای اجرایی با تصمیم پذیرفته‌شده هم‌راستا شد.

**قاعدهٔ نهایی:** موبایل اختیاری است؛ اگر ارائه شود باید معتبر و نرمال‌شده باشد. ثبت موبایل نامعتبر همچنان رد می‌شود.

## موارد باز مرحلهٔ اول

1. دریافت/تثبیت مرجع مصوب AC-01 تا AC-30 و ساخت Acceptance Map دقیق با تست‌های مشخص.
2. ممیزی کامل Domain invariants، DTOها، SQL constraints، indexes، migration history و concurrency در برابر همان معیارها.
3. تعیین تکلیف Migration 0002 و ستون‌های مالی موقت طبق ADR-0014؛ بدون انتشار این فیلدها از Persons.
4. انتقال فیزیکی Domain/Application/Infrastructure از مسیرهای BuildingBlocks به ساختار هدف src/Modules/Persons/{Domain,Application,Infrastructure} همراه با اصلاح namespace، DI، resourceهای Migration، solution و گیت معماری.
5. بررسی مستقل اینکه تمام قراردادهای مصرف‌کننده و OpenAPI با نسخهٔ Party 2.0 هم‌راستا هستند.
6. تکمیل تست‌های API واقعی و Desktop فقط پس از تکمیل IdentityAccess و مجوز G1؛ در وضعیت فعلی API تجاری و ذخیره‌سازی شبکه‌ای Desktop فعال نیستند.

## وضعیت امنیت و انتشار

- g1Authorized=false باقی می‌ماند.
- هیچ endpoint عملیاتی یا ذخیره‌سازی شبکه‌ای Desktop در این تغییر فعال نشده است.
- Migration تولیدی اجرا نشده است.
- PR این تغییر باید Draft و Unmerged بماند و روی شاخهٔ کاری مستقل بررسی شود.
- تست‌های این تغییر باید در CI روی SHA نهایی اجرا و نتیجه ثبت شود؛ این گزارش ادعای اجرای محلی یا CI ندارد.

## فایل‌های تغییرکرده

- src/BuildingBlocks/MDGsm.Application/Persons/PartyRegistration.cs
- src/BuildingBlocks/MDGsm.Infrastructure/Persons/PostgresPartyRepository.cs
- tests/MDGsm.Domain.Tests/Persons/PersonsEndToEndTests.cs
- src/Modules/Persons/Contracts/CreateNaturalPartyRequest.cs
- src/Modules/Persons/Contracts/CreateLegalPartyRequest.cs
- src/Modules/Persons/Contracts/PartyResponse.cs
- src/Modules/Persons/Contracts/PartyContractVersion.cs
- docs/persons/contracts/persons.openapi.yaml
- docs/persons/PERSONS_IMPLEMENTATION_BOUNDARIES_FA.md
- src/Modules/Persons/Contracts/README.md
- SHA256SUMS.txt
