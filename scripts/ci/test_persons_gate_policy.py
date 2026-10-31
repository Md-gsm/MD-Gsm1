from __future__ import annotations
import contextlib
import io
import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts" / "ci"))
import validate_persons_gates as gates  # noqa: E402
import write_ci_evidence as evidence_writer  # noqa: E402

class PersonsGatePolicyTests(unittest.TestCase):
    def test_unimplemented_retention_gate_is_explicitly_na(self):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            gates.run_retention(ROOT)
        self.assertIn("PASS-N/A", out.getvalue())
        self.assertIn("no temporary-data runtime", out.getvalue())

    def test_declared_retention_implementation_without_tests_fails_closed(self):
        with tempfile.TemporaryDirectory() as tmp:
            dst = Path(tmp) / "repo"
            shutil.copytree(ROOT, dst, ignore=shutil.ignore_patterns(".git", "bin", "obj", "TestResults"))
            manifest_path = dst / "docs/persons/persons-module-manifest.json"
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            manifest["temporaryDataRuntimeImplemented"] = True
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
            marker = dst / "src/MDGsm.Infrastructure/Persons/TemporaryWorker.cs"
            marker.write_text("public sealed class TemporaryDataRetentionWorker {}", encoding="utf-8")
            with self.assertRaises(gates.GateFailure):
                gates.run_retention(dst)

    def test_undeclared_retention_runtime_marker_fails_closed(self):
        with tempfile.TemporaryDirectory() as tmp:
            dst = Path(tmp) / "repo"
            shutil.copytree(ROOT, dst, ignore=shutil.ignore_patterns(".git", "bin", "obj", "TestResults"))
            marker = dst / "src/MDGsm.Infrastructure/Persons/TemporaryWorker.cs"
            marker.write_text("public sealed class TemporaryDataRetentionWorker {}", encoding="utf-8")
            with self.assertRaises(gates.GateFailure):
                gates.run_retention(dst)

    def test_unknown_partyid_consumer_fails_registry_gate(self):
        with tempfile.TemporaryDirectory() as tmp:
            dst = Path(tmp) / "repo"
            shutil.copytree(ROOT, dst, ignore=shutil.ignore_patterns(".git", "bin", "obj", "TestResults"))
            consumer = dst / "src/MDGsm.Application/Reporting/PersonReportQuery.cs"
            consumer.parent.mkdir(parents=True)
            consumer.write_text("public sealed record PersonReportQuery(Guid PartyId);", encoding="utf-8")
            with self.assertRaises(gates.GateFailure):
                gates.run_registry(dst)


    def test_g1_authorization_without_external_evidence_fails(self):
        with tempfile.TemporaryDirectory() as tmp:
            dst = Path(tmp) / "repo"
            shutil.copytree(ROOT, dst, ignore=shutil.ignore_patterns(".git", "bin", "obj", "TestResults", "__pycache__"))
            manifest_path = dst / "docs/persons/persons-module-manifest.json"
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            manifest["repositoryStage"] = "G1_CONTRACTS"
            manifest["g1Authorized"] = True
            manifest["g1Prerequisites"] = {key: True for key in manifest["g1Prerequisites"]}
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
            with self.assertRaisesRegex(gates.GateFailure, r"UNMAPPED|evidence\."):
                gates.run_spec(dst)

    def test_unknown_partyid_in_contract_json_fails_registry_gate(self):
        with tempfile.TemporaryDirectory() as tmp:
            dst = Path(tmp) / "repo"
            shutil.copytree(ROOT, dst, ignore=shutil.ignore_patterns(".git", "bin", "obj", "TestResults"))
            contract = dst / "src/MDGsm.Application/Contracts/ExternalConsumer.json"
            contract.write_text('{"PartyId":"example"}', encoding="utf-8")
            with self.assertRaises(gates.GateFailure):
                gates.run_registry(dst)

    def test_required_jobs_are_unconditionally_scheduled(self):
        workflow_path = ROOT / ".github/workflows/persons-module-gates.yml"
        workflow = workflow_path.read_text(encoding="utf-8")
        gates.run_spec(ROOT)
        self.assertNotRegex(workflow, r"(?m)^\s{4}paths(-ignore)?:")
        with tempfile.TemporaryDirectory() as tmp:
            dst = Path(tmp) / "repo"
            shutil.copytree(ROOT, dst, ignore=shutil.ignore_patterns(".git", "bin", "obj", "TestResults", "__pycache__"))
            bad = dst / ".github/workflows/persons-module-gates.yml"
            content = bad.read_text(encoding="utf-8")
            marker = "  persons-security-tests:\n    name: persons-security-tests\n"
            self.assertIn(marker, content)
            bad.write_text(content.replace(marker, marker + "    if: false\n", 1), encoding="utf-8")
            with self.assertRaisesRegex(gates.GateFailure, "job-level condition"):
                gates.run_spec(dst)

    def test_failed_gate_cannot_be_relabelled_pass_na(self):
        needs = {job: {"result": "success"} for job in evidence_writer.UPSTREAM_JOBS}
        needs["persons-security-tests"] = {"result": "failure"}
        with tempfile.TemporaryDirectory() as tmp:
            trx_dir = Path(tmp)
            (trx_dir / "sample.trx").write_text(
                '<TestRun><Results><UnitTestResult outcome="Passed" testName="synthetic_test" /></Results></TestRun>',
                encoding="utf-8",
            )
            env = {"GITHUB_SHA": "b" * 40, "GITHUB_RUN_ID": "124", "GITHUB_RUN_ATTEMPT": "1"}
            report = evidence_writer.build_evidence(ROOT, needs, trx_dir, env)
        failures = evidence_writer.enforce(report)
        self.assertTrue(any("persons-security-tests' result is failure" in x for x in failures))
        self.assertGreater(report["gateOutcomeCounts"]["FAIL"], 0)

    def test_ci_evidence_manifest_is_traceable_and_redacted(self):
        needs = {job: {"result": "success"} for job in evidence_writer.UPSTREAM_JOBS}
        with tempfile.TemporaryDirectory() as tmp:
            trx_dir = Path(tmp)
            (trx_dir / "sample.trx").write_text(
                '<TestRun><Results><UnitTestResult outcome="Passed" testName="synthetic_test" />'
                '<UnitTestResult outcome="Passed" testName="another_synthetic_test" /></Results></TestRun>',
                encoding="utf-8",
            )
            env = {"GITHUB_SHA": "a" * 40, "GITHUB_RUN_ID": "123", "GITHUB_RUN_ATTEMPT": "1"}
            report = evidence_writer.build_evidence(ROOT, needs, trx_dir, env)
        self.assertEqual("a" * 40, report["commitSha"])
        self.assertRegex(report["schemaFingerprintSha256"], r"^[0-9a-f]{64}$")
        self.assertEqual(len(evidence_writer.UPSTREAM_JOBS), report["gateOutcomeCounts"]["PASS"])
        self.assertEqual(2, report["testCounts"]["total"])
        self.assertEqual(2, report["testCounts"]["passed"])
        self.assertFalse(report["redaction"]["containsRawPersonalData"])
        self.assertFalse(report["redaction"]["containsRequestPayloads"])
        self.assertFalse(report["redaction"]["containsCredentials"])
        self.assertNotIn("private-person-name", json.dumps(report))

    def test_persons_api_route_without_contract_fails_closed(self):
        with tempfile.TemporaryDirectory() as tmp:
            dst = Path(tmp) / "repo"
            shutil.copytree(ROOT, dst, ignore=shutil.ignore_patterns(".git", "bin", "obj", "TestResults"))
            manifest_path = dst / "docs/persons/persons-module-manifest.json"
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            manifest["personsApiImplemented"] = True
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
            endpoint = dst / "src/MDGsm.Api/Endpoints/Persons/PersonsEndpoints.cs"
            endpoint.write_text('app.MapGroup("/api/v1/persons");', encoding="utf-8")
            with self.assertRaises(gates.GateFailure):
                gates.run_api(dst)

    def test_unimplemented_acceptance_criteria_remain_unmapped(self):
        mapping = gates.load_json(ROOT / "docs/persons/acceptance-gate-map.json")
        for item in mapping["criteria"]:
            if item["status"] == "UNMAPPED":
                self.assertIsNone(item.get("testFile"))
                self.assertIsNone(item.get("automatedTest"))
        manifest = gates.load_json(ROOT / "docs/persons/persons-module-manifest.json")
        self.assertFalse(manifest["g1Prerequisites"]["acceptanceCriteriaAc31ToAc49Mapped"])

    def test_g1_cannot_be_authorized_with_unmapped_acceptance_criteria(self):
        with tempfile.TemporaryDirectory() as tmp:
            dst = Path(tmp) / "repo"
            shutil.copytree(ROOT, dst, ignore=shutil.ignore_patterns(".git", "bin", "obj", "TestResults", "__pycache__"))
            manifest_path = dst / "docs/persons/persons-module-manifest.json"
            manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
            manifest["repositoryStage"] = "G1_CONTRACTS"
            manifest["g1Authorized"] = True
            manifest["g1Prerequisites"] = {key: True for key in manifest["g1Prerequisites"]}
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")
            with self.assertRaisesRegex(gates.GateFailure, "UNMAPPED|evidence"):
                gates.run_spec(dst)

    def test_windows_postgres18_job_is_required(self):
        with tempfile.TemporaryDirectory() as tmp:
            dst = Path(tmp) / "repo"
            shutil.copytree(ROOT, dst, ignore=shutil.ignore_patterns(".git", "bin", "obj", "TestResults", "__pycache__"))
            workflow_path = dst / ".github/workflows/persons-module-gates.yml"
            workflow = workflow_path.read_text(encoding="utf-8")
            workflow = workflow.replace("  persons-postgres-windows-migrations:\n", "  persons-postgres-windows-migrations_disabled:\n", 1)
            workflow_path.write_text(workflow, encoding="utf-8")
            with self.assertRaisesRegex(gates.GateFailure, "persons-postgres-windows-migrations"):
                gates.run_spec(dst)

    def test_application_migration_runner_is_required(self):
        with tempfile.TemporaryDirectory() as tmp:
            dst = Path(tmp) / "repo"
            shutil.copytree(ROOT, dst, ignore=shutil.ignore_patterns(".git", "bin", "obj", "TestResults", "__pycache__"))
            (dst / "src/MDGsm.Infrastructure/Persistence/DatabaseMigrationRunner.cs").unlink()
            with self.assertRaisesRegex(gates.GateFailure, "DatabaseMigrationRunner"):
                gates.run_spec(dst)

    def test_workflow_actions_are_sha_pinned_and_runs_are_serialized(self):
        workflow = (ROOT / ".github/workflows/persons-module-gates.yml").read_text(encoding="utf-8")
        action_lines = [line.strip().split("uses:", 1)[1].strip() for line in workflow.splitlines() if "uses:" in line]
        self.assertTrue(action_lines)
        self.assertTrue(all("@" in action and len(action.rsplit("@", 1)[1]) == 40 for action in action_lines), action_lines)
        self.assertIn("concurrency:", workflow)
        self.assertIn("cancel-in-progress: true", workflow)

if __name__ == "__main__":
    unittest.main(verbosity=2)
