using System.IO.Pipes;
using System.Security.Principal;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using MDGsm.Infrastructure.IdentityAccess.Bootstrap;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

[SupportedOSPlatform("windows")]
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
                O46LocalNamedPipeServer.Create(new O46ServiceProcessIdentity()));
            Assert.True(exception.NativeErrorCode is 5 or 231,
                $"Expected pipe-name collision to reject the first instance; Win32 error was {exception.NativeErrorCode}."); // ACCESS_DENIED / PIPE_BUSY
        }

        var serviceProcessIdentity = new O46ServiceProcessIdentity();
        using var server = O46LocalNamedPipeServer.Create(serviceProcessIdentity);
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
            Assert.Throws<InvalidOperationException>(() => server.ReadAuthenticatedRequest());
        }

        Assert.Equal(expectedSid, firstBinding.UserSid);
        Assert.NotEqual(0UL, firstBinding.AuthenticationId);
        Assert.Equal((uint)Process.GetCurrentProcess().SessionId, firstBinding.TerminalServicesSessionId);
        Assert.Equal(serviceProcessIdentity.ServiceInstanceId, firstBinding.ServiceInstanceId);

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

    [Theory]
    [InlineData(0u)]
    [InlineData((uint)(O46LocalNamedPipeServer.DefaultMaximumPayloadBytes + 1))]
    [Trait("Category", "Integration")]
    public async Task Invalid_frame_lengths_are_rejected_before_client_identity_is_accepted(uint payloadLength)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var serviceProcessIdentity = new O46ServiceProcessIdentity();
        using var server = O46LocalNamedPipeServer.Create(serviceProcessIdentity);
        var accept = Task.Run(server.WaitForClient);

        using var client = new NamedPipeClientStream(
            ".", O46LocalNamedPipeServer.PipeName, PipeDirection.Out, PipeOptions.None);
        client.Connect(10_000);
        await accept.WaitAsync(TimeSpan.FromSeconds(10));

        var header = new byte[sizeof(uint)];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(header, payloadLength);
        await client.WriteAsync(header);
        await client.FlushAsync();

        Assert.Throws<InvalidDataException>(() => server.ReadAuthenticatedRequest());
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
