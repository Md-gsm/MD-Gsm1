# O20-E Organization Assignment History — Records Retention and Legal Hold Policy

| Field | Value |
|---|---|
| Document ID | MDG-SECURITY-O20E-RECORDS-RETENTION-001 |
| Version / Date | 0.5 / 2026-11-22 |
| Supersedes | v0.4 |
| Supersedes | v0.3 |
| Status | OWNER-ACCEPTED RETENTION PRINCIPLES; IRAN LEGAL-SOURCE CROSSWALK EXPANDED; RECORD-CLASS CLASSIFICATION / HOLD OPERATIONS / PRODUCTION ENFORCEMENT OPEN |
| Authority | Explicit Product Owner approval recorded in the conversation on 2026-11-22; no digital signature is claimed |
| Governing architecture | MDG-ARCH-MASTER-001 v3.0.3 |
| Related policy | MDG-SECURITY-O20E-DATA-OWNERSHIP-001 v0.5 |
| Related design | MDG-ORGANIZATION-ASSIGNMENT-SCOPE-DESIGN-001 v0.5; MDG-ORGANIZATION-ASSIGNMENT-SCOPE-PHYSICAL-DESIGN-001 v0.5 |

## 1. Accepted policy decision

The Product Owner accepted the following default retention and disposal policy for Organization-owned Branch/Warehouse and AccountBranchAssignment/AccountWarehouseAssignment history:

1. Assignment and relationship lifecycle history is retained by default. Physical deletion is not the normal lifecycle operation.
2. No automatic expiry, purge, hard-delete, anonymization, irreversible masking, or cryptographic-erasure job may run until an applicable retention schedule and lawful disposal conditions have been explicitly approved and implemented.
3. Disposal may be considered only after the applicable legally required retention period has elapsed, there is no active Legal Hold covering the record, and a documented authorization has been recorded by the accountable records/legal authority under the approved disposal procedure.
4. A Legal Hold overrides ordinary retention expiry and blocks disposal for its covered records. A hold must remain effective until an authorized release is recorded.
5. The exact retention duration is intentionally **UNSET**. No number of years, months, or days is inferred by this decision. Until the owner/legal authority approves the schedule, automatic deletion remains disabled and the history remains retained.
6. This decision establishes a conservative policy baseline; it does not assert legal clearance, select a jurisdiction-specific statutory period, or authorize disposal.

## 2. Records in scope

The policy applies to Organization-owned historical evidence for:
- Branch/Warehouse master lifecycle transitions;
- Warehouse-to-Branch relationship changes and their historical intervals;
- AccountBranchAssignment and AccountWarehouseAssignment creation, change, approval, rejection and revocation;
- the actor, subject, approver, reason, effective time, aggregate/version, idempotency and correlation references needed to reconstruct the authorized state transition;
- associated local audit-intent/outbox delivery state to the extent needed for reconciliation and proof of canonical AuditSecurity acknowledgement.

The policy does not transfer ownership of canonical shared AuditEvent or SecurityEvent from AuditSecurity to Organization. Organization may own domain history for its aggregates; AuditSecurity remains the canonical shared audit/security-event owner.

## 3. Data minimization and separation

Retained history must contain only the minimum identifiers and decision metadata required to reconstruct and audit the Organization lifecycle. It must not include passwords, credential hashes, bearer/session tokens, recovery secrets, or copied IdentityAccess private records. AccountId remains an opaque reference. Free-text reasons must be subject to the approved data-minimization and secret/PII controls.

No duplicate canonical security-audit ledger may be introduced under the label of retention history.

## 4. Legal Hold controls

The implementation design must define:
- who may place, change, release and review a Legal Hold;
- authenticated authority, reason, scope, timestamps, version and audit evidence for each hold operation;
- how holds cover current records, historical versions, outbox evidence, replicas, exports and backup media where applicable;
- how conflicting holds and retention expiries are resolved;
- how the system proves that a held record was not disposed of.

Until those controls are approved and implemented, hold-related uncertainty must fail closed for disposal: the record is retained.

## 5. Lawful disposal controls

