using CQRS.Write;

namespace CQRS.Read;

/// <summary>
/// The READ model. Denormalised and shaped for exactly one screen -- no joins,
/// no aggregation at query time.
///
/// This is the point of CQRS: the shape that makes writes correct and the shape
/// that makes reads fast are usually not the same shape, and trying to serve
/// both from one model compromises both.
/// </summary>
public record ProductListItem(string Sku, string Name, decimal Price, int Stock, string Availability);

public class ReadStore
{
    private readonly Dictionary<string, ProductListItem> _items = new();

    public int ProjectedEvents { get; private set; }

    public IReadOnlyList<ProductListItem> All() => _items.Values.OrderBy(i => i.Sku).ToList();

    public ProductListItem? Get(string sku) => _items.GetValueOrDefault(sku);

    public void Seed(string sku, string name) =>
        _items[sku] = new ProductListItem(sku, name, 0, 0, "Unknown");

    /// <summary>
    /// The PROJECTION. It turns write-side events into read-side rows,
    /// precomputing anything a query would otherwise have to work out.
    /// </summary>
    public void Project(object @event)
    {
        ProjectedEvents++;

        switch (@event)
        {
            case ProductRepriced repriced when _items.TryGetValue(repriced.Sku, out var priced):
                _items[repriced.Sku] = priced with { Price = repriced.Price };
                break;

            case StockChanged stock when _items.TryGetValue(stock.Sku, out var item):
                // "Availability" is computed HERE, once per change, instead of
                // on every read. Reads become a dictionary lookup.
                _items[stock.Sku] = item with
                {
                    Stock = stock.Stock,
                    Availability = stock.Stock switch
                    {
                        0 => "Out of stock",
                        < 10 => "Low stock",
                        _ => "In stock"
                    }
                };
                break;
        }
    }
}
