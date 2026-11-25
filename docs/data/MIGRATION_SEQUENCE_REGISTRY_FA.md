# رجیستری مرکزی شماره‌گذاری و رزرو Migrationها

| مشخصه | مقدار |
|---|---|
| شناسه | MIG-REG-001 |
| مالک فرایند رزرو و نگهداری رجیستری | WG-0 |
| مالک SQL و schema | گروه مالک دامنه |
| مسیر Migrationها | `database/migrations/` |
| وضعیت | مرجع جاری رزرو و جلوگیری از برخورد |

## قواعد قطعی

1. شماره Migration یک شناسه سراسری عددی چهاررقمی است؛ یکتایی آن در تمام زیرپوشه‌های `database/migrations/**` الزامی است.
2. نام فایل باید `NNNN_descriptive_name.sql` باشد. نام فایل (بدون مسیر) مبنای Embedded Resource / LogicalName است؛ بنابراین هم شماره و هم نام فایل باید در کل درخت یکتا باشند.
3. پیش از نوشتن SQL، گروه متقاضی باید یک ردیف `RESERVED` با شماره، گروه، مالک schema، PR/Issue، محدوده و تاریخ درخواست ثبت کند. WG-0 رزروها را به‌صورت سریالی در همین فایل ثبت می‌کند.
4. تا زمانی که رزرو در شاخه/PR قابل مشاهده نشده، هیچ گروهی مجاز به استفاده از آن شماره نیست. اگر دو PR هم‌زمان یک شماره را پیشنهاد کنند، فقط رزروی معتبر است که نخست در مسیر رسمی ثبت و ادغام شود؛ PR دیگر باید شماره را اصلاح کند.
5. پس از ادغام و اعمال صرفاً در محیط disposable/مجاز، ردیف به `VERIFIED_DISPOSABLE` تغییر می‌کند و SHA-256 SQL و PR/commit ثبت می‌شود. این رجیستری مجوز اجرای migration روی دیتابیس واقعی نیست.
6. رزرو استفاده‌نشده فقط با ثبت دلیل، تأیید WG-0 و اثبات عدم ادغام/عدم اعمال آزاد می‌شود. شماره Migration اعمال‌شده هرگز بازاستفاده، جابه‌جا یا rename نمی‌شود.
7. هر تغییر در Migration باید هم‌زمان این رجیستری، تست یکتایی `MigrationResourceTests` و `SHA256SUMS.txt` را به‌روز کند.
8. تست CI باید نام فایل، شماره عددی، نام Embedded Resource و فهرست فایل‌های ثبت‌شده در این رجیستری را با کل زیرپوشه‌ها تطبیق دهد. ردیف `RESERVED` باید شماره و مسیر هدف یکتا داشته باشد اما فایل SQL متناظر نداشته باشد؛ ردیف‌های دارای SQL باید دقیقاً با فایل‌ها تطبیق یک‌به‌یک داشته باشند. قبولی تست به معنی مجوز schema change یا production apply نیست.

## موجودی فعلی

