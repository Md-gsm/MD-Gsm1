# Modular application boundary

This directory records the approved target topology. Modules are introduced only after G1 authorization. Each module owns its Domain, Application, Infrastructure, and versioned Contracts. Do not place cross-module implementation dependencies here; consumers reference published Contracts only.

Current G0 state: four isolated, version-marker-only Contracts assemblies are compiled and included in MDGsm.sln; the API composition project references these Contracts assemblies. No module business Domain, Application, Infrastructure, API endpoint, or persistence behavior is implemented. The Host and BuildingBlocks physical relocation remains pending.
