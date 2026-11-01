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
    "persons-postgres-windows-migrations",
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
        raise GateFailure(f"Temporary data policy does not match the proposed v1.1 target values: {policy}")
    criteria = mapping.get("criteria", [])
    ids = [x.get("id") for x in criteria]
    expected = [f"AC-{i:02d}" for i in range(31, 67)]
    if ids != expected:
        raise GateFailure("Acceptance map must contain AC-31..AC-66 exactly once, in order, with no gaps or extras.")
    for item in criteria:
        for key in ("gate", "status", "expectedBehavior"):
            if not str(item.get(key, "")).strip():
                raise GateFailure(f"{item.get('id')} is missing field '{key}'.")
        if item["status"] not in {"UNMAPPED", "IMPLEMENTED"}:
            raise GateFailure(f"Unexpected status for {item['id']}: {item['status']}")
        number = int(item["id"].split("-")[1])
        if number <= 38:
            expected_gate = "persons-temporary-data-retention"
        elif number <= 45:
            expected_gate = "persons-merge-contract"
        elif number <= 48:
            expected_gate = "persons-ci-enforcement-test"
        elif number == 49:
            expected_gate = "persons-ci-evidence"
        else:
            expected_gate = {
                50: "persons-api-contract", 51: "persons-api-contract",
                52: "persons-postgres-migrations", 53: "persons-api-contract",
                54: "persons-api-contract", 55: "persons-security-tests",
                56: "persons-api-contract", 57: "persons-api-contract",
                58: "persons-security-tests", 59: "persons-api-contract",
                60: "persons-postgres-migrations", 61: "persons-merge-contract",
                62: "persons-merge-contract", 63: "persons-merge-contract",
                64: "persons-security-tests", 65: "persons-consumer-registry",
                66: "persons-api-contract",
            }[number]
        if item["gate"] != expected_gate:
            raise GateFailure(f"{item['id']} is mapped to {item['gate']} instead of {expected_gate}.")
        if item["status"] == "UNMAPPED":
            if not item.get("testFile") or not item.get("automatedTest"):
                raise GateFailure(f"{item['id']} is UNMAPPED without a named future test.")
            candidates = list(root.rglob(item["testFile"]))
            if candidates and any(item["automatedTest"] in candidate.read_text(encoding="utf-8", errors="replace") for candidate in candidates):
                raise GateFailure(f"{item['id']} is UNMAPPED but its named automated test already exists.")
        if item["status"] == "IMPLEMENTED":
            if not item.get("testFile") or not item.get("automatedTest"):
                raise GateFailure(f"{item['id']} is IMPLEMENTED without a test file and method.")
            candidates = list(root.rglob(item["testFile"]))
            if not candidates:
                raise GateFailure(f"{item['id']} claims an implemented test but file '{item['testFile']}' is missing.")
            if not any(item["automatedTest"] in candidate.read_text(encoding="utf-8", errors="replace") for candidate in candidates):
                raise GateFailure(f"{item['id']} claims an implemented test but method '{item['automatedTest']}' is absent.")
    all_criteria_implemented = all(item.get("status") == "IMPLEMENTED" for item in criteria)
    if prereqs.get("acceptanceCriteriaAc31ToAc49Mapped") is True and not all_criteria_implemented:
        raise GateFailure("Manifest claims AC-31..AC-66 are mapped, but one or more criteria are UNMAPPED.")
    if manifest.get("g1Authorized") is True and not all_criteria_implemented:
        raise GateFailure("G1 cannot be authorized while any AC-31..AC-66 criterion is UNMAPPED.")
    if "src/Modules/Persons" not in registry.get("ownerPaths", []) or "registeredConsumers" not in registry:
        raise GateFailure("Consumer registry lacks owner paths or registeredConsumers.")
    for consumer in registry.get("registeredConsumers", []):
        for key in ("module", "path", "contractVersion", "mergeProtocolVersion"):
            if not str(consumer.get(key, "")).strip():
                raise GateFailure(f"Registered consumer is missing '{key}': {consumer}")
    owners_doc = root / "docs/persons/CI_JOB_OWNERS_TEMPLATE.md"
    prereqs_file = root / "docs/persons/g1-entry-evidence.json"
    if prereqs.get("jobOwnersAssigned") is True and "تکمیل شود" in owners_doc.read_text(encoding="utf-8"):
        raise GateFailure("jobOwnersAssigned is true but the CI job owner template still contains unassigned placeholders.")
    evidence = load_json(prereqs_file)
    if prereqs.get("requiredStatusChecksConfiguredInScm") is True and not str(evidence.get("branchProtectionEvidence", "")).strip():
        raise GateFailure("SCM Required Status Checks are claimed active but branch-protection evidence is empty.")
    if prereqs.get("negativeCiEnforcementTestPassed") is True and not str(evidence.get("negativeEnforcementPullRequest", "")).strip():
        raise GateFailure("Negative CI enforcement is claimed passed but the evidence PR reference is empty.")
    if prereqs.get("temporaryDataPolicyApproved") is True and not str(evidence.get("policyApprovalReference", "")).strip():
        raise GateFailure("Temporary-data policy approval reference is missing.")
    if prereqs.get("mergeChecklistApproved") is True and not str(evidence.get("mergeChecklistApprovalReference", "")).strip():
        raise GateFailure("Merge checklist approval reference is missing.")
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
    # Third-party actions must be pinned to immutable full commit SHAs.
    action_refs = re.findall(r"(?m)^\s+uses:\s+([^\s]+)", wf)
    unpinned = [ref for ref in action_refs if not re.search(r"@[0-9a-f]{40}$", ref)]
    if unpinned:
        raise GateFailure(f"GitHub Actions must be pinned to full commit SHAs: {unpinned}")
    if "concurrency:" not in wf or "cancel-in-progress: true" not in wf:
        raise GateFailure("Required workflow must serialize/cancel superseded runs with concurrency.")
    if "POSTGRES_HOST_AUTH_METHOD: trust" not in wf:
        raise GateFailure("Linux PostgreSQL service must use isolated runner-local trust authentication, not a repository-stored password.")
    if re.search(r"(?im)^\s*(?:POSTGRES_PASSWORD|PGPASSWORD)\s*:", wf) or re.search(r"(?i)Password\s*=", wf):
        raise GateFailure("The public CI workflow must not embed a static PostgreSQL password or password-bearing connection string.")
    if "image: postgres:18.6-bookworm@sha256:5c4783a8cc3f0074912164f94dfa1be4e043438f0f707cf968d7af9a0d31a2c8" not in wf:
        raise GateFailure("PostgreSQL integration smoke test must use the approved immutable PostgreSQL 18.6 Bookworm digest.")
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
    if "MDGsm.DbMigrator" not in smoke_text or "dotnet run" not in smoke_text:
        raise GateFailure("API-only smoke test must call the application-owned migration runner.")
    if "Get-Content -LiteralPath $MigrationPath" in smoke_text or "$migrationSql | & $PsqlPath" in smoke_text:
        raise GateFailure("API-only smoke test must not apply migrations directly through psql.")
    disposable = root / "scripts/Start-MDGsm-DisposableTest.ps1"
    disposable_text = disposable.read_text(encoding="utf-8", errors="replace") if disposable.exists() else ""
    if "MDGSM_DISPOSABLE_TEST_DB" not in disposable_text:
        raise GateFailure("Stage7 disposable runner must mark its newly created database explicitly as disposable.")
    if "MDGsm.DbMigrator" not in disposable_text or "dotnet run" not in disposable_text:
        raise GateFailure("Stage7 integrated test must call the application-owned migration runner.")
    if "Get-Content -LiteralPath $MigrationPath" in disposable_text or "$migrationSql | & $PsqlPath" in disposable_text:
        raise GateFailure("Stage7 integrated test must not apply migrations directly through psql.")

    runner = root / "src/BuildingBlocks/MDGsm.Infrastructure/Persistence/DatabaseMigrationRunner.cs"
    migrator_project = root / "tools/MDGsm.DbMigrator/MDGsm.DbMigrator.csproj"
    migrator_program = root / "tools/MDGsm.DbMigrator/Program.cs"
    infrastructure_project = root / "src/BuildingBlocks/MDGsm.Infrastructure/MDGsm.Infrastructure.csproj"
    if not runner.exists() or "class DatabaseMigrationRunner" not in runner.read_text(encoding="utf-8"):
        raise GateFailure("Application-owned DatabaseMigrationRunner implementation is missing.")
    if not migrator_project.exists() or not migrator_program.exists():
        raise GateFailure("Executable tools/MDGsm.DbMigrator project is missing.")
    runner_text = runner.read_text(encoding="utf-8")
    if "mdgsm_migration_history" not in runner_text or "pg_advisory_xact_lock" not in runner_text:
        raise GateFailure("Migration runner must track applied versions and serialize concurrent execution.")
    if "SHA256.HashData" not in runner_text:
        raise GateFailure("Migration runner must verify immutable migration checksums.")
    if "EmbeddedResource" not in infrastructure_project.read_text(encoding="utf-8"):
        raise GateFailure("SQL migrations must be embedded into the Infrastructure assembly.")
    manifest = load_json(root / "docs/persons/persons-module-manifest.json")
    if manifest.get("applicationMigrationRunnerImplemented") is not True:
        raise GateFailure("Manifest must declare the application-owned migration runner implementation.")
    workflow_text = workflow.read_text(encoding="utf-8")
    windows_job = re.search(r"(?ms)^  persons-postgres-windows-migrations:\s*\n(?P<body>.*?)(?=^  [A-Za-z0-9_-]+:\s*$|\Z)", workflow_text)
    if not windows_job or "runs-on: windows-latest" not in windows_job.group("body") or "Invoke-PostgreSQL18WindowsMigrationTest.ps1" not in windows_job.group("body"):
        raise GateFailure("A dedicated PostgreSQL 18 Windows job must invoke the Windows migration integration script.")
    windows_script = root / "scripts/Invoke-PostgreSQL18WindowsMigrationTest.ps1"
    windows_text = windows_script.read_text(encoding="utf-8") if windows_script.exists() else ""
    expected_installer_hash = "227e2bc08091b4c2b8338c7d61b11d057f895711480b006f90bfcfcd2410a58a"
    if "postgresql-18.6-5-windows-x64.exe" not in windows_text or expected_installer_hash not in windows_text or "Get-FileHash" not in windows_text:
        raise GateFailure("Windows PostgreSQL job must verify the official PostgreSQL 18.6-5 installer SHA-256.")
    if "MDGSM_DISPOSABLE_TEST_DB" not in windows_text or "newlyApplied=0" not in windows_text:
        raise GateFailure("Windows PostgreSQL job must use a marked disposable database and verify migrator idempotence.")
    if "'--mode', 'unattended'" not in windows_text or "'--optionfile'" not in windows_text:
        raise GateFailure("Windows PostgreSQL installer must explicitly run unattended and read its protected option file.")
    if "'--superpassword'" in windows_text or "'--servicepassword'" in windows_text:
        raise GateFailure("Windows PostgreSQL installer credentials must not be passed as process command-line arguments.")
    if "SetAccessRuleProtection" not in windows_text or "superpassword=$databasePassword" not in windows_text or "servicepassword=$servicePassword" not in windows_text:
        raise GateFailure("Windows PostgreSQL installer option file must be restricted to the runner identity and contain separate per-run credentials.")
    if manifest.get("windowsPostgres18CiIntegrationConfigured") is not True:
        raise GateFailure("Manifest must declare PostgreSQL 18 Windows integration configuration.")

    checksum_verifier = root / "scripts/Verify-SourceChecksums.ps1"
    if not checksum_verifier.exists() or "Get-FileHash" not in checksum_verifier.read_text(encoding="utf-8"):
        raise GateFailure("Source checksum verifier is missing or does not verify SHA-256 hashes.")
    print("PASS: specification, policy values, AC-31..AC-66 mapping, required job IDs, disposable-test safety guards, and checksum verifier.")