A future disposal workflow must, at minimum, verify all of the following before any destructive operation:
1. an approved, versioned retention schedule applies to the exact record class and jurisdiction;
2. the required retention period has elapsed using a trusted time source;
3. no active or unresolved Legal Hold covers the record;
4. the accountable records/legal authority has issued a documented, independently attributable authorization;
5. the disposal operation is scoped, idempotent, concurrency-protected and fully audited;
6. the disposal evidence is retained under its own applicable schedule without retaining the disposed protected content;
7. required primary, replica, export and backup dispositions are handled under separately approved controls.

No purge worker, hard-delete path, anonymization rule, backup pruning rule, or key-destruction workflow is authorized by this policy.

## 6. Unresolved legal and technical details

The following remain OPEN and must not be guessed:
- statutory/contractual retention duration by record class and applicable jurisdiction;
- the records/legal authority and required separation of duties for approving disposal;
- Legal Hold scope taxonomy, triggers, release controls and review interval;
- treatment of backups, immutable external audit evidence, replicas and exported artifacts;
- privacy/data-subject obligations and the lawful treatment of opaque AccountId references;
- technical enforcement, evidence format, operational alerts, recovery and independent test cases.

A duration or disposal rule becomes effective only through a controlled successor approved by the accountable authority. A later schedule must not weaken an active Legal Hold or retroactively authorize an earlier unapproved purge.

## 7. Acceptance tests required before implementation

- no physical-delete or automatic-purge path is available by default;
- expired/unknown retention duration does not trigger deletion;
- active, overlapping or unresolved Legal Hold blocks disposal;
- untrusted or self-authorized disposal requests are denied;
- missing legal schedule, missing authorization, unavailable hold resolver or audit failure blocks disposal;
- concurrent hold placement and disposal obey a documented serialization/linearization rule;
- disposal is idempotent, scoped, version-checked and produces durable audit evidence;
- backup/replica/export handling is proved against the separately approved media policy.

## 8. Authority and disposition

This is an owner-accepted policy baseline, not a completed technical implementation. It does not approve G1, Organization runtime, DDL, migrations, grants, production access, deletion, or Go-Live.

**PD-ORG-11 core retention policy: OWNER-ACCEPTED.** Retention schedule, Legal Hold implementation, lawful disposal workflow, technical enforcement and proofs remain OPEN. **O20-E remains OPEN; overall O-20 remains OPEN.**

**END — MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.1**


## 9. Fail-closed Legal Hold eligibility PoC — 2026-11-22 (v0.2)

Added disposable integration test:
tests/MDGsm.Domain.Tests/Security/O20OrganizationLegalHoldRetentionPostgresProofTests.cs

The test defines only an isolated eligibility predicate; it performs no deletion, masking, anonymization, cryptographic erasure or purge. It verifies that eligibility is denied when:
- the retention schedule is UNSET, missing, not approved or lacks approver evidence;
- the retention period has not elapsed;
- the Legal Hold resolver is unavailable;
- a hold is ACTIVE, PENDING or UNKNOWN;
- disposal authorization is absent, expired, consumed, schedule-version-mismatched or not independently approved;
- the actor and approver are the same;
- records/legal authority or a reason is missing.

The positive control is a synthetic fixture with an approved test schedule, elapsed synthetic date, available hold resolver, no unresolved holds and a distinct synthetic records/legal authorizer. The function only returns eligibility; it does not destroy a record and does not choose a real statutory schedule.

A second PostgreSQL test establishes a competing hold-vs-disposal-decision serialization rule in the fixture: hold placement obtains the protected record row lock; the eligibility decision waits behind that lock and, after the hold commits, returns DENY. This is a proposed proof of the fail-closed ordering requirement, not a production Hold service.

A dedicated explicit CI step has been added. Exact current-head compilation and PostgreSQL output are PENDING until the workflow on the resulting commit completes.

## 10. Unchanged legal and production boundary

The retention duration remains UNSET; no jurisdiction or legal interpretation has been selected. The accepted baseline remains retain-by-default, no automatic deletion and no physical deletion absent approved applicable schedule, no active/unresolved Legal Hold, and documented authorization from the accountable records/legal authority.

