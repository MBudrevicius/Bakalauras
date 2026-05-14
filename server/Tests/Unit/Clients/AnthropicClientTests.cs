using server.Clients;
using server.Models;

namespace server.Tests.Unit.Clients;

public class AnthropicClientTests
{
    private static int InvokeParseScore(string reply)
    {
        var method = typeof(AnthropicClient).GetMethod("ParseScore",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (int)method.Invoke(null, [reply])!;
    }

    private static int[] InvokeParseSegmentScores(string reply, int count)
    {
        var method = typeof(AnthropicClient).GetMethod("ParseSegmentScores",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (int[])method.Invoke(null, [reply, count])!;
    }

    private static CredibilityResult InvokeParseCredibilityResult(string reply)
    {
        var method = typeof(AnthropicClient).GetMethod("ParseCredibilityResult",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (CredibilityResult)method.Invoke(null, [reply])!;
    }

    [Theory]
    [InlineData("75", 75)]
    [InlineData("0", 0)]
    [InlineData("100", 100)]
    [InlineData("  42  ", 42)]
    public void ParseScore_ValidInteger_ReturnsParsedValue(string reply, int expected)
    {
        Assert.Equal(expected, InvokeParseScore(reply));
    }

    [Fact]
    public void ParseScore_NegativeValue_ClampsTo0()
    {
        Assert.Equal(0, InvokeParseScore("-5"));
    }

    [Fact]
    public void ParseScore_Over100_ClampsTo100()
    {
        Assert.Equal(100, InvokeParseScore("150"));
    }

    [Theory]
    [InlineData("The score is 85", 85)]
    [InlineData("I estimate 60%", 60)]
    [InlineData("About 30 percent", 30)]
    public void ParseScore_TextWithNumber_ExtractsFirst(string reply, int expected)
    {
        Assert.Equal(expected, InvokeParseScore(reply));
    }

    [Fact]
    public void ParseScore_NoNumber_Throws()
    {
        Assert.Throws<System.Reflection.TargetInvocationException>(
            () => InvokeParseScore("no numbers here"));
    }

    [Fact]
    public void ParseSegmentScores_ValidFormat_ParsesAll()
    {
        var reply = "[0] 75\n[1] 40\n[2] 90";
        var scores = InvokeParseSegmentScores(reply, 3);

        Assert.Equal(3, scores.Length);
        Assert.Equal(75, scores[0]);
        Assert.Equal(40, scores[1]);
        Assert.Equal(90, scores[2]);
    }

    [Fact]
    public void ParseSegmentScores_MissingIndex_Defaults0()
    {
        var reply = "[0] 60\n[2] 80";
        var scores = InvokeParseSegmentScores(reply, 3);

        Assert.Equal(60, scores[0]);
        Assert.Equal(0, scores[1]); // missing
        Assert.Equal(80, scores[2]);
    }

    [Fact]
    public void ParseSegmentScores_OutOfRangeIndex_Ignored()
    {
        var reply = "[0] 50\n[99] 80";
        var scores = InvokeParseSegmentScores(reply, 2);

        Assert.Equal(50, scores[0]);
        Assert.Equal(0, scores[1]);
    }

    [Fact]
    public void ParseSegmentScores_ClampsValues()
    {
        var reply = "[0] -10\n[1] 200";
        var scores = InvokeParseSegmentScores(reply, 2);

        Assert.Equal(0, scores[0]);
        Assert.Equal(100, scores[1]);
    }

    [Fact]
    public void ParseSegmentScores_EmptyReply_ReturnsZeros()
    {
        var scores = InvokeParseSegmentScores("", 3);
        Assert.All(scores, s => Assert.Equal(0, s));
    }

    [Fact]
    public void ParseSegmentScores_ExtraText_ParsesCorrectly()
    {
        var reply = "Here are the scores:\n[0] 55\n[1] 70\nDone.";
        var scores = InvokeParseSegmentScores(reply, 2);

        Assert.Equal(55, scores[0]);
        Assert.Equal(70, scores[1]);
    }

    [Fact]
    public void ParseCredibilityResult_ValidResponse_ParsesAll()
    {
        var reply = """
            SCORE: 75
            VERDICT: Mostly Supported
            CLAIMS:
            - GDP grew by 3%: Supported - matches official data
            - Unemployment dropped: Misleading - only in one sector
            - New policy passed: Supported - confirmed by congress records
            """;

        var result = InvokeParseCredibilityResult(reply);

        Assert.Equal(75, result.Score);
        Assert.Equal("Mostly Supported", result.Verdict);
        Assert.Equal(3, result.Claims.Count);
        Assert.Contains("GDP grew by 3%", result.Claims[0]);
    }

    [Fact]
    public void ParseCredibilityResult_ScoreClamped_To100()
    {
        var reply = "SCORE: 150\nVERDICT: Supported";
        var result = InvokeParseCredibilityResult(reply);
        Assert.Equal(100, result.Score);
    }

    [Fact]
    public void ParseCredibilityResult_ScoreClamped_To0()
    {
        var reply = "SCORE: -20\nVERDICT: Unsupported";
        var result = InvokeParseCredibilityResult(reply);
        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void ParseCredibilityResult_NoClaims_EmptyList()
    {
        var reply = "SCORE: 50\nVERDICT: Mixed";
        var result = InvokeParseCredibilityResult(reply);

        Assert.Equal(50, result.Score);
        Assert.Equal("Mixed", result.Verdict);
        Assert.Empty(result.Claims);
    }

    [Fact]
    public void ParseCredibilityResult_EmptyReply_DefaultValues()
    {
        var result = InvokeParseCredibilityResult("");
        Assert.Equal(0, result.Score);
        Assert.Empty(result.Claims);
    }

    [Fact]
    public void ParseCredibilityResult_InvalidScoreText_KeepsDefault()
    {
        var reply = "SCORE: not-a-number\nVERDICT: Unknown";
        var result = InvokeParseCredibilityResult(reply);
        Assert.Equal(0, result.Score);
        Assert.Equal("Unknown", result.Verdict);
    }
}
