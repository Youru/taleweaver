using Microsoft.AspNetCore.Http.HttpResults;

namespace Taleweaver.Api.Common;

/// <summary>Maps an <see cref="Error"/> to RFC 9457 problem details, in one place.</summary>
public static class ResultExtensions
{
    public static ProblemHttpResult ToProblem(this Error error) =>
        TypedResults.Problem(
            title: error.Code,
            detail: error.Message,
            statusCode: error.Type switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError,
            },
            extensions: new Dictionary<string, object?> { ["code"] = error.Code });
}
