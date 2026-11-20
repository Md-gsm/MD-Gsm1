# طراحی فیزیکی پایگاه داده هسته حسابداری
## MDG-ACC-PDS-001 — نسخه 0.1

| مشخصه | مقدار |
|---|---|
| وضعیت | HISTORICAL / SUPERSEDED BY MDG-ACC-PDS-001 v0.3; not an active implementation or DDL authority |
| تاریخ | 2026-11-18 |
| مالک داده | Accounting (ACC-01, ACC-02, FY-01) |
| معماری والد | MDG-ARCH-MASTER-001 v2.5 |
| دامنه تصمیم | MDG-ADR-0018 |
| مرجع COA | MDG-COA-MASTER-001 v1.2 (اصلی)، v1.4 (مکمل) |
| جایگاه Migration | 0009 در main رزرو شده؛ SQL همچنان مجاز نشده است |

## 1. هدف و مرز

این سند طراحی منطقی-فیزیکی persistence برای هسته Accounting را تثبیت می‌کند تا پیش از هر SQL یا EF mapping، مرز جدول‌ها، کلیدها، قیود، ایندکس‌ها، هم‌زمانی و بازیابی روشن باشد.

این سند به‌تنهایی مجوز ایجاد فایل SQL، افزودن Migration 0009، تغییر DbContext، نوشتن Journal در PostgreSQL، ایجاد Posting API، اجرای Posting، واردکردن COA، Seed حساب یا استقرار نیست. Migration 0009 همچنان `RESERVED — SQL NOT AUTHORIZED YET` است.

هیچ Tax Engine، Tax Line، Tax Posting، Posting Rule مثبت، adapter رویداد Inventory/Sales/Purchasing/Treasury/RepairOperations یا integration خارج از Accounting در این طراحی ایجاد نمی‌شود. Preflight موجود فقط خواندنی باقی می‌ماند.

## 2. مبانی قطعی استخراج‌شده از مدل فعلی

طراحی با مدل‌های موجود زیر سازگار است:
- `AccountDefinition` و `CanonicalAccountAttributes`: 27 فیلد شواهدی اجباری و 23 فیلد که باید Verified باشند؛ فهرست دقیق فقط از کد Domain گرفته می‌شود و در این سند دوباره‌تعریف نمی‌شود.
- `FieldEvidenceValidator`: برای هر فیلد دقیقاً یک evidence؛ Verified نیازمند value و source و بدون rationale؛ NotApplicable نیازمند source و rationale و بدون value؛ Unverified هرگز برای عبور از completeness کافی نیست.
- `FiscalPeriod`: بازه inclusive از `StartsOn` تا `EndsOn`، وضعیت Open/Closed، هشت attestation برای بستن دوره و الزام درخواست‌کننده/تأییدکننده مستقل برای بازگشایی.
- `Journal`: Draft/Finalized، حداقل دو خط، LineId یکتا، توازن بدهکار/بستانکار، ثبت UTC، audit references و سند reversal مستقل.
- `JournalLine`: دقیقاً یکی از Debit/Credit مثبت؛ مقادیر منفی ممنوع؛ AccountId و AccountCode snapshot؛ PartySubledgerReference و DimensionsJson.
- `PostingIdentity`: کلید SHA-256 از اجزای canonical شامل SourceModule, SourceDocumentId, SourceEventId, PostingPurpose, SourceVersion, SourceSnapshotId و AllocationId.

هیچ مقدار COA یا AccountId از نام، prefix یا کد حساب حدس زده نمی‌شود. هیچ حساب canonical در این طرح seed نمی‌شود.

## 3. تصمیم‌های طراحی

