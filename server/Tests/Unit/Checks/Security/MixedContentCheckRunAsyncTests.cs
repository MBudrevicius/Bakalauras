using Microsoft.Extensions.Logging.Abstractions;
using server.Checks.Security;
using server.Models;
using server.Tests.Unit.Helpers;

namespace server.Tests.Unit.Checks.Security;

public class MixedContentCheckRunAsyncTests
{
    private MixedContentCheck CreateCheck(MockHttpMessageHandler handler)
    {
        var factory = new MockHttpClientFactory(handler);
        return new MixedContentCheck(factory, NullLogger<MixedContentCheck>.Instance);
    }

    [Fact]
    public async Task RunAsync_InvalidUrl_ReturnsInfo()
    {
        var check = CreateCheck(new MockHttpMessageHandler());
        var result = await check.RunAsync("not-a-url");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
    }

    [Fact]
    public async Task RunAsync_HttpUrl_ReturnsInfo()
    {
        var check = CreateCheck(new MockHttpMessageHandler());
        var result = await check.RunAsync("http://example.com");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
        Assert.Contains("not use HTTPS", result.Description);
    }

    [Fact]
    public async Task RunAsync_PrivateIp_ReturnsInfo()
    {
        var check = CreateCheck(new MockHttpMessageHandler());
        var result = await check.RunAsync("https://10.0.0.1/page");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
    }

    [Fact]
    public async Task RunAsync_NoMixedContent_ReturnsPass()
    {
        var html = "<html><body><img src='https://safe.com/img.png'/><script src='https://cdn.com/js.js'></script></body></html>";
        var check = CreateCheck(new MockHttpMessageHandler(html));
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Pass, result.Severity);
        Assert.Contains("No mixed content", result.Description);
    }

    [Fact]
    public async Task RunAsync_ActiveMixedContent_ReturnsWarning()
    {
        var html = "<html><body><script src='http://evil.com/script.js'></script></body></html>";
        var check = CreateCheck(new MockHttpMessageHandler(html));
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("active", result.Description);
    }

    [Fact]
    public async Task RunAsync_PassiveMixedContent_ReturnsInfo()
    {
        var html = "<html><body><img src='http://images.com/photo.jpg'/></body></html>";
        var check = CreateCheck(new MockHttpMessageHandler(html));
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
        Assert.Contains("passive", result.Description);
    }

    [Fact]
    public async Task RunAsync_BothActiveAndPassive_ReturnsWarning()
    {
        var html = "<html><body>" +
                   "<script src='http://evil.com/script.js'></script>" +
                   "<img src='http://images.com/photo.jpg'/>" +
                   "</body></html>";
        var check = CreateCheck(new MockHttpMessageHandler(html));
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("active", result.Description);
        Assert.Contains("passive", result.Description);
    }

    [Fact]
    public async Task RunAsync_MixedContentForms_ReturnsWarning()
    {
        var html = "<html><body><form action='http://evil.com/submit'></form></body></html>";
        var check = CreateCheck(new MockHttpMessageHandler(html));
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
    }

    [Fact]
    public async Task RunAsync_HttpException_ReturnsInfo()
    {
        var factory = new MockHttpClientFactory(new ThrowingHandler());
        var check = new MixedContentCheck(factory, NullLogger<MixedContentCheck>.Instance);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
        Assert.Contains("Could not complete", result.Description);
    }

    [Fact]
    public void RunAsync_CorrectType()
    {
        var check = CreateCheck(new MockHttpMessageHandler());
        Assert.Equal(SecurityCheckType.MixedContent, check.Type);
    }
}
