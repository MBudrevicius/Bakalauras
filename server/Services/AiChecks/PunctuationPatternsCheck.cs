using server.Models;

namespace server.Services;

/// <summary>
/// Analyzes punctuation usage patterns.
/// AI tends to use very formulaic punctuation: heavy on commas and periods,
/// rarely uses dashes, ellipses, exclamation marks, or parentheses.
/// </summary>
public class PunctuationPatternsCheck : IAiCheck
{
    public AiCheckType Type => AiCheckType.PunctuationPatterns;

    public Task<AiCheckResult> RunAsync(string text)
    {
        if (text.Length < 100)
        {
            return Task.FromResult(new AiCheckResult
            {
                Type = Type,
                Title = "Punctuation Patterns",
                Description = "Not enough text for analysis.",
                AiScore = 0
            });
        }

        var totalChars = (double)text.Length;

        // Count punctuation types
        var commas      = text.Count(c => c == ',');
        var periods     = text.Count(c => c == '.');
        var exclamation = text.Count(c => c == '!');
        var question    = text.Count(c => c == '?');
        var semicolons  = text.Count(c => c == ';');
        var colons      = text.Count(c => c == ':');
        var dashes      = text.Count(c => c == '-' || c == '—' || c == '–');
        var ellipses    = CountSubstring(text, "...") + text.Count(c => c == '…');
        var parens      = text.Count(c => c == '(' || c == ')');

        var totalPunct = commas + periods + exclamation + question + semicolons + colons + dashes + ellipses + parens;
        if (totalPunct == 0)
        {
            return Task.FromResult(new AiCheckResult
            {
                Type = Type,
                Title = "Punctuation Patterns",
                Description = "No punctuation found in text.",
                AiScore = 50
            });
        }

        // Ratios
        var commaRatio      = commas / (double)totalPunct;
        var periodRatio     = periods / (double)totalPunct;
        var formalRatio     = (commas + periods + semicolons + colons) / (double)totalPunct;
        var informalRatio   = (exclamation + dashes + ellipses + parens) / (double)totalPunct;
        var exclamationRate = exclamation / (totalChars / 1000.0);
        var dashRate        = dashes / (totalChars / 1000.0);

        var aiSignals = 0;
        var humanSignals = 0;
        var details = new List<string>();

        // AI signal: commas + periods dominate (>85% of all punctuation)
        if (formalRatio > 0.85)
        {
            aiSignals += 2;
            details.Add("punctuation is dominated by commas/periods");
        }
        else if (formalRatio > 0.75)
        {
            aiSignals += 1;
        }

        // AI signal: very few exclamation marks
        if (exclamationRate < 0.5)
            aiSignals += 1;

        // AI signal: very few dashes
        if (dashRate < 1.0)
            aiSignals += 1;

        // AI signal: no ellipses, no parentheses
        if (ellipses == 0 && parens == 0)
        {
            aiSignals += 1;
            details.Add("no ellipses or parentheses");
        }

        // Human signal: uses exclamation marks
        if (exclamationRate > 2.0)
        {
            humanSignals += 2;
            details.Add("frequent exclamation marks");
        }

        // Human signal: uses dashes frequently
        if (dashRate > 3.0)
        {
            humanSignals += 2;
            details.Add("frequent dashes");
        }

        // Human signal: uses ellipses
        if (ellipses > 0)
        {
            humanSignals += 1;
            details.Add("uses ellipses");
        }

        // Human signal: uses parentheses
        if (parens > 2)
            humanSignals += 1;

        // Human signal: informal punctuation variety
        if (informalRatio > 0.20)
        {
            humanSignals += 2;
            details.Add("diverse informal punctuation");
        }

        // Calculate score: max AI signals ~6, max human signals ~8
        var netSignal = aiSignals - humanSignals;
        // Map from [-8, 6] to [0, 100]
        var aiScore = (int)Math.Round(50.0 + netSignal * 7.0);
        aiScore = Math.Clamp(aiScore, 5, 95);

        var detailStr = details.Count > 0
            ? string.Join("; ", details) + ". "
            : "";

        return Task.FromResult(new AiCheckResult
        {
            Type = Type,
            Title = "Punctuation Patterns",
            Description = $"Formal punct: {formalRatio:P0}, informal: {informalRatio:P0}. {detailStr}" +
                          (aiScore >= 60 ? "Punctuation is very formulaic, consistent with AI."
                              : aiScore >= 40 ? "Punctuation pattern is inconclusive."
                              : "Punctuation has human-like variety."),
            AiScore = aiScore
        });
    }

    private static int CountSubstring(string text, string sub)
    {
        var count = 0;
        var idx = 0;
        while ((idx = text.IndexOf(sub, idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += sub.Length;
        }
        return count;
    }
}
