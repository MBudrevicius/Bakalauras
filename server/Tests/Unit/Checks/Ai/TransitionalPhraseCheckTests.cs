using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

public class TransitionalPhraseCheckTests
{
    private readonly TransitionalPhraseCheck _check = new();

    [Fact]
    public async Task RunAsync_TooFewSentences_ReturnsZeroScore()
    {
        var text = "One sentence. Two sentences.";
        var result = await _check.RunAsync(text);

        Assert.Equal(0, result.AiScore);
    }

    [Fact]
    public async Task RunAsync_ManyTransitionalPhrases_ReturnsHigherScore()
    {
        var text = "Furthermore, the analysis reveals important findings. " +
                   "Moreover, the data supports the initial hypothesis clearly. " +
                   "Additionally, several other factors contribute to the outcome. " +
                   "Consequently, we can draw meaningful conclusions from this. " +
                   "Nevertheless, there are some limitations to consider here. " +
                   "In conclusion, the evidence strongly supports our theory.";

        var result = await _check.RunAsync(text);

        Assert.True(result.AiScore >= 40, $"Formulaic transitions should score higher, got {result.AiScore}");
    }

    [Fact]
    public async Task RunAsync_NoTransitions_ReturnsLowerScore()
    {
        var text = "I went to the store yesterday afternoon. " +
                   "The weather was pretty cold outside today. " +
                   "She bought three oranges and two apples. " +
                   "My dog ran across the entire big yard. " +
                   "The movie started at exactly eight pm. " +
                   "We left the theater before it ended.";

        var result = await _check.RunAsync(text);

        Assert.True(result.AiScore < 60, $"Non-formulaic text should score lower, got {result.AiScore}");
    }

    [Fact]
    public async Task RunAsync_ReturnsCorrectType()
    {
        var text = "A sentence here. B sentence here. C sentence here. D sentence here. E sentence here.";
        var result = await _check.RunAsync(text);

        Assert.Equal(AiCheckType.TransitionalPhrases, result.Type);
    }
}