This test does not implement a production retention schedule, Legal Hold authority/resolver, hold review/release, backup/replica/export coverage, disposal function, records authority identity, legal audit, operational alerts or independent review. It does not authorize a purge job or deletion endpoint.

**Disposition:** owner policy remains accepted; fail-closed retention/Hold PoC source added and exact-head CI pending; legal schedule, production Hold, lawful disposal and G1 remain OPEN.

**END — MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.2**


## 11. Exact-head fail-closed Legal Hold proof — 2026-11-22 (v0.3)

The exact-head PostgreSQL workflow `45063596273` passed both methods in `O20OrganizationLegalHoldRetentionPostgresProofTests`:
- `Disposal_eligibility_denies_until_schedule_hold_and_independent_authority_are_valid`;
- `Concurrent_hold_placement_serializes_before_disposal_eligibility_decision`.

The disposable proof verifies that disposal eligibility is denied unless an approved schedule, no covering hold and independently attributable authorization are all present, and that concurrent placement of a hold serializes against the eligibility decision.

This does not approve a retention duration or jurisdiction. It does not create a production Legal Hold resolver, hold authority identity, hold scope taxonomy/release workflow, backup/replica/export handling, production disposal service or legal opinion. The accepted baseline remains retain-by-default/no automatic deletion; missing, unresolved or unavailable hold state must fail closed for disposal.

**Disposition:** owner-accepted OD-05 policy and bounded fail-closed Legal Hold PoC PASS; jurisdictional schedule, hold operations, media scope and lawful disposal implementation remain OPEN. O20-E OPEN; G1 NOT GRANTED.

**END — O20-E Records Retention Policy v0.3**


## 12. Iran legal-source crosswalk — controlled research, not a legal opinion (2026-11-22; v0.4)

### 12.1 Research boundary and currency

This section records a source review for the Iran-market deployment. It is a technical/legal research aid only, not a legal opinion, statutory interpretation, classification of the customer's entity, or approval of a retention schedule. The controlling decision remains OD-05: retain by default, no automatic deletion, duration UNSET until the accountable records/legal authority approves a versioned schedule.

The consolidated AML Executive Regulation source identifies the base regulation of 2019 and its amendment of 1404/07/27, with later targeted amendments of 1404/12/03. The 1404/12/03 amendment changes, among other items, supervision wording and Article 158; the reviewed consolidated text continues to show Article 142 and Article 149 as amended 1404/07/27. Before production legal sign-off, counsel must verify the authoritative Gazette text and any amendments or subsequent Council designations issued after this research snapshot.

### 12.2 Candidate source rules and scope

| Source | Rule found in the text reviewed | Scope / caveat for MD-Gsm |
|---|---|---|
| Commercial Code Articles 1, 2(1), 6, 13 and 19 | Merchant is defined by ordinary engagement in commercial transactions; purchase of movable goods for resale is listed as a commercial transaction. Article 13 says the Article 6 books are retained at least ten years from the end of each year. | Article 6 expressly excludes small merchants from the specified mandatory books; Article 19 says the small-trader category is defined by a Ministry of Justice decree. Whether the particular mobile-parts sale/repair activity and legal form are within the merchant/book rules, and whether the small-merchant exception applies, requires counsel/accountant classification. This is not automatically a ten-year period for every Organization row. |
| AML Executive Regulation, current consolidated text: Articles 1(5), 1(7), 142 and 149 | Current Article 142 (amended 1404/07/27) places a ten-year retention duty on **financial institutions** for specified working-relationship, identification, account/correspondence and financial transaction records; the provision also preserves longer duties under other rules. Article 149 applies the listed AML obligations, including records retention, to specified non-financial professions in the specified activities. | The listed non-financial categories include real-estate brokers, certain vehicle and precious-metal/jewellery/art/handwoven-carpet sellers subject to cash/barter conditions, and notaries/accountants/auditors/lawyers when conducting specified client activities. Generic mobile-phone retail/repair is not expressly named in the list reviewed. The regulation contains a residual high-risk-profession category that depends on designation/approval by the Council; no source establishing such a designation for mobile shops/repair services was identified in this review. Do not treat AML Article 142 as a universal ten-year rule for every mobile shop. |
| VAT Executive Regulation Article 103 | The English translation reviewed states five years from the end of the related Tax Period for ordinary taxable-person records/books; five years from the end of the Tax Period in which the capital-asset adjustment period ends for capital assets; fifteen years from the end of the relevant Tax Period for real-estate records. It also permits the Tax Bureau to notify for additional retention up to five years before expiry. | The retrieved PDF labels itself an unofficial translation. Counsel/tax adviser must verify the controlling Persian text, current law/implementing instruments and whether any record falls within the relevant class. It is not a universal 5-year rule for all Organization history. |

