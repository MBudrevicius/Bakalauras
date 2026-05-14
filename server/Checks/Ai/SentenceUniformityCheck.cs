using System.Text.RegularExpressions;
using server.Models;

namespace server.Checks.Ai;

public partial class SentenceUniformityCheck : IAiCheck
{
    public AiCheckType Type => AiCheckType.SentenceUniformity;

    public Task<AiCheckResult> RunAsync(string text, string? apiKey = null, string? model = null)
    {
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

        var cv = mean > 0 ? stdDev / mean : 0;

        var aiScore = CalculateCvScore(cv);

        var similarPairs = 0;
        for (var i = 1; i < wordCounts.Count; i++)
        {
            var longer = Math.Max(wordCounts[i], wordCounts[i - 1]);
            var threshold = Math.Max(3, (int)Math.Round(longer * 0.15));
            if (Math.Abs(wordCounts[i] - wordCounts[i - 1]) <= threshold)
                similarPairs++;
        }
        var similarRatio = (double)similarPairs / (wordCounts.Count - 1);

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
        return cv switch
        {
            < 0.20 => 85,
            < 0.35 => (int)(85 - (cv - 0.20) / 0.15 * 15),   // 85 → 70
            < 0.50 => (int)(70 - (cv - 0.35) / 0.15 * 25),   // 70 → 45
            < 0.70 => (int)(45 - (cv - 0.50) / 0.20 * 20),   // 45 → 25
            _ => (int)Math.Max(5, 25 - (cv - 0.70) * 30)      // 25 → ~5
        };
    }

    [GeneratedRegex(@"(?<=[.!?])\s+(?=[A-Z])")]
    private static partial Regex SentenceSplitRegex();

    [GeneratedRegex(@"[\w']+")]
    private static partial Regex WordRegex();
}
