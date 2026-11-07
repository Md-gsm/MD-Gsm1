# Desktop Host

This directory contains the WPF `MDGsm.Desktop` presentation host. It references the HTTP client building block under `src/BuildingBlocks/MDGsm.Client` and communicates with the API through versioned HTTP contracts.

Desktop must never reference module Infrastructure, Npgsql, EF Core, or a PostgreSQL connection directly. Its Persons interface is a synthetic preview only; it does not create Persons records, call a Persons API, or persist business data.
