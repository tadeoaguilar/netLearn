using System.Data;
using EfCoreTransactions.Domain;
using EfCoreTransactions.Services;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Tests;

/// <summary>
/// Part 5: Read Committed vs. Serializable, and retrying a genuine 40001
/// serialization failure that Serializable provokes.
/// </summary>
[Collection("Postgres")]
public class IsolationLevelTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public IsolationLevelTests(PostgresFixture fixture) => _fixture = fixture;

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
    public async Task ReadCommitted_SeesOtherTransactionsCommittedChange_WithoutReopening()
    {
        var accountId = await SeedAccountAsync();

        await using var contextA = _fixture.CreateContext();
        await using var transactionA = await contextA.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var accountA = await contextA.Accounts.SingleAsync(a => a.Id == accountId);

        await using (var contextB = _fixture.CreateContext())
        {
            var accountB = await contextB.Accounts.SingleAsync(a => a.Id == accountId);
            accountB.Balance += 200m;
            await contextB.SaveChangesAsync();
        }

        await contextA.Entry(accountA).ReloadAsync();
        accountA.Balance.Should().Be(1200m, "Read Committed re-reads see the latest committed data, even mid-transaction");

        await transactionA.CommitAsync();
    }

    [Fact]
    public async Task Serializable_ConcurrentUpdateToSameRow_ThrowsPostgresExceptionWithSerializationFailureSqlState()
    {
        var accountId = await SeedAccountAsync();

        await using var contextA = _fixture.CreateContext();
        await using var transactionA = await contextA.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // A takes its snapshot by reading the row.
        var accountA = await contextA.Accounts.SingleAsync(a => a.Id == accountId);

        // B updates and commits the SAME row in a separate, already-committed transaction.
        await using (var contextB = _fixture.CreateContext())
        {
            var accountB = await contextB.Accounts.SingleAsync(a => a.Id == accountId);
            accountB.Balance += 10m;
            await contextB.SaveChangesAsync();
        }

        // A now tries to write a row it read BEFORE B's commit. Postgres can
        // raise the serialization failure either right on this UPDATE or at
        // COMMIT, depending on the exact conflict shape -- catch broadly and
        // let SerializationRetryPolicy (the same code Part 5's demo and
        // TransferService retry logic rely on) identify it, rather than
        // over-specifying exactly where and as what CLR type it surfaces.
        accountA.Balance -= 25m;

        Exception? caught = null;
        try
        {
            await contextA.SaveChangesAsync();
            await transactionA.CommitAsync();
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull("a Serializable transaction writing a row changed underneath it must fail");
        SerializationRetryPolicy.IsSerializationFailure(caught!).Should().BeTrue(
            $"expected a 40001 serialization failure, got: {caught}");
    }

    [Fact]
    public async Task SerializationRetryPolicy_RetriesOnceAndSucceeds_AfterATransientConflict()
    {
        var accountId = await SeedAccountAsync();
        var attempts = 0;
        var conflictInjected = false;

        var finalBalance = await SerializationRetryPolicy.ExecuteAsync(async () =>
        {
            attempts++;

            await using var contextA = _fixture.CreateContext();
            await using var transactionA = await contextA.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var accountA = await contextA.Accounts.SingleAsync(a => a.Id == accountId);

            if (!conflictInjected)
            {
                conflictInjected = true;

                await using var contextB = _fixture.CreateContext();
                var accountB = await contextB.Accounts.SingleAsync(a => a.Id == accountId);
                accountB.Balance += 10m;
                await contextB.SaveChangesAsync();
            }

            accountA.Balance -= 25m;
            await contextA.SaveChangesAsync();
            await transactionA.CommitAsync();

            return accountA.Balance;
        });

        attempts.Should().Be(2, "the first attempt must conflict and the second must succeed");
        finalBalance.Should().Be(1000m + 10m - 25m);

        await using var verify = _fixture.CreateContext();
        var final = await verify.Accounts.SingleAsync(a => a.Id == accountId);
        final.Balance.Should().Be(finalBalance);
    }

    [Fact]
    public async Task SerializationRetryPolicy_ExhaustsMaxAttempts_RethrowsTheSerializationFailure()
    {
        var accountId = await SeedAccountAsync();
        var attempts = 0;

        // This operation conflicts on EVERY attempt (never stops injecting a
        // competing write), so the policy must give up after maxAttempts and
        // surface the underlying serialization failure.
        Func<Task> act = () => SerializationRetryPolicy.ExecuteAsync(
            async () =>
            {
                attempts++;

                await using var contextA = _fixture.CreateContext();
                await using var transactionA = await contextA.Database.BeginTransactionAsync(IsolationLevel.Serializable);
                var accountA = await contextA.Accounts.SingleAsync(a => a.Id == accountId);

                await using (var contextB = _fixture.CreateContext())
                {
                    var accountB = await contextB.Accounts.SingleAsync(a => a.Id == accountId);
                    accountB.Balance += 1m;
                    await contextB.SaveChangesAsync();
                }

                accountA.Balance -= 1m;
                await contextA.SaveChangesAsync();
                await transactionA.CommitAsync();
            },
            maxAttempts: 2);

        Exception? caught = null;
        try
        {
            await act();
        }
        catch (Exception ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull();
        SerializationRetryPolicy.IsSerializationFailure(caught!).Should().BeTrue();
        attempts.Should().Be(2, "the policy must stop retrying once maxAttempts is reached");
    }

    [Fact]
    public void IsSerializationFailure_ReturnsFalse_ForUnrelatedException()
    {
        SerializationRetryPolicy.IsSerializationFailure(new InvalidOperationException("unrelated"))
            .Should().BeFalse();
    }
}
