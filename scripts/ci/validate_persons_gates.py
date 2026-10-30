#!/usr/bin/env python3
"""Static gate validator for the MD-Gsm Persons module preparation stage.

PASS-N/A is allowed only when a gate's runtime implementation is absent AND
its manifest flag explicitly says NOT IMPLEMENTED. Once runtime markers exist,
missing tests/contracts fail the gate rather than being treated as N/A.
"""
from __future__ import annotations
import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REQUIRED_JOBS = {
    "foundation-build-test",
    "persons-spec-lint",
    "persons-architecture-boundary",
    "persons-consumer-registry",
    "persons-api-contract",
    "persons-temporary-data-retention",
    "persons-merge-contract",
    "persons-postgres-migrations",
    "persons-security-tests",
    "persons-e2e-disposable",
    "persons-ci-enforcement-test",
    "persons-ci-evidence",
}
GATES = (
    "spec", "architecture", "registry", "api", "retention", "merge",
    "security", "e2e", "ci-enforcement",
)

class GateFailure(RuntimeError):
    pass

def load_json(path: Path):
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except FileNotFoundError as exc:
        raise GateFailure(f"Required file missing: {path.relative_to(path.parents[2]) if len(path.parents) > 2 else path}") from exc
    except json.JSONDecodeError as exc:
        raise GateFailure(f"Invalid JSON in {path}: {exc}") from exc

def code_files(root: Path):
    for base in (root / "src", root / "database" / "migrations"):
        if not base.exists():
            continue
        for p in base.rglob("*"):
            if p.is_file() and p.suffix.lower() in {".cs", ".sql", ".xaml"} and not any(x in {"bin", "obj"} for x in p.parts):
                yield p

def read_code(root: Path):
    return [(p, p.read_text(encoding="utf-8", errors="replace")) for p in code_files(root)]

def runtime_marker_found(root: Path, pattern: str) -> bool:
    rx = re.compile(pattern, re.IGNORECASE)
    return any(rx.search(content) for _, content in read_code(root))

def require_flag_consistency(root: Path, flag: str, marker_pattern: str, description: str) -> bool:
    manifest = load_json(root / "docs/persons/persons-module-manifest.json")
    declared = bool(manifest.get(flag, False))
    found = runtime_marker_found(root, marker_pattern)
    if declared != found:
        raise GateFailure(
            f"Implementation state mismatch for {description}: manifest={declared}, code-marker-found={found}. "
            "Update the manifest and implement the required contracts/tests together."
        )
    return found

def require_test(root: Path, filename: str, purpose: str) -> None:
    matches = list((root / "tests").rglob(filename)) if (root / "tests").exists() else []
    if not matches:
        raise GateFailure(f"{purpose} is implemented but required automated test file '{filename}' is missing.")

