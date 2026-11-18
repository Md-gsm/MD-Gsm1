# Decision 0001 — Clean start

- **Status:** Accepted for Phase 1
- **Decision:** MD-Gsm is authored as a new project from an empty project directory.
- **Boundary:** No code, project files, scripts, schemas, migrations, binaries, dependencies, or implementation artifacts from any earlier application may be copied, adapted, or imported.
- **Architecture:** The desktop client communicates with the API; the API uses application contracts and domain rules; infrastructure adapters are kept behind application-facing boundaries.
- **Phase 1 exclusions:** Persistence, authentication, accounting workflows, financial posting, installer/update pipeline, and production deployment.
- **Verification:** Review project inventory and run build/tests on a Windows machine with the .NET 10 SDK before accepting the phase.
