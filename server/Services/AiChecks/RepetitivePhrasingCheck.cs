using server.Models;

namespace server.Services;

/// <summary>
/// Detects phrases commonly overused by AI models.
/// High density of such phrases = higher AI probability.
/// </summary>
public class RepetitivePhrasingCheck : IAiCheck
{
    public AiCheckType Type => AiCheckType.RepetitivePhrasing;

    // Phrases strongly associated with AI-generated text, with weights
    private static readonly (string Phrase, int Weight)[] AiPhrases =
    [
        // High-signal AI markers (weight 3)
        ("it's important to note", 3),
        ("it is important to note", 3),
        ("it's worth noting", 3),
        ("it is worth noting", 3),
        ("it's worth mentioning", 3),
        ("it is worth mentioning", 3),
        ("in today's digital age", 3),
        ("in today's world", 3),
        ("in today's fast-paced", 3),
        ("dive into", 3),
        ("delve into", 3),
        ("delves into", 3),
        ("let's delve", 3),
        ("shall we delve", 3),
        ("navigating the", 3),
        ("navigate the complexities", 3),
        ("tapestry of", 3),
        ("rich tapestry", 3),
        ("multifaceted", 3),

        // Medium-signal (weight 2)
        ("landscape of", 2),
        ("evolving landscape", 2),
        ("ever-evolving", 2),
        ("ever-changing", 2),
        ("comprehensive guide", 2),
        ("comprehensive overview", 2),
        ("holistic approach", 2),
        ("leverage the", 2),
        ("leveraging", 2),
        ("foster a", 2),
        ("fostering", 2),
        ("in conclusion", 2),
        ("to summarize", 2),
        ("in summary", 2),
        ("as we've seen", 2),
        ("it's crucial to", 2),
        ("it is crucial to", 2),
        ("it's essential to", 2),
        ("it is essential to", 2),
        ("plays a crucial role", 2),
        ("plays a vital role", 2),
        ("plays a pivotal role", 2),
        ("a myriad of", 2),
        ("paradigm shift", 2),
        ("cutting-edge", 2),
        ("game-changer", 2),
        ("groundbreaking", 2),
        ("revolutionize", 2),
        ("revolutionizing", 2),
        ("streamline", 2),
        ("empower individuals", 2),
        ("empowering", 2),
        ("moreover", 2),
        ("furthermore", 2),
        ("additionally", 2),
        ("consequently", 2),

        // Lower-signal but cumulative (weight 1)
        ("robust", 1),
        ("seamless", 1),
        ("intricate", 1),
        ("pivotal", 1),
        ("nuanced", 1),
        ("realm of", 1),
        ("plethora", 1),
        ("utilize", 1),
        ("utilization", 1),
        ("facilitate", 1),
        ("harnessing", 1),
        ("harness the power", 1),
        ("at the end of the day", 1),
        ("when it comes to", 1),
        ("on the other hand", 1),
        ("that being said", 1),
        ("having said that", 1),
        ("with that in mind", 1),
        ("it goes without saying", 1),
    ];

    public Task<AiCheckResult> RunAsync(string text)
    {
        var lower = text.ToLowerInvariant();
        var wordCount = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

        if (wordCount < 30)
        {
            return Task.FromResult(new AiCheckResult
            {
                Type = Type,
                Title = "Repetitive Phrasing",
                Description = "Not enough text for analysis (need at least 30 words).",
                AiScore = 0
            });
        }

        var matchedPhrases = new List<string>();
        var totalWeight = 0;

        foreach (var (phrase, weight) in AiPhrases)
        {
            var count = CountOccurrences(lower, phrase);
            if (count > 0)
            {
                matchedPhrases.Add(phrase);
                totalWeight += weight * count;
            }
        }

        // Density: weighted hits per 100 words
        var density = (double)totalWeight / wordCount * 100.0;

        // Score mapping:
        // density 0       → 0
        // density 0.5     → 25
        // density 1.0     → 45
        // density 2.0     → 65
        // density 3.0+    → 80+
        var aiScore = density switch
        {
            0 => 5,
            < 0.5 => (int)(5 + density / 0.5 * 20),
            < 1.5 => (int)(25 + (density - 0.5) * 20),
            < 3.0 => (int)(45 + (density - 1.5) / 1.5 * 35),
            _ => (int)Math.Min(95, 80 + (density - 3.0) * 5)
        };
        aiScore = Math.Clamp(aiScore, 0, 100);

        var topPhrases = matchedPhrases.Take(5);
        var phraseList = matchedPhrases.Count > 0
            ? $"Found {matchedPhrases.Count} AI-associated phrase(s): {string.Join(", ", topPhrases.Select(p => $"'{p}'"))}{(matchedPhrases.Count > 5 ? "..." : "")}. "
            : "No AI-associated phrases found. ";

        return Task.FromResult(new AiCheckResult
        {
            Type = Type,
            Title = "Repetitive Phrasing",
            Description = phraseList +
                          (aiScore >= 60 ? "Phrasing is consistent with AI-generated text."
                              : aiScore >= 35 ? "Some AI-typical phrasing detected."
                              : "Phrasing appears natural."),
            AiScore = aiScore
        });
    }

    private static int CountOccurrences(string text, string phrase)
    {
        var count = 0;
        var idx = 0;
        while ((idx = text.IndexOf(phrase, idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += phrase.Length;
        }
        return count;
    }
}
