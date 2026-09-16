using System.Security.Cryptography;
using FunctionApi.Security;
using Microsoft.Extensions.Options;

namespace FunctionApi.Tests;

// Every test here runs with no network access and no real Entra ID tenant --
// see TestTokenFactory for why that's possible. This is the real validation
// logic (EntraIdTokenValidator), not a rewritten copy of it.
public class EntraIdTokenValidatorTests : IDisposable
{
    private readonly RSA _signingKey = TestTokenFactory.CreateSigningKey();

    private EntraIdTokenValidator CreateValidator(string? audience = null, string? requiredRole = null)
    {
        var configurationProvider = TestTokenFactory.CreateConfigurationProvider(_signingKey);
        var options = Options.Create(new EntraIdOptions
        {
            TenantId = "test-tenant",
            Audience = audience ?? TestTokenFactory.Audience,
            RequiredAppRole = requiredRole ?? TestTokenFactory.RequiredRole
        });
        return new EntraIdTokenValidator(configurationProvider, options);
    }

    [Fact]
    public async Task Valid_token_with_required_role_is_accepted()
    {
        var token = TestTokenFactory.CreateToken(_signingKey);
        var validator = CreateValidator();

        var outcome = await validator.ValidateAsync(token, CancellationToken.None);

        outcome.IsValid.Should().BeTrue();
        outcome.Principal.Should().NotBeNull();
        outcome.Principal!.FindAll("roles").Select(c => c.Value)
            .Should().Contain(TestTokenFactory.RequiredRole);
    }

    [Fact]
    public async Task Token_missing_the_required_app_role_is_rejected()
    {
        var token = TestTokenFactory.CreateToken(_signingKey, roles: ["SomeOtherRole"]);
        var validator = CreateValidator();

        var outcome = await validator.ValidateAsync(token, CancellationToken.None);

        outcome.IsValid.Should().BeFalse();
        outcome.FailureReason.Should().Contain("missing required app role");
    }

    [Fact]
    public async Task Token_with_wrong_audience_is_rejected()
    {
        var token = TestTokenFactory.CreateToken(_signingKey, audience: "api://some-other-api");
        var validator = CreateValidator();

        var outcome = await validator.ValidateAsync(token, CancellationToken.None);

        outcome.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Token_with_wrong_issuer_is_rejected()
    {
        var token = TestTokenFactory.CreateToken(
            _signingKey, issuer: "https://login.microsoftonline.com/some-other-tenant/v2.0");
        var validator = CreateValidator();

        var outcome = await validator.ValidateAsync(token, CancellationToken.None);

        outcome.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        var token = TestTokenFactory.CreateToken(_signingKey, expiresIn: TimeSpan.FromMinutes(-10));
        var validator = CreateValidator();

        var outcome = await validator.ValidateAsync(token, CancellationToken.None);

        outcome.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Token_signed_with_a_different_key_is_rejected()
    {
        using var otherKey = TestTokenFactory.CreateSigningKey();
        var token = TestTokenFactory.CreateToken(otherKey);
        // Validator only trusts _signingKey's public key, not otherKey's.
        var validator = CreateValidator();

        var outcome = await validator.ValidateAsync(token, CancellationToken.None);

        outcome.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Malformed_token_string_is_rejected_without_throwing()
    {
        var validator = CreateValidator();

        var outcome = await validator.ValidateAsync("not-a-jwt", CancellationToken.None);

        outcome.IsValid.Should().BeFalse();
    }

    public void Dispose() => _signingKey.Dispose();
}