def run_spec(root: Path) -> None:
    manifest = load_json(root / "docs/persons/persons-module-manifest.json")
    mapping = load_json(root / "docs/persons/acceptance-gate-map.json")
    registry = load_json(root / "docs/persons/consumer-registry.json")
    if manifest.get("module") != "Persons":
        raise GateFailure("Module manifest must identify the Persons module.")
    allowed_stages = {"G0_PREPARATION_ONLY", "G1_CONTRACTS", "G2_SCHEMA", "G3_IDENTITY", "G4_CONTACT_DUPLICATES", "G5_MERGE_SECURITY", "G6_IMPORT_DEPLOY", "G7_CONTROLLED_RELEASE"}
    if manifest.get("repositoryStage") not in allowed_stages:
        raise GateFailure(f"Unknown module stage: {manifest.get('repositoryStage')}")
    if manifest.get("implementationState") not in {"NOT_IMPLEMENTED", "PARTIAL", "IMPLEMENTED"}:
        raise GateFailure(f"Unknown implementationState: {manifest.get('implementationState')}")
    prereqs = manifest.get("g1Prerequisites", {})
    required_prereqs = ["temporaryDataPolicyApproved", "mergeChecklistApproved", "requiredStatusChecksConfiguredInScm", "negativeCiEnforcementTestPassed", "jobOwnersAssigned", "acceptanceCriteriaAc31ToAc49Mapped"]
    if manifest.get("g1Authorized") is True:
        if manifest.get("repositoryStage") != "G1_CONTRACTS":
            raise GateFailure("G1 authorization requires repositoryStage=G1_CONTRACTS.")
        missing = [name for name in required_prereqs if prereqs.get(name) is not True]
        evidence = load_json(root / "docs/persons/g1-entry-evidence.json")
        evidence_fields = ["scmRemoteRepository", "branchProtectionEvidence", "negativeEnforcementPullRequest", "policyApprovalReference", "mergeChecklistApprovalReference", "verifiedBy", "verifiedAtUtc"]
        missing.extend([f"evidence.{name}" for name in evidence_fields if not str(evidence.get(name, "")).strip()])
        if evidence.get("status") != "VERIFIED":
            missing.append("evidence.status=VERIFIED")
        if missing:
            raise GateFailure(f"G1 authorization is set but prerequisites/evidence are missing: {missing}")
    elif manifest.get("repositoryStage") == "G0_PREPARATION_ONLY" and manifest.get("implementationState") != "NOT_IMPLEMENTED":
        raise GateFailure("G0_PREPARATION_ONLY must not claim implemented runtime code.")
    policy = manifest.get("currentPolicy", {})
    expected_policy = {
        "temporaryDataTtlDefaultDays": 7,
        "temporaryDataMaxAgeDays": 30,
        "temporaryDataWorkerIntervalMinutes": 15,
        "temporaryDataPurgeDeadlineMinutes": 30,
        "temporaryDataFailureAlertDeadlineMinutes": 5,
        "temporaryDataMaxRenewals": 1,
        "autoMergeAllowed": False,
    }
    if policy != expected_policy:
        raise GateFailure(f"Temporary data policy does not match the approved v1.1 target values: {policy}")
    criteria = mapping.get("criteria", [])
    ids = [x.get("id") for x in criteria]
    expected = [f"AC-{i:02d}" for i in range(31, 50)]
    if ids != expected:
        raise GateFailure("Acceptance map must contain AC-31..AC-49 exactly once, in order, with no gaps or extras.")
    for item in criteria:
        for key in ("gate", "testFile", "automatedTest", "status", "expectedBehavior"):
            if not str(item.get(key, "")).strip():
                raise GateFailure(f"{item.get('id')} is missing field '{key}'.")
        if item["status"] not in {"MAPPED", "IMPLEMENTED"}:
            raise GateFailure(f"Unexpected status for {item['id']}: {item['status']}")
        number = int(item["id"].split("-")[1])
        expected_gate = "persons-temporary-data-retention" if number <= 38 else "persons-merge-contract" if number <= 45 else "persons-ci-enforcement-test" if number <= 48 else "persons-ci-evidence"
        if item["gate"] != expected_gate:
            raise GateFailure(f"{item['id']} is mapped to {item['gate']} instead of {expected_gate}.")
        if item["status"] == "IMPLEMENTED":
            candidates = list(root.rglob(item["testFile"]))
            if not candidates:
                raise GateFailure(f"{item['id']} claims an implemented test but file '{item['testFile']}' is missing.")
            if not any(item["automatedTest"] in candidate.read_text(encoding="utf-8", errors="replace") for candidate in candidates):
                raise GateFailure(f"{item['id']} claims an implemented test but method '{item['automatedTest']}' is absent.")
    if prereqs.get("acceptanceCriteriaAc31ToAc49Mapped") is not True:
        raise GateFailure("AC-31..AC-49 must be mapped before G1.")
    if len(registry.get("ownerPaths", [])) < 4 or "registeredConsumers" not in registry:
        raise GateFailure("Consumer registry lacks owner paths or registeredConsumers.")
    for consumer in registry.get("registeredConsumers", []):
        for key in ("module", "path", "contractVersion", "mergeProtocolVersion"):
            if not str(consumer.get(key, "")).strip():
                raise GateFailure(f"Registered consumer is missing '{key}': {consumer}")
    owners_doc = root / "docs/persons/CI_JOB_OWNERS_TEMPLATE.md"
    prereqs_file = root / "docs/persons/g1-entry-evidence.json"
    owners_text = owners_doc.read_text(encoding="utf-8")
    ci_ownership = manifest.get("ciOwnership", {})
    if prereqs.get("jobOwnersAssigned") is True:
        if ci_ownership.get("model") != "ACCOUNTABLE_TEAM":
            raise GateFailure("jobOwnersAssigned is true but the CI ownership model is not ACCOUNTABLE_TEAM.")
        if ci_ownership.get("individualNamesRequiredInRepository") is not False:
            raise GateFailure("Team-owned CI must explicitly declare that individual roster names are not required in the repository.")
        if ci_ownership.get("membershipMayRotate") is not True:
            raise GateFailure("Team-owned CI must explicitly allow membership rotation.")
        group_name = str(ci_ownership.get("groupDisplayName", "")).strip()
        if not group_name:
            raise GateFailure("CI accountable group display name is missing.")
        missing_job_rows = [
            job for job in REQUIRED_JOBS
            if not any(f"| `{job}` | {group_name} |" in line for line in owners_text.splitlines())
        ]
        if missing_job_rows:
            raise GateFailure(f"CI accountable group is missing ownership rows for: {sorted(missing_job_rows)}.")
    evidence = load_json(prereqs_file)
    if prereqs.get("requiredStatusChecksConfiguredInScm") is True and not str(evidence.get("branchProtectionEvidence", "")).strip():
        raise GateFailure("SCM Required Status Checks are claimed active but branch-protection evidence is empty.")
    if prereqs.get("negativeCiEnforcementTestPassed") is True and not str(evidence.get("negativeEnforcementPullRequest", "")).strip():
        raise GateFailure("Negative CI enforcement is claimed passed but the evidence PR reference is empty.")
    if prereqs.get("temporaryDataPolicyApproved") is True and not str(evidence.get("policyApprovalReference", "")).strip():
        raise GateFailure("Temporary-data policy approval reference is missing.")
    if prereqs.get("mergeChecklistApproved") is True and not str(evidence.get("mergeChecklistApprovalReference", "")).strip():
        raise GateFailure("Merge checklist approval reference is missing.")
    if prereqs.get("jobOwnersAssigned") is True and not str(evidence.get("ciOwnershipApprovalReference", "")).strip():
        raise GateFailure("CI accountable-team approval reference is missing.")
    workflow = root / ".github/workflows/persons-module-gates.yml"
    if not workflow.exists():
        raise GateFailure("Required CI workflow missing: .github/workflows/persons-module-gates.yml")
    wf = workflow.read_text(encoding="utf-8")
    for job in REQUIRED_JOBS:
        if not re.search(rf"(?m)^\s{{2}}{re.escape(job)}:\s*$", wf):
            raise GateFailure(f"Required CI job '{job}' is not defined in workflow.")
        block_match = re.search(
            rf"(?ms)^  {re.escape(job)}:\s*\n(?P<body>.*?)(?=^  [A-Za-z0-9_-]+:\s*$|\Z)", wf
        )
        if not block_match:
            raise GateFailure(f"Could not parse required CI job block '{job}'.")
        job_conditions = re.findall(r"(?m)^    if:\s*(.*?)\s*$", block_match.group("body"))
        if job == "persons-ci-evidence":
            if job_conditions != ["always()"]:
                raise GateFailure("persons-ci-evidence must run with exactly 'if: always()' so failures are still recorded.")
        elif job_conditions:
            raise GateFailure(f"Required CI job '{job}' has a job-level condition that may skip the required check: {job_conditions}.")
    # Do not allow PR/push path filters that could silently omit required checks.
    if re.search(r"(?m)^\s{4}paths(-ignore)?:", wf):
        raise GateFailure("Path-based workflow filters are prohibited for required Persons gates.")
    smoke = root / "scripts/Invoke-Stage7-PostgresSmokeTest.ps1"
    smoke_text = smoke.read_text(encoding="utf-8", errors="replace") if smoke.exists() else ""
    if not smoke_text:
        raise GateFailure("Stage7 PostgreSQL smoke script is missing.")
    if "[string] $DatabasePassword" in smoke_text or "[string]$DatabasePassword" in smoke_text:
        raise GateFailure("Smoke test must not accept a password as a command-line parameter.")
    if "Read-Host" not in smoke_text or "-AsSecureString" not in smoke_text:
        raise GateFailure("Smoke test must acquire the local test password through hidden secure input.")
    if "Safety stop: this test runner only permits a local PostgreSQL server" not in smoke_text:
        raise GateFailure("Smoke test must reject non-loopback database hosts.")
    if "MDGSM_DISPOSABLE_TEST_DB" not in smoke_text:
        raise GateFailure("Smoke test must mark its newly created database explicitly as disposable.")
    disposable = root / "scripts/Start-MDGsm-DisposableTest.ps1"
    disposable_text = disposable.read_text(encoding="utf-8", errors="replace") if disposable.exists() else ""
    if "MDGSM_DISPOSABLE_TEST_DB" not in disposable_text:
        raise GateFailure("Stage7 disposable runner must mark its newly created database explicitly as disposable.")
    checksum_verifier = root / "scripts/Verify-SourceChecksums.ps1"
    if not checksum_verifier.exists() or "Get-FileHash" not in checksum_verifier.read_text(encoding="utf-8"):
        raise GateFailure("Source checksum verifier is missing or does not verify SHA-256 hashes.")
    print("PASS: specification, policy values, AC-31..AC-49 mapping, required job IDs, disposable-test safety guards, and checksum verifier.")

