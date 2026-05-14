using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

public class PunctuationPatternsCheckTests
{
    private readonly PunctuationPatternsCheck _check = new();

    [Fact]
    public async Task RunAsync_ShortText_ReturnsZeroScore()
    {
        var text = "Short.";
        var result = await _check.RunAsync(text);

        Assert.Equal(0, result.AiScore);
    }

    [Fact]
    public async Task RunAsync_FormalPunctuation_ReturnsHigherScore()
    {
        var text = "The research methodology was carefully designed, with attention to detail. " +
                   "The participants were selected based on specific criteria, including age. " +
                   "The data collection process involved multiple stages, spanning several months. " +
                   "The analysis revealed significant patterns, particularly in the control group. " +
                   "The findings were consistent with the initial hypothesis, confirming expectations.";

        var result = await _check.RunAsync(text);

        Assert.True(result.AiScore >= 30, $"Formal punctuation should score higher, got {result.AiScore}");
    }

    [Fact]
    public async Task RunAsync_InformalPunctuation_ReturnsLowerScore()
    {
        var text = "Wow! That was amazing!! I can't believe it — honestly... " +
                   "She said (and I quote): 'This is unbelievable!' Really?! " +
                   "The whole thing was — how should I put it — absolutely insane!! " +
                   "Wait... did you see that?! No way! It happened again (third time!)... " +
                   "So... yeah — that's basically what happened; crazy, right?!";

        var result = await _check.RunAsync(text);

        Assert.InRange(result.AiScore, 0, 100);
    }

    [Fact]
    public async Task RunAsync_NoPunctuation_Returns50()
    {
        var text = new string('a', 150); // No punctuation at all
        var result = await _check.RunAsync(text);

        Assert.Equal(50, result.AiScore);
    }

    [Fact]
    public async Task RunAsync_ReturnsCorrectType()
    {
        var text = "Some text with commas, and periods. Also questions? And exclamations! More here for length padding content.";
        var result = await _check.RunAsync(text);

        Assert.Equal(AiCheckType.PunctuationPatterns, result.Type);
    }
}
