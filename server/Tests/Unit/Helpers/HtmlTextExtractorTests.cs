using Moq;
using server.Helpers;

namespace server.Tests.Unit.Helpers;

public class HtmlTextExtractorTests
{
    [Fact]
    public void ExtractTextFromHtml_RemovesScriptAndStyleTags()
    {
        var html = "<html><body><script>alert('hi')</script><style>.x{}</style><p>Hello World</p></body></html>";

        // Use reflection to test the private static method
        var method = typeof(HtmlTextExtractor).GetMethod("ExtractTextFromHtml",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (string)method!.Invoke(null, [html])!;

        Assert.Contains("Hello World", result);
        Assert.DoesNotContain("alert", result);
        Assert.DoesNotContain(".x{}", result);
    }

    [Fact]
    public void ExtractTextFromHtml_DecodesHtmlEntities()
    {
        var html = "<html><body><p>Tom &amp; Jerry &lt;3</p></body></html>";

        var method = typeof(HtmlTextExtractor).GetMethod("ExtractTextFromHtml",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (string)method!.Invoke(null, [html])!;

        Assert.Contains("Tom & Jerry <3", result);
    }

    [Fact]
    public void ExtractTextFromHtml_CollapsesWhitespace()
    {
        var html = "<html><body><p>Hello    \n\n\t   World</p></body></html>";

        var method = typeof(HtmlTextExtractor).GetMethod("ExtractTextFromHtml",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (string)method!.Invoke(null, [html])!;

        Assert.DoesNotContain("  ", result);
    }

    [Fact]
    public void ExtractTextFromHtml_EmptyHtml_ReturnsEmpty()
    {
        var html = "<html><body></body></html>";

        var method = typeof(HtmlTextExtractor).GetMethod("ExtractTextFromHtml",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (string)method!.Invoke(null, [html])!;

        Assert.Equal("", result);
    }

    [Fact]
    public void ExtractTextFromHtml_RemovesIframeAndSvg()
    {
        var html = "<html><body><iframe>hidden</iframe><svg><text>icon</text></svg><p>Visible</p></body></html>";

        var method = typeof(HtmlTextExtractor).GetMethod("ExtractTextFromHtml",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (string)method!.Invoke(null, [html])!;

        Assert.Contains("Visible", result);
        Assert.DoesNotContain("hidden", result);
    }
}
