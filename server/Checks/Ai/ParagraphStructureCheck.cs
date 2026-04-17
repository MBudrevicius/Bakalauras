using System.Text.RegularExpressions;
using server.Models;

namespace server.Checks.Ai;

public partial class ParagraphStructureCheck : IAiCheck
{
    public AiCheckType Type => AiCheckType.ParagraphStructure;

    public Task<AiCheckResult> RunAsync(string text, string? apiKey = null, string? model = null)
    {
        var paragraphs = ParagraphSplitRegex().Split(text)
            .Select(p => p.Trim())
            .Where(p => p.Length > 20) // skip tiny fragments
            .ToList();

        if (paragraphs.Count < 3)
        {
            return Task.FromResult(new AiCheckResult
            {
                Type = Type,
                Title = "Paragraph Structure",
                Description = "Not enough paragraphs for analysis (need at least 3).",
                AiScore = 0
            });
        }

        // Word counts per paragraph
        var wordCounts = paragraphs
            .Select(p => WordRegex().Matches(p).Count)
            .ToList();

        var mean = wordCounts.Average();
        var stdDev = Math.Sqrt(wordCounts.Sum(c => Math.Pow(c - mean, 2)) / wordCounts.Count);
        var cv = mean > 0 ? stdDev / mean : 0;

        // Sentence counts per paragraph
        var sentCounts = paragraphs
            .Select(p => SentenceSplitRegex().Split(p).Count(s => s.Trim().Length > 0))
            .ToList();

        var sentMean = sentCounts.Average();
        var sentStdDev = Math.Sqrt(sentCounts.Sum(c => Math.Pow(c - sentMean, 2)) / sentCounts.Count);
        var sentCv = sentMean > 0 ? sentStdDev / sentMean : 0;

        // AI: paragraphs tend to have similar length (low CV) and similar sentence count
        // Human: high variability
        var wordCvScore = cv switch
        {
            < 0.15 => 85,
            < 0.25 => (int)(85 - (cv - 0.15) / 0.10 * 15),    // 85 → 70
            < 0.40 => (int)(70 - (cv - 0.25) / 0.15 * 25),    // 70 → 45
            < 0.60 => (int)(45 - (cv - 0.40) / 0.20 * 20),    // 45 → 25
            _ => (int)Math.Max(5, 25 - (cv - 0.60) * 30)
        };

        var sentCvScore = sentCv switch
        {
            < 0.15 => 80,
            < 0.30 => (int)(80 - (sentCv - 0.15) / 0.15 * 15),
            < 0.50 => (int)(65 - (sentCv - 0.30) / 0.20 * 25),
            _ => (int)Math.Max(5, 40 - (sentCv - 0.50) * 40)
        };

        var starters = paragraphs
            .Select(p => WordRegex().Match(p).Value.ToLowerInvariant())
            .ToList();
        var mostCommonStarter = starters
            .GroupBy(s => s)
            .OrderByDescending(g => g.Count())
            .First();
        var starterRepeatRatio = (double)mostCommonStarter.Count() / paragraphs.Count;

        var starterBonus = starterRepeatRatio > 0.5 ? 10 : 0;

        var aiScore = (int)Math.Round(wordCvScore * 0.5 + sentCvScore * 0.35 + starterBonus * 0.15 * 10);
        aiScore = Math.Clamp(aiScore, 0, 100);

        return Task.FromResult(new AiCheckResult
        {
            Type = Type,
            Title = "Paragraph Structure",
            Description = $"{paragraphs.Count} paragraphs analyzed. " +
                          $"Word-count CV: {cv:F3}, sentence-count CV: {sentCv:F3}. " +
                          (starterRepeatRatio > 0.5 ? $"Many paragraphs start with '{mostCommonStarter.Key}'. " : "") +
                          (aiScore >= 60 ? "Paragraph structure is very uniform, consistent with AI."
                              : aiScore >= 40 ? "Paragraph structure is inconclusive."
                              : "Paragraphs vary naturally, appears human-like."),
            AiScore = aiScore
        });
    }

    [GeneratedRegex(@"\n\s*\n|\r\n\s*\r\n")]
    private static partial Regex ParagraphSplitRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+(?=[A-Z])")]
    private static partial Regex SentenceSplitRegex();

    [GeneratedRegex(@"[\w']+")]
    private static partial Regex WordRegex();
}
