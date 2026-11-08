# رجیستری مرکزی شماره‌گذاری و رزرو Migrationها

| مشخصه | مقدار |
|---|---|
| شناسه | MIG-REG-001 |
| مالک فرایند رزرو | WG-0 |
| مالک SQL و schema | گروه مالک دامنه |
| مسیر Migrationها | `database/migrations/` |
| وضعیت | مرجع جاری رزرو و جلوگیری از برخورد |

## قواعد قطعی

1. شماره Migration یک شناسه سراسری عددی چهاررقمی است؛ یکتایی آن در تمام زیرپوشه‌های `database/migrations/**` الزامی است.
2. نام فایل باید `NNNN_descriptive_name.sql` باشد. نام فایل (بدون مسیر) مبنای Embedded Resource / LogicalName است؛ بنابراین هم شماره و هم نام فایل باید در کل درخت یکتا باشند.
3. پیش از نوشتن SQL، گروه متقاضی باید یک ردیف `RESERVED` با شماره، گروه، مالک schema، PR/Issue، محدوده و تاریخ درخواست ثبت کند. WG-0 رزروها را به‌صورت سریالی در همین فایل ثبت می‌کند.
4. تا زمانی که رزرو در شاخه/PR قابل مشاهده نشده، هیچ گروهی مجاز به استفاده از آن شماره نیست. اگر دو PR هم‌زمان یک شماره را پیشنهاد کنند، فقط رزروی معتبر است که نخست در مسیر رسمی ثبت و ادغام شود؛ PR دیگر باید شماره را اصلاح کند.
5. پس از ادغام و اعمال صرفاً در محیط disposable/مجاز، ردیف به `APPLIED` تغییر می‌کند و SHA-256 SQL و PR/commit ثبت می‌شود. این رجیستری مجوز اجرای migration روی دیتابیس واقعی نیست.
6. رزرو استفاده‌نشده فقط با ثبت دلیل، تأیید WG-0 و اثبات عدم ادغام/عدم اعمال آزاد می‌شود. شماره Migration اعمال‌شده هرگز بازاستفاده، جابه‌جا یا rename نمی‌شود.
7. هر تغییر در Migration باید هم‌زمان این رجیستری، تست یکتایی `MigrationResourceTests` و `SHA256SUMS.txt` را به‌روز کند.
8. تست CI باید نام فایل، شماره عددی، نام Embedded Resource و فهرست فایل‌های ثبت‌شده در این رجیستری را با کل زیرپوشه‌ها تطبیق دهد. قبولی تست به معنی مجوز schema change یا production apply نیست.

## موجودی فعلی

| شماره | مسیر دقیق | مالک دامنه | وضعیت | PR/شاهد |
|---:|---|---|---|---|
| 0001 | `database/migrations/0001_infrastructure_schema_version.sql` | Platform / Infrastructure | APPLIED | main؛ migration baseline |
| 0002 | `database/migrations/persons/0002_persons_core.sql` | Persons | APPLIED | main؛ مسیر تاریخی زیرپوشه‌ای، بدون rename |
| 0003 | `database/migrations/0003_identityaccess_core.sql` | IdentityAccess | APPLIED | main |
| 0004 | `database/migrations/0004_persons_reserve_national_identifiers.sql` | Persons | APPLIED | main |
| 0005 | `database/migrations/0005_persons_nonblank_identity_names.sql` | Persons | APPLIED | main |
| 0006 | `database/migrations/0006_finance_party_accounts.sql` | Finance | APPLIED | main |
| 0007 | `database/migrations/0007_persons_temporary_workflows.sql` | Persons | APPLIED | main |
| 0008 | `database/migrations/0008_persons_party_merge.sql` | Persons | APPLIED | main |
| 0009 | — | تخصیص‌نیافته | AVAILABLE / رزرو نشده | اولین شماره قابل رزرو؛ استفاده فقط پس از ثبت رزرو |
| 0010+ | — | تخصیص‌نیافته | UNALLOCATED | تا ثبت درخواست رزرو استفاده نشود |

## فرم رزرو الزامی

هر ردیف رزرو جدید باید این اطلاعات را داشته باشد:

- شماره چهاررقمی یکتا؛
- شناسه/نام WG درخواست‌کننده و مالک دامنه/schema؛
- هدف و محدوده جداول/اشیای schema؛
- PR/Issue مربوط و SHA شاخه؛
- تاریخ رزرو و وضعیت `RESERVED`؛
- پس از ادغام: SHA-256 فایل SQL، commit ادغام و وضعیت `APPLIED`؛
- در صورت لغو: علت، تأیید WG-0 و شواهد عدم اعمال.

**هشدار:** در حال حاضر 0009 فقط AVAILABLE است، نه رزروشده. هیچ گروهی نباید شماره را صرفاً از روی این جدول در شاخه خودش مصرف کند؛ ابتدا باید ردیف رزرو رسمی ثبت شود.
