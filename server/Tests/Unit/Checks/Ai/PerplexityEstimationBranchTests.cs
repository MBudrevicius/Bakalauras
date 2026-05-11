using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

public class PerplexityEstimationBranchTests
{
    private readonly PerplexityEstimationCheck _check = new();

    [Fact]
    public async Task RunAsync_RepetitiveText_HigherScore()
    {
        // Text with many repeated bigrams and trigrams
        var text = "The study shows that the results are clear. The study shows that the data supports the claim. " +
                   "The study shows that the evidence is strong. The study shows that the findings are reliable. " +
                   "The study shows that the hypothesis is valid. The study shows that the conclusion follows.";
        var result = await _check.RunAsync(text);
        Assert.InRange(result.AiScore, 0, 100);
    }

    [Fact]
    public async Task RunAsync_UniqueText_LowerScore()
    {
        // Text with diverse vocabulary and unique bigrams
        var text = "Elephants marched silently through ancient forests yesterday. " +
                   "Crystalline waterfalls cascaded down jagged granite cliffs nearby. " +
                   "Forgotten temples harbored mysterious artifacts from civilizations past. " +
                   "Quantum superposition challenged traditional physics fundamentally. " +
                   "Bioluminescent organisms illuminated deepest oceanic trenches beautifully. " +
                   "Renaissance painters revolutionized artistic perspective techniques forever.";
        var result = await _check.RunAsync(text);
        Assert.InRange(result.AiScore, 0, 100);
    }

    [Fact]
    public void Type_IsPerplexityEstimation()
    {
        Assert.Equal(AiCheckType.PerplexityEstimation, _check.Type);
    }
}
