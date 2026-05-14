using server.Services;

namespace server.Tests.Unit.Services;

public class PageScoreStoreTests
{
    private static string InvokeExtractDomain(string url)
    {
        var method = typeof(PageScoreStore).GetMethod("ExtractDomain",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string)method.Invoke(null, [url])!;
    }

    private static int InvokeCalculatePageWithRelatedPagesScore(int ownScore, IEnumerable<int> relatedScores)
    {
        var method = typeof(PageScoreStore).GetMethod("CalculatePageWithRelatedPagesScore",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (int)method.Invoke(null, [ownScore, relatedScores])!;
    }

    [Theory]
    [InlineData("https://www.example.com/page", "www.example.com")]
    [InlineData("http://example.com", "example.com")]
    [InlineData("https://sub.example.com:8080/path?query=1", "sub.example.com")]
    public void ExtractDomain_ValidUrl_ReturnsDomain(string url, string expected)
    {
        Assert.Equal(expected, InvokeExtractDomain(url));
    }

    [Fact]
    public void ExtractDomain_InvalidUrl_ReturnsLowercasedInput()
    {
        Assert.Equal("not-a-url", InvokeExtractDomain("not-a-url"));
    }

    [Fact]
    public void ExtractDomain_UpperCase_ReturnsLowercase()
    {
        Assert.Equal("www.example.com", InvokeExtractDomain("https://WWW.EXAMPLE.COM"));
    }

    [Fact]
    public void ExtractDomain_WithTrailingSpaces_Trimmed()
    {
        Assert.Equal("example.com", InvokeExtractDomain("  https://example.com  "));
    }

    [Fact]
    public void CalculateScore_NoRelated_ReturnsOwnScore()
    {
        Assert.Equal(80, InvokeCalculatePageWithRelatedPagesScore(80, []));
    }

    [Fact]
    public void CalculateScore_WithRelated_Blends90_10()
    {
        var score = InvokeCalculatePageWithRelatedPagesScore(80, [60, 60]);
        Assert.Equal(78, score);
    }

    [Fact]
    public void CalculateScore_HighRelated_IncreasesSlightly()
    {
        var score = InvokeCalculatePageWithRelatedPagesScore(50, [100, 100]);
        Assert.Equal(55, score);
    }

    [Fact]
    public void CalculateScore_LowRelated_DecreasesSlightly()
    {
        var score = InvokeCalculatePageWithRelatedPagesScore(80, [0, 0]);
        Assert.Equal(72, score);
    }

    [Fact]
    public void CalculateScore_SingleRelated_CorrectBlend()
    {
        var score = InvokeCalculatePageWithRelatedPagesScore(100, [50]);
        Assert.Equal(95, score);
    }
}