def run_architecture(root: Path) -> None:
    allowed = {
        "MDGsm.Domain": set(),
        "MDGsm.Application": {"MDGsm.Domain"},
        "MDGsm.Infrastructure": {"MDGsm.Application"},
        "MDGsm.Api": {"MDGsm.Application", "MDGsm.Infrastructure"},
        "MDGsm.Client": set(),
        "MDGsm.Desktop": {"MDGsm.Client"},
    }
    projects = list((root / "src").glob("*/*.csproj"))
    names = {p.stem for p in projects}
    if names != set(allowed):
        raise GateFailure(f"Unexpected source project set: {sorted(names)}")
    for proj in projects:
        tree = ET.parse(proj)
        refs = set()
        for node in tree.iter():
            if node.tag.endswith("ProjectReference"):
                include = node.attrib.get("Include", "")
                filename = Path(include.replace("\\", "/")).stem
                refs.add(filename)
        if refs != allowed[proj.stem]:
            raise GateFailure(f"Project reference boundary violated in {proj.stem}: expected {sorted(allowed[proj.stem])}, found {sorted(refs)}")
    for proj_name in ("MDGsm.Client", "MDGsm.Desktop"):
        proj_dir = root / "src" / proj_name
        for p in proj_dir.rglob("*.cs"):
            text = p.read_text(encoding="utf-8", errors="replace")
            if re.search(r"MdGsmDbContext|Npgsql|Microsoft\.EntityFrameworkCore|UseNpgsql", text):
                raise GateFailure(f"Desktop/client source must not directly use database persistence: {p.relative_to(root)}")
    print("PASS: project dependency direction and API-only client boundary.")

