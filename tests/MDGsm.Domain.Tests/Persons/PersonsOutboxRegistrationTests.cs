using MDGsm.Persons.Application;
using MDGsm.Persons.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MDGsm.Domain.Tests.Persons;

public sealed class PersonsOutboxRegistrationTests
{
    [Fact]
    public void Infrastructure_registers_delivery_store_but_keeps_dispatcher_opt_in()
    {
        var services = new ServiceCollection();

        services.AddPersonsInfrastructure();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPersonsOutboxDeliveryStore));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(PersonsOutboxDispatcher));
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(IPersonsOutboxPublisher));
    }

    [Fact]
    public void Dispatcher_registration_is_rejected_without_an_explicit_publisher()
    {
        var services = new ServiceCollection();
        services.AddPersonsInfrastructure();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddPersonsOutboxDispatcher());

        Assert.Contains(nameof(IPersonsOutboxPublisher), exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(PersonsOutboxDispatcher));
    }

    [Fact]
    public void Dispatcher_registration_requires_and_uses_a_host_supplied_publisher()
    {
        var services = new ServiceCollection();
        services.AddPersonsInfrastructure();
        services.AddSingleton<IPersonsOutboxPublisher, TestPublisher>();

        services.AddPersonsOutboxDispatcher();

        var descriptor = Assert.Single(services.Where(
            item => item.ServiceType == typeof(PersonsOutboxDispatcher)));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.DoesNotContain(services, item =>
            item.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService) &&
            item.ImplementationType == typeof(PersonsOutboxDispatcher));
    }

    private sealed class TestPublisher : IPersonsOutboxPublisher
    {
        public Task PublishAsync(PersonsOutboxEvent message, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
