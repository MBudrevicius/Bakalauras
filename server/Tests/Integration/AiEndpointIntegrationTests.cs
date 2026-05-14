using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using server.Data;
using server.Models;

namespace server.Tests.Integration;

/// <summary>
/// Integration tests for AI detection endpoints - verifies text analysis pipeline:
/// HTTP request → AiCheckService → parallel heuristic checks → optional Claude → score → DB persistence.
/// </summary>
public class AiEndpointIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public AiEndpointIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AiChecks_WithText_RunsAllHeuristicsAndReturnsScore()
    {
        var text = "The implementation of artificial intelligence in modern applications has fundamentally transformed the way we interact with technology. " +
                   "Machine learning algorithms process vast amounts of data to generate insights that were previously impossible to obtain through traditional methods. " +
                   "Furthermore, natural language processing enables systems to understand and generate human-like text with remarkable accuracy.";

        // Act
        var response = await _client.PostAsJsonAsync("/api/ai-checks",
            new AiCheckRequest { Text = text });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiCheckResponse>();
        Assert.NotNull(result);
        Assert.True(result.TextLength > 0);
        Assert.NotEmpty(result.Results);
        Assert.InRange(result.OverallAiScore, 0, 100);
        Assert.True(result.Results.Count >= 8, "Expected at least 8 heuristic check results");
    }

    [Fact]
    public async Task AiChecks_WithText_TruncatesAnalyzedTextAt500Chars()
    {
        var text = new string('A', 600) + " " + new string('B', 200);

        // Act
        var response = await _client.PostAsJsonAsync("/api/ai-checks",
            new AiCheckRequest { Text = text });
        var result = await response.Content.ReadFromJsonAsync<AiCheckResponse>();

        // Assert
        Assert.NotNull(result);
        Assert.True(result.AnalyzedText.Length <= 501); // 500 + '…'
        Assert.Equal(text.Length, result.TextLength);
    }

    [Fact]
    public async Task AiChecks_ShortText_StillReturnsResults()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/ai-checks",
            new AiCheckRequest { Text = "Short text." });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiCheckResponse>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Results);
    }

    [Fact]
    public async Task AiChecks_WithApiKey_IncludesClaudeInResults()
    {
        _factory.FakeApiHandler.AnthropicAiScore = 75;

        var text = "This is a sufficiently long text for comprehensive AI analysis that examines multiple aspects of writing style and patterns across many sentences.";

        // Act
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ai-checks");
        request.Headers.Add("X-Claude-Api-Key", "test-key-123");
        request.Headers.Add("X-Claude-Model", "claude-haiku-4-5-20251001");
        request.Content = JsonContent.Create(new AiCheckRequest { Text = text });
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AiCheckResponse>();
        Assert.NotNull(result);
        var claudeResult = result.Results.FirstOrDefault(r => r.Type == AiCheckType.ClaudeAiModel);
        Assert.NotNull(claudeResult);
        Assert.Equal(75, claudeResult.AiScore);
    }

    [Fact]
    public async Task AiChecks_PersistsScoreToDatabase_WhenUrlProvided()
    {
        var url = "https://ai-persist-" + Guid.NewGuid().ToString("N")[..8] + ".com";
        var text = "Machine learning algorithms leverage sophisticated neural network architectures to achieve unprecedented levels of performance.";

        // Act
        var response = await _client.PostAsJsonAsync("/api/ai-checks",
            new AiCheckRequest { Text = text, Url = url });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var domain = new Uri(url).Host.ToLowerInvariant();
        var saved = db.PageScores.FirstOrDefault(p => p.Domain == domain);
        Assert.NotNull(saved);
        Assert.True(saved.AiCheckCount >= 1);
    }

    [Fact]
    public async Task AiChecks_WithoutUrl_DoesNotPersistToDatabase()
    {
        var text = "This text is analyzed but not associated with any URL.";

        // Act
        await _client.PostAsJsonAsync("/api/ai-checks", new AiCheckRequest { Text = text });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.DoesNotContain(db.PageScores, p => p.Domain == "");
    }

    [Fact]
    public async Task AiChecks_AllModels_RequiresApiKey()
    {
        var response = await _client.PostAsJsonAsync("/api/ai-checks/all-models",
            new AiCheckRequest { Text = "Some text to analyze" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Highlight_ValidSegments_ReturnsScoresArray()
    {
        var segments = new[] { "The algorithm processes data efficiently", "I went to the store today" };

        // Act
        var response = await _client.PostAsJsonAsync("/api/ai-checks/highlight",
            new HighlightRequest { Segments = segments });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("scores", content);
    }

    [Fact]
    public async Task Highlight_ExactlyMaxSegments_ReturnsOk()
    {
        var segments = Enumerable.Range(0, 500)
            .Select(i => $"Segment number {i} with enough words to be meaningful")
            .ToArray();

        // Act
        var response = await _client.PostAsJsonAsync("/api/ai-checks/highlight",
            new HighlightRequest { Segments = segments });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Highlight_OverMaxSegments_ReturnsBadRequest()
    {
        var segments = Enumerable.Range(0, 501).Select(i => $"Segment {i}").ToArray();

        // Act
        var response = await _client.PostAsJsonAsync("/api/ai-checks/highlight",
            new HighlightRequest { Segments = segments });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AiChecks_ResultsAreOrderedClaudeFirst()
    {
        _factory.FakeApiHandler.AnthropicAiScore = 60;

        var text = "Comprehensive analysis requires sufficient text content to produce meaningful heuristic scores across all check categories including multiple sentences.";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/ai-checks");
        request.Headers.Add("X-Claude-Api-Key", "test-key");
        request.Content = JsonContent.Create(new AiCheckRequest { Text = text });

        // Act
        var response = await _client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<AiCheckResponse>();

        Assert.NotNull(result);
        var claudeResult = result.Results.FirstOrDefault(r => r.Type == AiCheckType.ClaudeAiModel);
        if (claudeResult != null)
        {
            Assert.Equal(AiCheckType.ClaudeAiModel, result.Results.First().Type);
        }
    }
}
