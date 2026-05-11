using server.Checks.Security;
using HtmlAgilityPack;

namespace server.Tests.Unit.Checks.Security;

public class MixedContentCheckTests
{
    private static int InvokeCountHttpResources(HtmlDocument doc, string xpath, string attribute)
    {
        var method = typeof(MixedContentCheck).GetMethod("CountHttpResources",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (int)method.Invoke(null, [doc, xpath, attribute])!;
    }

    private static int InvokeCountHttpForms(HtmlDocument doc)
    {
        var method = typeof(MixedContentCheck).GetMethod("CountHttpForms",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (int)method.Invoke(null, [doc])!;
    }

    // CountHttpResources tests
    [Fact]
    public void CountHttpResources_HttpScripts_Counted()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><script src='http://evil.com/script.js'></script><script src='https://safe.com/script.js'></script></body></html>");
        Assert.Equal(1, InvokeCountHttpResources(doc, "//script[@src]", "src"));
    }

    [Fact]
    public void CountHttpResources_NoNodes_Returns0()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p>No scripts</p></body></html>");
        Assert.Equal(0, InvokeCountHttpResources(doc, "//script[@src]", "src"));
    }

    [Fact]
    public void CountHttpResources_AllHttps_Returns0()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><img src='https://safe.com/img.png'/><img src='https://safe2.com/img.png'/></body></html>");
        Assert.Equal(0, InvokeCountHttpResources(doc, "//img[@src]", "src"));
    }

    [Fact]
    public void CountHttpResources_MultipleHttp_CountsAll()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><img src='http://a.com/1.png'/><img src='http://b.com/2.png'/><img src='http://c.com/3.png'/></body></html>");
        Assert.Equal(3, InvokeCountHttpResources(doc, "//img[@src]", "src"));
    }

    [Fact]
    public void CountHttpResources_Iframes_Counted()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><iframe src='http://embed.com/widget'></iframe></body></html>");
        Assert.Equal(1, InvokeCountHttpResources(doc, "//iframe[@src]", "src"));
    }

    // CountHttpForms tests
    [Fact]
    public void CountHttpForms_HttpAction_Counted()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><form action='http://evil.com/submit'></form></body></html>");
        Assert.Equal(1, InvokeCountHttpForms(doc));
    }

    [Fact]
    public void CountHttpForms_HttpsAction_NotCounted()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><form action='https://safe.com/submit'></form></body></html>");
        Assert.Equal(0, InvokeCountHttpForms(doc));
    }

    [Fact]
    public void CountHttpForms_NoForms_Returns0()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><p>No forms</p></body></html>");
        Assert.Equal(0, InvokeCountHttpForms(doc));
    }

    [Fact]
    public void CountHttpForms_MultipleMixed_CountsOnlyHttp()
    {
        var doc = new HtmlDocument();
        doc.LoadHtml("<html><body><form action='http://a.com'></form><form action='https://b.com'></form><form action='http://c.com'></form></body></html>");
        Assert.Equal(2, InvokeCountHttpForms(doc));
    }
}
