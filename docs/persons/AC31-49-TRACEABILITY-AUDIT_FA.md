# ممیزی ردیابی معیارهای پذیرش Persons — AC-31 تا AC-49

- تاریخ: 2026-11-08؛ بسته کاری: WG1-01
- مخزن: `Md-gsm/MD-Gsm1`
- SHA مبنا: `177f897cae18d1ef2052c7a153b3404a1bf7421d`
- روش: readback کد، تست، قرارداد، رجیستری و Check Runs روی همان SHA.
- حکم: AC-31..AC-45 برای پذیرش معیاربه‌معیار بازند؛ AC-46..AC-49 در محدوده کنترل CI پیاده‌سازی و تست شده‌اند.

## ۱. قاعده تفسیر

«شواهد جزئی» و «باز» طبقه‌بندی ممیزی‌اند، نه مقادیر schema فایل ماشینی. Validator فقط `UNMAPPED` و `IMPLEMENTED` را می‌پذیرد و برای IMPLEMENTED وجود فایل و متد تست دقیق را الزام می‌کند. بنابراین AC-31..AC-45 در `acceptance-gate-map.json` فعلاً UNMAPPED می‌مانند؛ پیش‌نیاز `acceptanceCriteriaAc31ToAc49Mapped=false` و `g1Authorized=false` حفظ می‌شوند.

## ۲. AC-31 تا AC-38 — Temporary Workflow / Retention

| AC | شواهد و تست موجود در main | شکاف/حکم |
|---|---|---|
| 31 انقضای دقیق UTC | `PersonsTemporaryDataRetentionTests.cs`: `AC31_expiry_blocks_access_at_the_exact_utc_expiry_instant` و `AC31_expiry_comparison_normalizes_non_utc_offsets`؛ تست PostgreSQL `AC32_ciphertext_is_stored_and_expiry_blocks_reads_before_cleanup` | انقضای Domain و خواندن PostgreSQL در مرز زمانی پوشش دارد؛ مسیرهای عملیاتی/API و authorization سرتاسری کامل نیست. **شواهد جزئی؛ باز.** |
| 32 حذف فیزیکی در مهلت | تست PostgreSQL بالا حذف payload، وضعیت purged، صفرشدن payload row و purge دوم با نتیجه صفر را بررسی می‌کند؛ `PurgeExpiredAsync` موجود است. | SLA واقعی ۳۰ دقیقه برای worker و حذف همه مشتقات در آزمون زمان‌مند ثابت نشده؛ تست زمان را مصنوعی جلو می‌برد. **شواهد جزئی؛ باز.** |
| 33 سقف مطلق ۳۰ روز | `AC33_renewal_never_extends_beyond_absolute_thirty_day_age` و تست PostgreSQL `AC33_AC34_renewal_is_single_use_authorized_and_capped_at_thirty_days` | سقف در Domain و DB آزموده شده؛ ماتریس کامل ساعت/ترتیب تمدید و انقضا موجود نیست. **شواهد جزئی؛ باز.** |
| 34 یک تمدید مجاز با دلیل | `AC34_only_one_authorized_renewal_with_a_reason_is_accepted`، `AC34_renewal_rejects_missing_reason_or_missing_authorization` و تست PostgreSQL ترکیبی AC33/34 | تمدید مجاز، نبود مجوز/دلیل و تمدید دوم رد می‌شود؛ ماتریس مجوز واقعی IdentityAccess و audit trail کامل ثابت نشده. **شواهد جزئی؛ باز.** |
| 35 worker idempotent و retry | `AC35_closed_workflow_is_not_readable_and_is_purged_after_expiry`؛ worker دوره‌ای و catch/alert دارد. | close تکراری رد و purge تکراری بی‌اثر است؛ تست مستقیم worker برای خطای گذرا، retry چرخه بعد، شکست alert و SLA پنج‌دقیقه‌ای وجود ندارد. **شواهد جزئی؛ باز.** |
| 36 عدم حضور در global search/autocomplete | payload موقت در جدول جداست؛ `PostgresPartyRepository.SearchAsync` از Party و ContactPoint فعال جست‌وجو می‌کند. | تست منفی صریح workflow موقت در search/autocomplete در main وجود ندارد. **باز؛ تست پذیرش لازم.** |
| 37 promotion اتمیک/idempotent | Quick Registration و انتخاب Party وجود دارد؛ promotion از temporary workflow به Party در کد بررسی‌شده پیدا نشد. | انتخاب صریح، duplicate check، تراکنش اتمیک و replay برای promotion اثبات نشده. **باز؛ قابلیت/تست لازم.** |
| 38 عدم افشای PII در telemetry | worker فقط failure code و نوع exception را log می‌کند و exception.Message را ثبت نمی‌کند؛ تست redaction عمومی AC-49 وجود دارد. | تست اختصاصی payload یکتا در تمام log/audit/metrics/artifacts و اثبات عدم حضورش وجود ندارد. **شواهد جزئی؛ باز.** |

