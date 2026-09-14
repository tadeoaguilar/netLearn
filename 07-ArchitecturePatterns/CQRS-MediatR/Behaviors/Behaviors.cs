using System.Diagnostics;
using FluentValidation;
using MediatR;

namespace CqrsMediatR.Behaviors;

/// <summary>
/// Validation for every request, registered once.
///
/// This is the decorator pattern from module 01 applied to a whole pipeline:
/// no handler validates its own input, and adding a handler cannot forget to.
/// </summary>
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any()) return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);
        var failures = (await Task.WhenAll(_validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToArray();

        if (failures.Length > 0) throw new ValidationException(failures);

        return await next(cancellationToken);
    }
}

/// <summary>
/// A shared sink for the logging behaviour, registered as a singleton.
///
/// It exists because a `static` field on LoggingBehavior&lt;TRequest, TResponse&gt;
/// would NOT be shared: in C#, each closed generic type gets its own copy of
/// every static field. LoggingBehavior&lt;AddProductCommand, string&gt; and
/// LoggingBehavior&lt;GetProductQuery, ProductView&gt; would keep separate lists,
/// and you would see one entry where you expected four.
/// </summary>
public class RequestLog
{
    private readonly List<string> _entries = new();
    private readonly Lock _gate = new();

    public IReadOnlyList<string> Entries
    {
        get { lock (_gate) return _entries.ToArray(); }
    }

    public void Add(string entry)
    {
        lock (_gate) _entries.Add(entry);
    }

    public void Clear()
    {
        lock (_gate) _entries.Clear();
    }
}

/// <summary>Times every request. Same mechanism as validation, different concern.</summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly RequestLog _log;

    public LoggingBehavior(RequestLog log) => _log = log;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken);
            _log.Add($"{typeof(TRequest).Name} ok in {sw.ElapsedMilliseconds}ms");
            return response;
        }
        catch (Exception ex)
        {
            _log.Add($"{typeof(TRequest).Name} FAILED in {sw.ElapsedMilliseconds}ms: {ex.GetType().Name}");
            throw;
        }
    }
}