def run_architecture(root: Path) -> None:
    expected_projects = {
        "BuildingBlocks/MDGsm.Domain/MDGsm.Domain.csproj": set(),
        "BuildingBlocks/MDGsm.Application/MDGsm.Application.csproj": {"MDGsm.Domain"},
        "BuildingBlocks/MDGsm.Infrastructure/MDGsm.Infrastructure.csproj": {"MDGsm.Application"},
        "BuildingBlocks/MDGsm.Client/MDGsm.Client.csproj": set(),
        "Host/Api/MDGsm.Api.csproj": {
            "MDGsm.Application", "MDGsm.Infrastructure",
            "MDGsm.Persons.Contracts", "MDGsm.IdentityAccess.Contracts",
            "MDGsm.RepairOperations.Contracts", "MDGsm.Finance.Contracts",
        },
        "Host/Desktop/MDGsm.Desktop.csproj": {"MDGsm.Client"},
    }
    modules = ("Persons", "IdentityAccess", "RepairOperations", "Finance")
    for module in modules:
        expected_projects[f"Modules/{module}/Contracts/MDGsm.{module}.Contracts.csproj"] = set()

    project_root = root / "src"
    projects = {
        path.relative_to(project_root).as_posix(): path
        for path in project_root.rglob("*.csproj")
    }
    if set(projects) != set(expected_projects):
        missing = sorted(set(expected_projects) - set(projects))
        unexpected = sorted(set(projects) - set(expected_projects))
        raise GateFailure(f"Modular project inventory mismatch. Missing={missing}; unexpected={unexpected}")

    for relative_path, expected_refs in expected_projects.items():
        project = projects[relative_path]
        tree = ET.parse(project)
        actual_refs = {
            Path(node.attrib.get("Include", "").replace("\\", "/")).stem
            for node in tree.iter()
            if node.tag.endswith("ProjectReference")
        }
        if actual_refs != expected_refs:
            if "/Contracts/" in relative_path and actual_refs:
                module_name = relative_path.split("/")[1]
                raise GateFailure(
                    f"{module_name} Contracts project must not depend on module internals: {sorted(actual_refs)}"
                )
            raise GateFailure(
                f"Project reference boundary violated in {relative_path}: "
                f"expected {sorted(expected_refs)}, found {sorted(actual_refs)}"
            )

    for relative_dir in ("Host/Desktop", "BuildingBlocks/MDGsm.Client"):
        source_dir = project_root / relative_dir
        for source in source_dir.rglob("*.cs"):
            source_text = source.read_text(encoding="utf-8", errors="replace")
            if re.search(r"MdGsmDbContext|Npgsql|Microsoft\\.EntityFrameworkCore|UseNpgsql", source_text):
                raise GateFailure(
                    f"Desktop/client source must not directly use database persistence: {source.relative_to(root)}"
                )

    target = root / "docs/architecture/modular-target.md"
    required_readmes = [
        root / "src/Host/Api/README.md",
        root / "src/Host/Desktop/README.md",
        root / "src/Modules/README.md",
        root / "src/Modules/Persons/Contracts/README.md",
        root / "src/Modules/IdentityAccess/Contracts/README.md",
        root / "src/Modules/RepairOperations/Contracts/README.md",
        root / "src/Modules/Finance/Contracts/README.md",
    ]
    if not target.exists() or any(not path.exists() for path in required_readmes):
        raise GateFailure("Modular target specification or required Host/Contracts boundary documentation is missing.")

    solution = (root / "MDGsm.sln").read_text(encoding="utf-8", errors="replace")
    for module in modules:
        contract_dir = root / "src" / "Modules" / module / "Contracts"
        project = contract_dir / f"MDGsm.{module}.Contracts.csproj"
        marker = contract_dir / "ModuleContractVersion.cs"
        solution_path = f"src\\Modules\\{module}\\Contracts\\MDGsm.{module}.Contracts.csproj"
        if not project.exists() or solution_path not in solution:
            raise GateFailure(f"{module} Contracts project is missing from MDGsm.sln.")
        if not marker.exists() or 'Current = "1.0"' not in marker.read_text(encoding="utf-8"):
            raise GateFailure(f"{module} Contracts version marker is missing or invalid.")

    old_roots = (
        "MDGsm.Domain", "MDGsm.Application", "MDGsm.Infrastructure",
        "MDGsm.Api", "MDGsm.Desktop", "MDGsm.Client",
    )
    for old_root in old_roots:
        if (project_root / old_root).exists():
            raise GateFailure(f"Flat legacy project directory remains under src/: {old_root}")

    print("PASS: modular project inventory, Host/BuildingBlocks dependency graph, isolated Contracts assemblies, and source boundaries.")

