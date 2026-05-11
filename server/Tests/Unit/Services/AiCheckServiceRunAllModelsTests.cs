using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Moq;
using server.Checks.Ai;
using server.Clients;
using server.Data;
using server.Helpers;
using server.Models;
using server.Services;
using server.Tests.Unit.Helpers;

namespace server.Tests.Unit.Services;

public class AiCheckServiceRunAllModelsTests
{
    private static string AnthropicJsonResponse(string text) =>
        JsonSerializer.Serialize(new { content = new[] { new { text } } });

    private static (AiCheckService service, AppDbContext db) CreateService(MockHttpMessageHandler handler, params IAiCheck[] checks)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var factory = new MockHttpClientFactory(handler);
        var htmlExtractor = new HtmlTextExtractor(factory);
        var scoreStore = new PageScoreStore(db, htmlExtractor);
        var anthropic = new AnthropicClient(factory);
        var service = new AiCheckService(checks, scoreStore, anthropic, htmlExtractor);
        return (service, db);
    }

    [Fact]
    public async Task RunAllModelsAsync_EmptyText_ReturnsDefault()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("50"));
        var (service, db) = CreateService(handler);

        var request = new AiCheckRequest { Text = "" };
        var response = await service.RunAllModelsAsync(request, "sk-test");

        Assert.Equal(0, response.TextLength);
        Assert.Empty(response.ModelResults);
        Assert.Empty(response.HeuristicResults);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllModelsAsync_WhitespaceText_ReturnsDefault()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("50"));
        var (service, db) = CreateService(handler);

        var request = new AiCheckRequest { Text = "   \t\n  " };
        var response = await service.RunAllModelsAsync(request, "sk-test");

        Assert.Equal(0, response.TextLength);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllModelsAsync_WithText_RunsAllModels()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("70"));

        var heuristic = new Mock<IAiCheck>();
        heuristic.Setup(c => c.Type).Returns(AiCheckType.SentenceUniformity);
        heuristic.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 60, Title = "Heuristic" });

        var (service, db) = CreateService(handler, heuristic.Object);

        var request = new AiCheckRequest { Text = "Some text for analysis" };
        var response = await service.RunAllModelsAsync(request, "sk-test");

        Assert.Equal(3, response.ModelResults.Count); // 3 Claude models
        Assert.Single(response.HeuristicResults); // 1 heuristic check
        Assert.True(response.AverageAiScore > 0);
        Assert.Equal(22, response.TextLength);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllModelsAsync_ExcludesClaudeFromHeuristics()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("50"));

        var claude = new Mock<IAiCheck>();
        claude.Setup(c => c.Type).Returns(AiCheckType.ClaudeAiModel);
        claude.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.ClaudeAiModel, AiScore = 80, Title = "Claude" });

        var heuristic = new Mock<IAiCheck>();
        heuristic.Setup(c => c.Type).Returns(AiCheckType.VocabularyRichness);
        heuristic.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.VocabularyRichness, AiScore = 40, Title = "Vocab" });

        var (service, db) = CreateService(handler, claude.Object, heuristic.Object);

        var request = new AiCheckRequest { Text = "Text for testing" };
        var response = await service.RunAllModelsAsync(request, "sk-test");

        // Heuristic results should only include non-Claude checks
        Assert.Single(response.HeuristicResults);
        Assert.Equal(AiCheckType.VocabularyRichness, response.HeuristicResults[0].Type);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllModelsAsync_WithUrl_SavesScore()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("60"));

        var heuristic = new Mock<IAiCheck>();
        heuristic.Setup(c => c.Type).Returns(AiCheckType.SentenceUniformity);
        heuristic.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 50, Title = "Check" });

        var (service, db) = CreateService(handler, heuristic.Object);

        var request = new AiCheckRequest { Text = "Some text content", Url = "https://testsite.com" };
        await service.RunAllModelsAsync(request, "sk-test");

        var pageScore = await db.PageScores.FirstOrDefaultAsync(p => p.Domain == "testsite.com");
        Assert.NotNull(pageScore);
        Assert.Equal(1, pageScore!.AiCheckCount);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllModelsAsync_ModelLabels_Correct()
    {
        var handler = new MockHttpMessageHandler(AnthropicJsonResponse("55"));
        var (service, db) = CreateService(handler);

        var request = new AiCheckRequest { Text = "Analysis text" };
        var response = await service.RunAllModelsAsync(request, "sk-test");

        Assert.Equal(3, response.ModelResults.Count);
        Assert.Contains(response.ModelResults, m => m.Label == "Haiku 4.5");
        Assert.Contains(response.ModelResults, m => m.Label == "Sonnet 4.6");
        Assert.Contains(response.ModelResults, m => m.Label == "Opus 4.7");
        db.Dispose();
    }

    // AnalyzeSegmentsAsync with Claude blending
    [Fact]
    public async Task AnalyzeSegmentsAsync_WithClaudeApiKey_BlendsScores()
    {
        // Claude returns segment scores via DetectAiSegmentsAsync
        var response = AnthropicJsonResponse("[0] 80");
        var handler = new MockHttpMessageHandler(response);

        var heuristic = new Mock<IAiCheck>();
        heuristic.Setup(c => c.Type).Returns(AiCheckType.SentenceUniformity);
        heuristic.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 60, Title = "Check" });

        var (service, db) = CreateService(handler, heuristic.Object);

        var scores = await service.AnalyzeSegmentsAsync(
            ["This is a paragraph with more than five words for testing"], "sk-test");

        Assert.Single(scores);
        // Blended: claude * 0.6 + heuristic * 0.4 = 80 * 0.6 + 60 * 0.4 = 72
        Assert.Equal(72, scores[0]);
        db.Dispose();
    }

    [Fact]
    public async Task AnalyzeSegmentsAsync_ClaudeReturnsZero_UsesHeuristicOnly()
    {
        var response = AnthropicJsonResponse("[0] 0");
        var handler = new MockHttpMessageHandler(response);

        var heuristic = new Mock<IAiCheck>();
        heuristic.Setup(c => c.Type).Returns(AiCheckType.SentenceUniformity);
        heuristic.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 50, Title = "Check" });

        var (service, db) = CreateService(handler, heuristic.Object);

        var scores = await service.AnalyzeSegmentsAsync(
            ["This is a paragraph with more than five words for testing"], "sk-test");

        Assert.Single(scores);
        // Claude score is 0, so uses heuristic only
        Assert.Equal(50, scores[0]);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllModelsAsync_EmptyTextWithUrl_ExtractsFromUrl()
    {
        // The handler returns HTML for the text extraction, and then Anthropic JSON for AI detection
        var html = "<html><body><p>Extracted text from web page for analysis</p></body></html>";
        var multiHandler = new DelegatingMockHandler(request =>
        {
            if (request.RequestUri!.Host == "api.anthropic.com")
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                { Content = new StringContent(AnthropicJsonResponse("50")) };
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            { Content = new StringContent(html) };
        });

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var factory = new MockHttpClientFactory(multiHandler);
        var htmlExtractor = new HtmlTextExtractor(factory);
        var scoreStore = new PageScoreStore(db, htmlExtractor);
        var anthropic = new AnthropicClient(factory);
        var service = new AiCheckService([], scoreStore, anthropic, htmlExtractor);

        var request = new AiCheckRequest { Text = "", Url = "https://example.com/page" };
        var response = await service.RunAllModelsAsync(request, "sk-test");

        Assert.True(response.TextLength > 0);
        db.Dispose();
    }
}
