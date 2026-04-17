using System.Text.RegularExpressions;
using server.Models;

namespace server.Checks.Ai;

public partial class TransitionalPhraseCheck : IAiCheck
{
    public AiCheckType Type => AiCheckType.TransitionalPhrases;

    public Task<AiCheckResult> RunAsync(string text, string? apiKey = null, string? model = null)
    {
        var sentences = SentenceSplitRegex().Split(text)
            .Select(s => s.Trim())
            .Where(s => s.Length > 5)
            .ToList();

        if (sentences.Count < 5)
        {
            return Task.FromResult(new AiCheckResult
            {
                Type = Type,
                Title = "Transitional Phrases",
                Description = "Not enough text for analysis (need at least 5 sentences).",
                AiScore = 0
            });
        }

        var sentenceOpeners = sentences
            .Select(s => s.ToLowerInvariant().TrimStart())
            .ToList();

        // Sentences starting with a conjunctive adverb (furthermore, moreover, consequently, etc.)
        var conjunctiveStarters = sentenceOpeners.Count(s => ConjunctiveAdverbStartRegex().IsMatch(s));
        var conjunctiveRate = (double)conjunctiveStarters / sentences.Count;

        // "It is/it's + adj + to" filler constructions ("It is important to note", "It's worth mentioning", etc.)
        var lowerText = text.ToLowerInvariant();
        var itIsPatternCount = ItIsAdjectiveToRegex().Matches(lowerText).Count;
        var itIsRate = (double)itIsPatternCount / sentences.Count;

        // "This/That + verb" sentence starters ("This means", "This suggests", "That indicates")
        var demonstrativeStarters = sentenceOpeners.Count(s => DemonstrativeVerbStartRegex().IsMatch(s));
        var demonstrativeRate = (double)demonstrativeStarters / sentences.Count;

        // Prepositional phrase sentence openers ("In conclusion", "As a result", "At the end of")
        var prepositionalStarters = sentenceOpeners.Count(s => PrepositionalOpenerRegex().IsMatch(s));
        var prepositionalRate = (double)prepositionalStarters / sentences.Count;

        // Overall formulaic opener rate - what % of sentences start with any transitional structure
        var formulaicCount = conjunctiveStarters + demonstrativeStarters + prepositionalStarters + itIsPatternCount;
        var formulaicRate = (double)formulaicCount / sentences.Count;

        // Score each signal
        var conjunctiveScore = conjunctiveRate switch
        {
            > 0.35 => 90,
            > 0.25 => 75,
            > 0.15 => 60,
            > 0.08 => 40,
            _ => 15
        };

        var itIsScore = itIsRate switch
        {
            > 0.15 => 90,
            > 0.08 => 75,
            > 0.04 => 55,
            > 0.01 => 35,
            _ => 10
        };

        var demonstrativeScore = demonstrativeRate switch
        {
            > 0.25 => 85,
            > 0.15 => 70,
            > 0.08 => 50,
            > 0.03 => 30,
            _ => 10
        };

        var formulaicScore = formulaicRate switch
        {
            > 0.50 => 90,
            > 0.35 => 75,
            > 0.20 => 55,
            > 0.10 => 35,
            _ => 15
        };

        var aiScore = (int)Math.Round(
            conjunctiveScore * 0.30 +
            itIsScore * 0.20 +
            demonstrativeScore * 0.20 +
            formulaicScore * 0.30);
        aiScore = Math.Clamp(aiScore, 0, 100);

        var details = $"Conjunctive adverb openers: {conjunctiveRate:P0}, " +
                      $"\"it is/it's...to\" patterns: {itIsPatternCount}, " +
                      $"demonstrative starters: {demonstrativeRate:P0}, " +
                      $"total formulaic openers: {formulaicRate:P0}. ";

        return Task.FromResult(new AiCheckResult
        {
            Type = Type,
            Title = "Transitional Phrases",
            Description = details +
                          (aiScore >= 60 ? "Heavy use of formulaic transitions, typical of AI."
                              : aiScore >= 40 ? "Some formulaic phrasing detected."
                              : "Transitional language appears natural."),
            AiScore = aiScore
        });
    }

    // Sentences starting with conjunctive adverbs (closed word class, not specific phrases)
    [GeneratedRegex(@"^(furthermore|moreover|additionally|consequently|nevertheless|nonetheless|hence|therefore|thus|meanwhile|subsequently|likewise|similarly|conversely|alternatively|accordingly|regardless|otherwise)\b")]
    private static partial Regex ConjunctiveAdverbStartRegex();

    // "It is/it's + adj + to" filler pattern - only match evaluative adjectives, not generic usage
    [GeneratedRegex(@"\bit(?:'s| is| was)\s+(?:important|worth|essential|crucial|necessary|noteworthy|notable|significant|vital|critical|interesting|relevant|useful|helpful|key)\s+(?:to |that )", RegexOptions.IgnoreCase)]
    private static partial Regex ItIsAdjectiveToRegex();

    // "This/That + verb" demonstrative sentence starters
    [GeneratedRegex(@"^(this|that|these|those)\s+(means?|suggests?|indicates?|demonstrates?|shows?|implies?|highlights?|ensures?|provides?|creates?|leads?|allows?|enables?|makes?|results?)\b")]
    private static partial Regex DemonstrativeVerbStartRegex();

    // Prepositional phrase openers ("In summary", "As a result", "On the whole", "At this point")
    [GeneratedRegex(@"^(in|on|at|as|for|by|from|with|to)\s+(a |an |the |this |that |any |some |general|summary|conclusion|addition|contrast|particular|other|order)\b")]
    private static partial Regex PrepositionalOpenerRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+(?=[A-Z])")]
    private static partial Regex SentenceSplitRegex();
}
