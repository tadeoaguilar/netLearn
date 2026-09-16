namespace FunctionApi.KeyVault;

public interface ISecretReader
{
    Task<string> GetSecretValueAsync(string secretName, CancellationToken cancellationToken);
}
