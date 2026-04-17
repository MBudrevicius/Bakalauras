using System.Text.RegularExpressions;
using server.Models;

namespace server.Checks.Ai;

public partial class PerplexityEstimationCheck : IAiCheck
{
    public AiCheckType Type => AiCheckType.PerplexityEstimation;

    public Task<AiCheckResult> RunAsync(string text, string? apiKey = null, string? model = null)
    {
        var words = WordRegex().Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .ToList();

        if (words.Count < 40)
        {
            return Task.FromResult(new AiCheckResult
            {
                Type = Type,
                Title = "Text Predictability",
                Description = "Not enough text for analysis (need at least 40 words).",
                AiScore = 0
            });
        }

        // Bigram analysis - predictable text reuses word transitions
        var bigramCounts = new Dictionary<string, int>();
        for (var i = 0; i < words.Count - 1; i++)
        {
            var bigram = words[i] + " " + words[i + 1];
            bigramCounts.TryGetValue(bigram, out var count);
            bigramCounts[bigram] = count + 1;
        }

        var totalBigrams = words.Count - 1;
        var uniqueBigrams = bigramCounts.Count;
        var bigramTtr = (double)uniqueBigrams / totalBigrams;

        var repeatedBigrams = bigramCounts.Count(kv => kv.Value > 1);
        var repetitionRate = (double)repeatedBigrams / uniqueBigrams;

        // Trigram analysis - longer repeated sequences are a stronger AI signal
        var trigramCounts = new Dictionary<string, int>();
        for (var i = 0; i < words.Count - 2; i++)
        {
            var trigram = $"{words[i]} {words[i + 1]} {words[i + 2]}";
            trigramCounts.TryGetValue(trigram, out var count);
            trigramCounts[trigram] = count + 1;
        }

        var totalTrigrams = Math.Max(1, words.Count - 2);
        var uniqueTrigrams = trigramCounts.Count;
        var trigramTtr = (double)uniqueTrigrams / totalTrigrams;

        // Entropy estimation via bigram surprisal
        // Lower entropy = more predictable = more AI-like
        var entropy = 0.0;
        foreach (var kv in bigramCounts)
        {
            var p = (double)kv.Value / totalBigrams;
            entropy -= p * Math.Log2(p);
        }
        var maxEntropy = Math.Log2(totalBigrams);
        var normalizedEntropy = maxEntropy > 0 ? entropy / maxEntropy : 1.0;

        var ttrScore = bigramTtr switch
        {
            < 0.55 => 80.0,
            < 0.70 => 75.0 - (bigramTtr - 0.55) / 0.15 * 15,
            < 0.80 => 60.0 - (bigramTtr - 0.70) / 0.10 * 15,
            < 0.90 => 45.0 - (bigramTtr - 0.80) / 0.10 * 15,
            _ => Math.Max(10, 30.0 - (bigramTtr - 0.90) * 200)
        };

        var repScore = repetitionRate switch
        {
            > 0.30 => 80.0,
            > 0.20 => 60.0 + (repetitionRate - 0.20) / 0.10 * 20,
            > 0.10 => 40.0 + (repetitionRate - 0.10) / 0.10 * 20,
            _ => 20.0 + repetitionRate / 0.10 * 20
        };

        var trigramScore = trigramTtr switch
        {
            < 0.70 => 80.0,
            < 0.80 => 70.0 - (trigramTtr - 0.70) / 0.10 * 15,
            < 0.90 => 55.0 - (trigramTtr - 0.80) / 0.10 * 20,
            _ => Math.Max(10, 35.0 - (trigramTtr - 0.90) * 200)
        };

        var entropyScore = normalizedEntropy switch
        {
            < 0.75 => 80.0,
            < 0.85 => 65.0 + (0.85 - normalizedEntropy) / 0.10 * 15,
            < 0.92 => 45.0 + (0.92 - normalizedEntropy) / 0.07 * 20,
            _ => Math.Max(10, 30.0 - (normalizedEntropy - 0.92) * 200)
        };

        var aiScore = (int)Math.Round(ttrScore * 0.25 + repScore * 0.25 + trigramScore * 0.25 + entropyScore * 0.25);
        aiScore = Math.Clamp(aiScore, 0, 100);

        return Task.FromResult(new AiCheckResult
        {
            Type = Type,
            Title = "Text Predictability",
            Description = $"Bigram TTR: {bigramTtr:F3}, trigram TTR: {trigramTtr:F3}, " +
                          $"repetition rate: {repetitionRate:F3}, entropy: {normalizedEntropy:F3}. " +
                          (aiScore >= 60 ? "Text is highly predictable, consistent with AI."
                              : aiScore >= 40 ? "Predictability is inconclusive."
                              : "Text shows natural unpredictability."),
            AiScore = aiScore
        });
    }

    [GeneratedRegex(@"[\w']+")]
    private static partial Regex WordRegex();
}
