using System.Net.Http.Headers;
using Microsoft.Extensions.Hosting;
using Npgsql;
using MDGsm.Api.Security;
using MDGsm.Api.Persons;
using MDGsm.Application.Contracts;
using MDGsm.Application.Persons;
using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Contracts;
using MDGsm.IdentityAccess.Domain;
using MDGsm.Domain.Persons;
using MDGsm.Infrastructure.IdentityAccess;
using MDGsm.Infrastructure.Persons;
using MDGsm.Finance.Contracts;
using MDGsm.Finance.Infrastructure.Accounts;
using MDGsm.Persons.Contracts;
using MDGsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

if (args.Length > 0 && string.Equals(args[0], "--provision-persons-workflow-key", StringComparison.Ordinal))
{
    if (args.Length != 3)
        throw new ArgumentException("Expected the protected key path and service name for one-time provisioning.");
    if (!OperatingSystem.IsWindows())
        throw new PlatformNotSupportedException("Persons workflow key provisioning is Windows-only.");

    WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKeyForService(args[1], args[2]);
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService(options =>
    options.ServiceName = WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ApiServiceName);
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
builder.Services.AddMdGsmPersistence(builder.Configuration);
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
builder.Services.AddScoped<PostgresPartyMergeService>();

var app = builder.Build();
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

    var db = services.GetService<MdGsmDbContext>();
    if (db is null)
        return (IResult)Results.Json(new { status = "not-ready", reason = "database-service-not-registered" }, statusCode: StatusCodes.Status503ServiceUnavailable);

    try
    {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? (IResult)Results.Ok(new { status = "ready", dependency = "postgresql" })
            : Results.Json(new { status = "not-ready", reason = "database-unreachable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
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


// Every public create path performs duplicate resolution. There is intentionally no
// lower-level HTTP route that can bypass the quick-registration decision or ForceNew validation.
app.MapPost("/api/v2/persons", async (
    QuickRegisterNaturalPartyRequest request, HttpContext context, IConfiguration configuration,
    NpgsqlDataSource dataSource, IAuthenticatedActorContext actor, IRolePermissionStore permissions,
    IPartyRepository parties, CancellationToken cancellationToken) =>
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
    NpgsqlDataSource dataSource, IAuthenticatedActorContext actor, IRolePermissionStore permissions,
    IPartyRepository parties, CancellationToken cancellationToken) =>
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
    IPartyRepository parties,
    IAuthenticatedActorContext actor,
    IRolePermissionStore permissions,
    CancellationToken cancellationToken) =>
{
    var accountId = AccountId.From(actor.AccountId);
    // Technician row scope depends on authoritative repair-assignment ownership,
    // which is not yet available in this integration candidate. Fail closed rather
    // than treating the broad persons.read grant as permission to read every party.
    if (await permissions.HasRoleAsync(accountId, RoleCatalog.Technician, cancellationToken))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    if (!await permissions.HasPermissionAsync(accountId, "persons.read", cancellationToken))
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
            var candidates = result.Candidates.Select(candidate => new PartyQuickRegistrationCandidateContract(
                candidate.PartyId,
                candidate.Type == PartyType.NaturalPerson ? "NaturalPerson" : "LegalEntity",
                candidate.DisplayName,
                new[]
                {
                    (candidate.Roles & PartyBusinessRoles.Customer) == PartyBusinessRoles.Customer ? "Customer" : null,
                    (candidate.Roles & PartyBusinessRoles.Colleague) == PartyBusinessRoles.Colleague ? "Colleague" : null
                }.Where(role => role is not null).Select(role => role!).ToArray(),
                candidate.PartyIsActive, candidate.IsMerged, candidate.HasActiveMobile, candidate.MaskedMobile)).ToArray();
            return Results.Ok(new TemporaryWorkflowPromotionResponse(
                result.Outcome.ToString(), result.PartyId, result.Created, result.Replayed, candidates));
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
