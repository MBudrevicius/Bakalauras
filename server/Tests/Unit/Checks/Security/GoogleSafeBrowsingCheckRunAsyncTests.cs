using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using server.Checks.Security;
using server.Models;
using server.Tests.Unit.Helpers;

namespace server.Tests.Unit.Checks.Security;

public class GoogleSafeBrowsingCheckRunAsyncTests
{
    private GoogleSafeBrowsingCheck CreateCheck(MockHttpMessageHandler handler, string? apiKey = "test-key")
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleSafeBrowsing:ApiKey"] = apiKey
            })
            .Build();
        var factory = new MockHttpClientFactory(handler);
        return new GoogleSafeBrowsingCheck(config, factory, NullLogger<GoogleSafeBrowsingCheck>.Instance);
    }

    [Fact]
    public async Task RunAsync_NoApiKey_ReturnsInfo()
    {
        var check = CreateCheck(new MockHttpMessageHandler(), apiKey: null);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
    }

    [Fact]
    public async Task RunAsync_EmptyApiKey_ReturnsInfo()
    {
        var check = CreateCheck(new MockHttpMessageHandler(), apiKey: "");
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
    }

    [Fact]
    public async Task RunAsync_NoThreatsFound_ReturnsPass()
    {
        var responseJson = "{}"; // empty response = no threats
        var check = CreateCheck(new MockHttpMessageHandler(responseJson));
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Pass, result.Severity);
        Assert.Contains("No threats", result.Description);
    }

    [Fact]
    public async Task RunAsync_ThreatFound_ReturnsWarning()
    {
        var responseJson = """
        {
          "matches": [
            {"threatType": "MALWARE"},
            {"threatType": "SOCIAL_ENGINEERING"}
          ]
        }
        """;
        var check = CreateCheck(new MockHttpMessageHandler(responseJson));
        var result = await check.RunAsync("https://evil.com");
        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("Malware", result.Description);
        Assert.Contains("Social Engineering", result.Description);
    }

    [Fact]
    public async Task RunAsync_ApiError_ReturnsInfo()
    {
        var check = CreateCheck(new MockHttpMessageHandler("", System.Net.HttpStatusCode.InternalServerError));
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
        Assert.Contains("500", result.Description);
    }

    [Fact]
    public async Task RunAsync_HttpException_ReturnsInfo()
    {
        var factory = new MockHttpClientFactory(new ThrowingHandler());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GoogleSafeBrowsing:ApiKey"] = "test-key"
            })
            .Build();
        var check = new GoogleSafeBrowsingCheck(config, factory, NullLogger<GoogleSafeBrowsingCheck>.Instance);
        var result = await check.RunAsync("https://example.com");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
        Assert.Contains("failed", result.Description);
    }

    [Fact]
    public void RunAsync_CorrectType()
    {
        var check = CreateCheck(new MockHttpMessageHandler());
        Assert.Equal(SecurityCheckType.GoogleSafeBrowsing, check.Type);
    }
}
