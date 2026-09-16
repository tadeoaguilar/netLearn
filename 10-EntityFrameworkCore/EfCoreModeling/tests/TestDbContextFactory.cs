using EfCoreModeling.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EfCoreModeling.Tests;

/// <summary>
/// Every test in this project builds a LibraryDbContext against a connection
/// string that is never opened. EF Core only needs a connection when it
/// actually talks to a database (SaveChanges, a query enumerated, migrations
/// applied); building the model -- which is everything these tests inspect,
/// via context.Model -- happens the first time something touches the model
/// and never involves I/O. That is what lets this whole test project run
/// with no Docker and no Postgres.
/// </summary>
internal static class TestDbContextFactory
{
    private const string NeverOpenedConnectionString =
        "Host=localhost;Port=5432;Database=unused;Username=postgres;Password=postgres";

    public static LibraryDbContext Create()
    {
        var options = new DbContextOptionsBuilder<LibraryDbContext>()
            .UseNpgsql(NeverOpenedConnectionString)
            .Options;

        return new LibraryDbContext(options);
    }
}
