# طراحی فیزیکی پایگاه داده هسته حسابداری
## MDG-ACC-PDS-001 — نسخه 0.2

| مشخصه | مقدار |
|---|---|
| تاریخ | 2026-11-18 |
| وضعیت | DATABASE BUILD DESIGN — READY FOR FINAL REVIEW; SQL / MIGRATION NOT AUTHORIZED |
| مالک داده | Accounting (ACC-01, ACC-02, FY-01) |
| معماری والد | MDG-ARCH-MASTER-001 v2.5 |
| دامنه تصمیم | MDG-ADR-0018 |
| مرجع COA | MDG-COA-MASTER-001 v1.2 (اصلی)، v1.4 (مکمل)، v1.3 (میانی و غیرمرجع) |
| Migration | 0009 در main رزرو شده؛ SQL NOT AUTHORIZED YET |
| نسخه جایگزین | این نسخه طراحی v0.1 را تکمیل و جایگزین می‌کند؛ v0.1 به‌عنوان سابقه حفظ می‌شود |

## 1. هدف و مرز

این نسخه طراحی Accounting را تا مرز آمادگی برای ساخت schema پیش می‌برد: موجودیت‌ها، روابط، قیود، ایندکس‌ها، هم‌زمانی، ترتیب ایجاد اشیا، مدل ورود شواهد COA، ثبت unresolvedها، امنیت و بازیابی مشخص شده‌اند.

این سند مجوز ایجاد SQL، Migration 0009، تغییر DbContext، EF Persistence، DbSet، API، Journal persistence، Posting writer/runtime، Seed حساب‌ها، import داده COA یا استقرار نیست. Migration 0009 همچنان RESERVED — SQL NOT AUTHORIZED است. ساخت schema واقعی فقط پس از تأیید ناظر روی SHA نهایی و مجوز صریح جداگانه مالک برای SQL مجاز خواهد بود.

هدف نسخه 0.2 حل مصنوعی AP-08 نیست؛ هدف این است که دیتابیس بتواند منبع خام، شواهد، وضعیت تطبیق و حساب Canonical را از یکدیگر جدا کند تا فقدان منبع یا صفت با مقدار حدسی جایگزین نشود.

## 2. مبانی قطعی

- PostgreSQL 18 هدف پایگاه داده است؛ schema مستقل Accounting نام accounting دارد.
- مقادیر مالی IRR فقط integer/bigint هستند؛ float/double ممنوع است.
- Journal به یک COA snapshot مشخص متصل است؛ reversal سند مستقل است.
- سند Finalized و خطوط آن immutable هستند؛ اصلاح از مسیر سند اصلاحی/reversal انجام می‌شود.
- Audit و period transition append-only هستند.
- ActorReference و ارجاعات بین‌ماژولی opaque هستند؛ FK مستقیم به IdentityAccess یا ماژول‌های دیگر ساخته نمی‌شود.
- FiscalPeriod بازه inclusive دارد؛ Close نیازمند هر هشت attestation و Reopen نیازمند درخواست‌کننده و تأییدکننده مستقل است.
- PostingIdentity از payload fingerprint جداست؛ canonicalization نسخه‌دار و fingerprint مبتنی بر SHA-256 است.
- نبودن AccountId، evidence، attribute یا eligibility معتبر باید fail-closed باشد.
- TaxMode=0 تمام Tax Line و Tax Posting را ممنوع می‌کند؛ هیچ نرخ یا Tax Matrix ساخته نمی‌شود.
- هیچ positive Posting Rule پذیرفته‌شده‌ای از این طراحی نتیجه نمی‌شود.

## 3. تصمیم‌های فیزیکی

1. schema مستقل accounting؛ schema فعلی identity تغییر نمی‌کند.
2. تمام FKهای مالی ON DELETE RESTRICT؛ cascade delete ممنوع.
3. مبالغ JournalLine با bigint IRR.
4. COA در سه لایه مجزا نگهداری می‌شود: منبع و ردیف استخراج‌شده، رجیستر شناسه و disposition، و تعریف Canonical حساب.
5. Snapshot پذیرفته‌شده immutable است؛ اصلاح منبع با snapshot/artifact نسخه جدید انجام می‌شود.
6. Accepted بودن snapshot فقط پذیرش provenance و composition منبع را نشان می‌دهد، نه مجوز Posting.
7. عدم وجود ALLOW برای یک FE به معنی عدم اجازه است؛ هیچ default-ALLOW وجود ندارد.
8. هیچ outbox، event publisher یا adapter بین‌ماژولی در این schema ایجاد نمی‌شود.
9. هیچ COA seed در این PR ایجاد نمی‌شود.