1. Schema مستقل PostgreSQL با نام `accounting`؛ schema پیش‌فرض فعلی DbContext یعنی `identity` تغییر نمی‌کند.
2. هر Journal به یک COA snapshot تغییرناپذیر متصل است. این الزام به معنی افزودن `CoaSnapshotId` به مدل Domain و قراردادهای ساخت Journal پیش از persistence است (AP-01).
3. Idempotency identity و محتوای درخواست دو مفهوم مجزا هستند. علاوه بر کلید موجود PostingIdentity، SHA-256 از payload canonical و نسخه canonicalization ذخیره می‌شود (AP-02).
4. مقادیر مالی IRR در ستون‌های `bigint` ذخیره می‌شوند؛ هیچ float/double یا decimal برای مبلغ سند حسابداری استفاده نمی‌شود.
5. Account metadata و evidence نسخه‌دار و وابسته به COA snapshot هستند. مقدار unresolved با مقدار ساختگی جایگزین نمی‌شود.
6. Journal finalized و خطوط آن از طریق privilege و trigger پایگاه داده immutable می‌شوند؛ اصلاح با سند جدید و reversal مستقل است.
7. Audit append-only است. ActorReference به صورت opaque string ذخیره می‌شود و FK مستقیم به IdentityAccess ندارد.
8. شناسه‌های ماژولی و اسناد بیرونی در این slice opaque reference هستند؛ FK بین‌ماژولی ساخته نمی‌شود.
9. هیچ outbox یا event publisher در این schema اضافه نمی‌شود؛ قرارداد outbox مشترک فقط هنگام مجوز و طراحی integration جداگانه اعمال خواهد شد.

## 4. موجودیت‌ها و جدول‌ها

### 4.1 `accounting.coa_snapshots`

هدف: ثبت منبع و نسخه دقیق داده COA که هر حساب و Journal به آن متصل است.

ستون‌ها:
- `coa_snapshot_id uuid PRIMARY KEY`
- `authority_document text NOT NULL`
- `authority_version text NOT NULL`
- `source_reference text NOT NULL`
- `source_sha256 char(64) NOT NULL`
- `imported_at_utc timestamptz NOT NULL`
- `imported_by text NOT NULL` (opaque ActorReference)
- `status text NOT NULL`؛ حداقل Draft/Accepted/Retired، با تغییر وضعیت audit شده.

قیود:
- SHA-256 دقیقاً 64 رقم hex lowercase باشد.
- یکتایی authority_document + authority_version + source_sha256.
- Snapshot پذیرفته‌شده تغییرناپذیر است؛ اصلاح منبع با snapshot جدید انجام می‌شود.
- ایجاد Snapshot یا Import داده در این مرحله مجاز نیست؛ v1.2 مرجع اصلی و v1.4 مکمل است و mapping نهایی باید با crosswalk مستند اثبات شود.

### 4.2 `accounting.accounts`

هدف: نگهداری تعریف حساب در محدوده یک snapshot، بدون اختراع شناسه یا ویژگی canonical.

ستون‌ها:
- `coa_snapshot_id uuid NOT NULL`
- `account_code text NOT NULL`
- `account_id uuid NULL`؛ فقط وقتی منبع/تصمیم معتبر آن را اثبات کند.
- `title_fa text NOT NULL`; `title_en text NULL`
- `parent_account_id uuid NULL`
- `level integer NULL`; `account_class text NULL`
- `nature text NULL`; `normal_balance text NULL`
- `postable boolean NULL`; `direct_posting_allowed boolean NULL`
- `status text NOT NULL`
- `effective_from date NULL`; `effective_to date NULL`
- `subledger_type text NULL`
- `tax_mode integer NULL`; `tax_treatment text NULL`
- `recognition_gate text NULL`; `reconciliation_policy text NULL`
- `aging_policy text NULL`; `statement_mapping text NULL`
- `definition_version integer NULL`
- `source_reference text NULL`; `decision_reference text NULL`; `change_request_reference text NULL`; `audit_reference text NULL`
- PRIMARY KEY (`coa_snapshot_id, account_code`); UNIQUE (`coa_snapshot_id, account_id`) با رفتار طبیعی PostgreSQL برای NULLهای متعدد.

قیود و رفتار:
- FK به `coa_snapshots` با ON DELETE RESTRICT.
- کد حساب فقط رشته رقم ASCII مطابق Domain؛ مقدار آن از سند معتبر می‌آید.
- `effective_to >= effective_from` در صورت وجود هر دو.
- مقادیر enum-like با CHECK محدود می‌شوند، اما دامنه مقادیر فقط از مدل و اسناد پذیرفته‌شده گرفته می‌شود؛ مقدار جدید حدس زده نمی‌شود.
- `account_id` NULL یا هر فیلد canonical ناقص/Unverified به معنی غیرقابل‌استفاده بودن برای posting است.
- FK self-reference برای Parent فقط در همان snapshot و فقط وقتی parent canonical شناخته شده باشد.
- حساب ممنوع 110809 در validator و DB guard باید fail-closed بماند؛ این سند مجوز ایجاد یا تغییر mapping آن نیست.

