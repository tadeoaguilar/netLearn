using System.Net;
using FunctionApi.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace FunctionApi.Functions;

// Echoes back exactly what EntraIdAuthenticationMiddleware validated --
// the clearest possible way to see the claims a client-credentials token
// actually carries (appid, roles, tenant id, expiry) once it's past
// validation. Call this first when wiring up a new client app registration;
// if a role you expect isn't in the "roles" list here, admin consent or the
// role assignment is the place to look, not the validation code.
public class MeFunction
{
    [Function("Me")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequestData req,
        FunctionContext context)
    {
        if (context.GetUser() is not { } user)
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        var claims = user.Claims.Select(c => new { c.Type, c.Value });
        await response.WriteAsJsonAsync(new { claims });
        return response;
    }
}
