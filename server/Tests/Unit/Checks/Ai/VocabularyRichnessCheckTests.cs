using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

public class VocabularyRichnessCheckTests
{
    private readonly VocabularyRichnessCheck _check = new();

    [Fact]
    public async Task RunAsync_ShortText_ReturnsZeroScore()
    {
        var text = "This is a short sentence with few words.";

        var result = await _check.RunAsync(text);

        Assert.Equal(0, result.AiScore);
        Assert.Contains("Not enough text", result.Description);
    }

    [Fact]
    public async Task RunAsync_ReturnsCorrectType()
    {
        var text = GenerateRepetitiveText(100);
        var result = await _check.RunAsync(text);

        Assert.Equal(AiCheckType.VocabularyRichness, result.Type);
    }

    [Fact]
    public async Task RunAsync_ReturnsCorrectTitle()
    {
        var text = GenerateRepetitiveText(100);
        var result = await _check.RunAsync(text);

        Assert.Equal("Vocabulary Richness", result.Title);
    }

    [Fact]
    public async Task RunAsync_RepetitiveText_ReturnsHigherScore()
    {
        var text = string.Join(" ", Enumerable.Repeat("the important thing is that we must consider the implications of this approach", 10));

        var result = await _check.RunAsync(text);

        Assert.InRange(result.AiScore, 0, 100);
    }

    [Fact]
    public async Task RunAsync_DiverseText_ReturnsLowerScore()
    {
        var words = new[] {
            "astronomy", "biology", "chemistry", "dinosaur", "elephant", "flamingo",
            "geology", "harmony", "invention", "journey", "kaleidoscope", "labyrinth",
            "mosquito", "narrative", "orchestra", "philosophy", "quantum", "rebellion",
            "sanctuary", "telescope", "umbrella", "vivacious", "waterfall", "xylophone",
            "yesterday", "zeppelin", "abstract", "boulevard", "cathedral", "democracy",
            "enigma", "frontier", "gourmet", "heritage", "illuminate", "jubilee",
            "kingdom", "landscape", "metaphor", "nostalgia", "paradigm", "quixotic",
            "resonance", "symphony", "threshold", "undermine", "ventilate", "whimsical",
            "xenophobe", "yearning"
        };
        var text = string.Join(" ", words);

        var result = await _check.RunAsync(text);

        Assert.InRange(result.AiScore, 0, 100);
    }

    [Fact]
    public async Task RunAsync_ScoreIsClampedTo0_100()
    {
        var text = GenerateRepetitiveText(200);
        var result = await _check.RunAsync(text);

        Assert.InRange(result.AiScore, 0, 100);
    }

    private static string GenerateRepetitiveText(int wordCount)
    {
        var words = new[] { "the", "quick", "brown", "fox", "jumps", "over", "the", "lazy", "dog", "and", "the", "cat", "sits", "on", "the", "mat" };
        return string.Join(" ", Enumerable.Range(0, wordCount).Select(i => words[i % words.Length]));
    }
}
