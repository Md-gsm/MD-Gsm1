# EVD-FINANCE-INVENTORY-BUNDLE-004 — v0.6
Date: 2026-11-21 | Repository: Md-gsm/MD-Gsm1
Architecture: MDG-ARCH-MASTER-001 v3.0.3 — sole owner-accepted and repository-registered Master.
Status: EXACT-SHA DOCUMENTATION/CI SNAPSHOTS RECONCILED; PACKAGE 5 OWNER ACCEPTANCE AND INDEPENDENT REVIEW PENDING.
Supersedes v0.5 as current status; v0.4/v0.5 remain immutable history.

## 1. Integration state
- Main snapshot: `971194111d49a7ed1f624e3ba64a7d776f729a08`.
- PR #71 merged: `8f89653e25e6a599387128e0209f5009d9dd59cd`.
- PR #72 closed as superseded/contained by #71; not separately merged.
- PR #73 merged: `10c8a8ba8f9aad54c4e2ff05fad49ce12511380f`.
- PR #83 merged: `971194111d49a7ed1f624e3ba64a7d776f729a08`; v3.0.3 registered as sole accepted architecture baseline.
- Prior PR #81 head `1660a3c026a68c7de1f07b080a6804e8d6bc09c5` was based on `b8c672cb70dcb2b274f43f118ac08b2eea8438c5`, four commits behind current main. This commit synchronizes it to current main while preserving the old head as a second parent.

## 2. Current controlled source set on main
| Artifact | Version | SHA-256 |
|---|---:|---|
| Architecture Master | 3.0.3 | `2eac5d75a7834d0ee796ad72a643eb7e7843697b8367fa4281f534c4a63bbd52` |
| Finance Document Register | 1.9 | `5d1da360da641f02b8f6883054cfb0d0ab88a3a848833222d9c1a8180dd76a4d` |
| Integrated Finance 7B/7C/7D | 0.6 | `58765317df4d8ebb89783879fc420d7e2bb10bcd46f87db4f60a81bfa5e1eb1c` |
| Accounting Physical DB Design | 0.4 | `2f3e2bbfca63dab0c9b609c7138cfc2802fef068a05d2f4bda0c1c7e9a4ece41` |
| Inventory Stage 05 Equations | 0.1 | `5730d920ed6ad4a845ef165eb6386a7b8469e2e7098848b8af38aa81431f51e1` |
| Inventory Logical ERD | 0.4 | `04ddbb1d365e9c6d0f2c77460b5a940cbae10bd11563980f580e67c3a3e428e4` |
| Inventory PostgreSQL Physical Design | 0.5 | `3c7b0468831d9ee9827b4de148e882984d7638129bbaab30b509b298ff8d65fc` |
| Inventory Cross-Module Contract | 1.2 | `b95549d2c61408c97a3a8746f5295d4f42e93220f861dc263d55b92b906da2ef` |
| Inventory Source Event Proposal | 0.4 | `12d31f591337801914e6aef07583e21c5210f730c2f6aaf29eb3dcfafab97a95` |
| Canonical Open Issue Register | 1.4 | `7ec791b2915e20c034b55f155cdaf7345f35b8ab4b55918403afb51b58abcc85` |
| Architecture Baseline Registration | 1.0 | `6e7677a39e7ab0e15cd0d1ffa4eb9db0b4ac59d0fc31b14fec2dea1f9072ae64` |

These are SHA-256 entries in current main's `SHA256SUMS.txt`, not Git blob IDs.

## 3. Exact-head CI evidence
- PR #71 head `55e2f508444a09272e2f23d122291deb7630f9cd`: all observed check-runs SUCCESS, including PostgreSQL Linux/Windows, Finance structure, Windows installer and CodeQL. Checksum job `136404700639`: PASS, 372 tracked files.
- PR #73 head `ef48e23b32ee623efe3fdfb8f506c391d139eddb`: all observed check-runs SUCCESS, including PostgreSQL Linux/Windows, Finance structure, Windows installer and CodeQL. Checksum job `136437645211`: PASS, 376 tracked files.
- PR #83 head `e6d948918c2d395d5db7a90a391a7f61603c3219`: 18/18 check-runs SUCCESS. Checksum job `136589796157`: PASS, 404 tracked files. Finance structure job `136589795025`: PASS.
- PR #81 prior head `1660a3c026a68c7de1f07b080a6804e8d6bc09c5`: 18/18 SUCCESS; applies to that prior head only.

The above are historical exact-head results. Full CI and checksum validation must run again on the refreshed PR #81 head; no prior run is represented as a pass for the new head.

## 4. Document-control reconciliation
The original EVD-DOC-06 mismatch (superseded register filename `_v0.8.md` versus internal Version 1.4) is closed by the controlled successor chain and current register v2.0. The later v1.9 defects are corrected in v2.0: the internal version field now matches 2.0, PR #82/#83 merge states are current, and the main snapshot is updated. The parallel OIR v1.4 metadata defects are corrected in OIR v1.5: internal version and PR #82/#83 integration status now match the current record. Prior versions remain immutable history.

No policy or O-item status was altered by these administrative corrections.

## 5. Finance/Inventory boundaries
- Positive accepted Posting Rules: 0; Posting Release NOT GRANTED.
- Finance 7C canonical COA gaps OPEN; Finance 7D runtime tests NOT RUN.
- Inventory PD-T-001..039 are design/test definitions only; runtime tests NOT RUN.
- O-35 remains OPEN pending PostgreSQL watermark/race/replay/tie-out proof; O-64 enforcement/tests remain OPEN.
- FE-08 remains blocked pending Finance/Accounting resolution of WAC pool identity.
- D-15 policy approved, but MAX_CUTOVER_TAIL_COUNT and MAX_CUTOVER_LOCK_DURATION remain NOT_SET; operational Cutover prohibited.
- Migration 0009 RESERVED. No SQL/DDL/EF mapping, grants, roles, runtime/Inventory activation, Posting writer, Posting Release, production migration or Go-Live is authorized.

## 6. Gate status
Architecture baseline acceptance and PR #71/#73/#83 integration: COMPLETE.
Finance Document Register v2.0 and OIR v1.5 metadata reconciliation: COMPLETE.
Refreshed PR #81 exact-head CI/checksum: PENDING.
Independent review of refreshed PR #81 exact head: PENDING.
Whole Package 5 owner acceptance: PENDING; architecture v3.0.3 acceptance is a separate decision.
Final PDF/ZIP rebuild and hashes: PENDING.

Conclusion: candidate evidence is reconciled to current main and v3.0.3; Package 5 is not yet an owner-accepted final release.
**END — EVD-FINANCE-INVENTORY-BUNDLE-004 v0.6**
