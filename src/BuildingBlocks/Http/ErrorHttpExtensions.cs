using LeaveWorkforce.BuildingBlocks.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LeaveWorkforce.BuildingBlocks.Http;

public static class ErrorHttpExtensions
{
    /// <summary>Maps an Error to an RFC 9457 ProblemDetails response using the SEC-11 status codes.</summary>
    public static IResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var (status, title) = error.Kind switch
        {
            ErrorKind.Unauthenticated => (StatusCodes.Status401Unauthorized, "Unauthenticated"),
            ErrorKind.Throttled => (StatusCodes.Status429TooManyRequests, "Too many requests"),
            ErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorKind.NotFound => (StatusCodes.Status404NotFound, "Not found"),
            ErrorKind.Validation => (StatusCodes.Status400BadRequest, "Validation failed"),
            ErrorKind.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Kind, "Unknown error kind."),
        };

        var problem = new ProblemDetails { Status = status, Title = title, Detail = error.Detail };
        problem.Extensions["code"] = error.Code;
        if (error.Kind == ErrorKind.Validation && error.FieldErrors is not null)
        {
            problem.Extensions["errors"] = error.FieldErrors;
        }

        return Microsoft.AspNetCore.Http.Results.Problem(problem);
    }
}