Source texts reviewed:
- Iran Commercial Code, Articles 1–2 and 6–19: https://ticaret.gov.tr/data/5ee20f8213b876e308cc14f3/Commercial%20Code%20of%20Iran.pdf
- Consolidated AML Executive Regulation with amendments, including amendment 1404/07/27 and the 1404/12/03 wording changes: https://hoghogh.ai/circulars/c-af1d2dc5
- Amendment of 1404/12/03 to the AML Executive Regulation: https://hoghogh.ai/circulars/c-209c5307
- VAT Executive Regulation, Article 103 (PDF explicitly marked unofficial translation): https://nbrproduserdata-ir.s3.eu-west-1.amazonaws.com/media/nYIh7kL5rrySLPpuZqYYphBLFQyzvWyzpvQzZPY2.pdf

### 12.3 Preliminary record-class mapping — NOT APPROVED

| Candidate record class | Potential controlling source | Preliminary status |
|---|---|---|
| Statutory commercial books (where the merchant/bookkeeping rules apply) | Commercial Code Article 13; subject to Articles 6 and 19 classification | Potential minimum: ten years from year-end; exact applicability and small-merchant status require counsel |
| VAT books/records of a taxable person | VAT Executive Regulation Article 103, plus any longer applicable law or notice | Potential baseline: five years from relevant tax-period end; capital asset and real-estate records have separate rules |
| AML KYC/account/transaction records | AML Article 142 for financial institutions; Article 149 extension for the specified non-financial professions/activities | Potential ten-year duty only where the statutory scope applies; ordinary mobile retail/repair coverage is not established by this source review |
| Organization Branch/Warehouse/AccountAssignment history | No reviewed provision expressly classifies this domain history as a statutory commercial book, VAT record or AML record | **Unclassified / legal decision required.** Do not derive a ten-year rule by analogy or silently apply the longest period to all event data. OD-05 fail-closed retain-by-default remains in force. |
| Canonical AuditEvent/SecurityEvent, outbox delivery and cryptographic evidence | AuditSecurity/O21 policies and any applicable legal schedule | Separate owner and schedule; do not duplicate canonical AuditSecurity history in Organization. |

Where multiple statutory requirements apply to one record, the eventual schedule must apply the legally controlling and longer applicable requirement, including any written extension or Legal Hold. A schedule must operate by record class, entity/activity, triggering date and jurisdiction; it must not set one indiscriminate duration for every table.

### 12.4 Counsel / records-owner classification checklist

Before adopting a numeric schedule, the accountable Iran legal/tax authority must determine:
1. The operator's legal form and whether it is a merchant under the Commercial Code for each activity (retail resale, repair service, wholesale/import, online sales).
2. Whether the operator falls within the legally defined small-merchant exception and the currently applicable implementing decree.
3. VAT taxpayer status and which specific system records are VAT books, invoices, capital-asset records or real-estate records.
4. Whether the operator is a financial institution, a listed non-financial profession under AML Article 149, or has been formally designated under the residual high-risk-profession route; identify the effective designation and supervisory authority if so.
5. Which Organization records are legally required accounting/tax/audit evidence versus operational access-control history, and whether a record can be separated or minimized without losing required proof.
6. Trigger dates, expiry calculation, pending audit/tax assessment/appeal periods, statutory extensions, limitation periods, Legal Hold placement/release authority, backup/replica/export treatment and lawful disposal approval.
7. Privacy, confidentiality and data-minimization obligations for AccountId references, actor/approver/reason metadata and linked customer information.

