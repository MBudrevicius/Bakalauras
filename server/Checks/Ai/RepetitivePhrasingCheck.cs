using System.Text.RegularExpressions;
using server.Models;

namespace server.Checks.Ai;

public partial class RepetitivePhrasingCheck : IAiCheck
{
    public AiCheckType Type => AiCheckType.RepetitivePhrasing;

    public Task<AiCheckResult> RunAsync(string text, string? apiKey = null, string? model = null)
    {
        var sentences = SentenceSplitRegex().Split(text)
            .Select(s => s.Trim())
            .Where(s => s.Length > 10)
            .ToList();

        if (sentences.Count < 5)
        {
            return Task.FromResult(new AiCheckResult
            {
                Type = Type,
                Title = "Repetitive Phrasing",
                Description = "Not enough sentences for analysis (need at least 5).",
                AiScore = 0
            });
        }

        var openers3 = sentences
            .Select(s => GetFirstNWords(s, 3))
            .Where(s => s.Length > 0)
            .ToList();

        var openers5 = sentences
            .Select(s => GetFirstNWords(s, 5))
            .Where(s => s.Length > 0)
            .ToList();

        // Repeated sentence starters (3-word)
        var starterGroups = openers3.GroupBy(o => o, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();
        var repeatedStarterCount = starterGroups.Sum(g => g.Count());
        var starterRepetitionRate = (double)repeatedStarterCount / sentences.Count;

        // Repeated 5-word openings (stronger signal)
        var opener5Groups = openers5.GroupBy(o => o, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();
        var repeated5Count = opener5Groups.Sum(g => g.Count());
        var opener5Rate = (double)repeated5Count / sentences.Count;

        var words = WordRegex().Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .ToList();

        var trigramCounts = new Dictionary<string, int>();
        for (var i = 0; i < words.Count - 2; i++)
        {
            var trigram = $"{words[i]} {words[i + 1]} {words[i + 2]}";
            trigramCounts.TryGetValue(trigram, out var count);
            trigramCounts[trigram] = count + 1;
        }

        var totalTrigrams = Math.Max(1, words.Count - 2);
        var repeatedTrigrams = trigramCounts.Count(kv => kv.Value >= 3);
        var trigramRepRate = (double)repeatedTrigrams / totalTrigrams * 100;

        var starterScore = starterRepetitionRate switch
        {
            > 0.50 => 85,
            > 0.35 => 70,
            > 0.20 => 55,
            > 0.10 => 40,
            _ => 20
        };

        var opener5Score = opener5Rate switch
        {
            > 0.30 => 90,
            > 0.15 => 70,
            > 0.05 => 50,
            _ => 20
        };

        var trigramScore = trigramRepRate switch
        {
            > 2.0 => 80,
            > 1.0 => 65,
            > 0.5 => 50,
            _ => 25
        };

        var aiScore = (int)Math.Round(starterScore * 0.35 + opener5Score * 0.35 + trigramScore * 0.30);
        aiScore = Math.Clamp(aiScore, 0, 100);

        var details = new List<string>
        {
            $"{repeatedStarterCount}/{sentences.Count} sentences share repeated openings"
        };

        if (repeatedTrigrams > 0)
            details.Add($"{repeatedTrigrams} trigram(s) appear 3+ times");

        if (starterGroups.Count > 0)
        {
            var topStarter = starterGroups.OrderByDescending(g => g.Count()).First();
            details.Add($"most repeated opener: \"{topStarter.Key}\" ({topStarter.Count()}x)");
        }

        return Task.FromResult(new AiCheckResult
        {
            Type = Type,
            Title = "Repetitive Phrasing",
            Description = string.Join(". ", details) + ". " +
                          (aiScore >= 60 ? "High repetition in phrasing, consistent with AI."
                              : aiScore >= 40 ? "Phrasing repetition is inconclusive."
                              : "Phrasing varies naturally, appears human-like."),
            AiScore = aiScore
        });
    }

    private static string GetFirstNWords(string sentence, int n)
    {
        var words = WordRegex().Matches(sentence)
            .Take(n)
            .Select(m => m.Value.ToLowerInvariant())
            .ToList();
        return string.Join(' ', words);
    }

    [GeneratedRegex(@"(?<=[.!?])\s+(?=[A-Z])")]
    private static partial Regex SentenceSplitRegex();

    [GeneratedRegex(@"[\w']+")]
    private static partial Regex WordRegex();
}
