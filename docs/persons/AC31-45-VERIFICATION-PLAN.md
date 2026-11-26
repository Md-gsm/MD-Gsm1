# برنامه و نگاشت آزمون پذیرش Persons — AC-31 تا AC-49

**نسخه:** Stage 6 / Acceptance Map v1.4  
**مخزن:** `Md-gsm/MD-Gsm1`  
**شاخه کاری:** `feature/persons-g1-completion-20261124`  
**محدوده:** نگاشت معیارها به آزمون‌های قابل اجرا؛ این سند به‌تنهایی مجوز G1، استقرار یا اجرای migration واقعی نیست.

نقشهٔ ماشینی مرجع `docs/persons/acceptance-gate-map.json` است. CI وجود دقیق نام فایل و متد اصلی و تمام آزمون‌های پشتیبان را بررسی می‌کند. معیارهای AC-46 تا AC-49 کنترل‌های CI و شواهد هستند و نباید به‌عنوان قابلیت کسب‌وکاری Runtime تفسیر شوند.

## ماتریس نگاشت آزمون‌ها

| معیار | آزمون اصلی | آزمون‌های پشتیبان / شواهد تکمیلی |
|---|---|---|
| AC-31 | `PersonsTemporaryDataRetentionTests.cs::AC31_expiry_blocks_access_at_the_exact_utc_expiry_instant` | `AC31_expiry_comparison_normalizes_non_utc_offsets`؛ PostgreSQL: مسدودشدن خواندن در لحظهٔ انقضا |
| AC-32 | `PersonsTemporaryDataRetentionTests.cs::AC32_worker_is_registered_drains_all_due_batches_and_uses_the_fifteen_minute_default` | `PersonsTemporaryWorkflowPostgresTests.cs::AC32_ciphertext_is_stored_and_expiry_blocks_reads_before_cleanup` |
| AC-33 | `PersonsTemporaryWorkflowPostgresTests.cs::AC33_AC34_renewal_is_single_use_authorized_and_capped_at_thirty_days` | `PersonsTemporaryDataRetentionTests.cs::AC33_renewal_never_extends_beyond_absolute_thirty_day_age` |
| AC-34 | `PersonsTemporaryWorkflowPostgresTests.cs::AC33_AC34_renewal_is_single_use_authorized_and_capped_at_thirty_days` | `AC34_only_one_authorized_renewal_with_a_reason_is_accepted`؛ `AC34_renewal_rejects_missing_reason_or_missing_authorization` |
| AC-35 | `PersonsTemporaryDataRetentionTests.cs::AC35_worker_retries_on_next_cycle_and_logs_only_redacted_failure_codes` | `AC38_structured_retention_alert_accepts_only_closed_failure_code_and_emits_no_payload` |
| AC-36 | `PersonsTemporaryWorkflowPostgresTests.cs::AC36_temporary_workflow_payload_does_not_appear_in_Persons_search_or_search_route` | جست‌وجوی Repository، route registration و ciphertext/no-plaintext assertion |
| AC-37 | `PersonsTemporaryWorkflowPromotionPostgresTests.cs::AC37_new_party_promotion_commits_party_tombstone_payload_purge_and_outbox_atomically` | authorization matrix، Existing/New selection، ForceNew confirmation، corrupt ciphertext fail-closed، idempotency/concurrency و failure injection در close/purge/tombstone/Outbox |
| AC-38 | `PersonsTemporaryWorkflowPromotionPostgresTests.cs::AC38_temporary_payload_is_excluded_from_audit_metadata_and_metric_telemetry` | redacted retention logs/alerts، absence of raw payload from metric tags، و `test_persons_gate_policy.py::test_ci_evidence_manifest_is_traceable_and_redacted` با sentinel در TRX |
| AC-39 | `PersonsMergeContractTests.cs::AC39_AC40_AllExternalPartyIdConsumersAreRegisteredAndClassifiedExactlyOnce` | source scan در `src` و migrationها در برابر Consumer Registry |
| AC-40 | `PersonsMergeContractTests.cs::AC39_AC40_AllExternalPartyIdConsumersAreRegisteredAndClassifiedExactlyOnce` | ردکردن consumer ناشناخته، classification مفقود و registration تکراری در تست‌های policy |
| AC-41 | `PersonsMergeContractTests.cs::AC41_AllRegisteredParticipantsImplementVersionedProtocolHooks` | بررسی هر دو participant واقعی Finance و Persons برای Preview/Prepare/Validate/Apply/Verify/Recover |
| AC-42 | `PersonsMergeContractTests.cs::AC42_preview_is_read_only_and_counts_all_finance_and_persons_references` | شمارش active/historical/transferable/blocking و تأیید عدم تغییر Party/Account/Contact/Address در preview |
| AC-43 | `PersonsMergeContractTests.cs::AC43_two_existing_finance_accounts_are_a_blocking_conflict_and_no_state_changes` | اثبات توقف قبل از Apply و حفظ وضعیت Party، Finance account، ContactPoint و Address |
| AC-44 | `PersonsMergeContractTests.cs::AC44_MergeIsAtomicConcurrentAndIdempotentWithFailureRollback` | موفقیت/replay، failure injection و rollback/recovery، رقابت دو مقصد و انتخاب دقیق یک برنده |
| AC-45 | `PersonsMergeContractTests.cs::AC45_merge_preserves_existing_lifecycle_snapshots_and_reconciles_active_references` | مقایسهٔ snapshotهای JSON history با همان HistoryId قبل/بعد از merge؛ تعداد، origin، consent، primary، tombstone و merge-history؛ حفظ AccountId/CreatedAt |
| AC-46 | `test_persons_gate_policy.py::test_required_jobs_are_unconditionally_scheduled` | آزمون منفی برای job غیرفعال و path-filter bypass |
| AC-47 | `test_persons_gate_policy.py::test_failed_gate_cannot_be_relabelled_pass_na` | شکست بسته در برابر required job ناموفق |
| AC-48 | `test_persons_gate_policy.py::test_g1_authorization_without_external_evidence_fails` | منع G1 بدون شواهد SCM/review/enforcement خارجی |
| AC-49 | `test_persons_gate_policy.py::test_ci_evidence_manifest_is_traceable_and_redacted` | commit، schema fingerprint، gate/test counts و ردکردن sentinel حساس از manifest |