## 4. جداول

نسخه 0.2 سیزده جدول را تعریف می‌کند: ده جدول هسته نسخه 0.1 و سه جدول جدید برای provenance و رجیستر COA.

### 4.1 accounting.audit_events

- event_id text PRIMARY KEY
- event_type, aggregate_type, aggregate_id, actor_reference text NOT NULL
- occurred_at_utc, recorded_at_utc timestamptz NOT NULL
- correlation_id, causation_id, source_reference, reason text NULL
- details jsonb NOT NULL DEFAULT '{}'

Append-only؛ runtime حق UPDATE/DELETE/TRUNCATE ندارد. ActorReference opaque است. details نباید credential، token، password، secret یا PII غیرضروری داشته باشد. Audit و تغییر aggregate در یک transaction commit می‌شوند.

### 4.2 accounting.coa_snapshots

- coa_snapshot_id uuid PRIMARY KEY
- authority_document text NOT NULL
- authority_version text NOT NULL
- source_reference text NOT NULL (ارجاع به manifest منابع)
- source_sha256 char(64) NOT NULL (hash manifest ترکیب منابع، نه الزاماً یک فایل)
- imported_at_utc timestamptz NOT NULL
- imported_by text NOT NULL (opaque)
- status text NOT NULL: Draft / Accepted / Retired

Hash باید 64 رقم hex lowercase باشد. یکتایی authority_document + authority_version + source_sha256. source_sha256 hash یک manifest قطعی و مرتب‌شده از Artifactهای snapshot است. پیش از Accepted شدن، تمام Artifactهای الزامی باید از نظر authority و integrity تعیین تکلیف شده باشند؛ وجود Artifact با وضعیت MISSING/NOT_VERIFIED مانع پذیرش snapshot به‌عنوان source-complete است. Snapshot Accepted immutable است. Accepted فقط تأیید manifest و سلسله‌مراتب منبع را نشان می‌دهد و به‌تنهایی هیچ حسابی را Canonical، Postable یا مجاز برای Posting نمی‌کند.

### 4.3 accounting.coa_source_artifacts — افزوده نسخه 0.2

برای ثبت هر منبعی که در composition یک snapshot نقش دارد؛ یک snapshot می‌تواند چند Artifact از نوع Master، Supplementary، OwnerDecision یا ControlledExtract داشته باشد.

- artifact_id uuid PRIMARY KEY
- coa_snapshot_id uuid NOT NULL FK → coa_snapshots ON DELETE RESTRICT
- artifact_role text NOT NULL
- authority_document, authority_version text NOT NULL
- source_reference text NOT NULL
- content_location text NULL (opaque reference به evidence store نسخه‌دار)
- expected_sha256, observed_sha256 char(64) NULL
- integrity_status text NOT NULL: VERIFIED / MISSING / NOT_VERIFIED / PROPOSED
- media_type text NULL
- captured_at_utc timestamptz NOT NULL
- captured_by text NOT NULL
- notes text NULL

هر hash موجود باید 64 رقم hex lowercase باشد. VERIFIED فقط وقتی مجاز است که بایت واقعی Artifact در دسترس بوده، hash محاسبه و با مرجع معتبر تطبیق داده شده و authority آن نیز معتبر باشد. ثبت expected hash به‌تنهایی VERIFIED نیست. بایت اصلی فایل در evidence store نسخه‌دار نگهداری می‌شود؛ DB آرشیو byte-for-byte نیست.

### 4.4 accounting.coa_source_rows — افزوده نسخه 0.2

برای نگهداری استخراج قابل ردیابی ردیف‌های منبع، بدون تبدیل خودکار آن به حساب Canonical.

- source_row_id uuid PRIMARY KEY
- artifact_id uuid NOT NULL FK → coa_source_artifacts ON DELETE RESTRICT
- row_locator text NOT NULL (مانند sheet/row؛ ساختار فایل فرض نمی‌شود)
- account_code text NULL
- extraction_schema_version integer NOT NULL
- row_payload jsonb NOT NULL
- row_sha256 char(64) NULL
- row_disposition text NOT NULL: CAPTURED / RECONCILED / CONFLICT / UNRESOLVED / PROHIBITED
- recorded_at_utc timestamptz NOT NULL
- recorded_by text NOT NULL
- UNIQUE(artifact_id, row_locator)

