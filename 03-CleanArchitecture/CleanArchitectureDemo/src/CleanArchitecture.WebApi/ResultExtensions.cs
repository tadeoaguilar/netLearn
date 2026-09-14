using CleanArchitecture.Application.Abstractions;

namespace CleanArchitecture.WebApi;

/// <summary>
/// Translates a use case Result into an HTTP response.
///
/// This mapping lives in the presentation layer because status codes are an
/// HTTP concern. The Application layer knows "not found" and "conflict"; it
/// does not know 404 and 409.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult>? onSuccess = null)
    {
        if (result.IsSuccess)
        {
            return onSuccess?.Invoke(result.Value!) ?? Results.Ok(result.Value);
        }

        var error = result.Error!;

        return error.Kind switch
        {
            ResultErrorKind.NotFound => Results.Problem(error.Message, statusCode: StatusCodes.Status404NotFound),
            ResultErrorKind.Validation => Results.Problem(error.Message, statusCode: StatusCodes.Status400BadRequest),
            ResultErrorKind.Conflict => Results.Problem(error.Message, statusCode: StatusCodes.Status409Conflict),
            _ => Results.Problem(error.Message, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
