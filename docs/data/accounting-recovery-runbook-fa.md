# رویه پشتیبان‌گیری و بازیابی هسته Accounting

شناسه: MDG-ACC-OPS-REC-001  
نسخه: 0.1  
وضعیت: رویه عملیاتی پیشنهادی؛ اجرای عملیاتی Accounting مجاز نشده است.  
والد: MDG-ARCH-MASTER-001 v3.0.3 / MDG-ADR-0018 / MDG-ACC-PDS-001

## 1. اصل غیرقابل‌مذاکره

سیاست بازیابی به وجود یا عدم وجود داده مالی finalized وابسته است:

- **پیش از وجود داده مالی واقعی:** حذف و بازسازی فقط برای دیتابیس disposable که نام و marker آن به‌طور مستقل تأیید شده باشد، قابل بررسی است. این کار rollback migration محسوب نمی‌شود؛ runner فعلی Down/Rollback ندارد.
- **پس از ایجاد هرگونه داده مالی واقعی:** حذف schema/database، Down migration، بازگرداندن backup روی دیتابیس زنده یا بازنویسی migration ممنوع است. فقط forward-fix نسخه‌دار و بازیابی کنترل‌شده از backup/PITR به محیط جداگانه مجاز است.

این سند مجوز اجرای هیچ دستور destructive یا تغییر Migration 0009 نیست.

## 2. محیط disposable و guard

یک دیتابیس فقط زمانی disposable محسوب می‌شود که تمام شروط زیر هم‌زمان برقرار باشند:

1. نام دیتابیس از prefix اختصاصی تست و شناسه run/attempt یکتا پیروی کند؛ نام به‌تنهایی کافی نیست.
2. marker در comment دیتابیس دقیقاً مقدار مورد انتظار harness تست را داشته باشد، مانند `MDGSM_DISPOSABLE_TEST_DB`.
3. connection target، host/port و نام دیتابیس در همان job از متغیرهای run ساخته و دوباره بررسی شوند.
4. عملیات حذف فقط در همان harness محدودشده انجام شود؛ هیچ fallback به نام دیتابیس توسعه، staging یا production وجود نداشته باشد.
5. نبود marker، marker نامعتبر، عدم تطابق نام یا عدم قطعیت درباره مقصد باید fail-closed متوقف کند.

هیچ اسکریپت عمومی پاکسازی برای دیتابیس‌های فاقد marker معتبر تعریف نمی‌شود.

## 3. پیش از هر تغییر schema

1. نسخه PostgreSQL و مقصد دیتابیس را ثبت کن.
2. backup کامل با فرمت قابل‌بازیابی تهیه کن؛ زمان، مقصد، نسخه ابزار و checksum artifact ثبت شود.
3. backup را در یک دیتابیس جدا و disposable restore کن؛ restore روی منبع backup یا دیتابیس اصلی انجام نشود.
4. صحت ساختار، تاریخچه migration، شمارش جدول‌ها و smoke testهای پایه را بررسی کن.
5. فقط پس از restore موفق و تأیید مقصد، migration مجاز را روی دیتابیس disposable اجرا کن.
6. migration runner را بار دوم اجرا و صفر بودن migration pending و نبود checksum/name drift را اثبات کن.
7. نتیجه، SHA کد، نسخه migration، backup/restore evidence و خروجی تست‌ها را به همان commit متصل کن.

## 4. شکست پیش از داده مالی واقعی

- ابتدا خطا و transaction outcome را ثبت کن؛ فرض نکن همه عملیات rollback شده‌اند، آن را با query و migration history بررسی کن.
- اگر دیتابیس صریحاً disposable و marker آن معتبر است، دیتابیس آزمایشی ناموفق را کنار بگذار و از ابتدا روی دیتابیس disposable جدید تکرار کن.
- runner فعلی Down ندارد؛ حذف یک دیتابیس آزمایشی به معنی اضافه‌شدن قابلیت Down به runner نیست.
- فایل migration اعمال‌شده یا checksum آن را درجا دستکاری نکن؛ اصلاحات schema از مسیر migration forward و بازبینی‌شده انجام می‌شوند.
- اگر حتی یک داده مالی واقعی وجود دارد یا وضعیت داده نامعلوم است، این مسیر ممنوع است و رویه بخش 5 اعمال می‌شود.

## 5. شکست پس از ایجاد داده مالی

1. عملیات نوشتن/Posting را طبق رویه incident و با حفظ شواهد متوقف یا محدود کن؛ داده را دستی حذف یا اصلاح نکن.
2. backup/PITR نقطه مناسب را به یک دیتابیس جدا restore کن؛ دیتابیس زنده را overwrite نکن.
3. شمارش و پیوستگی Journal، JournalLine، PostingIdentity/Idempotency، Audit و FiscalPeriod را با شواهد قبل از incident تطبیق بده.
4. علت را در کد یا schema تشخیص بده؛ اصلاح فقط با forward migration یا forward application fix نسخه‌دار و review‌شده انجام شود.
5. اگر بازیابی از backup لازم است، مقصد restore ابتدا جداگانه اعتبارسنجی شود؛ cutover فقط با مجوز صریح عملیات/مالک و برنامه بازگشت کنترل‌شده انجام می‌شود.
6. audit trail و اسناد finalized را با SQL دستی تغییر نده؛ اصلاح مالی فقط با سند اصلاحی یا reversal مجاز در سیاست Accounting انجام می‌شود.
7. پس از cutover، reconciliation کامل و آزمون تکرارپذیری migration/backup/restore ثبت شود.

## 6. معیار موفقیت Backup/Restore

- backup قابل خواندن و restore کامل بدون خطای ابزار؛
- اتصال restore به مقصد جداگانه و اثبات عدم overwrite منبع؛
- تطبیق migration history، version و checksum؛
- تطبیق شمارش و integrity داده‌های Accounting، از جمله Journal/Lines/Idempotency/Audit؛
- اجرای smoke tests و read-only reconciliation؛
- ثبت دقیق زمان، SHA، PostgreSQL version، run id و نتیجه؛
- تأیید مستقل شواهد بازیابی پیش از هر cutover.

## 7. محدودیت‌های فعلی MD-Gsm

- Migration 0009 در main رزرو است؛ SQL آن هنوز مجاز نشده است.
- Accounting Infrastructure، persistence واقعی و Journal posting runtime هنوز وجود ندارد.
- runner فعلی فقط migrationهای forward را اعمال می‌کند و Down/Rollback ندارد.
- تست backup/restore موجود در CI متعلق به دیتابیس disposable فعلی و migrationهای موجود است؛ تا زمان وجود Accounting persistence، آن تست به‌تنهایی صحت بازیابی داده مالی Accounting را اثبات نمی‌کند.
- این runbook مستند سیاست است؛ هیچ ادعایی درباره اجرای backup/restore عملیاتی Accounting ندارد.

## 8. کنترل تغییر

هر تغییر در این رویه باید با بررسی هم‌زمان امنیت، صحت مالی، مالکیت داده، runner فعلی و قابلیت آزمون انجام شود. تغییر در معماری مرجع v3.0.3 یا اعطای مجوز SQL/Migration 0009 از این سند استنتاج نمی‌شود.
