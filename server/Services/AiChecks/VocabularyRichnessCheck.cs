using System.Text.RegularExpressions;
using server.Models;

namespace server.Services;

/// <summary>
/// Measures Type-Token Ratio (unique words / total words).
/// AI text tends to have a more uniform, mid-range TTR (~0.45–0.65).
/// Human writing shows more extreme values (very high or very low).
/// </summary>
public partial class VocabularyRichnessCheck : IAiCheck
{
    public AiCheckType Type => AiCheckType.VocabularyRichness;

    // AI-typical TTR band — text that falls squarely in this range scores higher
    private const double AiBandLow = 0.40;
    private const double AiBandHigh = 0.65;
    private const double AiBandCenter = 0.525;

    public Task<AiCheckResult> RunAsync(string text)
    {
        var words = WordRegex().Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .ToList();

        if (words.Count < 30)
        {
            return Task.FromResult(new AiCheckResult
            {
                Type = Type,
                Title = "Vocabulary Richness",
                Description = "Not enough text for analysis (need at least 30 words).",
                AiScore = 0
            });
        }

        var uniqueWords = words.Distinct().Count();
        var ttr = (double)uniqueWords / words.Count;

        // Hapax legomena ratio (words appearing exactly once)  
        var hapaxCount = words.GroupBy(w => w).Count(g => g.Count() == 1);
        var hapaxRatio = (double)hapaxCount / words.Count;

        // Score: how close TTR is to the AI "sweet spot"
        var ttrScore = CalculateTtrScore(ttr);

        // Hapax: AI tends to have a moderate hapax ratio (0.3–0.55)
        // Humans tend toward higher hapax ratios (more unique-once words)
        var hapaxScore = CalculateHapaxScore(hapaxRatio);

        // Combine: TTR contributes 60%, hapax 40%
        var aiScore = (int)Math.Round(ttrScore * 0.6 + hapaxScore * 0.4);
        aiScore = Math.Clamp(aiScore, 0, 100);

        return Task.FromResult(new AiCheckResult
        {
            Type = Type,
            Title = "Vocabulary Richness",
            Description = $"TTR: {ttr:F3} ({uniqueWords}/{words.Count} unique words), " +
                          $"Hapax ratio: {hapaxRatio:F3}. " +
                          (aiScore >= 60 ? "Vocabulary pattern is consistent with AI-generated text."
                              : aiScore >= 40 ? "Vocabulary pattern is inconclusive."
                              : "Vocabulary pattern appears human-like."),
            AiScore = aiScore
        });
    }

    private static double CalculateTtrScore(double ttr)
    {
        // If TTR is in the AI band center, score is high
        // Further from center → lower score
        if (ttr >= AiBandLow && ttr <= AiBandHigh)
        {
            // Inside the AI band: 55–85 depending on how centered
            var distFromCenter = Math.Abs(ttr - AiBandCenter);
            var halfWidth = (AiBandHigh - AiBandLow) / 2.0;
            return 85.0 - (distFromCenter / halfWidth) * 30.0;
        }

        // Outside the band: drops off
        if (ttr < AiBandLow)
        {
            var dist = AiBandLow - ttr;
            return Math.Max(0, 55.0 - dist * 200);
        }
        else
        {
            var dist = ttr - AiBandHigh;
            return Math.Max(0, 55.0 - dist * 200);
        }
    }

    private static double CalculateHapaxScore(double hapaxRatio)
    {
        // AI hapax sweet spot: 0.30–0.55
        if (hapaxRatio >= 0.30 && hapaxRatio <= 0.55)
            return 70.0;

        // Low hapax (< 0.30) → very repetitive, could be AI or just short text
        if (hapaxRatio < 0.30)
            return 50.0;

        // High hapax (> 0.55) → very diverse vocabulary, more human-like
        var dist = hapaxRatio - 0.55;
        return Math.Max(0, 60.0 - dist * 200);
    }

    [GeneratedRegex(@"[\w']+")]
    private static partial Regex WordRegex();
}
