using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

public class PerplexityEstimationCheckTests
{
    private readonly PerplexityEstimationCheck _check = new();

    [Fact]
    public async Task RunAsync_ShortText_ReturnsZeroScore()
    {
        var text = "Too short for analysis.";
        var result = await _check.RunAsync(text);

        Assert.Equal(0, result.AiScore);
        Assert.Contains("Not enough text", result.Description);
    }

    [Fact]
    public async Task RunAsync_HighlyPredictableText_ScoresHigher()
    {
        // Very repetitive bigrams/trigrams — AI-like
        var text = string.Join(" ", Enumerable.Repeat("it is important to note that the key factor in this analysis is the fundamental approach to the problem", 5));

        var result = await _check.RunAsync(text);

        Assert.True(result.AiScore >= 30, $"Predictable text should produce moderate/high score, got {result.AiScore}");
    }

    [Fact]
    public async Task RunAsync_ReturnsCorrectTypeAndTitle()
    {
        var text = string.Join(" ", Enumerable.Range(0, 50).Select(i => $"word{i}"));
        var result = await _check.RunAsync(text);

        Assert.Equal(AiCheckType.PerplexityEstimation, result.Type);
        Assert.Equal("Text Predictability", result.Title);
    }

    [Fact]
    public async Task RunAsync_ScoreClamped0To100()
    {
        var text = string.Join(" ", Enumerable.Repeat("the cat sat on the mat and the dog lay on the rug", 10));
        var result = await _check.RunAsync(text);

        Assert.InRange(result.AiScore, 0, 100);
    }
}
