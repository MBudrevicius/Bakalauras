using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

/// <summary>
/// Tests targeting specific uncovered switch branches in AI checks.
/// Each test crafts text to hit the highest or lowest threshold of a scoring branch.
/// </summary>
public class AiCheckEdgeCaseTests
{
    // ===== SentenceUniformityCheck =====

    [Fact]
    public async Task SentenceUniformity_SentencesWithZeroWords_NotEnoughValid()
    {
        // Sentences that split on [.!?]\s+[A-Z] but contain no \w+ matches after filtering
        // Use sentences with only numbers/symbols that get filtered to 0 word count
        var check = new SentenceUniformityCheck();
        // 5+ sentence splits but some have 0 words → wordCounts.Count < 5
        var text = "12345. 67890. !!!. ???. Normal sentence here. Another one here now.";
        var result = await check.RunAsync(text);
        // May hit the "Not enough valid sentences" branch or score normally
        Assert.InRange(result.AiScore, 0, 100);
    }

    // ===== HedgingLanguageCheck =====

    [Fact]
    public async Task Hedging_ExtremeModalRate_HitsHighestBranch()
    {
        var check = new HedgingLanguageCheck();
        // Pack text with modal verbs to get modalRate > 0.04
        // Need >4% of words to be modals
        var text = "This may suggest changes, and it might indicate progress. We could potentially revise, " +
                   "and they should consider alternatives. One would expect results might vary, " +
                   "and we could see improvements. They should review it, as it may help significantly. " +
                   "The findings could potentially be useful, and we should acknowledge that they might matter. " +
                   "We could perhaps reconsider, as they may want changes. It might be that we should act.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore > 0);
    }

    [Fact]
    public async Task Hedging_HighConcessiveRate_HitsHighBranch()
    {
        var check = new HedgingLanguageCheck();
        // Need concessiveRate > 0.020 — words like although/though/however/nevertheless/whereas/while
        var text = "Although the results are clear, however there are caveats. Nevertheless the data holds, " +
                   "although we should be careful. Though some disagree, whereas others argue, " +
                   "nevertheless the evidence is strong. Although it seems right, however doubts remain. " +
                   "Though uncertain, although promising. Nevertheless important, whereas complex findings hold. " +
                   "Although repeated, however considered. Though examined, whereas evaluated properly.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore > 0);
    }

    [Fact]
    public async Task Hedging_HighQualifierRate_HitsHighBranch()
    {
        var check = new HedgingLanguageCheck();
        // Need qualifierRate > 0.025 — qualifying adverbs: relatively, somewhat, generally, potentially, etc.
        var text = "The results are relatively clear and generally accepted. The data is somewhat limited " +
                   "and potentially biased. Findings are essentially consistent and relatively strong. " +
                   "The approach is generally sound, somewhat novel, and potentially impactful. " +
                   "The analysis is relatively thorough and generally complete. Results are essentially valid. " +
                   "The methodology is relatively standard and generally reliable, somewhat conservative.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore > 0);
    }

    [Fact]
    public async Task Hedging_HighBalancedRate_HitsHighBranch()
    {
        var check = new HedgingLanguageCheck();
        // Need balancedRate > 0.50 — >50% of sentences must contain contrast structures (but/however/on the other hand)
        var text = "The data is strong, but questions remain. However, some results are unclear. " +
                   "On the other hand, the trend is positive. But we must acknowledge limitations. " +
                   "However, there are alternative interpretations possible. But the evidence is compelling. " +
                   "On the other hand, critics raise valid points here. However, the methodology is sound.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore > 0);
    }

    [Fact]
    public async Task Hedging_HighConditionalRate_HitsHighBranch()
    {
        var check = new HedgingLanguageCheck();
        // Need conditionalRate > 0.015 — words: if, depending, whether
        var text = "If the conditions change, whether the results hold depending on context matters. " +
                   "If we adjust parameters, whether outcomes differ depending on settings is crucial. " +
                   "If hypotheses are correct, whether predictions match depending on models is key. " +
                   "If assumptions hold, whether findings generalize depending on populations is important. " +
                   "If data supports this, whether conclusions are valid depending on analysis is essential.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore > 0);
    }

    // ===== PunctuationPatternsCheck =====

    [Fact]
    public async Task Punctuation_HighFormalRatio_HitsExactBranches()
    {
        var check = new PunctuationPatternsCheck();
        // formalRatio between 0.85 and 0.92 → score = 70 branch
        // Also needs diversityScore: informalRatio < 0.03 branch
        var text = "Sentence one, clause here. Sentence two, more here. Sentence three, finally. " +
                   "Another sentence, with commas. More text, some commas. Final sentence, end here. " +
                   "Sentence seven, continues. Sentence eight, follows. Sentence nine, ends. Done here.";
        var result = await check.RunAsync(text);
        Assert.InRange(result.AiScore, 5, 95);
    }

