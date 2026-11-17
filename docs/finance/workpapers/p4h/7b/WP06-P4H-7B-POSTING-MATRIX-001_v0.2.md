# WP06-P4H-7B-POSTING-MATRIX-001
## Master Posting Matrix — FE-01..FE-38 — Controlled Replacement

| مشخصه | مقدار |
|---|---|
| Version | 0.2 |
| Classification | CONTROLLED RECONSTRUCTION / CONTROLLED REPLACEMENT |
| Parent | WP06-P4H-7B-POSTING-MATRIX-001 v0.1 |
| Architecture | MDG-ARCH-MASTER-001 v2.5 |
| O-01 Change Request | CR-ARCH-O01-REPLACEMENT-ACCEPTANCE-001 v0.1 — OWNER APPROVED |
| Design | COMPLETE EVENT DISPOSITION REGISTER; POSTING RULE DETAIL PARTIAL |
| Implementation | NOT VERIFIED |
| Tests | NOT RUN / NOT VERIFIED |
| Evidence | NOT ACCEPTED |
| Posting Release | NOT GRANTED |

## 1. قاعده تعیین تکلیف

هر FE-01..FE-38 باید یا Posting Rule کامل داشته باشد یا به‌صورت صریح BLOCKED/DEFERRED بماند. در این نسخه هیچ Debit/Credit، NormalBalance، حساب مقابل، AmountBasis یا tax treatment از روی نام حساب یا prefix حدس زده نشده است. هر رویداد OPEN/CONDITIONAL تا تکمیل تمام فیلدهای قرارداد 7B از نظر ایجاد Journal جدید در وضعیت `POSTING_BLOCKED` است؛ وضعیت عملیاتی/تجاری رویداد می‌تواند جداگانه و بدون اثر مالی حفظ شود.

## 2. قرارداد اجباری هر Posting Rule

`FEId, EventName, SourceEvent, Trigger, Preconditions, RecognitionTime, SourceDocument, DebitAccountCode, DebitCondition, CreditAccountCode, CreditCondition, AmountBasis, CostBasis, TaxMode, SubledgerUpdates, Dimensions, InventoryEffect, PartyEffect, PostingIdentity, IdempotencyKey, ReversalRule, PeriodRule, AtomicityBoundary, RetryBehavior, Authorization, AuditEvents, TestCaseIDs, EvidenceRequirements, SourceRefs, DesignStatus, OpenIssueIDs`.

## 3. ماتریس تعیین تکلیف نهایی FE-01..FE-38

