namespace FunctionApi.Security;

// Bound from configuration ("EntraId" section). Every value here is public
// information about the App Registration -- a tenant ID, an audience, a
// role name -- none of it is a secret, which is why local.settings.json.example
// can be checked in with real-looking values and nothing needs redacting.
public class EntraIdOptions
{
    public required string TenantId { get; set; }
    public required string Audience { get; set; }
    public required string RequiredAppRole { get; set; }

    public string Authority => $"https://login.microsoftonline.com/{TenantId}/v2.0";
    public string MetadataAddress => $"{Authority}/.well-known/openid-configuration";
}
