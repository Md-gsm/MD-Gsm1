# API Host

This directory contains the executable `MDGsm.Api` host project and its composition root. It references shared building blocks under `src/BuildingBlocks/*` and the versioned public Contracts assemblies under `src/Modules/*/Contracts`.

The API host is the server-side boundary for persistence, database connections, configuration, and secrets. It may compose module contracts, but it must not reach into module-private Domain or Infrastructure implementations.

Persons endpoints are not implemented. The technical health and system-info endpoints do not imply a Persons API or business persistence.
