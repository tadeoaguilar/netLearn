using System.Net;
using FunctionApi.KeyVault;
using FunctionApi.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Options;

namespace FunctionApi.Functions;

// Proves the Function can read its own secret with no key or connection
// string anywhere in its configuration -- see KeyVaultSecretReader for the
// DefaultAzureCredential + managed identity mechanism, and
// infra/modules/keyvault.bicep for the RBAC role assignment that makes it
// actually work once deployed. Requires a valid token like every other
// secured endpoint -- Managed Identity secures the Function's OWN
// credentials, it says nothing about who's allowed to call the Function.
public class ConfigCheckFunction(ISecretReader secretReader, IOptions<KeyVaultOptions> keyVaultOptions)
{
    [Function("ConfigCheck")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "secure/config-check")] HttpRequestData req,
        FunctionContext context)
    {
        if (context.GetUser() is null)
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var secretValue = await secretReader.GetSecretValueAsync(
            keyVaultOptions.Value.SecretName, context.CancellationToken);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            message = "Read this value from Key Vault using the Function's managed identity -- no secret in config.",
            secretLength = secretValue.Length,
            secretPreview = secretValue.Length > 4 ? $"{secretValue[..2]}...{secretValue[^2..]}" : "***"
        });
        return response;
    }
}
