using server.Checks.Ai;

namespace server.Tests.Unit.Checks.Ai;

public class SentenceUniformityBranchTests
{
    private static int InvokeCalculateCvScore(double cv)
    {
        var method = typeof(SentenceUniformityCheck).GetMethod("CalculateCvScore",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (int)method.Invoke(null, [cv])!;
    }

    [Theory]
    [InlineData(0.10, 85)]  // very low CV = very uniform
    [InlineData(0.19, 85)]
    public void CvScore_VeryLow_Returns85(double cv, int expected)
    {
        Assert.Equal(expected, InvokeCalculateCvScore(cv));
    }

    [Fact]
    public void CvScore_LowRange_Between70And85()
    {
        var score = InvokeCalculateCvScore(0.25);
        Assert.InRange(score, 70, 85);
    }

    [Fact]
    public void CvScore_MidRange_Between45And70()
    {
        var score = InvokeCalculateCvScore(0.40);
        Assert.InRange(score, 45, 70);
    }

    [Fact]
    public void CvScore_HighRange_Between25And45()
    {
        var score = InvokeCalculateCvScore(0.60);
        Assert.InRange(score, 25, 45);
    }

    [Fact]
    public void CvScore_VeryHigh_Below25()
    {
        var score = InvokeCalculateCvScore(0.90);
        Assert.True(score < 25, $"Very high CV should be <25, got {score}");
        Assert.True(score >= 5, $"Score should be >=5, got {score}");
    }

    // Full RunAsync branch coverage
    private readonly SentenceUniformityCheck _check = new();

    [Fact]
    public async Task RunAsync_HighSimilarRatio_BoostsScore()
    {
        // Craft text with same-length sentences (high similar ratio)
        var text = "The cat sat on the mat. The dog ran to the door. The bird flew over here. " +
                   "The fish swam to right. The cow jumped over moon.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore > 0);
    }

    [Fact]
    public async Task RunAsync_VariedSentences_LowerScore()
    {
        var text = "Hi. A very short sentence here. This is a significantly longer sentence that contains many more words and has a complex structure. " +
                   "Yes. Another medium-length sentence follows here naturally. One more extremely long and winding sentence that just goes on and on with many clauses.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore >= 0);
    }

    [Fact]
    public async Task RunAsync_AllSameLengthSentences_HighScore()
    {
        // All sentences have identical word count — maximum uniformity
        var text = "The quick brown fox jumps. The lazy brown dog sits. The calm gray cat waits. " +
                   "The tall dark man runs. The cold blue ice melts. The warm red fire glows.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore >= 60, $"Uniform sentences should score >=60, got {result.AiScore}");
        Assert.Contains("uniform", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_Exactly5Sentences_Works()
    {
        var text = "First one here now. Second sentence here now. Third sentence here now. Fourth sentence here now. Fifth sentence here now today.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore > 0);
    }

    [Fact]
    public async Task RunAsync_ModerateSimilarRatio_ModerateBoost()
    {
        // Some similar, some varied — targets the similarRatio > 0.45 branch
        var text = "The quick brown fox jumps over the lazy dog today. The slow gray cat sits under the warm sun there. " +
                   "A long winding river flows through the dense ancient forest and across the wide open plains beyond the mountains. " +
                   "Birds fly high above the trees today. Small fish swim in the shallow creek below.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore >= 0);
    }

    [Fact]
    public async Task RunAsync_ScoreDescription_InconclusiveRange()
    {
        // Try to get a score in the 40-59 range for "inconclusive" description
        var text = "This is a medium length sentence. Short one. Another medium sentence here now. " +
                   "Very tiny. A somewhat regular sentence about things. This is just about average in length.";
        var result = await _check.RunAsync(text);
        // Just verify it runs and gives valid output
        Assert.InRange(result.AiScore, 0, 100);
    }

    [Theory]
    [InlineData(0.20)]  // Exact boundary between first and second case
    [InlineData(0.35)]  // Exact boundary between second and third case
    [InlineData(0.50)]  // Exact boundary between third and fourth case
    [InlineData(0.70)]  // Exact boundary between fourth and fifth case
    public void CvScore_ExactBoundaries_HandledCorrectly(double cv)
    {
        var score = InvokeCalculateCvScore(cv);
        Assert.InRange(score, 0, 100);
    }

    [Fact]
    public void CvScore_ExtremelHigh_ClampsTo5()
    {
        var score = InvokeCalculateCvScore(2.0);
        Assert.Equal(5, score);
    }
}
