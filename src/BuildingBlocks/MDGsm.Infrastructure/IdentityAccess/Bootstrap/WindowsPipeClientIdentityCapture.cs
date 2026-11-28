using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

namespace MDGsm.Infrastructure.IdentityAccess.Bootstrap;

/// <summary>
/// Server-derived evidence for one live Windows logon session and one API-service
/// process lifetime. Values supplied by a pipe client are never accepted here.
/// </summary>
public readonly record struct WindowsLogonSessionBinding(
    string UserSid,
    ulong AuthenticationId,
    uint TerminalServicesSessionId,
    Guid ServiceInstanceId)
{
    public WindowsLogonSessionBinding Validate()
    {
        if (string.IsNullOrWhiteSpace(UserSid) ||
            AuthenticationId == 0 ||
            ServiceInstanceId == Guid.Empty)
        {
            throw new InvalidOperationException("The authenticated Windows logon-session binding is incomplete.");
        }

        return this;
    }

    /// <summary>
    /// Continuation is allowed only for the exact same logon session and the same
    /// service process lifetime. A matching SID alone is deliberately insufficient.
    /// </summary>
    public bool IsExactContinuationOf(WindowsLogonSessionBinding original) =>
        UserSid.Equals(original.UserSid, StringComparison.Ordinal) &&
        AuthenticationId == original.AuthenticationId &&
        TerminalServicesSessionId == original.TerminalServicesSessionId &&
        ServiceInstanceId == original.ServiceInstanceId;
}

/// <summary>
/// State owned by one Named Pipe worker. A failed RevertToSelf poisons the worker;
/// no later privileged request may be dispatched on it.
/// </summary>
public sealed class WindowsPipeWorkerState
{
    private int _quarantined;

    public bool IsQuarantined => Volatile.Read(ref _quarantined) != 0;

    internal void Quarantine() => Interlocked.Exchange(ref _quarantined, 1);

    public void ThrowIfQuarantined()
    {
        if (IsQuarantined)
        {
            throw new PipeWorkerQuarantinedException(
                "This Named Pipe worker is quarantined after an impersonation cleanup failure.");
        }
    }
}

public sealed class PipeWorkerQuarantinedException(string message, Exception? inner = null)
    : InvalidOperationException(message, inner);

/// <summary>
/// Captures the authenticated client token for one complete Named Pipe message.
/// Impersonation is always reverted before this method returns. No operation is
/// performed under the service identity as a fallback for a failed capture.
/// </summary>
public static class WindowsPipeClientIdentityCapture
{
    private const uint TokenQuery = 0x0008;
    private const int ErrorInsufficientBuffer = 122;

    public static WindowsLogonSessionBinding Capture(
        SafePipeHandle pipeHandle,
        Guid serviceInstanceId,
        WindowsPipeWorkerState worker)
    {
        ArgumentNullException.ThrowIfNull(pipeHandle);
        ArgumentNullException.ThrowIfNull(worker);
        worker.ThrowIfQuarantined();

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("O-46 local bootstrap IPC is Windows-only.");
        }

        if (serviceInstanceId == Guid.Empty)
        {
            throw new ArgumentException("A non-empty service-instance identifier is required.", nameof(serviceInstanceId));
        }

        if (pipeHandle.IsInvalid || pipeHandle.IsClosed)
        {
            throw new ArgumentException("The Named Pipe handle is not open.", nameof(pipeHandle));
        }

        if (!ImpersonateNamedPipeClient(pipeHandle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                "The Named Pipe client could not be impersonated; the request was rejected.");
        }

        Exception? captureFailure = null;
        WindowsLogonSessionBinding binding = default;

