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

    // ExtractDomain tests
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

    // CalculatePageWithRelatedPagesScore tests
    [Fact]
    public void CalculateScore_NoRelated_ReturnsOwnScore()
    {
        Assert.Equal(80, InvokeCalculatePageWithRelatedPagesScore(80, []));
    }

    [Fact]
    public void CalculateScore_WithRelated_Blends90_10()
    {
        // Own=80, Related avg=60
        // 80*0.9 + 60*0.1 = 72 + 6 = 78
        var score = InvokeCalculatePageWithRelatedPagesScore(80, [60, 60]);
        Assert.Equal(78, score);
    }

    [Fact]
    public void CalculateScore_HighRelated_IncreasesSlightly()
    {
        // Own=50, Related avg=100
        // 50*0.9 + 100*0.1 = 45 + 10 = 55
        var score = InvokeCalculatePageWithRelatedPagesScore(50, [100, 100]);
        Assert.Equal(55, score);
    }

    [Fact]
    public void CalculateScore_LowRelated_DecreasesSlightly()
    {
        // Own=80, Related avg=0
        // 80*0.9 + 0*0.1 = 72
        var score = InvokeCalculatePageWithRelatedPagesScore(80, [0, 0]);
        Assert.Equal(72, score);
    }

    [Fact]
    public void CalculateScore_SingleRelated_CorrectBlend()
    {
        // Own=100, Related=50
        // 100*0.9 + 50*0.1 = 90 + 5 = 95
        var score = InvokeCalculatePageWithRelatedPagesScore(100, [50]);
        Assert.Equal(95, score);
    }
}
