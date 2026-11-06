using MDGsm.Application.Persons;
using MDGsm.IdentityAccess.Application;
using MDGsm.Infrastructure.IdentityAccess;
using MDGsm.Infrastructure.Persons;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
namespace MDGsm.Infrastructure.Persistence;
public static class PersistenceRegistration
{
 public const string ConnectionStringName="MDGsmDatabase";
 public static IServiceCollection AddMdGsmPersistence(this IServiceCollection services,IConfiguration configuration)
 {
  var cs=configuration.GetConnectionString(ConnectionStringName);
  if(string.IsNullOrWhiteSpace(cs))return services;
  services.AddDbContext<MdGsmDbContext>(o=>o.UseNpgsql(cs,n=>n.EnableRetryOnFailure(maxRetryCount:3)));
  services.AddSingleton(_=>NpgsqlDataSource.Create(cs));
  services.AddScoped<IPartyRepository,PostgresPartyRepository>();
  services.AddScoped<IPartySensitiveDataAuthorizer,IdentityAccessPartySensitiveDataAuthorizer>();
  services.AddScoped<PostgresIdentityAccessStores>();
  services.AddScoped<IAccountStore>(s=>s.GetRequiredService<PostgresIdentityAccessStores>());
  services.AddScoped<ISessionStore>(s=>s.GetRequiredService<PostgresIdentityAccessStores>());
  services.AddScoped<ILoginAttemptStore>(s=>s.GetRequiredService<PostgresIdentityAccessStores>());
  services.AddScoped<IRolePermissionStore>(s=>s.GetRequiredService<PostgresIdentityAccessStores>());
  services.AddScoped<IAuditWriter>(s=>s.GetRequiredService<PostgresIdentityAccessStores>());
  services.AddScoped<ISystemPolicyStore>(s=>s.GetRequiredService<PostgresIdentityAccessStores>());
  services.AddScoped<IRecoveryStore>(s=>s.GetRequiredService<PostgresIdentityAccessStores>());
  services.AddScoped<IIdentityAccessUnitOfWork,IdentityAccessUnitOfWork>();
  return services;
 }
 public static IServiceCollection AddIdentityAccessSecurity(this IServiceCollection services, IConfiguration configuration)
 {
  ArgumentNullException.ThrowIfNull(configuration);
  var privacyDigest = new HmacPrivacyDigest(configuration);
  services.AddSingleton<IPasswordHasher,PlatformPasswordHasher>();
  services.AddSingleton<IAccessTokenFactory,OpaqueAccessTokenFactory>();
  services.AddSingleton<IPrivacyDigest>(privacyDigest);
  services.AddSingleton(TimeProvider.System);
  return services;
 }
}