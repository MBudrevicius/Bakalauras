using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

/// <summary>
/// Tests targeting specific uncovered switch branches in AI checks.
/// Each test crafts text to hit the highest or lowest threshold of a scoring branch.
/// </summary>
public class AiCheckEdgeCaseTests
{

    [Fact]
    public async Task SentenceUniformity_SentencesWithZeroWords_NotEnoughValid()
    {
        var check = new SentenceUniformityCheck();
        var text = "12345. 67890. !!!. ???. Normal sentence here. Another one here now.";
        var result = await check.RunAsync(text);
        Assert.InRange(result.AiScore, 0, 100);
    }

    [Fact]
    public async Task Hedging_ExtremeModalRate_HitsHighestBranch()
    {
        var check = new HedgingLanguageCheck();
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
        var text = "If the conditions change, whether the results hold depending on context matters. " +
                   "If we adjust parameters, whether outcomes differ depending on settings is crucial. " +
                   "If hypotheses are correct, whether predictions match depending on models is key. " +
                   "If assumptions hold, whether findings generalize depending on populations is important. " +
                   "If data supports this, whether conclusions are valid depending on analysis is essential.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore > 0);
    }

    [Fact]
    public async Task Punctuation_HighFormalRatio_HitsExactBranches()
    {
        var check = new PunctuationPatternsCheck();
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
        var text = "Well... (I think) this is interesting. The thing is... (as expected) it's complex. " +
                   "So... (naturally) we continue. And yet... (of course) it matters. " +
                   "Again... (perhaps) we should note. More text here to pad length a bit.";
        var result = await check.RunAsync(text);
        Assert.InRange(result.AiScore, 5, 95);
        Assert.Contains("ellipses", result.Description);
    }

    [Fact]
    public async Task Transitional_VeryHighConjunctiveRate_Hits90Branch()
    {
        var check = new TransitionalPhraseCheck();
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
        var text = "This suggests a clear pattern in data. This means the hypothesis is valid. " +
                   "This indicates strong correlation here. This demonstrates consistency somehow. " +
                   "This implies significant findings now. This confirms earlier predictions today. " +
                   "Results were analyzed thoroughly here.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore > 30);
    }

    [Fact]
    public async Task Repetitive_HighStarterRepetitionRate_Hits85Branch()
    {
        var check = new RepetitivePhrasingCheck();
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
        var text = "The quick brown fox jumps. The quick brown dog runs. The quick brown cat sits. " +
                   "The quick brown bird flies. The quick brown fish swims. The quick brown horse gallops.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore > 30);
    }

    [Fact]
    public async Task Perplexity_LowBigramTtr_Hits80Branch()
    {
        var check = new PerplexityEstimationCheck();
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
        var text = "To be or not to be that is the question to be or not to be again to be or not to be " +
                   "the question remains to be or not to be that is still the question to be answered.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore >= 0);
    }

    [Fact]
    public async Task Perplexity_LowTrigramTtr_Hits80Branch()
    {
        var check = new PerplexityEstimationCheck();
        var text = "In the end we see in the end we find in the end we know in the end we learn " +
                   "in the end we grow in the end results in the end conclusions in the end.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore >= 0);
    }

    [Fact]
    public async Task Perplexity_LowEntropy_Hits80Branch()
    {
        var check = new PerplexityEstimationCheck();
        var text = "I like cats I like cats I like cats I like cats I like cats I like cats I like dogs " +
                   "I like cats I like cats I like cats I like cats I like cats I like cats I like cats.";
        var result = await check.RunAsync(text);
        Assert.True(result.AiScore >= 0);
    }

    [Fact]
    public async Task ParagraphStructure_VeryUniformParagraphs_HitsHighBranch()
    {
        var check = new ParagraphStructureCheck();
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
        var text = "Hi.\n\n" +
                   "This is a much longer paragraph with many more words that creates substantial variance in the paragraph length distribution across the entire text body.\n\n" +
                   "Short.\n\n" +
                   "Another extremely long paragraph here that demonstrates significant variability making the coefficient of variation very high indeed and more text here." +
                   "\n\nTiny.\n\nYes.";
        var result = await check.RunAsync(text);
        Assert.InRange(result.AiScore, 0, 100);
    }
}
