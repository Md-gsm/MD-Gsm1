using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Runtime.Versioning;
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
    public bool IsExactContinuationOf(WindowsLogonSessionBinding original)
    {
        // A default/partially deserialized binding must never match another
        // default value. Treat invalid evidence as a denial, not an exception
        // and never use SID-only equality as a fallback.
        try
        {
            Validate();
            original.Validate();
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return string.Equals(UserSid, original.UserSid, StringComparison.Ordinal) &&
            AuthenticationId == original.AuthenticationId &&
            TerminalServicesSessionId == original.TerminalServicesSessionId &&
            ServiceInstanceId == original.ServiceInstanceId;
    }
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
[SupportedOSPlatform("windows")]
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
                var statistics = ReadTokenStatistics(token);
                ValidateElevatedClientToken(token, statistics);

                var sid = ReadUserSid(token);
                var authenticationId = ReadAuthenticationId(statistics);
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

    private static TokenStatistics ReadTokenStatistics(SafeAccessTokenHandle token)
    {
        using var buffer = GetTokenInformationBuffer(token, TokenInformationClass.TokenStatistics);
        return Marshal.PtrToStructure<TokenStatistics>(buffer.DangerousGetHandle());
    }

    private static ulong ReadAuthenticationId(TokenStatistics statistics)
    {
        var value = unchecked(((ulong)(uint)statistics.AuthenticationId.HighPart << 32) |
            statistics.AuthenticationId.LowPart);
        if (value == 0)
        {
            throw new InvalidOperationException("The client token has no valid logon AuthenticationId.");
        }

        return value;
    }

    private static void ValidateElevatedClientToken(
        SafeAccessTokenHandle token,
        TokenStatistics statistics)
    {
        const int TokenImpersonation = 2;
        const int SecurityImpersonation = 2;

        if (statistics.TokenType != TokenImpersonation ||
            statistics.ImpersonationLevel != SecurityImpersonation)
        {
            throw new UnauthorizedAccessException(
                "The pipe client token is not a local SecurityImpersonation token; the request was rejected.");
        }

        // Check the effective group membership, not TOKEN_ELEVATION. The
        // protected pipe DACL grants access to the local Administrators SID;
        // CheckTokenMembership rejects UAC deny-only/filtered group entries.
        if (!IsEnabledAdministratorMember(token))
        {
            throw new UnauthorizedAccessException(
                "The O-46 client token does not have enabled local Administrators membership.");
        }

        var isAppContainer = ReadTokenDword(token, TokenInformationClass.TokenIsAppContainer);
        if (isAppContainer != 0)
        {
            throw new UnauthorizedAccessException(
                "The O-46 client token belongs to an AppContainer.");
        }
    }

    private static bool IsEnabledAdministratorMember(SafeAccessTokenHandle token)
    {
        var administratorsSid = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var sidBytes = new byte[administratorsSid.BinaryLength];
        administratorsSid.GetBinaryForm(sidBytes, 0);
        var sidPointer = Marshal.AllocHGlobal(sidBytes.Length);

        try
        {
            Marshal.Copy(sidBytes, 0, sidPointer, sidBytes.Length);
            if (!CheckTokenMembership(token, sidPointer, out var isMember))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(),
                    "Effective local Administrators membership could not be verified.");
            }

            return isMember;
        }
        finally
        {
            Marshal.FreeHGlobal(sidPointer);
        }
    }

    private static uint ReadTokenDword(
        SafeAccessTokenHandle token,
        TokenInformationClass informationClass)
    {
        using var buffer = GetTokenInformationBuffer(token, informationClass);
        return unchecked((uint)Marshal.ReadInt32(buffer.DangerousGetHandle()));
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
        // Most queried token-information classes in this binding are fixed-size
        // DWORDs (or the fixed TOKEN_STATISTICS structure). Querying their size
        // first is unnecessary and can be rejected by some impersonation-token
        // implementations. Allocate the documented structure size directly.
        var fixedSize = informationClass switch
        {
            TokenInformationClass.TokenStatistics => Marshal.SizeOf<TokenStatistics>(),
            TokenInformationClass.TokenUser => 0,
            _ => sizeof(uint)
        };

        if (fixedSize > 0)
        {
            var fixedBuffer = new SafeHGlobalBuffer(fixedSize);
            if (!GetTokenInformation(
                    token, informationClass, fixedBuffer.DangerousGetHandle(),
                    checked((uint)fixedSize), out var returnedLength))
            {
                var error = Marshal.GetLastWin32Error();
                fixedBuffer.Dispose();
                throw new Win32Exception(error,
                    $"Windows token information '{informationClass}' could not be read.");
            }

            if (returnedLength == 0 || returnedLength > fixedSize)
            {
                fixedBuffer.Dispose();
                throw new InvalidOperationException(
                    $"Windows returned an invalid size for token information '{informationClass}'.");
            }

            return fixedBuffer;
        }

        // TOKEN_USER is variable-sized because it embeds a SID. Use the
        // documented insufficient-buffer sizing call only for this structure.
        _ = GetTokenInformation(token, informationClass, IntPtr.Zero, 0, out var required);
        var sizingError = Marshal.GetLastWin32Error();
        if (required == 0 || required > 64 * 1024 || sizingError != ErrorInsufficientBuffer)
        {
            throw new Win32Exception(sizingError,
                $"Windows did not provide a valid TOKEN_USER buffer size (required={required}).");
        }

        var buffer = new SafeHGlobalBuffer(checked((int)required));
        if (!GetTokenInformation(token, informationClass, buffer.DangerousGetHandle(), required, out var actualLength))
        {
            var exception = new Win32Exception(Marshal.GetLastWin32Error(),
                "Windows TOKEN_USER information could not be read.");
            buffer.Dispose();
            throw exception;
        }

        if (actualLength == 0 || actualLength > required)
        {
            buffer.Dispose();
            throw new InvalidOperationException("Windows returned an invalid TOKEN_USER size.");
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
        TokenSessionId = 12,
        TokenIsAppContainer = 29
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
    private static extern bool CheckTokenMembership(
        SafeAccessTokenHandle tokenHandle,
        IntPtr sidToCheck,
        [MarshalAs(UnmanagedType.Bool)] out bool isMember);

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
