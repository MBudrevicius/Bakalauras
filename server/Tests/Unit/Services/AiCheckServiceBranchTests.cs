using server.Models;
using server.Services;

namespace server.Tests.Unit.Services;

public class AiCheckServiceBranchTests
{
    private static int InvokeCalculateOverallScoreFromParts(int claudeScore, AiCheckResult[] heuristicResults)
    {
        var method = typeof(AiCheckService).GetMethod("CalculateOverallScoreFromParts",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (int)method.Invoke(null, [claudeScore, heuristicResults])!;
    }

    [Fact]
    public void FromParts_ClaudeOnly_Returns60Percent()
    {
        var score = InvokeCalculateOverallScoreFromParts(80, []);
        Assert.Equal(48, score);
    }

    [Fact]
    public void FromParts_HeuristicsOnly_ReturnsWeighted()
    {
        var results = new[]
        {
            new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 50 },
            new AiCheckResult { Type = AiCheckType.VocabularyRichness, AiScore = 50 },
        };
        var score = InvokeCalculateOverallScoreFromParts(0, results);
        Assert.Equal(50, score);
    }

    [Fact]
    public void FromParts_BothPresent_BlendsProperly()
    {
        var results = new[]
        {
            new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 60 },
        };
        var score = InvokeCalculateOverallScoreFromParts(80, results);
        Assert.Equal(72, score);
    }

    [Fact]
    public void FromParts_ZeroScoreHeuristics_Excluded()
    {
        var results = new[]
        {
            new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 0 },
            new AiCheckResult { Type = AiCheckType.VocabularyRichness, AiScore = 80 },
        };
        var score = InvokeCalculateOverallScoreFromParts(0, results);
        Assert.Equal(80, score);
    }

    [Fact]
    public void FromParts_AllZero_ReturnsZero()
    {
        var results = new[]
        {
            new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 0 },
        };
        var score = InvokeCalculateOverallScoreFromParts(0, results);
        Assert.Equal(0, score);
    }

    [Fact]
    public void FromParts_WeightsAppliedCorrectly()
    {
        var results = new[]
        {
            new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 100 },
            new AiCheckResult { Type = AiCheckType.HedgingLanguage, AiScore = 100 },
        };
        var score = InvokeCalculateOverallScoreFromParts(0, results);
        Assert.Equal(100, score);
    }

    [Fact]
    public void FromParts_DifferentWeights_ShiftsResult()
    {
        var results = new[]
        {
            new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 100 },
            new AiCheckResult { Type = AiCheckType.HedgingLanguage, AiScore = 0 },
        };
        var score = InvokeCalculateOverallScoreFromParts(0, results);
        Assert.Equal(100, score);
    }

    [Fact]
    public void FromParts_UnknownCheckType_DefaultWeight1()
    {
        var results = new[]
        {
            new AiCheckResult { Type = (AiCheckType)999, AiScore = 60 },
        };
        var score = InvokeCalculateOverallScoreFromParts(0, results);
        Assert.Equal(60, score);
    }
}
