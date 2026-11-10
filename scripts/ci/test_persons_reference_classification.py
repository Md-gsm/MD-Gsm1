from __future__ import annotations

import json
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "scripts" / "ci"))
import validate_persons_gates as gates  # noqa: E402


class PersonsReferenceClassificationTests(unittest.TestCase):
    def copy_repo(self) -> Path:
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        destination = Path(temporary.name) / "repo"
        shutil.copytree(
            ROOT,
            destination,
            ignore=shutil.ignore_patterns(".git", "bin", "obj", "TestResults", "__pycache__"),
        )
        return destination

    def read_registry(self, root: Path) -> tuple[Path, dict]:
        path = root / "docs/persons/consumer-registry.json"
        return path, json.loads(path.read_text(encoding="utf-8"))

    def write_registry(self, path: Path, registry: dict) -> None:
        path.write_text(json.dumps(registry, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

    def test_every_partyid_occurrence_has_exactly_one_classification(self):
        gates.run_registry(ROOT)
        _, registry = self.read_registry(ROOT)
        references = [
            reference
            for consumer in registry["registeredConsumers"]
            for reference in consumer["references"]
        ]
        identifiers = [reference["referenceId"] for reference in references]
        self.assertEqual(len(identifiers), len(set(identifiers)))
        self.assertTrue(all(reference["classification"] in {
            "TRANSFER_ACTIVE", "HISTORICAL_SNAPSHOT", "BLOCKING_CONFLICT", "REBUILDABLE_DERIVED"
        } for reference in references))

    def test_missing_classification_fails_closed(self):
        root = self.copy_repo()
        path, registry = self.read_registry(root)
        registry["registeredConsumers"][0]["references"][0].pop("classification")
        self.write_registry(path, registry)
        with self.assertRaisesRegex(gates.GateFailure, "exactly one allowed classification"):
            gates.run_registry(root)

    def test_duplicate_reference_id_fails_closed(self):
        root = self.copy_repo()
        path, registry = self.read_registry(root)
        refs = registry["registeredConsumers"][0]["references"]
        refs[1]["referenceId"] = refs[0]["referenceId"]
        self.write_registry(path, registry)
        with self.assertRaisesRegex(gates.GateFailure, "Duplicate PartyId referenceId"):
            gates.run_registry(root)

    def test_unclassified_source_occurrence_fails_closed(self):
        root = self.copy_repo()
        path, registry = self.read_registry(root)
        registry["registeredConsumers"][0]["references"].pop()
        self.write_registry(path, registry)
        with self.assertRaisesRegex(gates.GateFailure, "coverage differs from source occurrences"):
            gates.run_registry(root)

    def test_invalid_classification_fails_closed(self):
        root = self.copy_repo()
        path, registry = self.read_registry(root)
        registry["registeredConsumers"][0]["references"][0]["classification"] = "TRANSFER_OR_DELETE"
        self.write_registry(path, registry)
        with self.assertRaisesRegex(gates.GateFailure, "exactly one allowed classification"):
            gates.run_registry(root)


if __name__ == "__main__":
    unittest.main()
