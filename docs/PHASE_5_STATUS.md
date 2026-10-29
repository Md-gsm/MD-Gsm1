# Stage 5 — Desktop-to-API connection

## Implemented
- Added the small `MDGsm.Client` project, independent of WPF, with a typed `ApiClient`.
- Desktop reads `Api:BaseUrl` and `Api:TimeoutSeconds` from its output `appsettings.json`; invalid values fail fast rather than silently falling back.
- API listens on `http://127.0.0.1:5080` by default for this local development workflow. The setting can be overridden using normal ASP.NET Core configuration.
- Main window has a manual connection-check action that calls `GET /api/v1/system/info` and displays safe metadata.
- Connection failures, timeouts, non-success HTTP responses, and invalid response envelopes are handled without showing server response bodies or exception details.
- Added automated client tests for success deserialization, safe HTTP error handling, and invalid response rejection. Updated architecture boundary test to allow Desktop -> Client only.

## Run locally on Windows
Terminal 1 (API):
```powershell
dotnet run --project .\src\MDGsm.Api\MDGsm.Api.csproj
```
Terminal 2 (Desktop):
```powershell
dotnet run --project .\src\MDGsm.Desktop\MDGsm.Desktop.csproj
```
Click **بررسی اتصال به API**. The default API URL is `http://127.0.0.1:5080/` in Desktop `appsettings.json`.

## Boundaries and limitations
- This is a local development connection, not a production deployment or security claim. Plain HTTP is only for loopback development; remote/network use requires TLS and a separately reviewed deployment configuration.
- The API still has no authentication/authorization, database, or accounting operations. Do not expose it to a network.
- The Desktop connection is user-triggered; startup does not assume the API is already running.
- Build and tests must be run on the target Windows/.NET 10 environment before this stage is accepted.

## Validation status
The source package has not yet been built or tested on the user's Windows machine. Successful archive integrity is not a substitute for Windows build/test results.
