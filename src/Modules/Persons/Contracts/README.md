# Persons Contracts

Reserved for versioned public contracts owned by Persons. The shared identity identifier is PartyId (UUIDv7), for natural and legal persons.

## Party identity reference (1.1)

`PartyReference` carries the stable PartyId. Its constructor is not proof that a Party exists or is active. Consumers must call the Persons-owned `IPartyReferenceValidator` inside the same caller-owned PostgreSQL transaction before creating dependent records.

The validator locks an active Party row with `FOR SHARE` until the transaction ends. Unknown and inactive Parties are rejected. This prevents a Party from being deactivated while a dependent Finance account is being provisioned. No cross-module foreign key or direct Finance ownership of Persons data is introduced.
