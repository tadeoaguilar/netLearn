using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace FunctionApi.Functions;

// Deliberately anonymous -- a health probe that required a token would be
// useless to whatever's checking it. Contrast with NotesFunctions and
// MeFunction below, which check context.GetUser() themselves.
public class HealthFunction
{
    [Function("Health")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.WriteString("Healthy");
        return response;
    }
}