### 12.5 Decision status

The research does **not** select 5, 10 or 15 years for Organization assignment/relationship history. It does not establish that a mobile shop/repair business is an AML-covered non-financial profession, nor that every mobile business is subject to the same Commercial Code books. The accepted OD-05 baseline remains:
- retain Organization history by default;
- do not hard-delete or automatically purge;
- disposal only after an approved schedule for the exact record class/jurisdiction has elapsed, no active or unresolved Legal Hold applies, and accountable records/legal authorization is documented;
- missing or ambiguous classification/schedule/hold state fails closed for disposal.

**Disposition:** legal source crosswalk researched and recorded; legal classification, numeric schedule, Legal Hold authority/scope/release, media scope and lawful disposal workflow remain OPEN. No destructive worker, production deletion, retention migration or G1 authorization.

**END — MDG-SECURITY-O20E-RECORDS-RETENTION-001 v0.4**


## 13. Expanded Iran legal-source crosswalk — record classification boundaries (2026-11-22; v0.5)

This addendum refines the v0.4 legal research. It remains a technical requirements crosswalk, **not** a legal opinion, tax classification, or owner-approved numeric retention schedule.

### 13.1 Commercial Code — small-merchant exemption is conditional

The amendment to the Article 19 Regulation dated 1400/06/03 describes small traders as **natural persons**, except holders of occupations within the referenced Article 95(a) category of the Direct Taxes Act as it stood in the stated amendment, where:
- shopkeepers, craftspeople, producers and similar activity have annual sales not exceeding 4,000,000,000 Rials; or
- service providers have non-gross receipts not exceeding 2,000,000,000 Rials.

Those classes are exempted from keeping *commercial trading books* under that regulation. This is not a general exemption from tax-invoice, VAT, electronic taxpayer, AML, employment, consumer, evidence-preservation or other statutory duties. It also does not automatically classify a legal entity as a small trader.

The reference in the 1400 amendment to Article 95(a) as then worded must not be silently mapped to the later Article 95 grouping regulation. The operator's natural/legal form, activities, amounts, and the continued legal interaction of the historical reference with later amendments require Iran counsel/accountant confirmation.

### 13.2 Direct Taxes Act Article 95 — later taxpayer groups are a distinct regime

The Article 95 implementing regulation dated 1404/08/21 groups business taxpayers for the duties covered by that regulation. The reviewed text places specified categories (including all importers/exporters, factories/production units, specified hotels/healthcare, currency exchanges, licensed chain stores and transport businesses) in Group 1 regardless of the ordinary turnover grouping. For other taxpayers it uses prior-year declared/assessed turnover thresholds including more than 450 billion Rials for Group 1 and more than 150 billion up to 450 billion Rials for Group 2; service-only and other specific provisions require the full regulation to be applied.

This Article 95 grouping is not the same legal test as the Article 19 small-trader definition, and neither number creates a retention duration for Organization assignment history. Classification must use the operator's activity, legal form, tax returns, assessed figures and effective regulation.

### 13.3 Article 100 — current performance-year fixed-tax procedure is not a retention schedule

The Tax Administration circular dated 1405/07/29 for performance year 1404 states that business taxpayers (individual or participatory) whose net goods/services sales in 1404 did not exceed 720,000,000,000 Rials, and for whom the fixed-tax form is uploaded in the Tax Administration portal, may qualify for the specified Article 100 procedure and exemption from the Direct Taxes Act books/documents/return for that performance year if they do not file a return. The circular also lists exclusions and related conditions.

This is a year-specific, portal/form-dependent tax procedure. It is **not**:
- a general small-merchant classification;
- a threshold for AML coverage;
- a universal exemption from all recordkeeping;
- a duration for keeping Organization assignment or Warehouse/Branch history.

Re-check the relevant performance-year circular and thresholds for each future tax year. Never hard-code these thresholds as permanent product constants.