def run_registry(root: Path) -> None:
    reg = load_json(root / "docs/persons/consumer-registry.json")
    registered = {str(x.get("path", "")).replace("\\", "/") for x in reg.get("registeredConsumers", [])}
    owner_paths = [str(x).replace("\\", "/").rstrip("/") for x in reg.get("ownerPaths", [])]
    found = set()
    owner_paths.extend(["database/migrations/persons"])
    rx = re.compile(r"\bPersonId\b|\bperson_id\b|REFERENCES\s+persons\b", re.IGNORECASE)
    scanned_extensions = {".cs", ".sql", ".xaml", ".json", ".yml", ".yaml", ".xml", ".props", ".csproj"}
    scan_roots = [root / "src", root / "database" / "migrations"]
    for base in scan_roots:
        if not base.exists():
            continue
        for p in base.rglob("*"):
            if not p.is_file() or p.suffix.lower() not in scanned_extensions or any(x in {"bin", "obj"} for x in p.parts):
                continue
            rel = p.relative_to(root).as_posix()
            if not (rel.startswith("src/") or rel.startswith("database/migrations/")):
                continue
            if any(rel.startswith(path + "/") for path in owner_paths):
                continue
            content = p.read_text(encoding="utf-8", errors="replace")
            if rx.search(content):
                found.add(rel)
    unknown = found - registered
    stale = registered - found
    if unknown:
        raise GateFailure(f"Unregistered PersonId consumer(s): {sorted(unknown)}")
    if stale:
        raise GateFailure(f"Registry contains consumer paths not found in source: {sorted(stale)}")
    print(f"PASS: PersonId consumer registry reconciles. Registered external consumers: {len(registered)}; unknown: 0.")

def run_api(root: Path) -> None:
    manifest = load_json(root / "docs/persons/persons-module-manifest.json")
    found = runtime_marker_found(root, r"/api/v1/persons|MapPersonsEndpoints|PersonsEndpointGroup|PersonController")
    declared = bool(manifest.get("personsApiImplemented", False))
    if found != declared:
        raise GateFailure(f"Persons API implementation state mismatch: manifest={declared}, route/endpoint marker={found}.")
    if not found:
        print("PASS-N/A: no Persons API runtime exists yet; the manifest and source scan prove the not-implemented state.")
        return
    contract = root / "docs/persons/contracts/persons.openapi.yaml"
    contract_text = contract.read_text(encoding="utf-8") if contract.exists() else ""
    if not re.search(r"(?m)^openapi:\s*['\"]?3\.(0|1)\.", contract_text):
        raise GateFailure("Persons API exists but a valid versioned OpenAPI 3.0/3.1 contract is missing.")
    require_test(root, "PersonsApiContractTests.cs", "Persons API")
    print("PASS: Persons API has a versioned contract and automated contract tests.")

