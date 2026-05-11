using Microsoft.Extensions.Logging.Abstractions;
using server.Checks.Security;
using server.Models;
using server.Tests.Unit.Helpers;

namespace server.Tests.Unit.Checks.Security;

public class SuspiciousLinksCheckRunAsyncTests
{
    private SuspiciousLinksCheck CreateCheck(MockHttpMessageHandler handler)
    {
        var factory = new MockHttpClientFactory(handler);
        return new SuspiciousLinksCheck(factory, NullLogger<SuspiciousLinksCheck>.Instance);
    }

    [Fact]
    public async Task RunAsync_InvalidUrl_ReturnsInfo()
    {
        var check = CreateCheck(new MockHttpMessageHandler());
        var result = await check.RunAsync("not-a-url");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
    }

    [Fact]
    public async Task RunAsync_PrivateIp_ReturnsInfo()
    {
        var check = CreateCheck(new MockHttpMessageHandler());
        var result = await check.RunAsync("https://192.168.1.1/page");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
    }

    [Fact]
    public async Task RunAsync_NoLinks_ReturnsPass()
    {
        var html = "<html><body><p>No links here</p></body></html>";
        var check = CreateCheck(new MockHttpMessageHandler(html));
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Pass, result.Severity);
    }

    [Fact]
    public async Task RunAsync_SafeLinks_ReturnsPass()
    {
        var html = "<html><body><a href='https://safe.com/page'>Safe Link</a></body></html>";
        var check = CreateCheck(new MockHttpMessageHandler(html));
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Pass, result.Severity);
    }

    [Fact]
    public async Task RunAsync_HiddenLinks_ReturnsWarning()
    {
        var html = "<html><body><a href='https://evil.com' style='display:none'>Hidden</a></body></html>";
        var check = CreateCheck(new MockHttpMessageHandler(html));
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("hidden", result.Description);
    }

    [Fact]
    public async Task RunAsync_SuspiciousTldLinks_ReturnsInfo()
    {
        var html = "<html><body><a href='https://evil.tk/page'>Evil</a></body></html>";
        var check = CreateCheck(new MockHttpMessageHandler(html));
        var result = await check.RunAsync("https://example.com");
        Assert.Contains("suspicious TLD", result.Description);
    }

    [Fact]
    public async Task RunAsync_MismatchedLinkText_ReturnsWarning()
    {
        var html = "<html><body><a href='https://evil.com/steal'>https://paypal.com/login</a></body></html>";
        var check = CreateCheck(new MockHttpMessageHandler(html));
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("mismatched", result.Description);
    }

    [Fact]
    public async Task RunAsync_SameDomainLinks_NotCountedAsExternal()
    {
        var html = "<html><body>" +
                   "<a href='https://example.com/page1'>Page 1</a>" +
                   "<a href='https://example.com/page2'>Page 2</a>" +
                   "</body></html>";
        var check = CreateCheck(new MockHttpMessageHandler(html));
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Pass, result.Severity);
    }

    [Fact]
    public async Task RunAsync_HttpException_ReturnsInfo()
    {
        var factory = new MockHttpClientFactory(new ThrowingHandler());
        var check = new SuspiciousLinksCheck(factory, NullLogger<SuspiciousLinksCheck>.Instance);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
        Assert.Contains("Could not fetch", result.Description);
    }

    [Fact]
    public async Task RunAsync_HighExternalLinks_WarnsAboutCount()
    {
        var links = string.Join("\n", Enumerable.Range(1, 25).Select(i => $"<a href='https://external{i}.com/page'>Link {i}</a>"));
        var html = $"<html><body>{links}</body></html>";
        var check = CreateCheck(new MockHttpMessageHandler(html));
        var result = await check.RunAsync("https://example.com");
        Assert.Contains("external links", result.Description);
    }

    [Fact]
    public async Task RunAsync_MismatchedAnchorText_ReturnsWarning()
    {
        var html = "<html><body><a href='https://evil.com/steal'>https://bankofamerica.com</a></body></html>";
        var check = CreateCheck(new MockHttpMessageHandler(html));
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("mismatched", result.Description);
    }

    [Fact]
    public void RunAsync_CorrectType()
    {
        var check = CreateCheck(new MockHttpMessageHandler());
        Assert.Equal(SecurityCheckType.SuspiciousLinks, check.Type);
    }
}