| شماره | مسیر دقیق | مالک دامنه | وضعیت | PR/شاهد |
|---:|---|---|---|---|
| 0001 | `database/migrations/0001_infrastructure_schema_version.sql` | Platform / Infrastructure | VERIFIED_DISPOSABLE | main؛ migration baseline |
| 0002 | `database/migrations/persons/0002_persons_core.sql` | Persons | VERIFIED_DISPOSABLE | main؛ مسیر تاریخی زیرپوشه‌ای، بدون rename |
| 0003 | `database/migrations/0003_identityaccess_core.sql` | IdentityAccess | VERIFIED_DISPOSABLE | main |
| 0004 | `database/migrations/0004_persons_reserve_national_identifiers.sql` | Persons | VERIFIED_DISPOSABLE | main |
| 0005 | `database/migrations/0005_persons_nonblank_identity_names.sql` | Persons | VERIFIED_DISPOSABLE | main |
| 0006 | `database/migrations/0006_finance_party_accounts.sql` | Finance | VERIFIED_DISPOSABLE | main |
| 0007 | `database/migrations/0007_persons_temporary_workflows.sql` | Persons | VERIFIED_DISPOSABLE | main |
| 0008 | `database/migrations/0008_persons_party_merge.sql` | Persons | VERIFIED_DISPOSABLE | main |
| 0009 | `database/migrations/0009_accounting_core.sql` | Accounting | RESERVED — SQL NOT AUTHORIZED YET | Issue #69؛ WG-0 PR #70 merged to main at `b8c672cb70dcb2b274f43f118ac08b2eea8438c5`; scope محدود به COA metadata، fiscal periods، journal header/lines، posting identity/idempotency و audit linkage؛ بدون tax یا external module posting |
| 0010 | `database/migrations/0010_identityaccess_owner_bootstrap.sql` | IdentityAccess | RESERVED — SQL NOT AUTHORIZED | Issue #93؛ PR #92 (`security/o20-o21-final-closure`)؛ WG-0 reservation decision 2026-11-24؛ schema-only: سه جدول bootstrap مربوط به IdentityAccess به‌همراه constraints/indexes و ستون nullable `identity.accounts.display_name` با CHECK محدود به bootstrap؛ بدون routine، role، grant، credential، seed یا runtime activation |
| 0011 | `database/migrations/0011_auditsecurity_canonical_ledger.sql` | AuditSecurity / Platform | RESERVED — SQL NOT AUTHORIZED | PR #92؛ reservation commit `51f28acc3da0590c8f2ae25e360450a0f44cb081`؛ 2026-11-24؛ stream heads، immutable canonical event records، unique constraints برای EventId و (AuditStreamId, StreamSequence)، شاخص‌های لازم و guard عدم UPDATE/DELETE؛ بدون source-module Outbox، checkpoint/anchor، role/LOGIN، credential، grant، operational routine یا runtime activation |
| 0012 | `database/migrations/0012_persons_workflow_promotion_results.sql` | Persons | IMPLEMENTED — DISPOSABLE VERIFICATION PENDING | Issue #100؛ PR #96؛ 2026-11-25؛ نتیجه Promotion و Tombstone حداقلی و بدون PII: WorkflowId، OperationId یکتا، ActorId، PartyId، زمان UTC، Existing/Created و نسخه قرارداد؛ append-only |
| 0013 | `database/migrations/0013_persons_outbox.sql` | Persons | IMPLEMENTED — DISPOSABLE VERIFICATION PENDING | Issue #97 و #100؛ PR #96؛ 2026-11-25؛ Outbox منبع Persons فقط برای PartyRegistered بدون PII، همراه رکورد اولیه Delivery اتمیک؛ retry با همان EventId؛ بدون PartyMerged |
| 0014+ | — | تخصیص‌نیافته | UNALLOCATED | تا ثبت درخواست رزرو استفاده نشود |

## فرم رزرو الزامی

هر ردیف رزرو جدید باید این اطلاعات را داشته باشد:

- شماره چهاررقمی یکتا؛
- شناسه/نام WG درخواست‌کننده و مالک دامنه/schema؛
- هدف و محدوده جداول/اشیای schema؛
- PR/Issue مربوط و SHA شاخه؛
- تاریخ رزرو و وضعیت `RESERVED`؛
- پس از ادغام Migration: SHA-256 فایل SQL، commit ادغام و وضعیت `VERIFIED_DISPOSABLE`؛
- در صورت لغو: علت، تأیید WG-0 و شواهد عدم ادغام/عدم اعمال.

**وضعیت در 2026-11-18:** رزرو 0009 از طریق PR #70 در `main` ادغام شده و شماره به‌طور رسمی برای Accounting رزرو است. این فقط مجوز نگهداری slot رزروشده است؛ فایل SQL ایجاد نشده و `SQL NOT AUTHORIZED YET` باقی می‌ماند. نوشتن Migration 0009 نیازمند پذیرش طراحی فیزیکی، رفع پیش‌نیازهای ثبت‌شده و مجوز صریح جداگانه مالک پروژه است.


**همگام‌سازی WG-0 در 2026-11-25:** رزروهای 0010 (IdentityAccess) و 0011 (AuditSecurity / Platform) که پیش‌تر در شاخه PR #92 ثبت شده بودند، بدون ادغام PR #92 و بدون انتقال سایر تغییرات آن، در رجیستری جاری `main` منعکس شدند. هر دو ردیف صرفاً `RESERVED — SQL NOT AUTHORIZED` هستند؛ فایل‌های SQL متناظر ایجاد نشده‌اند و هیچ مجوزی برای نوشتن یا اجرای SQL/DDL، تخصیص Principal/Grant، فعال‌سازی runtime یا production صادر نمی‌شود. شماره 0012 به بعد همچنان تخصیص‌نیافته است.


**رزرو WG-0 در 2026-11-25 برای AC-37:** پس از تأیید صریح مالک در Issue #100، شماره‌های 0012 و 0013 در همین PR #96 به‌ترتیب برای نتیجه/Tombstone فرایند Promotion و Outbox منبع Persons رزرو شدند. این شماره‌ها ابتدا در شاخه PR #96 قابل مشاهده شدند و سپس SQL متناظر فقط در محدوده مصوب Issue #100 نوشته شد. وضعیت هر دو Migration برابر `IMPLEMENTED — DISPOSABLE VERIFICATION PENDING` است؛ هنوز در محیط disposable اجرا و تأیید نشده‌اند. هیچ Migration در دیتابیس واقعی اجرا نشده و هیچ مجوزی برای Principal/Grant، runtime یا production صادر نشده است.