row_payload فقط داده استخراج‌شده است، نه حقیقت Canonical. هیچ مقدار آن بدون SourceRef/DecisionRef و evidence به accounts منتقل نمی‌شود. استخراج اصلاح‌شده با نسخه جدید ثبت می‌شود؛ overwrite خاموش ممنوع است. پس از پذیرش snapshot، artifact و source rowهای متصل به آن immutable می‌شوند. Hash ردیف باید بر اساس extraction/canonicalization version مشخص محاسبه شود.

### 4.5 accounting.coa_registry_entries — افزوده نسخه 0.2

رجیستر شناسه‌های COA در هر snapshot، مستقل از کامل‌بودن تعریف Canonical.

- coa_snapshot_id uuid NOT NULL
- account_code text NOT NULL
- disposition text NOT NULL: UNRESOLVED / CANDIDATE / DESIGN_ONLY / INACTIVE / PROHIBITED / RESERVED / CANONICAL
- source_artifact_id uuid NULL FK → coa_source_artifacts ON DELETE RESTRICT
- source_row_id uuid NULL FK → coa_source_rows ON DELETE RESTRICT
- owner_decision_reference text NULL
- disposition_reason text NOT NULL
- audit_event_id text NOT NULL FK → audit_events ON DELETE RESTRICT
- recorded_at_utc timestamptz NOT NULL
- PRIMARY KEY(coa_snapshot_id, account_code)
- اگر source_row_id مقدار داشته باشد، آن ردیف باید متعلق به همان source_artifact_id باشد؛ در صورت نیاز این تطابق با FK مرکب enforce می‌شود.

هر کد در هر snapshot یک disposition جاری دارد. تغییر disposition باید با تصمیم/نسخه جدید و audit انجام شود. نبود منبع به معنی UNRESOLVED است، نه CANONICAL. کد 110809 باید PROHIBITED باشد؛ ساخت حساب Canonical، JournalLine، eligibility مثبت یا Posting برای آن باید در Application و DB رد شود. 2114 و 5106 تا تکمیل صفات و شواهد حداکثر DESIGN_ONLY/CANDIDATE هستند. وجود 78 شناسه به معنی وجود 78 حساب Canonical نیست.

### 4.6 accounting.accounts

تعریف حساب در محدوده یک snapshot؛ این جدول با source_rows یا registry entries یکی نیست.

- coa_snapshot_id uuid NOT NULL
- account_code text NOT NULL
- account_id uuid NULL (NULL فقط برای Draft/Unresolved؛ برای هر JournalLine باید غیر NULL و معتبر باشد)
- title_fa text NULL; title_en text NULL (عنوان نامعلوم حدس زده نمی‌شود)
- parent_account_id uuid NULL
- level integer NULL; account_class text NULL
- nature text NULL; normal_balance text NULL
- postable boolean NULL; direct_posting_allowed boolean NULL
- status text NOT NULL
- effective_from date NULL; effective_to date NULL
- subledger_type text NULL
- tax_mode integer NULL; tax_treatment text NULL
- recognition_gate text NULL; reconciliation_policy text NULL
- aging_policy text NULL; statement_mapping text NULL
- definition_version integer NULL
- source_reference, decision_reference, change_request_reference, audit_reference text NULL
- PRIMARY KEY(coa_snapshot_id, account_code)
- UNIQUE(coa_snapshot_id, account_id) با رفتار PostgreSQL برای NULLهای متعدد

FK به coa_snapshots و FK مرکب به coa_registry_entries؛ ON DELETE RESTRICT. کد فقط ASCII digits است. effective_to >= effective_from در صورت وجود هر دو. Parent فقط در همان snapshot و با رابطه صریح مجاز است. account_id تهی یا هر attribute ناقص/Unverified، وضعیت غیر Canonical، inactive، prohibited، design-only یا unresolved به معنی غیرقابل استفاده بودن برای Posting است. برای 110809 هیچ ردیف Canonical در accounts مجاز نیست.

### 4.7 accounting.account_field_evidence

یک شاهد مستقل برای هر فیلد از 27 فیلد RequiredFields در Domain.

