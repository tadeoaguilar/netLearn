using CqrsMediatR.Behaviors;
using CqrsMediatR.Features;
using FluentValidation;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RepositoryUnitOfWork.Data;

namespace ArchitecturePatterns.Tests;

public class RepositoryUnitOfWorkTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ShopDbContext _context;

    public RepositoryUnitOfWorkTests()
    {
        _connection.Open();
        _context = new ShopDbContext(
            new DbContextOptionsBuilder<ShopDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task A_repository_round_trips_an_entity()
    {
        await using var unitOfWork = new UnitOfWork(_context);
        var customer = new Customer { Name = "Ada", Credit = 500m };

        await unitOfWork.Customers.AddAsync(customer);
        await unitOfWork.SaveChangesAsync();

        var loaded = await unitOfWork.Customers.GetByIdAsync(customer.Id);
        loaded!.Name.Should().Be("Ada");
    }

    [Fact]
    public async Task Changes_are_invisible_until_SaveChanges_is_called()
    {
        // This is what makes the unit of work a unit of work: the repository
        // stages, and only SaveChangesAsync commits.
        await using var unitOfWork = new UnitOfWork(_context);

        await unitOfWork.Customers.AddAsync(new Customer { Name = "Grace" });
        (await _context.Customers.AsNoTracking().CountAsync()).Should().Be(0);

        await unitOfWork.SaveChangesAsync();
        (await _context.Customers.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Two_repositories_commit_in_one_transaction()
    {
        await using var unitOfWork = new UnitOfWork(_context);
        var customer = new Customer { Name = "Linus", Credit = 100m };

        await unitOfWork.Customers.AddAsync(customer);
        await unitOfWork.Orders.AddAsync(new Order { CustomerId = customer.Id, Amount = 60m });
        await unitOfWork.SaveChangesAsync();

        (await _context.Customers.CountAsync()).Should().Be(1);
        (await _context.Orders.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task A_specific_repository_aggregates_in_the_database()
    {
        await using var unitOfWork = new UnitOfWork(_context);
        var customer = new Customer { Name = "Ada" };
        await unitOfWork.Customers.AddAsync(customer);

        for (var i = 1; i <= 100; i++)
        {
            await unitOfWork.Orders.AddAsync(new Order
            {
                CustomerId = customer.Id,
                Amount = i,
                Status = i % 2 == 0 ? "Pending" : "Shipped"
            });
        }
        await unitOfWork.SaveChangesAsync();

        var total = await unitOfWork.Orders.GetTotalSpendAsync(customer.Id);
        var pending = await unitOfWork.Orders.GetPendingForCustomerAsync(customer.Id);

        total.Should().Be(5050m, "SUM ran in SQL, not in C#");
        pending.Should().HaveCount(50);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class CqrsMediatRTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<Catalogue>();
        services.AddSingleton<RequestLog>();
        services.AddMediatR(c => c.RegisterServicesFromAssembly(typeof(Catalogue).Assembly));
        services.AddValidatorsFromAssembly(typeof(Catalogue).Assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task A_command_changes_state_and_a_query_reads_it()
    {
        var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new AddProductCommand("SKU-1", "Keyboard", 99m, 25));
        var view = await sender.Send(new GetProductQuery("SKU-1"));

        view!.Name.Should().Be("Keyboard");
        view.Availability.Should().Be("In stock");
    }

    [Fact]
    public async Task The_validation_behaviour_rejects_before_the_handler_runs()
    {
        var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();
        var catalogue = provider.GetRequiredService<Catalogue>();

        var act = async () => await sender.Send(new AddProductCommand("nonsense", "", -5m, -1));

        await act.Should().ThrowAsync<ValidationException>();
        catalogue.WriteCount.Should().Be(0, "the handler was never reached");
    }

    [Fact]
    public async Task Validation_reports_every_broken_rule_at_once()
    {
        var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();

        var act = async () => await sender.Send(new AddProductCommand("nope", "", -5m, -1));

        var thrown = await act.Should().ThrowAsync<ValidationException>();
        thrown.Which.Errors.Should().HaveCountGreaterThanOrEqualTo(4);
    }

    [Fact]
    public async Task One_behaviour_observes_every_request_type()
    {
        // The reason RequestLog is an injected singleton rather than a static
        // on the generic behaviour: statics on a generic type are per closed
        // type, so each TRequest would get its own list.
        var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();
        var log = provider.GetRequiredService<RequestLog>();

        await sender.Send(new AddProductCommand("SKU-1", "Keyboard", 99m, 5));
        await sender.Send(new GetProductQuery("SKU-1"));
        await sender.Send(new ListProductsQuery());

        log.Entries.Should().HaveCount(3);
        log.Entries.Should().Contain(e => e.StartsWith("AddProductCommand"));
        log.Entries.Should().Contain(e => e.StartsWith("GetProductQuery"));
        log.Entries.Should().Contain(e => e.StartsWith("ListProductsQuery"));
    }

    [Fact]
    public async Task A_failed_request_is_logged_too()
    {
        var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();
        var log = provider.GetRequiredService<RequestLog>();

        try { await sender.Send(new RepriceProductCommand("SKU-1", -1m)); }
        catch (ValidationException) { /* expected */ }

        log.Entries.Should().ContainSingle()
            .Which.Should().Contain("FAILED").And.Contain("ValidationException");
    }

    [Fact]
    public async Task A_query_returns_null_rather_than_throwing_for_a_missing_item()
    {
        var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();

        var view = await sender.Send(new GetProductQuery("SKU-404"));

        view.Should().BeNull();
    }

    [Fact]
    public async Task A_query_can_filter_without_any_handler_changing()
    {
        var provider = BuildProvider();
        var sender = provider.GetRequiredService<ISender>();

        await sender.Send(new AddProductCommand("SKU-1", "Cheap", 45m, 10));
        await sender.Send(new AddProductCommand("SKU-2", "Pricey", 699m, 10));

        var cheap = await sender.Send(new ListProductsQuery(MaxPrice: 100m));

        cheap.Should().ContainSingle().Which.Name.Should().Be("Cheap");
    }
}
