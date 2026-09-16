using System.Collections.Concurrent;

namespace FunctionApi.Notes;

// Deliberately not a database -- this module is about the request pipeline
// (auth, identity, secrets), not persistence. State lives in process memory
// and resets every time the Function App restarts or scales to a new
// instance, which is itself worth noticing: a Consumption-plan Function can
// have any number of instances running your code at once, each with its
// own copy of this list. See EXERCISE.md Part 1.4.
public class InMemoryNotesStore : INotesStore
{
    private readonly ConcurrentQueue<Note> _notes = new();
    private int _nextId;

    public IReadOnlyList<Note> GetAll() => _notes.ToArray();

    public Note Add(string text, string createdBy)
    {
        var note = new Note(Interlocked.Increment(ref _nextId), text, createdBy, DateTimeOffset.UtcNow);
        _notes.Enqueue(note);
        return note;
    }
}