- coa_snapshot_id uuid NOT NULL
- account_code text NOT NULL
- field_name text NOT NULL
- disposition text NOT NULL: Verified / NotApplicable / Unverified
- field_value text NULL
- source_reference text NOT NULL
- rationale text NULL
- PRIMARY KEY(coa_snapshot_id, account_code, field_name)
- FK مرکب به accounts ON DELETE RESTRICT

Verified: value غیرخالی و rationale خالی. NotApplicable: value NULL و rationale غیرخالی. Unverified هیچ‌گاه gate را باز نمی‌کند. برای هر account draft دقیقاً یک evidence برای هر یک از 27 فیلد لازم است تا completeness سنجیده شود؛ پذیرش Canonical علاوه بر آن مستلزم Verified بودن 23 فیلد RequiredVerified مطابق Domain است. فهرست 27/23 فقط از Domain authority گرفته می‌شود تا drift رخ ندهد.

### 4.8 accounting.account_event_eligibility

- coa_snapshot_id uuid NOT NULL
- account_code text NOT NULL
- financial_event_id text NOT NULL
- eligibility text NOT NULL: ALLOW / FORBID
- source_reference text NOT NULL
- decision_reference text NOT NULL
- PRIMARY KEY(coa_snapshot_id, account_code, financial_event_id)
- FK مرکب به accounts ON DELETE RESTRICT

فقط disposition دارای تصمیم پذیرفته‌شده قابل ثبت است. نبود ردیف یعنی عدم اجازه. این جدول Posting Rule نیست. تا پذیرش مستقل 7B، ALLOW مثبت ساخته نمی‌شود. تعارض یا event ناشناخته fail-closed است.

### 4.9 accounting.fiscal_periods

- period_id uuid PRIMARY KEY
- starts_on, ends_on date NOT NULL (inclusive)
- status text NOT NULL: Open / Closed
- version bigint NOT NULL DEFAULT 1
- last_changed_by text NOT NULL
- last_changed_at_utc timestamptz NOT NULL
- last_audit_event_id text NOT NULL

starts_on <= ends_on و version > 0. هیچ دو بازه‌ای هم‌پوشانی ندارند، چه Open و چه Closed؛ exclusion constraint روی daterange(starts_on, ends_on, '[]') با GiST. تاریخ Journal باید در دوره Open باشد. تغییر وضعیت با row lock و optimistic version انجام می‌شود.

### 4.10 accounting.period_transition_events

- transition_id uuid PRIMARY KEY
- period_id uuid NOT NULL FK → fiscal_periods ON DELETE RESTRICT
- from_status, to_status text NOT NULL
- requested_by text NOT NULL; approved_by text NULL
- reason text NOT NULL
- transitioned_at_utc timestamptz NOT NULL
- audit_event_id text NOT NULL FK → audit_events ON DELETE RESTRICT
- هشت attestation مدل PeriodCloseEvidence به صورت boolean nullable برای رویداد Close

Close فقط Open→Closed و هر هشت attestation باید true باشند. Reopen فقط Closed→Open؛ requested_by و approved_by غیرتهی و متفاوت، reason و audit الزامی. رویدادها append-only هستند. TaxReconciled صرفاً attestation دوره است و Tax Engine/Tax Line ایجاد نمی‌کند.

### 4.11 accounting.posting_idempotency

- idempotency_key char(64) PRIMARY KEY
- source_module, source_document_id, source_event_id, posting_purpose, source_version text NOT NULL
- source_snapshot_id, allocation_id text NULL
- canonicalization_version integer NOT NULL
- request_payload_sha256 char(64) NOT NULL
- created_at_utc timestamptz NOT NULL

Hashها 64 رقم hex lowercase. UNIQUE روی اجزای identity با NULLS NOT DISTINCT برای اجزای optional. جدول عمداً journal_id ندارد تا FK دوری ایجاد نشود. همان key و همان payload hash/version یعنی replay؛ هر mismatch conflict است و Journal قبلی overwrite نمی‌شود. race در persistence با unique constraint و transaction rollback/re-read کنترل می‌شود.

### 4.12 accounting.journals

- journal_id uuid PRIMARY KEY
- fiscal_period_id uuid NOT NULL
- journal_date date NOT NULL
- coa_snapshot_id uuid NOT NULL
- idempotency_key char(64) NOT NULL UNIQUE
- source_document_reference text NOT NULL
- status text NOT NULL: Draft / Finalized
- created_by text NOT NULL; created_at_utc timestamptz NOT NULL; created_audit_event_id text NOT NULL
- finalized_by, finalized_at_utc, finalized_audit_event_id nullable
- reversal_of_journal_id uuid NULL
- version bigint NOT NULL DEFAULT 1

