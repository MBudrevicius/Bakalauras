using server.Services;

namespace server.Tests.Unit.Services;

public class CrossCheckServiceTests
{
    private static (int domainCount, List<string> samples) InvokeAnalyzePageLinks(List<string> pageLinks, string originalUrl)
    {
        var method = typeof(CrossCheckService).GetMethod("AnalyzePageLinks",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return ((int, List<string>))method.Invoke(null, [pageLinks, originalUrl])!;
    }

    private static bool InvokeIsCommonNonSourceDomain(string host)
    {
        var method = typeof(CrossCheckService).GetMethod("IsCommonNonSourceDomain",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (bool)method.Invoke(null, [host])!;
    }

    private static string InvokeCleanTitle(string title)
    {
        var method = typeof(CrossCheckService).GetMethod("CleanTitle",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string)method.Invoke(null, [title])!;
    }

    [Fact]
    public void AnalyzePageLinks_EmptyList_ReturnsZero()
    {
        var (count, samples) = InvokeAnalyzePageLinks([], "https://example.com");
        Assert.Equal(0, count);
        Assert.Empty(samples);
    }

    [Fact]
    public void AnalyzePageLinks_SameDomainLinks_NotCounted()
    {
        var links = new List<string>
        {
            "https://example.com/page1",
            "https://example.com/page2",
        };
        var (count, _) = InvokeAnalyzePageLinks(links, "https://example.com");
        Assert.Equal(0, count);
    }

    [Fact]
    public void AnalyzePageLinks_ExternalLinks_Counted()
    {
        var links = new List<string>
        {
            "https://reuters.com/article",
            "https://bbc.co.uk/news",
            "https://example.com/local",
        };
        var (count, samples) = InvokeAnalyzePageLinks(links, "https://example.com");
        Assert.Equal(2, count);
        Assert.Equal(2, samples.Count);
    }

    [Fact]
    public void AnalyzePageLinks_CommonSocialMedia_Filtered()
    {
        var links = new List<string>
        {
            "https://facebook.com/share",
            "https://twitter.com/post",
            "https://youtube.com/watch",
            "https://linkedin.com/profile",
            "https://reuters.com/article",
        };
        var (count, _) = InvokeAnalyzePageLinks(links, "https://example.com");
        Assert.Equal(1, count); // Only reuters
    }

    [Fact]
    public void AnalyzePageLinks_InvalidUrls_Skipped()
    {
        var links = new List<string>
        {
            "not-a-url",
            "ftp://files.example.com/data",
            "https://valid.com/page",
        };
        var (count, _) = InvokeAnalyzePageLinks(links, "https://example.com");
        Assert.Equal(1, count); // Only valid HTTPS
    }

    [Fact]
    public void AnalyzePageLinks_DuplicateDomains_CountedOnce()
    {
        var links = new List<string>
        {
            "https://reuters.com/article1",
            "https://reuters.com/article2",
        };
        var (count, samples) = InvokeAnalyzePageLinks(links, "https://example.com");
        Assert.Equal(1, count);
        Assert.Single(samples);
    }

    [Fact]
    public void AnalyzePageLinks_MoreThan10Samples_Limited()
    {
        var links = new List<string>();
        for (var i = 0; i < 15; i++)
            links.Add($"https://domain{i}.com/page");

        var (count, samples) = InvokeAnalyzePageLinks(links, "https://example.com");
        Assert.Equal(15, count);
        Assert.Equal(10, samples.Count);
    }

    [Theory]
    [InlineData("facebook.com", true)]
    [InlineData("twitter.com", true)]
    [InlineData("x.com", true)]
    [InlineData("youtube.com", true)]
    [InlineData("google.com", true)]
    [InlineData("www.facebook.com", true)]
    [InlineData("m.youtube.com", true)]
    public void IsCommonNonSourceDomain_SocialMedia_ReturnsTrue(string host, bool expected)
    {
        Assert.Equal(expected, InvokeIsCommonNonSourceDomain(host));
    }

    [Theory]
    [InlineData("reuters.com", false)]
    [InlineData("bbc.co.uk", false)]
    [InlineData("nytimes.com", false)]
    public void IsCommonNonSourceDomain_NewsSites_ReturnsFalse(string host, bool expected)
    {
        Assert.Equal(expected, InvokeIsCommonNonSourceDomain(host));
    }

    [Fact]
    public void CleanTitle_WithSeparator_RemovesSiteName()
    {
        Assert.Equal("Breaking News Article", InvokeCleanTitle("Breaking News Article - CNN"));
    }

    [Fact]
    public void CleanTitle_WithPipeSeparator_RemovesSiteName()
    {
        Assert.Equal("Major Discovery Found", InvokeCleanTitle("Major Discovery Found | Science Daily"));
    }

    [Fact]
    public void CleanTitle_WithEnDash_RemovesSiteName()
    {
        Assert.Equal("Event Happened Today", InvokeCleanTitle("Event Happened Today \u2013 News Site"));
    }

    [Fact]
    public void CleanTitle_ShortPrefix_DoesNotRemove()
    {
        var result = InvokeCleanTitle("Short - X");
        Assert.Equal("Short - X", result);
    }

    [Fact]
    public void CleanTitle_Empty_ReturnsEmpty()
    {
        Assert.Equal("", InvokeCleanTitle(""));
    }

    [Fact]
    public void CleanTitle_NullOrWhitespace_ReturnsEmpty()
    {
        Assert.Equal("", InvokeCleanTitle("   "));
    }

    [Fact]
    public void CleanTitle_NoSeparator_ReturnsOriginal()
    {
        Assert.Equal("Simple Title Without Separator", InvokeCleanTitle("Simple Title Without Separator"));
    }
}
