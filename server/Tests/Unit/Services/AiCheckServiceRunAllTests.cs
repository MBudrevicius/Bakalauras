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

public class AiCheckServiceRunAllTests
{
    private static (AiCheckService service, AppDbContext db) CreateService(params IAiCheck[] checks)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var httpFactory = Mock.Of<IHttpClientFactory>();
        var htmlExtractor = new HtmlTextExtractor(httpFactory);
        var scoreStore = new PageScoreStore(db, htmlExtractor);
        var anthropic = new AnthropicClient(httpFactory);
        var service = new AiCheckService(checks, scoreStore, anthropic, htmlExtractor);
        return (service, db);
    }

    [Fact]
    public async Task RunAllAsync_EmptyText_ReturnsZeroScore()
    {
        var (service, db) = CreateService();
        var request = new AiCheckRequest { Text = "" };
        var response = await service.RunAllAsync(request);

        Assert.Equal(0, response.OverallAiScore);
        Assert.Equal(0, response.TextLength);
        Assert.Empty(response.Results);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllAsync_WhitespaceText_ReturnsZeroScore()
    {
        var (service, db) = CreateService();
        var request = new AiCheckRequest { Text = "   \n\t  " };
        var response = await service.RunAllAsync(request);

        Assert.Equal(0, response.OverallAiScore);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllAsync_WithText_RunsAllChecks()
    {
        var check1 = new Mock<IAiCheck>();
        check1.Setup(c => c.Type).Returns(AiCheckType.SentenceUniformity);
        check1.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 60, Title = "Check1" });

        var check2 = new Mock<IAiCheck>();
        check2.Setup(c => c.Type).Returns(AiCheckType.VocabularyRichness);
        check2.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.VocabularyRichness, AiScore = 40, Title = "Check2" });

        var (service, db) = CreateService(check1.Object, check2.Object);
        var request = new AiCheckRequest { Text = "Some test text for analysis" };
        var response = await service.RunAllAsync(request);

        Assert.Equal(2, response.Results.Count);
        Assert.True(response.OverallAiScore > 0);
        Assert.Equal(27, response.TextLength);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllAsync_LongText_TruncatesAnalyzedText()
    {
        var (service, db) = CreateService();
        var longText = new string('a', 600);
        var request = new AiCheckRequest { Text = longText };
        var response = await service.RunAllAsync(request);

        Assert.True(response.AnalyzedText.Length <= 501); // 500 + ellipsis
        db.Dispose();
    }

    [Fact]
    public async Task RunAllAsync_WithUrl_SavesScore()
    {
        var check = new Mock<IAiCheck>();
        check.Setup(c => c.Type).Returns(AiCheckType.SentenceUniformity);
        check.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 70, Title = "Check" });

        var (service, db) = CreateService(check.Object);
        var request = new AiCheckRequest { Text = "Some text", Url = "https://test.com" };
        await service.RunAllAsync(request);

        var pageScore = await db.PageScores.FirstOrDefaultAsync(p => p.Domain == "test.com");
        Assert.NotNull(pageScore);
        Assert.Equal(1, pageScore.AiCheckCount);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllAsync_NoUrl_DoesNotSaveScore()
    {
        var check = new Mock<IAiCheck>();
        check.Setup(c => c.Type).Returns(AiCheckType.SentenceUniformity);
        check.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 70, Title = "Check" });

        var (service, db) = CreateService(check.Object);
        var request = new AiCheckRequest { Text = "Some text" };
        await service.RunAllAsync(request);

        Assert.Empty(db.PageScores);
        db.Dispose();
    }

    [Fact]
    public async Task AnalyzeSegmentsAsync_EmptySegments_ReturnsNoApiKey()
    {
        var (service, db) = CreateService();
        var scores = await service.AnalyzeSegmentsAsync([], null);
        Assert.Empty(scores);
        db.Dispose();
    }

    [Fact]
    public async Task AnalyzeSegmentsAsync_ShortSegment_ReturnsZero()
    {
        var check = new Mock<IAiCheck>();
        check.Setup(c => c.Type).Returns(AiCheckType.SentenceUniformity);
        check.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 50, Title = "Check" });

        var (service, db) = CreateService(check.Object);
        var scores = await service.AnalyzeSegmentsAsync(["hi", "too short"], null);

        Assert.Equal(2, scores.Length);
        Assert.Equal(0, scores[0]); // less than 5 words
        Assert.Equal(0, scores[1]);
        db.Dispose();
    }

    [Fact]
    public async Task AnalyzeSegmentsAsync_ValidSegment_ReturnsHeuristicScore()
    {
        var check = new Mock<IAiCheck>();
        check.Setup(c => c.Type).Returns(AiCheckType.SentenceUniformity);
        check.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 60, Title = "Check" });

        var (service, db) = CreateService(check.Object);
        var scores = await service.AnalyzeSegmentsAsync(
            ["This is a long enough paragraph for analysis"], null);

        Assert.Single(scores);
        Assert.Equal(60, scores[0]);
        db.Dispose();
    }

    [Fact]
    public async Task AnalyzeSegmentsAsync_WhitespaceSegment_ReturnsZero()
    {
        var check = new Mock<IAiCheck>();
        check.Setup(c => c.Type).Returns(AiCheckType.SentenceUniformity);
        check.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 50, Title = "Check" });

        var (service, db) = CreateService(check.Object);
        var scores = await service.AnalyzeSegmentsAsync(["   \t\n  "], null);

        Assert.Single(scores);
        Assert.Equal(0, scores[0]);
        db.Dispose();
    }

    [Fact]
    public async Task AnalyzeSegmentsAsync_MixedSegments_CorrectScores()
    {
        var check = new Mock<IAiCheck>();
        check.Setup(c => c.Type).Returns(AiCheckType.SentenceUniformity);
        check.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 70, Title = "Check" });

        var (service, db) = CreateService(check.Object);
        var scores = await service.AnalyzeSegmentsAsync(
            ["short", "This is a paragraph with enough words for analysis checking"], null);

        Assert.Equal(2, scores.Length);
        Assert.Equal(0, scores[0]);   // too short
        Assert.Equal(70, scores[1]);  // valid
        db.Dispose();
    }

    [Fact]
    public async Task AnalyzeSegmentsAsync_MultipleChecks_AveragesScores()
    {
        var check1 = new Mock<IAiCheck>();
        check1.Setup(c => c.Type).Returns(AiCheckType.SentenceUniformity);
        check1.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 80, Title = "C1" });

        var check2 = new Mock<IAiCheck>();
        check2.Setup(c => c.Type).Returns(AiCheckType.VocabularyRichness);
        check2.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.VocabularyRichness, AiScore = 40, Title = "C2" });

        var (service, db) = CreateService(check1.Object, check2.Object);
        var scores = await service.AnalyzeSegmentsAsync(
            ["This is a paragraph with more than five words for analysis"], null);

        Assert.Single(scores);
        Assert.Equal(60, scores[0]); // average of 80 and 40
        db.Dispose();
    }

    [Fact]
    public async Task RunAllAsync_ClaudeCheckExcludedFromHeuristics()
    {
        var claudeCheck = new Mock<IAiCheck>();
        claudeCheck.Setup(c => c.Type).Returns(AiCheckType.ClaudeAiModel);
        claudeCheck.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.ClaudeAiModel, AiScore = 0, Title = "Claude" });

        var heuristic = new Mock<IAiCheck>();
        heuristic.Setup(c => c.Type).Returns(AiCheckType.SentenceUniformity);
        heuristic.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 50, Title = "Heuristic" });

        var (service, db) = CreateService(claudeCheck.Object, heuristic.Object);
        var request = new AiCheckRequest { Text = "Test text for analysis" };
        var response = await service.RunAllAsync(request);

        Assert.Equal(2, response.Results.Count);
        // Claude has score 0, so only heuristic contributes
        Assert.Equal(50, response.OverallAiScore);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllAsync_ClaudeAndHeuristic_WeightedAverage()
    {
        var claudeCheck = new Mock<IAiCheck>();
        claudeCheck.Setup(c => c.Type).Returns(AiCheckType.ClaudeAiModel);
        claudeCheck.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.ClaudeAiModel, AiScore = 80, Title = "Claude" });

        var heuristic = new Mock<IAiCheck>();
        heuristic.Setup(c => c.Type).Returns(AiCheckType.SentenceUniformity);
        heuristic.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 60, Title = "Heuristic" });

        var (service, db) = CreateService(claudeCheck.Object, heuristic.Object);
        var request = new AiCheckRequest { Text = "Test text for analysis" };
        var response = await service.RunAllAsync(request);

        // Claude 80 * 0.6 + Heuristic 60 * 0.4 = 48 + 24 = 72
        Assert.Equal(72, response.OverallAiScore);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllAsync_ResultsOrderedClaudeFirst()
    {
        var claudeCheck = new Mock<IAiCheck>();
        claudeCheck.Setup(c => c.Type).Returns(AiCheckType.ClaudeAiModel);
        claudeCheck.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.ClaudeAiModel, AiScore = 50, Title = "Claude" });

        var heuristic = new Mock<IAiCheck>();
        heuristic.Setup(c => c.Type).Returns(AiCheckType.SentenceUniformity);
        heuristic.Setup(c => c.RunAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 60, Title = "Heuristic" });

        var (service, db) = CreateService(heuristic.Object, claudeCheck.Object);
        var request = new AiCheckRequest { Text = "Test text for analysis" };
        var response = await service.RunAllAsync(request);

        // Claude should be first in results
        Assert.Equal(AiCheckType.ClaudeAiModel, response.Results[0].Type);
        db.Dispose();
    }

    [Fact]
    public async Task RunAllAsync_EmptyTextWithUrl_ExtractsFromUrl()
    {
        var html = "<html><body><p>Extracted text content from the web page</p></body></html>";
        var handler = new MockHttpMessageHandler(html);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);
        var factory = new MockHttpClientFactory(handler);
        var htmlExtractor = new HtmlTextExtractor(factory);
        var scoreStore = new PageScoreStore(db, htmlExtractor);
        var anthropic = new AnthropicClient(factory);
        var service = new AiCheckService([], scoreStore, anthropic, htmlExtractor);

        var request = new AiCheckRequest { Text = "", Url = "https://example.com/page" };
        var response = await service.RunAllAsync(request);

        Assert.True(response.TextLength > 0);
        Assert.NotEmpty(response.AnalyzedText);
        db.Dispose();
    }
}
