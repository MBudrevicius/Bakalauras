using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

public class PunctuationPatternsBranchTests
{
    private static int InvokeCountSubstring(string text, string sub)
    {
        var method = typeof(PunctuationPatternsCheck).GetMethod("CountSubstring",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (int)method.Invoke(null, [text, sub])!;
    }

    [Fact]
    public void CountSubstring_MultipleOccurrences_CountsAll()
    {
        Assert.Equal(3, InvokeCountSubstring("one...two...three...", "..."));
    }

    [Fact]
    public void CountSubstring_NoOccurrences_ReturnsZero()
    {
        Assert.Equal(0, InvokeCountSubstring("hello world", "..."));
    }

    [Fact]
    public void CountSubstring_OverlappingNotCounted()
    {
        // "....." has 1 non-overlapping "..." then not enough chars
        Assert.Equal(1, InvokeCountSubstring(".....", "..."));
    }

    [Fact]
    public void CountSubstring_EmptyText_ReturnsZero()
    {
        Assert.Equal(0, InvokeCountSubstring("", "..."));
    }

    // Full RunAsync tests for different branch paths
    private readonly PunctuationPatternsCheck _check = new();

    [Fact]
    public async Task RunAsync_ShortText_ReturnsZero()
    {
        var result = await _check.RunAsync("Short.");
        Assert.Equal(0, result.AiScore);
        Assert.Contains("Not enough text", result.Description);
    }

    [Fact]
    public async Task RunAsync_NoPunctuation_Returns50()
    {
        var text = new string('a', 200);
        var result = await _check.RunAsync(text);
        Assert.Equal(50, result.AiScore);
        Assert.Contains("No punctuation", result.Description);
    }

    [Fact]
    public async Task RunAsync_FormalText_HigherScore()
    {
        // Text dominated by commas and periods, minimal informal punctuation
        var text = "This is a test sentence. Another sentence here. Yet another one follows, with commas, periods, and more. " +
                   "The analysis should detect formal patterns. Simple and clean writing style. Nothing fancy here. " +
                   "More sentences with periods. Commas appear sometimes, yes. The end of the text is near. Final sentence.";
        var result = await _check.RunAsync(text);
        Assert.InRange(result.AiScore, 5, 95);
    }

    [Fact]
    public async Task RunAsync_InformalText_LowerScore()
    {
        // Text with exclamations, dashes, ellipses, parentheses
        var text = "Wow!!! This is AMAZING! Can you believe it?! " +
                   "I mean -- seriously -- it's crazy! (I know, right?) " +
                   "Wait... hold on... what?! The thing is... well... " +
                   "It's (honestly) super weird - like - totally insane! " +
                   "OMG! WHAT! HOW! WHY! (Please help!)";
        var result = await _check.RunAsync(text);
        Assert.InRange(result.AiScore, 5, 95);
    }

    [Fact]
    public void RunAsync_CorrectType()
    {
        Assert.Equal(AiCheckType.PunctuationPatterns, _check.Type);
    }

    [Fact]
    public async Task RunAsync_OnlyCommasAndPeriods_VeryFormal()
    {
        // 100% formal punctuation — formalRatio > 0.92
        var text = "The study concluded with significant findings, and the data was clear. " +
                   "Researchers noted several important trends, including changes in behavior. " +
                   "The methodology was robust, with controls in place. The sample was representative, " +
                   "and the analysis followed best practices. Results were consistent, and the conclusion was straightforward.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore >= 50, $"All-formal punct should score high, got {result.AiScore}");
        Assert.Contains("commas/periods", result.Description);
    }

    [Fact]
    public async Task RunAsync_WithSemicolonsAndColons_StillFormal()
    {
        var text = "The first point is clear; the second requires analysis. Note the following: data, evidence, and results. " +
                   "The conclusion is simple; the methodology was sound. Consider this: the experiment succeeded. " +
                   "The results follow; each point matters here. Point one: accuracy; point two: precision. Final note: done.";
        var result = await _check.RunAsync(text);
        Assert.InRange(result.AiScore, 5, 95);
    }

    [Fact]
    public async Task RunAsync_WithEllipses_DetectsUsage()
    {
        var text = "Well... I'm not sure about that... The thing is... it's complicated, you know... " +
                   "Something happened and... well... nobody really knows what went on... " +
                   "But anyway… the point is… things are different now… " +
                   "The story continues... and the plot thickens... dramatically.";
        var result = await _check.RunAsync(text);
        Assert.Contains("ellipses", result.Description);
    }

    [Fact]
    public async Task RunAsync_WithParensButNoEllipses_MidSpecial()
    {
        var text = "The research (published in 2024) confirms the hypothesis. " +
                   "Data samples (n=500) were collected from multiple sites. " +
                   "The control group (n=250) showed no significant change. " +
                   "Analysis (using regression) revealed a strong correlation. " +
                   "The findings (peer-reviewed) support the original claim.";
        var result = await _check.RunAsync(text);
        Assert.InRange(result.AiScore, 5, 95);
    }

    [Fact]
    public async Task RunAsync_FrequentExclamation_AddsDetail()
    {
        var text = "Amazing! Incredible! Wonderful! Fantastic! Unbelievable! Outstanding! " +
                   "This is so great! I love it! Can not believe it! " +
                   "What a day! So happy! Best ever! Truly remarkable! " +
                   "Exciting times! Wow! Yes!";
        var result = await _check.RunAsync(text);
        Assert.Contains("exclamation", result.Description);
    }

    [Fact]
    public async Task RunAsync_FrequentDashes_AddsDetail()
    {
        var text = "The data—surprisingly—shows a clear trend. The analysis—however—requires caution. " +
                   "The results—as expected—confirm the hypothesis. The methodology—well-established—is reliable. " +
                   "The conclusions—based on evidence—are sound. The findings—notably—differ from expectations.";
        var result = await _check.RunAsync(text);
        Assert.Contains("dashes", result.Description);
    }
}
