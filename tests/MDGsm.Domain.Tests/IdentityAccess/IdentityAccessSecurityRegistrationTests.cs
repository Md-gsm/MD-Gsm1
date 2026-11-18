using MDGsm.IdentityAccess.Application;
using MDGsm.Infrastructure.IdentityAccess;
using MDGsm.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MDGsm.Domain.Tests.IdentityAccess;

public sealed class IdentityAccessSecurityRegistrationTests
{
 [Fact] public void Registration_requires_privacy_secret_and_keeps_token_and_privacy_adapters_separate()
 {
  var services = new ServiceCollection();
  Assert.Throws<InvalidOperationException>(() => services.AddIdentityAccessSecurity(new ConfigurationBuilder().Build()));
  var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
  {
   [HmacPrivacyDigest.ConfigurationKey] = Convert.ToBase64String(Enumerable.Range(1,32).Select(x => (byte)x).ToArray())
  }).Build();
  services.AddIdentityAccessSecurity(config);
  using var provider = services.BuildServiceProvider();
  Assert.IsType<OpaqueAccessTokenFactory>(provider.GetRequiredService<IAccessTokenFactory>());
  Assert.IsType<HmacPrivacyDigest>(provider.GetRequiredService<IPrivacyDigest>());
 }
}