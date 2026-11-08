from __future__ import annotations

import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "Configure-MainBranchProtection.ps1"

EXPECTED_REQUIRED_CHECKS = [
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
    "persons-postgres-windows-migrations",
    "project-charter-owner-gate",
]


class MainBranchProtectionPolicyTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.source = SCRIPT.read_text(encoding="utf-8")

    def test_required_checks_match_all_fourteen_live_contexts(self):
        match = re.search(r"\$RequiredChecks\s*=\s*@\((.*?)\n\)", self.source, re.S)
        self.assertIsNotNone(match, "RequiredChecks array must remain explicit")
        actual = re.findall(r"'([^']+)'", match.group(1))
        self.assertEqual(EXPECTED_REQUIRED_CHECKS, actual)
        self.assertEqual(14, len(actual))
        self.assertIn("$PreflightRequiredChecks = @($RequiredChecks)", self.source)

    def test_review_policy_requires_independent_approval_and_code_owner(self):
        self.assertIn("require_code_owner_reviews = $true", self.source)
        self.assertIn("required_approving_review_count = 1", self.source)
        self.assertIn("require_last_push_approval = $true", self.source)
        self.assertIn("-not $verified.required_pull_request_reviews.require_code_owner_reviews", self.source)
        self.assertIn("$verified.required_pull_request_reviews.required_approving_review_count -ne 1", self.source)
        self.assertIn("-not $verified.required_pull_request_reviews.require_last_push_approval", self.source)

    def test_unsafe_review_policy_cannot_reappear(self):
        self.assertNotIn("required_approving_review_count = 0", self.source)
        self.assertNotIn("require_code_owner_reviews = $false", self.source)
        self.assertNotIn("require_last_push_approval = $false", self.source)

    def test_preflight_requires_every_check_to_be_successful(self):
        self.assertIn("foreach ($required in $PreflightRequiredChecks)", self.source)
        self.assertIn("$latest.status -ne 'completed' -or $latest.conclusion -ne 'success'", self.source)
        self.assertIn("Sort-Object { [DateTime]$_.created_at } -Descending", self.source)
        self.assertIn("throw 'Preflight blocked. No protection settings were changed.'", self.source)

    def test_apply_is_opt_in_and_post_apply_readback_is_mandatory(self):
        self.assertIn("if (-not $Apply)", self.source)
        self.assertIn("PREVIEW COMPLETE — no GitHub settings were changed.", self.source)
        self.assertIn("$verified = Invoke-GhJson -Arguments @('api',", self.source)
        self.assertIn("Post-apply verification failed.", self.source)


if __name__ == "__main__":
    unittest.main(verbosity=2)
