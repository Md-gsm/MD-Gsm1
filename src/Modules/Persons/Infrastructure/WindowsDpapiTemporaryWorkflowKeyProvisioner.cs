using System.Runtime.Versioning;
using MDGsm.Persons.Application;

namespace MDGsm.Persons.Infrastructure;

/// <summary>Adapter that exposes one-time DPAPI provisioning through an Application port.</summary>
public sealed class WindowsDpapiTemporaryWorkflowKeyProvisioner : ITemporaryWorkflowKeyProvisioner
{
    [SupportedOSPlatform("windows")]
    public void ProvisionNewKeyForService(string keyFilePath, string serviceName) =>
        WindowsDpapiTemporaryWorkflowEncryptionKeyProvider.ProvisionNewKeyForService(keyFilePath, serviceName);
}