        try
        {
            if (!OpenThreadToken(GetCurrentThread(), TokenQuery, true, out var token))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "The impersonated Named Pipe client token could not be opened.");
            }

            using (token)
            {
                var sid = ReadUserSid(token);
                var authenticationId = ReadAuthenticationId(token);
                var sessionId = ReadSessionId(token);
                binding = new WindowsLogonSessionBinding(
                    sid, authenticationId, sessionId, serviceInstanceId).Validate();
            }
        }
        catch (Exception exception)
        {
            captureFailure = exception;
        }

        // RevertToSelf must run even when token parsing or handle operations fail.
        if (!RevertToSelf())
        {
            var revertFailure = new Win32Exception(Marshal.GetLastWin32Error(),
                "RevertToSelf failed. The pipe worker must not process another request.");
            worker.Quarantine();
            throw new PipeWorkerQuarantinedException(
                "Named Pipe worker quarantined because impersonation could not be reverted.",
                captureFailure is null ? revertFailure : new AggregateException(captureFailure, revertFailure));
        }

        if (captureFailure is not null)
        {
            throw new InvalidOperationException(
                "The authenticated Named Pipe client identity could not be established; request denied.",
                captureFailure);
        }

        worker.ThrowIfQuarantined();
        return binding;
    }

    private static string ReadUserSid(SafeAccessTokenHandle token)
    {
        using var buffer = GetTokenInformationBuffer(token, TokenInformationClass.TokenUser);
        var user = Marshal.PtrToStructure<TokenUser>(buffer.DangerousGetHandle());
        if (user.User.Sid == IntPtr.Zero)
        {
            throw new InvalidOperationException("The client token has no user SID.");
        }

        return new SecurityIdentifier(user.User.Sid).Value;
    }

    private static ulong ReadAuthenticationId(SafeAccessTokenHandle token)
    {
        using var buffer = GetTokenInformationBuffer(token, TokenInformationClass.TokenStatistics);
        var statistics = Marshal.PtrToStructure<TokenStatistics>(buffer.DangerousGetHandle());
        var value = unchecked(((ulong)(uint)statistics.AuthenticationId.HighPart << 32) |
            statistics.AuthenticationId.LowPart);
        if (value == 0)
        {
            throw new InvalidOperationException("The client token has no valid logon AuthenticationId.");
        }

        return value;
    }

    private static uint ReadSessionId(SafeAccessTokenHandle token)
    {
        using var buffer = GetTokenInformationBuffer(token, TokenInformationClass.TokenSessionId);
        return unchecked((uint)Marshal.ReadInt32(buffer.DangerousGetHandle()));
    }

    private static SafeHGlobalBuffer GetTokenInformationBuffer(
        SafeAccessTokenHandle token,
        TokenInformationClass informationClass)
    {
        _ = GetTokenInformation(token, informationClass, IntPtr.Zero, 0, out var required);
        var error = Marshal.GetLastWin32Error();
        if (required == 0 || error != ErrorInsufficientBuffer)
        {
            throw new Win32Exception(error, "Windows did not provide the required token-information buffer size.");
        }

        var buffer = new SafeHGlobalBuffer(checked((int)required));
        if (!GetTokenInformation(token, informationClass, buffer.DangerousGetHandle(), required, out _))
        {
            var exception = new Win32Exception(Marshal.GetLastWin32Error(),
                "Windows token information could not be read.");
            buffer.Dispose();
            throw exception;
        }

        return buffer;
    }

    private sealed class SafeHGlobalBuffer : SafeHandle
    {
        public SafeHGlobalBuffer(int length) : base(IntPtr.Zero, true)
        {
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            SetHandle(Marshal.AllocHGlobal(length));
        }

        public override bool IsInvalid => handle == IntPtr.Zero;

        protected override bool ReleaseHandle()
        {
            Marshal.FreeHGlobal(handle);
            return true;
        }
    }

    private enum TokenInformationClass
    {
        TokenUser = 1,
        TokenStatistics = 10,
        TokenSessionId = 12
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SidAndAttributes
    {
        public IntPtr Sid;
        public uint Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenUser
    {
        public SidAndAttributes User;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Luid
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TokenStatistics
    {
        public Luid TokenId;
        public Luid AuthenticationId;
        public long ExpirationTime;
        public int TokenType;
        public int ImpersonationLevel;
        public uint DynamicCharged;
        public uint DynamicAvailable;
        public uint GroupCount;
        public uint PrivilegeCount;
        public Luid ModifiedId;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImpersonateNamedPipeClient(SafePipeHandle pipe);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenThreadToken(
        IntPtr threadHandle,
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool openAsSelf,
        out SafeAccessTokenHandle token);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(
        SafeAccessTokenHandle token,
        TokenInformationClass informationClass,
        IntPtr information,
        uint informationLength,
        out uint returnLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RevertToSelf();
}
