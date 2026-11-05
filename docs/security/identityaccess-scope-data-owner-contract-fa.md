# قرارداد مالکیت داده برای Scope شعبه و انبار در IdentityAccess

- تاریخ: 2026-11-06
- وضعیت: قرارداد مرزی/طراحی؛ **مالک canonical داده شعبه و انبار هنوز با شواهد کد تأیید نشده است**
- مبنا: ماتریس مصوب مالک و `identityaccess-authorization-runtime-contract-fa.md`
- دامنه: تعیین مرز مسئولیت، قرارداد resolver و شروط پذیرش؛ بدون runtime، migration، seed، FK یا تغییر DB.

## 1. تصمیم‌های قطعی معماری

1. IdentityAccess مالک موجودیت‌های Branch، Warehouse، عضویت شعبه، تخصیص انبار یا تخصیص کاربر نیست.
2. IdentityAccess فقط grantهای نقش/permission/scope را نگهداری و principal معتبر را عرضه می‌کند؛ اعتبار وجود، وضعیت و انتساب شعبه/انبار را ماژول مالک داده تعیین می‌کند.
3. شناسه ارسالی client، claim قدیمی، cache یا مقدار scope داخل request سند عضویت/تخصیص نیست.
4. نبود resolver، عدم دسترسی به منبع authoritative، رکورد غیرفعال، ambiguity، timeout یا خطای DB برابر Deny است؛ هیچ fallback به `all` مجاز نیست.
5. تا تعیین مالک واقعی، نام schema/table، نوع کلید، FK بین schemaها و migration نباید حدس زده یا ایجاد شود.

## 2. تفکیک مالکیت و مسئولیت

| داده/تصمیم | مسئولیت IdentityAccess | مسئولیت ماژول مالک داده |
|---|---|---|
| account و principal جاری | مالک | مصرف‌کننده principal |
| role و permission grant | مالک | اعلام نیازمندی permission |
| scope grant از نوع branch/warehouse | نگهداری grant و scope_id | اعتبارسنجی شناسه و assignment |
| فهرست شعبه‌ها و وضعیت فعال/غیرفعال | هیچ مالکیتی ندارد | منبع canonical |
| عضویت account در branch | هیچ مالکیتی ندارد | منبع canonical و تاریخچه/وضعیت |
| فهرست انبارها و تعلق آنها به branch | هیچ مالکیتی ندارد | منبع canonical |
| تخصیص account به warehouse | هیچ مالکیتی ندارد | منبع canonical و وضعیت مؤثر |
| branch/warehouse منبع یا مقصد سند | ارزیابی grant و نتیجه resolver | تعیین context واقعی سند و اعمال predicate |

**مالک canonical ماژول/جدول برای Branch و Warehouse در شواهد فعلی این قرارداد احراز نشده است.** این سند عمداً نام ماژول یا جدول را جعل نمی‌کند. پیش از runtime باید owner هر چهار رابطه (Branch، عضویت شعبه، Warehouse، تخصیص انبار) با مسیر کد، schema و migration واقعی ثبت شود.

## 3. قراردادهای resolver موردنیاز

قراردادها مفهومی‌اند و هنوز interface اجرایی در سورس اعلام نمی‌شوند:

- `IBranchScopeResolver`: دریافت account/principal و branch_id؛ برگرداندن وضعیت معتبر بودن branch و assignment مؤثر در زمان درخواست.
- `IWarehouseScopeResolver`: دریافت account/principal و warehouse_id؛ برگرداندن وضعیت معتبر بودن warehouse، assignment مؤثر و branch متناظر.
- `IResourceScopeResolver<T>`: برای منبع دامنه، context واقعی رکورد را از مالک آن resolve کند؛ از جمله branch_id، warehouse_id و state.
- resolver باید tenant/organization boundary (اگر در مدل محصول وجود دارد) را نیز از منبع authoritative بررسی کند؛ تا زمانی که وجود و معنای آن در schema اثبات نشده، tenant concept جدیدی اختراع نشود.

