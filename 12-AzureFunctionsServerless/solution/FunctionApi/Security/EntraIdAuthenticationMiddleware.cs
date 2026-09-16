using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace FunctionApi.Security;

// Runs on every function invocation. If the request carries an
// "Authorization: Bearer ..." header, this validates it and stashes the
// resulting ClaimsPrincipal on the FunctionContext (see
// FunctionContextExtensions.GetUser). It deliberately does NOT reject
// requests with no token or an invalid one -- see EXERCISE.md Part 2.5 for
// why that decision belongs to each function, not to this middleware.
public class EntraIdAuthenticationMiddleware(
    ITokenValidator tokenValidator,
    ILogger<EntraIdAuthenticationMiddleware> logger) : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var requestData = await context.GetHttpRequestDataAsync();

        if (requestData is not null && TryGetBearerToken(requestData, out var token))
        {
            var outcome = await tokenValidator.ValidateAsync(token, context.CancellationToken);

            if (outcome.IsValid)
            {
                context.SetUser(outcome.Principal!);
            }
            else
            {
                logger.LogWarning("Bearer token rejected: {Reason}", outcome.FailureReason);
            }
        }

        await next(context);
    }

    private static bool TryGetBearerToken(HttpRequestData requestData, out string token)
    {
        token = string.Empty;

        if (!requestData.Headers.TryGetValues("Authorization", out var values))
        {
            return false;
        }

        var header = values.FirstOrDefault();
        const string prefix = "Bearer ";

        if (header is null || !header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        token = header[prefix.Length..].Trim();
        return token.Length > 0;
    }
}
