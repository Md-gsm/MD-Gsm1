# Stage 5 — Desktop-to-API connection

## Implemented
- The typed HTTP client is in the shared building block project `src/BuildingBlocks/MDGsm.Client`, independent of WPF.
- Desktop reads `Api:BaseUrl` and `Api:TimeoutSeconds` from its output `appsettings.json`; invalid values fail fast rather than silently falling back.
- API host is in `src/Host/Api` and listens on `http://127.0.0.1:5080` by default for the local development workflow. The setting can be overridden using normal ASP.NET Core configuration.
- WPF Desktop host is in `src/Host/Desktop`. The main window has a manual connection-check action that calls `GET /api/v1/system/info` and displays safe metadata.
- Connection failures, timeouts, non-success HTTP responses, and invalid response envelopes are handled without showing server response bodies or exception details.
- Automated client tests cover success deserialization, safe HTTP error handling, and invalid response rejection. Architecture tests enforce Desktop -> Client only.

## Run locally on Windows

Terminal 1 (API):
```powershell
dotnet run --project .\src\Host\Api\MDGsm.Api.csproj
```

Terminal 2 (Desktop):
```powershell
dotnet run --project .\src\Host\Desktop\MDGsm.Desktop.csproj
```

Click **بررسی اتصال به API**. The default API URL is `http://127.0.0.1:5080/` in Desktop `appsettings.json`.

## Boundaries and limitations
- This is a local development connection, not a production deployment or security claim. Plain HTTP is only for loopback development; remote/network use requires TLS and a separately reviewed deployment configuration.
- The API still has no production authentication/authorization, Persons business endpoints, or accounting operations. Do not expose it to a network.
- The Desktop connection is user-triggered; startup does not assume the API is already running.
- The relocated Host/BuildingBlocks projects and project references are covered by the architecture boundary tests and current-head CI.

## Validation status
The current-source Windows foundation build/test and Windows preview installer are executed through GitHub Actions. The latest branch validation run must be read before declaring its final CI state. This stage does not claim that Persons business APIs or persistence have been implemented.
