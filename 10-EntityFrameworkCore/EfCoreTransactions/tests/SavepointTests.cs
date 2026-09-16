using EfCoreTransactions.Domain;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Tests;

/// <summary>
/// Part 3: a savepoint lets one step of a multi-step transaction be undone
/// without aborting the whole transaction.
/// </summary>
[Collection("Postgres")]
public class SavepointTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public SavepointTests(PostgresFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => _fixture.ResetAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(int aliceId, int bobId, int carolId)> SeedAccountsAsync()
    {
        await using var context = _fixture.CreateContext();
        var alice = new Account { Owner = "Alice", Balance = 300m };
        var bob = new Account { Owner = "Bob", Balance = 50m };
        var carol = new Account { Owner = "Carol", Balance = 20m };
        context.Accounts.AddRange(alice, bob, carol);
        await context.SaveChangesAsync();
        return (alice.Id, bob.Id, carol.Id);
    }

    [Fact]
    public async Task RollbackToSavepoint_UndoesOnlyThatStep_EarlierStepSurvivesCommit()
    {
        var (aliceId, bobId, carolId) = await SeedAccountsAsync();

        await using var context = _fixture.CreateContext();
        var alice = await context.Accounts.SingleAsync(a => a.Id == aliceId);
        var bob = await context.Accounts.SingleAsync(a => a.Id == bobId);
        var carol = await context.Accounts.SingleAsync(a => a.Id == carolId);

        await using var transaction = await context.Database.BeginTransactionAsync();

        // Step 1: survives.
        alice.Balance -= 100m;
        bob.Balance += 100m;
        await context.SaveChangesAsync();

        // Step 2: would drive Alice negative -- rejected by the CHECK
        // constraint, then undone with a savepoint rather than the whole
        // transaction.
        await transaction.CreateSavepointAsync("step2");
        try
        {
            alice.Balance -= 500m;
            carol.Balance += 500m;
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackToSavepointAsync("step2");
            await context.Entry(alice).ReloadAsync();
            await context.Entry(carol).ReloadAsync();
        }

        await transaction.CommitAsync();

        await using var verify = _fixture.CreateContext();
        var aliceAfter = await verify.Accounts.SingleAsync(a => a.Id == aliceId);
        var bobAfter = await verify.Accounts.SingleAsync(a => a.Id == bobId);
        var carolAfter = await verify.Accounts.SingleAsync(a => a.Id == carolId);

        aliceAfter.Balance.Should().Be(200m, "step 1's debit survived, step 2's never applied");
        bobAfter.Balance.Should().Be(150m, "step 1's credit committed");
        carolAfter.Balance.Should().Be(20m, "step 2 was rolled back to the savepoint, so Carol never received it");
    }

    [Fact]
    public async Task RollbackToSavepoint_TransactionRemainsUsable_LaterStepStillCommits()
    {
        var (aliceId, bobId, carolId) = await SeedAccountsAsync();

        await using var context = _fixture.CreateContext();
        var alice = await context.Accounts.SingleAsync(a => a.Id == aliceId);
        var bob = await context.Accounts.SingleAsync(a => a.Id == bobId);
        var carol = await context.Accounts.SingleAsync(a => a.Id == carolId);

        await using var transaction = await context.Database.BeginTransactionAsync();

        await transaction.CreateSavepointAsync("failing-step");
        try
        {
            alice.Balance -= 5_000m; // would go negative
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Without rolling back to the savepoint, Postgres would refuse
            // every further command in this transaction with "current
            // transaction is aborted". This proves that doesn't happen.
            await transaction.RollbackToSavepointAsync("failing-step");
            await context.Entry(alice).ReloadAsync();
        }

        // A brand new step, in the SAME transaction that just recovered.
        bob.Balance -= 10m;
        carol.Balance += 10m;
        await context.SaveChangesAsync();

        await transaction.CommitAsync();

        await using var verify = _fixture.CreateContext();
        var bobAfter = await verify.Accounts.SingleAsync(a => a.Id == bobId);
        var carolAfter = await verify.Accounts.SingleAsync(a => a.Id == carolId);

        bobAfter.Balance.Should().Be(40m);
        carolAfter.Balance.Should().Be(30m);
    }

    [Fact]
    public async Task NegativeBalance_ViolatesCheckConstraint_ThrowsDbUpdateException()
    {
        var (aliceId, _, _) = await SeedAccountsAsync();

        await using var context = _fixture.CreateContext();
        var alice = await context.Accounts.SingleAsync(a => a.Id == aliceId);
        alice.Balance = -1m;

        DbUpdateException? caught = null;
        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull();
        caught!.InnerException.Should().BeOfType<Npgsql.PostgresException>();
        ((Npgsql.PostgresException)caught.InnerException!).SqlState.Should().Be("23514"); // check_violation
    }

    [Fact]
    public async Task RollbackToSavepoint_WithoutCommit_DiscardsEverythingOnDispose()
    {
        var (aliceId, bobId, _) = await SeedAccountsAsync();

        await using (var context = _fixture.CreateContext())
        {
            var alice = await context.Accounts.SingleAsync(a => a.Id == aliceId);
            var bob = await context.Accounts.SingleAsync(a => a.Id == bobId);

            await using var transaction = await context.Database.BeginTransactionAsync();
            await transaction.CreateSavepointAsync("s1");

            alice.Balance -= 100m;
            bob.Balance += 100m;
            await context.SaveChangesAsync();

            // Never commit -- disposing the transaction rolls back
            // everything, savepoints included.
        }

        await using var verify = _fixture.CreateContext();
        var aliceAfter = await verify.Accounts.SingleAsync(a => a.Id == aliceId);
        var bobAfter = await verify.Accounts.SingleAsync(a => a.Id == bobId);

        aliceAfter.Balance.Should().Be(300m);
        bobAfter.Balance.Should().Be(50m);
    }
}
