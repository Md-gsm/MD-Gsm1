# Desktop Host

The WPF Desktop host communicates with the API through the MDGsm.Client HTTP boundary and versioned IdentityAccess and Persons contracts. It must never reference module Infrastructure, Npgsql, EF Core, or a PostgreSQL connection directly.

The Persons workspace uses the authenticated IdentityAccess session and the natural/legal quick-registration API. The bearer token and any pending operation key are held in memory only; credentials and tokens are never written to disk or logs. The first-login password-change response is honored before showing the registration workspace.

The client uses Api:BaseUrl and Api:TimeoutSeconds from appsettings.json. HTTP is accepted only for loopback development; a remote API endpoint must use HTTPS. Persons:WriteApiEnabled is a server-side feature gate and is never enabled or overridden by the Desktop client. When disabled, the server returns 503 and the UI does not fabricate success or create local substitute records.

Search/list HTTP exposure remains withheld pending persistent per-actor/purpose quotas, abuse-lock and audited admin-unlock controls. The Desktop workspace therefore does not display synthetic DEMO persons or pretend that an in-memory list is the real database.
