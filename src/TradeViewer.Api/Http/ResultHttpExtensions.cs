using TradeViewer.Application.Abstractions.Messaging;

namespace TradeViewer.Api.Http;

/// <summary>Maps the outcome of a handler to an HTTP answer (architecture, chapter 4): the endpoint calls the handler and maps the result, nothing else.</summary>
public static class ResultHttpExtensions
{
    /// <summary>200 with the value, or the problem details of the error.</summary>
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        return result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error);
    }

    /// <summary>204 for a command that returns nothing, or the problem details of the error.</summary>
    public static IResult ToNoContentResult(this Result<Unit> result)
    {
        return result.IsSuccess ? Results.NoContent() : ToProblem(result.Error);
    }

    public static IResult ToProblem(Error error)
    {
        var extensions = new Dictionary<string, object?> { ["code"] = error.Code };

        if (error.Type == ErrorType.Validation)
        {
            var fields = error.Fields.ToDictionary(pair => pair.Key, pair => pair.Value);
            return Results.ValidationProblem(fields, detail: error.Message, extensions: extensions);
        }

        var (status, title) = error.Type switch
        {
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not found"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            ErrorType.InvalidData => (StatusCodes.Status422UnprocessableEntity, "Invalid data"),
            ErrorType.Unavailable => (StatusCodes.Status503ServiceUnavailable, "Unavailable"),
            _ => (StatusCodes.Status500InternalServerError, "Error")
        };

        return Results.Problem(detail: error.Message, statusCode: status, title: title, extensions: extensions);
    }
}
