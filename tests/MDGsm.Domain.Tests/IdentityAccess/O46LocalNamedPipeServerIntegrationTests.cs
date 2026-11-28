using System.IO.Pipes;
using System.Security.Principal;
using System.ComponentModel;
using System.Diagnostics;
using MDGsm.Infrastructure.IdentityAccess.Bootstrap;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class O46LocalNamedPipeServerIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task First_instance_blocks_pipe_squatting_and_captures_same_session_reconnect()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Simulate an untrusted process pre-creating the well-known pipe name.
        // FILE_FLAG_FIRST_PIPE_INSTANCE must make the production server fail closed.
        using (var squattedPipe = new NamedPipeServerStream(
            O46LocalNamedPipeServer.PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte))
        {
            var exception = Assert.Throws<Win32Exception>(() =>
                O46LocalNamedPipeServer.Create(Guid.NewGuid()));
            Assert.Equal(5, exception.NativeErrorCode); // ERROR_ACCESS_DENIED
        }

        var serviceInstanceId = Guid.NewGuid();
        using var server = O46LocalNamedPipeServer.Create(serviceInstanceId);
        var expectedSid = WindowsIdentity.GetCurrent().User?.Value;
        Assert.False(string.IsNullOrWhiteSpace(expectedSid));

        WindowsLogonSessionBinding firstBinding;
        var acceptFirst = Task.Run(server.WaitForClient);
        using (var firstClient = new NamedPipeClientStream(
            ".", O46LocalNamedPipeServer.PipeName, PipeDirection.Out, PipeOptions.None))
        {
            firstClient.Connect(10_000);
            await acceptFirst.WaitAsync(TimeSpan.FromSeconds(10));
            await WriteFrameAsync(firstClient, [0x4f, 0x34, 0x36, 0x01]);

            var request = server.ReadAuthenticatedRequest();
            Assert.Equal(new byte[] { 0x4f, 0x34, 0x36, 0x01 }, request.Payload);
            firstBinding = request.ClientBinding;
        }

        Assert.Equal(expectedSid, firstBinding.UserSid);
        Assert.NotEqual(0UL, firstBinding.AuthenticationId);
        Assert.Equal((uint)Process.GetCurrentProcess().SessionId, firstBinding.TerminalServicesSessionId);
        Assert.Equal(serviceInstanceId, firstBinding.ServiceInstanceId);

        server.DisconnectClient();

        var acceptReconnect = Task.Run(server.WaitForClient);
        using (var reconnectClient = new NamedPipeClientStream(
            ".", O46LocalNamedPipeServer.PipeName, PipeDirection.Out, PipeOptions.None))
        {
            reconnectClient.Connect(10_000);
            await acceptReconnect.WaitAsync(TimeSpan.FromSeconds(10));
            await WriteFrameAsync(reconnectClient, [0x4f, 0x34, 0x36, 0x02]);

            var reconnect = server.ReadAuthenticatedRequest();
            Assert.Equal(new byte[] { 0x4f, 0x34, 0x36, 0x02 }, reconnect.Payload);
            Assert.True(reconnect.ClientBinding.IsExactContinuationOf(firstBinding));
        }

        server.DisconnectClient();
    }

    private static async Task WriteFrameAsync(Stream client, byte[] payload)
    {
        var header = new byte[sizeof(uint)];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)payload.Length);
        await client.WriteAsync(header);
        await client.WriteAsync(payload);
        await client.FlushAsync();
    }
}
