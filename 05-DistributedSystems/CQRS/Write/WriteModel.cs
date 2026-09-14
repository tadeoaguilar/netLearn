namespace CQRS.Write;

/// <summary>
/// The WRITE model. Normalised, rule-enforcing, and shaped for correctness
/// rather than for display.
/// </summary>
public class Product
{
    public Product(string sku, string name, decimal price, int stock)
    {
        Sku = sku;
        Name = name;
        Price = price;
        Stock = stock;
    }

    public string Sku { get; }
    public string Name { get; private set; }
    public decimal Price { get; private set; }
    public int Stock { get; private set; }

    public void Reprice(decimal price)
    {
        if (price <= 0) throw new InvalidOperationException("Price must be positive.");
        Price = price;
    }

    public void Reserve(int quantity)
    {
        if (quantity > Stock) throw new InvalidOperationException($"Only {Stock} of {Sku} in stock.");
        Stock -= quantity;
    }

    public void Restock(int quantity) => Stock += quantity;
}

public record ProductRepriced(string Sku, decimal Price);
public record StockChanged(string Sku, int Stock);

/// <summary>
/// Commands go here. Every write emits an event describing what changed, which
/// is how the read side finds out.
/// </summary>
public class WriteStore
{
    private readonly Dictionary<string, Product> _products = new();
    private readonly List<object> _events = new();

    public IReadOnlyList<object> PendingEvents => _events.ToArray();

    public void Add(Product product)
    {
        _products[product.Sku] = product;
        _events.Add(new StockChanged(product.Sku, product.Stock));
        _events.Add(new ProductRepriced(product.Sku, product.Price));
    }

    public Product Get(string sku) => _products.TryGetValue(sku, out var p)
        ? p
        : throw new KeyNotFoundException($"No product {sku}");

    public void Reprice(string sku, decimal price)
    {
        var product = Get(sku);
        product.Reprice(price);
        _events.Add(new ProductRepriced(sku, price));
    }

    public void Reserve(string sku, int quantity)
    {
        var product = Get(sku);
        product.Reserve(quantity);
        _events.Add(new StockChanged(sku, product.Stock));
    }

    public IReadOnlyList<object> DrainEvents()
    {
        var drained = _events.ToArray();
        _events.Clear();
        return drained;
    }
}
