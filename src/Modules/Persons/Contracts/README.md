# Persons Contracts

Reserved for versioned public contracts owned by Persons. The shared identity identifier is PartyId (UUIDv7), for natural and legal persons. No runtime contract is published yet; this folder is an architecture boundary marker only.


## Party identity reference (1.1)

`PartyReference` is the minimal cross-module reference for an existing Party. It contains only the stable PartyId and rejects an empty identifier. Consumers must obtain it from an authorized Persons workflow; it is not a substitute for creating or validating a Party.
