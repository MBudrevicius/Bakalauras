using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using server.Data;
using server.Models;

namespace server.Tests.Integration;

/// <summary>
/// Integration tests for cross-check (credibility) endpoints - verifies:
/// HTTP request → CrossCheckService → BraveSearch → source reliability → Claude verification → DB persistence.
/// </summary>
public class CrossCheckEndpointIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public CrossCheckEndpointIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CrossCheck_ValidUrlWithoutApiKey_ReturnsOkWithBasicData()
    {
        // Without API key, Claude calls are skipped but the endpoint still works
        // BraveSearch will return fake results from the handler

        // Act
        var response = await _client.PostAsJsonAsync("/api/cross-check",
            new CrossCheckRequest
            {
                Url = "https://news-example.com/article",
                Title = "Test Article About Technology",
                Text = "Content of the article about technology advancements"
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CrossCheckResponse>();
        Assert.NotNull(result);
        Assert.Equal("https://news-example.com/article", result.Url);
        Assert.NotEmpty(result.Topic); // Should fall back to cleaned title
    }

    [Fact]
    public async Task CrossCheck_WithApiKey_PerformsFullPipeline()
    {
        // Configure fake handler for full pipeline
        _factory.FakeApiHandler.AnthropicTopic = "climate change effects 2026";
        _factory.FakeApiHandler.AnthropicCredibilityScore = 78;
        _factory.FakeApiHandler.AnthropicCredibilityVerdict = "Mostly Supported";
        _factory.FakeApiHandler.AnthropicClaims = ["Temperature rise: Supported - matches NASA data", "Sea level claim: Supported - corroborated by NOAA"];
        _factory.FakeApiHandler.AnthropicSourceReliabilityScores = [85, 70];
        _factory.FakeApiHandler.BraveSearchResults =
        [
            ("https://reliable-source.com", "Climate Report 2026", "Scientific data on climate change effects"),
            ("https://another-source.com", "Environmental Study", "Research findings on global warming")
        ];

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/cross-check");
        request.Headers.Add("X-Claude-Api-Key", "test-key-123");
        request.Headers.Add("X-Claude-Model", "claude-haiku-4-5-20251001");
        request.Content = JsonContent.Create(new CrossCheckRequest
        {
            Url = "https://article-test.com/news",
            Title = "Climate Change Impact",
            Text = "Article about the effects of climate change on global ecosystems.",
            PageLinks = ["https://external-site.com/ref1", "https://example.org/data"]
        });
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CrossCheckResponse>();
        Assert.NotNull(result);
        Assert.Equal("climate change effects 2026", result.Topic);
        Assert.NotEmpty(result.RelatedPages);
        Assert.NotNull(result.Credibility);
        Assert.Equal(78, result.Credibility.Score);
        Assert.Equal("Mostly Supported", result.Credibility.Verdict);
        Assert.NotEmpty(result.SourceReliability);
    }

    [Fact]
    public async Task CrossCheck_WithApiKey_PersistsCredibilityScore()
    {
        var url = "https://cred-persist-" + Guid.NewGuid().ToString("N")[..8] + ".com/page";

        _factory.FakeApiHandler.AnthropicCredibilityScore = 65;
        _factory.FakeApiHandler.AnthropicSourceReliabilityScores = [90];
        _factory.FakeApiHandler.BraveSearchResults =
        [
            ("https://source.com/article", "Source", "Info about the topic")
        ];

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/cross-check");
        request.Headers.Add("X-Claude-Api-Key", "test-key");
        request.Content = JsonContent.Create(new CrossCheckRequest
        {
            Url = url,
            Title = "Test",
            Text = "Some article text for credibility testing"
        });
        await _client.SendAsync(request);

        // Assert - credibility stored
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var domain = new Uri(url).Host.ToLowerInvariant();
        var saved = db.PageScores.FirstOrDefault(p => p.Domain == domain);
        Assert.NotNull(saved);
        Assert.Equal(65, saved.CredibilityScore);
        Assert.Equal(1, saved.CredibilityCheckCount);
    }

    [Fact]
    public async Task CrossCheck_AnalyzesPageLinks_ReturnsExternalDomainCount()
    {
        _factory.FakeApiHandler.BraveSearchResults = [];

        // Act
        var response = await _client.PostAsJsonAsync("/api/cross-check",
            new CrossCheckRequest
            {
                Url = "https://testsite.com/article",
                Title = "Test Article Title",
                Text = "Article content",
                PageLinks = [
                    "https://external1.com/page",
                    "https://external2.com/page",
                    "https://testsite.com/internal", // same domain - not counted
                    "https://facebook.com/share"     // common non-source - not counted
                ]
            });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CrossCheckResponse>();
        Assert.NotNull(result);
        Assert.True(result.PageLinkDomains >= 2);
    }

    [Fact]
    public async Task CrossCheckAllModels_RequiresApiKey()
    {
        // Act - no API key
        var response = await _client.PostAsJsonAsync("/api/cross-check/all-models",
            new CrossCheckRequest { Url = "https://example.com", Title = "T", Text = "C" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CrossCheckAllModels_WithApiKey_RunsThreeModels()
    {
        _factory.FakeApiHandler.AnthropicTopic = "test topic";
        _factory.FakeApiHandler.AnthropicCredibilityScore = 70;
        _factory.FakeApiHandler.AnthropicCredibilityVerdict = "Mostly Supported";
        _factory.FakeApiHandler.AnthropicSourceReliabilityScores = [80];
        _factory.FakeApiHandler.BraveSearchResults =
        [
            ("https://s.com/page", "Source Title", "Relevant snippet about the topic")
        ];

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/cross-check/all-models");
        request.Headers.Add("X-Claude-Api-Key", "test-key");
        request.Content = JsonContent.Create(new CrossCheckRequest
        {
            Url = "https://multi-model-test.com",
            Title = "Article",
            Text = "Content for multi-model cross-check analysis"
        });
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CrossCheckResponse>();
        Assert.NotNull(result);
        // Should have model-specific results for 3 models
        Assert.NotNull(result.ModelResults);
        Assert.Equal(3, result.ModelResults.Count);
    }

    [Fact]
    public async Task CrossCheck_LowReliabilitySources_FilteredOut()
    {
        // Set source reliability scores below threshold (< 50)
        _factory.FakeApiHandler.AnthropicSourceReliabilityScores = [30, 20];
        _factory.FakeApiHandler.AnthropicCredibilityScore = 50;
        _factory.FakeApiHandler.BraveSearchResults =
        [
            ("https://unreliable1.com", "Bad Source", "Unreliable content"),
            ("https://unreliable2.com", "Low Quality", "Tabloid content")
        ];

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/cross-check");
        request.Headers.Add("X-Claude-Api-Key", "test-key");
        request.Content = JsonContent.Create(new CrossCheckRequest
        {
            Url = "https://filter-test.com/article",
            Title = "Test",
            Text = "Article that should still get credibility checked"
        });
        var response = await _client.SendAsync(request);

        // Assert - endpoint should still succeed
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CrossCheckResponse>();
        Assert.NotNull(result);
        // All sources are unreliable (< 50), but the pipeline still falls back
        Assert.NotEmpty(result.SourceReliability);
    }
}
