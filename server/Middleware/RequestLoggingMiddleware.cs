using System.Diagnostics;

namespace server.Middleware;

/// <summary>
/// Middleware to log incoming API requests and responses with structured logging.
/// Captures request details, response status, and execution time.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestId = context.TraceIdentifier;
        var method = context.Request.Method;
        var path = context.Request.Path;
        var queryString = context.Request.QueryString.ToString();

        // Log request start
        _logger.LogInformation(
            "API Request Started {@RequestDetails}",
            new
            {
                RequestId = requestId,
                Method = method,
                Path = path,
                QueryString = queryString,
                RemoteIP = context.Connection.RemoteIpAddress,
                Timestamp = DateTime.UtcNow
            });

        // Store original response stream
        var originalResponseBody = context.Response.Body;

        try
        {
            using (var responseBody = new MemoryStream())
            {
                context.Response.Body = responseBody;

                // Process the request
                await _next(context);

                stopwatch.Stop();

                var statusCode = context.Response.StatusCode;
                var isSuccess = statusCode >= 200 && statusCode < 300;
                var logLevel = isSuccess ? LogLevel.Information : LogLevel.Warning;

                // Log request completed
                _logger.Log(
                    logLevel,
                    "API Request Completed {@RequestCompletionDetails}",
                    new
                    {
                        RequestId = requestId,
                        Method = method,
                        Path = path,
                        StatusCode = statusCode,
                        ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                        IsSuccess = isSuccess,
                        Timestamp = DateTime.UtcNow
                    });

                // Copy response back to original stream
                await responseBody.CopyToAsync(originalResponseBody);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "API Request Failed {@RequestErrorDetails}",
                new
                {
                    RequestId = requestId,
                    Method = method,
                    Path = path,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    Exception = ex.GetType().Name,
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                });

            throw;
        }
        finally
        {
            context.Response.Body = originalResponseBody;
        }
    }
}

/// <summary>
/// Extension method to add request logging middleware to the pipeline.
/// </summary>
public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
