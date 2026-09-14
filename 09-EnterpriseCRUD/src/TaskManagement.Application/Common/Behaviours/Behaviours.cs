using System.Diagnostics;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TaskManagement.Application.Common.Interfaces;

namespace TaskManagement.Application.Common.Behaviours;

/// <summary>
/// Validates every request before its handler runs, so no handler repeats it
/// and no new handler can forget it.
/// </summary>
public class ValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehaviour(IEnumerable<IValidator<TRequest>> validators) => _validators = validators;

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any()) return await next(cancellationToken);

        var context = new ValidationContext<TRequest>(request);

        var failures = (await Task.WhenAll(
                _validators.Select(v => v.ValidateAsync(context, cancellationToken))))
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToArray();

        if (failures.Length > 0) throw new ValidationException(failures);

        return await next(cancellationToken);
    }
}

/// <summary>Structured logging for every request, with the caller's identity.</summary>
public class LoggingBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehaviour<TRequest, TResponse>> _logger;
    private readonly ICurrentUser _currentUser;

    public LoggingBehaviour(ILogger<LoggingBehaviour<TRequest, TResponse>> logger, ICurrentUser currentUser)
    {
        _logger = logger;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken);

            _logger.LogInformation("{Request} handled for {User} in {Elapsed}ms",
                name, _currentUser.UserId ?? "anonymous", sw.ElapsedMilliseconds);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("{Request} failed for {User} after {Elapsed}ms: {Error}",
                name, _currentUser.UserId ?? "anonymous", sw.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }
}