خروجی resolver باید صریح و سه‌حالته باشد: AllowWithAuthoritativeContext، Deny، Unavailable. فقط حالت اول وارد ارزیابی بعدی می‌شود. Unavailable هرگز به Deny قابل‌چشم‌پوشی یا داده cache شده تبدیل نمی‌شود؛ پاسخ API مطابق قرارداد خطای موقت fail-closed خواهد بود.

## 4. قواعد دقیق ارزیابی scope

1. `self`: فقط مالکیت رکورد که ماژول مالک با account جاری اثبات می‌کند.
2. `assigned`: فقط رابطه تخصیص فعال و قابل‌ردیابی از منبع مالک؛ در صورت نیاز، تاریخ شروع/پایان و لغو assignment لحاظ شود.
3. `branch:<id>`: شناسه branch باید در ماژول مالک موجود و فعال باشد و account جاری assignment مؤثر به همان branch داشته باشد.
4. `warehouse:<id>`: warehouse باید موجود و فعال باشد؛ تعلق warehouse به branch و assignment account به warehouse هر دو از منبع مالک اثبات شوند. دسترسی branch به‌تنهایی به همه warehouseهای branch تعمیم داده نمی‌شود مگر policy محصول صریحاً چنین کند.
5. `all`: فقط grant صریح همان permission است؛ محدودیت‌های سازمانی/دامنه‌ای، وضعیت سند، separation-of-duties و audit همچنان برقرارند.
6. چند grant برای همان permission فقط union مجموعه شناسه‌های معتبر branch/warehouse را می‌سازد؛ scopeهای permission دیگر قابل انتقال نیستند.
7. در عملیات انتقال انبار، هم مبدأ و هم مقصد باید جداگانه مجاز باشند؛ دسترسی به یکی، مجوز دیگری نیست.
8. برای رکورد چندشعبه‌ای/چندانباری، رفتار باید برای هر نوع منبع جداگانه تعریف شود؛ حدس زدن از branch اصلی یا اولین warehouse ممنوع است.

## 5. قرارداد Query و Mutation

- فیلتر scope باید در SQL/EF query قبل از materialization اعمال شود؛ بارگیری گسترده و فیلتر در حافظه ممنوع.
- list، search، detail، dashboard، report، print، export و API مستقیم باید از predicate مشترک همان منبع استفاده کنند.
- مسیرهای write باید resource context و assignment را در همان transaction یا با concurrency token/guard معتبر تا نقطه commit دوباره بررسی کنند.
- لغو یا تغییر assignment هم‌زمان با write نباید اجازه عبور از نقطه linearization بدهد.
- شناسه branch/warehouse از body فقط یک selector است؛ هرگز proof مجوز نیست.
- پاسخ منبع ناموجود و خارج از scope باید طبق قرارداد endpoint غیرقابل‌تمایز باشد؛ خطای resolver/DB نباید اطلاعات وجود منبع را افشا کند.
- bulk operation باید برای تک‌تک منابع scope را اعمال کند و در صورت سیاست all-or-nothing، کل batch را rollback کند.

## 6. حداقل مدل داده منطقی (غیر اجرایی)

Scope grant در IdentityAccess می‌تواند `scope_kind` و `scope_id` را نگهداری کند؛ برای branch/warehouse، `scope_id` صرفاً opaque identifier است تا وقتی که مالک داده و قرارداد کلید مشخص نشده است. FK مستقیم از schema هویت به جدول‌های ماژول‌های دیگر **پیشنهاد نمی‌شود** تا coupling و چرخه وابستگی ایجاد نشود.

مالک داده باید تضمین کند:
- شناسه پایدار و غیرقابل‌بازیافت یا semantics روشن برای شناسه‌های حذف/آرشیو؛
- وضعیت فعال/غیرفعال و زمان اثر assignment؛
- یکتایی و عدم ابهام assignmentهای هم‌پوشان؛
- audit تغییر assignment؛
- index مناسب روی account_id، branch_id/warehouse_id و وضعیت/بازه اثر؛
- قیود referential integrity در schema تحت مالکیت خودش.

هیچ جدول یا ستون جدیدی با این سند ایجاد نشده است.

## 7. ماتریس شواهد لازم برای تعیین Data Owner

