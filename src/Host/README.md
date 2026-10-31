# MD-Gsm Host boundaries

The API and Desktop runtime projects are physically located under this directory:

- `Api/MDGsm.Api.csproj` owns server composition, database access, API endpoints, and server-side secrets.
- `Desktop/MDGsm.Desktop.csproj` owns the WPF presentation layer and accesses server capabilities through the API/HTTP client.

Shared technical code is in `src/BuildingBlocks/*`. Business modules are isolated under `src/Modules/*`; consumers use their published Contracts assemblies, never another module's private implementation.
