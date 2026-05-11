using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using server.Data;
using server.Models;

namespace server.Tests.Integration;

/// <summary>
/// Integration tests for Security endpoints - verifies the full pipeline:
/// HTTP request → endpoint routing → service orchestration → individual checks → DB persistence → response.
/// </summary>
public class SecurityEndpointIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public SecurityEndpointIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SecurityChecks_ValidHttpsUrl_ReturnsAllCheckResults()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://example.com" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);
        Assert.Equal("https://example.com", result.Url);
        Assert.NotEmpty(result.Results);
        Assert.InRange(result.OverallScore, 0, 100);
    }

    [Fact]
    public async Task SecurityChecks_HttpUrl_ContainsHttpsWarning()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "http://example.com" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);

        var httpsCheck = result.Results.FirstOrDefault(r => r.Type == SecurityCheckType.Https);
        Assert.NotNull(httpsCheck);
        Assert.Equal(SecurityCheckSeverity.Warning, httpsCheck.Severity);
    }

    [Fact]
    public async Task SecurityChecks_PersistsScoreToDatabase()
    {
        var url = "https://persist-test-" + Guid.NewGuid().ToString("N")[..8] + ".com";

        // Act
        await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = url });

        // Assert - verify the score was stored
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var domain = new Uri(url).Host.ToLowerInvariant();
        var saved = db.PageScores.FirstOrDefault(p => p.Domain == domain);
        Assert.NotNull(saved);
        Assert.True(saved.SecurityCheckCount >= 1);
        Assert.InRange(saved.SecurityScore, 0, 100);
    }

    [Fact]
    public async Task SecurityChecks_MultipleRuns_UpdatesRollingAverage()
    {
        var url = "https://rolling-" + Guid.NewGuid().ToString("N")[..8] + ".com";

        // Act - run checks twice on the same URL
        await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = url });
        await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = url });

        // Assert - check count should be 2
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var domain = new Uri(url).Host.ToLowerInvariant();
        var saved = db.PageScores.FirstOrDefault(p => p.Domain == domain);
        Assert.NotNull(saved);
        Assert.Equal(2, saved.SecurityCheckCount);
        Assert.Equal(2, saved.CheckCount);
    }

    [Fact]
    public async Task SecurityChecks_ReturnsCorrectScoreCalculation()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://example.com" });
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();

        // Assert - verify the score matches the severity-based formula
        Assert.NotNull(result);
        var expectedScore = 0;
        foreach (var r in result.Results)
        {
            expectedScore += r.Severity switch
            {
                SecurityCheckSeverity.Pass => 100,
                SecurityCheckSeverity.Info => 80,
                SecurityCheckSeverity.Warning => 0,
                _ => 100
            };
        }
        expectedScore /= result.Results.Count;
        Assert.Equal(expectedScore, result.OverallScore);
    }

    [Fact]
    public async Task SecurityChecks_NullBody_ReturnsBadRequest()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = null! });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SecurityChecks_UrlWithSpecialCharacters_HandlesGracefully()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://example.com/path?q=hello&lang=en#section" });

        // Assert - should still process correctly
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Results);
    }
}
