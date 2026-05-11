using System.Net;
using System.Text.Json;
using server.Clients;
using server.Models;
using server.Tests.Unit.Helpers;

namespace server.Tests.Unit.Clients;

public class AnthropicClientAsyncTests
{
    private static string AnthropicJsonResponse(string text) =>
        JsonSerializer.Serialize(new
        {
            content = new[] { new { text } }
        });

    // --- SendMessageAsync (tested indirectly) ---

    [Fact]
    public async Task DetectAiTextAsync_ValidResponse_ReturnsScore()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("75"));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var score = await client.DetectAiTextAsync("sk-test", "Some text to analyze for AI detection");

        Assert.Equal(75, score);
        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://api.anthropic.com/v1/messages", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task DetectAiTextAsync_TextWithNumber_ParsesScore()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("I estimate 85"));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var score = await client.DetectAiTextAsync("sk-test", "Some text");
        Assert.Equal(85, score);
    }

    [Fact]
    public async Task DetectAiTextAsync_ApiError_Throws()
    {
        var handler = new MockHttpMessageHandler("{\"error\":\"bad\"}", HttpStatusCode.Unauthorized);
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.DetectAiTextAsync("sk-test", "Some text"));
    }

    [Fact]
    public async Task DetectAiTextAsync_LongText_Truncated()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("50"));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var longText = new string('a', 5000);
        await client.DetectAiTextAsync("sk-test", longText);

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        // The prompt should not contain the full 5000 chars - it should be truncated to 4000
        Assert.DoesNotContain(new string('a', 5000), body);
    }

    [Fact]
    public async Task DetectAiTextAsync_SetsCorrectHeaders()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("50"));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        await client.DetectAiTextAsync("sk-test-key", "Some text");

        Assert.Contains("sk-test-key", handler.LastRequest!.Headers.GetValues("x-api-key"));
        Assert.Contains("2023-06-01", handler.LastRequest!.Headers.GetValues("anthropic-version"));
    }

    [Fact]
    public async Task DetectAiTextAsync_CustomModel_UsesIt()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("50"));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        await client.DetectAiTextAsync("sk-test", "Some text", "claude-sonnet-4-6");

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("claude-sonnet-4-6", body);
    }

    [Fact]
    public async Task DetectAiTextAsync_InvalidModel_UsesDefault()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("50"));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        await client.DetectAiTextAsync("sk-test", "Some text", "invalid-model");

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("claude-haiku-4-5-20251001", body);
    }

    [Fact]
    public async Task DetectAiTextAsync_NullModel_UsesDefault()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("50"));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        await client.DetectAiTextAsync("sk-test", "Some text", null);

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("claude-haiku-4-5-20251001", body);
    }

    // --- DetectAiSegmentsAsync ---

    [Fact]
    public async Task DetectAiSegmentsAsync_EmptySegments_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse(""));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var result = await client.DetectAiSegmentsAsync("sk-test", []);
        Assert.Null(result);
    }

    [Fact]
    public async Task DetectAiSegmentsAsync_ValidResponse_ParsesScores()
    {
        var response = AnthropicJsonResponse("[0] 75\n[1] 40\n[2] 90");
        var handler = new MockHttpMessageHandler(response);
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var result = await client.DetectAiSegmentsAsync("sk-test", ["para1", "para2", "para3"]);

        Assert.NotNull(result);
        Assert.Equal(3, result!.Length);
        Assert.Equal(75, result[0]);
        Assert.Equal(40, result[1]);
        Assert.Equal(90, result[2]);
    }

    [Fact]
    public async Task DetectAiSegmentsAsync_ApiError_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler("{\"error\":\"bad\"}", HttpStatusCode.InternalServerError);
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var result = await client.DetectAiSegmentsAsync("sk-test", ["paragraph"]);
        Assert.Null(result);
    }

    [Fact]
    public async Task DetectAiSegmentsAsync_LongSegments_Truncated()
    {
        var response = AnthropicJsonResponse("[0] 50");
        var handler = new MockHttpMessageHandler(response);
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var longSegment = new string('x', 500);
        await client.DetectAiSegmentsAsync("sk-test", [longSegment]);

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        // Segment truncated to 300 chars
        Assert.DoesNotContain(new string('x', 500), body);
    }

    // --- ExtractTopicAsync ---

    [Fact]
    public async Task ExtractTopicAsync_ValidResponse_ReturnsTopic()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("\"Arctic ice extent record low 2026\""));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var topic = await client.ExtractTopicAsync("sk-test", "Article about Arctic ice melting in 2026");

        Assert.Equal("Arctic ice extent record low 2026", topic);
    }

    [Fact]
    public async Task ExtractTopicAsync_WhitespaceResponse_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("   "));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var topic = await client.ExtractTopicAsync("sk-test", "Some text");
        Assert.Null(topic);
    }

    [Fact]
    public async Task ExtractTopicAsync_ApiError_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler("{\"error\":\"bad\"}", HttpStatusCode.InternalServerError);
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var topic = await client.ExtractTopicAsync("sk-test", "Some text");
        Assert.Null(topic);
    }

    [Fact]
    public async Task ExtractTopicAsync_TrimsQuotes()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("\"some topic\""));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var topic = await client.ExtractTopicAsync("sk-test", "Some article text here for analysis");
        Assert.Equal("some topic", topic);
    }

    // --- VerifyCredibilityAsync ---

    [Fact]
    public async Task VerifyCredibilityAsync_EmptySources_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse(""));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var result = await client.VerifyCredibilityAsync("sk-test", "text", []);
        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyCredibilityAsync_ValidResponse_ParsesResult()
    {
        var credResponse = "SCORE: 80\nVERDICT: Mostly Supported\nCLAIMS:\n- Claim1: Supported - reason";
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse(credResponse));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var sources = new List<SourceSnippet> { new() { Title = "Src1", Snippet = "snippet" } };
        var result = await client.VerifyCredibilityAsync("sk-test", "page text", sources);

        Assert.NotNull(result);
        Assert.Equal(80, result!.Score);
        Assert.Equal("Mostly Supported", result.Verdict);
        Assert.Single(result.Claims);
    }

    [Fact]
    public async Task VerifyCredibilityAsync_ApiError_ReturnsNull()
    {
        var handler = new MockHttpMessageHandler("{\"error\":\"bad\"}", HttpStatusCode.InternalServerError);
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var sources = new List<SourceSnippet> { new() { Title = "Src1", Snippet = "snippet" } };
        var result = await client.VerifyCredibilityAsync("sk-test", "text", sources);
        Assert.Null(result);
    }

    [Fact]
    public async Task VerifyCredibilityAsync_LongText_Truncated()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("SCORE: 50\nVERDICT: Mixed"));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var longText = new string('a', 5000);
        var sources = new List<SourceSnippet> { new() { Title = "S", Snippet = "s" } };
        await client.VerifyCredibilityAsync("sk-test", longText, sources);

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.DoesNotContain(new string('a', 5000), body);
    }

    [Fact]
    public async Task VerifyCredibilityAsync_LongSnippet_Truncated()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("SCORE: 50\nVERDICT: Mixed"));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var longSnippet = new string('b', 500);
        var sources = new List<SourceSnippet> { new() { Title = "S", Snippet = longSnippet } };
        await client.VerifyCredibilityAsync("sk-test", "text", sources);

        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.DoesNotContain(new string('b', 500), body);
    }

    // --- EvaluateSourceReliabilityAsync ---

    [Fact]
    public async Task EvaluateSourceReliabilityAsync_EmptySources_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse(""));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var result = await client.EvaluateSourceReliabilityAsync("sk-test", "text", []);
        Assert.Empty(result);
    }

    [Fact]
    public async Task EvaluateSourceReliabilityAsync_ValidResponse_ReturnsReliability()
    {
        var response = AnthropicJsonResponse("[0] 85\n[1] 40");
        var handler = new MockHttpMessageHandler(response);
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var sources = new List<SourceSnippet>
        {
            new() { Title = "CNN Article", Snippet = "snippet1" },
            new() { Title = "Blog Post", Snippet = "snippet2" }
        };
        var result = await client.EvaluateSourceReliabilityAsync("sk-test", "article text", sources);

        Assert.Equal(2, result.Count);
        Assert.Equal("CNN Article", result[0].Title);
        Assert.Equal(85, result[0].Score);
        Assert.Equal("Blog Post", result[1].Title);
        Assert.Equal(40, result[1].Score);
    }

    [Fact]
    public async Task EvaluateSourceReliabilityAsync_ApiError_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler("{\"error\":\"bad\"}", HttpStatusCode.InternalServerError);
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var sources = new List<SourceSnippet> { new() { Title = "S", Snippet = "s" } };
        var result = await client.EvaluateSourceReliabilityAsync("sk-test", "text", sources);
        Assert.Empty(result);
    }

    [Fact]
    public async Task EvaluateSourceReliabilityAsync_MoreThan8Sources_LimitsTo8()
    {
        var scores = string.Join("\n", Enumerable.Range(0, 8).Select(i => $"[{i}] 70"));
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse(scores));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var sources = Enumerable.Range(0, 10)
            .Select(i => new SourceSnippet { Title = $"Source{i}", Snippet = "snippet" })
            .ToList();
        var result = await client.EvaluateSourceReliabilityAsync("sk-test", "text", sources);

        Assert.True(result.Count <= 8);
    }

    [Fact]
    public async Task EvaluateSourceReliabilityAsync_NullScores_ReturnsEmpty()
    {
        // Response that can't be parsed as segment scores
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("no valid scores here"));
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);

        var sources = new List<SourceSnippet> { new() { Title = "S", Snippet = "s" } };
        var result = await client.EvaluateSourceReliabilityAsync("sk-test", "text", sources);

        // ParseSegmentScores returns array of zeros, not null, so this should still work
        Assert.Single(result);
        Assert.Equal(0, result[0].Score);
    }
}
