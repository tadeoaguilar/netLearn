using System.Security.Claims;

namespace FunctionApi.Security;

public interface ITokenValidator
{
    Task<TokenValidationOutcome> ValidateAsync(string bearerToken, CancellationToken cancellationToken);
}

public record TokenValidationOutcome
{
    public required bool IsValid { get; init; }
    public ClaimsPrincipal? Principal { get; init; }
    public string? FailureReason { get; init; }

    public static TokenValidationOutcome Success(ClaimsPrincipal principal) =>
        new() { IsValid = true, Principal = principal };

    public static TokenValidationOutcome Failure(string reason) =>
        new() { IsValid = false, FailureReason = reason };
}
