# Modular application boundary

This directory records the approved target topology. Modules are introduced only after G1 authorization. Each module owns its Domain, Application, Infrastructure, and versioned Contracts. Do not place cross-module implementation dependencies here; consumers reference published Contracts only.

Current state: four isolated Contracts assemblies are compiled and included in MDGsm.sln. IdentityAccess has partial server-side runtime. Finance has a narrowly scoped runtime slice for Party subledger account identity and merge participation, referencing only published Finance and Persons Contracts. Persons has partial BuildingBlocks runtime for Party registration, temporary workflow retention, and controlled merge coordination. These slices do not constitute complete modules or G1 acceptance; production migration and G1 authorization remain prohibited until their independent gates pass.
