using System.Net;

namespace server.Tests.Unit.Helpers;

/// <summary>
/// A test HttpMessageHandler that returns a pre-configured response.
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _content;
    private readonly Dictionary<string, string> _responseHeaders;

    public HttpRequestMessage? LastRequest { get; private set; }

    public MockHttpMessageHandler(string content = "", HttpStatusCode statusCode = HttpStatusCode.OK, Dictionary<string, string>? responseHeaders = null)
    {
        _content = content;
        _statusCode = statusCode;
        _responseHeaders = responseHeaders ?? [];
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        var response = new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_content)
        };

        foreach (var header in _responseHeaders)
        {
            response.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return Task.FromResult(response);
    }
}

/// <summary>
/// An IHttpClientFactory that returns a client with the given handler.
/// </summary>
public class MockHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public MockHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name) => new(_handler);
}

/// <summary>
/// A handler that invokes a callback per request, allowing different responses per URL.
/// </summary>
public class DelegatingMockHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
    public DelegatingMockHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(_handler(request));
}

/// <summary>
/// A handler that always throws an HttpRequestException.
/// </summary>
public class ThrowingHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => throw new HttpRequestException("Connection refused");
}
