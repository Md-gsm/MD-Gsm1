using System.Runtime.Versioning;
using MDGsm.Infrastructure.IdentityAccess.Bootstrap;
using Microsoft.Extensions.Hosting;

namespace MDGsm.Api;

/// <summary>
/// Owns the O-46 local pipe handle for the actual Windows Service process
/// lifetime. This stage deliberately does not accept connections or dispatch
/// requests; the Bootstrap protocol remains inactive until its separate gates
/// and authorization are complete.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class O46PipeEndpointLifecycleService(
    O46ServiceProcessIdentity serviceProcessIdentity,
    ILogger<O46PipeEndpointLifecycleService> logger) : IHostedService, IDisposable
{
    private O46LocalNamedPipeServer? _endpoint;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _endpoint = O46LocalNamedPipeServer.Create(serviceProcessIdentity);

        logger.LogInformation(
            "O-46 local pipe object created for this Windows Service process. PipeName={PipeName}; ServiceInstanceId={ServiceInstanceId}; ProcessId={ProcessId}; RequestAdmission=disabled; BootstrapDispatch=disabled",
            O46LocalNamedPipeServer.PipeName,
            serviceProcessIdentity.ServiceInstanceId,
            Environment.ProcessId);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        ReleaseEndpoint();
        logger.LogInformation(
            "O-46 local pipe object released as the Windows Service process stops. ProcessId={ProcessId}",
            Environment.ProcessId);
        return Task.CompletedTask;
    }

    public void Dispose() => ReleaseEndpoint();

    private void ReleaseEndpoint()
    {
        Interlocked.Exchange(ref _endpoint, null)?.Dispose();
    }
}
