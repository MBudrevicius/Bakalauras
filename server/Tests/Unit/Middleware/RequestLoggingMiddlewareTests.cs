using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using server.Middleware;

namespace server.Tests.Unit.Middleware;

public class RequestLoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SuccessfulRequest_LogsInformation()
    {
        var logger = new Mock<ILogger<RequestLoggingMiddleware>>();
        var nextCalled = false;

        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/test";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        // Verify logging happened (at least 2 calls: start + complete)
        logger.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(2));
    }

    [Fact]
    public async Task InvokeAsync_FailedRequest_LogsWarning()
    {
        var logger = new Mock<ILogger<RequestLoggingMiddleware>>();

        RequestDelegate next = (ctx) =>
        {
            ctx.Response.StatusCode = 500;
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/api/security-checks";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(1));
    }

    [Fact]
    public async Task InvokeAsync_ExceptionThrown_LogsErrorAndRethrows()
    {
        var logger = new Mock<ILogger<RequestLoggingMiddleware>>();

        RequestDelegate next = (_) => throw new InvalidOperationException("Test error");

        var middleware = new RequestLoggingMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/api/crash";
        context.Response.Body = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => middleware.InvokeAsync(context));

        logger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeast(1));
    }

    [Fact]
    public async Task InvokeAsync_ResponseBodyRestoredAfterSuccess()
    {
        var logger = new Mock<ILogger<RequestLoggingMiddleware>>();
        var originalBody = new MemoryStream();

        RequestDelegate next = (ctx) =>
        {
            ctx.Response.StatusCode = 200;
            var bytes = System.Text.Encoding.UTF8.GetBytes("response body");
            ctx.Response.Body.Write(bytes);
            return Task.CompletedTask;
        };

        var middleware = new RequestLoggingMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = originalBody;

        await middleware.InvokeAsync(context);

        // Body should be restored to original stream
        Assert.Same(originalBody, context.Response.Body);
        Assert.True(originalBody.Length > 0);
    }

    [Fact]
    public async Task InvokeAsync_ResponseBodyRestoredAfterException()
    {
        var logger = new Mock<ILogger<RequestLoggingMiddleware>>();
        var originalBody = new MemoryStream();

        RequestDelegate next = (_) => throw new Exception("boom");

        var middleware = new RequestLoggingMiddleware(next, logger.Object);

        var context = new DefaultHttpContext();
        context.Response.Body = originalBody;

        try { await middleware.InvokeAsync(context); } catch { }

        Assert.Same(originalBody, context.Response.Body);
    }
}
