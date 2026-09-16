using EfCoreTransactions.Domain;
using EfCoreTransactions.Services;
using Microsoft.EntityFrameworkCore;

namespace EfCoreTransactions.Tests;

/// <summary>
/// Part 6: ITransferService / UnitOfWork -- the "begin transaction, do the
/// work, commit or rollback" boilerplate factored out so callers don't
/// repeat it, exercised end to end against real Postgres.
/// </summary>
[Collection("Postgres")]
public class UnitOfWorkTests : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public UnitOfWorkTests(PostgresFixture fixture) => _fixture = fixture;

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

    private ITransferService CreateTransferService(out EfCoreTransactions.Data.BankDbContext context)
    {
        context = _fixture.CreateContext();
        var unitOfWork = new UnitOfWork(context);
        return new TransferService(context, unitOfWork);
    }

    [Fact]
    public async Task TransferAsync_HappyPath_MovesMoneyAndRecordsACompletedTransfer()
    {
        var (aliceId, bobId) = await SeedAccountsAsync();
        var service = CreateTransferService(out var context);
        await using var _ = context;

        var transferId = await service.TransferAsync(aliceId, bobId, 150m);

        await using var verify = _fixture.CreateContext();
        var alice = await verify.Accounts.SingleAsync(a => a.Id == aliceId);
        var bob = await verify.Accounts.SingleAsync(a => a.Id == bobId);
        var transfer = await verify.Transfers.SingleAsync(t => t.Id == transferId);

        alice.Balance.Should().Be(350m);
        bob.Balance.Should().Be(250m);
        transfer.Status.Should().Be(TransferStatus.Completed);
        transfer.Amount.Should().Be(150m);
        transfer.FromAccountId.Should().Be(aliceId);
        transfer.ToAccountId.Should().Be(bobId);
    }

    [Fact]
    public async Task TransferAsync_InsufficientFunds_ThrowsAndPersistsNothingAtAll()
    {
        var (aliceId, bobId) = await SeedAccountsAsync(aliceBalance: 50m);
        var service = CreateTransferService(out var context);
        await using var _ = context;

        var act = () => service.TransferAsync(aliceId, bobId, 1_000m);

        await act.Should().ThrowAsync<InsufficientFundsException>();

        await using var verify = _fixture.CreateContext();
        var alice = await verify.Accounts.SingleAsync(a => a.Id == aliceId);
        var bob = await verify.Accounts.SingleAsync(a => a.Id == bobId);
        var transferCount = await verify.Transfers.CountAsync();

        alice.Balance.Should().Be(50m, "the whole unit of work rolled back");
        bob.Balance.Should().Be(100m);
        transferCount.Should().Be(0, "even the Pending transfer row must roll back with everything else");
    }

    [Fact]
    public async Task TransferAsync_SelfTransfer_ThrowsArgumentExceptionWithoutTouchingTheDatabase()
    {
        var (aliceId, _) = await SeedAccountsAsync();
        var service = CreateTransferService(out var context);
        await using var _ = context;

        var act = () => service.TransferAsync(aliceId, aliceId, 10m);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-25)]
    public async Task TransferAsync_NonPositiveAmount_ThrowsArgumentOutOfRangeException(decimal amount)
    {
        var (aliceId, bobId) = await SeedAccountsAsync();
        var service = CreateTransferService(out var context);
        await using var _ = context;

        var act = () => service.TransferAsync(aliceId, bobId, amount);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task TransferAsync_MultipleSuccessiveTransfers_AccumulateCorrectly()
    {
        var (aliceId, bobId) = await SeedAccountsAsync(aliceBalance: 1000m, bobBalance: 0m);
        var service = CreateTransferService(out var context);
        await using var _ = context;

        await service.TransferAsync(aliceId, bobId, 100m);
        await service.TransferAsync(aliceId, bobId, 200m);
        await service.TransferAsync(bobId, aliceId, 50m);

        await using var verify = _fixture.CreateContext();
        var alice = await verify.Accounts.SingleAsync(a => a.Id == aliceId);
        var bob = await verify.Accounts.SingleAsync(a => a.Id == bobId);
        var completedCount = await verify.Transfers.CountAsync(t => t.Status == TransferStatus.Completed);

        alice.Balance.Should().Be(1000m - 100m - 200m + 50m);
        bob.Balance.Should().Be(100m + 200m - 50m);
        completedCount.Should().Be(3);
    }

    [Fact]
    public async Task UnitOfWork_ExecuteAsync_RollsBackOnAnyException_NotJustDomainOnes()
    {
        var (aliceId, bobId) = await SeedAccountsAsync();
        await using var context = _fixture.CreateContext();
        var unitOfWork = new UnitOfWork(context);

        var act = () => unitOfWork.ExecuteAsync(async () =>
        {
            var alice = await context.Accounts.SingleAsync(a => a.Id == aliceId);
            alice.Balance -= 10m;
            await context.SaveChangesAsync();

            throw new InvalidOperationException("boom");
        });

        await act.Should().ThrowAsync<InvalidOperationException>();

        await using var verify = _fixture.CreateContext();
        var aliceAfter = await verify.Accounts.SingleAsync(a => a.Id == aliceId);
        aliceAfter.Balance.Should().Be(500m, "any exception -- not just a domain-specific one -- must roll the unit of work back");
    }
}