| موضوع | شاهد الزامی | وضعیت فعلی |
|---|---|---|
| Branch canonical entity | entity/aggregate، schema و migration | احراز نشده |
| Account-Branch membership | entity/table، lifecycle و service/query | احراز نشده |
| Warehouse canonical entity | entity/aggregate، schema و migration | احراز نشده |
| Account-Warehouse assignment | entity/table، lifecycle و service/query | احراز نشده |
| Warehouse-to-Branch relation | FK/constraint و invariant دامنه | احراز نشده |
| Consumers | تمام query/list/report/export/write paths | نیازمند inventory سورس |
| Delete/archive semantics | lifecycle، reassignment و historical records | احراز نشده |
| Multi-branch / multi-warehouse | تصمیم مالک محصول و قواعد scope | اصل چندانتساب باید از مدل واقعی تأیید شود |

تا ارائه این شواهد، قرارداد مرزی حاضر قابل استفاده برای جلوگیری از coupling است، اما Data Owner sign-off کامل محسوب نمی‌شود.

## 8. آزمون‌های پذیرش الزامی پس از مجوز G1

1. branch معتبر و assignment فعال => فقط همان branch مجاز.
2. branch غیرفعال/ناموجود یا assignment لغوشده => Deny.
3. warehouse معتبر با assignment فعال => فقط همان warehouse مجاز.
4. assignment branch بدون warehouse assignment => دسترسی warehouse به‌صورت پیش‌فرض Deny.
5. warehouse متعلق به branch دیگر => Deny حتی اگر branch انتخابی مجاز باشد.
6. جعل scope_id در request => بدون اثر بر تصمیم.
7. دو نقش با branchهای A و B => union دقیق A∪B برای همان permission.
8. grant `all` برای permission P1 => هیچ توسعه‌ای برای P2 ایجاد نکند.
9. transfer از warehouse مجاز به مقصد غیرمجاز => Deny و بدون side effect.
10. لغو assignment هم‌زمان با mutation => نتیجه مطابق serialization/linearization و بدون write غیرمجاز.
11. resolver timeout/DB outage/exception => fail-closed؛ بدون cache fallback.
12. list/detail/search/report/print/export => predicate scope یکسان و عدم نشت PII.
13. bulk IDs مخلوط مجاز/غیرمجاز => رفتار atomic مطابق قرارداد و بدون partial unauthorized write.
14. branch/warehouse archive یا reassignment => دسترسی تاریخی و عملیاتی طبق lifecycle مصوب، نه حدس runtime.
15. تست property-based روی مجموعه نقش‌ها، grants، assignments و شناسه‌های جعلی.

این‌ها سناریوهای پذیرش طراحی‌اند؛ هنوز تست .NET/PostgreSQL نوشته یا اجرا نشده‌اند.

## 9. شروط بستن این موضوع

- [ ] معرفی و ثبت مالک canonical Branch و مسیر کد/schema/migration.
- [ ] معرفی و ثبت مالک membership حساب-شعبه.
- [ ] معرفی و ثبت مالک canonical Warehouse و رابطه warehouse-branch.
- [ ] معرفی و ثبت مالک assignment حساب-انبار.
- [ ] بررسی همه consumerها و query pathها برای IDOR و export leakage.
- [ ] تأیید semantics چندشعبه‌ای، چندانباری، archive و reassignment توسط مالک محصول.
- [ ] review وابستگی و قرارداد resolver توسط معماری مستقل.
- [ ] پس از G1 و مجوز اجرایی: interface/runtime، migration در صورت نیاز، و تست‌های واقعی.

## 10. وضعیت گیت

این قرارداد، مرز معماری را مشخص می‌کند ولی به‌دلیل نبود شواهد canonical source و sign-off ماژول مالک، موضوع Data Owner را **کامل/بسته اعلام نمی‌کند**. تا تکمیل شروط بخش 9، branch/warehouse authorization باید fail-closed بماند. این تغییر هیچ runtime، endpoint، middleware، migration، seed، FK، permission grant یا DB mutation ایجاد نمی‌کند. `g1Authorized=false` بدون تغییر باقی می‌ماند.