### 4.3 `accounting.account_field_evidence`

هدف: نگهداری یک شاهد مستقل برای هر فیلد از مجموعه 27 فیلد تعریف‌شده در `CanonicalAccountAttributes.RequiredFields`.

ستون‌ها:
- `coa_snapshot_id uuid NOT NULL`, `account_code text NOT NULL`
- `field_name text NOT NULL`
- `disposition text NOT NULL`؛ Verified / NotApplicable / Unverified
- `field_value text NULL`
- `source_reference text NOT NULL`
- `rationale text NULL`
- PRIMARY KEY (`coa_snapshot_id, account_code, field_name`)

قیود:
- FK مرکب به accounts با ON DELETE RESTRICT.
- Verified: value غیرخالی و rationale خالی.
- NotApplicable: value NULL و rationale غیرخالی.
- Unverified: برای نگهداری وضعیت unresolved مجاز است ولی هرگز gate را باز نمی‌کند.
- completeness برابر دقیقاً یک ردیف برای هر یک از 27 فیلد است؛ تکرار، فقدان، فیلد ناشناخته یا evidence بدون source باعث رد می‌شود.
- مجموعه 23 فیلد RequiredVerified از Domain authority است و باید در validator و DB posting guard به‌صورت یکسان آزمون شود. فهرست در این سند کپی و مستقل نمی‌شود تا drift ایجاد نکند.

### 4.4 `accounting.account_event_eligibility`

هدف: ثبت disposition مستند هر FinancialEvent برای حساب در همان snapshot.

ستون‌ها:
- `coa_snapshot_id uuid NOT NULL`, `account_code text NOT NULL`
- `financial_event_id text NOT NULL`
- `eligibility text NOT NULL`؛ ALLOW / FORBID
- `source_reference text NOT NULL`
- `decision_reference text NOT NULL`
- PRIMARY KEY (`coa_snapshot_id, account_code, financial_event_id`)

قیود:
- FK مرکب به accounts.
- فقط تصمیم پذیرفته‌شده و evidence-backed قابل ثبت است.
- نبودن ردیف به معنی عدم اجازه است؛ هیچ default-ALLOW وجود ندارد.
- این جدول Posting Rule نیست و هیچ FE Posting Rule تولید یا تأیید نمی‌کند. تا تکمیل crosswalk، ALLOW مثبت ایجاد نمی‌شود.
- validator باید تعارض ALLOW/FORBID، شناسه event ناشناخته و evidence ناقص را fail-closed رد کند.

### 4.5 `accounting.fiscal_periods`

ستون‌ها:
- `period_id uuid PRIMARY KEY`
- `starts_on date NOT NULL`, `ends_on date NOT NULL`
- `status text NOT NULL`؛ Open / Closed
- `version bigint NOT NULL DEFAULT 1` برای optimistic concurrency
- `last_changed_by text NOT NULL`
- `last_changed_at_utc timestamptz NOT NULL`
- `last_audit_event_id text NOT NULL`

قیود:
- `starts_on <= ends_on`; version > 0.
- هیچ بازه دو FiscalPeriodای هم‌پوشانی ندارد، چه Open چه Closed. از exclusion constraint مبتنی بر `daterange(starts_on, ends_on, '[]') WITH &&` استفاده شود؛ دامنه inclusive با Domain یکسان است.
- تاریخ Journal باید داخل همان دوره Open باشد.
- تغییر وضعیت با قفل ردیف و کنترل version انجام می‌شود؛ در conflict، عملیات retry خودکار بدون revalidation مجاز نیست.

### 4.6 `accounting.period_transition_events`

هدف: سابقه append-only هر Close/Reopen و شاهد تصمیم.

ستون‌ها:
- `transition_id uuid PRIMARY KEY`
- `period_id uuid NOT NULL`
- `from_status text NOT NULL`, `to_status text NOT NULL`
- `requested_by text NOT NULL`, `approved_by text NULL`
- `reason text NOT NULL`
- `transitioned_at_utc timestamptz NOT NULL`
- `audit_event_id text NOT NULL`
- هشت attestation مدل `PeriodCloseEvidence` به صورت boolean nullable برای رویداد Close: BankReconciled, CashReconciled, PartySubledgersReconciled, InventoryReconciled, TaxReconciled, SuspenseAccountsResolved, NoPendingJournals, NoPendingApprovalManualJournals.

