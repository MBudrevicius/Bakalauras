using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using server.Checks.Security;
using server.Models;
using server.Tests.Unit.Helpers;

namespace server.Tests.Unit.Checks.Security;

/// <summary>
/// Handler that simulates a redirect chain: maps URLs to (status, location) pairs.
/// </summary>
public class RedirectHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode status, string? location)> _responses;
    private readonly HttpStatusCode _defaultStatus;

    public RedirectHandler(Dictionary<string, (HttpStatusCode status, string? location)> responses, HttpStatusCode defaultStatus = HttpStatusCode.OK)
    {
        _responses = responses;
        _defaultStatus = defaultStatus;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        if (_responses.TryGetValue(url, out var entry))
        {
            var response = new HttpResponseMessage(entry.status);
            if (entry.location != null)
            {
                response.Headers.Location = new Uri(entry.location, UriKind.RelativeOrAbsolute);
            }
            return Task.FromResult(response);
        }
        return Task.FromResult(new HttpResponseMessage(_defaultStatus));
    }
}

public class RedirectChainCheckTests
{
    private static RedirectChainCheck CreateCheck(HttpMessageHandler handler)
    {
        var factory = new MockHttpClientFactory(handler);
        return new RedirectChainCheck(factory, NullLogger<RedirectChainCheck>.Instance);
    }

    [Fact]
    public async Task RunAsync_InvalidUrl_ReturnsInfo()
    {
        var check = CreateCheck(new MockHttpMessageHandler());
        var result = await check.RunAsync("not-a-url");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
        Assert.Contains("Could not parse", result.Description);
    }

    [Fact]
    public async Task RunAsync_PrivateIp_ReturnsInfo()
    {
        var check = CreateCheck(new MockHttpMessageHandler());
        var result = await check.RunAsync("https://192.168.1.1/page");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
        Assert.Contains("private", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_Localhost_ReturnsInfo()
    {
        var check = CreateCheck(new MockHttpMessageHandler());
        var result = await check.RunAsync("https://127.0.0.1/page");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
    }

    [Fact]
    public void Type_IsRedirectChain()
    {
        var check = CreateCheck(new MockHttpMessageHandler());
        Assert.Equal(SecurityCheckType.RedirectChain, check.Type);
    }

    [Fact]
    public async Task RunAsync_NoRedirects_ReturnsPass()
    {
        var handler = new MockHttpMessageHandler("OK");
        var check = CreateCheck(handler);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Pass, result.Severity);
        Assert.Contains("No redirects", result.Description);
    }

    [Fact]
    public async Task RunAsync_SingleCleanRedirect_ReturnsPass()
    {
        var handler = new RedirectHandler(new Dictionary<string, (HttpStatusCode, string?)>
        {
            ["https://example.com/"] = (HttpStatusCode.MovedPermanently, "https://example.com/new"),
            ["https://example.com/new"] = (HttpStatusCode.OK, null),
        });
        var check = CreateCheck(handler);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Pass, result.Severity);
        Assert.Contains("1 hop", result.Description);
    }

    [Fact]
    public async Task RunAsync_HttpsToHttpDowngrade_ReturnsWarning()
    {
        var handler = new RedirectHandler(new Dictionary<string, (HttpStatusCode, string?)>
        {
            ["https://example.com/"] = (HttpStatusCode.Found, "http://example.com/insecure"),
            ["http://example.com/insecure"] = (HttpStatusCode.OK, null),
        });
        var check = CreateCheck(handler);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("HTTPS to HTTP", result.Description);
    }

    [Fact]
    public async Task RunAsync_CrossDomainRedirect_ReturnsWarning()
    {
        var handler = new RedirectHandler(new Dictionary<string, (HttpStatusCode, string?)>
        {
            ["https://example.com/"] = (HttpStatusCode.Found, "https://evil.com/phish"),
            ["https://evil.com/phish"] = (HttpStatusCode.OK, null),
        });
        var check = CreateCheck(handler);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("different domain", result.Description);
    }

    [Fact]
    public async Task RunAsync_RedirectToPrivateIp_ReturnsWarning()
    {
        var handler = new RedirectHandler(new Dictionary<string, (HttpStatusCode, string?)>
        {
            ["https://example.com/"] = (HttpStatusCode.Found, "http://127.0.0.1/admin"),
        });
        var check = CreateCheck(handler);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("private", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_ExcessiveRedirects_ReturnsWarning()
    {
        var responses = new Dictionary<string, (HttpStatusCode, string?)>();
        for (int i = 0; i < 5; i++)
        {
            responses[$"https://example.com/r{i}"] = (HttpStatusCode.Found, $"https://example.com/r{i + 1}");
        }
        responses["https://example.com/r5"] = (HttpStatusCode.OK, null);
        responses["https://example.com/"] = (HttpStatusCode.Found, "https://example.com/r0");

        var handler = new RedirectHandler(responses);
        var check = CreateCheck(handler);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("Excessive", result.Description);
    }

    [Fact]
    public async Task RunAsync_RedirectWithNoLocation_StopsCleanly()
    {
        var handler = new RedirectHandler(new Dictionary<string, (HttpStatusCode, string?)>
        {
            ["https://example.com/"] = (HttpStatusCode.Found, null),
        });
        var check = CreateCheck(handler);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Pass, result.Severity);
        Assert.Contains("No redirects", result.Description);
    }

    [Fact]
    public async Task RunAsync_RelativeRedirect_Resolved()
    {
        var handler = new RedirectHandler(new Dictionary<string, (HttpStatusCode, string?)>
        {
            ["https://example.com/"] = (HttpStatusCode.Found, "/page2"),
            ["https://example.com/page2"] = (HttpStatusCode.OK, null),
        });
        var check = CreateCheck(handler);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Pass, result.Severity);
        Assert.Contains("1 hop", result.Description);
    }

    [Fact]
    public async Task RunAsync_SubdomainRedirect_NotCrossDomain()
    {
        var handler = new RedirectHandler(new Dictionary<string, (HttpStatusCode, string?)>
        {
            ["https://example.com/"] = (HttpStatusCode.Found, "https://www.example.com/"),
            ["https://www.example.com/"] = (HttpStatusCode.OK, null),
        });
        var check = CreateCheck(handler);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Pass, result.Severity);
    }

    [Fact]
    public async Task RunAsync_HttpException_ReturnsInfo()
    {
        var handler = new RedirectHandler(new Dictionary<string, (HttpStatusCode, string?)>(),
            HttpStatusCode.InternalServerError);
        var throwingHandler = new ThrowingHandler();
        var check = CreateCheck(throwingHandler);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
        Assert.Contains("Could not complete", result.Description);
    }
}

public class ThrowingHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        throw new HttpRequestException("Connection refused");
    }
}
