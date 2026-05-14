using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using server.Checks.Security;
using server.Models;
using server.Tests.Unit.Helpers;

namespace server.Tests.Unit.Checks.Security;

public class SecurityHeadersCheckTests
{
    private SecurityHeadersCheck CreateCheck(MockHttpMessageHandler handler)
    {
        var factory = new MockHttpClientFactory(handler);
        return new SecurityHeadersCheck(factory, NullLogger<SecurityHeadersCheck>.Instance);
    }

    [Fact]
    public async Task RunAsync_InvalidUrl_ReturnsInfo()
    {
        var handler = new MockHttpMessageHandler();
        var check = CreateCheck(handler);
        var result = await check.RunAsync("not-a-url");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
    }

    [Fact]
    public async Task RunAsync_PrivateIp_ReturnsInfo()
    {
        var handler = new MockHttpMessageHandler();
        var check = CreateCheck(handler);
        var result = await check.RunAsync("https://192.168.1.1/page");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
        Assert.Contains("private", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_AllHeadersPresent_ReturnsPass()
    {
        var handler = new MockHttpMessageHandler("", HttpStatusCode.OK,
            new Dictionary<string, string>
            {
                ["Strict-Transport-Security"] = "max-age=31536000",
                ["Content-Security-Policy"] = "default-src 'self'",
                ["X-Content-Type-Options"] = "nosniff",
                ["X-Frame-Options"] = "DENY",
                ["Referrer-Policy"] = "no-referrer",
                ["Permissions-Policy"] = "geolocation=()"
            });
        var check = CreateCheck(handler);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Pass, result.Severity);
    }

    [Fact]
    public async Task RunAsync_MostHeadersMissing_ReturnsWarning()
    {
        var handler = new MockHttpMessageHandler("", HttpStatusCode.OK);
        var check = CreateCheck(handler);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("Missing", result.Description);
    }

    [Fact]
    public async Task RunAsync_FewHeadersMissing_ReturnsInfo()
    {
        var handler = new MockHttpMessageHandler("", HttpStatusCode.OK,
            new Dictionary<string, string>
            {
                ["Strict-Transport-Security"] = "max-age=31536000",
                ["Content-Security-Policy"] = "default-src 'self'",
                ["X-Content-Type-Options"] = "nosniff",
            });
        var check = CreateCheck(handler);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
    }

    [Fact]
    public async Task RunAsync_HttpException_ReturnsInfo()
    {
        var factory = new MockHttpClientFactory(new ThrowingHandler());
        var check = new SecurityHeadersCheck(factory, NullLogger<SecurityHeadersCheck>.Instance);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
        Assert.Contains("Could not check", result.Description);
    }

    [Fact]
    public void RunAsync_CorrectType()
    {
        var handler = new MockHttpMessageHandler();
        var check = CreateCheck(handler);
        Assert.Equal(SecurityCheckType.SecurityHeaders, check.Type);
    }
}
