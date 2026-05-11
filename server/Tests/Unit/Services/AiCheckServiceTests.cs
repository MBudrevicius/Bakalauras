using server.Models;
using server.Services;

namespace server.Tests.Unit.Services;

public class AiCheckServiceTests
{
    [Fact]
    public void CalculateOverallScore_EmptyResults_Returns0()
    {
        var score = InvokeCalculateOverallScore([]);
        Assert.Equal(0, score);
    }

    [Fact]
    public void CalculateOverallScore_OnlyHeuristics_ReturnsWeightedAverage()
    {
        var results = new[]
        {
            new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 60 },
            new AiCheckResult { Type = AiCheckType.VocabularyRichness, AiScore = 40 },
            new AiCheckResult { Type = AiCheckType.RepetitivePhrasing, AiScore = 50 },
        };

        var score = InvokeCalculateOverallScore(results);

        Assert.InRange(score, 1, 100);
    }

    [Fact]
    public void CalculateOverallScore_WithClaude_Weights60_40()
    {
        var results = new[]
        {
            new AiCheckResult { Type = AiCheckType.ClaudeAiModel, AiScore = 80 },
            new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 40 },
            new AiCheckResult { Type = AiCheckType.VocabularyRichness, AiScore = 40 },
        };

        var score = InvokeCalculateOverallScore(results);

        // Claude 80 * 0.6 = 48, heuristics ~40 * 0.4 = 16 => ~64
        Assert.True(score >= 50, $"With Claude at 80, expected around 60+, got {score}");
    }

    [Fact]
    public void CalculateOverallScore_ClaudeZeroScore_IgnoresClaudeWeight()
    {
        var results = new[]
        {
            new AiCheckResult { Type = AiCheckType.ClaudeAiModel, AiScore = 0 },
            new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 60 },
            new AiCheckResult { Type = AiCheckType.VocabularyRichness, AiScore = 60 },
        };

        var score = InvokeCalculateOverallScore(results);

        // Claude score 0 is skipped, so result should be heuristics only (~60)
        Assert.InRange(score, 50, 70);
    }

    [Fact]
    public void CalculateOverallScore_AllZeroScores_Returns0()
    {
        var results = new[]
        {
            new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 0 },
            new AiCheckResult { Type = AiCheckType.VocabularyRichness, AiScore = 0 },
        };

        var score = InvokeCalculateOverallScore(results);
        Assert.Equal(0, score);
    }

    [Fact]
    public void CalculateOverallScore_AllChecksMax_ReturnsHighScore()
    {
        var results = new[]
        {
            new AiCheckResult { Type = AiCheckType.ClaudeAiModel, AiScore = 95 },
            new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 90 },
            new AiCheckResult { Type = AiCheckType.VocabularyRichness, AiScore = 85 },
            new AiCheckResult { Type = AiCheckType.RepetitivePhrasing, AiScore = 88 },
            new AiCheckResult { Type = AiCheckType.PerplexityEstimation, AiScore = 80 },
            new AiCheckResult { Type = AiCheckType.TransitionalPhrases, AiScore = 75 },
            new AiCheckResult { Type = AiCheckType.ParagraphStructure, AiScore = 70 },
            new AiCheckResult { Type = AiCheckType.PunctuationPatterns, AiScore = 65 },
            new AiCheckResult { Type = AiCheckType.HedgingLanguage, AiScore = 60 },
        };

        var score = InvokeCalculateOverallScore(results);

        Assert.True(score >= 85, $"All high scores should yield 85+, got {score}");
    }

    [Fact]
    public void CalculateOverallScore_WeightsOrder_SentenceUniformityWeighedMore()
    {
        // SentenceUniformity has weight 1.2, HedgingLanguage 0.7
        // With more checks to make the weight difference visible
        var resultsA = new[]
        {
            new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 100 },
            new AiCheckResult { Type = AiCheckType.HedgingLanguage, AiScore = 0 },
            new AiCheckResult { Type = AiCheckType.PunctuationPatterns, AiScore = 50 },
        };
        var resultsB = new[]
        {
            new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 0 },
            new AiCheckResult { Type = AiCheckType.HedgingLanguage, AiScore = 100 },
            new AiCheckResult { Type = AiCheckType.PunctuationPatterns, AiScore = 50 },
        };

        var scoreA = InvokeCalculateOverallScore(resultsA);
        var scoreB = InvokeCalculateOverallScore(resultsB);

        Assert.True(scoreA > scoreB, $"SentenceUniformity at 100 should weigh more ({scoreA}) than HedgingLanguage at 100 ({scoreB})");
    }

    private static int InvokeCalculateOverallScore(AiCheckResult[] results)
    {
        var method = typeof(AiCheckService).GetMethod("CalculateOverallScore",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (int)method!.Invoke(null, [results])!;
    }
}
