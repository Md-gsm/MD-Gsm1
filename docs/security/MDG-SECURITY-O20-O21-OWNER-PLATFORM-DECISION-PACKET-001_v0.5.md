# O20 / O21 Remaining Owner and Platform Decision Packet

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 |
| Version / Date | 0.5 / 2026-11-23 |
| Supersedes | v0.4 |
| Supersedes | v0.2 |
| Supersedes | v0.1 |
| Status | RECOMMENDATIONS FOR EXPLICIT OWNER / PLATFORM DECISION — NONE ACCEPTED BY THIS DOCUMENT |
| Repository / Branch | Md-gsm/MD-Gsm1 / security/o20-o21-final-closure |
| Current package | PR #92 Draft / unmerged |

## 1. Purpose and decision boundary

The current branch has executable disposable PostgreSQL proofs for bounded O20-E policy cases, the actual IdentityAccess transaction-scoped authorization guard, outbox/idempotency/lock-order candidates, worker object-level restrictions and the O21 hash-chain PoC. These tests do not constitute production Organization, AuditSecurity, WORM/KMS, operational or G1 acceptance.

This packet records remaining decisions and recommends a least-privilege direction. Recommendations are not accepted requirements until the accountable Product Owner, Platform and module owners explicitly approve them. Nothing here authorizes DDL, migration, production identity/credential/grant, cloud resource, SMTP, key material, service activation, Organization runtime or G1.

## 2. DP-01 — Product Owner binding and cardinality

**Recommendation:** exactly one active Product Owner principal binding per installation/system scope. The binding identifies one exact AccountId and a monotonic BindingVersion. It is not a role, username, email, GitHub identity, local OS administrator claim, caller flag or ordinary super_admin permission.

- Initial designation occurs only in an approved attended bootstrap ceremony after the initial administrator account is securely established and first-login credential/profile change is complete.
- The first administrator is not automatically Product Owner. The ceremony must explicitly designate whether that account is also Product Owner.
- An ordinary super_admin without the exact active protected binding is NotOwner.
- The bound Product Owner direct path is limited to Organization Branch/Warehouse assignment create/change/revoke, including self-assignment, without a second approver.
- Owner binding enrollment, transfer/change, recovery, revocation, emergency disable and restoration use a separate independent-control ceremony. The assignment-command exception does not apply to owner-binding lifecycle.
- For change/recovery, require two independent custodians/verifiers distinct from the initiating actor and proposed new owner. Preserve prior binding history.
- Binding unavailable, stale, pending, revoked or version-inconsistent always returns Unavailable/NotOwner and fails closed.

**Owner decision required:** accept or amend cardinality, first designation, independent custodians, change/recovery process, installation scope and accountable data owner (IdentityAccess vs Platform). No binding table, seed, route or authority is created until accepted.

## 3. DP-02 — Bootstrap trust boundary and one-time provisioning

**Recommendation:** attended local installation/setup ceremony, with ordinary network listener and all business routes disabled until bootstrap completion.

Candidate safe sequence:
1. A trusted installer/setup process creates one bootstrap enrollment transaction bound to a unique installation identifier.
2. A CSPRNG generates at least 256 bits of one-time bootstrap secret material. Show it once through the approved local setup UI; do not put it in command-line arguments, environment variables, files, installer logs, telemetry, crash dumps, source, CI artifacts or database plaintext.
3. Store only a verifier/digest using a separately provisioned, versioned secret/key mechanism; never store or recover the raw secret.
4. Apply a short owner-approved expiry (30 minutes recommended), single-use claim and bounded attempts. Serialize the claim transactionally and reconcile unknown commit via a stable operation ID.
5. Constrain the account to the approved initial administrator role. First login must force username/password/profile completion. Enable listener and ordinary routes only after durable completion is verified.
6. Record bootstrap completion and owner designation with secret-free audit evidence.
7. Missing DB state, replay, expired token, installation mismatch, key-service outage or ambiguous result fails closed.

The local-console trust assumptions and OS administrator threat must be documented. A local installer is not trustworthy merely because it runs on the same host. No static password or reusable master secret is permitted.

**Owner/Platform decisions required:** local setup trust boundary, installation binding, operator identity/authority, bootstrap expiry, delivery UI, verifier mechanism, and whether installer may explicitly designate the Product Owner. This does not initiate live bootstrap.

