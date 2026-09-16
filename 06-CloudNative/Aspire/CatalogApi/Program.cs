// Follow EXERCISE.md Part A.3 to build this out step by step.

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "CatalogApi workspace -- start with EXERCISE.md.");

app.Run();

public partial class Program;
