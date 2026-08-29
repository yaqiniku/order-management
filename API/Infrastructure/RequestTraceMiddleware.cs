using System.Diagnostics;

namespace API.Infrastructure;

public sealed class RequestTraceMiddleware(RequestDelegate next, ILogger<RequestTraceMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        var suppliedId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        context.TraceIdentifier = string.IsNullOrWhiteSpace(suppliedId)
            ? Guid.NewGuid().ToString("N")
            : suppliedId.Trim();
        context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;

        var stopwatch = Stopwatch.StartNew();
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = context.TraceIdentifier
        });

        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var (status, code) = ApiErrors.Classify(exception);
            logger.LogError(exception, "Unhandled request error {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(
                ApiErrors.Create(context, code,
                    status == StatusCodes.Status500InternalServerError
                        ? "Terjadi kesalahan internal server."
                        : exception.Message));
        }
        finally
        {
            logger.LogInformation(
                "Request {Method} {Path} completed with {StatusCode} in {ElapsedMs} ms",
                context.Request.Method, context.Request.Path,
                context.Response.StatusCode, stopwatch.ElapsedMilliseconds);
        }
    }
}
