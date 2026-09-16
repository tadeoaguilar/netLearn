// Follow EXERCISE.md Part A.4 to build this out step by step.

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "Storefront workspace -- start with EXERCISE.md.");

app.Run();

public partial class Program;
