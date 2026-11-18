#!/usr/bin/env python3
"""Structural cross-check for the controlled finance 7B/7C/7D documents."""
from __future__ import annotations
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
P7B = ROOT / "docs/finance/workpapers/p4h/7b/WP06-P4H-7B-POSTING-MATRIX-001_v0.2.md"
P7C = ROOT / "docs/finance/workpapers/p4h/7c/WP06-P4H-7C-COA-RECONCILIATION-001_v0.4.md"
ADD = ROOT / "docs/finance/workpapers/p4h/integration/MDG-WP06-5-STAGE-EXECUTION-REPORT_v0.2.md"
P7D = ROOT / "docs/finance/workpapers/p4h/7d/WP06-P4H-7D-TEST-REGISTRY-001_v0.2.md"

def fail(message: str) -> None:
    print(f"FAIL: {message}", file=sys.stderr)
    raise SystemExit(1)

def read(path: Path) -> str:
    if not path.is_file():
        fail(f"missing file: {path.relative_to(ROOT)}")
    return path.read_text(encoding="utf-8")

def exact(label: str, actual: list[str], expected: set[str]) -> None:
    if len(actual) != len(set(actual)) or set(actual) != expected or len(actual) != len(expected):
        fail(f"{label}: actual={sorted(actual)} expected={sorted(expected)}")

b, c, a, d = map(read, (P7B, P7C, ADD, P7D))
section = b.split("## 3. ماتریس تعیین تکلیف نهایی FE-01..FE-38", 1)[1].split("## 4. کنترل‌های عمومی غیرقابل عدول", 1)[0]
fe_rows = [x for x in section.splitlines() if x.startswith("| FE-")]
fe_ids = [re.search(r"^\|\s*(FE-\d{2}b?)\s*\|", x).group(1) for x in fe_rows]
expected_fe = {f"FE-{n:02d}" for n in range(1, 39)} | {"FE-10b"}
exact("7B FE rows", fe_ids, expected_fe)
fe_gates = [re.search(r"(MDG-FIN-GATE-\d{3})", x).group(1) for x in fe_rows]
exact("7B FE gate references", fe_gates, {f"MDG-FIN-GATE-{n:03d}" for n in range(1, 40)})
for row in fe_rows:
    cells = [v.strip() for v in row.strip("|").split("|")]
    if len(cells) < 3 or "BLOCKED" not in cells[1]:
        fail(f"non fail-closed FE row: {row}")

def table_rows_between(document: str, start: str, end: str) -> list[str]:
    if start not in document or end not in document:
        fail(f"missing section boundary: {start} / {end}")
    return [x for x in document.split(start, 1)[1].split(end, 1)[0].splitlines()
            if x.startswith("| FE-")]

owner_rows = table_rows_between(
    b, "## 6. Owner Action Register برای هر FE", "## 7. Field-Level Disposition Register"
)
field_rows = table_rows_between(
    b, "## 7. Field-Level Disposition Register", "### 7.1 تفسیر وضعیت"
)
issue_rows = table_rows_between(
    b, "### 7.2 OpenIssue ID و SourceRef دوطرفه برای تمام FEها", "تمام OpenIssueها"
)
for label, rows in (("7B Owner Action", owner_rows), ("7B Field-Level", field_rows),
                    ("7B OpenIssue", issue_rows)):
    ids = [re.search(r"^\|\s*(FE-\d{2}b?)\s*\|", x).group(1)
           for x in rows if re.search(r"^\|\s*(FE-\d{2}b?)\s*\|", x)]
    exact(label, ids, expected_fe)

issue_ids = [re.search(r"(7B-OPEN-\d{3})", x).group(1)
             for x in issue_rows if re.search(r"(7B-OPEN-\d{3})", x)]
exact("7B OpenIssue IDs", issue_ids, {f"7B-OPEN-{n:03d}" for n in range(1, 40)})
if "- Event disposition coverage: 39/39 rows" not in b:
    fail("7B must distinguish 38 FE identifiers from 39 event rows including FE-10b")
if "Integrated v0.6 §6.2" not in b or "7C v0.4 §5" not in b:
    fail("7B source crosswalk must reference current integrated v0.6 and 7C v0.4")