def run_retention(root: Path) -> None:
    found = require_flag_consistency(root, "temporaryDataRuntimeImplemented", r"WorkflowContactStaging|TemporaryContactStaging|TemporaryDataRetentionWorker|expires_at_utc|workflow_contact_staging", "temporary data retention")
    if not found:
        print("PASS-N/A: no temporary-data runtime or schema is present; gate is active and will fail if implementation appears without tests.")
        return
    require_test(root, "PersonsTemporaryDataRetentionTests.cs", "Temporary data retention")
    print("PASS: temporary-data runtime has its required automated test file.")

def run_merge(root: Path) -> None:
    found = require_flag_consistency(root, "mergeExecutionImplemented", r"PersonMergeService|MergePersonsAsync|MergePersonsEndpoint|MergedIntoPersonId|person_merge_record", "person merge")
    if not found:
        print("PASS-N/A: merge execution is not implemented; gate remains active and rejects untested merge code.")
        return
    require_test(root, "PersonsMergeContractTests.cs", "Person merge")
    reg = load_json(root / "docs/persons/consumer-registry.json")
    # The registry gate independently enumerates all external references; the merge
    # gate must not confuse the Persons module's own PersonId with a consumer.
    run_registry(root)
    print("PASS: merge code has required contract-test file.")

def run_security(root: Path) -> None:
    found = runtime_marker_found(root, r"/api/v1/persons|MapPersonsEndpoints|PersonController")
    if not found:
        print("PASS-N/A: no Persons API endpoints exist yet; no unauthenticated Persons surface is exposed.")
        return
    require_test(root, "PersonsSecurityTests.cs", "Persons API security")
    print("PASS: Persons endpoints have a required security test file.")

def run_e2e(root: Path) -> None:
    manifest = load_json(root / "docs/persons/persons-module-manifest.json")
    schema_found = runtime_marker_found(root, r"CREATE\s+TABLE\s+(IF\s+NOT\s+EXISTS\s+)?persons\b")
    declared_schema = bool(manifest.get("personsBusinessMigrationsPresent", False))
    if schema_found != declared_schema:
        raise GateFailure(f"Persons schema manifest mismatch: declared={declared_schema}, migration-marker-found={schema_found}.")
    found = runtime_marker_found(root, r"/api/v1/persons|MapPersonsEndpoints|CREATE\s+TABLE\s+(IF\s+NOT\s+EXISTS\s+)?persons\b")
    if not found:
        print("PASS-N/A: Persons API and business schema are absent; E2E test gate is active for later implementation.")
        return
    require_test(root, "PersonsEndToEndTests.cs", "Persons E2E")
    print("PASS: Persons E2E test file exists for implemented runtime/schema.")

def run_ci_enforcement(root: Path) -> None:
    run_spec(root)
    tests = root / "scripts/ci/test_persons_gate_policy.py"
    if not tests.exists():
        raise GateFailure("Gate policy self-tests are missing.")
    if not (root / "scripts/ci/write_ci_evidence.py").exists():
        raise GateFailure("CI evidence writer is missing.")
    print("PASS: required job names, policy self-tests, and CI evidence writer are present. External SCM branch protection still requires remote verification.")

def run_gate(root: Path, gate: str) -> None:
    funcs = {
        "spec": run_spec,
        "architecture": run_architecture,
        "registry": run_registry,
        "api": run_api,
        "retention": run_retention,
        "merge": run_merge,
        "security": run_security,
        "e2e": run_e2e,
        "ci-enforcement": run_ci_enforcement,
    }
    funcs[gate](root)

def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--gate", choices=[*GATES, "all"], default="all")
    args = parser.parse_args()
    root = args.root.resolve()
    if not (root / "MDGsm.sln").exists():
        print(f"FAIL: not an MD-Gsm solution root: {root}", file=sys.stderr)
        return 2
    try:
        for gate in GATES if args.gate == "all" else (args.gate,):
            print(f"--- gate: {gate} ---")
            run_gate(root, gate)
    except (GateFailure, OSError, ValueError, ET.ParseError) as exc:
        print(f"FAIL: {exc}", file=sys.stderr)
        return 1
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