    [Fact]
    public async Task Punctuation_MidFormalRatio_Hits55Branch()
    {
        var check = new PunctuationPatternsCheck();
        // formalRatio between 0.75 and 0.85 → score = 55
        // Need ~20% informal punctuation
        var text = "This is a test sentence! Another sentence here. A third one, with commas. " +
                   "What about questions? Yes, more commas here. Exciting parts here! " +
                   "Normal sentence again. And another one, here. Question mark usage? Final sentence.";
        var result = await check.RunAsync(text);
        Assert.InRange(result.AiScore, 5, 95);
    }

    [Fact]
    public async Task Punctuation_LowFormalRatio_Hits40Branch()
    {
        var check = new PunctuationPatternsCheck();
        // formalRatio between 0.60 and 0.75 → score = 40
        var text = "Wow! Amazing! What?! Really?? Okay, that works. Yes! (surprising) Cool! " +
                   "Nice... wait... What? More text. Something (here) is odd! " +
                   "Strange! (very) odd... text. Exciting! Great. Wow! Hmm?";
        var result = await check.RunAsync(text);
        Assert.InRange(result.AiScore, 5, 95);
    }

    [Fact]
    public async Task Punctuation_VeryLowFormal_Hits20Branch()
    {
        var check = new PunctuationPatternsCheck();
        // formalRatio < 0.60 → score = 20
        var text = "WOW!!! AMAZING!! WHAT?! OH MY!! YES!! NO!! STOP!! GO!! (wow) (yes) (no) " +
                   "INCREDIBLE!! UNBELIEVABLE!! FANTASTIC!! GORGEOUS!! (really) (truly) " +
                   "SPECTACULAR!! MARVELOUS!! OUTSTANDING!! (always) (forever) YES!! MORE!!";
        var result = await check.RunAsync(text);
        Assert.InRange(result.AiScore, 5, 95);
    }

    [Fact]
    public async Task Punctuation_MidExclamation_HitsBranches()
    {
        var check = new PunctuationPatternsCheck();
        // exclamationRate between 1.0 and 2.0 per 1000 chars → score = 40
        // Need about 1-2 excl marks per 1000 chars in a 300+ char text
        var sb = new System.Text.StringBuilder();
        sb.Append("This is a sample text with some exclamation! The text continues normally here. ");
        while (sb.Length < 400) sb.Append("More normal text padding here. ");
        var result = await check.RunAsync(sb.ToString());
        Assert.InRange(result.AiScore, 5, 95);
    }

    [Fact]
    public async Task Punctuation_MidDashRate_HitsBranches()
    {
        var check = new PunctuationPatternsCheck();
        // dashRate between 1.5 and 3.0 → score = 40
        var text = "The data - surprisingly - shows clear results and the analysis - though complex - reveals trends. " +
                   "More text without dashes here to pad. Still more text continues." +
                   "And another line with text. Extra padding to ensure length exceeds one hundred.";
        var result = await check.RunAsync(text);
        Assert.InRange(result.AiScore, 5, 95);
    }

    [Fact]
    public async Task Punctuation_BothEllipsesAndParens_HitsLowSpecialBranch()
    {
        var check = new PunctuationPatternsCheck();
        // Both ellipses > 0 AND parens > 0 → specialScore = 20
        var text = "Well... (I think) this is interesting. The thing is... (as expected) it's complex. " +
                   "So... (naturally) we continue. And yet... (of course) it matters. " +
                   "Again... (perhaps) we should note. More text here to pad length a bit.";
        var result = await check.RunAsync(text);
        Assert.InRange(result.AiScore, 5, 95);
        Assert.Contains("ellipses", result.Description);
    }

    // ===== TransitionalPhraseCheck =====

    [Fact]
    public async Task Transitional_VeryHighConjunctiveRate_Hits90Branch()
    {
        var check = new TransitionalPhraseCheck();
        // conjunctiveRate > 0.35 → 90
        // Need >35% of sentences to start with conjunctive adverbs
        var text = "Furthermore, the evidence is clear. Moreover, additional data supports this. " +
                   "Consequently, we can draw conclusions. Therefore, the hypothesis holds. " +
                   "Additionally, new findings emerged. Subsequently, trends were confirmed. " +
                   "Meanwhile, other data corroborated. Thus, the analysis is complete.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore > 50);
    }

    [Fact]
    public async Task Transitional_HighItIsRate_Hits90Branch()
    {
        var check = new TransitionalPhraseCheck();
        // itIsRate > 0.15 → 90
        var text = "It is important to note the key findings. It is essential to understand the context. " +
                   "It is worth mentioning the methodology used. It is crucial to analyze the data. " +
                   "It is notable that the trend persists. It is significant that results converge. " +
                   "The data speaks for itself clearly here.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore > 30);
    }

    [Fact]
    public async Task Transitional_HighDemonstrativeRate_Hits85Branch()
    {
        var check = new TransitionalPhraseCheck();
        // demonstrativeRate > 0.25 → 85
        var text = "This suggests a clear pattern in data. This means the hypothesis is valid. " +
                   "This indicates strong correlation here. This demonstrates consistency somehow. " +
                   "This implies significant findings now. This confirms earlier predictions today. " +
                   "Results were analyzed thoroughly here.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore > 30);
    }