## 4. DP-03 — Email-only Recovery

The email-only OTP policy is already owner-accepted for this phase and remains unchanged: 600-second TTL, one-time use, dedicated HMAC key outside PostgreSQL, bounded attempts/send quotas and 24-hour lock, generic enumeration-resistant responses, no SMS and no offline recovery codes. This is not MFA.

**Recommendation:** after authoritative verified-email enrollment exists, expose one HTTPS Recovery endpoint under IdentityAccess host/session/rate-limiting/monitoring controls; return generic responses and never reveal account existence. Do not create a local file or unauthenticated desktop shortcut as hidden recovery bypass. A successful proof atomically changes password, increments credential version, revokes all sessions, requires password change and consumes the challenge.

**Owner/Platform decisions required:** initial email enrollment/verification, Recovery endpoint threat boundary, SMTP provider/relay, TLS/authentication, provider failure handling, dedicated HMAC key service, rotation/recovery and WG-0 migration/schema reservation. Migration 0009 remains reserved and must not be consumed without WG-0 approval.

## 5. DP-04 — Outbox worker identity and unchanged N.6 baseline

The accepted N.6 five logical identity categories and separate NOLOGIN schema owner remain unchanged. The disposable worker proof tests a candidate outbox_worker LOGIN with SET-only membership to a narrow NOLOGIN ACL role; it can read immutable event/delivery data and update only delivery metadata columns. It cannot mutate payload/hash/event ID, delete rows, access Branch/Warehouse or IdentityAccess credential data, perform DDL or assume other roles.

**Recommendation:** approve a dedicated sixth logical service identity for an O21/AuditSecurity outbox worker, physically implemented as a unique LOGIN principal with SET-only membership to a narrow NOLOGIN ACL role and column-scoped grants. Keep it distinct from interactive runtime and the accepted five categories. If not approved, keep production delivery disabled; do not broaden runtime or reuse an interactive credential.

**Owner/Platform/DB/Security decisions required:** approve/reject the sixth logical identity, name/owner, secret and pool binding, target database, exact object scope, rotation/revocation, service authentication and production audit. Nothing is provisioned by this recommendation.

## 6. DP-05 — Platform identifier standard

**Recommendation:** native PostgreSQL uuid storage and .NET Guid.CreateVersion7() for new BranchId, WarehouseId, AssignmentId, relationship-history and event/idempotency identifiers where an opaque generated UUID is appropriate. The codebase already uses this standard for PartyId and AccountId.

- Generate only in trusted server-side code.
- Preserve typed identifiers per aggregate even where storage is UUID.
- Keep EventId/idempotency receipt stable across retry and unknown-commit reconciliation.
- UUIDv7 timestamp bits are a locality hint only, never authorization evidence, commit order, sequence/watermark or causality proof.
- Clock rollback/skew and multi-instance generation require explicit tests; if ordering is needed, use a separate database-owned sequence/commit fact.

**Platform decision required:** accept UUIDv7 or select another standard; define supported .NET/runtime implementation, distributed deployment, clock behavior, compatibility and cross-language conformance. Existing IDs are not rewritten.

## 7. DP-06 — Platform global lock order and authorization linearization

IdentityAccess contract v1.2 provides a transaction-scoped authorization guard. The disposable PostgreSQL proof demonstrates actual account/session/membership/role/grant/permission mutations wait behind FOR SHARE locks until the supplied transaction ends. This is not a total cross-module order.

**Recommendation for Platform review:** publish a single total resource-rank registry covering command/idempotency receipt, IdentityAccess authorization facts, maker-checker approval, Branch aggregates, Warehouse aggregates, assignment aggregates, append-only history and local audit outbox. Same-rank resource IDs are sorted deterministically in binary UUID order. The exact ranks must be proven against actual multi-aggregate operations and affected modules before acceptance.

Mandatory races: session/account/permission revoke vs protected write; approval and assignment mutation vs duplicate/altered request; Branch deactivate/archive vs Warehouse reassignment; Warehouse reassignment vs exact-Warehouse assignment create/revoke; same-key/same-payload and same-key/different-payload; reversed-order multi-resource writers; lock timeout, deadlock, cancellation, crash-before-commit, unknown commit and bounded replay; outbox delivery/reconciliation without holding domain locks across network IO.

