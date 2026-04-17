using System.Diagnostics;

namespace server.Middleware;

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

        var originalResponseBody = context.Response.Body;
        try
        {
            using var responseBody = new MemoryStream();
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
                    stopwatch.ElapsedMilliseconds,
                    IsSuccess = isSuccess,
                    Timestamp = DateTime.UtcNow
                });

            // Copy response back to original stream
            responseBody.Position = 0;
            await responseBody.CopyToAsync(originalResponseBody);
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
                    stopwatch.ElapsedMilliseconds,
                    Exception = ex.GetType().Name,
                    ex.Message,
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

public static class RequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RequestLoggingMiddleware>();
    }
}
