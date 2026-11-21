# EVD-PACKAGE-05-FINAL-RELEASE-001 — v0.1
Date: 2026-11-20 | Repository: Md-gsm/MD-Gsm1 | Baseline: MDG-ARCH-MASTER-001 v3.0.3
Status: RELEASE CANDIDATE ONLY — owner acceptance, independent review and controlled main merge pending.
Related evidence: EVD-FINANCE-INVENTORY-BUNDLE-004 v0.5.

## Scope
Architecture v3.0.3; Finance 7B v0.2 / 7C v0.4 / 7D v0.2 and integrated workpapers; Accounting Physical Database Design v0.3; Inventory Stage 05 equations v0.1, logical ERD v0.2, PostgreSQL Physical Design v0.3 and re-audit 003. Design/evidence package only, not implementation or production release.

## Exact source and CI snapshots
- PR #71 source ab0149a2fb61c2ffe9b08c6447420992a9f2a4f2, tested merge-ref 60398e049cd41e59c442d0dcf449bd0149beaef1. Runs: Persons 44754601754; Finance 44754601638; Charter 44754602310; Windows 44754602040; CodeQL 44754602161. All SUCCESS; Persons 13/13.
- PR #72 source 6e797d08c149e7ebce32a5c679c99e16e79a325f, tested merge-ref f48c4ae67a851b277a86fc0496f61fedd2914eb4. Runs: Persons 44754064866; Finance 44754064012; Charter 44754063726; Windows 44754064188; CodeQL 44754063880. All SUCCESS; Persons 13/13.
- PR #73 source d59eb170824f2d9750ff62e46168d9051d780cc6, tested merge-ref 6442d2ae4294ef4fc3197addc48c612e28429702. Runs: Persons 44754339786; Finance 44754339612; Charter 44754339684; Windows 44754339555; CodeQL 44754339594. All SUCCESS; Persons 13/13.

Git compare: #71 is 47 ahead / 0 behind #72; #71 contains #72 plus Accounting changes. Merge #71 first; close #72 as superseded, not separately merged. PR #73 is separate and must be integrated atop post-#71 main, then SHA256SUMS and CI/review evidence refreshed.

## Source SHA-256 checkpoints
- Architecture v3.0.3: 2eac5d75a7834d0ee796ad72a643eb7e7843697b8367fa4281f534c4a63bbd52
- Finance 7B v0.2: 0cb1752e8daa2cd5059f6a6032ebd11128096ba2c4d3c8fa4c7a30e110d7920f
- Finance 7C v0.4: 9dfad17869513572df6e9a6847469d89627f96624a1ec585828a36c0673e8c42
- Finance 7D v0.2: d9b177e9480bb963781fdd608daac21348e2ddaab512178c41d83dd984fa502a
- Document Register file v0.8 / internal Version 1.4: fcac54322db469a572ce221e5660ef3cdac252e7a0815a363cdb166e8facc7e2
- Stage Execution Report v0.2: 4280739f73284379e8a10836f7656299eb243a733f923d3cc6ad4d50c07e9dfd
- Integrated 7B/7C/7D v0.6: 58765317df4d8ebb89783879fc420d7e2bb10bcd46f87db4f60a81bfa5e1eb1c
- Open Issue Register v1.0: 26efd4e0e52c498ba1ad43d1b29491d813b2fa6ec35f2901af324d3a7bd9e105
- Accounting PDS v0.3: ad2043715aeb8a7facf71ce7123d8db1d01b34335ca645c7a52a54d9c7fa55d3
- Inventory Stage 05 v0.1: 5730d920ed6ad4a845ef165eb6386a7b8469e2e7098848b8af38aa81431f51e1
- Inventory ERD v0.2: 6f4771d16eb1021cc1200b94279a3804ccfe65a9552ef7e5b9830f90841b66fc
- Inventory physical design v0.3: c14fc7ec96f1d5f5f692688aacf8a2fa7fe9d046a50d93e1f56e0995f29ceb4b
- Inventory Re-audit 003: 0bd6f2032a218d0a852fbc7983141268b45d22a51364167aa03eba305a37b025

## Current acceptance state
- PR #71, #73 and #81 independent exact-SHA approval: PENDING.
- Package 5 owner acceptance: PENDING (architecture v3.0.3 acceptance is a distinct prior decision).
- EVD-DOC-06 OPEN: Document Register filename _v0.8.md vs internal Version 1.4.
- Main at snapshot: b8c672cb70dcb2b274f43f118ac08b2eea8438c5; no source PR merged during candidate preparation.

## Controlled merge sequence
1. Independent review #71 exact head; close findings. Obtain explicit owner acceptance of Package 5 and disposition EVD-DOC-06.
2. Merge #71 to main under branch controls; close #72 as superseded/contained.
3. Integrate #73 on the new main head preserving both histories; update SHA256SUMS; rerun CI; obtain review on the new exact SHA.
4. Merge #73 only after green checks, approval and mergeability.
5. Publish final evidence with actual merge SHAs and final main SHA; run final evidence CI; merge evidence PR last.

## Non-authorizations
Finance 7B has zero positive accepted Posting Rules; 7C canonical gaps remain open; 7D runtime tests NOT RUN. Inventory PD-T-001..039 NOT RUN; O-35 OPEN. Migration 0009 RESERVED; SQL/DDL, EF mapping, grants, COA seed, Posting writer, Posting Release, production migration and Go-Live NOT AUTHORIZED.
Decision: Package 5 candidate assembled; source SHA/checksum and listed CI evidence PASS. Not OWNER-ACCEPTED or registered in main. Replace with a final post-merge revision only after all approvals and controlled integration.
