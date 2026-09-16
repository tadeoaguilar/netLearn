using EfCoreModeling.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Reference solution for the EfCoreModeling exercise.
//
// This prints a summary of the configured EF Core model -- entities, columns,
// relationships, indexes -- without ever opening a database connection. EF
// Core builds its model lazily, the first time something touches
// context.Model; UseNpgsql only records a connection string, it does not
// connect. Start `docker compose up -d` in 10-EntityFrameworkCore/ first if
// you want to point this at a real database instead (nothing here requires
// it).

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Library")));

var host = builder.Build();

using var scope = host.Services.CreateScope();
var context = scope.ServiceProvider.GetRequiredService<LibraryDbContext>();

Console.WriteLine("=== EfCoreModeling: library catalog model ===\n");

// Check constraints (and a few other design-time-only annotations) are
// stripped from the runtime-optimized `context.Model` for performance, so
// inspecting them needs the design-time model instead. Neither one opens a
// database connection -- both are still just metadata built in memory.
var model = context.GetService<IDesignTimeModel>().Model;

foreach (var entityType in model.GetEntityTypes().OrderBy(e => e.ClrType.Name))
{
    // Owned types (Money) are printed inline with their owner below, not as
    // their own top-level entry.
    if (entityType.IsOwned())
    {
        continue;
    }

    var baseType = entityType.BaseType is null ? string.Empty : $" : {entityType.BaseType.ClrType.Name}";
    Console.WriteLine($"{entityType.ClrType.Name}{baseType} -> table \"{entityType.GetTableName()}\"");

    foreach (var property in entityType.GetDeclaredProperties())
    {
        var nullable = property.IsNullable ? "?" : string.Empty;
        Console.WriteLine($"    {property.Name}{nullable} : {property.ClrType.Name} -> {property.GetColumnName()}");
    }

    foreach (var navigation in entityType.GetDeclaredNavigations().Where(n => n.TargetEntityType.IsOwned()))
    {
        Console.WriteLine($"    {navigation.Name} (owned) ->");
        foreach (var ownedProperty in navigation.TargetEntityType.GetProperties())
        {
            Console.WriteLine($"        {ownedProperty.Name} : {ownedProperty.ClrType.Name} -> {ownedProperty.GetColumnName()}");
        }
    }

    foreach (var fk in entityType.GetDeclaredForeignKeys())
    {
        var columns = string.Join(", ", fk.Properties.Select(p => p.GetColumnName()));
        Console.WriteLine($"    FK -> {fk.PrincipalEntityType.ClrType.Name} ({columns}) [{fk.DeleteBehavior}]");
    }

    foreach (var index in entityType.GetDeclaredIndexes())
    {
        var unique = index.IsUnique ? "UNIQUE " : string.Empty;
        var columns = string.Join(", ", index.Properties.Select(p => p.GetColumnName()));
        Console.WriteLine($"    {unique}INDEX ({columns})");
    }

    foreach (var check in entityType.GetCheckConstraints())
    {
        Console.WriteLine($"    CHECK {check.Name}: {check.Sql}");
    }

    Console.WriteLine();
}

Console.WriteLine("Model built without opening a database connection.");
