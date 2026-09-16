using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;

namespace FunctionApi.Security;

public static class FunctionContextExtensions
{
    private const string UserItemKey = "EntraIdUser";

    internal static void SetUser(this FunctionContext context, ClaimsPrincipal principal) =>
        context.Items[UserItemKey] = principal;

    // Every secured function starts with:
    //     if (context.GetUser() is not { } user) return req.CreateResponse(HttpStatusCode.Unauthorized);
    // The middleware runs on every invocation and validates a token if one
    // is present, but does NOT decide which endpoints require auth -- that
    // stays an explicit, visible check in each function. See EXERCISE.md
    // Part 2.5 for why that split is deliberate.
    public static ClaimsPrincipal? GetUser(this FunctionContext context) =>
        context.Items.TryGetValue(UserItemKey, out var value) ? value as ClaimsPrincipal : null;
}
