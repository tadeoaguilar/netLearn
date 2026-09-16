using Microsoft.IdentityModel.Tokens;

namespace FunctionApi.Security;

// The one seam between "validate a JWT" and "talk to the network." Real
// production code fetches this from Entra ID's OIDC discovery document and
// caches it (see EntraIdConfigurationProvider); tests supply a fixed,
// locally-generated key instead, so the validation logic itself -- issuer,
// audience, signature, lifetime, role checks -- is exercised with no
// network call and no real tenant. See EXERCISE.md Part 2.3.
public interface IEntraIdConfigurationProvider
{
    Task<EntraIdSigningConfiguration> GetConfigurationAsync(CancellationToken cancellationToken);
}

public record EntraIdSigningConfiguration(string Issuer, IReadOnlyList<SecurityKey> SigningKeys);