**Platform and affected module-owner acceptance required:** total ranks, SQL isolation, timeout/deadlock/retry policy, cancellation and machine-readable PostgreSQL evidence. Until accepted, the transaction guard must not be wired into an Organization production write path.

## 8. DP-07 — O21 external trust infrastructure

Owner-approved O21 design specifies SHA-256, RSA-3072/RSA-PSS and an external Object Lock Compliance (or demonstrably equivalent) anchor. Disposable PoC tests use ephemeral test keys and local temporary checkpoint storage.

Platform must choose an actual deployment provider/service and region for non-exportable signing keys/HSM/KMS; name independent custodians; provision key backup/recovery, rotation/revocation and trust-epoch process; choose an independently administered Object Lock Compliance account or equivalent; approve retention duration, checkpoint cadence, Legal Hold, privacy/export and recovery policy; and assign verifier deployment, alert recipient, on-call owner and runbook.

No provider, region, bucket, key, duration, custodian or secret is guessed here. Provider-neutral interfaces and disposable PoC do not substitute for real provider evidence. Production O21 cannot be closed until an actual immutable anchor and key custody are proven.

## 9. DP-08 — Records retention and Legal Hold

OD-05 owner policy is accepted: retain assignment and Branch/Warehouse relationship history by default; no physical deletion or automatic purge; disposal only after the applicable approved retention period, no active Legal Hold and documented records/legal authorization. The duration remains intentionally UNSET.

Records/legal authority must decide jurisdiction and record-class schedule; authority to place/change/release Legal Hold; hold scope; backup/replica/export handling; disposal authorization and separation of duties; and privacy/data-subject treatment. Until then deletion/purge/anonymization/cryptographic erasure remain disabled. No duration is guessed.

## 10. Required sequence and release gates

| Sequence | Accountable owner | Required output | State |
|---:|---|---|---|
| 1 | IdentityAccess/Product Owner | Accept public Contracts v1.2 semantics and transaction guard | Source published; module-owner review requested, not yet accepted |
| 2 | Platform + affected module owners | Accept global lock-order/linearization registry and full race matrix | Proposal only |
| 3 | Product Owner + IdentityAccess + Platform + Security | Accept Product Owner binding/cardinality and trusted bootstrap | Proposal only |
| 4 | Product Owner + Platform + DB + Security | Decide extra outbox worker identity vs disabled alternative | Proposal only; N.6 five remain unchanged |
| 5 | Platform | Accept UUIDv7 (or alternative) and generator conformance | Recommendation only |
| 6 | Platform + Security + AuditSecurity owner | Select/provision KMS/HSM, WORM/Object Lock, verifier and operations | No provider/resources provisioned |
| 7 | Records/legal authority | Approve retention schedule and Legal Hold implementation | OD-05 baseline accepted; legal schedule OPEN |
| 8 | WG-0 + module owners | Reserve Organization schema/migration and authorize G1 implementation | Not reserved; G1 NOT GRANTED |
| 9 | Independent Security/Architecture reviewer | Review one exact final SHA, findings and retests | Review requested on PR #92; approval not received |
| 10 | Product Owner | Explicit G1 decision after all evidence | NOT GRANTED |

## 11. Current non-authorization

This packet does not approve its recommendations. It creates no Product Owner binding, Bootstrap/Recovery route, SMTP/key, worker identity/credential, UUID standard, global lock order, KMS/HSM/WORM resource, Legal Hold schedule, Organization module/schema/migration, production grants or G1.

**END — MDG-SECURITY-O20-O21-OWNER-PLATFORM-DECISION-PACKET-001 v0.1**

## 12. DP-09 — Iran records-retention legal classification and schedule

**Current research finding:** the Iran-source crosswalk is recorded in `MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.4`. It identifies several non-uniform rules:
- Commercial Code Article 13: ten years from year-end for the statutory Article 6 commercial books, subject to the small-merchant exception in Article 6 and its classification rule in Article 19.
- AML Executive Regulation Article 142 (amended 1404/07/27): ten years for specified records held by financial institutions; Article 149 extends the listed AML obligations to specified non-financial professions/activities. A general mobile retail/repair shop is not expressly named in the categories reviewed; any residual high-risk designation must be confirmed from an effective Council decision.
- VAT Executive Regulation Article 103: the reviewed unofficial translation states five years for ordinary taxable records from the end of the relevant tax period, with separate capital-asset and real-estate rules and a possible written extension.

