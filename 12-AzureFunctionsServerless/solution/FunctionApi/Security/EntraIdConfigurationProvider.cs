using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace FunctionApi.Security;

// Fetches Entra ID's OpenID Connect discovery document -- which includes
// the JWKS (JSON Web Key Set, the public signing keys) -- and caches it.
// This is the actual mechanism behind "the platform validates the token for
// you" in frameworks like Easy Auth or Microsoft.Identity.Web; here it's
// explicit so you can see it happen. ConfigurationManager refreshes the
// keys automatically on its own schedule (default: every 24h, or sooner if
// a validation fails with an unrecognized key ID, which happens when Entra
// ID rotates its signing keys).
public class EntraIdConfigurationProvider : IEntraIdConfigurationProvider
{
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;

    public EntraIdConfigurationProvider(IOptions<EntraIdOptions> options)
    {
        _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            options.Value.MetadataAddress,
            new OpenIdConnectConfigurationRetriever());
    }

    public async Task<EntraIdSigningConfiguration> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        var configuration = await _configurationManager.GetConfigurationAsync(cancellationToken);
        return new EntraIdSigningConfiguration(configuration.Issuer, configuration.SigningKeys.ToList());
    }
}