قیود:
- FK به fiscal_periods و audit_events با RESTRICT.
- Close فقط Open→Closed؛ همه هشت attestation باید true باشند.
- Reopen فقط Closed→Open؛ requested_by و approved_by باید غیرتهی و متفاوت باشند؛ reason الزامی.
- رویدادها update/delete نمی‌شوند.
- TaxReconciled فقط یک attestation ثبت‌شده برای کنترل دوره است؛ این جدول Tax Engine، Tax Line یا Tax Posting ایجاد نمی‌کند و TaxMode=0 همچنان Tax Lines/Posting را ممنوع می‌کند.

### 4.7 `accounting.journals`

ستون‌ها:
- `journal_id uuid PRIMARY KEY`
- `fiscal_period_id uuid NOT NULL`
- `journal_date date NOT NULL`
- `coa_snapshot_id uuid NOT NULL`
- `idempotency_key char(64) NOT NULL UNIQUE`
- `source_document_reference text NOT NULL`
- `status text NOT NULL`؛ Draft / Finalized
- `created_by text NOT NULL`, `created_at_utc timestamptz NOT NULL`, `created_audit_event_id text NOT NULL`
- `finalized_by text NULL`, `finalized_at_utc timestamptz NULL`, `finalized_audit_event_id text NULL`
- `reversal_of_journal_id uuid NULL`
- `version bigint NOT NULL DEFAULT 1`

قیود:
- FK به fiscal_periods، coa_snapshots، posting_idempotency و audit_events با RESTRICT؛ reversal self-FK با RESTRICT. رابطه یک‌به‌یک با posting_idempotency فقط از journals به کلید idempotency برقرار می‌شود تا FK دوری ایجاد نشود.
- قید سازگاری: Draft فاقد finalized metadata؛ Finalized دارای finalized_by/at/audit و زمان UTC.
- journal_date باید در بازه دوره باشد و هنگام Finalize دوره Open باشد.
- reversal فقط به Journal Finalized اشاره می‌کند و identity مستقل با PostingPurpose=REVERSAL دارد؛ reversal خود نیز سند مستقل است.
- AP-01: مدل Journal فعلی فاقد CoaSnapshotId است؛ پیش از persistence باید Domain factoryها، reversal و تست‌ها آن را اجباری کنند. بدون snapshot شناسه‌ای Journal قابل ذخیره نیست.
- اجزای PostingIdentity فقط یک‌بار در posting_idempotency نگهداری می‌شوند؛ Journal فقط idempotency_key را reference می‌کند تا داده تکراری و drift ایجاد نشود.

### 4.8 `accounting.journal_lines`

ستون‌ها:
- `line_id uuid PRIMARY KEY`
- `journal_id uuid NOT NULL`
- `line_number integer NOT NULL`
- `coa_snapshot_id uuid NOT NULL`
- `account_id uuid NOT NULL`
- `account_code_snapshot text NOT NULL`
- `debit_irr bigint NOT NULL DEFAULT 0`
- `credit_irr bigint NOT NULL DEFAULT 0`
- `party_subledger_reference text NULL` (opaque PartyId/reference)
- `dimensions jsonb NULL`

قیود:
- UNIQUE (`journal_id, line_number`) و UNIQUE (`journal_id, line_id`).
- FK به journals و FK مرکب (`coa_snapshot_id, account_id`) به accounts(account snapshot, canonical account ID)؛ account_id باید در snapshot دارای شناسه معتبر باشد.
- دقیقاً یکی از debit_irr یا credit_irr مثبت؛ دیگری صفر؛ هر دو non-negative؛ صفر/صفر و هر دو مثبت ممنوع.
- حداقل دو خط و توازن مجموع debit/credit با deferred constraint trigger در انتهای transaction کنترل می‌شود؛ overflow مجموع با numeric accumulator در trigger و حد long در مرز Domain کنترل شود.
- account_code_snapshot باید با حساب مرجع در همان snapshot برابر باشد.
- پس از Finalize هیچ INSERT/UPDATE/DELETE روی خطوط مجاز نیست.
- dimensions فقط JSON معتبر است؛ schema و dimension semantics در قرارداد جداگانه تعیین می‌شود. هیچ dimension دلخواهی مجوز توسعه قرارداد نیست.