## ۳. AC-39 تا AC-45 — Party Merge

| AC | شواهد و تست موجود در main | شکاف/حکم |
|---|---|---|
| 39 ثبت همه مصرف‌کنندگان PartyId | `consumer-registry.json`، `run_registry` در `validate_persons_gates.py` و تست‌های `test_unknown_partyid_consumer_fails_registry_gate` و `test_unknown_partyid_in_contract_json_fails_registry_gate` | scan رجیستری را با کد تطبیق می‌دهد و مصرف‌کننده ناشناخته gate را fail می‌کند؛ completeness ارجاع‌های غیرمستقیم/خارج از پسوندها و pattern اسکن ثابت نشده. **شواهد جزئی؛ باز.** |
| 40 یک classification برای هر reference | رجیستری و checklist Merge موجودند. | تستی که classification مفقود یا تکراری را رد کند یافت نشد؛ فهرست مصرف‌کننده به‌تنهایی اثبات طبقه‌بندی یکتا نیست. **باز؛ schema/test لازم.** |
| 41 قرارداد نسخه‌دار پنج مرحله‌ای | `IPartyMergeParticipant.cs`، Finance participant، coordinator و تست `AC41_AC44_merge_transfers_registered_reference_atomically_and_replays_idempotently` | happy path Finance موجود است؛ فقط participant واقعی Finance در این برش ثبت شده و سازگاری نسخه/تمام failure stageها برای همه مصرف‌کنندگان ثابت نشده. **شواهد جزئی؛ باز.** |
| 42 preview فقط‌خواندنی | `AC42_preview_is_read_only_and_counts_transferable_finance_references` و `PreviewAsync` | read-only و شمارش referenceهای Finance بررسی می‌شود؛ شمارش جامع historical/blocking همه مصرف‌کنندگان اثبات نشده. **شواهد جزئی؛ باز.** |
| 43 conflict مانع اجرا | `AC43_two_existing_finance_accounts_are_a_blocking_conflict_and_no_state_changes` | conflict دو حساب Finance و عدم تغییر state آزموده شده؛ ماتریس conflict سایر participantها موجود نیست. **شواهد جزئی؛ باز.** |
| 44 atomicity/concurrency/idempotency | تست‌های `AC41_AC44_merge_transfers_registered_reference_atomically_and_replays_idempotently`، `AC44_failure_after_apply_rolls_back_every_participant_and_runs_recovery` و `AC44_concurrent_merges_of_one_source_allow_exactly_one_winner` | replay، rollback و یک برنده در رقابت PostgreSQL آزموده‌اند؛ تمام مصرف‌کنندگان و failure pathها پوشش ندارند. **شواهد جزئی؛ باز.** |
| 45 حفظ snapshot و reconciliation | تست merge شناسه و created_at حساب Finance را پس از انتقال بررسی می‌کند؛ coordinator snapshot تاریخی را immutable فرض می‌کند. | همه snapshotهای تاریخی و reconciliation تمام referenceهای فعال آزموده نشده‌اند. **شواهد جزئی؛ باز.** |

## ۳-الف. شواهد افزوده‌شده در PR #96 (Draft؛ هنوز وارد main نشده‌اند)

موارد زیر فقط روی شاخهٔ PR #96 پیاده‌سازی و آزموده می‌شوند؛ تا زمان merge و بازبینی مستقل، نباید به‌عنوان قابلیت‌های موجود در main گزارش شوند:

| AC | آزمون افزوده‌شده در PR #96 | دامنهٔ اثبات |
|---|---|---|
| 35 | `PersonsTemporaryDataRetentionTests.cs::AC35_worker_retries_on_next_cycle_and_logs_only_redacted_failure_codes` به‌همراه تست‌های PostgreSQL تکرار purge/close | worker روی failure گذرا alert code بدون PII می‌فرستد و در چرخه زمان‌بندی بعدی sweep را دوباره اجرا می‌کند؛ خطای alert نیز فقط با نوع exception ثبت می‌شود و message/PII وارد log نمی‌شود. تست PostgreSQL جداگانه تکرار purge/close را بدون عملیات تکراری اثبات می‌کند. |
| 36 | `PersonsTemporaryWorkflowPostgresTests.cs::AC36_temporary_workflow_payload_does_not_appear_in_Persons_search_or_search_route` | درج ciphertext موقت در PostgreSQL، عدم بازگشت آن از repository search، نبود Search API/autocomplete route و ذخیره‌نشدن marker متنی خام در payload. |
| 39–40 | `PersonsMergeContractTests.cs::AC39_AC40_AllExternalPartyIdConsumersAreRegisteredAndClassifiedExactlyOnce` | تطبیق scan کد با registry، رد مسیرهای تکراری، الزام یک classification مجاز و حذف مصرف‌کننده ناشناخته. طبقه‌بندی در این نسخه به سطح consumer/source path انجام می‌شود. |
| 41 | `PersonsMergeContractTests.cs::AC41_FinanceMergeParticipantImplementsVersionedProtocolHooks` | اثبات پیاده‌سازی participant ثبت‌شدهٔ Finance و تمام hookهای Preview/Prepare/Validate/Apply/Verify/Recover با نسخه‌های قرارداد ثبت‌شده. |
| 42–43 | تست‌های موجود `AC42_preview_is_read_only_and_counts_transferable_finance_references` و `AC43_two_existing_finance_accounts_are_a_blocking_conflict_and_no_state_changes` | preview بدون تغییر state و جلوگیری از merge در تعارض دو حساب Finance. |
| 44 | `PersonsMergeContractTests.cs::AC44_MergeIsAtomicConcurrentAndIdempotentWithFailureRollback` | suite ترکیبی replay/idempotency، rollback پس از خطای Verify و انتخاب دقیقاً یک برنده در mergeهای هم‌زمان. |

در تکمیل شواهد AC-38 روی شاخه PR #96، تست `PersonsTemporaryDataRetentionTests.cs::AC38_structured_retention_alert_accepts_only_closed_failure_code_and_emits_no_payload` نیز ثبت شد. این تست نشان می‌دهد adapter فعلی alert فقط enum بستهٔ failure code می‌پذیرد و event ساختاریافته payload/PII دریافت یا چاپ نمی‌کند. این شواهد محدود است؛ AC-38 همچنان UNMAPPED می‌ماند تا تمام مسیرهای logging، audit، metrics و CI artifacts با sentinelهای PII پوشش داده شوند.

AC-45 و AC-31..34، AC-37..38 همچنان باز هستند. این تست‌ها فقط بر مصرف‌کنندگان و runtime فعلیِ ثبت‌شده در مخزن تکیه دارند؛ اثبات تمام consumerهای آینده، همهٔ historical snapshotها، تمام failure stageها یا SLA عملیاتی production محسوب نمی‌شوند.

## ۴. AC-46 تا AC-49 — CI/evidence

| AC | متد تست ثبت‌شده در acceptance map | نتیجه |
|---|---|---|
| 46 | `scripts/ci/test_persons_gate_policy.py::test_required_jobs_are_unconditionally_scheduled` | CI enforcement موفق |
| 47 | `test_failed_gate_cannot_be_relabelled_pass_na` | CI enforcement موفق |
| 48 | `test_g1_authorization_without_external_evidence_fails` | CI enforcement موفق |
| 49 | `test_ci_evidence_manifest_is_traceable_and_redacted` | CI evidence موفق |

این چهار معیار کنترل سیاست CI/evidence هستند و جایگزین پذیرش runtime معیارهای 31 تا 45 نیستند.

## ۵. CI دقیق روی SHA مبنا

GitHub Check Runs روی main و SHA `177f897cae18d1ef2052c7a153b3404a1bf7421d`: **15/15 success، صفر failure، صفر pending**. Persons workflow run: `42464749823`. Gateهای consumer-registry، retention، merge-contract، Linux/Windows PostgreSQL migrations، security، API contract، disposable E2E و CI evidence موفق‌اند. PostgreSQL فقط روی DBهای disposable CI آزموده شده؛ DB واقعی/Production تغییر نکرده است. سبز بودن CI کمبودهای criterion-level جدول‌ها را رفع نمی‌کند.

## ۶. مالکیت و مرز تغییر

- Persons مالک PartyId، هویت حقیقی/حقوقی، ContactPoint، Address، نقش تجاری و API/persistence اختصاصی خود است.
- IdentityAccess مالک account، credential، session و authorization است.
- Finance مالک حساب Party و داده مالی است؛ حساب/مانده موازی در Persons ممنوع.
- Organization مالک Branch/Warehouse و membership/scope؛ RepairOperations مالک مهارت و تخصیص تکنسین است.
- مصرف‌کنندگان فقط Contracts عمومی Persons را مصرف می‌کنند.
- طبق ADR-0014 و ADR-0016، internals اشخاص هنوز انتقالی و در `src/BuildingBlocks` هستند. انتقال به `src/Modules/Persons/{Domain,Application,Infrastructure}` باید هماهنگ با WG-0 و شامل project/namespace/DI/CI/tests باشد؛ migration اعمال‌شده بازنویسی نمی‌شود.
- مسیرهای مشترک `src/Host/Api/Program.cs`، `src/BuildingBlocks/MDGsm.Infrastructure/Persistence/**`، `src/BuildingBlocks/MDGsm.Client/**`، `src/BuildingBlocks/MDGsm.Application/**` و `Directory.Build.props` با WG-0 هماهنگ می‌شوند.

