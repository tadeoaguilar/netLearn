namespace FunctionApi.Notes;

public record Note(int Id, string Text, string CreatedBy, DateTimeOffset CreatedAt);
