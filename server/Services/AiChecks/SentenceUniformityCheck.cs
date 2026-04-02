using System.Text.RegularExpressions;
using server.Models;

namespace server.Services;

/// <summary>
/// Analyzes standard deviation of sentence lengths.
/// AI tends to produce sentences of very consistent length.
/// High uniformity (low std-dev relative to mean) = higher AI probability.
/// </summary>
public partial class SentenceUniformityCheck : IAiCheck
{
    public AiCheckType Type => AiCheckType.SentenceUniformity;

    public Task<AiCheckResult> RunAsync(string text)
    {
        // Split into sentences
        var sentences = SentenceSplitRegex().Split(text)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        if (sentences.Count < 5)
        {
            return Task.FromResult(new AiCheckResult
            {
                Type = Type,
                Title = "Sentence Uniformity",
                Description = "Not enough sentences for analysis (need at least 5).",
                AiScore = 0
            });
        }

        // Get word counts per sentence
        var wordCounts = sentences
            .Select(s => WordRegex().Matches(s).Count)
            .Where(c => c > 0)
            .ToList();

        if (wordCounts.Count < 5)
        {
            return Task.FromResult(new AiCheckResult
            {
                Type = Type,
                Title = "Sentence Uniformity",
                Description = "Not enough valid sentences for analysis.",
                AiScore = 0
            });
        }

        var mean = wordCounts.Average();
        var stdDev = Math.Sqrt(wordCounts.Sum(c => Math.Pow(c - mean, 2)) / wordCounts.Count);

        // Coefficient of Variation (CV) = stdDev / mean
        // Low CV = very uniform sentence lengths = AI-like
        // Typical AI CV: 0.15–0.40
        // Typical human CV: 0.45–0.90+
        var cv = mean > 0 ? stdDev / mean : 0;

        var aiScore = CalculateCvScore(cv);

        // Also check for consecutive sentences with similar length (±3 words)
        var similarPairs = 0;
        for (var i = 1; i < wordCounts.Count; i++)
        {
            if (Math.Abs(wordCounts[i] - wordCounts[i - 1]) <= 3)
                similarPairs++;
        }
        var similarRatio = (double)similarPairs / (wordCounts.Count - 1);

        // If many consecutive sentences are similar length, boost AI score
        if (similarRatio > 0.6)
            aiScore = Math.Min(100, aiScore + 15);
        else if (similarRatio > 0.45)
            aiScore = Math.Min(100, aiScore + 8);

        aiScore = Math.Clamp(aiScore, 0, 100);

        return Task.FromResult(new AiCheckResult
        {
            Type = Type,
            Title = "Sentence Uniformity",
            Description = $"Mean sentence length: {mean:F1} words, Std dev: {stdDev:F1}, CV: {cv:F3}. " +
                          $"{similarPairs}/{wordCounts.Count - 1} consecutive sentence pairs have similar length. " +
                          (aiScore >= 60 ? "Sentence lengths are very uniform, consistent with AI."
                              : aiScore >= 40 ? "Sentence length variation is inconclusive."
                              : "Sentence lengths vary naturally, appears human-like."),
            AiScore = aiScore
        });
    }

    private static int CalculateCvScore(double cv)
    {
        // CV < 0.20 → extremely uniform → very likely AI (score ~85)
        // CV 0.20–0.35 → uniform → likely AI (score ~70)
        // CV 0.35–0.50 → moderate → inconclusive (score ~45)
        // CV 0.50–0.70 → varied → likely human (score ~25)
        // CV > 0.70 → very varied → very likely human (score ~10)
        return cv switch
        {
            < 0.20 => 85,
            < 0.35 => (int)(85 - (cv - 0.20) / 0.15 * 15),   // 85 → 70
            < 0.50 => (int)(70 - (cv - 0.35) / 0.15 * 25),   // 70 → 45
            < 0.70 => (int)(45 - (cv - 0.50) / 0.20 * 20),   // 45 → 25
            _ => (int)Math.Max(5, 25 - (cv - 0.70) * 30)      // 25 → ~5
        };
    }

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceSplitRegex();

    [GeneratedRegex(@"[\w']+")]
    private static partial Regex WordRegex();
}
