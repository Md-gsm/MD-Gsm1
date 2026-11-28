using MDGsm.Infrastructure.IdentityAccess.Bootstrap;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class O46WindowsLogonSessionBindingTests
{
    private static readonly Guid ServiceInstance = Guid.Parse("7d0f96c6-1b1d-4c8f-a5ab-1db5d2f15a90");

    [Fact]
    public void Exact_same_logon_session_and_service_instance_is_the_only_continuation()
    {
        var original = new WindowsLogonSessionBinding("S-1-5-21-100-200-300-1001",
            0x0000000100000042, 3, ServiceInstance);

        Assert.True(original.IsExactContinuationOf(original));
    }

    [Fact]
    public void Same_sid_with_a_different_authentication_luid_is_denied()
    {
        var original = new WindowsLogonSessionBinding("S-1-5-21-100-200-300-1001",
            0x0000000100000042, 3, ServiceInstance);
        var secondLogon = original with { AuthenticationId = 0x0000000100000043 };

        Assert.False(secondLogon.IsExactContinuationOf(original));
    }

    [Fact]
    public void Same_logon_luid_with_a_different_terminal_services_session_is_denied()
    {
        var original = new WindowsLogonSessionBinding("S-1-5-21-100-200-300-1001",
            0x0000000100000042, 3, ServiceInstance);
        var secondSession = original with { TerminalServicesSessionId = 4 };

        Assert.False(secondSession.IsExactContinuationOf(original));
    }

    [Fact]
    public void Service_restart_changes_the_binding_and_blocks_automatic_continuation()
    {
        var original = new WindowsLogonSessionBinding("S-1-5-21-100-200-300-1001",
            0x0000000100000042, 3, ServiceInstance);
        var restartedService = original with { ServiceInstanceId = Guid.NewGuid() };

        Assert.False(restartedService.IsExactContinuationOf(original));
    }

    [Fact]
    public void Incomplete_or_uninitialized_binding_is_rejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new WindowsLogonSessionBinding("", 0, 0, Guid.Empty).Validate());
    }
}
