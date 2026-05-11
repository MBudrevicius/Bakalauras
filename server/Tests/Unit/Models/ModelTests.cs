using server.Models;

namespace server.Tests.Unit.Models;

public class AiCheckResultTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var result = new AiCheckResult();

        Assert.Equal(string.Empty, result.Title);
        Assert.Equal(string.Empty, result.Description);
        Assert.Equal(0, result.AiScore);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var result = new AiCheckResult
        {
            Type = AiCheckType.VocabularyRichness,
            Title = "Test",
            Description = "Description",
            AiScore = 75
        };

        Assert.Equal(AiCheckType.VocabularyRichness, result.Type);
        Assert.Equal("Test", result.Title);
        Assert.Equal("Description", result.Description);
        Assert.Equal(75, result.AiScore);
    }
}

public class SecurityCheckResultTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var result = new SecurityCheckResult();

        Assert.Equal(string.Empty, result.Title);
        Assert.Equal(string.Empty, result.Description);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var result = new SecurityCheckResult
        {
            Type = SecurityCheckType.Https,
            Severity = SecurityCheckSeverity.Warning,
            Title = "HTTPS",
            Description = "Not secure"
        };

        Assert.Equal(SecurityCheckType.Https, result.Type);
        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
    }
}

public class SecurityCheckResponseTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var response = new SecurityCheckResponse();

        Assert.Equal(string.Empty, response.Url);
        Assert.Empty(response.Results);
        Assert.Equal(0, response.OverallScore);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var response = new SecurityCheckResponse
        {
            Url = "https://example.com",
            OverallScore = 85,
            Results = [new SecurityCheckResult { Title = "Test" }]
        };

        Assert.Equal("https://example.com", response.Url);
        Assert.Equal(85, response.OverallScore);
        Assert.Single(response.Results);
    }
}

public class AiCheckRequestTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var request = new AiCheckRequest();

        Assert.Equal(string.Empty, request.Text);
        Assert.Null(request.Url);
    }

    [Fact]
    public void Properties_CanBeSet()
    {
        var request = new AiCheckRequest
        {
            Text = "Hello world",
            Url = "https://example.com"
        };

        Assert.Equal("Hello world", request.Text);
        Assert.Equal("https://example.com", request.Url);
    }
}

public class EnumTests
{
    [Theory]
    [InlineData(AiCheckType.VocabularyRichness)]
    [InlineData(AiCheckType.SentenceUniformity)]
    [InlineData(AiCheckType.PerplexityEstimation)]
    [InlineData(AiCheckType.PunctuationPatterns)]
    [InlineData(AiCheckType.RepetitivePhrasing)]
    [InlineData(AiCheckType.ParagraphStructure)]
    [InlineData(AiCheckType.TransitionalPhrases)]
    [InlineData(AiCheckType.HedgingLanguage)]
    [InlineData(AiCheckType.ClaudeAiModel)]
    public void AiCheckType_AllValuesAreDefined(AiCheckType type)
    {
        Assert.True(Enum.IsDefined(type));
    }

    [Theory]
    [InlineData(SecurityCheckSeverity.Pass)]
    [InlineData(SecurityCheckSeverity.Info)]
    [InlineData(SecurityCheckSeverity.Warning)]
    public void SecurityCheckSeverity_AllValuesAreDefined(SecurityCheckSeverity severity)
    {
        Assert.True(Enum.IsDefined(severity));
    }

    [Fact]
    public void AiCheckType_HasCorrectCount()
    {
        Assert.Equal(9, Enum.GetValues<AiCheckType>().Length);
    }

    [Fact]
    public void SecurityCheckType_HasCorrectCount()
    {
        Assert.Equal(9, Enum.GetValues<SecurityCheckType>().Length);
    }
}