This is a legal research crosswalk, not a legal opinion or a record-class decision. In particular, no blanket AML 10-year rule is assumed for mobile shops, no assumption is made that every operator is subject to the same Commercial Code books, and VAT Article 103 does not by itself classify Organization assignment history.

**Recommendation:** keep the already accepted OD-05 fail-closed baseline in force. Ask the accountable Iran legal/tax authority to classify the operator and each record class, then return a schedule containing (a) record class, (b) legal basis and effective version, (c) trigger date, (d) minimum retention period, (e) any extension/hold conditions, (f) Legal Hold authority/scope/release, (g) backup/replica/export treatment and (h) disposal authorizer. Do not adopt a numeric Organization-history duration until that schedule is approved.

**Decision required:** Product Owner to nominate the accountable legal/records authority and obtain an approved schedule. This document does not authorize deletion, purge, anonymization, key destruction, production retention worker, schema or G1.

**Status:** source crosswalk COMPLETE; legal classification/schedule and Legal Hold operating model OPEN.

**END — O20/O21 decision packet v0.2**


## 13. Exact-head validation and consolidated owner decision request — 2026-11-23 (v0.3)

The exact branch head `83b8bf35345b14faf6ce549d80640180aca1c28e` passed the current complete CI set: primary workflow 13/13, CodeQL, Windows Preview Installer, Project Charter Owner Gate, Release build 0 warnings/0 errors, .NET 243/243, PostgreSQL 18 61/61, Linux/Windows migration idempotence and 683-file checksum verification. A new DI fail-closed registration test is pending on successor head `6a07f13fb8cfe00ddf317acf7e86157055eebeed`.

The technical work now has bounded executable disposable evidence for the listed local security invariants. The remaining gates require explicit external authority rather than more speculative code. The following recommendation set is consolidated for one accountable owner decision:

| Decision | Recommended direction | Why it is safe | What stays blocked until separately proven |
|---|---|---|---|
| DP-01 Product Owner binding | Exactly one active bound AccountId per installation; owner-binding transfer/recovery requires two independent custodians/verifiers distinct from initiator and proposed owner | Narrow, unambiguous authority; does not infer owner from role | Enrollment/bootstrap, binding store, recovery, emergency disable and restore |
| DP-02 bootstrap | Attended one-time enrollment, 256-bit CSPRNG secret, capture-once display, digest only at rest, short expiry and single-use; business listener disabled until first-login setup is completed | No static password or unattended default credential | Trusted installer/local-console channel and Platform-provisioned key/secret service |
| DP-04 outbox worker | Keep production delivery DISABLED until AuditSecurity public API and worker identity are accepted; then provision a dedicated sixth logical service identity with the already-proved narrow worker ACL | Preserves N.6 five-identity baseline and prevents runtime credential expansion | No production worker/secret/grant until identity and service are approved |
| DP-05 identifiers | Platform standard: PostgreSQL native UUID; .NET Guid.CreateVersion7() for new opaque aggregate/event identifiers; never use UUID ordering as commit chronology | Aligns with existing PartyId precedent while preserving timestamp/sequence as separate facts | Platform acceptance and clock/multi-instance/rollback conformance |
| DP-06 lock order | Accept only after Platform and every affected module owner approve the full global rank and race matrix; until then transaction guard remains unused by production cross-module writes | Avoids unilateral, unsafe global rank inference | No Organization production UoW |
| DP-07 O21 provider | Do not infer provider/account/region/retention/custodians. Product Owner/Platform must name actual KMS/HSM and independently administered Object Lock Compliance service and authorize provisioning | Prevents creating unapproved cloud resources or secrets | Production signing, WORM, verifier, restore and operations |
| DP-09 Iran records retention | No numeric period for Organization assignment history until Iran counsel/records authority classifies entity and record class; keep OD-05 no-auto-delete | Crosswalk shows Commercial, AML and VAT scope is not identical | Legal schedule and Legal Hold operations |

### 13.1 Explicit decision boundaries

Accepting this recommendation set does **not** by itself:
- identify a particular human as the Product Owner;
- grant G1, Organization runtime, migration or production permissions;
- select a cloud provider/region or authorize a paid resource;
- appoint a legal authority or declare a statutory retention period;
- approve production AuditSecurity delivery or provision the sixth worker identity;
- replace independent review, WG-0 reservation or required module-owner acceptance.