FK به fiscal_periods، coa_snapshots، posting_idempotency، audit_events و self-FK reversal با RESTRICT. Draft فاقد finalized metadata؛ Finalized دارای metadata کامل است. journal_date در بازه دوره است و هنگام Finalize دوره Open است. Reversal فقط به Journal Finalized اشاره می‌کند و سند مستقل با identity مستقل است. هر Journal به snapshot مشخص bind است.

### 4.13 accounting.journal_lines

- line_id uuid PRIMARY KEY
- journal_id uuid NOT NULL
- line_number integer NOT NULL
- coa_snapshot_id uuid NOT NULL
- account_id uuid NOT NULL
- account_code_snapshot text NOT NULL
- debit_irr, credit_irr bigint NOT NULL DEFAULT 0
- party_subledger_reference text NULL (opaque PartyId/reference)
- dimensions jsonb NULL
- UNIQUE(journal_id, line_number); UNIQUE(journal_id, line_id)

FK به journals و FK مرکب (coa_snapshot_id, account_id) به accounts. دقیقاً یکی از debit_irr/credit_irr مثبت، دیگری صفر؛ هر دو non-negative؛ صفر/صفر و هر دو مثبت ممنوع. حداقل دو خط و توازن مجموع بدهکار/بستانکار با deferred constraint trigger در پایان transaction؛ overflow با numeric accumulator و کنترل bigint در مرز Domain. account_code_snapshot باید با account مرجع همان snapshot برابر باشد. بعد از Finalize هیچ INSERT/UPDATE/DELETE روی خطوط مجاز نیست.

## 5. روابط و حذف

- coa_snapshots 1—N coa_source_artifacts؛ artifacts 1—N source_rows.
- coa_snapshots 1—N coa_registry_entries؛ registry entry 1—0..1 canonical account.
- accounts 1—N account_field_evidence و account_event_eligibility.
- fiscal_periods 1—N journals و period_transition_events.
- coa_snapshots 1—N journals.
- journals 1—N journal_lines و journals 1—1 posting_idempotency از طریق idempotency_key.
- journals 0—N reversal journals.
- audit_events مرجع append-only تغییرات حساس.

تمام FKهای مالی ON DELETE RESTRICT؛ cascade delete ممنوع.

## 6. ایندکس‌ها

- coa_source_artifacts: index روی (integrity_status, artifact_role)؛ uniqueness روی artifact identity و observed hash با رفتار صریح NULL.
- coa_source_rows: UNIQUE(artifact_id, row_locator)، index روی (account_code, row_disposition).
- coa_registry_entries: PK مرکب، index روی (coa_snapshot_id, disposition).
- accounts: PK/UNIQUE(coa_snapshot_id, account_code)، UNIQUE(coa_snapshot_id, account_id)، index روی (coa_snapshot_id, status, postable).
- account_field_evidence: PK مرکب و index روی (coa_snapshot_id, account_code, disposition).
- account_event_eligibility: PK مرکب و index روی (financial_event_id, eligibility).
- fiscal_periods: exclusion GiST برای overlap و index روی (status, starts_on, ends_on).
- period_transition_events: (period_id, transitioned_at_utc DESC).
- journals: (fiscal_period_id, journal_date, status)، (coa_snapshot_id, journal_date)، UNIQUE(idempotency_key)، index روی reversal_of_journal_id.
- journal_lines: UNIQUE(journal_id, line_number)، index روی (account_id, journal_id).
- posting_idempotency: PK و UNIQUE identity components؛ index زمانی فقط با نیاز عملیاتی اثبات‌شده.
- audit_events: (aggregate_type, aggregate_id, recorded_at_utc DESC)؛ correlation index فقط با نیاز عملیاتی.

از indexهای speculative اجتناب شود.

## 7. تراکنش‌ها و هم‌زمانی

