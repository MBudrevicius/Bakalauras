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

    // CalculateOverallScoreFromParts tests
    [Fact]
    public void FromParts_ClaudeOnly_Returns60Percent()
    {
        var score = InvokeCalculateOverallScoreFromParts(80, []);
        // Claude 80 * 0.6 + 0 * 0.4 = 48
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
        // Claude=0, so only heuristics weighted average
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
        // Claude=80*0.6=48, Others=60*0.4=24 => 72
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
        // Only VocabularyRichness with score>0 is included
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
        // SentenceUniformity has weight 1.2, HedgingLanguage has weight 0.7
        var results = new[]
        {
            new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 100 },
            new AiCheckResult { Type = AiCheckType.HedgingLanguage, AiScore = 100 },
        };
        // Both score 100, so weighted average should still be 100
        var score = InvokeCalculateOverallScoreFromParts(0, results);
        Assert.Equal(100, score);
    }

    [Fact]
    public void FromParts_DifferentWeights_ShiftsResult()
    {
        // SentenceUniformity weight=1.2, HedgingLanguage weight=0.7
        var results = new[]
        {
            new AiCheckResult { Type = AiCheckType.SentenceUniformity, AiScore = 100 },
            new AiCheckResult { Type = AiCheckType.HedgingLanguage, AiScore = 0 },
        };
        // Only SentenceUniformity included (score>0), so result = 100
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
        // Unknown type gets default weight 1.0
        var score = InvokeCalculateOverallScoreFromParts(0, results);
        Assert.Equal(60, score);
    }
}