The module owner must still accept the IdentityAccess public contract; Platform/data owners must still accept total lock order; AuditSecurity must still accept canonical event/ACK semantics; independent Security/Architecture review and explicit G1 remain separate release gates.

**Disposition:** exact technical CI at 83b8bf… PASS within disposable scope; one DI regression test pending on 6a07f13…. Decision packet v0.3 is a recommendation request only; no recommendation is treated as accepted. O20-E/O-20/O-21 OPEN; G1 NOT GRANTED.

**END — O20/O21 decision packet v0.3**


## 14. DP-05 evidence update — UUIDv7 implementation conformance PoC (2026-11-23; v0.4)

The branch now includes Platform Identifier Policy Request v0.2 and two bounded tests:
- AccountId/PartyId UUIDv7 conformance, timestamp encoding, same-millisecond sample, pre-epoch rejection and 20,000 parallel in-process generation checks;
- 512 UUIDv7 values stored and retrieved with PostgreSQL native `uuid` in a marked disposable database, with exact Guid identity/version/variant comparison.

These results are designed to inform DP-05. They do not choose the Platform-wide ID standard. They do not prove multi-host/clock rollback behavior, collision bounds under production load, cross-language support, mixed-version migration, database index performance or restore continuity. A Platform owner must still accept RFC 9562 UUIDv7 / .NET `Guid.CreateVersion7()` as the standard or select an alternative and define the cross-runtime contract.

**Exact-head state:** the last complete validation at 83b8bf… predates these tests. The updated source/doc/checksum head 0c25437… has a fresh CI run in progress; no PASS is claimed until the exact PostgreSQL output is inspected.

**Disposition:** DP-05 PoC scope expanded; Platform standard remains OPEN. No Organization runtime or ID generator service was introduced and no production IDs or schema were changed.

**END — O20/O21 decision packet v0.4**


## 15. DP-09 legal-source crosswalk correction — 2026-11-23 (v0.5)

The controlling records-retention crosswalk is `MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.6`.

Current sources distinguish:
- VAT Executive Regulation Article 103: ordinary records five years from Tax Period end, capital-asset records five years after the adjustment-period Tax Period, real-estate records fifteen years from Tax Period end, with up to five additional years after prior notice. The retrieved bilingual PDF marks its English translation unofficial; the Persian source must be verified.
- VAT Act 1400 Article 55 repeals the 1387 VAT Act's provisions subject to listed exceptions, so the old ten-year Article 34 must not be treated as a current blanket VAT rule.
- Commercial Code Article 13's ten-year rule concerns the defined statutory books under Article 6 and depends on the Article 19 small-trader classification.
- AML Law Article 7 provides a five-year minimum for legally covered persons and preserves longer duties; the 1404 AML Executive Regulation amendments to Articles 149 and 158 require activity/entity-specific interpretation. Article 142's ten-year records obligation is not generalized from financial institutions to every operator or Organization row.

These are distinct legal classifications and triggers. None of them on its own decides that Organization assignment/Branch/Warehouse history is a VAT book, commercial book or AML record. The actual operator's legal form, activities, tax/AML status, record class, triggering event, extensions and Legal Hold rules must be classified by Iran legal/tax authority.

**DP-09 recommendation remains unchanged:** do not select a numeric retention period for Organization history until the accountable Iran counsel/records authority accepts a written record-class schedule. Keep OD-05 retain-by-default/no-auto-delete. This packet does not make that legal decision.

## 15.1 Exact technical evidence caveat

The latest completed exact test head 014b559… showed 249/249 .NET tests, 62/62 Linux PostgreSQL tests, UUIDv7 PostgreSQL round-trip PASS, Linux migration idempotence 8/0, 696 checksum matches, CodeQL/Windows Preview/Owner Gate SUCCESS; the required Windows PostgreSQL migration job was CANCELLED and the all-required-gates evidence step failed accordingly. A rerun was requested, and newer cross-document updates require another exact-head full run. No complete final CI PASS is claimed yet.

**Disposition:** DP-09 still requires legal authority; no numeric period selected. O20-E/O-20/O-21 OPEN; G1 NOT GRANTED.

**END — O20/O21 decision packet v0.5**