## ۷. شاخه‌ها و PRهای تاریخی

PR #47 بسته و `merged=false` است؛ بدنه آن **Superseded / Do Not Merge** اعلام می‌کند و می‌گوید منتخب‌ها پس از بازسازی در PR #50 وارد main شده‌اند. شاخه `feature/persons-ac31-45-runtime` در مقایسه با main فعلی divergent (146 جلو و 15 عقب) بود و مبنای توسعه/cherry-pick نیست. هنگام ممیزی هیچ PR بازی وجود نداشت. مبنا main جاری و کد بازسازی‌شده PR #50 است.

## ۸. نتیجه WG1-01 و گام بعد

**WG1-01 از نظر ممیزی و تفکیک شواهد این دور تکمیل شد؛ AC-31..AC-45 از نظر پذیرش runtime همچنان OPEN هستند.**

گام بعدی: (1) AC-31..34 و AC-37..38 برای SLA حذف، authorization/audit تمدید، promotion و redaction جامع؛ (2) AC-45 برای snapshot تاریخی و reconciliation همه ارجاعات؛ (3) پیش از تغییرات schema/Outbox/quota، تصمیم Owner/WG-0 و رزرو migration از مسیر Issue #97/#98؛ (4) چرخه کامل ContactPoint/Address و انتقال internals مطابق ADR-0016 در workstream هماهنگ جداگانه.

این سند فقط ممیزی مستنداتی است؛ runtime، schema، migration، DB، تنظیم زنده GitHub یا تصمیم معماری را تغییر نمی‌دهد. `g1Authorized=false` باقی است.


## AC-37 — promotion evidence update (2026-11-25)

**Current criterion status: UNMAPPED / independent review pending.** Do not mark IMPLEMENTED or G1-ready solely from CI.

### PostgreSQL integration test coverage

Test file: `tests/MDGsm.Domain.Tests/Persons/PersonsTemporaryWorkflowPromotionPostgresTests.cs`

- `AC37_authorization_matrix_denies_each_required_permission_before_mutation` — New Party requires promote/read/create; Existing Party requires promote/read; each missing permission is denied before state mutation.
- `AC37_existing_party_path_does_not_require_create_permission` — positive Existing Party route with create permission denied.
- `AC37_unprotectable_payload_is_rejected_without_state_change_and_can_retry_after_repair` — corrupted authenticated ciphertext fails closed, retains workflow/payload, and same OperationId succeeds after test-only payload repair.
- `AC37_workflow_close_failure_rolls_back_and_same_operation_retries` — injected failure while transitioning workflow to promoted rolls back Party/operation/payload/tombstone state and retry succeeds.
- `AC37_payload_purge_failure_rolls_back_and_same_operation_retries` — injected payload deletion failure rolls back and retry succeeds.
- `AC37_failure_at_tombstone_insert_rolls_back_and_same_operation_retries` — injected tombstone failure rolls back and retry succeeds.
- `AC37_failed_outbox_insert_rolls_back_party_workflow_tombstone_and_payload_deletion` — injected Outbox failure rolls back; same operation succeeds after removing the test trigger.
- Existing tests cover duplicate review, explicit ForceNew confirmation, Existing Party selection, actor-bound replay, concurrent idempotency, PII-free tombstone/event envelope, and atomic Party/tombstone/payload/outbox commit.

### Exact-head evidence

- Commit: `44950e2303415e785c4d3dfb545b09d5c1ba27a7`
- Linux PostgreSQL migration/integration job: run `45670296726`, job `139403651514` — passed; 63 tests passed.
- Foundation build/test: job `139403651418` — passed.
- API contract, merge contract, security, E2E disposable, architecture, consumer registry, CI enforcement and spec/checksum gates — passed.
- Windows PostgreSQL migration, Windows installer integration and CodeQL were still pending at this evidence snapshot.

The canonical mainline migration reservation is being handled by registry-only PR #104; PR #103 merged 0010/0011 only. Migration 0012/0013 remains pending full Linux/Windows disposable evidence and independent review. No real database migration, production key provisioning, runtime flag or G1 authorization is performed.
