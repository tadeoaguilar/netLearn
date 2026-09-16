using EfCoreMigrations.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Reference solution for the EfCoreMigrations exercise.
//
// Applies the full migration history to whatever Postgres the connection
// string points at, runs the idempotent runtime seed from Part 5, then
// prints row counts so you can see it worked. Requires Docker Postgres
// running (`docker compose up -d` from 10-EntityFrameworkCore/).

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    Console.WriteLine("Applying migrations...");
    await context.Database.MigrateAsync();

    Console.WriteLine("Running idempotent runtime seed...");
    await SeedData.EnsureDemoDataAsync(context);

    var authorCount = await context.Authors.CountAsync();
    var bookCount = await context.Books.CountAsync();
    var genreCount = await context.Genres.CountAsync();

    Console.WriteLine();
    Console.WriteLine($"Authors: {authorCount}");
    Console.WriteLine($"Books:   {bookCount}");
    Console.WriteLine($"Genres:  {genreCount}");
}
