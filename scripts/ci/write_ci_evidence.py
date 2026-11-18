#!/usr/bin/env python3
"""Create a PII-free, commit-bound CI evidence manifest for the Persons gates."""
from __future__ import annotations
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

UPSTREAM_JOBS = (
    "foundation-build-test",
    "persons-spec-lint",
    "persons-architecture-boundary",
    "persons-consumer-registry",
    "persons-api-contract",
    "persons-temporary-data-retention",
    "persons-merge-contract",
    "persons-postgres-migrations",
    "persons-postgres-windows-migrations",
    "persons-security-tests",
    "persons-e2e-disposable",
    "persons-ci-enforcement-test",
)


def sha256_bytes(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def schema_fingerprint(root: Path) -> str:
    migration_root = root / "database" / "migrations"
    h = hashlib.sha256()
    files = sorted(migration_root.rglob("*.sql")) if migration_root.exists() else []
    for path in files:
        rel = path.relative_to(root).as_posix().encode("utf-8")
        content = path.read_bytes()
        h.update(len(rel).to_bytes(4, "big")); h.update(rel)
        h.update(len(content).to_bytes(8, "big")); h.update(content)
    return h.hexdigest()


def summarize_trx(results_dir: Path | None) -> dict:
    if not results_dir or not results_dir.exists():
        return {"available": False, "files": 0, "total": None, "passed": None, "failed": None, "skipped": None, "other": None}
    files = sorted(results_dir.rglob("*.trx"))
    if not files:
        return {"available": False, "files": 0, "total": None, "passed": None, "failed": None, "skipped": None, "other": None}
    counts = {"passed": 0, "failed": 0, "skipped": 0, "other": 0}
    for trx in files:
        try:
            tree = ET.parse(trx)
        except ET.ParseError:
            counts["other"] += 1
            continue
        seen_result = False
        for node in tree.iter():
            if node.tag.rsplit("}", 1)[-1] != "UnitTestResult":
                continue
            seen_result = True
            outcome = (node.attrib.get("outcome") or "").lower()
            if outcome in {"passed", "pass", "success"}:
                counts["passed"] += 1
            elif outcome in {"failed", "fail", "error", "timeout", "aborted"}:
                counts["failed"] += 1
            elif outcome in {"notexecuted", "not executed", "skipped", "inconclusive", "pending"}:
                counts["skipped"] += 1
            else:
                counts["other"] += 1
        if not seen_result:
            counts["other"] += 1
    total = sum(counts.values())
    return {"available": total > 0, "files": len(files), "total": total, **counts}


def build_evidence(root: Path, needs: dict, results_dir: Path | None = None, env: dict | None = None) -> dict:
    env = env or os.environ
    manifest = json.loads((root / "docs/persons/persons-module-manifest.json").read_text(encoding="utf-8"))
    acceptance = json.loads((root / "docs/persons/acceptance-gate-map.json").read_text(encoding="utf-8"))
    consumers = json.loads((root / "docs/persons/consumer-registry.json").read_text(encoding="utf-8"))
    states = {}
    for job in UPSTREAM_JOBS:
        item = needs.get(job)
        states[job] = item.get("result", "unknown") if isinstance(item, dict) else "unknown"
    counts = {"PASS": 0, "FAIL": 0, "SKIP": 0, "UNKNOWN": 0}
    for state in states.values():
        if state == "success": counts["PASS"] += 1
        elif state == "failure": counts["FAIL"] += 1
        elif state == "skipped": counts["SKIP"] += 1
        else: counts["UNKNOWN"] += 1

    contract_path = root / "docs/persons/contracts/persons.openapi.yaml"
    contract_version = None
    if contract_path.exists():
        contract_version = sha256_bytes(contract_path.read_bytes())
    report = {
        "evidenceVersion": "1.0",
        "generatedAtUtc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "commitSha": env.get("GITHUB_SHA", "local-uncommitted"),
        "workflowRunId": env.get("GITHUB_RUN_ID", "local"),
        "workflowRunAttempt": env.get("GITHUB_RUN_ATTEMPT", "1"),
        "contractVersions": {
            "personsManifest": manifest.get("manifestVersion"),
            "acceptanceMap": acceptance.get("mapVersion"),
            "consumerRegistry": consumers.get("registryVersion"),
            "personsOpenApiSha256": contract_version,
            "personsOpenApiStatus": "PUBLISHED" if contract_path.exists() else "NOT_PUBLISHED_IN_PREPARATION_STAGE",
        },
        "schemaFingerprintSha256": schema_fingerprint(root),
        "gateResults": states,
        "gateOutcomeCounts": counts,
        "testCounts": summarize_trx(results_dir),
        "redaction": {
            "containsRawPersonalData": False,
            "containsRequestPayloads": False,
            "containsCredentials": False,
            "includedFields": ["commitSha", "workflowRunId", "contractVersions", "schemaFingerprintSha256", "gateResults", "gateOutcomeCounts", "testCounts"],
        },
    }
    return report


def enforce(report: dict) -> list[str]:
    failures = []
    states = report.get("gateResults", {})
    for job in UPSTREAM_JOBS:
        if states.get(job) != "success":
            failures.append(f"required gate '{job}' result is {states.get(job, 'missing')}")
    counts = report.get("gateOutcomeCounts", {})
    if counts.get("SKIP", 0) or counts.get("UNKNOWN", 0):
        failures.append("one or more required jobs were skipped or have unknown status")
    tests = report.get("testCounts", {})
    if not tests.get("available") or not isinstance(tests.get("total"), int) or tests["total"] < 1:
        failures.append("TRX test result summary is missing or empty")
    elif tests.get("failed", 0) or tests.get("skipped", 0) or tests.get("other", 0):
        failures.append("one or more .NET tests failed, skipped, or had an unknown outcome")
    if not report.get("commitSha") or report["commitSha"] == "local-uncommitted":
        failures.append("evidence is not bound to a committed revision")
    if not report.get("schemaFingerprintSha256"):
        failures.append("schema fingerprint is missing")
    redaction = report.get("redaction", {})
    if redaction.get("containsRawPersonalData") is not False or redaction.get("containsRequestPayloads") is not False or redaction.get("containsCredentials") is not False:
        failures.append("redaction policy is not satisfied")
    return failures


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--out", type=Path, default=Path("artifacts/persons-ci-evidence.json"))
    parser.add_argument("--enforce", action="store_true", help="Exit nonzero unless every upstream gate and .NET test passes.")
    args = parser.parse_args()
    root = args.root.resolve()
    out = args.out if args.out.is_absolute() else (root / args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    try:
        needs = json.loads(os.environ.get("GATE_NEEDS_JSON", "{}"))
        results_dir = root / "artifacts" / "test-results"
        report = build_evidence(root, needs, results_dir)
        out.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        print(f"Wrote PII-free CI evidence: {out.relative_to(root) if out.is_relative_to(root) else out}")
        if args.enforce:
            failures = enforce(report)
            if failures:
                for failure in failures: print(f"FAIL: {failure}", file=sys.stderr)
                return 1
            print("PASS: all required gates and .NET tests succeeded; evidence is commit-bound and redacted.")
        return 0
    except (OSError, ValueError, json.JSONDecodeError, ET.ParseError) as exc:
        print(f"FAIL: could not produce CI evidence: {type(exc).__name__}", file=sys.stderr)
        return 1

if __name__ == "__main__":
    raise SystemExit(main())
