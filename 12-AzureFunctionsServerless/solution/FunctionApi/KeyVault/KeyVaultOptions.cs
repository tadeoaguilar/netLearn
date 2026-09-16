namespace FunctionApi.KeyVault;

public class KeyVaultOptions
{
    public required string VaultUri { get; set; }
    public required string SecretName { get; set; }
}