### 4.9 `accounting.posting_idempotency`

ستون‌ها:
- `idempotency_key char(64) PRIMARY KEY`
- `source_module text NOT NULL`
- `source_document_id text NOT NULL`
- `source_event_id text NOT NULL`
- `posting_purpose text NOT NULL`
- `source_version text NOT NULL`
- `source_snapshot_id text NULL`
- `allocation_id text NULL`
- `canonicalization_version integer NOT NULL`
- `request_payload_sha256 char(64) NOT NULL`
- `created_at_utc timestamptz NOT NULL`

قیود:
- هر دو hash دقیقاً 64 رقم hex lowercase.
- UNIQUE روی ترکیب identity components با `NULLS NOT DISTINCT` برای optional components، تا دو representation یکسان با key متفاوت ساخته نشود.
- Journal از طریق `journals.idempotency_key` به این جدول FK می‌دهد؛ `posting_idempotency` عمداً `journal_id` ندارد تا چرخه FK ایجاد نشود. unique بودن کلید در هر دو سمت، رابطه یک‌به‌یک را برقرار می‌کند.
- همان key + همان payload hash + همان canonicalization_version: Journal قبلی از طریق idempotency_key بازیابی می‌شود.
- همان key با payload hash یا canonicalization_version متفاوت: درخواست رد و conflict یکپارچگی audit می‌شود؛ هیچ overwrite انجام نمی‌شود.
- رقابت هم‌زمان با UNIQUE PK تعیین تکلیف می‌شود؛ transaction بازنده rollback می‌کند و پس از خواندن رکورد برنده، hash و canonicalization_version را مقایسه می‌کند.
- AP-02: payload hash و canonicalization_version باید در Application contract تعریف شوند؛ hash از payload canonical و versioned، نه از JSON serialization تصادفی، محاسبه شود.
- هیچ source event adapter در این slice ساخته نمی‌شود.

### 4.10 `accounting.audit_events`

ستون‌ها:
- `event_id text PRIMARY KEY` (مطابق نوع string در Domain؛ UUID فرض نمی‌شود)
- `event_type text NOT NULL`
- `aggregate_type text NOT NULL`, `aggregate_id text NOT NULL`
- `actor_reference text NOT NULL`
- `occurred_at_utc timestamptz NOT NULL`, `recorded_at_utc timestamptz NOT NULL`
- `correlation_id text NULL`, `causation_id text NULL`
- `source_reference text NULL`, `reason text NULL`
- `details jsonb NOT NULL DEFAULT '{}'::jsonb`

قیود:
- append-only؛ UPDATE/DELETE/TRUNCATE برای runtime ممنوع و با trigger دفاعی رد شود.
- actor opaque است؛ FK مستقیم به IdentityAccess ممنوع.
- details باید فاقد credential، token، password، secret و PII غیرضروری باشد؛ جزئیات فقط حداقل داده لازم.
- ثبت audit در همان transaction با aggregate مربوط انجام می‌شود؛ audit مستقلِ موفقیت‌آمیز بدون commit aggregate یا بالعکس مجاز نیست.

## 5. روابط و کلیدهای خارجی

- coa_snapshots 1—N accounts
- accounts 1—N account_field_evidence
- accounts 1—N account_event_eligibility
- fiscal_periods 1—N journals
- fiscal_periods 1—N period_transition_events
- coa_snapshots 1—N journals
- journals 1—N journal_lines
- journals 1—1 posting_idempotency
- journals 0—N journals از مسیر reversal_of_journal_id
- audit_events به‌عنوان مرجع مستقل و append-only برای تغییرات حساس.

تمام FKهای مالی از ON DELETE RESTRICT استفاده می‌کنند. Cascade delete برای داده مالی ممنوع است.

## 6. ایندکس‌های لازم

- accounts: UNIQUE(coa_snapshot_id, account_code)، UNIQUE(coa_snapshot_id, account_id) و index روی (coa_snapshot_id, status, postable).
- evidence: PK مرکب و index روی (coa_snapshot_id, account_code, disposition).
- eligibility: PK مرکب و index روی (financial_event_id, eligibility).
- fiscal_periods: exclusion GiST برای overlap و index روی (status, starts_on, ends_on).
- period_transition_events: (period_id, transitioned_at_utc DESC).
- journals: (fiscal_period_id, journal_date, status)، (coa_snapshot_id, journal_date)، UNIQUE(idempotency_key)، index روی reversal_of_journal_id.
- journal_lines: UNIQUE(journal_id, line_number)، index روی (account_id, journal_id)، index روی party_subledger_reference در صورت اثبات query workload.
- posting_idempotency: PK key، UNIQUE(identity components)، index روی created_at_utc فقط اگر retention/operations نیاز آن را اثبات کند.
- audit_events: (aggregate_type, aggregate_id, recorded_at_utc DESC)، (correlation_id) در صورت نیاز عملیاتی.

