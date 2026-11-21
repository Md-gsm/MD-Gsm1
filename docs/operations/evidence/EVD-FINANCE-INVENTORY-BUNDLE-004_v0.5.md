# EVD-FINANCE-INVENTORY-BUNDLE-004 — v0.5
Date: 2026-11-20
Repository: Md-gsm/MD-Gsm1 | Architecture: MDG-ARCH-MASTER-001 v3.0.3
Status: CI/checksum evidence PASS for recorded PR snapshots; Package 5 acceptance, independent reviews and main registration pending.
Supersedes v0.4 as the current status snapshot; v0.4 is preserved historically.

## Exact source snapshots
| PR | Source head | PR merge-ref tested by Actions | State |
|---|---|---|---|
| #71 Accounting + consolidated Architecture/Finance | ab0149a2fb61c2ffe9b08c6447420992a9f2a4f2 | 60398e049cd41e59c442d0dcf449bd0149beaef1 | Open; review pending |
| #72 Architecture/Finance + Inventory Stage 04–06 | 6e797d08c149e7ebce32a5c679c99e16e79a325f | f48c4ae67a851b277a86fc0496f61fedd2914eb4 | Open; contained by #71 |
| #73 Inventory physical-design extension | d59eb170824f2d9750ff62e46168d9051d780cc6 | 6442d2ae4294ef4fc3197addc48c612e28429702 | Open; stacked on #72 |
| #81 prior evidence branch snapshot | 613b892627024b8a27aac8cbf44cc1d978445931 | Evidence snapshot | Open |

Actions logs confirm PR workflows checked out synthetic refs/pull/<n>/merge commits; both source-head and merge-ref SHAs are recorded. Compare confirms #71 is 47 ahead / 0 behind #72, so #71 contains #72. PR #72 must not be separately merged after #71. PR #73 is a separate inventory extension and must be integrated on the post-#71 main head, then checksums, CI and independent review refreshed.

## Exact-SHA workflow results
| PR | Persons gates | Finance structural | Charter gate | Windows installer | CodeQL |
|---|---:|---:|---:|---:|---:|
| #71 | 44754601754 SUCCESS, 13/13 | 44754601638 SUCCESS | 44754602310 SUCCESS | 44754602040 SUCCESS | 44754602161 SUCCESS |
| #72 | 44754064866 SUCCESS, 13/13 | 44754064012 SUCCESS | 44754063726 SUCCESS | 44754064188 SUCCESS | 44754063880 SUCCESS |
| #73 | 44754339786 SUCCESS, 13/13 | 44754339612 SUCCESS | 44754339684 SUCCESS | 44754339555 SUCCESS | 44754339594 SUCCESS |

Checksum verifier PASS: #71 371 tracked files; #72 362; #73 366. All 13 Persons jobs passed on each PR snapshot, including Linux and Windows PostgreSQL migration validation.

## Commit-bound artifacts
- #71: Persons evidence ID 14637591621, SHA-256 e48fc8a3f561b95c8d72bf4b2f8df13655c68580dd53217aebbf8fee737e4d32; foundation tests ID 14636967018, SHA-256 2b86ea2c528e2101cb5920a8870a383b05fa4d9dd78730f08de508c806329470.
- #72: Persons evidence ID 14637571314, SHA-256 6c76df26579885fc71e81f78168cd7721213bffce46a4cc72a83e1b3df3bb9ad; foundation tests ID 14636213662, SHA-256 b9d659fbd21ef4bf6297e9a9d9adae615d88cea7b7be17577d1c4941b5f25580.
- #73: Persons evidence ID 14636754750, SHA-256 0c5e41914adff9cb3fb809c76d593bae5543ff67e4ff0470b722c6c38ddb5a6b; foundation tests ID 14637245669, SHA-256 3963e1245b10f9879fa7e1c7c8b05aef3f4a9b47ca8ed4fd832322c81ff426b2.

## Document-control reconciliation
EVD-DOC-01 (7D header/authority), DOC-02 (Register active 7C/Gate 2 authority), DOC-03 (Stage Execution Report controlling authority correction), DOC-04 (Accounting PDS v0.3), and DOC-05 (PR #71 Draft metadata/title) are CLOSED on current source snapshots. Historical statements are retained and explicitly superseded by controlling overlays/revisions.
EVD-DOC-06 remains OPEN: the Document Register path suffix is _v0.8.md, while its internal controlled document header says Version 1.4. This is an administrative naming mismatch; the internal authority hierarchy is explicit, but the alias needs controlled correction or owner disposition.

## Acceptance boundary
- Independent exact-SHA APPROVED reviews on #71, #73 and #81: PENDING.
- Architecture v3.0.3 owner acceptance is recorded; whole Package 5 owner acceptance is NOT recorded.
- Finance 7B: 0 accepted positive Posting Rules; 7C canonical attribute gaps OPEN; 7D runtime tests NOT RUN.
- Inventory PD-T-001..039 are specifications only; O-35 OPEN pending PostgreSQL watermark/concurrency/replay/tie-out evidence.
- Migration 0009 RESERVED; SQL/DDL, schema migration, COA seed, Posting writer, Posting Release and Go-Live NOT AUTHORIZED.
Conclusion: exact-snapshot CI and checksum evidence PASS. This does not equal runtime acceptance or final main release.
