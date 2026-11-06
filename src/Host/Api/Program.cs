using MDGsm.Api.Security;
using MDGsm.Application.Contracts;
using MDGsm.Application.Persons;
using MDGsm.IdentityAccess.Application;
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
builder.Services.AddSingleton(TimeProvider.System);
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

app.MapGet("/api/v1/persons/{partyId}", async (
    string partyId,
    bool? includeSensitive,
    IPartyRepository parties,
    IAuthenticatedActorContext actor,
    CancellationToken cancellationToken) =>
{
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
