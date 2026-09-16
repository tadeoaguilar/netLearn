using System.Net;
using System.Net.Http.Json;
using FunctionApi.Notes;
using FunctionApi.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace FunctionApi.Functions;

// Every [HttpTrigger] here uses AuthorizationLevel.Anonymous -- that
// controls Azure's own FUNCTION KEY mechanism (a shared secret in a query
// string or header), a completely different, older gate than the Entra ID
// bearer token this module is about. "Anonymous" means "no function key
// required," not "no authentication required" -- see EXERCISE.md Part 2.1.
// The real gate is the explicit context.GetUser() check in every method
// below, backed by EntraIdAuthenticationMiddleware.
public class NotesFunctions(INotesStore notesStore)
{
    [Function("GetNotes")]
    public async Task<HttpResponseData> GetNotes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "notes")] HttpRequestData req,
        FunctionContext context)
    {
        if (context.GetUser() is null)
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(notesStore.GetAll());
        return response;
    }

    [Function("CreateNote")]
    public async Task<HttpResponseData> CreateNote(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "notes")] HttpRequestData req,
        FunctionContext context)
    {
        var user = context.GetUser();
        if (user is null)
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var payload = await req.ReadFromJsonAsync<CreateNoteRequest>();
        if (payload is null || string.IsNullOrWhiteSpace(payload.Text))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { error = "'text' is required." });
            return badRequest;
        }

        var callerId = user.FindFirst("appid")?.Value
            ?? user.FindFirst("azp")?.Value
            ?? "unknown-caller";

        var note = notesStore.Add(payload.Text, callerId);

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(note);
        return response;
    }

    private record CreateNoteRequest(string Text);
}
