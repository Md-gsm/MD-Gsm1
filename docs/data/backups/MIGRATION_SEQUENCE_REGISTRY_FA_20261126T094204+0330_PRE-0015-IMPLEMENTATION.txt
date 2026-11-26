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
| 0012 | `database/migrations/0012_persons_workflow_promotion_results.sql` | Persons | RESERVED — AC-37 implementation in PR #96; DISPOSABLE VERIFICATION PENDING | Issue #100؛ owner-approved promotion contract؛ PR #96؛ branch `feature/persons-g1-completion-20261124`؛ schema-only promotion tombstone/access audit; PII-free; no production apply |
| 0013 | `database/migrations/0013_persons_outbox.sql` | Persons | RESERVED — AC-37 implementation in PR #96; DISPOSABLE VERIFICATION PENDING | Issue #97/#100؛ PR #96؛ depends on 0002/0007/0012؛ source-owned PII-free PartyRegistered Outbox and delivery state; PartyMerged excluded |
| 0014 | `database/migrations/0014_persons_search_control_plane.sql` | Persons | IMPLEMENTED — DISPOSABLE VERIFICATION PENDING | PR #108؛ Issue #97؛ owner-approved quota/abuse policy؛ WG-0 branch `wg0/persons-search-history-reservations-20261126`; durable Actor/Purpose quotas (10/minute, 100/day), global 1000/day warning, qualifying abuse violations (5/rolling 10 minutes), Actor-only 15-minute lock and immutable admin-unlock audit; SQL SHA-256 `d4421e6717dd543bcc1777d59c1bd817bab43e39c9cf0905ddc6d3af5adfc035`; additive schema only; Search HTTP remains disabled |
| 0015 | `database/migrations/0015_persons_lifecycle_history.sql` | Persons | IMPLEMENTED — DISPOSABLE VERIFICATION PENDING | PR #108؛ Issue #98؛ owner-approved Party/contact/address lifecycle history; branch `wg0/persons-search-history-reservations-20261126`; actor/UTC/reason and versioned before/after evidence, lifecycle deactivation/reactivation, merge transfer provenance and atomic reconciliation; SQL SHA-256 `29c374ee49b6cf0a8295657eae682c8618f8bf6c096a066221e977e6d5de0a8b`; append-only history retained without purge; no Finance-column transfer/removal, no production apply |
| 0016+ | — | تخصیص‌نیافته | UNALLOCATED | تا ثبت درخواست رزرو استفاده نشود |

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


**WG-0 canonical reservation — 2026-11-25:** Following the Owner-approved AC-37 contract in Issue #100, 0012 is reserved for Persons workflow promotion results/tombstone and access audit; 0013 is reserved for the Persons source Outbox. The complete untouched pre-change `main` registry is preserved at `docs/data/backups/MIGRATION_SEQUENCE_REGISTRY_FA_20261125T212554+0330_PRE-AC37-0012-0013-CANONICAL-RESERVATION.txt` with timestamped metadata. The SQL implementation is in PR #96 and remains subject to exact-head disposable PostgreSQL verification and independent review. This registry PR only canonicalizes sequence ownership; it does not apply SQL to a real database, enable runtime, provision keys, or grant G1. The 0012/0013 status will be upgraded to `VERIFIED_DISPOSABLE` only after successful Linux/Windows disposable migration evidence and SQL SHA-256 values are recorded.


**WG-0 reservation decision — 2026-11-26 (owner approval):** Slots 0014 and 0015 are proposed/reserved in this registry PR only. 0014 is allocated to the Persons Search Control Plane under Issue #97, implementing only the approved persistent per-Actor/Purpose quotas, global warning threshold, approved qualifying-security-violation window and Actor-specific Abuse Lock/admin-unlock audit. Ordinary quota exhaustion and ordinary validation/network errors do not count as abuse violations. Search HTTP remains disabled until implementation and tests pass. 0015 is allocated to Persons Party/ContactPoint/Address lifecycle history under Issue #98, preserving actor, UTC time, reason, versioned before/after evidence, deactivation/reactivation and merge provenance. Finance-owned fields remain Finance-owned and are not removed/transferred by this reservation. Both are additive forward-only schema reservations; SQL writing/execution, production migration, runtime activation and G1 remain separately gated. The complete pre-change registry is preserved at `docs/data/backups/MIGRATION_SEQUENCE_REGISTRY_FA_20261126T085900+0330_PRE-0014-0015-RESERVATION.txt` with timestamped metadata. The slots become official only after WG-0 PR #107 is merged to main and the registry/checksum are revalidated.


**WG-0 implementation update — 2026-11-26T09:34:22.808+03:30:** Migration 0014 and the persistent Search Control Plane implementation are included in PR #108 (stacked on PR #96). SQL SHA-256: `14a853afcaa28f0b7010f250ee6402110f086f7d569f84eef86209f28587e5c8`. This status means implemented only; disposable PostgreSQL 18 Linux/Windows replay, integration/security CI and independent review remain pending. `Persons:SearchApiEnabled` is explicitly false; no runtime activation or G1 is granted. Reservation 0015 remains reserved for lifecycle/history implementation.


**WG-0 implementation update — 2026-11-26T09:41:40.312+03:30:** Migration 0015 and Persons Party/ContactPoint/Address lifecycle/history implementation are included in PR #108 (stacked on PR #96). SQL SHA-256: `29c374ee49b6cf0a8295657eae682c8618f8bf6c096a066221e977e6d5de0a8b`. Status is implemented pending disposable PostgreSQL 18 Linux/Windows replay, integration/security CI and independent review. Lifecycle endpoints are explicitly disabled by default; no production migration, Finance data transfer, runtime activation or G1 is granted. History is append-only with no purge; any future retention/deletion exception requires a separate legal/owner decision.
