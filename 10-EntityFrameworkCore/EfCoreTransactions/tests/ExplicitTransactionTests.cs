using EfCoreTransactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Tests;

/// <summary>
/// Part 2: once a business operation needs more than one SaveChanges() call,
/// only an explicit transaction keeps it atomic.
/// </summary>
[Collection("Postgres")]
public class ExplicitTransactionTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public ExplicitTransactionTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(int aliceId, int bobId)> SeedAccountsAsync(decimal aliceBalance = 500m, decimal bobBalance = 100m)
    {
        await using var context = _fixture.CreateContext();
        var alice = new Account { Owner = "Alice", Balance = aliceBalance };
        var bob = new Account { Owner = "Bob", Balance = bobBalance };
        context.Accounts.AddRange(alice, bob);
        await context.SaveChangesAsync();
        return (alice.Id, bob.Id);
    }

    [Fact]
    public async Task WithoutTransaction_FailureBetweenSaveChangesCalls_LeavesDebitPersistedAlone()
    {
        var (aliceId, bobId) = await SeedAccountsAsync();

        await using var context = _fixture.CreateContext();
        var alice = await context.Accounts.SingleAsync(a => a.Id == aliceId);
        var bob = await context.Accounts.SingleAsync(a => a.Id == bobId);

        alice.Balance -= 200m;
        await context.SaveChangesAsync(); // debit persisted, no transaction wrapping it

        // Simulated crash: the credit never happens.

        await using var verify = _fixture.CreateContext();
        var aliceAfter = await verify.Accounts.SingleAsync(a => a.Id == aliceId);
        var bobAfter = await verify.Accounts.SingleAsync(a => a.Id == bobId);

        aliceAfter.Balance.Should().Be(300m, "the debit already committed on its own");
        bobAfter.Balance.Should().Be(100m, "the credit never ran -- money has effectively vanished");
    }

    [Fact]
    public async Task WithTransaction_FailureBetweenSaveChangesCalls_RollsBackTheDebitToo()
    {
        var (aliceId, bobId) = await SeedAccountsAsync();

        await using var context = _fixture.CreateContext();
        var alice = await context.Accounts.SingleAsync(a => a.Id == aliceId);
        var bob = await context.Accounts.SingleAsync(a => a.Id == bobId);

        await using var transaction = await context.Database.BeginTransactionAsync();

        alice.Balance -= 200m;
        await context.SaveChangesAsync(); // debit written, but not committed

        Action simulatedFailure = () => throw new InvalidOperationException("simulated failure before the credit leg");

        try
        {
            simulatedFailure();
            bob.Balance += 200m;
            await context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (InvalidOperationException)
        {
            await transaction.RollbackAsync();
        }

        await using var verify = _fixture.CreateContext();
        var aliceAfter = await verify.Accounts.SingleAsync(a => a.Id == aliceId);
        var bobAfter = await verify.Accounts.SingleAsync(a => a.Id == bobId);

        aliceAfter.Balance.Should().Be(500m, "the rollback must undo the debit as well as skip the credit");
        bobAfter.Balance.Should().Be(100m);
    }

    [Fact]
    public async Task WithTransaction_NoFailure_CommitsBothLegs()
    {
        var (aliceId, bobId) = await SeedAccountsAsync();

        await using var context = _fixture.CreateContext();
        var alice = await context.Accounts.SingleAsync(a => a.Id == aliceId);
        var bob = await context.Accounts.SingleAsync(a => a.Id == bobId);

        await using var transaction = await context.Database.BeginTransactionAsync();

        alice.Balance -= 200m;
        await context.SaveChangesAsync();

        bob.Balance += 200m;
        await context.SaveChangesAsync();

        await transaction.CommitAsync();

        await using var verify = _fixture.CreateContext();
        var aliceAfter = await verify.Accounts.SingleAsync(a => a.Id == aliceId);
        var bobAfter = await verify.Accounts.SingleAsync(a => a.Id == bobId);

        aliceAfter.Balance.Should().Be(300m);
        bobAfter.Balance.Should().Be(300m);
    }

    [Fact]
    public async Task WithTransaction_NotCommitted_LeavesNoTraceAfterContextDisposed()
    {
        var (aliceId, bobId) = await SeedAccountsAsync();

        await using (var context = _fixture.CreateContext())
        {
            var alice = await context.Accounts.SingleAsync(a => a.Id == aliceId);
            await using var transaction = await context.Database.BeginTransactionAsync();

            alice.Balance -= 200m;
            await context.SaveChangesAsync();

            // Deliberately never call CommitAsync(). Disposing the
            // transaction (via `await using`, at scope exit) rolls it back.
        }

        await using var verify = _fixture.CreateContext();
        var aliceAfter = await verify.Accounts.SingleAsync(a => a.Id == aliceId);
        aliceAfter.Balance.Should().Be(500m, "an uncommitted transaction rolls back when it's disposed");
    }
}
