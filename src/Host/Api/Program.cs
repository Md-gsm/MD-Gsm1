using MDGsm.Api.Security;
using MDGsm.Application.Contracts;
using MDGsm.Application.Persons;
using MDGsm.IdentityAccess.Application;
using MDGsm.IdentityAccess.Contracts;
using MDGsm.IdentityAccess.Domain;
using MDGsm.Infrastructure.IdentityAccess;
using MDGsm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
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
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IAuthenticatedActorContext, HttpAuthenticatedActorContext>();

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
    var source = context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? "unknown";
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

app.MapGet("/api/v1/persons/{partyId}", async (
    string partyId,
    bool? includeSensitive,
    IPartyRepository parties,
    IAuthenticatedActorContext actor,
    IRolePermissionStore permissions,
    CancellationToken cancellationToken) =>
{
    if (!await permissions.HasPermissionAsync(
        AccountId.From(actor.AccountId), "persons.read", cancellationToken))
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    try
    {
        var details = await parties.GetAsync(
            partyId, actor.AccountId.ToString("D"), includeSensitive ?? false, null, cancellationToken);
        return details is null ? Results.NotFound() : Results.Ok(details);
    }
    catch (UnauthorizedAccessException)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }
});

app.Lifetime.ApplicationStopped.Register(() => logger.LogInformation("MD-Gsm API stopped."));
app.Run();

public sealed record SystemInfo(string Product, string ApiVersion, string ReleaseStage);

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var requestId = httpContext.TraceIdentifier;
        logger.LogError("Unhandled API exception. RequestId={RequestId}; ExceptionType={ExceptionType}", requestId, exception.GetType().Name);
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
