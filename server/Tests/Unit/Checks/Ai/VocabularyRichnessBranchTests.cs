using server.Checks.Ai;

namespace server.Tests.Unit.Checks.Ai;

public class VocabularyRichnessBranchTests
{
    private static double InvokeCalculateMattrScore(double mattr)
    {
        var method = typeof(VocabularyRichnessCheck).GetMethod("CalculateMattrScore",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (double)method.Invoke(null, [mattr])!;
    }

    private static double InvokeCalculateHapaxScore(double hapaxRatio)
    {
        var method = typeof(VocabularyRichnessCheck).GetMethod("CalculateHapaxScore",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (double)method.Invoke(null, [hapaxRatio])!;
    }

    private static double InvokeCalculateMattr(List<string> words, int window)
    {
        var method = typeof(VocabularyRichnessCheck).GetMethod("CalculateMattr",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (double)method.Invoke(null, [words, window])!;
    }

    // CalculateMattrScore tests
    [Fact]
    public void MattrScore_CenterOfAiRange_HighScore()
    {
        var score = InvokeCalculateMattrScore(0.76);
        Assert.True(score >= 80, $"MATTR 0.76 (AI center) should be >=80, got {score}");
    }

    [Fact]
    public void MattrScore_EdgeOfAiRange_LowerScore()
    {
        var score = InvokeCalculateMattrScore(0.70);
        Assert.True(score > 0 && score < 85, $"Edge of AI range, got {score}");
    }

    [Fact]
    public void MattrScore_BelowAiRange_LowScore()
    {
        var score = InvokeCalculateMattrScore(0.50);
        Assert.True(score < 60, $"Below AI range should be low score, got {score}");
    }

    [Fact]
    public void MattrScore_AboveAiRange_LowScore()
    {
        var score = InvokeCalculateMattrScore(0.95);
        Assert.True(score < 60, $"Above AI range should be low score, got {score}");
    }

    [Fact]
    public void MattrScore_VeryLow_ClampsTo0()
    {
        var score = InvokeCalculateMattrScore(0.30);
        Assert.Equal(0, score);
    }

    [Fact]
    public void MattrScore_VeryHigh_ClampsTo0()
    {
        var score = InvokeCalculateMattrScore(1.2);
        Assert.Equal(0, score);
    }

    // CalculateHapaxScore tests
    [Fact]
    public void HapaxScore_MiddleRange_Returns70()
    {
        var score = InvokeCalculateHapaxScore(0.40);
        Assert.Equal(70.0, score);
    }

    [Fact]
    public void HapaxScore_LowRatio_HigherScore()
    {
        // Low hapax = AI-like reuse
        var score = InvokeCalculateHapaxScore(0.10);
        Assert.True(score > 70, $"Low hapax ratio should be >70, got {score}");
    }

    [Fact]
    public void HapaxScore_VeryLow_ApproachesMax()
    {
        var score = InvokeCalculateHapaxScore(0.0);
        Assert.Equal(90.0, score);
    }

    [Fact]
    public void HapaxScore_HighRatio_LowerScore()
    {
        var score = InvokeCalculateHapaxScore(0.70);
        Assert.True(score < 60, $"High hapax ratio should reduce score, got {score}");
    }

    [Fact]
    public void HapaxScore_VeryHigh_ClampsTo0()
    {
        var score = InvokeCalculateHapaxScore(1.0);
        Assert.Equal(0.0, score);
    }

    // CalculateMattr tests
    [Fact]
    public void CalculateMattr_ShortList_FallsBackToTTR()
    {
        var words = new List<string> { "the", "cat", "sat", "on", "the" };
        var mattr = InvokeCalculateMattr(words, 50);
        // 4 unique / 5 total = 0.8
        Assert.Equal(0.8, mattr, 2);
    }

    [Fact]
    public void CalculateMattr_AllUnique_Returns1()
    {
        var words = new List<string> { "a", "b", "c" };
        var mattr = InvokeCalculateMattr(words, 50);
        Assert.Equal(1.0, mattr, 2);
    }

    [Fact]
    public void CalculateMattr_AllSame_ReturnsLow()
    {
        var words = Enumerable.Repeat("word", 100).ToList();
        var mattr = InvokeCalculateMattr(words, 50);
        Assert.True(mattr < 0.1, $"All same words should have very low MATTR, got {mattr}");
    }
}
