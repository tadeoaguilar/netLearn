using EfCoreTransactions.Data;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Demos;

/// <summary>
/// Small helpers shared by every Part*.cs demo, so each one can focus on the
/// transaction behaviour it's illustrating instead of connection plumbing.
/// </summary>
internal static class DemoSupport
{
    public static DbContextOptions<BankDbContext> BuildOptions(string connectionString) =>
        new DbContextOptionsBuilder<BankDbContext>().UseNpgsql(connectionString).Options;

    /// <summary>
    /// Opens a context against a schema that definitely exists, with empty
    /// accounts/transfers tables, so every demo starts from the same clean
    /// slate no matter what earlier demos left behind.
    /// </summary>
    public static async Task<BankDbContext> CreateFreshContextAsync(
        string connectionString, CancellationToken cancellationToken = default)
    {
        var context = new BankDbContext(BuildOptions(connectionString));
        await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        await ResetAsync(context, cancellationToken).ConfigureAwait(false);
        return context;
    }

    public static Task ResetAsync(BankDbContext context, CancellationToken cancellationToken = default) =>
        context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE transfers, accounts RESTART IDENTITY CASCADE;", cancellationToken);
}