### 13.4 AML Regulation — Article 142 / 149 / 158 must be read together

The consolidated AML Executive Regulation source reviewed (base 1398/07/21, Article 149 amended 1404/07/27 and Article 158 amended 1404/12/03) provides materially different scopes:
- Article 142 states a ten-year retention duty for the specified working-relationship identification, account/correspondence and financial-transaction records held by **financial institutions**; other rules may require longer retention.
- Article 149 applies specified chapters, including records retention, to named non-financial professions only within defined activities/transaction conditions and within the limits of procedures notified by the Financial Intelligence Center. The listed groups include real-estate brokers acting for clients; specified vehicle/precious-metal/antique/art/handwoven-carpet sellers when cash/barter transactions exceed the prescribed threshold; and notaries, independent accountants/auditors and lawyers when carrying out specified client asset, account or company activities. Other high-risk professions may be added by the Council through the prescribed designation route.
- Article 158, as amended 1404/12/03, states that the Regulation and its implementing rules are enforceable for all natural and legal persons.

Because Article 158 is broad while Article 149 identifies activity-specific non-financial coverage and Article 142 names financial institutions, this crosswalk does **not** declare generic mobile-phone retail/repair either covered or exempt from every AML record duty. The interaction between the general applicability clause, the statutory “obliged persons” definitions, the activity-specific Article 149 provisions, relevant supervisory directions and any Council high-risk designation requires an Iran AML counsel/regulator determination. No universal ten-year duration is inferred for every mobile shop.

### 13.5 VAT Article 103

The retrieved English Article 103 translation remains explicitly unofficial. It describes distinct retention periods for ordinary taxable-person records, capital assets and real-estate records, and possible written extension. The controlling Persian instrument, any later amendments and the specific record-class applicability must be confirmed by an Iran tax adviser. It is not a blanket schedule for Organization history.

### 13.6 Record-class and operator decision still required

The accountable Iran legal/tax authority must complete a written matrix for the actual operator:
1. natural person vs legal entity, merchant classification, retail/resale, repair, wholesale/import, e-commerce and other licensed activity;
2. whether Article 19 small-trader criteria apply, and whether the Article 95(a) exception/reference excludes that person;
3. Article 95 taxpayer group based on activity, turnover and applicable year's regulation;
4. any Article 100 fixed-tax election/form, performance year, applicable turnover and exclusions;
5. AML obliged-person status, Article 149 activity/transaction scope, any Article 158 interpretation, applicable regulator and any Council designation;
6. exact record classes: legal commercial books, tax/VAT invoices and files, AML/KYC/transaction evidence, Organization master/assignment history, support logs, canonical AuditSecurity events and backup/replica/export;
7. each record's trigger date, minimum legal period, extensions, assessment/appeal/pending-investigation holds, Legal Hold authority/scope/release, lawful disposal approver and evidence.

The numeric values in the cited regulations and annual circulars must be configuration/legal evidence with effective dates, never hard-coded retention policy.

### 13.7 Current disposition

No 4-billion, 2-billion, 150-billion, 450-billion or 720-billion Rial threshold is adopted as a global MD-Gsm system setting. They are conditional criteria in different regimes, with different subjects and legal effects. No 5-, 10- or 15-year retention period is selected for Organization assignment history.

Until the actual operator and record classes are classified and a schedule is approved:
- retain assignment and Branch/Warehouse relationship history by default;
- keep automatic expiry, purge, hard-delete, irreversible masking and cryptographic erasure disabled;
- fail closed when schedule, legal status or hold state is missing, stale, disputed or unavailable;
- do not infer AML exemption/coverage solely from a business name or a sales threshold;
- preserve the ownership boundary: canonical AuditEvent/SecurityEvent remains AuditSecurity-owned.

**Disposition:** legal-source coverage expanded; operator-specific classification, numeric schedule, Legal Hold operating authority, media scope and disposal procedure remain OPEN. No purge worker, production delete, retention migration or G1 authorization.

**END — O20-E Records Retention Policy v0.5**