### Finalize Journal
1. transaction آغاز شود.
2. FiscalPeriod با SELECT ... FOR UPDATE قفل و version/status/date بررسی شود.
3. snapshot و registry disposition، AccountId، evidence completeness، attributes و event eligibility در همان snapshot بررسی شوند.
4. فقط حساب Canonical، فعال و مجاز قابل استفاده است؛ unknown/inactive/prohibited/design-only/unresolved رد می‌شود.
5. حداقل دو خط، مبلغ صحیح و توازن کنترل شود؛ deferred trigger نیز invariant را در پایان transaction بررسی کند.
6. audit finalize و تغییر status با optimistic version در همان transaction ثبت شود.
7. هر خطا کل transaction را rollback می‌کند.

### Idempotent persistence
1. PostingIdentity و payload fingerprint canonical/versioned محاسبه شوند.
2. idempotency record با unique key درج شود.
3. Journal و Lines در همان transaction درج شوند.
4. duplicate key: transaction بازنده rollback؛ سپس رکورد برنده خوانده و hash/version مقایسه شود.
5. تطابق کامل replay؛ mismatch conflict و audit مستقل محدودشده، بدون تغییر سند قبلی.

### Close/Reopen
قفل دوره، کنترل version، ثبت هشت attestation، منع pending journals؛ Reopen با requester != approver، reason و audit؛ همه در یک transaction. هیچ cached preflight مجوز commit نیست.

## 8. ترتیب ایجاد اشیای دیتابیس

1. schema accounting و extension لازم برای GiST exclusion پس از بررسی DBA.
2. audit_events.
3. coa_snapshots.
4. coa_source_artifacts.
5. coa_source_rows.
6. coa_registry_entries.
7. accounts.
8. account_field_evidence و account_event_eligibility.
9. fiscal_periods.
10. period_transition_events.
11. posting_idempotency.
12. journals.
13. journal_lines.
14. triggers، deferred constraints، exclusion constraints، indexes باقی‌مانده و grants.
15. role grants فقط پس از تأیید ماتریس و آزمون؛ هرگز broad/default grants.

ترتیب بالا طرح وابستگی است؛ ترتیب دقیق triggerها و constraintهای deferred باید در DDL نهایی با clean install و repeat test اثبات شود.

## 9. امنیت و privilege

- migrator/deployment role تنها مالک schema و مجاز به DDL است.
- runtime مالک schema/table نیست؛ CREATE/ALTER/DROP/TRUNCATE/DELETE عمومی ندارد.
- COA، registry، evidence، eligibility و fiscal_periods برای runtime read-only هستند مگر مجوز جداگانه‌ای در ماتریس مصوب تعریف شود.
- period_transition_events و audit_events append-only.
- journal_lines پس از درج immutable؛ journal header فقط transitionهای محدود و مجاز را می‌پذیرد.
- grants محدود Journal فقط مطابق ماتریس امنیتی تأییدشده؛ مجوز IdentityAccess به Accounting تسری نمی‌یابد.
- triggerها دفاع لایه دوم‌اند، نه جایگزین authorization.

## 10. بازیابی و Migration lifecycle

- قبل از هر schema change backup دیتابیس disposable و restore به DB جدا انجام شود.
- runner فعلی Down/Rollback ندارد.
- پیش از داده مالی واقعی، فقط DB disposable علامت‌گذاری‌شده قابل حذف است.
- پس از داده مالی واقعی، فقط forward-fix نسخه‌دار؛ migration مخرب و دستکاری سند Finalized ممنوع.
- backup/restore عملیاتی Accounting هنوز اثبات نشده؛ runbook این محدودیت را صریح اعلام می‌کند.

## 11. وضعیت AP-01 تا AP-08

| شناسه | نتیجه فعلی | وضعیت |
|---|---|---|
| AP-01 | CoaSnapshotId در Domain، factory، finalize، reversal و tests اعمال شده؛ persistence mapping بعدی است. | CI VERIFIED ON BASELINE SHA |
| AP-02 | canonical payload fingerprint نسخه‌دار SHA-256 و تشخیص replay/conflict در Application و unit tests. race persistence بعدی است. | CI VERIFIED ON BASELINE SHA |
| AP-03 | deferred balance/line count/overflow در PoC PostgreSQL 18.6. | PASSED ON BASELINE SHA |
| AP-04 | immutability، append-only، accepted snapshot و TRUNCATE/runtime guards در PoC. | PASSED ON BASELINE SHA |
| AP-05 | recovery runbook موجود؛ restore عملیاتی Accounting هنوز اجرا نشده است. | DOCUMENTED; OPERATIONAL RESTORE NOT VERIFIED |
| AP-06 | migration registry و checksum CI. | PASSED ON BASELINE SHA |
| AP-07 | privilege matrix و least-privilege PoC. هیچ grant دائمی اعمال نشده است. | MATRIX + DISPOSABLE POC PASSED ON BASELINE SHA |
| AP-08 | مدل Artifact/Row/Registry و نگهداری unresolvedها در v0.2 تعریف شد؛ صفات و source crosswalk Canonical کامل نیست. | PHYSICAL MODEL DEFINED; SOURCE RECONCILIATION OPEN |

