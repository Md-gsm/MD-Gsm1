# Persons Infrastructure

This module-owned project contains the PostgreSQL repository, temporary-workflow persistence, authenticated-encryption adapter, Windows DPAPI key provider, retention worker and Party merge coordinator. Its only project dependencies are the Persons Domain, Application and published Contracts assemblies. Cross-module IdentityAccess authorization adapters are composed by the API Host, not by this project.

Persons migrations are embedded here with the historical `MDGsm.Migrations.{Filename}{Extension}` logical resource names. The shared DbMigrator explicitly loads this assembly as an additional migration source.
