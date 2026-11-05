# گزارش اجرای هم‌زمان AC-32 تا AC-38 — ۲۰۲۶-۱۱-۰۶

این سند وضعیت واقعی کد و شواهد را از ادعای تکمیل جدا می‌کند. هیچ معیار صرفاً به‌خاطر وجود یک فایل یا نام تست، IMPLEMENTED اعلام نشده است.

## AC-32 — پاک‌سازی فیزیکی حداکثر ۳۰ دقیقه پس از انقضا

- cadence اجرایی مطابق سیاست مصوب ۱۵ دقیقه است؛ Worker در startup بلافاصله sweep می‌کند و سپس با PeriodicTimer هر ۱۵ دقیقه اجرا می‌شود. فاصله ۱۵ دقیقه‌ای، ۱۵ دقیقه حاشیه نسبت به مهلت purge سی‌دقیقه‌ای می‌گذارد.
- زمان ایجاد، تمدید، بستن، خواندن و پاک‌سازی Workflow همگی در PostgreSQL و با clock_timestamp() ارزیابی می‌شوند؛ API دیگر زمان دلخواه caller را برای این انتقال‌های lifecycle نمی‌پذیرد.
- TemporaryWorkflowRetentionSweep تمام batchهای ۲۵۰تایی را تا پایان صف تخلیه می‌کند.
- تست Integration جدید ۲۵۱ رکورد منقضی (بیش از یک batch) ایجاد می‌کند، زمان واقعی sweep را زیر ۳۰ دقیقه می‌سنجد و حذف ciphertext و وضعیت purged همه رکوردها را بررسی می‌کند.
- شرط عملیاتی: Host و PostgreSQL باید در دسترس باشند؛ اگر سرویس متوقف باشد، هیچ Worker درون‌پردازه‌ای نمی‌تواند مهلت فیزیکی را تضمین کند. تست CI زمان sweep مجموعه ۲۵۱ رکوردی را می‌سنجد، نه ظرفیت نامحدود یا قطعی سرویس. تحت این مرز عملیاتی، cadence پانزده‌دقیقه‌ای و اجرای فوری startup، همراه با تخلیهٔ کامل batchها، سیاست ۳۰ دقیقه را پوشش می‌دهد.

## AC-35 — idempotency، retry و هشدار

- لایه Sweep از Worker جدا و با قرارداد ITemporaryWorkflowRetentionStore قابل‌آزمون شد؛ اجرای چندباره روی PostgreSQL idempotent است و اجرای تکراری پس از پاک‌سازی، داده یا وضعیت جدیدی ایجاد نمی‌کند.
- شکست یک اجرا، وضعیت موفقیت کاذب نمی‌دهد؛ Worker در tick بعدی (حداکثر ۵ دقیقه در حالت سالم) Sweep را دوباره اجرا می‌کند.
- تست PostgreSQL جدید ابتدا شکست موقت تزریقی، سپس retry موفق و اجرای سوم بدون اثر (idempotency) را بررسی می‌کند؛ همچنین حذف واقعی ciphertext و lifecycle_status=purged را می‌سنجد.
- FailureCode فقط از نوع exception ساخته می‌شود؛ متن exception، SQL و پارامترهای احتمالی وارد log/alert نمی‌شوند.
- در شکست، StructuredLogTemporaryWorkflowRetentionAlert فوراً رویداد Critical با FailureCode پالایش‌شده منتشر می‌کند؛ متن Exception/PII وارد پیام نمی‌شود و تست آن را از Logger واقعیِ تزریقی دریافت و بررسی می‌کند. تحویل downstream وابسته به sink/monitoring پیکربندی‌شده در Host است؛ این PR ادعای وجود سرویس خارجی یا تأیید SLA آن را ندارد.

## AC-36 — عدم کشف داده موقت در جست‌وجوی عمومی

- بررسی کد نشان داد API فعلی هیچ endpoint اشخاص یا autocomplete عمومی ثبت نکرده است.
- PostgresPartyRepository.SearchAsync فقط از جداول پایدار persons، persons_contact_points و persons_addresses می‌خواند؛ جدول staging موقت در query آن وجود ندارد.
- تست Integration جدید متن داخل ciphertext یک Workflow موقت را در جست‌وجوی اشخاص جست‌وجو می‌کند و نتیجه خالی را انتظار دارد.
- محدودیت باقی‌مانده: چون endpoint عمومی/سراسری autocomplete هنوز وجود ندارد، آزمون انتها‌به‌انتهای همه مصرف‌کنندگان عمومی ممکن نیست. AC-36 باز می‌ماند.

## AC-37 — ارتقای صریح، اتمیک و idempotent به Person

- پیاده‌سازی Runtime انجام نشد؛ انجام آن با قرارداد فعلی ناایمن است.
- جدول فقط ciphertext و encryption_key_version نگه می‌دارد. در کد موجود، payload schema نسخه‌دار، سرویس رمزگشایی/مدیریت کلید، مدل رضایت/انتخاب صریح شخص، و orchestrator مشترک تراکنش برای ثبت Person و تغییر وضعیت Workflow وجود ندارد.
- ساخت سرویس ارتقا بدون این قراردادها می‌تواند به رمزگشایی ناسازگار، ثبت شخص ناخواسته، یا نیمه‌کاره‌ماندن دو طرف تراکنش منجر شود.
- AC-37 باز می‌ماند تا قرارداد payload و key management و دو مسیر انتخاب «شخص موجود» / «ایجاد شخص جدید» تصویب و سپس پیاده‌سازی شود.

## AC-38 — عدم افشای PII در log، audit، metrics و CI artifacts

- مسیر retention فقط FailureCode محدودشده و نوع exception را log می‌کند؛ متن exception منتشر نمی‌شود.
- StructuredLogTemporaryWorkflowRetentionAlert اکنون FailureCode نامعتبر یا شامل نویسه‌های غیرمجاز را به invalid_failure_code تبدیل می‌کند.
- تست جدید یک مقدار canary شبیه PII را به exception و Alert می‌دهد و نبود آن را در log/alert بررسی می‌کند.
- محدودیت باقی‌مانده: این شواهد هنوز پوشش جامع همه audit writers، metrics exporters، dumpها و CI artifacts نیست. AC-38 باز می‌ماند.

## نتیجه

- AC-32 و AC-35 اکنون به تست‌های PostgreSQL مشخص در Acceptance Gate Map متصل شده‌اند؛ AC-36 و AC-38 همچنان UNMAPPED هستند.
- AC-37 به دلیل نبود قرارداد رمزگشایی/انتخاب/تراکنش، عمداً پیاده‌سازی نشده است.
- هیچ وضعیت G1، انتشار یا ادغام تأیید نشده است.
