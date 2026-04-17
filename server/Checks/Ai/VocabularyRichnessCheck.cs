using System.Text.RegularExpressions;
using server.Models;

namespace server.Checks.Ai;

public partial class VocabularyRichnessCheck : IAiCheck
{
    public AiCheckType Type => AiCheckType.VocabularyRichness;

    public Task<AiCheckResult> RunAsync(string text, string? apiKey = null, string? model = null)
    {
        var words = WordRegex().Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .ToList();

        if (words.Count < 50)
        {
            return Task.FromResult(new AiCheckResult
            {
                Type = Type,
                Title = "Vocabulary Richness",
                Description = "Not enough text for analysis (need at least 50 words).",
                AiScore = 0
            });
        }

        // MATTR: Moving Average TTR with window of 50 words (length-independent)
        const int window = 50;
        var mattr = CalculateMattr(words, window);

        var hapaxCount = words.GroupBy(w => w).Count(g => g.Count() == 1);
        var hapaxRatio = (double)hapaxCount / words.Count;

        var mattrScore = CalculateMattrScore(mattr);
        var hapaxScore = CalculateHapaxScore(hapaxRatio);

        var aiScore = (int)Math.Round(mattrScore * 0.6 + hapaxScore * 0.4);
        aiScore = Math.Clamp(aiScore, 0, 100);

        var uniqueWords = words.Distinct().Count();

        return Task.FromResult(new AiCheckResult
        {
            Type = Type,
            Title = "Vocabulary Richness",
            Description = $"MATTR: {mattr:F3} (window={window}), " +
                          $"{uniqueWords}/{words.Count} unique words, " +
                          $"Hapax ratio: {hapaxRatio:F3}. " +
                          (aiScore >= 60 ? "Vocabulary pattern is consistent with AI-generated text."
                              : aiScore >= 40 ? "Vocabulary pattern is inconclusive."
                              : "Vocabulary pattern appears human-like."),
            AiScore = aiScore
        });
    }

    private static double CalculateMattr(List<string> words, int window)
    {
        if (words.Count <= window)
        {
            return (double)words.Distinct().Count() / words.Count;
        }

        var sum = 0.0;
        var steps = words.Count - window + 1;
        for (var i = 0; i < steps; i++)
        {
            var segment = words.Skip(i).Take(window);
            var unique = segment.Distinct().Count();
            sum += (double)unique / window;
        }
        return sum / steps;
    }

    private static double CalculateMattrScore(double mattr)
    {
        // AI text typically has MATTR in the 0.70-0.82 range
        // Human text is usually more varied (higher or lower depending on genre)
        if (mattr >= 0.70 && mattr <= 0.82)
        {
            var distFromCenter = Math.Abs(mattr - 0.76);
            var halfWidth = 0.06;
            return 85.0 - (distFromCenter / halfWidth) * 30.0;
        }

        if (mattr < 0.70)
        {
            var dist = 0.70 - mattr;
            return Math.Max(0, 55.0 - dist * 200);
        }
        else
        {
            var dist = mattr - 0.82;
            return Math.Max(0, 55.0 - dist * 200);
        }
    }

    private static double CalculateHapaxScore(double hapaxRatio)
    {
        // AI text tends to reuse vocabulary → lower hapax ratio
        // Human text has more unique words → higher hapax ratio
        if (hapaxRatio >= 0.30 && hapaxRatio <= 0.55)
            return 70.0;

        if (hapaxRatio < 0.30)
        {
            // Very low hapax = heavy vocab reuse = more AI-like
            return 70.0 + (0.30 - hapaxRatio) / 0.30 * 20.0; // 70 → 90
        }

        var dist = hapaxRatio - 0.55;
        return Math.Max(0, 60.0 - dist * 200);
    }

    [GeneratedRegex(@"[\w']+")]
    private static partial Regex WordRegex();
}
