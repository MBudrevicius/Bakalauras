using System.Text.RegularExpressions;
using server.Models;

namespace server.Checks.Ai;

public partial class HedgingLanguageCheck : IAiCheck
{
    public AiCheckType Type => AiCheckType.HedgingLanguage;

    public Task<AiCheckResult> RunAsync(string text, string? apiKey = null, string? model = null)
    {
        var words = WordRegex().Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .ToList();

        var sentences = SentenceSplitRegex().Split(text)
            .Select(s => s.Trim())
            .Where(s => s.Length > 5)
            .ToList();

        if (sentences.Count < 5 || words.Count < 40)
        {
            return Task.FromResult(new AiCheckResult
            {
                Type = Type,
                Title = "Hedging Language",
                Description = "Not enough text for analysis (need at least 5 sentences).",
                AiScore = 0
            });
        }

        var totalWords = (double)words.Count;

        // Modal verb ratio - AI overuses may/might/could/would/should to soften claims
        var modalCount = words.Count(w => ModalVerbRegex().IsMatch(w));
        var modalRate = modalCount / totalWords;

        // Concessive conjunction ratio - although/though/while/whereas/however/nevertheless
        var concessiveCount = words.Count(w => ConcessiveRegex().IsMatch(w));
        var concessiveRate = concessiveCount / totalWords;

        // Qualifying adverb ratio - words ending in -ly that soften: relatively, somewhat, generally, etc.
        var qualifierCount = words.Count(w => QualifierAdverbRegex().IsMatch(w));
        var qualifierRate = qualifierCount / totalWords;

        // Structural balancing - sentences containing "but", "however", "on the other hand" type contrast
        var balancedSentences = 0;
        foreach (var sentence in sentences)
        {
            var lower = sentence.ToLowerInvariant();
            if (ContrastStructureRegex().IsMatch(lower))
                balancedSentences++;
        }
        var balancedRate = (double)balancedSentences / sentences.Count;

        // Conditional constructions - "if", "depending", "it depends", "whether"
        var conditionalCount = words.Count(w => ConditionalRegex().IsMatch(w));
        var conditionalRate = conditionalCount / totalWords;

        var modalScore = modalRate switch
        {
            > 0.04 => 75,
            > 0.025 => 60,
            > 0.015 => 45,
            > 0.008 => 30,
            _ => 15
        };

        var concessiveScore = concessiveRate switch
        {
            > 0.020 => 75,
            > 0.012 => 60,
            > 0.006 => 45,
            > 0.003 => 30,
            _ => 15
        };

        var qualifierScore = qualifierRate switch
        {
            > 0.025 => 75,
            > 0.015 => 60,
            > 0.008 => 45,
            > 0.004 => 30,
            _ => 15
        };

        var balancedScore = balancedRate switch
        {
            > 0.50 => 80,
            > 0.35 => 65,
            > 0.20 => 50,
            > 0.10 => 30,
            _ => 15
        };

        var conditionalScore = conditionalRate switch
        {
            > 0.015 => 65,
            > 0.008 => 50,
            > 0.004 => 35,
            _ => 20
        };

        var aiScore = (int)Math.Round(
            modalScore * 0.25 +
            concessiveScore * 0.20 +
            qualifierScore * 0.20 +
            balancedScore * 0.20 +
            conditionalScore * 0.15);
        aiScore = Math.Clamp(aiScore, 0, 100);

        var details = $"Modals: {modalRate:P1}, concessive: {concessiveRate:P1}, " +
                      $"qualifiers: {qualifierRate:P1}, balanced sentences: {balancedRate:P0}, " +
                      $"conditionals: {conditionalRate:P1}. ";

        return Task.FromResult(new AiCheckResult
        {
            Type = Type,
            Title = "Hedging Language",
            Description = details +
                          (aiScore >= 60 ? "Text is excessively hedged and balanced, typical of AI."
                              : aiScore >= 40 ? "Some hedging detected, inconclusive."
                              : "Language is direct and opinionated, appears human-like."),
            AiScore = aiScore
        });
    }

    // Modal verbs that soften claims
    [GeneratedRegex(@"^(may|might|could|would|should)$")]
    private static partial Regex ModalVerbRegex();

    // Concessive conjunctions that introduce contrast/balance
    [GeneratedRegex(@"^(although|though|however|nevertheless|nonetheless|whereas|yet|conversely)$")]
    private static partial Regex ConcessiveRegex();

    // Qualifying adverbs that weaken assertions
    [GeneratedRegex(@"^(relatively|somewhat|fairly|generally|typically|usually|arguably|potentially|perhaps|possibly|largely|mostly|partly|approximately|essentially|virtually|seemingly|apparently|presumably|supposedly|ostensibly)$")]
    private static partial Regex QualifierAdverbRegex();

    // Contrast structures within a sentence
    [GeneratedRegex(@"\b(but|however|on the other hand|while .+ also|although|yet|at the same time|conversely)\b")]
    private static partial Regex ContrastStructureRegex();

    // Conditional/uncertain framing
    [GeneratedRegex(@"^(if|whether|depending|depends)$")]
    private static partial Regex ConditionalRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+(?=[A-Z])")]
    private static partial Regex SentenceSplitRegex();

    [GeneratedRegex(@"[\w']+")]
    private static partial Regex WordRegex();
}
