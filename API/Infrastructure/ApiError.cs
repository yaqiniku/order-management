namespace API.Infrastructure;

public sealed record ApiErrorBody(ApiError Error);

public sealed record ApiError(string Code, string Message, string CorrelationId);

public static class ApiErrors
{
    public static ApiErrorBody Create(HttpContext context, string code, string message) =>
        new(new ApiError(code, message, context.TraceIdentifier));

    public static (int Status, string Code) Classify(Exception exception) => exception switch
    {
        ArgumentException => (StatusCodes.Status400BadRequest, "validation_error"),
        KeyNotFoundException => (StatusCodes.Status404NotFound, "not_found"),
        InvalidOperationException => (StatusCodes.Status409Conflict, "conflict"),
        _ => (StatusCodes.Status500InternalServerError, "internal_error")
    };
}
