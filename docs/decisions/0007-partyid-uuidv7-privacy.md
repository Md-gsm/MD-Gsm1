# ADR-0007 — UUIDv7 PartyId timestamp disclosure

- Status: **Proposed**.
- Scope: Persons V1 identity identifier.
- Decision owner: Project Owner.
- Review: Group of Software Advisers; no acceptance is implied by this proposal.

## Context

The Persons design uses UUIDv7 for the stable PartyId shared by natural persons and legal entities. UUIDv7 places a Unix-epoch timestamp in the identifier, so a PartyId can reveal the approximate time at which it was generated. This is a privacy-relevant metadata disclosure even when the identifier contains no name, phone number, national identifier, or business meaning.

## Proposed decision

1. Keep UUIDv7 for PartyId, subject to the remaining architecture and owner gates.
2. Treat the embedded timestamp as approximate creation-time metadata that may be observable wherever PartyId is exposed.
3. PartyId is an identifier, **not** a secret, password, bearer token, authentication factor, or authorization grant.
4. Every read/write operation must enforce authenticated actor, explicit permission, and server-side scope independently of possession or knowledge of PartyId.
5. Do not rely on UUID opacity or timestamp ordering to prevent enumeration or unauthorized access.
6. Avoid unauthenticated endpoints or differentiated responses that reveal whether a PartyId exists.
7. Where a use case requires a non-public or unguessable capability, design a separate cryptographically random, purpose-bound token with its own lifecycle; do not reuse PartyId.

## Consequences

- API and audit design must regard PartyId as a correlatable identifier, not as a confidential credential.
- UUIDv7 can aid index locality and approximate temporal ordering, but ordering is not a security property and can disclose creation-time patterns.
- Security tests must prove that an authenticated caller without the relevant permission/scope cannot access a Party merely by knowing its PartyId.
- The timestamp disclosure does not by itself prohibit UUIDv7, but it must remain explicit in privacy review and threat modelling.

## Acceptance

This ADR remains Proposed until the Project Owner and required architecture review process explicitly accept or amend it. Its presence does not authorize additional Persons runtime, API, persistence, or G1 activation.
