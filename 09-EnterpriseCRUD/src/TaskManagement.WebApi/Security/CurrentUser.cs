using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using TaskManagement.Application.Common.Interfaces;

namespace TaskManagement.WebApi.Security;

/// <summary>
/// Reads the caller from the validated token. The claims are already verified
/// by the JWT middleware, so nothing here re-checks a signature.
/// </summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    // `sub` is the OIDC standard subject claim. ASP.NET maps it to NameIdentifier
    // unless that mapping is disabled, so both are checked.
    public string? UserId =>
        Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName =>
        Principal?.FindFirstValue(JwtRegisteredClaimNames.PreferredUsername)
        ?? Principal?.FindFirstValue(ClaimTypes.Name);

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;
}

/// <summary>
/// Issues tokens for local development ONLY.
///
/// A real deployment points at Keycloak, Entra ID or Auth0 and validates
/// asymmetrically signed tokens against the provider's published keys. This
/// exists so the API can be run and tested without standing up an identity
/// provider first -- and it is refused outright outside Development.
/// </summary>
public class DevelopmentTokenIssuer
{
    private readonly JwtOptions _options;

    public DevelopmentTokenIssuer(JwtOptions options) => _options = options;

    public string Issue(string userId, string userName, params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.PreferredUsername, userName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "taskmanagement-dev";
    public string Audience { get; set; } = "taskmanagement-api";

    /// <summary>
    /// Development-only symmetric key. A real deployment uses an identity
    /// provider's asymmetric keys and never holds a signing secret at all.
    /// </summary>
    public string SigningKey { get; set; } = "dev-only-signing-key-change-me-at-least-32-bytes";

    /// <summary>When set, tokens are validated against this OIDC authority instead.</summary>
    public string? Authority { get; set; }
}
