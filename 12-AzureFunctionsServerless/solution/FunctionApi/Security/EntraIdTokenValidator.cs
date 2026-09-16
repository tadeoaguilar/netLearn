using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FunctionApi.Security;

// The actual validation logic: fetch the current signing keys/issuer (via
// IEntraIdConfigurationProvider), then check everything a bearer token
// needs to pass before you trust it -- signature, issuer, audience,
// lifetime -- and finally the one thing Entra ID does NOT check for you:
// does this specific caller have the app role this API requires? A token
// can be perfectly valid and issued by the right tenant for the wrong
// application entirely; only the audience + role checks catch that.
public class EntraIdTokenValidator(
    IEntraIdConfigurationProvider configurationProvider,
    IOptions<EntraIdOptions> options) : ITokenValidator
{
    private const string RoleClaimType = "roles";

    public async Task<TokenValidationOutcome> ValidateAsync(string bearerToken, CancellationToken cancellationToken)
    {
        var signingConfiguration = await configurationProvider.GetConfigurationAsync(cancellationToken);
        var entraOptions = options.Value;

        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = signingConfiguration.Issuer,
            IssuerSigningKeys = signingConfiguration.SigningKeys,
            ValidAudience = entraOptions.Audience,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2)
        };

        var handler = new JwtSecurityTokenHandler
        {
            // JwtSecurityTokenHandler remaps short claim names to legacy
            // WS-Federation URIs by default -- "roles" silently becomes
            // "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
            // on the validated ClaimsPrincipal, even though the raw JWT
            // payload says "roles". Turning this off keeps claim types
            // exactly as Entra ID issued them, which is what every Entra ID
            // guide (and the "roles" check below) expects. Delete this line
            // and Part 2.6's "missing required app role" test starts
            // failing for a token that plainly has the role -- see
            // EXERCISE.md for the trace that catches this.
            MapInboundClaims = false
        };

        System.Security.Claims.ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(bearerToken, validationParameters, out _);
        }
        catch (SecurityTokenException ex)
        {
            return TokenValidationOutcome.Failure($"Token failed validation: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            // ValidateToken throws ArgumentException (not SecurityTokenException)
            // for a malformed token string -- e.g. not three base64url segments.
            return TokenValidationOutcome.Failure($"Token is malformed: {ex.Message}");
        }

        var roles = principal.FindAll(RoleClaimType).Select(c => c.Value).ToArray();
        if (!roles.Contains(entraOptions.RequiredAppRole))
        {
            return TokenValidationOutcome.Failure(
                $"Token is valid but missing required app role '{entraOptions.RequiredAppRole}'. " +
                $"Roles present: [{string.Join(", ", roles)}]");
        }

        return TokenValidationOutcome.Success(principal);
    }
}
