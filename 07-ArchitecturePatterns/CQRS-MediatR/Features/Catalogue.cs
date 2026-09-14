using FluentValidation;
using MediatR;

namespace CqrsMediatR.Features;

/// <summary>In-memory store so the demo needs no database.</summary>
public class Catalogue
{
    private readonly Dictionary<string, (string Name, decimal Price, int Stock)> _items = new();

    public int WriteCount { get; private set; }
    public int ReadCount { get; private set; }

    public void Add(string sku, string name, decimal price, int stock)
    {
        _items[sku] = (name, price, stock);
        WriteCount++;
    }

    public bool TryGet(string sku, out (string Name, decimal Price, int Stock) item)
    {
        ReadCount++;
        return _items.TryGetValue(sku, out item);
    }

    public void SetPrice(string sku, decimal price)
    {
        var item = _items[sku];
        _items[sku] = (item.Name, price, item.Stock);
        WriteCount++;
    }

    public IEnumerable<(string Sku, string Name, decimal Price, int Stock)> All()
    {
        ReadCount++;
        return _items.Select(kv => (kv.Key, kv.Value.Name, kv.Value.Price, kv.Value.Stock));
    }
}

// ---------- COMMANDS: change state, return as little as possible ----------

public record AddProductCommand(string Sku, string Name, decimal Price, int Stock) : IRequest<string>;

public class AddProductValidator : AbstractValidator<AddProductCommand>
{
    public AddProductValidator()
    {
        RuleFor(x => x.Sku).NotEmpty().Matches("^SKU-[0-9]+$")
            .WithMessage("Sku must look like SKU-123.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Price).GreaterThan(0);
        RuleFor(x => x.Stock).GreaterThanOrEqualTo(0);
    }
}

public class AddProductHandler : IRequestHandler<AddProductCommand, string>
{
    private readonly Catalogue _catalogue;
    public AddProductHandler(Catalogue catalogue) => _catalogue = catalogue;

    public Task<string> Handle(AddProductCommand request, CancellationToken cancellationToken)
    {
        _catalogue.Add(request.Sku, request.Name, request.Price, request.Stock);
        return Task.FromResult(request.Sku);
    }
}

public record RepriceProductCommand(string Sku, decimal Price) : IRequest<Unit>;

public class RepriceProductValidator : AbstractValidator<RepriceProductCommand>
{
    public RepriceProductValidator() => RuleFor(x => x.Price).GreaterThan(0);
}

public class RepriceProductHandler : IRequestHandler<RepriceProductCommand, Unit>
{
    private readonly Catalogue _catalogue;
    public RepriceProductHandler(Catalogue catalogue) => _catalogue = catalogue;

    public Task<Unit> Handle(RepriceProductCommand request, CancellationToken cancellationToken)
    {
        _catalogue.SetPrice(request.Sku, request.Price);
        return Task.FromResult(Unit.Value);
    }
}

// ---------- QUERIES: return data, change nothing ----------

public record ProductView(string Sku, string Name, decimal Price, string Availability);

public record GetProductQuery(string Sku) : IRequest<ProductView?>;

public class GetProductHandler : IRequestHandler<GetProductQuery, ProductView?>
{
    private readonly Catalogue _catalogue;
    public GetProductHandler(Catalogue catalogue) => _catalogue = catalogue;

    public Task<ProductView?> Handle(GetProductQuery request, CancellationToken cancellationToken)
    {
        if (!_catalogue.TryGet(request.Sku, out var item))
        {
            return Task.FromResult<ProductView?>(null);
        }

        // A query shapes data for display. It does not enforce rules -- there
        // is nothing to enforce, because it changes nothing.
        return Task.FromResult<ProductView?>(new ProductView(
            request.Sku, item.Name, item.Price,
            item.Stock switch { 0 => "Out of stock", < 10 => "Low stock", _ => "In stock" }));
    }
}

public record ListProductsQuery(decimal? MaxPrice = null) : IRequest<IReadOnlyList<ProductView>>;

public class ListProductsHandler : IRequestHandler<ListProductsQuery, IReadOnlyList<ProductView>>
{
    private readonly Catalogue _catalogue;
    public ListProductsHandler(Catalogue catalogue) => _catalogue = catalogue;

    public Task<IReadOnlyList<ProductView>> Handle(
        ListProductsQuery request, CancellationToken cancellationToken)
    {
        var items = _catalogue.All();

        if (request.MaxPrice is { } max)
        {
            items = items.Where(i => i.Price <= max);
        }

        return Task.FromResult<IReadOnlyList<ProductView>>(items
            .OrderBy(i => i.Sku)
            .Select(i => new ProductView(i.Sku, i.Name, i.Price,
                i.Stock switch { 0 => "Out of stock", < 10 => "Low stock", _ => "In stock" }))
            .ToList());
    }
}
