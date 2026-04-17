using server.Models;

namespace server.Checks.Ai;

public class PunctuationPatternsCheck : IAiCheck
{
    public AiCheckType Type => AiCheckType.PunctuationPatterns;

    public Task<AiCheckResult> RunAsync(string text, string? apiKey = null, string? model = null)
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

        var details = new List<string>();

        // Score each dimension independently on 0-100 scale
        var formalScore = formalRatio switch
        {
            > 0.92 => 85,
            > 0.85 => 70,
            > 0.75 => 55,
            > 0.60 => 40,
            _ => 20
        };
        if (formalRatio > 0.85) details.Add("punctuation is dominated by commas/periods");

        var diversityScore = informalRatio switch
        {
            < 0.03 => 80,
            < 0.08 => 65,
            < 0.15 => 45,
            < 0.25 => 30,
            _ => 15
        };

        var exclamationScore = exclamationRate switch
        {
            < 0.3 => 70,
            < 1.0 => 55,
            < 2.0 => 40,
            < 4.0 => 25,
            _ => 10
        };
        if (exclamationRate > 2.0) details.Add("frequent exclamation marks");

        var dashScore = dashRate switch
        {
            < 0.5 => 70,
            < 1.5 => 55,
            < 3.0 => 40,
            < 5.0 => 25,
            _ => 10
        };
        if (dashRate > 3.0) details.Add("frequent dashes");

        var specialScore = (ellipses, parens) switch
        {
            (0, 0) => 75,
            (0, _) or (_, 0) => 50,
            _ => 20
        };
        if (ellipses == 0 && parens == 0) details.Add("no ellipses or parentheses");
        if (ellipses > 0) details.Add("uses ellipses");

        var aiScore = (int)Math.Round(
            formalScore * 0.30 +
            diversityScore * 0.25 +
            exclamationScore * 0.15 +
            dashScore * 0.15 +
            specialScore * 0.15);
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