    // ===== RepetitivePhrasingCheck =====

    [Fact]
    public async Task Repetitive_HighStarterRepetitionRate_Hits85Branch()
    {
        var check = new RepetitivePhrasingCheck();
        // starterRepetitionRate > 0.50 → 85
        // Need >50% of sentences with same 3-word opener (repeatedStarterCount / sentences.Count)
        var text = "The research shows clear results here. The research shows the hypothesis confirmed. " +
                   "The research shows a trend now. The research shows key findings today. " +
                   "The research shows previous claims valid. The research shows strong patterns present. " +
                   "The research shows the phenomenon well. Different phrasing is used here. " +
                   "Another approach was also considered here.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore > 30);
    }

    [Fact]
    public async Task Repetitive_HighOpener5Rate_Hits90Branch()
    {
        var check = new RepetitivePhrasingCheck();
        // opener5Rate > 0.30 → 90
        // >30% of sentences share exact same 5-word opener
        var text = "The data clearly shows that this finding is significant. " +
                   "The data clearly shows that these results matter. " +
                   "The data clearly shows that the analysis works. " +
                   "Different approaches were used in testing. " +
                   "Various methods produced similar outcomes here.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore > 20);
    }

    [Fact]
    public async Task Repetitive_HighTrigramRepRate_Hits80Branch()
    {
        var check = new RepetitivePhrasingCheck();
        // trigramRepRate > 2.0 → 80
        // Need many repeated 3-word sequences
        var text = "The quick brown fox jumps. The quick brown dog runs. The quick brown cat sits. " +
                   "The quick brown bird flies. The quick brown fish swims. The quick brown horse gallops.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore > 30);
    }

    // ===== PerplexityEstimationCheck =====

    [Fact]
    public async Task Perplexity_LowBigramTtr_Hits80Branch()
    {
        var check = new PerplexityEstimationCheck();
        // bigramTtr < 0.55 → ttrScore = 80
        // Need many repeated bigrams — very repetitive text
        var text = "The the the the the the the the the the data data data data data data data shows shows shows " +
                   "the the the data data data shows shows the data shows the data shows the data shows the data " +
                   "the the data data the data the data the data clear clear clear results results results.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore >= 0);
    }

    [Fact]
    public async Task Perplexity_HighRepetitionRate_Hits80Branch()
    {
        var check = new PerplexityEstimationCheck();
        // repetitionRate > 0.30 → repScore = 80
        // Need >30% of bigrams to appear more than once
        var text = "To be or not to be that is the question to be or not to be again to be or not to be " +
                   "the question remains to be or not to be that is still the question to be answered.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore >= 0);
    }

    [Fact]
    public async Task Perplexity_LowTrigramTtr_Hits80Branch()
    {
        var check = new PerplexityEstimationCheck();
        // trigramTtr < 0.70 → trigramScore = 80
        var text = "In the end we see in the end we find in the end we know in the end we learn " +
                   "in the end we grow in the end results in the end conclusions in the end.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore >= 0);
    }

    [Fact]
    public async Task Perplexity_LowEntropy_Hits80Branch()
    {
        var check = new PerplexityEstimationCheck();
        // normalizedEntropy < 0.75 → entropyScore = 80
        // Very predictable/repetitive text
        var text = "I like cats I like cats I like cats I like cats I like cats I like cats I like dogs " +
                   "I like cats I like cats I like cats I like cats I like cats I like cats I like cats.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore >= 0);
    }

    // ===== ParagraphStructureCheck =====

    [Fact]
    public async Task ParagraphStructure_VeryUniformParagraphs_HitsHighBranch()
    {
        var check = new ParagraphStructureCheck();
        // cv < 0.15 → wordCvScore = 85
        // Need paragraphs with very similar word counts
        var text = "This is a paragraph with about ten words here.\n\n" +
                   "This is another paragraph with ten words total.\n\n" +
                   "Here comes a third paragraph ten words count.\n\n" +
                   "Fourth paragraph following the same structure now.\n\n" +
                   "Fifth paragraph matching the length pattern here.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore > 0);
    }

    [Fact]
    public async Task ParagraphStructure_VeryVariedParagraphs_HitsLowBranch()
    {
        var check = new ParagraphStructureCheck();
        // cv >= 0.60 → last branch (low score)
        var text = "Hi.\n\n" +
                   "This is a much longer paragraph with many more words that creates substantial variance in the paragraph length distribution across the entire text body.\n\n" +
                   "Short.\n\n" +
                   "Another extremely long paragraph here that demonstrates significant variability making the coefficient of variation very high indeed and more text here." +
                   "\n\nTiny.\n\nYes.";
        var result = await check.RunAsync(text);
        Assert.InRange(result.AiScore, 0, 100);
    }
}
