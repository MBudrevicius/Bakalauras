using server.Checks.Security;
using server.Models;

namespace server.Tests.Unit.Checks.Security;

public class HttpsCheckTests
{
    private readonly HttpsCheck _check = new();

    [Fact]
    public async Task RunAsync_HttpsUrl_ReturnsPass()
    {
        var result = await _check.RunAsync("https://example.com");

        Assert.Equal(SecurityCheckSeverity.Pass, result.Severity);
        Assert.Contains("HTTPS", result.Description);
    }

    [Fact]
    public async Task RunAsync_HttpUrl_ReturnsWarning()
    {
        var result = await _check.RunAsync("http://example.com");

        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("not use HTTPS", result.Description);
    }

    [Fact]
    public async Task RunAsync_InvalidUrl_ReturnsInfo()
    {
        var result = await _check.RunAsync("not-a-url");

        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
    }

    [Fact]
    public async Task RunAsync_ReturnsCorrectType()
    {
        var result = await _check.RunAsync("https://example.com");

        Assert.Equal(SecurityCheckType.Https, result.Type);
    }

    [Fact]
    public async Task RunAsync_ReturnsCorrectTitle()
    {
        var result = await _check.RunAsync("https://example.com");

        Assert.Equal("HTTPS", result.Title);
    }
}
