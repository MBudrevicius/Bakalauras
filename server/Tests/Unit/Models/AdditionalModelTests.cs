using server.Models;

namespace server.Tests.Unit.Models;

public class AiCheckResponseTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var response = new AiCheckResponse
        {
            AnalyzedText = "Sample text",
            TextLength = 100,
            OverallAiScore = 75,
            Results = [new AiCheckResult { Title = "Test", AiScore = 80 }]
        };

        Assert.Equal("Sample text", response.AnalyzedText);
        Assert.Equal(100, response.TextLength);
        Assert.Equal(75, response.OverallAiScore);
        Assert.Single(response.Results);
    }
}

public class AllModelsAiCheckResponseTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var response = new AllModelsAiCheckResponse
        {
            AverageAiScore = 65,
            TextLength = 500,
            ModelResults = [new ModelResult { Model = "haiku", Label = "Haiku", AiScore = 60, OverallAiScore = 55 }],
            HeuristicResults = [new AiCheckResult { Title = "Vocab", AiScore = 70 }]
        };

        Assert.Equal(65, response.AverageAiScore);
        Assert.Equal(500, response.TextLength);
        Assert.Single(response.ModelResults);
        Assert.Single(response.HeuristicResults);
    }
}

public class ModelResultTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var result = new ModelResult
        {
            Model = "claude-sonnet-4-6",
            Label = "Sonnet 4.6",
            AiScore = 80,
            OverallAiScore = 72
        };

        Assert.Equal("claude-sonnet-4-6", result.Model);
        Assert.Equal("Sonnet 4.6", result.Label);
        Assert.Equal(80, result.AiScore);
        Assert.Equal(72, result.OverallAiScore);
    }
}

public class HighlightRequestTests
{
    [Fact]
    public void Segments_CanBeSetAndGet()
    {
        var request = new HighlightRequest
        {
            Segments = ["paragraph 1", "paragraph 2"]
        };

        Assert.Equal(2, request.Segments.Length);
        Assert.Equal("paragraph 1", request.Segments[0]);
    }
}

public class BraveSearchResultTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var result = new BraveSearchResult
        {
            Url = "https://example.com/article",
            Title = "Article Title",
            Snippet = "A short description"
        };

        Assert.Equal("https://example.com/article", result.Url);
        Assert.Equal("Article Title", result.Title);
        Assert.Equal("A short description", result.Snippet);
    }
}

public class CrossCheckRequestTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var request = new CrossCheckRequest
        {
            Url = "https://example.com",
            Title = "Page Title",
            Text = "Page content",
            PageLinks = ["https://link1.com", "https://link2.com"]
        };

        Assert.Equal("https://example.com", request.Url);
        Assert.Equal("Page Title", request.Title);
        Assert.Equal("Page content", request.Text);
        Assert.Equal(2, request.PageLinks.Count);
    }
}

public class CrossCheckResponseTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var response = new CrossCheckResponse
        {
            Url = "https://example.com",
            Topic = "Climate Change",
            RelatedPages = [new RelatedPage { Url = "https://related.com", Title = "Related", Snippet = "Content" }],
            Credibility = new CredibilityResult { Score = 80, Verdict = "Supported" },
            SourceReliability = [new SourceReliability { Title = "Source", Score = 90 }],
            PageLinkDomains = 5,
            PageLinkSamples = ["https://sample.com"],
            ModelResults = [new ModelCredibilityResult { Model = "haiku", Label = "Haiku" }]
        };

        Assert.Equal("https://example.com", response.Url);
        Assert.Equal("Climate Change", response.Topic);
        Assert.Single(response.RelatedPages);
        Assert.NotNull(response.Credibility);
        Assert.Equal(80, response.Credibility!.Score);
        Assert.Single(response.SourceReliability);
        Assert.Equal(5, response.PageLinkDomains);
        Assert.Single(response.PageLinkSamples);
        Assert.Single(response.ModelResults);
    }
}

public class ModelCredibilityResultTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var result = new ModelCredibilityResult
        {
            Model = "claude-opus-4-7",
            Label = "Opus 4.7",
            Credibility = new CredibilityResult { Score = 90, Verdict = "Supported" }
        };

        Assert.Equal("claude-opus-4-7", result.Model);
        Assert.Equal("Opus 4.7", result.Label);
        Assert.NotNull(result.Credibility);
    }
}

public class RelatedPageTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var page = new RelatedPage
        {
            Url = "https://related.com",
            Title = "Related Article",
            Snippet = "Related content snippet"
        };

        Assert.Equal("https://related.com", page.Url);
        Assert.Equal("Related Article", page.Title);
        Assert.Equal("Related content snippet", page.Snippet);
    }
}

public class SourceReliabilityTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var sr = new SourceReliability { Title = "Reuters", Score = 95 };

        Assert.Equal("Reuters", sr.Title);
        Assert.Equal(95, sr.Score);
    }
}

public class SourceSnippetTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var ss = new SourceSnippet { Title = "BBC", Snippet = "News content" };

        Assert.Equal("BBC", ss.Title);
        Assert.Equal("News content", ss.Snippet);
    }
}

public class PageScoreTests
{
    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var score = new PageScore
        {
            Id = 1,
            Url = "example.com",
            Domain = "example.com",
            SecurityScore = 85,
            CredibilityScore = 70,
            AiScore = 45,
            SecurityCheckCount = 2,
            CredibilityCheckCount = 1,
            AiCheckCount = 3,
            LastChecked = new DateTime(2025, 1, 1),
            CheckCount = 6
        };

        Assert.Equal(1, score.Id);
        Assert.Equal("example.com", score.Url);
        Assert.Equal("example.com", score.Domain);
        Assert.Equal(85, score.SecurityScore);
        Assert.Equal(70, score.CredibilityScore);
        Assert.Equal(45, score.AiScore);
        Assert.Equal(2, score.SecurityCheckCount);
        Assert.Equal(1, score.CredibilityCheckCount);
        Assert.Equal(3, score.AiCheckCount);
        Assert.Equal(new DateTime(2025, 1, 1), score.LastChecked);
        Assert.Equal(6, score.CheckCount);
    }
}

public class SecurityCheckRequestTests
{
    [Fact]
    public void Url_CanBeSetAndGet()
    {
        var request = new SecurityCheckRequest { Url = "https://test.com" };
        Assert.Equal("https://test.com", request.Url);
    }
}

public class CredibilityResultTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var result = new CredibilityResult();

        Assert.Equal(0, result.Score);
        Assert.Empty(result.Claims);
    }

    [Fact]
    public void Properties_CanBeSetAndGet()
    {
        var result = new CredibilityResult
        {
            Score = 80,
            Verdict = "Mostly Supported",
            Claims = ["Claim 1: Supported", "Claim 2: Contradicted"]
        };

        Assert.Equal(80, result.Score);
        Assert.Equal("Mostly Supported", result.Verdict);
        Assert.Equal(2, result.Claims.Count);
    }
}
