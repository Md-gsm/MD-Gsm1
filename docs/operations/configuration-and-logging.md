# Configuration and logging baseline

- ASP.NET Core configuration uses standard configuration providers: `appsettings.json`, environment-specific settings, environment variables, and command-line arguments.
- `appsettings.json` contains only non-secret defaults. Do not put passwords, tokens, private keys, or database credentials in it.
- Use environment variables or an approved secret store for deployment secrets; do not commit local secret files.
- API logs currently go to the console using the built-in .NET logging abstractions.
- Log lifecycle events, operation identifiers, and sanitized failures. Never log passwords, bearer tokens, full connection strings, or sensitive accounting/customer payloads.
- Keep health/liveness responses minimal and do not expose exception details to clients.
- File-based rolling logs, retention, correlation IDs, centralized collection, and production redaction policy remain future deployment decisions and are not claimed implemented in this phase.
