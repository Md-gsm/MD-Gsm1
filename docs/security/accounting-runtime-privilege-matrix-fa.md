# ماتریس حداقل دسترسی پایگاه داده Accounting

شناسه: MDG-ACC-SEC-PRIV-001  
نسخه: 0.1  
وضعیت: طراحی اجرایی پیشنهادی برای بازبینی؛ هنوز هیچ grant واقعی روی schema دائمی Accounting اعمال نشده است.  
والد: MDG-ARCH-MASTER-001 v3.0.4 / MDG-ADR-0018  
دامنه: Accounting core persistence

## 1. اصول

1. اتصال برنامه هرگز مالک schema یا جدول نیست.
2. نقش runtime فقط مجوزهای صریح و موردنیاز هر جدول/عملیات را می‌گیرد؛ هیچ مجوزی از IdentityAccess یا ماژول دیگر به Accounting ارث نمی‌رسد.
3. هیچ مجوزی به PUBLIC داده نمی‌شود؛ default privilege عمومی تعریف نمی‌شود.
4. Runtime مجاز به DDL، CREATE در schema، TRUNCATE، تغییر مالکیت، ساخت role، ساخت database یا دور زدن triggerها نیست.
5. Trigger و constraint دفاع لایه دوم‌اند؛ privilege حداقلی و authorization برنامه همچنان الزامی است.
6. حذف اسناد مالی، خطوط، idempotency، audit، transition history و snapshot پذیرفته‌شده از مسیر runtime ممنوع است.
7. این ماتریس تا زمان پذیرش نهایی، مجوز اجرای GRANT/REVOKE یا ایجاد نقش در دیتابیس عملیاتی نیست.

## 2. نقش‌ها

| نقش | کاربرد | مالکیت و محدودیت |
|---|---|---|
| `mdgsm_accounting_migrator` | اجرای کنترل‌شده DDL در deployment پس از مجوز Migration | مالک schema و اشیای Accounting؛ credential فقط در pipeline امن deployment؛ هرگز در connection string برنامه |
| `mdgsm_accounting_runtime` | اتصال سرویس برنامه برای عملیات Accounting | NOLOGIN group role؛ بدون مالکیت، DDL یا عضویت در migrator |
| `mdgsm_accounting_period_operator` | نقش آینده برای Close/Reopen دوره | هنوز ایجاد/اعطا نمی‌شود؛ تا تعیین authorization matrix هیچ write privilege جداگانه ندارد |
| `mdgsm_accounting_coa_importer` | نقش آینده برای ورود COA پذیرفته‌شده | تا تکمیل AP-08 و crosswalk معتبر ایجاد/اعطا نمی‌شود |

نام‌ها شناسه‌های پیشنهادی‌اند و تا تصویب و طراحی deployment نباید در محیط واقعی ساخته شوند.

## 3. ماتریس جدول به جدول

| جدول | Runtime مجاز | Runtime ممنوع | دلیل |
|---|---|---|---|
| `coa_snapshots` | SELECT | INSERT/UPDATE/DELETE/TRUNCATE | ورود و پذیرش snapshot فرایند مستقل و evidence-backed است |
| `accounts` | SELECT | INSERT/UPDATE/DELETE/TRUNCATE | حساب canonical بدون crosswalk پذیرفته‌شده seed یا اصلاح نمی‌شود |
| `account_field_evidence` | SELECT | INSERT/UPDATE/DELETE/TRUNCATE | شواهد باید در مسیر import/review کنترل‌شده ثبت شوند |
| `account_event_eligibility` | SELECT | INSERT/UPDATE/DELETE/TRUNCATE | نبودن تصمیم یا evidence به معنی عدم اجازه است |
| `fiscal_periods` | SELECT | INSERT/UPDATE/DELETE/TRUNCATE | تغییر وضعیت دوره فقط با اختیار مستقل و تراکنش کنترل‌شده مجاز می‌شود؛ role هنوز تعریف نشده |
| `period_transition_events` | SELECT/INSERT | UPDATE/DELETE/TRUNCATE | تاریخچه Close/Reopen append-only است |
| `journals` | SELECT/INSERT؛ UPDATE فقط روی ستون‌های transition نهایی‌سازی: `status`, `finalized_by`, `finalized_at_utc`, `finalized_audit_event_id`, `version` | UPDATE سایر ستون‌ها؛ DELETE/TRUNCATE | تغییرات header پس از Draft باید محدود و تغییرناپذیری Finalized در trigger نیز enforce شود |
| `journal_lines` | SELECT/INSERT | UPDATE/DELETE/TRUNCATE | مدل Domain سند immutable است؛ اصلاح با سند جدید یا reversal انجام می‌شود |
| `posting_idempotency` | SELECT/INSERT | UPDATE/DELETE/TRUNCATE | هویت و fingerprint درخواست پس از ثبت نباید overwrite شود |
| `audit_events` | SELECT/INSERT | UPDATE/DELETE/TRUNCATE | audit append-only است |

