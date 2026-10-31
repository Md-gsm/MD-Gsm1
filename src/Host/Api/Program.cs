using MDGsm.Application.Contracts;
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

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
var logger = app.Logger;
logger.LogInformation("MD-Gsm API starting. Database, authentication, and accounting workflows are not configured.");

app.MapGet("/health/live", () => Results.Ok(new
{
    product = "MD-Gsm",
    status = "alive",
    scope = "process-liveness-only"
}));

app.MapGet("/health/ready", async (IServiceProvider services, IConfiguration configuration, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(configuration.GetConnectionString(PersistenceRegistration.ConnectionStringName)))
    {
        return (IResult)Results.Json(new { status = "not-ready", reason = "database-not-configured" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var db = services.GetService<MdGsmDbContext>();
    if (db is null)
    {
        return (IResult)Results.Json(new { status = "not-ready", reason = "database-service-not-registered" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        if (canConnect)
        {
            return (IResult)Results.Ok(new { status = "ready", dependency = "postgresql" });
        }

        return (IResult)Results.Json(new { status = "not-ready", reason = "database-unreachable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        app.Logger.LogWarning("Database readiness check failed. ExceptionType={ExceptionType}", ex.GetType().Name);
        return (IResult)Results.Json(new { status = "not-ready", reason = "database-unreachable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapGet("/api/v1/system/info", (HttpContext context) =>
    Results.Ok(new ApiResponse<SystemInfo>(new SystemInfo("MD-Gsm", "v1", "foundation"), context.TraceIdentifier)));

app.Lifetime.ApplicationStopped.Register(() => logger.LogInformation("MD-Gsm API stopped."));
app.Run();

public sealed record SystemInfo(string Product, string ApiVersion, string ReleaseStage);

public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var requestId = httpContext.TraceIdentifier;
        // Never return exception details or log potentially sensitive request payloads.
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
