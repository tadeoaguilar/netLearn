using Microsoft.EntityFrameworkCore;

namespace RepositoryUnitOfWork.Data;

/// <summary>
/// A generic repository. Useful for the four operations every entity needs,
/// and a trap if you stop there.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default);
    Task AddAsync(T entity, CancellationToken cancellationToken = default);
    void Remove(T entity);
}

public class EfRepository<T> : IRepository<T> where T : class
{
    protected readonly ShopDbContext Context;

    public EfRepository(ShopDbContext context) => Context = context;

    public async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await Context.Set<T>().FindAsync([id], cancellationToken);

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default)
        => await Context.Set<T>().ToListAsync(cancellationToken);

    public async Task AddAsync(T entity, CancellationToken cancellationToken = default)
        => await Context.Set<T>().AddAsync(entity, cancellationToken);

    public void Remove(T entity) => Context.Set<T>().Remove(entity);

    // Note: there is NO SaveChangesAsync here. That is deliberate -- see
    // IUnitOfWork. A repository that saves cannot participate in a transaction
    // with any other repository.
}

/// <summary>
/// Where the generic repository runs out: a query that is specific to one
/// entity and one use case. Inherit and add it, rather than bending
/// IRepository&lt;T&gt; into something that takes arbitrary predicates.
/// </summary>
public interface IOrderRepository : IRepository<Order>
{
    Task<IReadOnlyList<Order>> GetPendingForCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalSpendAsync(Guid customerId, CancellationToken cancellationToken = default);
}

public class OrderRepository : EfRepository<Order>, IOrderRepository
{
    public OrderRepository(ShopDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Order>> GetPendingForCustomerAsync(
        Guid customerId, CancellationToken cancellationToken = default)
        => await Context.Orders
            .Where(o => o.CustomerId == customerId && o.Status == "Pending")
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Aggregation in the DATABASE. A generic repository would have forced
    /// ListAsync() and a client-side Sum -- correct, and catastrophic on a
    /// table with a million rows.
    /// </summary>
    public async Task<decimal> GetTotalSpendAsync(
        Guid customerId, CancellationToken cancellationToken = default)
        => await Context.Orders
            .Where(o => o.CustomerId == customerId)
            .SumAsync(o => o.Amount, cancellationToken);
}

/// <summary>
/// One transaction spanning several repositories.
///
/// Without this, each repository saving independently means a failure halfway
/// through leaves some changes committed and some not.
/// </summary>
public interface IUnitOfWork : IAsyncDisposable
{
    IRepository<Customer> Customers { get; }
    IOrderRepository Orders { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public class UnitOfWork : IUnitOfWork
{
    private readonly ShopDbContext _context;

    public UnitOfWork(ShopDbContext context)
    {
        _context = context;
        Customers = new EfRepository<Customer>(context);
        Orders = new OrderRepository(context);
    }

    // Both repositories share ONE DbContext, so they share one change tracker
    // and one transaction. That sharing is the entire pattern.
    public IRepository<Customer> Customers { get; }
    public IOrderRepository Orders { get; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public ValueTask DisposeAsync() => _context.DisposeAsync();
}
