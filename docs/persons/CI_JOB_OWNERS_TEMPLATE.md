# مالکیت کنترل‌های CI ماژول اشخاص — تکمیل اجباری پیش از G1

این جدول عمداً با نقش‌های مسئول و محل درج مالک واقعی آماده شده است. نقش یا نام واقعی را حدس نزنید. پیش از شروع G1 باید مسئول انسانی، مسیر رسیدگی به شکست و جایگزین مسئول ثبت شود.

| Required check / job | مسئول واقعی | جایگزین | محل رسیدگی به شکست | وضعیت |
|---|---|---|---|---|
| `foundation-build-test` | تکمیل شود | تکمیل شود | PR / CI | باز |
| `persons-spec-lint` | تکمیل شود | تکمیل شود | PR / CI | باز |
| `persons-architecture-boundary` | تکمیل شود | تکمیل شود | PR / CI | باز |
| `persons-consumer-registry` | تکمیل شود | تکمیل شود | PR / CI | باز |
| `persons-api-contract` | تکمیل شود | تکمیل شود | PR / CI | باز |
| `persons-temporary-data-retention` | تکمیل شود | تکمیل شود | PR / CI | باز |
| `persons-merge-contract` | تکمیل شود | تکمیل شود | PR / CI | باز |
| `persons-postgres-migrations` | تکمیل شود | تکمیل شود | PR / CI | باز |
| `persons-security-tests` | تکمیل شود | تکمیل شود | PR / CI | باز |
| `persons-e2e-disposable` | تکمیل شود | تکمیل شود | PR / CI | باز |
| `persons-ci-enforcement-test` | تکمیل شود | تکمیل شود | PR / CI و SCM ruleset | باز |
| `persons-ci-evidence` | تکمیل شود | تکمیل شود | PR / CI evidence artifact | باز |

## شواهد لازم

- شناسه/نام مالک واقعی در این فایل ثبت شود.
- نام دقیق job در CI با Required Status Check منطبق باشد.
- قفل شاخه یا ruleset در مخزن remote فعال باشد؛ اجرای workflow در این ZIP به تنهایی قفل Merge را فعال نمی‌کند.
- یک Pull Request آزمایشی با شکست عمدی یک check واقعاً قابل Merge نباشد؛ URL/شناسه PR و نتیجه ثبت شود.
- هر تغییر در job، نام check، شرط اجرا یا وضعیت Required نیازمند بازبینی و آزمون مجدد enforcement است.