| FE | وضعیت ثبت در R1 | قاعده قطعی/محدودیت | شرط رفع BLOCK | Test ID |
|---|---|---|---|---|
| FE-01 | BLOCKED_PENDING_RULE | 4101 فقط candidate؛ فروش کالا قبل از trigger/تحویل معتبر revenue نمی‌شود | SourceEvent، delivery trigger، recognition، هر دو سمت، مبلغ/مالیات و زیرمعین | MDG-FIN-GATE-001 |
| FE-02 | BLOCKED_PENDING_RULE | 4102 فقط candidate؛ خدمت باز/تکمیل‌نشده revenue نیست | تکمیل فنی/تحویل، سند مبدأ، recognition، debit/credit، tax و زیرمعین | MDG-FIN-GATE-002 |
| FE-03 | BLOCKED_PENDING_RULE | دریافت وجه به‌تنهایی درآمد نیست | روش cash/bank/PSP، clearing، source identity، allocation، fee و هر دو سمت | MDG-FIN-GATE-003 |
| FE-04 | BLOCKED_PENDING_RULE | 2105 فقط در چارچوب پیش‌دریافت مشتری؛ income نیست | lifecycle تعهد، receipt account، allocation/consumption، refund، reversal و subledger | MDG-FIN-GATE-004 |
| FE-05 | BLOCKED_PENDING_RULE | تخصیص مستقل از receipt؛ revenue duplication ممنوع | partial/multiple allocation، over-allocation، unapplied balance، reversal و idempotency | MDG-FIN-GATE-005 |
| FE-06 | BLOCKED_PENDING_RULE | 2101 بدهی فاکتورشده؛ 2103 کالا GRNI؛ 110701 مالیات خرید مشروط | invoice/receipt timing، tax matrix، inventory/cost recognition و هر دو سمت | MDG-FIN-GATE-006 |
| FE-07 | BLOCKED_PENDING_RULE | 110801 تنها GL inventory control؛ 110802..110806 زیرمعین‌اند | ownership/receipt trigger، valuation، inventory subledger و atomicity | MDG-FIN-GATE-007 |
| FE-08 | BLOCKED_PENDING_RULE | 5101 candidate؛ WAC در ProductVariant×Warehouse و Specific Identification برای serial item | valuation policy کامل، source movement، cost layers، inventory credit و concurrency | MDG-FIN-GATE-008 |
| FE-09 | BLOCKED_R1 | 110807 inactive R1؛ COGS پیش از تحویل/شرط شناخت ممنوع | تصمیم scope/ownership و قرارداد مستقل؛ بدون فعال‌سازی 110807 در R1 | MDG-FIN-GATE-009 |
| FE-10 | BLOCKED_PENDING_RULE | برگشت مالی از فروش اصلی؛ مستقل از برگشت فیزیکی | credit/refund، tax/price/COGS، source link و reversal | MDG-FIN-GATE-010 |
| FE-10b | BLOCKED_PENDING_RULE | حرکت فیزیکی برگشت کالا مستقل از refund | inspection, disposition, warehouse, valuation و link به اصل | MDG-FIN-GATE-011 |
| FE-11 | BLOCKED_PENDING_RULE | برگشت خرید به سند خرید/دریافت اصلی وصل باشد | supplier credit, inventory, tax, AP/GRNI و reversal | MDG-FIN-GATE-012 |
| FE-12 | BLOCKED_NO_REFUND_PAYABLE | 2107 Refund Payable نیست؛ بدهی refund هنوز canonical نشده | تعیین حساب/چرخه refund از منبع/تصمیم معتبر و mapping کامل | MDG-FIN-GATE-013 |
| FE-13 | BLOCKED_PENDING_MAPPING | ماهیت تخفیف Contra Revenue؛ 4201..4203 unresolved | mapping حساب، reason code، NormalBalance، authorization و test | MDG-FIN-GATE-014 |
| FE-14 | BLOCKED_PENDING_SOURCE_CROSSWALK | نقش چک طبق OWN-7C-004-R1؛ source title/attributes و posting mapping جداگانه باز | canonical attribute/source crosswalk، state machine، transition rules و lifecycle test | MDG-FIN-GATE-015 |
| FE-15 | BLOCKED | 6104 حساب عمومی Landed Cost نیست | ماهیت اقتصادی، capitalization/expense، allocation basis و policy approval | MDG-FIN-GATE-016 |
| FE-16 | BLOCKED_PENDING_COUNT_CONTROL | 110808 کسری تعلیقی؛ 2113 مازاد شمارش؛ مازاد درآمد خودکار نیست | count evidence، recount، independent approval، suspense clearing و test | MDG-FIN-GATE-017 |
| FE-17 | BLOCKED_PENDING_CASH_CONTROL | 2112 suspense؛ 610801 فقط کسری نقدی قطعی و تأییدشده | cash count، cause, independent approval، aging/clearing و test | MDG-FIN-GATE-018 |
| FE-18 | BLOCKED_PENDING_CLEARING_RULE | 1110 فقط وجه نقد فیزیکی در راه؛ 110302 PSP | transfer/clearing lifecycle، bank/PSP reconciliation و عدم دوباره‌شماری | MDG-FIN-GATE-019 |
| FE-19 | BLOCKED_PENDING_NATURE | خسارت/ضایعات/سرقت/خرابی و مسئولیت تکنسین ماهیت‌های مستقل‌اند | cause classification، source evidence، approval، recovery vs expense و tax | MDG-FIN-GATE-020 |
| FE-20 | BLOCKED | 2114 فقط خدمت دریافت/تأییدشده با تعهد قابل اثبات؛ 5103 blocked؛ 2102 inactive | service evidence، recognition، cost account، invoice settlement، reversal | MDG-FIN-GATE-021 |
| FE-21 | BLOCKED_PENDING_TRADEIN | خرید دستگاه دست‌دوم و فروش کالای جدید دو سند جدا با TradeInId مشترک؛ netting ممنوع | ownership acceptance، valuation، inventory recognition، tax و linked documents | MDG-FIN-GATE-022 |
| FE-22 | BLOCKED_PENDING_FEE_RULE | 6106/6107 candidate؛ 110302 PSP double count ممنوع | statement source، fee recognition، settlement reconciliation و fee identity | MDG-FIN-GATE-023 |
| FE-23 | BLOCKED_PENDING_COMMISSION_RULE | 5104/2108 فقط پس از technical completion معتبر؛ payroll جداست | commission basis، eligibility, approval, reversal/clawback و no duplicate | MDG-FIN-GATE-024 |
| FE-24 | SCOPE_BLOCKED_R1 | 1201 cost، 1202 accumulated depreciation، 6105 depreciation expense؛ ماژول کامل R2 | بازشدن R2، fixed asset subledger و lifecycle tests | MDG-FIN-GATE-025 |
| FE-25 | SCOPE_BLOCKED_R1 | اقساط خارج از R1 | R2 scope approval و schedule/interest/receivable rules | MDG-FIN-GATE-026 |
| FE-26 | SCOPE_BLOCKED_R1 | FX/revaluation خارج از R1 | R2 scope approval، rate source/effective date و FX rules | MDG-FIN-GATE-027 |
| FE-27 | BLOCKED_PENDING_TAX_MATRIX | TaxMode و Tax Matrix معتبر لازم؛ TaxMode=0 تمام tax channels را مسدود می‌کند | tax authority source, version/effective date, exemptions, rounding, negative tests | MDG-FIN-GATE-028 |
| FE-28 | BLOCKED_PENDING_OPENING_EVIDENCE | 3199 موقت؛ opening balance بدون evidence/approval ممنوع | source balance, measurement basis, approval, reconciliation and audit | MDG-FIN-GATE-029 |
| FE-29 | BLOCKED_PENDING_PERIOD_CONTROL | close تا reconciliation و رفع pending journals مجاز نیست | close checklist، hard/soft close، lock, reopen approval و idempotency | MDG-FIN-GATE-030 |
| FE-30 | BLOCKED_PENDING_PERMISSION | Manual Journal نیازمند permission، SoD، audit و reversal است | role matrix، approval workflow، account eligibility و tests | MDG-FIN-GATE-031 |
| FE-31 | SCOPE_BLOCKED_R1 | انتقال بین انبارها خارج از R1 | R2 scope approval، Inventory contract و atomicity test | MDG-FIN-GATE-032 |
| FE-32 | SCOPE_BLOCKED_R1 | WIP عمومی R1 فعال نیست؛ 110807 جایگزین WIP نیست | R2 scope approval و WIP ownership/accounting contract | MDG-FIN-GATE-033 |
| FE-33 | BLOCKED_PENDING_ALLOCATION_RULE | 110703 خروجی تخصیص‌نیافته؛ 2107 ورودی تخصیص‌نیافته | receipt/payment directions، allocation lifecycle، aging و reconciliation | MDG-FIN-GATE-034 |
| FE-34 | BLOCKED_PENDING_EQUITY_CLOSE | year close نیازمند idempotency، atomicity و mapping حقوق مالکانه | equity map، period/year workflow، repeat-safe close و approval | MDG-FIN-GATE-035 |
| FE-35 | BLOCKED_PENDING_EXPENSE_TAXONOMY | هزینه بر اساس ماهیت واقعی؛ 110702 فقط پیش‌پرداخت واقعی | expense taxonomy، source evidence، service period و allocation schedule | MDG-FIN-GATE-036 |
| FE-36 | BLOCKED_FAIL_CLOSED | event ناشناخته یا mapping ناشناخته هیچ fallback posting ندارد | فقط با افزودن event/rule نسخه‌دار، review و tests قابل پذیرش | MDG-FIN-GATE-037 |
| FE-37 | SCOPE_BLOCKED_R1 | payroll خارج از R1؛ commission عملیاتی FE-23 جداست | R2 approval، payroll owner contract و tax/accounting tests | MDG-FIN-GATE-038 |
| FE-38 | BLOCKED_PENDING_EQUITY_MAP | 3101..3105 unresolved؛ 3199 temporary opening control است | equity agreement، source, NormalBalance, mapping and year-close test | MDG-FIN-GATE-039 |