def run_registry(root: Path) -> None:
    reg = load_json(root / "docs/persons/consumer-registry.json")
    registered = {str(x.get("path", "")).replace("\\", "/") for x in reg.get("registeredConsumers", [])}
    owner_paths = [str(x).replace("\\", "/").rstrip("/") for x in reg.get("ownerPaths", [])]
    found = set()
    owner_paths.extend(["database/migrations/persons"])
    rx = re.compile(r"\bPartyId\b|\bparty_id\b|REFERENCES\s+parties\b", re.IGNORECASE)
    legacy_rx = re.compile(r"\bPersonId\b|\bperson_id\b", re.IGNORECASE)
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
            if legacy_rx.search(content):
                raise GateFailure(f"Legacy PersonId/person_id identifier is forbidden in source: {rel}")
            if rx.search(content):
                found.add(rel)
    unknown = found - registered
    stale = registered - found
    if unknown:
        raise GateFailure(f"Unregistered PartyId consumer(s): {sorted(unknown)}")
    if stale:
        raise GateFailure(f"Registry contains consumer paths not found in source: {sorted(stale)}")
    print(f"PASS: PartyId consumer registry reconciles. Registered external consumers: {len(registered)}; unknown: 0.")

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
    found = require_flag_consistency(root, "mergeExecutionImplemented", r"PartyMergeService|MergePartiesAsync|MergePartiesEndpoint|MergedIntoPartyId|party_merge_record", "party merge")
    if not found:
        print("PASS-N/A: merge execution is not implemented; gate remains active and rejects untested merge code.")
        return
    require_test(root, "PersonsMergeContractTests.cs", "Person merge")
    reg = load_json(root / "docs/persons/consumer-registry.json")
    # The registry gate independently enumerates all external references; the merge
    # gate must not confuse the Persons module's own PartyId with a consumer.
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