از ایندکس‌های speculative یا پرهزینه بدون query plan واقعی اجتناب شود.

## 7. تراکنش‌ها و هم‌زمانی

### Finalize Journal
1. آغاز transaction.
2. قفل ردیف FiscalPeriod با SELECT ... FOR UPDATE و بررسی version/status/date.
3. بررسی snapshot Accepted و حساب‌های قابل استفاده در همان snapshot؛ evidence completeness و event eligibility باید fail-closed باشد.
4. کنترل خطوط، حداقل دو خط و توازن؛ deferred trigger نیز همان invariant را در انتهای transaction بررسی می‌کند.
5. ثبت audit finalize و تغییر status به Finalized با optimistic version.
6. commit؛ هر خطا کل transaction را rollback می‌کند.

### Idempotent persistence
1. محاسبه PostingIdentity موجود و payload hash canonical/versioned.
2. درج idempotency record با unique key در transaction.
3. ایجاد Journal با همان idempotency_key و خطوط؛ FK در انتهای transaction برقرار است.
4. در duplicate key، transaction بازنده rollback؛ سپس رکورد موجود خوانده و payload hash و canonicalization_version مقایسه شود.
5. تطابق کامل => Journal موجود از طریق idempotency_key بازیابی شود؛ هر mismatch => رد قطعی و audit conflict در transaction مستقل و محدودشده، بدون تغییر Journal قبلی.

### Close/Reopen period
- قفل ردیف دوره و بررسی version در همان transaction.
- Close: تمام هشت attestation باید true باشد و هیچ Journal Draft/در انتظار باقی نمانده باشد؛ transition event و audit هم‌زمان درج شوند.
- Reopen: requester != approver، reason و audit الزامی؛ ثبت event و تغییر دوره اتمیک.
- پس از commit، درخواست‌های هم‌زمان با وضعیت جدید revalidate می‌شوند؛ هیچ cached preflight مجوز commit نیست.

## 8. امنیت و privilege

- نقش migrator/deployment مالک schema و تنها مجاز به DDL است.
- نقش runtime مالک هیچ schema/table نیست.
- runtime مجوز SELECT/INSERT/UPDATE محدود بر draftها و state transitions مشخص را دارد؛ UPDATE/DELETE روی finalized journal/lines، audit، transition history و accepted COA snapshot ممنوع.
- Triggerهای immutable و deferred validation دفاع لایه دوم‌اند؛ جایگزین application authorization نیستند.
- هیچ privilege عمومی، schema CREATE یا default grant وسیع داده نمی‌شود.
- ماتریس grant برای Accounting باید جداگانه طراحی و review شود؛ مجوز IdentityAccess به‌طور ضمنی به Accounting تسری ندارد.

## 9. بازیابی و Migration lifecycle

- پیش از هر schema change: backup دیتابیس disposable، restore به دیتابیس جدا و ثبت نتیجه.
- قبل از وجود داده مالی واقعی: rollback مخرب فقط برای محیط disposable و با guard صریح و backup تأییدشده قابل بررسی است؛ rollback عمومی در runner فعلی وجود ندارد.
- پس از ایجاد داده مالی: migration مخرب/Down ممنوع؛ فقط forward-fix نسخه‌دار و بازبینی‌شده.
- runner فعلی Down/Rollback ندارد؛ این سند وجود rollback اجرایی را ادعا نمی‌کند.
- Migration 0009 فقط پس از تأیید مستقل همین طراحی، رفع موارد باز و مجوز صریح جداگانه برای نوشتن SQL می‌تواند وارد مرحله بعد شود.
- هیچ migration در این PR ایجاد نمی‌شود.

## 10. موارد باز پیش از SQL