## 4. کنترل‌های عمومی غیرقابل عدول

1. هر Posting Rule باید تمام فیلدهای §2 را تکمیل کند.
2. حساب inactive/prohibited/unverified/non-postable یا بدون direct-posting permission از سمت سرور و DB مسدود شود.
3. هر FE دارای PostingIdentity و idempotency key پایدار باشد.
4. posting در دوره بسته ممنوع؛ reopen نیازمند role، reason و تأیید مستقل دوم.
5. journal finalized immutable؛ اصلاح با reversal/adjustment مرتبط.
6. inventory/GL/subledger effects atomic یا دارای recovery contract اثبات‌شده باشند.
7. TaxMode=0 از UI/API/import/integration/manual journal هیچ tax posting ایجاد نکند.
8. unknown event/account/mapping fail closed؛ fallback account ممنوع.
9. 100,000 IRR per line یک موضوع مستقل و OPEN است؛ tolerance یا rounding threshold نیست.
10. ADR-0017: integer-only IRR، AwayFromZero، largest remainder deterministic allocation و حداکثر ±1 IRR اثر گردکردن در سطح سند؛ این سیاست به‌تنهایی eligibility حساب‌های 4107/5106 را مجاز نمی‌کند.

## 5. وضعیت

- FE disposition coverage: 38/38 — COMPLETE AT DESIGN-GATE LEVEL.
- Posting rule detail: PARTIAL; no incomplete FE is authorized to post.
- Positive posting journals: NOT SPECIFIED where 7B/7C incomplete.
- Test linkage: MDG-FIN-GATE-001..039 in 7D v0.2.
- Implementation: NOT VERIFIED.
- Tests: NOT RUN / NOT VERIFIED.
- Evidence: NOT ACCEPTED.
- Posting Release: NOT GRANTED.

 
## 4. Owner Action Register برای هر FE

