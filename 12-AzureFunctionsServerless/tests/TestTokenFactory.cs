using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FunctionApi.Security;
using Microsoft.IdentityModel.Tokens;

namespace FunctionApi.Tests;

// Everything a real Entra ID token needs to pass EntraIdTokenValidator --
// but signed with a throwaway RSA key generated in-process, never touching
// the network or a real tenant. This is what makes the validator's tests
// runnable anywhere, including a sandbox with no Docker and no Azure
// access: the "trust" in a JWT is entirely in the signature, so a test that
// controls both the signing key and the validation config can exercise the
// real validation logic end to end.
public static class TestTokenFactory
{
    public const string Issuer = "https://login.microsoftonline.com/test-tenant/v2.0";
    public const string Audience = "api://test-api-app-id";
    public const string RequiredRole = "Notes.Access";

    public static RSA CreateSigningKey() => RSA.Create(2048);

    public static IEntraIdConfigurationProvider CreateConfigurationProvider(RSA signingKey, string? issuer = null)
    {
        var securityKey = new RsaSecurityKey(signingKey) { KeyId = "test-key" };
        return new FakeEntraIdConfigurationProvider(
            new EntraIdSigningConfiguration(issuer ?? Issuer, [securityKey]));
    }

    public static string CreateToken(
        RSA signingKey,
        string? issuer = null,
        string? audience = null,
        IEnumerable<string>? roles = null,
        TimeSpan? expiresIn = null,
        string appId = "test-client-app-id")
    {
        var credentials = new SigningCredentials(
            new RsaSecurityKey(signingKey) { KeyId = "test-key" }, SecurityAlgorithms.RsaSha256);

        var claims = new List<Claim>
        {
            new("appid", appId),
            new("azp", appId)
        };
        claims.AddRange((roles ?? [RequiredRole]).Select(role => new Claim("roles", role)));

        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: issuer ?? Issuer,
            audience: audience ?? Audience,
            claims: claims,
            // Always well before "now" so a negative expiresIn (used to test
            // an already-expired token) never puts expires before notBefore.
            notBefore: now.AddHours(-1),
            expires: now.Add(expiresIn ?? TimeSpan.FromHours(1)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class FakeEntraIdConfigurationProvider(EntraIdSigningConfiguration configuration)
        : IEntraIdConfigurationProvider
    {
        public Task<EntraIdSigningConfiguration> GetConfigurationAsync(CancellationToken cancellationToken) =>
            Task.FromResult(configuration);
    }
}
