using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Options;

namespace FunctionApi.KeyVault;

// No connection string, no access key, nowhere in configuration. DefaultAzureCredential
// uses the Function App's system-assigned managed identity when running in
// Azure (falls back to Azure CLI / Visual Studio / environment credentials
// for local development, in that order -- see EXERCISE.md Part 3.4 for how
// to authenticate locally without ever touching a Key Vault access key).
// The Function's identity still needs the "Key Vault Secrets User" RBAC
// role on this vault -- see infra/modules/keyvault.bicep -- Managed
// Identity gets you WHO the caller is, RBAC decides WHAT they can do.
public class KeyVaultSecretReader : ISecretReader
{
    private readonly SecretClient _client;

    public KeyVaultSecretReader(IOptions<KeyVaultOptions> options)
    {
        _client = new SecretClient(new Uri(options.Value.VaultUri), new DefaultAzureCredential());
    }

    public async Task<string> GetSecretValueAsync(string secretName, CancellationToken cancellationToken)
    {
        var secret = await _client.GetSecretAsync(secretName, cancellationToken: cancellationToken);
        return secret.Value.Value;
    }
}
