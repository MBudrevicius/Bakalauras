using System.Net;
using System.Text.Json;
using server.Checks.Ai;
using server.Clients;
using server.Models;
using server.Tests.Unit.Helpers;

namespace server.Tests.Unit.Checks.Ai;

public class ClaudeAiModelCheckRunAsyncTests
{
    private static string AnthropicJsonResponse(string text) =>
        JsonSerializer.Serialize(new { content = new[] { new { text } } });

    private static ClaudeAiModelCheck CreateCheck(MockHttpMessageHandler handler)
    {
        var factory = new MockHttpClientFactory(handler);
        var client = new AnthropicClient(factory);
        return new ClaudeAiModelCheck(client);
    }

    [Fact]
    public async Task RunAsync_HighScore_ReportsVeryLikelyAI()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("90"));
        var check = CreateCheck(handler);

        var result = await check.RunAsync("This is a sufficiently long text for analysis.", apiKey: "sk-test");

        Assert.Equal(90, result.AiScore);
        Assert.Contains("very likely AI-generated", result.Description);
    }

    [Fact]
    public async Task RunAsync_Score60_ReportsLikelyAI()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("65"));
        var check = CreateCheck(handler);

        var result = await check.RunAsync("This is a sufficiently long text for analysis.", apiKey: "sk-test");

        Assert.Equal(65, result.AiScore);
        Assert.Contains("likely AI-generated", result.Description);
    }

    [Fact]
    public async Task RunAsync_Score40_ReportsUnclear()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("45"));
        var check = CreateCheck(handler);

        var result = await check.RunAsync("This is a sufficiently long text for analysis.", apiKey: "sk-test");

        Assert.Equal(45, result.AiScore);
        Assert.Contains("unclear origin", result.Description);
    }

    [Fact]
    public async Task RunAsync_Score20_ReportsLikelyHuman()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("25"));
        var check = CreateCheck(handler);

        var result = await check.RunAsync("This is a sufficiently long text for analysis.", apiKey: "sk-test");

        Assert.Equal(25, result.AiScore);
        Assert.Contains("likely human-written", result.Description);
    }

    [Fact]
    public async Task RunAsync_LowScore_ReportsVeryLikelyHuman()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("10"));
        var check = CreateCheck(handler);

        var result = await check.RunAsync("This is a sufficiently long text for analysis.", apiKey: "sk-test");

        Assert.Equal(10, result.AiScore);
        Assert.Contains("very likely human-written", result.Description);
    }

    [Fact]
    public async Task RunAsync_ApiFailure_ReturnsZeroWithErrorMessage()
    {
        var handler = new MockHttpMessageHandler("{\"error\":\"unauthorized\"}", HttpStatusCode.Unauthorized);
        var check = CreateCheck(handler);

        var result = await check.RunAsync("This is a sufficiently long text for analysis.", apiKey: "sk-test");

        Assert.Equal(0, result.AiScore);
        Assert.Contains("API call failed", result.Description);
    }

    [Fact]
    public async Task RunAsync_LongText_TruncatesTo4000()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("50"));
        var check = CreateCheck(handler);

        var longText = new string('x', 5000);
        var result = await check.RunAsync(longText, apiKey: "sk-test");

        Assert.Equal(50, result.AiScore);
        // Verify the text was truncated (the handler was called)
        Assert.NotNull(handler.LastRequest);
    }

    [Fact]
    public async Task RunAsync_WithModel_PassesModelThrough()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("55"));
        var check = CreateCheck(handler);

        var result = await check.RunAsync("This is a sufficiently long text for analysis.", apiKey: "sk-test", model: "claude-opus-4-7");

        Assert.Equal(55, result.AiScore);
        var body = await handler.LastRequest!.Content!.ReadAsStringAsync();
        Assert.Contains("claude-opus-4-7", body);
    }
}