هیچ grant مربوط به `fiscal_periods`، COA import یا تغییر شواهد canonical تا تعیین نقش مستقل و تکمیل تصمیمات مربوطه صادر نمی‌شود.

## 4. کنترل‌های schema و نقش

- `USAGE` روی schema فقط برای runtime؛ `CREATE` ممنوع.
- هیچ table/schema ownership به runtime داده نمی‌شود.
- هیچ privilege برای TRUNCATE، REFERENCES، TRIGGER، CREATE، TEMPORARY یا اجرای DDL داده نمی‌شود، مگر ضرورت مستقل و review‌شده.
- `DELETE` روی تمام جدول‌های مالی برای runtime ممنوع است.
- `UPDATE` روی `journals` فقط به ستون‌های نهایی‌سازی محدود می‌شود؛ کنترل transition و version در همان transaction انجام می‌شود.
- `journal_lines`، `posting_idempotency`، `audit_events` و `period_transition_events` برای runtime update/delete ندارند.
- Triggerهای immutable برای Journal نهایی، خطوط وابسته، audit، transition history و COA snapshot پذیرفته‌شده فعال می‌مانند.
- privilegeها به‌صورت explicit اعطا می‌شوند؛ از `GRANT ALL`، role inheritance ناخواسته و default privileges گسترده استفاده نمی‌شود.

## 5. آزمون پذیرش

PoC مستقل PostgreSQL 18 در مسیر `tests/MDGsm.Domain.Tests/Accounting/PostgreSQL/ap03-ap04-proof.sql` و runner مربوط، حداقل این موارد را بررسی می‌کند:

- نبود CREATE روی schema و نبود INSERT برای snapshot پذیرفته‌شده؛
- INSERT مجاز برای Journal/JournalLine و UPDATE ستونی محدود برای status؛
- نبود UPDATE/DELETE برای JournalLine، audit و transition history؛
- رد تغییر status یک Journal نهایی توسط trigger، حتی با وجود مجوز ستونی UPDATE؛
- رد تغییر accepted snapshot و append-only records؛
- رد TRUNCATE روی داده‌های محافظت‌شده.

این PoC روی schema موقت `ap_proof` و دیتابیس disposable اجرا می‌شود؛ grantهای آن عین grantهای عملیاتی نیستند و هیچ نقش دائمی Accounting ایجاد نمی‌کنند. نتیجه CI فقط پس از اجرای موفق روی SHA نهایی معتبر است.

## 6. موارد خارج از مجوز

این ماتریس به‌تنهایی مجوز ایجاد schema، migration، seed، import COA، API، Posting writer، دوره مالی عملیاتی یا اتصال ماژول‌های دیگر نیست. Migration 0009 همچنان RESERVED و SQL NOT AUTHORIZED باقی می‌ماند.


## Current Architecture Master authority — 2026-11-24

The Product Owner has designated **MDG-ARCH-MASTER-001 v3.0.4** as the sole current, accepted, official and governing Architecture/Accounting Master. Any v3.0.3 references retained above identify the earlier source snapshot or the baseline used when this artifact was authored; they do not define current authority. Current decisions must conform to v3.0.4. This notice does not alter historical execution evidence or imply implementation, test PASS, independent acceptance, migration authorization or release.
