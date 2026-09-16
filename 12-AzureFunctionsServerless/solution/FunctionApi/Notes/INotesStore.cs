namespace FunctionApi.Notes;

public interface INotesStore
{
    IReadOnlyList<Note> GetAll();
    Note Add(string text, string createdBy);
}