### 11.1 شواهد واقعی AP-08

مرجعیت فعال: COA v1.2 سند مادر، v1.4 رجیستر تکمیلی جاری، v1.3 نسخه میانی غیرمرجع. دامنه تطبیق 78 شناسه (76 کد پایه + 2114 و 5106) است؛ 110809 ممنوع است.

گزارش Source Recovery ثبت می‌کند:
- snapshot استخراجی workbook تطبیق: 76 ردیف، 24 ستون، 76 کد یکتا و بدون duplicate در همان snapshot.
- فایل Excel مادر با Hash مورد انتظار a6ca3377fde5863455a3b4bccf9a000d0dbf23f44bd66eeae51c44d0f3697866 در منابع قابل بازیابی نبود؛ Hash فایل اصلی NOT VERIFIED است.
- snapshot موجود Hash af3d02cab3c2f5a430508356680b42146320efb9122b1e5fd32d62ebb8c5c0fd دارد؛ جایگزین بایت‌های فایل مادر نیست.
- در snapshot 76 ردیفی، AccountId برای هر 76 مورد تخصیص نیافته؛ ParentAccountId برای هر 76 مورد تخصیص نیافته؛ EffectiveFrom برای هر 76 مورد تعیین نشده است.
- فقط سه ParentAccountCode صریح‌اند: 110302→1103، 610801→6108، 620201→6202. هیچ Parent دیگری از prefix استنتاج نمی‌شود.
- Postable: 56 UNVERIFIED، 8 NO IN R1، 6 CONDITIONAL، 5 NO RELEASE/source open، 1 UNRESOLVED؛ هیچ‌کدام به‌تنهایی مجوز Canonical یا Posting نیست.
- DirectPostingAllowed: 72 UNVERIFIED و 4 NO.
- SubledgerRequired: 35 YES/CONTROLLED SUBLEDGER، 35 NOT SPECIFIED، 6 TO BE CONFIRMED.
- 2114 و 5106 هر دو OWNER APPROVED — DESIGN ONLY؛ صفات Canonical و eligibility کامل نیست.
- 4107 و 5106 در مجموعه فعلی 78 کد تکرار مستقیم ندارند؛ No-Collision در کل namespace Canonical/رزرو/تاریخی اثبات نشده است.
- Positive Posting Rules پذیرفته‌شده در 7B برابر صفر است؛ FEها تا تکمیل قرارداد و شواهد مسدودند.

هیچ AccountId، Parent، Level، Nature، NormalBalance، EffectiveDate، Postable یا DirectPostingAllowed جدیدی در نسخه 0.2 تعیین نشده است.

### 11.2 سیاست ورود داده

1. Artifact با source reference، expected/observed hash و integrity status ثبت شود.
2. تا زمان بازیابی بایت اصلی و تطبیق hash معتبر، وضعیت MISSING/NOT_VERIFIED/PROPOSED باقی بماند.
3. extraction نسخه‌دار در coa_source_rows ذخیره شود؛ JSON استخراجی جایگزین فایل اصلی نیست.
4. 78 شناسه با disposition و lineage در coa_registry_entries ثبت شوند؛ 110809 PROHIBITED.
5. فقط source row دارای منبع و تصمیم پذیرفته‌شده می‌تواند در accounts تعریف Draft/Canonical ایجاد کند.
6. برای عبور Canonical، 27 evidence field و 23 RequiredVerified field مطابق Domain، AccountId معتبر، hierarchy بدون cycle، effective dating و تمام gateهای account لازم است.
7. نبود event eligibility یا Posting Rule پذیرفته‌شده به معنی BLOCK است؛ هیچ ALLOW مثبت پیش‌فرض نداریم.
8. import باید idempotent و version-bound باشد؛ hash/payload mismatch conflict است، نه overwrite.

## 12. آزمون‌های الزامی پیش از پذیرش Persistence

