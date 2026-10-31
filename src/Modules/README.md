# Modular application boundary

This directory records the approved target topology. Modules are introduced only after G1 authorization. Each module owns its Domain, Application, Infrastructure, and versioned Contracts. Do not place cross-module implementation dependencies here; consumers reference published Contracts only.

Current state: architecture specification only; no runtime module is implemented.
