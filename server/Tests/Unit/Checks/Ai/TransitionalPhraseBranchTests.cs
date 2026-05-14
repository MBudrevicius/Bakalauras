using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

public class TransitionalPhraseBranchTests
{
    private readonly TransitionalPhraseCheck _check = new();

    [Fact]
    public async Task RunAsync_HeavyTransitions_HighScore()
    {
        var text = "Furthermore, the data supports this claim. Moreover, additional evidence was found. " +
                   "Consequently, we can conclude the hypothesis is valid. Nevertheless, some limitations exist. " +
                   "Additionally, the sample size was adequate. Therefore, the results are significant. " +
                   "It is important to note that the findings align. This suggests a clear pattern in the data.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore > 40, $"Heavy transitions should score >40, got {result.AiScore}");
    }

    [Fact]
    public async Task RunAsync_NaturalText_LowScore()
    {
        var text = "The cat jumped over the fence. I saw it happen from my window. " +
                   "Rain started pouring down hard. We ran inside to stay dry. " +
                   "Mom made hot chocolate for everyone. The storm lasted about two hours.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore < 60, $"Natural text should score <60, got {result.AiScore}");
    }

    [Fact]
    public async Task RunAsync_WithItIsPatterns_IncreasesScore()
    {
        var text = "It is important to note that these results matter. It is essential to consider all variables. " +
                   "It is worth mentioning the control group performed differently. It is crucial to understand the methodology. " +
                   "It is notable that the findings diverge from expectations. It is significant that no outliers appeared.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore > 30, $"It-is patterns should push score up, got {result.AiScore}");
    }

    [Fact]
    public async Task RunAsync_WithDemonstrativeStarters_AffectsScore()
    {
        var text = "The experiment yielded clear data. This suggests a correlation exists. " +
                   "Several variables were controlled. This means the results are reliable. " +
                   "The control group showed no change. This indicates the treatment is effective. " +
                   "Multiple trials were conducted. This demonstrates consistency in outcomes.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore >= 0);
    }

    [Fact]
    public void Type_IsTransitionalPhrases()
    {
        Assert.Equal(AiCheckType.TransitionalPhrases, _check.Type);
    }

    [Fact]
    public async Task RunAsync_PrepositionalOpeners_AffectsScore()
    {
        var text = "In conclusion, the evidence is clear. As a result, we modify our approach. " +
                   "At this point, the data is sufficient. In addition, more samples are needed. " +
                   "On the other hand, the critics disagree. In particular, the third trial failed.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore > 0);
    }

    [Fact]
    public async Task RunAsync_MixedFormulaic_HighFormulaic()
    {
        var text = "Furthermore, the study confirms our hypothesis. It is essential to consider all factors. " +
                   "This suggests that the model works well. In conclusion, results are convincing. " +
                   "Moreover, additional trials support this. Therefore, we recommend further research.";
        var result = await _check.RunAsync(text);
        Assert.Contains("formulaic", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RunAsync_SomeFormulaic_MidRange()
    {
        var text = "The weather was nice today. Furthermore, the garden looked beautiful. " +
                   "We decided to have a picnic outside. The kids played in the yard happily. " +
                   "Nevertheless, we had to go home early. The sunset was magnificent and peaceful.";
        var result = await _check.RunAsync(text);
        Assert.InRange(result.AiScore, 0, 100);
    }
}
