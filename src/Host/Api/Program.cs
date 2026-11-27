using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Hosting;
using Npgsql;
using MDGsm.Api.Security;
using MDGsm.Application.Contracts;
using MDGsm.Persons.Application;
using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Contracts;
using MDGsm.IdentityAccess.Domain;
using MDGsm.Infrastructure.IdentityAccess;
using MDGsm.Finance.Contracts;
using MDGsm.Finance.Infrastructure.Accounts;
using MDGsm.Persons.Contracts;
using MDGsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// The central installer writes this machine-local, non-secret endpoint configuration.
// It is not shipped in the application package and contains no private key material.
var networkSettingsPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "MD-Gsm", "Network", "api-network.json");
builder.Configuration.AddJsonFile(networkSettingsPath, optional: true, reloadOnChange: false);

X509Certificate2? apiTlsCertificate = null;
var networkMode = builder.Configuration["Network:Mode"];
if (string.Equals(networkMode, "Https", StringComparison.OrdinalIgnoreCase))
{
    if (!OperatingSystem.IsWindows())
        throw new PlatformNotSupportedException("The machine certificate-store API listener is Windows-only.");

    var bindAddressText = builder.Configuration["Network:BindAddress"];
    if (!IPAddress.TryParse(bindAddressText, out var bindAddress) ||
        bindAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        throw new InvalidOperationException("The configured API HTTPS bind address is invalid.");

    if (!int.TryParse(builder.Configuration["Network:Port"], out var httpsPort) ||
        httpsPort is < 1024 or > 65535)
        throw new InvalidOperationException("The configured API HTTPS port is invalid.");

    var thumbprint = builder.Configuration["Network:CertificateThumbprint"];
    var selectedCertificate = ApiTlsCertificateLoader.LoadFromLocalMachine(thumbprint ?? string.Empty);
    apiTlsCertificate = selectedCertificate;

    // Listen() defines the only endpoint in central-server mode. The production
    // loopback HTTP Urls setting is therefore not also exposed as a downgrade path.
    builder.WebHost.ConfigureKestrel(options =>
        options.Listen(bindAddress, httpsPort, listenOptions =>
            listenOptions.UseHttps(selectedCertificate)));
}
else if (!string.IsNullOrWhiteSpace(networkMode))
{
    throw new InvalidOperationException("The configured API network mode is unsupported; refusing to start an unverified listener.");
}

var protectedDatabaseConnectionFile = builder.Configuration["Database:ProtectedConnectionFile"];
if (!string.IsNullOrWhiteSpace(protectedDatabaseConnectionFile))
{
    builder.Configuration.AddProtectedDatabaseConnectionString(protectedDatabaseConnectionFile);
}
builder.Services.AddMdGsmPersistence(builder.Configuration);

if (args.Length > 0 && string.Equals(args[0], "--provision-persons-workflow-key", StringComparison.Ordinal))
{
    if (args.Length != 3)
        throw new ArgumentException("Expected the protected key path and service name for one-time provisioning.");
    if (!OperatingSystem.IsWindows())
        throw new PlatformNotSupportedException("Persons workflow key provisioning is Windows-only.");

#pragma warning disable ASP0000 // The one-shot command exits before host startup and no provider escapes this scope.
    using (var provisioningServices = builder.Services.BuildServiceProvider())
    {
        provisioningServices.GetRequiredService<ITemporaryWorkflowKeyProvisioner>()
            .ProvisionNewKeyForService(args[1], args[2]);
    }
#pragma warning restore ASP0000
    return;
}
builder.Host.UseWindowsService(options =>
    options.ServiceName = PersonsApiServiceIdentity.ApiServiceName);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
// Windows Service console output is not a reliable diagnostic sink. Mirror
// application errors to the standard Windows Application event log.
if (OperatingSystem.IsWindows())
{
    builder.Logging.AddEventLog(settings =>
    {
        // The EventLog settings delegate is analyzed independently of the
        // outer platform guard, so guard its Windows-only properties here too.
        if (OperatingSystem.IsWindows())
        {
            settings.LogName = "Application";
            settings.SourceName = "MDGsm.Api";
        }
    });
}
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddSingleton<IAccessTokenFactory, OpaqueAccessTokenFactory>();
builder.Services.AddSingleton<IPasswordHasher, PlatformPasswordHasher>();
// Construct the HMAC key lazily. The login endpoint explicitly returns 503 when
// secure server configuration is absent; the API can still expose liveness safely.
builder.Services.AddSingleton<IPrivacyDigest>(sp =>
    new HmacPrivacyDigest(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<LoginHandler>();
builder.Services.AddScoped<ChangeOwnPasswordHandler>();
builder.Services.AddScoped<LogoutHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthenticatedActorContext, HttpAuthenticatedActorContext>();
builder.Services.AddScoped<IFinanceActorContext, AuthenticatedFinanceActorContext>();
builder.Services.AddScoped<PostgresPartyAccountProvisioner>();
builder.Services.AddScoped<IPartyMergeParticipant, PostgresPartyAccountMergeParticipant>();

// The database-backed Persons endpoints must not be added to the endpoint data
// source unless their complete persistence/authorization dependency set exists.
// The server installer intentionally starts without a database connection string;
// registering these handlers anyway makes Minimal API infer missing services as
// request parameters and can break endpoint initialization (including /health/live).
var personsPersistenceAvailable =
    builder.Services.Any(descriptor => descriptor.ServiceType == typeof(NpgsqlDataSource)) &&
    builder.Services.Any(descriptor => descriptor.ServiceType == typeof(IRolePermissionStore)) &&
    builder.Services.Any(descriptor => descriptor.ServiceType == typeof(IPartyRepository));

var app = builder.Build();
if (apiTlsCertificate is not null)
    app.Lifetime.ApplicationStopped.Register(apiTlsCertificate.Dispose);
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SessionAuthenticationMiddleware>();
var logger = app.Logger;
logger.LogInformation("MD-Gsm API starting. Protected Persons requests require a validated PostgreSQL-backed session.");

app.MapGet("/health/live", () => Results.Ok(new
{
    product = "MD-Gsm",
    status = "alive",
    scope = "process-liveness-only"
}));

app.MapGet("/health/ready", async (IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(PersistenceRegistration.ConnectionStringName)))
        return (IResult)Results.Json(new { status = "not-ready", reason = "database-not-configured" }, statusCode: StatusCodes.Status503ServiceUnavailable);

    var dataSource = services.GetService<NpgsqlDataSource>();
    if (dataSource is null)
        return (IResult)Results.Json(new { status = "not-ready", reason = "database-service-not-registered" }, statusCode: StatusCodes.Status503ServiceUnavailable);

    try
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT session_user, current_user, current_database(), current_setting('server_version_num')::integer, host(inet_server_addr())",
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return (IResult)Results.Json(new { status = "not-ready", reason = "database-identity-unverified" }, statusCode: StatusCodes.Status503ServiceUnavailable);

        var sessionLogin = reader.GetString(0);
        var effectiveRole = reader.GetString(1);
        var databaseName = reader.GetString(2);
        var serverVersion = reader.GetInt32(3);
        var serverAddress = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);
        var expectedLogin = configuration["Database:ExpectedRuntimeLogin"] ?? "mdgsm_api_runtime";
        var expectedDatabase = configuration["Database:ExpectedDatabaseName"] ?? "mdgsm";
        var expectedMajor = configuration.GetValue<int?>("Database:ExpectedPostgreSqlMajor") ?? 18;
        var serverMajor = serverVersion / 10000;
        // PostgreSQL may report an IPv4 loopback peer as an IPv4-mapped IPv6 address
        // depending on the listener/socket family. Normalize mapped addresses before
        // enforcing the loopback-only runtime database boundary.
        var isLoopback = System.Net.IPAddress.TryParse(serverAddress, out var parsedServerAddress)
            && System.Net.IPAddress.IsLoopback(parsedServerAddress.IsIPv4MappedToIPv6
                ? parsedServerAddress.MapToIPv4()
                : parsedServerAddress);

        if (!string.Equals(sessionLogin, expectedLogin, StringComparison.Ordinal)
            || !string.Equals(effectiveRole, expectedLogin, StringComparison.Ordinal)
            || !string.Equals(databaseName, expectedDatabase, StringComparison.Ordinal)
            || serverMajor != expectedMajor
            || !isLoopback)
        {
            app.Logger.LogWarning(
                "Database readiness identity check failed. LoginMatches={LoginMatches}; RoleMatches={RoleMatches}; DatabaseMatches={DatabaseMatches}; MajorMatches={MajorMatches}; Loopback={Loopback}",
                string.Equals(sessionLogin, expectedLogin, StringComparison.Ordinal),
                string.Equals(effectiveRole, expectedLogin, StringComparison.Ordinal),
                string.Equals(databaseName, expectedDatabase, StringComparison.Ordinal),
                serverMajor == expectedMajor,
                isLoopback);
            app.Logger.LogWarning("Database readiness server address reported by PostgreSQL: {ServerAddress}",
                string.IsNullOrWhiteSpace(serverAddress) ? "(null)" : serverAddress);
            return (IResult)Results.Json(new { status = "not-ready", reason = "database-identity-mismatch" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Results.Ok(new { status = "ready", dependency = "postgresql", serverMajor, identity = "verified" });
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        app.Logger.LogWarning("Database readiness check failed. ExceptionType={ExceptionType}", ex.GetType().Name);
        return (IResult)Results.Json(new { status = "not-ready", reason = "database-unreachable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/api/v1/system/info", (HttpContext context) =>
    Results.Ok(new ApiResponse<SystemInfo>(new SystemInfo("MD-Gsm", "v1", "foundation"), context.TraceIdentifier)));

app.MapPost("/api/v1/identity/login", async (
    LoginRequest request,
    HttpContext context,
    IConfiguration configuration,
    IServiceProvider services,
    CancellationToken cancellationToken) =>
{
    if (!configuration.GetValue<bool>("IdentityAccess:HttpLoginEnabled")
        || string.IsNullOrWhiteSpace(configuration.GetConnectionString(PersistenceRegistration.ConnectionStringName))
        || string.IsNullOrWhiteSpace(configuration[HmacPrivacyDigest.ConfigurationKey]))
        return Results.Json(new { error = "identity_access_not_configured" },
            statusCode: StatusCodes.Status503ServiceUnavailable);

    var digest = services.GetRequiredService<IPrivacyDigest>();
    // Never trust X-Forwarded-For or a client-provided source key here. The direct
    // peer address is transformed with the server-held HMAC key before persistence.
    var source = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var sourceDigest = Convert.ToHexString(digest.ComputeSha256(source));
    var outcome = await services.GetRequiredService<LoginHandler>().HandleAsync(
        new LoginCommand(request.Username, request.Password, request.ClientInstanceId,
            sourceDigest, context.TraceIdentifier), cancellationToken);

    if (!outcome.Succeeded)
    {
        if (outcome.Failure == LoginFailure.SourceRateLimited)
            return Results.Json(new { error = "login_rate_limited" },
                statusCode: StatusCodes.Status429TooManyRequests);
        if (outcome.Failure == LoginFailure.SessionCapacityReached)
            return Results.Json(new { error = "session_capacity_reached" },
                statusCode: StatusCodes.Status409Conflict);
        return Results.Json(new { error = "invalid_credentials" },
            statusCode: StatusCodes.Status401Unauthorized);
    }

    return Results.Ok(new LoginResponse(outcome.AccessToken!, outcome.LeaseExpiresAtUtc!.Value,
        outcome.MustChangePassword));
});

app.MapPost("/api/v1/identity/password/change", async (
    ChangePasswordRequest request,
    IAuthenticatedActorContext actor,
    ChangeOwnPasswordHandler handler,
    HttpContext context,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrEmpty(request.NewPassword) || request.NewPassword.Length < 12
        || request.NewPassword.Length > 128
        || string.Equals(request.CurrentPassword, request.NewPassword, StringComparison.Ordinal))
        return Results.BadRequest(new { error = "password_policy_failed" });

    var changed = await handler.HandleAsync(AccountId.From(actor.AccountId),
        request.CurrentPassword, request.NewPassword, context.TraceIdentifier, cancellationToken);
    return changed
        ? Results.NoContent()
        : Results.Json(new { error = "invalid_credentials" }, statusCode: StatusCodes.Status401Unauthorized);
});

app.MapPost("/api/v1/identity/heartbeat", () => Results.NoContent());

app.MapPost("/api/v1/identity/logout", async (
    HttpContext context,
    LogoutHandler handler,
    CancellationToken cancellationToken) =>
{
    var header = context.Request.Headers.Authorization.ToString();
    if (AuthenticationHeaderValue.TryParse(header, out var authorization)
        && string.Equals(authorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
        && authorization.Parameter is not null)
    {
        await handler.HandleAsync(authorization.Parameter, context.TraceIdentifier, cancellationToken);
    }

    // Deliberately return the same response for unknown, expired or already-revoked tokens.
    return Results.NoContent();
});


if (personsPersistenceAvailable)
{
// Every public create path performs duplicate resolution. There is intentionally no
// lower-level HTTP route that can bypass the quick-registration decision or ForceNew validation.
app.MapPost("/api/v2/persons", async (
    QuickRegisterNaturalPartyRequest request, HttpContext context, IConfiguration configuration,
    [FromServices] NpgsqlDataSource dataSource, IAuthenticatedActorContext actor, [FromServices] IRolePermissionStore permissions,
    [FromServices] IPartyRepository parties, CancellationToken cancellationToken) =>
{
    if (!configuration.GetValue<bool>("Persons:WriteApiEnabled"))
        return (IResult)Results.Json(new { error = "persons_write_api_disabled" },
            statusCode: StatusCodes.Status503ServiceUnavailable);

    var accountId = AccountId.From(actor.AccountId);
    if (!await permissions.HasPermissionAsync(accountId, "persons.create", cancellationToken) ||
        !await permissions.HasPermissionAsync(accountId, "persons.read", cancellationToken))
        return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
    if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"].ToString(), out var operationId) ||
        operationId == Guid.Empty)
        return (IResult)Results.BadRequest(new { error = "idempotency_key_required" });

    try
    {
        var registration = PersonsRegistrationMapper.Map(request.Party);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var result = await parties.QuickRegisterNaturalAsync(
            registration, actor.AccountId.ToString("D"), operationId, request.ForceNew, request.SelectedPartyId,
            transaction, cancellationToken, request.ConfirmDistinctParty);
        await transaction.CommitAsync(cancellationToken);
        var response = PersonsRegistrationMapper.Map(result);
        return result.Created
            ? (IResult)Results.Created($"/api/v2/persons/{result.PartyId}", response)
            : (IResult)Results.Ok(response);
    }
    catch (IdempotencyKeyConflictException)
    {
        return (IResult)Results.Conflict(new { error = "idempotency_key_conflict" });
    }
    catch (DuplicatePartyIdentifierException)
    {
        return (IResult)Results.Conflict(new { error = "duplicate_party_identifier" });
    }
    catch (InvalidOperationException)
    {
        return (IResult)Results.Conflict(new { error = "quick_registration_selection_invalid" });
    }
    catch (ArgumentException)
    {
        return (IResult)Results.UnprocessableEntity(new { error = "validation_failed" });
    }
});

app.MapPost("/api/v2/persons/legal", async (
    QuickRegisterLegalPartyRequest request, HttpContext context, IConfiguration configuration,
    [FromServices] NpgsqlDataSource dataSource, IAuthenticatedActorContext actor, [FromServices] IRolePermissionStore permissions,
    [FromServices] IPartyRepository parties, CancellationToken cancellationToken) =>
{
    if (!configuration.GetValue<bool>("Persons:WriteApiEnabled"))
        return (IResult)Results.Json(new { error = "persons_write_api_disabled" },
            statusCode: StatusCodes.Status503ServiceUnavailable);

    var accountId = AccountId.From(actor.AccountId);
    if (!await permissions.HasPermissionAsync(accountId, "persons.create", cancellationToken) ||
        !await permissions.HasPermissionAsync(accountId, "persons.read", cancellationToken))
        return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
    if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"].ToString(), out var operationId) ||
        operationId == Guid.Empty)
        return (IResult)Results.BadRequest(new { error = "idempotency_key_required" });

    try
    {
        var registration = PersonsRegistrationMapper.Map(request.Party);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var result = await parties.QuickRegisterLegalAsync(
            registration, actor.AccountId.ToString("D"), operationId, request.ForceNew, request.SelectedPartyId,
            transaction, cancellationToken, request.ConfirmDistinctParty);
        await transaction.CommitAsync(cancellationToken);
        var response = PersonsRegistrationMapper.Map(result);
        return result.Created
            ? (IResult)Results.Created($"/api/v2/persons/{result.PartyId}", response)
            : (IResult)Results.Ok(response);
    }
    catch (IdempotencyKeyConflictException)
    {
        return (IResult)Results.Conflict(new { error = "idempotency_key_conflict" });
    }
    catch (DuplicatePartyIdentifierException)
    {
        return (IResult)Results.Conflict(new { error = "duplicate_party_identifier" });
    }
    catch (InvalidOperationException)
    {
        return (IResult)Results.Conflict(new { error = "quick_registration_selection_invalid" });
    }
    catch (ArgumentException)
    {
        return (IResult)Results.UnprocessableEntity(new { error = "validation_failed" });
    }
});

app.MapGet("/api/v2/persons/{partyId}", async (
    string partyId,
    bool? includeSensitive,
    [FromServices] IPartyRepository parties,
    IAuthenticatedActorContext actor,
    [FromServices] IRolePermissionStore permissions,
    CancellationToken cancellationToken) =>
{
    var accountId = AccountId.From(actor.AccountId);
    // Party detail is a global/rowless read until authoritative assignment scope exists.
    if (!await PersonsGlobalReadAuthorization.CanReadAsync(permissions, accountId, cancellationToken))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    try
    {
        var details = await parties.GetAsync(
            partyId, actor.AccountId.ToString("D"), includeSensitive ?? false, null, cancellationToken);
        return details is null ? Results.NotFound() : Results.Ok(PersonsRegistrationMapper.Map(details));
    }
    catch (UnauthorizedAccessException)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
});

 
// Search is absent from the endpoint table unless the explicit feature flag is enabled.
// Every request still requires the current permission and durable server-side controls.
if (builder.Configuration.GetValue<bool>("Persons:SearchApiEnabled") &&
    builder.Services.Any(descriptor => descriptor.ServiceType == typeof(IPersonsSearchControl)))
{
    app.MapGet("/api/v2/persons/search", async (
        string term, int? limit, HttpContext context, IAuthenticatedActorContext actor,
        [FromServices] IRolePermissionStore permissions, [FromServices] IPartyRepository parties,
        [FromServices] IPersonsSearchControl searchControl, CancellationToken cancellationToken) =>
    {
        var accountId = AccountId.From(actor.AccountId);
        // Search is global and rowless; use the same fail-closed role scope as Party details.
        if (!await PersonsGlobalReadAuthorization.CanReadAsync(permissions, accountId, cancellationToken))
        {
            await searchControl.RecordSecurityViolationAsync(actor.AccountId.ToString("D"),
                PersonsSearchSecurityViolation.PermissionDenied, context.TraceIdentifier, cancellationToken);
            return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        var control = await searchControl.TryConsumeAsync(actor.AccountId.ToString("D"), "persons.search", cancellationToken);
        if (!control.Allowed)
        {
            context.Response.Headers["Retry-After"] = control.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var error = control.Rejection == PersonsSearchControlRejection.ActorLocked
                ? "persons_search_actor_locked" : "persons_search_rate_limited";
            return (IResult)Results.Json(new { error, retryAfterSeconds = control.RetryAfterSeconds },
                statusCode: StatusCodes.Status429TooManyRequests);
        }

        try
        {
            var results = await parties.SearchAsync(term, limit ?? PartySearchLimits.MaxResults,
                actor.AccountId.ToString("D"), cancellationToken);
            // Search returns only the fields needed to identify a candidate.
            // Do not serialize the internal list DTO (roles, city, or creation timestamp).
            var items = results.Select(item => new
            {
                partyId = item.PartyId,
                partyType = item.Type.ToString(),
                displayName = item.DisplayName,
                maskedMobile = item.MaskedPrimaryMobile
            });
            return (IResult)Results.Ok(new { items, requestId = context.TraceIdentifier });
        }
        catch (ArgumentException)
        {
            return (IResult)Results.UnprocessableEntity(new { error = "persons_search_validation_failed" });
        }
    });

    app.MapPost("/api/v2/persons/search/actors/{actorId}/unlock", async (
        string actorId, AdminSearchUnlockRequest request, HttpContext context,
        IAuthenticatedActorContext admin, [FromServices] IRolePermissionStore permissions,
        [FromServices] IPersonsSearchControl searchControl, CancellationToken cancellationToken) =>
    {
        if (!Guid.TryParse(actorId, out var targetActor) || targetActor == Guid.Empty)
            return (IResult)Results.BadRequest(new { error = "actor_id_invalid" });
        var adminAccount = AccountId.From(admin.AccountId);
        if (!await permissions.HasRoleAsync(adminAccount, RoleCatalog.SuperAdmin, cancellationToken))
            return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
        try
        {
            await searchControl.AdminUnlockAsync(targetActor.ToString("D"), admin.AccountId.ToString("D"),
                request.Reason, context.TraceIdentifier, cancellationToken);
            return (IResult)Results.NoContent();
        }
        catch (ArgumentException)
        {
            return (IResult)Results.BadRequest(new { error = "unlock_reason_invalid" });
        }
    });
}

 
if (builder.Configuration.GetValue<bool>("Persons:LifecycleApiEnabled") &&
    builder.Services.Any(descriptor => descriptor.ServiceType == typeof(IPartyLifecycleService)))
{
    static IResult MapLifecycleResult(PartyLifecycleChangeResult result) => result.Status switch
    {
        PartyLifecycleChangeStatus.Applied => Results.NoContent(),
        PartyLifecycleChangeStatus.NotFound => Results.NotFound(),
        PartyLifecycleChangeStatus.VersionConflict => Results.Conflict(new { error = "persons_lifecycle_version_conflict", currentRowVersion = result.CurrentRowVersion }),
        PartyLifecycleChangeStatus.PartyInactiveOrMerged => Results.Conflict(new { error = "persons_party_inactive_or_merged", currentRowVersion = result.CurrentRowVersion }),
        _ => Results.Conflict(new { error = "persons_lifecycle_conflict" })
    };

    app.MapPatch("/api/v2/persons/{partyId:guid}/lifecycle", async (
        Guid partyId, SetPartyActiveRequest request, HttpContext context,
        IAuthenticatedActorContext actor, [FromServices] IRolePermissionStore permissions,
        [FromServices] IPartyLifecycleService lifecycleService, CancellationToken cancellationToken) =>
    {
        if (!await permissions.HasPermissionAsync(AccountId.From(actor.AccountId), "persons.update", cancellationToken))
            return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
        try
        {
            var result = await lifecycleService.SetPartyActiveAsync(partyId, request.IsActive,
                request.ExpectedRowVersion, actor.AccountId.ToString("D"), request.Reason, cancellationToken);
            return MapLifecycleResult(result);
        }
        catch (ArgumentException) { return Results.BadRequest(new { error = "persons_lifecycle_request_invalid" }); }
    });

    app.MapPatch("/api/v2/persons/contact-points/{contactPointId:guid}/lifecycle", async (
        Guid contactPointId, SetContactPointActiveRequest request,
        IAuthenticatedActorContext actor, [FromServices] IRolePermissionStore permissions,
        [FromServices] IPartyLifecycleService lifecycleService, CancellationToken cancellationToken) =>
    {
        if (!await permissions.HasPermissionAsync(AccountId.From(actor.AccountId), "persons.update", cancellationToken))
            return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
        try
        {
            return MapLifecycleResult(await lifecycleService.SetContactPointActiveAsync(contactPointId, request.IsActive,
                request.ExpectedRowVersion, actor.AccountId.ToString("D"), request.Reason, cancellationToken));
        }
        catch (ArgumentException) { return Results.BadRequest(new { error = "persons_lifecycle_request_invalid" }); }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        { return Results.Conflict(new { error = "persons_contact_duplicate" }); }
    });

    app.MapPatch("/api/v2/persons/contact-points/{contactPointId:guid}", async (
        Guid contactPointId, UpdateContactPointRequest request,
        IAuthenticatedActorContext actor, [FromServices] IRolePermissionStore permissions,
        [FromServices] IPartyLifecycleService lifecycleService, CancellationToken cancellationToken) =>
    {
        if (!await permissions.HasPermissionAsync(AccountId.From(actor.AccountId), "persons.update", cancellationToken))
            return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
        try
        {
            return MapLifecycleResult(await lifecycleService.UpdateContactPointValueAsync(contactPointId, request.Value,
                request.ExpectedRowVersion, actor.AccountId.ToString("D"), request.Reason, cancellationToken));
        }
        catch (ArgumentException) { return Results.BadRequest(new { error = "persons_lifecycle_request_invalid" }); }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        { return Results.Conflict(new { error = "persons_contact_duplicate" }); }
    });

    app.MapPatch("/api/v2/persons/addresses/{addressId:guid}/lifecycle", async (
        Guid addressId, SetAddressActiveRequest request,
        IAuthenticatedActorContext actor, [FromServices] IRolePermissionStore permissions,
        [FromServices] IPartyLifecycleService lifecycleService, CancellationToken cancellationToken) =>
    {
        if (!await permissions.HasPermissionAsync(AccountId.From(actor.AccountId), "persons.update", cancellationToken))
            return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
        try
        {
            return MapLifecycleResult(await lifecycleService.SetAddressActiveAsync(addressId, request.IsActive,
                request.ExpectedRowVersion, actor.AccountId.ToString("D"), request.Reason, cancellationToken));
        }
        catch (ArgumentException) { return Results.BadRequest(new { error = "persons_lifecycle_request_invalid" }); }
    });

    app.MapPatch("/api/v2/persons/addresses/{addressId:guid}", async (
        Guid addressId, UpdateAddressRequest request,
        IAuthenticatedActorContext actor, [FromServices] IRolePermissionStore permissions,
        [FromServices] IPartyLifecycleService lifecycleService, CancellationToken cancellationToken) =>
    {
        if (!await permissions.HasPermissionAsync(AccountId.From(actor.AccountId), "persons.update", cancellationToken))
            return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);
        try
        {
            return MapLifecycleResult(await lifecycleService.UpdateAddressAsync(addressId, request.CountryCode,
                request.Province, request.City, request.PostalCode, request.AddressLine, request.ExpectedRowVersion,
                actor.AccountId.ToString("D"), request.Reason, cancellationToken));
        }
        catch (ArgumentException) { return Results.BadRequest(new { error = "persons_lifecycle_request_invalid" }); }
    });
}

 
if (builder.Configuration.GetValue<bool>("Persons:HistoryApiEnabled") &&
    builder.Services.Any(descriptor => descriptor.ServiceType == typeof(IPartyLifecycleHistoryReader)))
{
    app.MapGet("/api/v2/persons/{partyId:guid}/history", async (
        Guid partyId, int? limit, DateTimeOffset? beforeOccurredAtUtc, Guid? beforeHistoryId,
        IAuthenticatedActorContext actor, [FromServices] IRolePermissionStore permissions,
        [FromServices] IPartyLifecycleHistoryReader historyReader, CancellationToken cancellationToken) =>
    {
        var accountId = AccountId.From(actor.AccountId);
        if (!await permissions.HasPermissionAsync(accountId, "persons.read", cancellationToken) ||
            !await permissions.HasPermissionAsync(accountId, "persons.sensitive.read", cancellationToken))
            return (IResult)Results.StatusCode(StatusCodes.Status403Forbidden);

        if ((beforeOccurredAtUtc is null) != (beforeHistoryId is null))
            return (IResult)Results.BadRequest(new { error = "persons_history_cursor_invalid" });
        var cursor = beforeOccurredAtUtc is null
            ? null
            : new PartyLifecycleHistoryCursor(beforeOccurredAtUtc.Value, beforeHistoryId!.Value);
        try
        {
            var page = await historyReader.GetAsync(partyId, limit ?? 50, cursor,
                actor.AccountId.ToString("D"), cancellationToken);
            return (IResult)Results.Ok(page);
        }
        catch (ArgumentException)
        {
            return (IResult)Results.BadRequest(new { error = "persons_history_request_invalid" });
        }
    });
}

}

if (!personsPersistenceAvailable)
{
    app.Logger.LogInformation(
        "Database-backed Persons API routes are not registered because required persistence services are unavailable.");
}

// Promotion routes are absent unless both an explicit API flag and an approved
// encryption-key provider are supplied by the host. The repository does not invent
// or configure key custody, and the default flag is false.
var workflowPromotionApiEnabled =
    builder.Configuration.GetValue<bool>("Persons:WorkflowPromotionApiEnabled") &&
    builder.Services.Any(descriptor => descriptor.ServiceType == typeof(ITemporaryWorkflowPromotionService));

if (workflowPromotionApiEnabled)
{
    app.MapGet("/api/v2/persons/workflows/{workflowRecordId:guid}/promotion-preview", async (
        Guid workflowRecordId,
        IAuthenticatedActorContext actor,
        ITemporaryWorkflowPromotionService promotion,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var preview = await promotion.PreviewAsync(
                workflowRecordId, actor.AccountId, Guid.CreateVersion7(), cancellationToken);
            return Results.Ok(preview);
        }
        catch (UnauthorizedAccessException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (TemporaryWorkflowOwnershipException)
        {
            return Results.NotFound();
        }
        catch (TemporaryWorkflowUnavailableException)
        {
            return Results.NotFound();
        }
        catch (TemporaryWorkflowPayloadException)
        {
            return Results.UnprocessableEntity(new { error = "temporary_workflow_payload_invalid" });
        }
    });

    app.MapPost("/api/v2/persons/workflows/{workflowRecordId:guid}/promote", async (
        Guid workflowRecordId,
        TemporaryWorkflowPromotionRequest request,
        HttpContext context,
        IAuthenticatedActorContext actor,
        ITemporaryWorkflowPromotionService promotion,
        CancellationToken cancellationToken) =>
    {
        if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"].ToString(), out var operationId) ||
            operationId == Guid.Empty)
            return Results.BadRequest(new { error = "idempotency_key_required" });

        try
        {
            var result = await promotion.PromoteAsync(new TemporaryWorkflowPromotionCommand(
                workflowRecordId, operationId, actor.AccountId, request.SelectedPartyId,
                request.CreateNewParty, request.ConfirmDistinctParty, Guid.CreateVersion7()), cancellationToken);
            return Results.Ok(PersonsRegistrationMapper.Map(result));
        }
        catch (UnauthorizedAccessException)
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (TemporaryWorkflowOwnershipException)
        {
            return Results.NotFound();
        }
        catch (TemporaryWorkflowUnavailableException)
        {
            return Results.Conflict(new { error = "temporary_workflow_unavailable" });
        }
        catch (TemporaryWorkflowAlreadyPromotedException)
        {
            return Results.Conflict(new { error = "temporary_workflow_already_promoted" });
        }
        catch (TemporaryWorkflowPayloadException)
        {
            return Results.UnprocessableEntity(new { error = "temporary_workflow_payload_invalid" });
        }
        catch (IdempotencyKeyConflictException)
        {
            return Results.Conflict(new { error = "idempotency_key_conflict" });
        }
        catch (DuplicatePartyIdentifierException)
        {
            return Results.Conflict(new { error = "duplicate_party_identifier" });
        }
        catch (ArgumentException)
        {
            return Results.UnprocessableEntity(new { error = "validation_failed" });
        }
    });
}

app.Lifetime.ApplicationStopped.Register(() => logger.LogInformation("MD-Gsm API stopped."));
app.Run();

public sealed record SystemInfo(string Product, string ApiVersion, string ReleaseStage);

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var requestId = httpContext.TraceIdentifier;
        logger.LogError("Unhandled API exception. RequestId={RequestId}; ExceptionType={ExceptionType}; HResult={HResult}; StackTrace={StackTrace}", requestId, exception.GetType().FullName, exception.HResult, exception.StackTrace);
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["requestId"] = requestId;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}

public partial class Program;

public sealed record AdminSearchUnlockRequest(string Reason);

public sealed record SetPartyActiveRequest(bool IsActive, long ExpectedRowVersion, string Reason);
public sealed record SetContactPointActiveRequest(bool IsActive, long ExpectedRowVersion, string Reason);
public sealed record UpdateContactPointRequest(string Value, long ExpectedRowVersion, string Reason);
public sealed record SetAddressActiveRequest(bool IsActive, long ExpectedRowVersion, string Reason);
public sealed record UpdateAddressRequest(string CountryCode, string Province, string City, string PostalCode, string AddressLine, long ExpectedRowVersion, string Reason);

/// <summary>
/// Central authorization rule for global/rowless Party reads. Technician access is
/// denied until the API can enforce authoritative repair-assignment row scope.
/// </summary>
internal static class PersonsGlobalReadAuthorization
{
    public static async Task<bool> CanReadAsync(
        IRolePermissionStore permissions,
        AccountId accountId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        if (await permissions.HasRoleAsync(accountId, RoleCatalog.Technician, cancellationToken))
            return false;

        return await permissions.HasPermissionAsync(accountId, "persons.read", cancellationToken);
    }
}

