using EfCoreTransactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Tests;

/// <summary>
/// Part 4: two separate DbContext instances racing to update the same row
/// via Postgres's xmin system column, mapped as EF Core's concurrency token.
/// </summary>
[Collection("Postgres")]
public class OptimisticConcurrencyTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public OptimisticConcurrencyTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<int> SeedAccountAsync(decimal balance = 1000m)
    {
        await using var context = _fixture.CreateContext();
        var account = new Account { Owner = "Alice", Balance = balance };
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account.Id;
    }

    [Fact]
    public async Task TwoContexts_SecondSaveChanges_ThrowsDbUpdateConcurrencyException()
    {
        var accountId = await SeedAccountAsync();

        // Two genuinely separate DbContext instances -- not the same
        // instance queried twice -- standing in for two concurrent requests.
        await using var contextA = _fixture.CreateContext();
        await using var contextB = _fixture.CreateContext();

        var accountA = await contextA.Accounts.SingleAsync(a => a.Id == accountId);
        var accountB = await contextB.Accounts.SingleAsync(a => a.Id == accountId);

        accountA.Balance += 100m;
        await contextA.SaveChangesAsync(); // wins the race, moves xmin forward

        accountB.Balance -= 50m;
        var act = () => contextB.SaveChangesAsync(); // still holds the OLD xmin

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task AfterConcurrencyException_ReloadAndRetry_SucceedsAndAppliesBothChanges()
    {
        var accountId = await SeedAccountAsync();

        await using var contextA = _fixture.CreateContext();
        await using var contextB = _fixture.CreateContext();

        var accountA = await contextA.Accounts.SingleAsync(a => a.Id == accountId);
        var accountB = await contextB.Accounts.SingleAsync(a => a.Id == accountId);

        accountA.Balance += 100m;
        await contextA.SaveChangesAsync();

        accountB.Balance -= 50m;
        try
        {
            await contextB.SaveChangesAsync();
            throw new InvalidOperationException("Expected a DbUpdateConcurrencyException that did not happen.");
        }
        catch (DbUpdateConcurrencyException)
        {
            await contextB.Entry(accountB).ReloadAsync();
            accountB.Balance -= 50m;
            await contextB.SaveChangesAsync();
        }

        await using var verify = _fixture.CreateContext();
        var final = await verify.Accounts.SingleAsync(a => a.Id == accountId);
        final.Balance.Should().Be(1000m + 100m - 50m);
    }

    [Fact]
    public async Task Version_ChangesAfterEveryUpdate()
    {
        var accountId = await SeedAccountAsync();

        await using var context = _fixture.CreateContext();
        var account = await context.Accounts.SingleAsync(a => a.Id == accountId);
        var originalVersion = account.Version;

        account.Balance += 1m;
        await context.SaveChangesAsync();

        account.Version.Should().NotBe(originalVersion, "Postgres bumps xmin on every UPDATE of the row");
    }

    [Fact]
    public async Task NoConflict_SequentialUpdatesFromSameContext_BothSucceed()
    {
        var accountId = await SeedAccountAsync();

        await using var context = _fixture.CreateContext();
        var account = await context.Accounts.SingleAsync(a => a.Id == accountId);

        account.Balance += 10m;
        await context.SaveChangesAsync();

        account.Balance += 20m;
        var act = () => context.SaveChangesAsync();

        await act.Should().NotThrowAsync("the same context always has the latest xmin after each SaveChanges()");
        account.Balance.Should().Be(1030m);
    }

    [Fact]
    public async Task StaleContext_UpdatingDeletedRow_AlsoThrowsDbUpdateConcurrencyException()
    {
        var accountId = await SeedAccountAsync();

        await using var contextA = _fixture.CreateContext();
        await using var contextB = _fixture.CreateContext();

        var accountA = await contextA.Accounts.SingleAsync(a => a.Id == accountId);
        var accountB = await contextB.Accounts.SingleAsync(a => a.Id == accountId);

        contextA.Accounts.Remove(accountA);
        await contextA.SaveChangesAsync();

        accountB.Balance += 5m;
        var act = () => contextB.SaveChangesAsync();

        // Zero rows affected either way -- EF Core can't tell "someone
        // changed it" from "someone deleted it" apart, and reports both as
        // a concurrency conflict.
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
