using server.Helpers;
using server.Tests.Unit.Helpers;

namespace server.Tests.Unit.Helpers;

public class HtmlTextExtractorAsyncTests
{
    private static HtmlTextExtractor CreateExtractor(MockHttpMessageHandler handler)
    {
        var factory = new MockHttpClientFactory(handler);
        return new HtmlTextExtractor(factory);
    }

    // ExtractTextFromUrl tests
    [Fact]
    public async Task ExtractTextFromUrl_ValidHtml_ReturnsText()
    {
        var html = "<html><body><p>Hello World</p><script>var x = 1;</script></body></html>";
        var extractor = CreateExtractor(new MockHttpMessageHandler(html));
        var result = await extractor.ExtractTextFromUrl("https://example.com");
        Assert.Contains("Hello World", result);
        Assert.DoesNotContain("var x", result);
    }

    [Fact]
    public async Task ExtractTextFromUrl_HttpError_ReturnsEmpty()
    {
        var extractor = CreateExtractor(new MockHttpMessageHandler("", System.Net.HttpStatusCode.InternalServerError));
        var result = await extractor.ExtractTextFromUrl("https://example.com");
        Assert.Equal("", result);
    }

    // ExtractLinksFromUrl tests
    [Fact]
    public async Task ExtractLinksFromUrl_ValidHtml_ReturnsExternalLinks()
    {
        var html = """
        <html><body>
            <a href="https://external.com/page">External</a>
            <a href="https://example.com/internal">Internal</a>
            <a href="https://other.com/article">Other</a>
        </body></html>
        """;
        var extractor = CreateExtractor(new MockHttpMessageHandler(html));
        var links = await extractor.ExtractLinksFromUrl("https://example.com");

        Assert.Equal(2, links.Count);
        Assert.Contains(links, l => l.Contains("external.com"));
        Assert.Contains(links, l => l.Contains("other.com"));
    }

    [Fact]
    public async Task ExtractLinksFromUrl_NoAnchors_ReturnsEmpty()
    {
        var html = "<html><body><p>No links here</p></body></html>";
        var extractor = CreateExtractor(new MockHttpMessageHandler(html));
        var links = await extractor.ExtractLinksFromUrl("https://example.com");
        Assert.Empty(links);
    }

    [Fact]
    public async Task ExtractLinksFromUrl_RelativeLinks_Resolved()
    {
        var html = """<html><body><a href="/page">Relative</a></body></html>""";
        var extractor = CreateExtractor(new MockHttpMessageHandler(html));
        var links = await extractor.ExtractLinksFromUrl("https://example.com");
        // Relative link to same domain → filtered out
        Assert.Empty(links);
    }

    [Fact]
    public async Task ExtractLinksFromUrl_HttpError_ReturnsEmpty()
    {
        var extractor = CreateExtractor(new MockHttpMessageHandler("", System.Net.HttpStatusCode.InternalServerError));
        var links = await extractor.ExtractLinksFromUrl("https://example.com");
        Assert.Empty(links);
    }

    [Fact]
    public async Task ExtractLinksFromUrl_NonHttpSchemes_Filtered()
    {
        var html = """
        <html><body>
            <a href="javascript:void(0)">JS</a>
            <a href="mailto:test@example.com">Email</a>
            <a href="https://valid.com">Valid</a>
        </body></html>
        """;
        var extractor = CreateExtractor(new MockHttpMessageHandler(html));
        var links = await extractor.ExtractLinksFromUrl("https://example.com");
        Assert.Single(links);
    }

    [Fact]
    public async Task ExtractLinksFromUrl_DuplicateLinks_Deduplicated()
    {
        var html = """
        <html><body>
            <a href="https://external.com/page">Link 1</a>
            <a href="https://external.com/page">Link 2</a>
        </body></html>
        """;
        var extractor = CreateExtractor(new MockHttpMessageHandler(html));
        var links = await extractor.ExtractLinksFromUrl("https://example.com");
        Assert.Single(links);
    }
}
