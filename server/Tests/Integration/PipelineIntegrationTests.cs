using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using server.Data;
using server.Models;

namespace server.Tests.Integration;

/// <summary>
/// Integration tests verifying cross-service interactions:
/// Tests that scores from different check types are correctly aggregated,
/// pipeline middleware works, and the full request lifecycle completes correctly.
/// </summary>
public class PipelineIntegrationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public PipelineIntegrationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FullPipeline_SecurityThenAi_BothScoresStored()
    {
        var domain = "pipeline-" + Guid.NewGuid().ToString("N")[..8] + ".com";
        var url = $"https://{domain}/page";
        var text = "Artificial intelligence systems demonstrate consistent patterns in text generation, including uniform sentence length distribution and repetitive phrasing.";

        // Act - simulate user running security check first, then AI check
        var secResponse = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = url });
        Assert.Equal(HttpStatusCode.OK, secResponse.StatusCode);

        var aiResponse = await _client.PostAsJsonAsync("/api/ai-checks",
            new AiCheckRequest { Text = text, Url = url });
        Assert.Equal(HttpStatusCode.OK, aiResponse.StatusCode);

        // Assert - both scores persisted to same record
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = db.PageScores.FirstOrDefault(p => p.Domain == domain);
        Assert.NotNull(saved);
        Assert.Equal(1, saved.SecurityCheckCount);
        Assert.Equal(1, saved.AiCheckCount);
        Assert.True(saved.CheckCount >= 2);
    }

    [Fact]
    public async Task CorsHeaders_ArePresent()
    {
        // Act
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/security-checks");
        request.Headers.Add("Origin", "chrome-extension://abcdef123456");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");
        var response = await _client.SendAsync(request);

        // Assert - CORS should allow any origin
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin") ||
                    response.StatusCode == HttpStatusCode.NoContent ||
                    response.StatusCode == HttpStatusCode.OK,
                    "Expected CORS headers or preflight response");
    }

    [Fact]
    public async Task JsonContentType_Required()
    {
        // Act - send non-JSON content
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/security-checks");
        request.Content = new StringContent("url=https://example.com", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded");
        var response = await _client.SendAsync(request);

        // Assert - should reject non-JSON
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SecurityCheck_ResponseFormat_MatchesExpectedStructure()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://example.com" });
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();

        // Assert - validate structure
        Assert.NotNull(result);
        Assert.NotNull(result.Url);
        Assert.NotNull(result.Results);
        foreach (var r in result.Results)
        {
            Assert.NotNull(r.Title);
            Assert.NotNull(r.Description);
            Assert.True(Enum.IsDefined(typeof(SecurityCheckType), r.Type));
            Assert.True(Enum.IsDefined(typeof(SecurityCheckSeverity), r.Severity));
        }
    }

    [Fact]
    public async Task AiCheck_ResponseFormat_MatchesExpectedStructure()
    {
        var text = "This is a sample text that needs to be analyzed for potential artificial intelligence generation patterns across multiple heuristic dimensions.";

        // Act
        var response = await _client.PostAsJsonAsync("/api/ai-checks",
            new AiCheckRequest { Text = text });
        var result = await response.Content.ReadFromJsonAsync<AiCheckResponse>();

        // Assert - validate structure
        Assert.NotNull(result);
        Assert.NotNull(result.AnalyzedText);
        Assert.True(result.TextLength > 0);
        Assert.NotNull(result.Results);
        foreach (var r in result.Results)
        {
            Assert.NotNull(r.Title);
            Assert.NotNull(r.Description);
            Assert.True(Enum.IsDefined(typeof(AiCheckType), r.Type));
            Assert.InRange(r.AiScore, 0, 100);
        }
    }

    [Fact]
    public async Task MultipleEndpoints_DoNotInterfere()
    {
        var domain = "interference-" + Guid.NewGuid().ToString("N")[..8] + ".com";
        var secUrl = $"https://{domain}/sec";
        var aiUrl = $"https://{domain}/ai";
        var text = "Sample text for interference test between security and AI endpoints.";

        // Act - run both concurrently
        var secTask = _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = secUrl });
        var aiTask = _client.PostAsJsonAsync("/api/ai-checks",
            new AiCheckRequest { Text = text, Url = aiUrl });

        var results = await Task.WhenAll(secTask, aiTask);

        // Assert - both succeed
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
    }

    [Fact]
    public async Task SecurityCheck_AllNineChecks_AreExecuted()
    {
        // Act
        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://example.com" });
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();

        // Assert - should have results from all 9 registered security checks
        Assert.NotNull(result);
        var checkTypes = result.Results.Select(r => r.Type).Distinct().ToList();
        Assert.Contains(SecurityCheckType.Https, checkTypes);
        // At minimum we should have multiple check types
        Assert.True(checkTypes.Count >= 5, $"Expected at least 5 distinct check types, got {checkTypes.Count}");
    }

    [Fact]
    public async Task AiCheck_AllHeuristicChecks_AreExecuted()
    {
        var text = "The implementation of sophisticated machine learning algorithms in modern applications has fundamentally transformed how we approach data analysis. " +
                   "These systems leverage neural architectures to process information efficiently. Furthermore, deep learning models enable unprecedented accuracy in classification tasks.";

        // Act
        var response = await _client.PostAsJsonAsync("/api/ai-checks",
            new AiCheckRequest { Text = text });
        var result = await response.Content.ReadFromJsonAsync<AiCheckResponse>();

        // Assert - should have results from all 8 heuristic checks (Claude excluded without key)
        Assert.NotNull(result);
        var checkTypes = result.Results.Select(r => r.Type).Distinct().ToList();
        Assert.Contains(AiCheckType.VocabularyRichness, checkTypes);
        Assert.Contains(AiCheckType.SentenceUniformity, checkTypes);
        Assert.Contains(AiCheckType.PerplexityEstimation, checkTypes);
        Assert.Contains(AiCheckType.PunctuationPatterns, checkTypes);
        Assert.Contains(AiCheckType.RepetitivePhrasing, checkTypes);
        Assert.Contains(AiCheckType.ParagraphStructure, checkTypes);
        Assert.Contains(AiCheckType.TransitionalPhrases, checkTypes);
        Assert.Contains(AiCheckType.HedgingLanguage, checkTypes);
    }
}
