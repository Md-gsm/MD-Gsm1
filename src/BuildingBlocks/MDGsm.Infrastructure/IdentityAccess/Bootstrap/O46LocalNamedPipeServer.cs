using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace MDGsm.Infrastructure.IdentityAccess.Bootstrap;

public sealed record O46AuthenticatedPipeRequest(
    byte[] Payload,
    WindowsLogonSessionBinding ClientBinding);

/// <summary>
/// One-instance, local-only O-46 bootstrap pipe. The pipe accepts one bounded
/// length-prefixed request per connection and captures the client token only
/// after the complete request frame has been consumed.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class O46LocalNamedPipeServer : IDisposable
{
    public const string PipeName = "MDGsm.O46.OwnerBootstrap.v1";
    public const int DefaultMaximumPayloadBytes = 64 * 1024;

    private const string PipePath = @"\\.\pipe\" + PipeName;
    // The Setup Service owns the server handle; only an elevated local
    // administrator needs client access. Do not grant SYSTEM client access.
    private const string PipeSecuritySddl = "D:P(A;;GA;;;BA)";
    private const uint PipeAccessDuplex = 0x00000003;
    private const uint FileFlagFirstPipeInstance = 0x00080000;
    private const uint PipeRejectRemoteClients = 0x00000008;
    private const uint ErrorPipeConnected = 535;
    private const uint PipeBufferBytes = 4096;

    private readonly SafePipeHandle _pipeHandle;
    private readonly Guid _serviceInstanceId;
    private readonly WindowsPipeWorkerState _workerState = new();
    private int _connected;
    private int _frameConsumed;
    private int _disposed;

    private O46LocalNamedPipeServer(SafePipeHandle pipeHandle, Guid serviceInstanceId)
    {
        _pipeHandle = pipeHandle;
        _serviceInstanceId = serviceInstanceId;
    }

    /// <summary>
    /// Creates the fixed O-46 endpoint with a protected DACL granting client
    /// access only to the local Administrators group. The service retains its
    /// server handle; SYSTEM is not authorized as a pipe client.
    /// </summary>
    public static O46LocalNamedPipeServer Create(Guid serviceInstanceId)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("O-46 local bootstrap IPC is Windows-only.");
        }

        if (serviceInstanceId == Guid.Empty)
        {
            throw new ArgumentException("A non-empty service-instance identifier is required.", nameof(serviceInstanceId));
        }

        if (!ConvertStringSecurityDescriptorToSecurityDescriptor(
                PipeSecuritySddl, 1, out var securityDescriptor, out _))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "The O-46 Named Pipe security descriptor could not be created.");
        }

        try
        {
            var attributes = new SecurityAttributes
            {
                Length = (uint)Marshal.SizeOf<SecurityAttributes>(),
                SecurityDescriptor = securityDescriptor,
                InheritHandle = false
            };

            var handle = CreateNamedPipe(
                PipePath,
                PipeAccessDuplex | FileFlagFirstPipeInstance,
                PipeRejectRemoteClients,
                1,
                PipeBufferBytes,
                PipeBufferBytes,
                0,
                ref attributes);

            if (handle.IsInvalid)
            {
                var error = Marshal.GetLastWin32Error();
                handle.Dispose();
                throw new Win32Exception(error,
                    "The first-instance O-46 local Named Pipe could not be created. The request was not exposed.");
            }

            return new O46LocalNamedPipeServer(handle, serviceInstanceId);
        }
        finally
        {
            _ = LocalFree(securityDescriptor);
        }
    }

    /// <summary>
    /// Blocks until one local client connects. The handle is created with
    /// FILE_FLAG_FIRST_PIPE_INSTANCE and only one pipe instance is permitted.
    /// </summary>
    public void WaitForClient()
    {
        ThrowIfDisposed();
        _workerState.ThrowIfQuarantined();

        if (Interlocked.CompareExchange(ref _connected, 1, 0) != 0)
        {
            throw new InvalidOperationException("The O-46 pipe already has a connected client.");
        }

        Interlocked.Exchange(ref _frameConsumed, 0);

        if (ConnectNamedPipe(_pipeHandle, IntPtr.Zero))
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        if (error == ErrorPipeConnected)
        {
            // The client connected between CreateNamedPipe and ConnectNamedPipe.
            return;
        }

        Interlocked.Exchange(ref _connected, 0);
        throw new Win32Exception(error, "A client could not be connected to the O-46 local Named Pipe.");
    }

    /// <summary>
    /// Reads exactly one frame: a 4-byte unsigned little-endian byte count
    /// followed by that many payload bytes. Zero-length, oversized, truncated
    /// and repeated frames on one connection are rejected. Identity capture
    /// occurs after the complete frame is read and before the payload is returned.
    /// </summary>
    public O46AuthenticatedPipeRequest ReadAuthenticatedRequest(
        int maximumPayloadBytes = DefaultMaximumPayloadBytes)
    {
        ThrowIfDisposed();
        _workerState.ThrowIfQuarantined();

        if (Volatile.Read(ref _connected) == 0)
        {
            throw new InvalidOperationException("No O-46 Named Pipe client is connected.");
        }

        if (maximumPayloadBytes is < 1 or > DefaultMaximumPayloadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPayloadBytes),
                $"The maximum O-46 request size must be between 1 and {DefaultMaximumPayloadBytes} bytes.");
        }

        if (Interlocked.Exchange(ref _frameConsumed, 1) != 0)
        {
            throw new InvalidOperationException("Only one O-46 request frame is accepted per pipe connection.");
        }

        Span<byte> header = stackalloc byte[sizeof(uint)];
        ReadExactly(header);
        var payloadLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(header);

        if (payloadLength == 0 || payloadLength > (uint)maximumPayloadBytes)
        {
            throw new InvalidDataException("The O-46 request frame length is invalid or exceeds the configured limit.");
        }

        var payload = new byte[checked((int)payloadLength)];
        ReadExactly(payload);

        // This method must run on a clean worker thread after the complete client
        // frame has been read. RevertToSelf is mandatory inside the capture path.
        var binding = WindowsPipeClientIdentityCapture.Capture(
            _pipeHandle, _serviceInstanceId, _workerState);

        return new O46AuthenticatedPipeRequest(payload, binding);
    }

    /// <summary>
    /// Ends the current connection. The pipe handle remains owned by this
    /// instance so the service may accept a later reconnect in the same process.
    /// </summary>
    public void DisconnectClient()
    {
        ThrowIfDisposed();
        _workerState.ThrowIfQuarantined();

        if (Interlocked.Exchange(ref _connected, 0) == 0)
        {
            return;
        }

        if (!DisconnectNamedPipe(_pipeHandle))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != 233) // ERROR_PIPE_NOT_CONNECTED: client already disconnected.
            {
                throw new Win32Exception(error, "The O-46 Named Pipe client could not be disconnected.");
            }
        }
    }

    private void ReadExactly(Span<byte> destination)
    {
        var offset = 0;
        var buffer = new byte[destination.Length];

        while (offset < destination.Length)
        {
            var requested = (uint)(destination.Length - offset);
            if (!ReadFile(_pipeHandle, buffer, requested, out var read, IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "The O-46 Named Pipe request frame could not be read completely.");
            }

            if (read == 0)
            {
                throw new EndOfStreamException("The O-46 client disconnected before the complete request frame arrived.");
            }

            buffer.AsSpan(0, checked((int)read)).CopyTo(destination[offset..]);
            offset += checked((int)read);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _pipeHandle.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public uint Length;
        public IntPtr SecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)]
        public bool InheritHandle;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true,
        EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
        string stringSecurityDescriptor,
        uint stringSDRevision,
        out IntPtr securityDescriptor,
        out uint securityDescriptorSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true,
        EntryPoint = "CreateNamedPipeW")]
    private static extern SafePipeHandle CreateNamedPipe(
        string name,
        uint openMode,
        uint pipeMode,
        uint maximumInstances,
        uint outputBufferSize,
        uint inputBufferSize,
        uint defaultTimeout,
        ref SecurityAttributes securityAttributes);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConnectNamedPipe(SafePipeHandle pipeHandle, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DisconnectNamedPipe(SafePipeHandle pipeHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(
        SafePipeHandle file,
        [Out] byte[] buffer,
        uint bytesToRead,
        out uint bytesRead,
        IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr memory);
}
