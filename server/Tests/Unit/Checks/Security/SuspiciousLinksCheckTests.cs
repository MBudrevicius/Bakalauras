using server.Checks.Security;
using HtmlAgilityPack;

namespace server.Tests.Unit.Checks.Security;

public class SuspiciousLinksCheckTests
{
    private static bool InvokeIsHiddenElement(HtmlNode node)
    {
        var method = typeof(SuspiciousLinksCheck).GetMethod("IsHiddenElement",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (bool)method.Invoke(null, [node])!;
    }

    private static bool InvokeHasSuspiciousTld(string host)
    {
        var method = typeof(SuspiciousLinksCheck).GetMethod("HasSuspiciousTld",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (bool)method.Invoke(null, [host])!;
    }

    private static bool InvokeLooksLikeUrl(string text)
    {
        var method = typeof(SuspiciousLinksCheck).GetMethod("LooksLikeUrl",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (bool)method.Invoke(null, [text])!;
    }

    // IsHiddenElement tests
    [Theory]
    [InlineData("display:none", true)]
    [InlineData("display: none", true)]
    [InlineData("visibility:hidden", true)]
    [InlineData("visibility: hidden", true)]
    [InlineData("opacity:0", true)]
    [InlineData("opacity: 0", true)]
    public void IsHiddenElement_HidingStyles_ReturnsTrue(string style, bool expected)
    {
        var doc = new HtmlDocument();
        var node = doc.CreateElement("a");
        node.SetAttributeValue("style", style);
        Assert.Equal(expected, InvokeIsHiddenElement(node));
    }

    [Fact]
    public void IsHiddenElement_TinyFontSize_ReturnsTrue()
    {
        var doc = new HtmlDocument();
        var node = doc.CreateElement("a");
        node.SetAttributeValue("style", "font-size: 0px");
        Assert.True(InvokeIsHiddenElement(node));
    }

    [Fact]
    public void IsHiddenElement_TinyWidth_ReturnsTrue()
    {
        var doc = new HtmlDocument();
        var node = doc.CreateElement("a");
        node.SetAttributeValue("style", "width: 0em");
        Assert.True(InvokeIsHiddenElement(node));
    }

    [Fact]
    public void IsHiddenElement_NoStyle_ReturnsFalse()
    {
        var doc = new HtmlDocument();
        var node = doc.CreateElement("a");
        Assert.False(InvokeIsHiddenElement(node));
    }

    [Fact]
    public void IsHiddenElement_NormalStyle_ReturnsFalse()
    {
        var doc = new HtmlDocument();
        var node = doc.CreateElement("a");
        node.SetAttributeValue("style", "color: blue; font-size: 14px");
        Assert.False(InvokeIsHiddenElement(node));
    }

    // HasSuspiciousTld tests
    [Theory]
    [InlineData("evil.tk", true)]
    [InlineData("phishing.ml", true)]
    [InlineData("scam.xyz", true)]
    [InlineData("bad.buzz", true)]
    [InlineData("hack.click", true)]
    [InlineData("malware.download", true)]
    [InlineData("virus.icu", true)]
    [InlineData("site.monster", true)]
    public void HasSuspiciousTld_SuspiciousTlds_ReturnsTrue(string host, bool expected)
    {
        Assert.Equal(expected, InvokeHasSuspiciousTld(host));
    }

    [Theory]
    [InlineData("google.com", false)]
    [InlineData("example.org", false)]
    [InlineData("university.edu", false)]
    [InlineData("government.gov", false)]
    public void HasSuspiciousTld_SafeTlds_ReturnsFalse(string host, bool expected)
    {
        Assert.Equal(expected, InvokeHasSuspiciousTld(host));
    }

    // LooksLikeUrl tests
    [Theory]
    [InlineData("http://example.com", true)]
    [InlineData("https://example.com", true)]
    [InlineData("www.example.com", true)]
    [InlineData("HTTP://EXAMPLE.COM", true)]
    public void LooksLikeUrl_ValidPrefixes_ReturnsTrue(string text, bool expected)
    {
        Assert.Equal(expected, InvokeLooksLikeUrl(text));
    }

    [Theory]
    [InlineData("Click here", false)]
    [InlineData("example.com", false)]
    [InlineData("ftp://files.example.com", false)]
    public void LooksLikeUrl_NonUrlText_ReturnsFalse(string text, bool expected)
    {
        Assert.Equal(expected, InvokeLooksLikeUrl(text));
    }
}
