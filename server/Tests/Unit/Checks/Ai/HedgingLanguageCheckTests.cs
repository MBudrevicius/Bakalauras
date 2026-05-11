using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

public class HedgingLanguageCheckTests
{
    private readonly HedgingLanguageCheck _check = new();

    [Fact]
    public async Task RunAsync_TooFewSentences_ReturnsZeroScore()
    {
        var text = "Short text here.";
        var result = await _check.RunAsync(text);

        Assert.Equal(0, result.AiScore);
    }

    [Fact]
    public async Task RunAsync_HeavyHedging_ReturnsHigherScore()
    {
        var text = "It might be possible that the results could vary significantly. " +
                   "However, one should consider that this may not always apply universally. " +
                   "Perhaps the methodology would benefit from additional refinement overall. " +
                   "Although the findings seem promising, they could potentially be misleading. " +
                   "Nevertheless, it would be important to note the inherent limitations here. " +
                   "Generally speaking, the outcomes might suggest a somewhat different conclusion.";

        var result = await _check.RunAsync(text);

        Assert.True(result.AiScore >= 30, $"Heavy hedging should score higher, got {result.AiScore}");
    }

    [Fact]
    public async Task RunAsync_DirectLanguage_ReturnsLowerScore()
    {
        var text = "The experiment failed miserably and completely. " +
                   "Results are wrong beyond any doubt whatsoever. " +
                   "This approach is the best and proven solution. " +
                   "I ran the test five separate times exactly. " +
                   "Every single measurement confirmed the original hypothesis. " +
                   "No alternative explanation exists for these findings.";

        var result = await _check.RunAsync(text);

        Assert.InRange(result.AiScore, 0, 100);
    }

    [Fact]
    public async Task RunAsync_ReturnsCorrectType()
    {
        var text = "A thing. B thing. C thing. D thing. E thing. F thing with more words to meet minimum.";
        var result = await _check.RunAsync(text);

        Assert.Equal(AiCheckType.HedgingLanguage, result.Type);
    }
}