| شناسه | موضوع | اقدام لازم | وضعیت |
|---|---|---|---|
| AP-01 | COA snapshot در Journal Domain | `Journal.CoaSnapshotId` اجباری شد؛ factory، finalize، reversal و تست‌های مثبت/منفی به‌روزرسانی شدند. Persistence mapping همچنان مرحله بعد است. | DOMAIN IMPLEMENTED — CI NOT YET VERIFIED |
| AP-02 | payload conflict برای idempotency | قرارداد `ICanonicalPostingPayload`، fingerprint نسخه‌دار SHA-256 و policy تشخیص replay/conflict در Application اضافه شد؛ تست payload یکسان، payload متفاوت، نسخه متفاوت و درخواست جدید افزوده شد. | APPLICATION CONTRACT + UNIT TESTS IMPLEMENTED — CI NOT YET VERIFIED |
| AP-03 | deferred balance trigger | PoC مستقل در schema آزمایشی `ap_proof`؛ حداقل دو خط، توازن، overflow و بررسی deferred در commit | PREVIOUS POC RUN PASSED; LATEST GRANT-ALIGNED RUN PENDING |
| AP-04 | finalized immutability | PoC مستقل برای header/lines، append-only audit/transition، accepted COA snapshot، TRUNCATE guards و runtime least-privilege | PREVIOUS POC RUN PASSED; LATEST GRANT-ALIGNED RUN PENDING |
| AP-05 | recovery policy | افزودن `MDG-ACC-OPS-REC-001` برای guard محیط disposable، backup/restore، recovery پیش و پس از داده مالی و forward-only repair | RUNBOOK ADDED; OPERATIONAL RECOVERY NOT VERIFIED |
| AP-06 | registry CI | اصلاح MigrationResourceTests تا RESERVED future slot را بدون SQL مجاز بداند، اما IMPLEMENTED/VERIFIED را با SQL تطبیق دهد | INCLUDED IN THIS PR |
| AP-07 | DB privilege | افزودن `MDG-ACC-SEC-PRIV-001` و هم‌راستاسازی grants و آزمون runtime least-privilege در PoC PostgreSQL | MATRIX + TESTS ADDED; LATEST CI PENDING |
| AP-08 | COA evidence crosswalk | تکمیل crosswalk معتبر v1.2/v1.4؛ بدون seed و بدون حدس | BLOCKED BY SOURCE EVIDENCE |

### وضعیت اجرای AP-05 و AP-07 در برش فعلی

- AP-05: رویه `MDG-ACC-OPS-REC-001` اضافه شد. این runbook بین کنارگذاشتن دیتابیس disposable پیش از داده مالی و forward-only recovery پس از ایجاد داده مالی تمایز می‌گذارد. این سند اجرای backup/restore عملیاتی Accounting را ادعا نمی‌کند.
- AP-07: ماتریس `MDG-ACC-SEC-PRIV-001` اضافه شد. Runtime برای خطوط و append-only records مجوز UPDATE/DELETE ندارد؛ UPDATE روی Journal فقط به ستون‌های transition نهایی‌سازی محدود می‌شود؛ COA و FiscalPeriod برای runtime read-only باقی می‌مانند.
- PoC قبلی AP-03/AP-04 روی PostgreSQL 18.6 موفق شد؛ پس از هم‌راستاسازی grants با ماتریس AP-07، CI جدید باید دوباره روی SHA نهایی اجرا شود.
- هیچ GRANT واقعی روی schema دائمی Accounting اعمال نشده و هیچ نقش عملیاتی ساخته نشده است.

### وضعیت اجرای AP-03 و AP-04 در برش فعلی

- دو فایل PoC فقط زیر مسیرهای `tests/` و `scripts/ci/` افزوده شده‌اند؛ این‌ها migration نیستند و schema دائمی `accounting` یا Migration 0009 ایجاد نمی‌کنند.
- PoC در schema موقت `ap_proof` روی همان دیتابیس disposable علامت‌گذاری‌شده اجرا می‌شود و پس از پایان job همراه دیتابیس آزمایشی حذف خواهد شد.
- AP-03: deferred constraint trigger در پایان transaction حداقل دو خط، برابری مجموع بدهکار/بستانکار و سقف bigint را بررسی می‌کند. آزمون‌های منفی برای یک‌خطی، عدم توازن و overflow افزوده شده‌اند.
- AP-04: triggerهای دفاعی برای header/lines اسناد نهایی، audit و period-transition append-only، snapshot پذیرفته‌شده و TRUNCATE افزوده شده‌اند؛ privilegeهای runtime نیز جداگانه بررسی می‌شوند.
- اجرای واقعی فقط پس از CI روی PostgreSQL 18.6 قابل تأیید است؛ تا آن زمان AP-03/AP-04 برابر `NOT VERIFIED` باقی می‌مانند.
- این PoC هنوز جایگزین schema review و پیاده‌سازی نهایی Migration 0009 نیست؛ تفاوت‌های محیط آزمایشی با DDL نهایی باید در مرحله مجاز SQL دوباره ممیزی شوند.

