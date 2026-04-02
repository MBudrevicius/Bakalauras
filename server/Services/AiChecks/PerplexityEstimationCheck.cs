using System.Text.RegularExpressions;
using server.Models;

namespace server.Services;

/// <summary>
/// Estimates text predictability using word transition patterns.
/// AI text is more predictable — common word pairs appear more often.
/// Uses bigram repetition rate + word predictability as a proxy.
/// </summary>
public partial class PerplexityEstimationCheck : IAiCheck
{
    public AiCheckType Type => AiCheckType.PerplexityEstimation;

    public Task<AiCheckResult> RunAsync(string text)
    {
        var words = WordRegex().Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .ToList();

        if (words.Count < 40)
        {
            return Task.FromResult(new AiCheckResult
            {
                Type = Type,
                Title = "Perplexity Estimation",
                Description = "Not enough text for analysis (need at least 40 words).",
                AiScore = 0
            });
        }

        // Build bigram frequency map
        var bigramCounts = new Dictionary<string, int>();
        for (var i = 0; i < words.Count - 1; i++)
        {
            var bigram = words[i] + " " + words[i + 1];
            bigramCounts.TryGetValue(bigram, out var count);
            bigramCounts[bigram] = count + 1;
        }

        var totalBigrams = words.Count - 1;
        var uniqueBigrams = bigramCounts.Count;

        // Bigram Type-Token Ratio: unique bigrams / total bigrams
        // Lower ratio = more repetitive transitions = more predictable = more AI-like
        var bigramTtr = (double)uniqueBigrams / totalBigrams;

        // Repeated bigrams: how many bigrams appear more than once
        var repeatedBigrams = bigramCounts.Count(kv => kv.Value > 1);
        var repetitionRate = (double)repeatedBigrams / uniqueBigrams;

        // Word predictability: what fraction of words are among the top 50 most common
        var wordFreq = new Dictionary<string, int>();
        foreach (var w in words)
        {
            wordFreq.TryGetValue(w, out var c);
            wordFreq[w] = c + 1;
        }
        var top50Words = wordFreq.OrderByDescending(kv => kv.Value)
            .Take(50)
            .Select(kv => kv.Key)
            .ToHashSet();
        var top50Coverage = (double)words.Count(w => top50Words.Contains(w)) / words.Count;

        // Score components
        // bigramTtr: AI ~0.55-0.80, human ~0.80-0.98
        var ttrScore = bigramTtr switch
        {
            < 0.55 => 75.0,   // very repetitive
            < 0.70 => 70.0 - (bigramTtr - 0.55) / 0.15 * 10,
            < 0.80 => 60.0 - (bigramTtr - 0.70) / 0.10 * 15,
            < 0.90 => 45.0 - (bigramTtr - 0.80) / 0.10 * 15,
            _ => Math.Max(10, 30.0 - (bigramTtr - 0.90) * 200)
        };

        // repetitionRate: AI ~0.15-0.30, human ~0.05-0.15
        var repScore = repetitionRate switch
        {
            > 0.30 => 80.0,
            > 0.20 => 60.0 + (repetitionRate - 0.20) / 0.10 * 20,
            > 0.10 => 40.0 + (repetitionRate - 0.10) / 0.10 * 20,
            _ => 20.0 + repetitionRate / 0.10 * 20
        };

        // top50Coverage: AI tends to cover more with common words
        var coverScore = top50Coverage switch
        {
            > 0.85 => 70.0,
            > 0.75 => 55.0 + (top50Coverage - 0.75) / 0.10 * 15,
            > 0.65 => 40.0 + (top50Coverage - 0.65) / 0.10 * 15,
            _ => 20.0 + top50Coverage / 0.65 * 20
        };

        var aiScore = (int)Math.Round(ttrScore * 0.4 + repScore * 0.35 + coverScore * 0.25);
        aiScore = Math.Clamp(aiScore, 0, 100);

        return Task.FromResult(new AiCheckResult
        {
            Type = Type,
            Title = "Perplexity Estimation",
            Description = $"Bigram TTR: {bigramTtr:F3}, repetition rate: {repetitionRate:F3}, " +
                          $"top-word coverage: {top50Coverage:P0}. " +
                          (aiScore >= 60 ? "Text is highly predictable, consistent with AI."
                              : aiScore >= 40 ? "Predictability is inconclusive."
                              : "Text shows natural unpredictability."),
            AiScore = aiScore
        });
    }

    [GeneratedRegex(@"[\w']+")]
    private static partial Regex WordRegex();
}