## مرز شواهد و تفسیر

- AC-31 تا AC-38 آزمون‌های Domain، PostgreSQL و CI را به هم وصل می‌کنند. چرخه پاک‌سازی با sweep فوری هنگام شروع و تناوب پیش‌فرض ۱۵ دقیقه، تخلیهٔ تمام batchهای سررسیدشده، حذف ciphertext در دیتابیس disposable و retry پس از شکست پوشش داده می‌شود.
- آزمون CI برای AC-35/38 نشان می‌دهد پیام‌های داخلی worker/alert فقط کد خطای بسته و فاقد payload را ثبت می‌کنند. این آزمون جایگزین اتصال مقصد هشدار در محیط استقرار و اثبات SLA واقعی تحویل ۵ دقیقه‌ای نمی‌شود؛ همین‌طور اجرای disposable جایگزین آزمون بار/ظرفیت روی محیط مشتری نیست.
- AC-39 تا AC-45 هر دو participant واقعی Finance و Persons را در مسیر Merge بررسی می‌کنند. Migration شمارهٔ 0006 صریحاً `finance_party_accounts` را فقط شناسهٔ حساب تفصیلی می‌داند، نه مانده یا دفترکل؛ در این schema هنوز ledger rows وجود ندارد. بنابراین AC-45 snapshotهای تاریخی موجود Persons و هویت حساب Finance را تطبیق می‌دهد؛ پس از ایجاد persistence دفترکل، هر consumer/participant جدید باید به رجیستری و آزمون snapshot/reconciliation افزوده شود.
- AC-46 تا AC-49 گیت‌های حاکمیتی و تولید artifact هستند، نه پذیرش Runtime ماژول.
- همهٔ آزمون‌های PostgreSQL فقط روی دیتابیس علامت‌گذاری‌شدهٔ disposable در CI اجرا می‌شوند. هیچ migration روی دیتابیس واقعی اجرا نشده است.
- `g1Authorized=false` باقی می‌ماند. سبزشدن این آزمون‌ها، بررسی مستقل فعلی PR، هم‌ترازی معماری، provision کلید مشتری، فعال‌سازی API/feature flag، انتشار یا Go-Live را مجاز نمی‌کند.

## شواهد CI نامزد Stage 6

- SHA کد/آزمون: `ad5a1144b16268c19cd170a2e2759cddc448e972`
- Persons Module Gates: run `45800993885` — در snapshot ثبت این سند، Windows PostgreSQL هنوز در حال اجرا بود؛ سایر jobهای فهرست‌شده در workflow موفق شده بودند.
- Foundation build/test job `139843963380`: build بدون warning/error و **275/275** آزمون موفق.
- Linux PostgreSQL job `139843963151`: **82/82** آزمون یکپارچه‌سازی موفق؛ 12 migration در اجرای اول اعمال شد و اجرای دوم `newlyApplied=0` را نشان داد. نسخه‌های اعمال‌شده شامل 0012 تا 0015 در محیط disposable بودند.
- Jobهای spec/checksum، security، consumer registry، architecture، API contract، retention، merge contract، E2E disposable و CI enforcement روی همین SHA موفق بودند.
- Project Charter Owner Gate run `45800993685`: موفق.
- Windows PostgreSQL، CodeQL و Windows Installer در زمان ثبت snapshot در حال اجرا بودند. نتیجهٔ آن‌ها باید از run نهایی روی SHA مستندشده read-back شود؛ نتیجهٔ runهای قدیمی به این SHA منتقل نمی‌شود.

**قانون تکمیل:** نگاشت ماشینی به معنی پیاده‌سازی ردیابی‌پذیر است؛ پذیرش نهایی مرحله به CI کامل روی آخرین SHA، بازبینی مستقل همان SHA و حفظ مرزهای صریح G1 وابسته است.
