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
print(f"7B: {len(fe_ids)} FE rows; 39 fail-closed gate references")
print(f"7C: {len(open_ids)} Open Items; 16 mapped COA gate tests")
print(f"7D: {len(fin)} FE + {len(coa)} COA + {len(shared)} shared = 67 unique Test IDs")
print("Scope: document structure only; no accounting runtime or positive journal execution.")
