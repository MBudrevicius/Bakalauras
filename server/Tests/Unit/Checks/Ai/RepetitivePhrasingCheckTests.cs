using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

public class RepetitivePhrasingCheckTests
{
    private readonly RepetitivePhrasingCheck _check = new();

    [Fact]
    public async Task RunAsync_TooFewSentences_ReturnsZeroScore()
    {
        var text = "One. Two. Three.";
        var result = await _check.RunAsync(text);

        Assert.Equal(0, result.AiScore);
    }

    [Fact]
    public async Task RunAsync_IdenticalStarters_ReturnsNonZeroScore()
    {
        var text = "The weather is nice today. " +
                   "The people went shopping. " +
                   "The sun was setting slowly. " +
                   "The horizon glowed orange. " +
                   "The air was cool and fresh. " +
                   "The birds sang their songs. " +
                   "The market closed at five.";

        var result = await _check.RunAsync(text);

        Assert.True(result.AiScore > 0, $"Repetitive starters should produce non-zero score, got {result.AiScore}");
        Assert.InRange(result.AiScore, 0, 100);
    }

    [Fact]
    public async Task RunAsync_VariedStarters_ReturnsLowerScore()
    {
        var text = "Yesterday I went home. " +
                   "She called me later. " +
                   "After dinner we talked. " +
                   "Surprisingly it rained. " +
                   "Not a single cloud remained. " +
                   "Finally the sun appeared.";

        var result = await _check.RunAsync(text);

        Assert.InRange(result.AiScore, 0, 100);
    }

    [Fact]
    public async Task RunAsync_ReturnsCorrectType()
    {
        var text = "One sentence here. Two sentences here. Three sentences here. Four sentences here. Five sentences here.";
        var result = await _check.RunAsync(text);

        Assert.Equal(AiCheckType.RepetitivePhrasing, result.Type);
    }
}