| FE | Owner action / مسئول تکمیل قرارداد | شرط خروج از BLOCKED |
|---|---|---|
| FE-01 | Sales + Finance | تحویل/شناخت، سند مبدأ، هر دو سمت و amount/tax basis |
| FE-02 | RepairOperations + Finance | تکمیل فنی/تحویل، recognition و حساب مقابل |
| FE-03 | Treasury + Finance | payment method، clearing، receipt identity و allocation |
| FE-04 | Treasury + Finance | پیش‌دریافت، liability lifecycle و settlement/refund |
| FE-05 | Treasury + Finance | partial allocation، balance، over-allocation و reversal |
| FE-06 | Purchasing + Finance + Tax | invoice/receipt order، AP/GRNI و Tax Matrix |
| FE-07 | Inventory + Finance | receipt/ownership، valuation و inventory subledger |
| FE-08 | Sales + Inventory + Finance | valuation policy، stock issue و COGS recognition |
| FE-09 | RepairOperations + Inventory + Finance | قرارداد مالکیت/مصرف قطعه و R1/R2 guard؛ 110807 در R1 غیرفعال |
| FE-10 | Sales + Finance | credit/refund linkage، tax و reversal |
| FE-10b | Inventory + Sales | inspection، stock disposition و movement |
| FE-11 | Purchasing + Inventory + Finance | supplier return، credit note، inventory و AP/GRNI |
| FE-12 | Treasury + Sales + Finance | تعیین Refund Payable و lifecycle پرداخت |
| FE-13 | Sales + Pricing + Finance | discount reason, account map و approval |
| FE-14 | Treasury + Finance | source-title crosswalk، cheque state machine و lifecycle tests |
| FE-15 | Purchasing + Finance | economic nature، capitalization/expense و allocation |
| FE-16 | Inventory + Finance | count evidence، independent approval و suspense clearing |
| FE-17 | Treasury + Finance | cash count، variance approval و clearing |
| FE-18 | Treasury + Finance | bank/PSP/cash clearing و reconciliation |
| FE-19 | Inventory/RepairOperations + Finance | cause classification، evidence و recovery/expense policy |
| FE-20 | Purchasing + RepairOperations + Finance | service evidence، acceptance، liability/cost account و reversal |
| FE-21 | Purchasing + Sales + Inventory + Finance | two linked docs، ownership، valuation و inventory recognition |
| FE-22 | Treasury + Finance | bank/PSP statement، fee recognition و no-double-count |
| FE-23 | RepairOperations + Payroll + Finance | commission eligibility، completion trigger و clawback |
| FE-24 | Finance + Fixed Assets (R2) | R2 authorization و fixed-asset lifecycle |
| FE-25 | Finance + Treasury (R2) | installment schedule، interest و settlement rules |
| FE-26 | Finance + Treasury (R2) | FX source/effective date و revaluation rules |
| FE-27 | Finance + Tax | Tax Matrix، effective dates، exemptions و negative channel tests |
| FE-28 | Finance | opening evidence، measurement basis و approval |
| FE-29 | Finance | close checklist، reconciliation blockers و reopen approval |
| FE-30 | Finance + IdentityAccess/Security | permission matrix، SoD، audit و immutable journal |
| FE-31 | Inventory + Organization + Finance (R2) | warehouse transfer contract و atomic movement |
| FE-32 | RepairOperations + Inventory + Finance (R2) | WIP ownership/valuation و scope guard |
| FE-33 | Treasury + Finance | directionally distinct unallocated receipt/payment lifecycle |
| FE-34 | Finance | equity mapping، idempotent year close و approval |
| FE-35 | Finance + Treasury | expense taxonomy، source evidence و prepayment distinction |
| FE-36 | Source module owner + Finance | event registry، versioned rule و fail-closed tests |
| FE-37 | Payroll + RepairOperations + Finance (R2) | payroll ownership، commission separation و R2 tests |
| FE-38 | Finance + Product Owner | equity agreement، source، NormalBalance و year-close mapping |

این Owner Action Register نقش‌های دامنه‌ای را مشخص می‌کند؛ به معنی شروع runtime فازهای آینده یا تغییر ترتیب فازها نیست. مسئولیت اجرای قراردادها تابع بازشدن فاز، DoR و اختیارهای مصوب است.
