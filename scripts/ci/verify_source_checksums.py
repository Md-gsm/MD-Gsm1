#!/usr/bin/env python3
"""Verify SHA256SUMS.txt against the git-tracked files (both directions).

Fails when a listed file is missing or modified, when a tracked file is not
listed, or when the manifest lists a file that is not tracked. SHA256SUMS.txt
itself is excluded. Regenerate with:  python scripts/ci/verify_source_checksums.py --write
"""
import hashlib
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "SHA256SUMS.txt"


def tracked() -> list[str]:
    out = subprocess.check_output(["git", "ls-files", "-z"], cwd=ROOT).decode("utf-8")
    return sorted((p for p in out.split("\0") if p and p != "SHA256SUMS.txt"), key=lambda s: s.encode("utf-8"))


def digest(rel: str) -> str:
    return hashlib.sha256((ROOT / rel).read_bytes()).hexdigest()


def main() -> int:
    files = tracked()
    if "--write" in sys.argv:
        MANIFEST.write_text("".join(f"{digest(f)}  {f}\n" for f in files), encoding="utf-8", newline="\n")
        print(f"Wrote {len(files)} entries.")
        return 0
    listed: dict[str, str] = {}
    for n, line in enumerate(MANIFEST.read_text(encoding="utf-8").splitlines(), 1):
        if not line.strip() or line.startswith("#"):
            continue
        h, sep, p = line.partition("  ")
        if not sep or len(h) != 64:
            print(f"FAIL: invalid line {n}")
            return 1
        listed[p] = h
    errors = []
    for f in files:
        if f not in listed:
            errors.append(f"not listed: {f}")
    for p, h in listed.items():
        if p not in files:
            errors.append(f"listed but not tracked: {p}")
        elif digest(p) != h:
            errors.append(f"checksum mismatch: {p} (actual sha256: {digest(p)})")
    if errors:
        print("FAIL: SHA256SUMS.txt is out of date:\n  " + "\n  ".join(errors[:50]))
        return 1
    print(f"PASS: {len(files)} tracked files match SHA256SUMS.txt.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
