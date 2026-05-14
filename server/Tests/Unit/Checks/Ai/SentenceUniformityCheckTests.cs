using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

public class SentenceUniformityCheckTests
{
    private readonly SentenceUniformityCheck _check = new();

    [Fact]
    public async Task RunAsync_TooFewSentences_ReturnsZeroScore()
    {
        var text = "One sentence. Two sentences.";
        var result = await _check.RunAsync(text);

        Assert.Equal(0, result.AiScore);
        Assert.Contains("Not enough", result.Description);
    }

    [Fact]
    public async Task RunAsync_UniformSentences_ReturnsHighScore()
    {
        var text = "The weather is quite nice today in the city. " +
                   "The garden looks very beautiful right now indeed. " +
                   "The children are playing happily outside on swings. " +
                   "The market was very busy today morning overall. " +
                   "The sunset was absolutely gorgeous and quite calm. " +
                   "The dinner tasted really delicious this fine evening.";

        var result = await _check.RunAsync(text);

        Assert.True(result.AiScore >= 40, $"Uniform sentences should produce higher score, got {result.AiScore}");
    }

    [Fact]
    public async Task RunAsync_VariedSentences_ReturnsLowerScore()
    {
        var text = "Hi. " +
                   "The quick brown fox jumps over the lazy dog. " +
                   "Yes! " +
                   "A much longer sentence with significantly more words that goes into great detail about absolutely nothing important. " +
                   "OK. " +
                   "Another moderately sized sentence here. " +
                   "This one is a tad longer than the last one but still reasonably short.";

        var result = await _check.RunAsync(text);

        Assert.InRange(result.AiScore, 0, 100);
    }

    [Fact]
    public async Task RunAsync_ReturnsCorrectTypeAndTitle()
    {
        var text = "Sentence one here. Sentence two here. Sentence three here. Sentence four here. Sentence five here.";
        var result = await _check.RunAsync(text);

        Assert.Equal(AiCheckType.SentenceUniformity, result.Type);
        Assert.Equal("Sentence Uniformity", result.Title);
    }

    [Fact]
    public async Task RunAsync_ScoreClamped0To100()
    {
        var text = string.Join(". ", Enumerable.Repeat("The same exact sentence length repeated many times over", 20)) + ".";
        var result = await _check.RunAsync(text);

        Assert.InRange(result.AiScore, 0, 100);
    }
}