- PostgreSQL 18 clean install، repeat، checksum drift، out-of-order و missing lower version.
- Artifact hash match/mismatch/missing؛ row extraction repeatability و immutable versioning.
- Registry uniqueness، 78 identifier inventory، 110809 prohibited و عدم امکان promotion آن.
- 27 evidence field exact coverage، 23 RequiredVerified، missing/duplicate/unknown field و fail-closed.
- FK/RESTRICT و جلوگیری از Parent خارج از snapshot/cycle.
- Effective dating، inclusive period overlap، boundary dates، concurrent close/reopen، requester=approver.
- Journal کمتر از دو خط، duplicate line, debit/credit صفر/منفی/هر دو مثبت، imbalance و overflow؛ deferred check در commit.
- Finalized Journal/Line UPDATE/DELETE و Audit/transition UPDATE/DELETE/TRUNCATE باید fail شود.
- Idempotency same-key same-payload، same-key different-payload، concurrent duplicate و canonicalization version change.
- rollback اتمیک Journal/Lines/Idempotency/Audit.
- backup/restore به DB جدا و اجرای migration دوباره با zero pending.
- همه آزمون‌های DDL نهایی تا اجرای واقعی برابر NOT RUN / NOT VERIFIED هستند.

## 13. معیار خروج از طراحی و گیت ساخت دیتابیس

| گیت | وضعیت | اثر |
|---|---|---|
| مرز مالکیت و schema Accounting | DEFINED | مستقل از Identity |
| مدل 13 جدول، FK، constraint، index و ترتیب ایجاد | DEFINED — REVIEW REQUIRED | آماده بازبینی فنی نهایی |
| Source Artifact / Row provenance | DEFINED | منبع ناقص بدون جعل قابل ثبت است |
| Registry 78 شناسه و ممنوعیت 110809 | DEFINED | شناسه به معنی حساب Canonical نیست |
| صفات Canonical همه حساب‌ها | OPEN | Seed Canonical ممنوع |
| Positive Posting Rules 7B | 0 ACCEPTED | Posting مثبت ممنوع |
| AP-03/AP-04 PostgreSQL 18.6 PoC | PASSED ON BASELINE SHA | DDL نهایی باید دوباره آزمون شود |
| AP-05 backup/restore عملیاتی | NOT VERIFIED | پیش از production باید اجرا شود |
| AP-07 privilege matrix | DEFINED + DISPOSABLE POC PASSED | grant دائمی هنوز اعمال نشده |
| AP-08 source crosswalk | PARTIAL / OPEN | canonical import و Posting مسدود |
| ناظر روی SHA نهایی v0.2 | PENDING | PR Draft باقی می‌ماند |
| مجوز صریح SQL برای Migration 0009 | NOT GRANTED | SQL/Migration ایجاد نشود |

حکم آمادگی: طراحی به مرز آماده بازبینی نهایی برای ساخت schema رسیده است. دیتابیس می‌تواند در مرحله مجاز بعدی به‌صورت خالی از COA Canonical ساخته شود و بعد از ورود شواهد، رجیستر و unresolvedها را نگهداری کند. ورود Canonical COA و Posting همچنان مسدود است. هیچ SQL در این نسخه تولید نشده است.

## 14. شواهد CI مبنای نسخه 0.2

آخرین اجرای کامل قبل از commit این نسخه روی SHA b0e4a7c9949d4b7572a46ab674fdeac4905c7ef7:
- MD-Gsm Persons Module Gates run 44351210766: تمام 13 Job موفق، شامل PostgreSQL Linux و Windows، AP-03/AP-04 PoC، Foundation، checksum و evidence enforcement.
- CodeQL run 44351210712: موفق.
- Windows Preview Installer run 44351210970: موفق.

این نتایج به SHA مبنا تعلق دارند. هر commit جدید v0.2 باید CI را روی SHA جدید مجدداً اجرا کند؛ تا آن زمان CI نسخه 0.2 برابر NOT VERIFIED است.

## 15. تصمیم‌های خارج از دامنه

این نسخه هیچ تغییر در Architecture Master v2.5، Migration registry، 7B Posting Matrix، 7C canonical account attributes، Tax Matrix، Inventory ownership، source-event contracts یا مجوزهای Posting ایجاد نمی‌کند. هر تغییر در این حوزه‌ها نیازمند تصمیم/شواهد مستقل است.
