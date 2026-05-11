using Moq;
using server.Checks.Ai;
using server.Clients;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

public class ClaudeAiModelCheckTests
{
    private static ClaudeAiModelCheck CreateCheck()
    {
        var httpFactory = Mock.Of<IHttpClientFactory>();
        var client = new AnthropicClient(httpFactory);
        return new ClaudeAiModelCheck(client);
    }

    [Fact]
    public async Task RunAsync_NoApiKey_ReturnsZeroScore()
    {
        var check = CreateCheck();
        var result = await check.RunAsync("Some text to analyze", apiKey: null);

        Assert.Equal(AiCheckType.ClaudeAiModel, result.Type);
        Assert.Equal(0, result.AiScore);
        Assert.Contains("No API key", result.Description);
    }

    [Fact]
    public async Task RunAsync_EmptyApiKey_ReturnsZeroScore()
    {
        var check = CreateCheck();
        var result = await check.RunAsync("Some text", apiKey: "   ");

        Assert.Equal(0, result.AiScore);
        Assert.Contains("No API key", result.Description);
    }

    [Fact]
    public async Task RunAsync_WhitespaceApiKey_ReturnsZeroScore()
    {
        var check = CreateCheck();
        var result = await check.RunAsync("Some text", apiKey: "\t\n");

        Assert.Equal(0, result.AiScore);
    }

    [Fact]
    public async Task RunAsync_TextTooShort_ReturnsZeroScore()
    {
        var check = CreateCheck();
        var result = await check.RunAsync("Hi", apiKey: "sk-test");

        Assert.Equal(0, result.AiScore);
        Assert.Contains("too short", result.Description);
    }

    [Fact]
    public async Task RunAsync_EmptyText_ReturnsZeroScore()
    {
        var check = CreateCheck();
        var result = await check.RunAsync("", apiKey: "sk-test");

        Assert.Equal(0, result.AiScore);
        Assert.Contains("too short", result.Description);
    }

    [Fact]
    public void RunAsync_Type_IsClaudeAiModel()
    {
        var check = CreateCheck();
        Assert.Equal(AiCheckType.ClaudeAiModel, check.Type);
    }

    [Fact]
    public async Task RunAsync_Title_IsClaude()
    {
        var check = CreateCheck();
        var result = await check.RunAsync("text", apiKey: null);
        Assert.Equal("Claude AI Analysis", result.Title);
    }
}
