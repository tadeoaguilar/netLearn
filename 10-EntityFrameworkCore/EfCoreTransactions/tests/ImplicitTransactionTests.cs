using EfCoreTransactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Tests;

/// <summary>
/// Part 1: a single SaveChanges() call carrying two tracked changes is
/// already atomic, with no explicit transaction code.
/// </summary>
[Collection("Postgres")]
public class ImplicitTransactionTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public ImplicitTransactionTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SaveChanges_WithTwoTrackedChanges_AppliesBothInOneCall()
    {
        await using var context = _fixture.CreateContext();
        var alice = new Account { Owner = "Alice", Balance = 500m };
        var bob = new Account { Owner = "Bob", Balance = 100m };
        context.Accounts.AddRange(alice, bob);
        await context.SaveChangesAsync();

        alice.Balance -= 50m;
        bob.Balance += 50m;
        var written = await context.SaveChangesAsync();

        written.Should().Be(2);
        alice.Balance.Should().Be(450m);
        bob.Balance.Should().Be(150m);
    }

    [Fact]
    public async Task SaveChanges_WithTwoTrackedChanges_BothPersistToDatabase()
    {
        int aliceId, bobId;

        await using (var context = _fixture.CreateContext())
        {
            var alice = new Account { Owner = "Alice", Balance = 500m };
            var bob = new Account { Owner = "Bob", Balance = 100m };
            context.Accounts.AddRange(alice, bob);
            await context.SaveChangesAsync();

            alice.Balance -= 50m;
            bob.Balance += 50m;
            await context.SaveChangesAsync();

            aliceId = alice.Id;
            bobId = bob.Id;
        }

        // Read back from a completely fresh context -- proves the change
        // reached the database, not just the first context's change tracker.
        await using var verify = _fixture.CreateContext();
        var aliceAfter = await verify.Accounts.SingleAsync(a => a.Id == aliceId);
        var bobAfter = await verify.Accounts.SingleAsync(a => a.Id == bobId);

        aliceAfter.Balance.Should().Be(450m);
        bobAfter.Balance.Should().Be(150m);
    }

    [Fact]
    public async Task SaveChanges_ChecksConstraintEvenWithinOneCall_RollsBackBothChanges()
    {
        await using var context = _fixture.CreateContext();
        var alice = new Account { Owner = "Alice", Balance = 30m };
        var bob = new Account { Owner = "Bob", Balance = 100m };
        context.Accounts.AddRange(alice, bob);
        await context.SaveChangesAsync();

        // A single SaveChanges() call that would drive Alice negative. Even
        // though the credit to Bob is individually valid, the whole call --
        // both statements -- is one transaction, so a constraint violation
        // on either statement rolls back the other too.
        alice.Balance -= 50m;
        bob.Balance += 50m;

        var act = () => context.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();

        await using var verify = _fixture.CreateContext();
        var bobAfter = await verify.Accounts.SingleAsync(a => a.Id == bob.Id);
        bobAfter.Balance.Should().Be(100m, "the credit must roll back along with the rejected debit");
    }
}
