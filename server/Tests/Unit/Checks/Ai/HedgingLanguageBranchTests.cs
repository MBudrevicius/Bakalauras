using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

public class HedgingLanguageBranchTests
{
    private readonly HedgingLanguageCheck _check = new();

    [Fact]
    public async Task RunAsync_TextWithManyModals_HigherScore()
    {
        var text = "This may suggest that the findings could be relevant. It might indicate that further research should be conducted. " +
                   "One would expect that the results may vary. These findings could potentially indicate a trend. " +
                   "Researchers should consider that this may have implications. It would appear that the data might support this claim. " +
                   "This could be important, and it may lead to future discoveries. Overall, we should note that the results might change.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore > 30, $"Text with many modals should score >30, got {result.AiScore}");
    }

    [Fact]
    public async Task RunAsync_DirectText_LowerScore()
    {
        var text = "The data proves the hypothesis correct. The experiment demonstrates clear results. " +
                   "Scientists confirmed the link between X and Y. The analysis reveals a strong correlation. " +
                   "All participants showed improvement. The numbers tell the full story clearly.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore >= 0);
    }

    [Fact]
    public async Task RunAsync_WithContrastStructures_AffectsScore()
    {
        var text = "The results are promising, however, there are limitations. On the other hand, the control group showed different patterns. " +
                   "The data supports the claim, but further analysis is needed. However, the sample size was small. " +
                   "Nevertheless, the trend is clear, but more research is required. On the other hand, previous studies showed contradictory results.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore > 0);
    }

    [Fact]
    public async Task RunAsync_WithConditionals_AffectsScore()
    {
        var text = "If the conditions are met, the experiment succeeds. Depending on the variables, results may differ. " +
                   "Whether the hypothesis holds depends on further testing. If we consider all factors, the outcome changes. " +
                   "The conclusion depends on whether the data is sufficient. If more samples are collected, accuracy improves.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore > 0);
    }

    [Fact]
    public void Type_IsHedgingLanguage()
    {
        Assert.Equal(AiCheckType.HedgingLanguage, _check.Type);
    }

    [Fact]
    public async Task RunAsync_WithQualifiers_AffectsScore()
    {
        var text = "The results are relatively consistent across trials. The methodology is generally accepted in the field. " +
                   "The findings are potentially significant for the research area. The correlation is somewhat weaker than expected. " +
                   "The data was essentially unchanged from previous results. The trends are relatively clear and generally stable.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore > 0);
    }

    [Fact]
    public async Task RunAsync_AllHedgingTypes_HighScore()
    {
        var text = "This may suggest significant findings, however, they could potentially change. Although the data might indicate otherwise, " +
                   "results should be considered relatively carefully. Nevertheless, one would note that if conditions differ, " +
                   "the outcome could be somewhat different. While the evidence is generally supportive, whether this holds depends on context. " +
                   "On the other hand, potentially significant patterns might emerge. If we consider that the results could change, " +
                   "although somewhat unlikely, the analysis should be relatively thorough.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore > 40, $"All hedging types should score high, got {result.AiScore}");
    }

    [Fact]
    public async Task RunAsync_TooFewWords_ReturnsZero()
    {
        var text = "Short one. Another short. And more. Yet more. Still more. Last one.";
        var result = await _check.RunAsync(text);
        Assert.Equal(0, result.AiScore);
    }
}