### وضعیت اجرای AP-01 و AP-02 در برش فعلی

- AP-01 در Domain اعمال شده است: هر Journal شناسه غیرخالی `CoaSnapshotId` می‌خواهد؛ Finalize آن را حفظ می‌کند و Reversal باید snapshot صریح خود را دریافت کند. این تغییر هیچ COA واقعی را seed یا تأیید نمی‌کند.
- AP-02 در Application به‌صورت قرارداد pure اضافه شده است: تولیدکننده payload باید UTF-8 canonical و نسخه‌دار ارائه کند؛ fingerprint با SHA-256 محاسبه می‌شود؛ فقط تطابق هم‌زمان نسخه و hash، replay معتبر است. payload متفاوت یا نسخه متفاوت برای همان identity، Conflict است.
- این قرارداد هنوز persistence/transactional idempotency را اجرا نمی‌کند و به‌تنهایی مانع race در دیتابیس نیست؛ رفتار رقابت هم‌زمان باید در لایه persistence با unique constraint و PostgreSQL integration tests اثبات شود.
- تست‌های جدید به مخزن افزوده شده‌اند؛ تا نتیجه CI روی SHA نهایی، وضعیت آن‌ها `NOT VERIFIED` است.

## 11. آزمون‌های الزامی پیش از پذیرش persistence

- Migration registry: RESERVED path یکتا و بدون فایل؛ IMPLEMENTED/VERIFIED path دقیقاً یک فایل؛ فایل SQL بدون ردیف یا ردیف stale ممنوع.
- PostgreSQL 18 clean install، repeat, checksum drift, out-of-order, missing lower version.
- FK و RESTRICT؛ AccountId null/incomplete/unverified؛ event eligibility missing/FORBID.
- Fiscal period overlap inclusive، boundary dates، close با هر یک از هشت evidence ناقص، concurrent close/reopen، requester=approver.
- Journal کمتر از دو خط، duplicate line id/number، debit/credit صفر/منفی/هر دو مثبت، imbalance و overflow؛ deferred check در commit.
- Finalized journal/line UPDATE/DELETE و audit UPDATE/DELETE/TRUNCATE باید fail شود.
- Idempotency same-key same-payload، same-key different-payload، concurrent duplicate requests و canonicalization version change.
- Transaction rollback باید Journal، lines، idempotency و audit را یکجا برگرداند.
- Backup/restore به دیتابیس جدا و اثبات اجرای migration دوباره با zero pending.
- تمام تست‌ها تا اجرای واقعی در CI وضعیت NOT RUN / NOT VERIFIED دارند.

## 12. معیار خروج از طراحی

طراحی فیزیکی در این سند baseline مورد تأیید مالک است؛ اما عبور به SQL فقط وقتی مجاز است که:
1. AP-01 و AP-02 در Domain/Application و تست‌ها تکمیل و مستقل review شوند.
2. AP-03/AP-04 با PostgreSQL 18 proof-of-concept disposable اجرا و مستقل review شوند.
3. AP-05 و AP-07 تعیین تکلیف و grant matrix بازبینی شود.
4. AP-08 فقط بر اساس شواهد معتبر COA حل شود؛ در غیر این صورت حساب‌ها unresolved باقی می‌مانند و seed نمی‌شوند.
5. رجیستری و تست migration اصلاح‌شده در CI سبز شوند.
6. ناظر مستقل روی SHA نهایی همین بسته تأیید کند.
7. مالک پروژه مجوز جداگانه و صریح ایجاد SQL برای Migration 0009 را صادر کند.

**نتیجه:** این سند طراحی persistence را مشخص می‌کند؛ هیچ جدول، SQL، EF entity، migration، Journal persisted یا Posting runtime در مخزن به وجود نمی‌آورد.