if "Integrated v0.4 §6.2" in b or "7C v0.2 §5" in b:
    fail("7B contains stale source-version references")
fe09 = next((x for x in fe_rows if x.startswith("| FE-09 |")), "")
if "110807 inactive R1" not in fe09 or "OWN-7C-038" not in fe09 or "6105/1202" not in fe09:
    fail("7B must keep FE-09 separate from OWN-7C-038 fixed-asset treatment")
fe14_issue = next((x for x in issue_rows if x.startswith("| FE-14 |")), "")
if "OWN-7C-004-R1" not in fe14_issue:
    fail("7B FE-14 must cite the controlling cheque owner decision")

base_open = re.findall(r"^\|\s*(7C-OPEN-\d{3})\s*\|", c, re.MULTILINE)
added_open = re.findall(r"^\|\s*(7C-OPEN-\d{3})\s*\|", a, re.MULTILINE)
open_ids = base_open + added_open
exact("7C Open Items", open_ids, {f"7C-OPEN-{n:03d}" for n in range(1, 17)})
for token in ("COA v1.2", "v1.4", "110302→1103", "610801→6108", "620201→6202", "2114", "5106", "110809"):
    if token not in c + a:
        fail(f"missing 7C control: {token}")
if "NOT EXECUTABLE — SOURCE CONTENT REQUIRED" in c + a:
    fail("obsolete v1.4 source-content blocker must be removed")
if "v1.4 در دسترس و بررسی شده است" not in c or "7C-OPEN-001 مانع دسترسی نیست" not in c:
    fail("7C v0.4 must affirm current v1.4 source availability and identify canonical reconciliation—not source access—as the remaining blocker")

fin = re.findall(r"^\|\s*(MDG-FIN-GATE-\d{3})\s*\|", d, re.MULTILINE)
coa = re.findall(r"^\|\s*(MDG-COA-GATE-\d{3})\s*\|", d, re.MULTILINE)
shared = re.findall(r"^\|\s*(MDG-FIN-(?!GATE-)[A-Z0-9-]+)\s*\|", d, re.MULTILINE)
exact("7D FE tests", fin, {f"MDG-FIN-GATE-{n:03d}" for n in range(1, 40)})
exact("7D COA tests", coa, {f"MDG-COA-GATE-{n:03d}" for n in range(1, 17)})
exact("7D shared controls", shared, {
    "MDG-FIN-ATOMIC-001", "MDG-FIN-ATOMIC-002", "MDG-FIN-ATOMIC-003",
    "MDG-FIN-IDEM-001", "MDG-FIN-IDEM-002", "MDG-FIN-REV-001",
    "MDG-FIN-PERIOD-001", "MDG-FIN-COA-001", "MDG-FIN-COA-002",
    "MDG-FIN-TAX-001", "MDG-FIN-SUBLEDGER-001", "MDG-FIN-IMEI-001",
})
if len(fin + coa + shared) != 67 or len(set(fin + coa + shared)) != 67:
    fail("7D must contain 67 unique Test IDs")
for row in [x for x in d.splitlines() if x.startswith("| MDG-COA-GATE-")]:
    ref = re.search(r"(7C-OPEN-\d{3})", row)
    if not ref or ref.group(1) not in set(open_ids):
        fail(f"orphan 7D COA test: {row}")
if "Positive accounting journal cases | BLOCKED UNTIL 7B/7C CANONICAL" not in d:
    fail("positive journal tests must remain blocked")
if "NOT RUN / NOT VERIFIED" not in d or "Posting Release | NOT GRANTED" not in d:
    fail("7D execution/no-release guards missing")

print("PASS: finance workpaper structural consistency")
print(f"7B: {len(fe_ids)} event rows (38 FE identifiers + FE-10b); 39 fail-closed gate references")
print(f"7C: {len(open_ids)} Open Items; 16 mapped COA gate tests")
print(f"7D: {len(fin)} FE + {len(coa)} COA + {len(shared)} shared = 67 unique Test IDs")
print("Scope: document structure only; no accounting runtime or positive journal execution.")
