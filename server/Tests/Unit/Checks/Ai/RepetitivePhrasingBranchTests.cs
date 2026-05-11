using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

public class RepetitivePhrasingBranchTests
{
    private readonly RepetitivePhrasingCheck _check = new();

    [Fact]
    public async Task RunAsync_HighlyRepetitive_HighScore()
    {
        // Same sentence starters and repeated trigrams
        var text = "The study shows that results are significant. The study shows that data supports the claim. " +
                   "The study shows that evidence is strong. The study shows that the hypothesis holds. " +
                   "The study shows that the trend continues. The study shows that findings are consistent.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore > 50, $"Highly repetitive text should score >50, got {result.AiScore}");
    }

    [Fact]
    public async Task RunAsync_VariedText_LowScore()
    {
        // No repeated sentence starters, varied vocabulary
        var text = "The cat jumped over the tall wooden fence yesterday. Running quickly through the park felt liberating. " +
                   "Many birds sang loudly from ancient oak trees nearby. Children played happily in the warm summer rain. " +
                   "A gentle breeze carried the sweet scent of jasmine. Sunset painted the sky in brilliant orange hues.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore < 70, $"Varied text should score <70, got {result.AiScore}");
    }

    [Fact]
    public void Type_IsRepetitivePhrasing()
    {
        Assert.Equal(AiCheckType.RepetitivePhrasing, _check.Type);
    }
}
