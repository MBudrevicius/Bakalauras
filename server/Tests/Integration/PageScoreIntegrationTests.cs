using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using server.Data;
using server.Models;

namespace server.Tests.Integration;

/// <summary>
/// Integration tests for PageScore storage and retrieval through the API pipeline:
/// Tests the GET /api/page-score endpoint and verifies cross-service score persistence.
/// </summary>
public class PageScoreIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public PageScoreIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PageScore_UnknownUrl_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/page-score?url=https://never-seen-before-xyz.com");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("false", content); // found: false
    }

    [Fact]
    public async Task PageScore_AfterSecurityCheck_ReturnsStoredScore()
    {
        var url = "https://score-retrieve-" + Guid.NewGuid().ToString("N")[..8] + ".com";

        await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = url });

        var response = await _client.GetAsync($"/api/page-score?url={Uri.EscapeDataString(url)}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("true", content); // found: true
    }

    [Fact]
    public async Task PageScore_AggregatesMultipleCheckTypes()
    {
        var url = "https://aggregate-" + Guid.NewGuid().ToString("N")[..8] + ".com";
        var text = "Test content for aggregation of scores across different analysis types.";

        await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = url });
        await _client.PostAsJsonAsync("/api/ai-checks",
            new AiCheckRequest { Text = text, Url = url });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var domain = new Uri(url).Host.ToLowerInvariant();
        var saved = db.PageScores.FirstOrDefault(p => p.Domain == domain);
        Assert.NotNull(saved);
        Assert.True(saved.SecurityCheckCount >= 1);
        Assert.True(saved.AiCheckCount >= 1);
        Assert.InRange(saved.SecurityScore, 0, 100);
        Assert.InRange(saved.AiScore, 0, 100);
    }

    [Fact]
    public async Task PageScore_EmptyQueryParam_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/page-score?url=");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PageScore_SameDomainDifferentPaths_SharesScore()
    {
        var domain = "shared-domain-" + Guid.NewGuid().ToString("N")[..8] + ".com";
        var url1 = $"https://{domain}/article1";
        var url2 = $"https://{domain}/article2";

        await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = url1 });
        await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = url2 });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var records = db.PageScores.Where(p => p.Domain == domain).ToList();
        Assert.Single(records);
        Assert.Equal(2, records[0].SecurityCheckCount);
    }

    [Fact]
    public async Task PageScore_PreSeeded_ReturnsSavedData()
    {
        var domain = "preseeded-" + Guid.NewGuid().ToString("N")[..8] + ".com";
        var url = $"https://{domain}/page";

        await _factory.SeedDatabaseAsync(db =>
        {
            db.PageScores.Add(new PageScore
            {
                Url = domain,
                Domain = domain,
                SecurityScore = 90,
                AiScore = 30,
                CredibilityScore = 75,
                SecurityCheckCount = 3,
                AiCheckCount = 2,
                CredibilityCheckCount = 1,
                CheckCount = 6,
                LastChecked = DateTime.UtcNow
            });
        });

        // Act
        var response = await _client.GetAsync($"/api/page-score?url={Uri.EscapeDataString(url)}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("true", content);
    }
}
