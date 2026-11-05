using MDGsm.Finance.Application;
using MDGsm.Finance.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace MDGsm.Finance.Infrastructure;

/// <summary>
/// Registers Finance account provisioning. The host must separately provide
/// IFinanceActorContext and IPartyIdentityLookup from authenticated host/Persons boundaries.
/// </summary>
public static class FinanceModuleRegistration
{
    public static IServiceCollection AddFinanceModule(this IServiceCollection services)
    {
        services.AddScoped<IPartyAccountRepository, PostgresPartyAccountRepository>();
        services.AddScoped<IAccountsReceivableAccountProvisioner, AccountsReceivableAccountProvisioner>();
        return services;
    }
}
