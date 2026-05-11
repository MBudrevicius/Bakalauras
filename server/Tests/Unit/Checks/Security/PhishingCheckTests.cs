using server.Checks.Security;
using server.Models;

namespace server.Tests.Unit.Checks.Security;

public class PhishingCheckTests
{
    private readonly PhishingCheck _check;

    public PhishingCheckTests()
    {
        var settings = new PhishingSettings
        {
            TargetedBrands = ["google", "facebook", "paypal", "amazon", "microsoft", "apple"]
        };
        _check = new PhishingCheck(settings);
    }

    [Fact]
    public async Task RunAsync_CleanUrl_ReturnsPass()
    {
        var result = await _check.RunAsync("https://google.com/search?q=test");

        Assert.Equal(SecurityCheckSeverity.Pass, result.Severity);
        Assert.Contains("No phishing", result.Description);
    }

    [Fact]
    public async Task RunAsync_InvalidUrl_ReturnsInfo()
    {
        var result = await _check.RunAsync("not-a-valid-url");

        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
    }

    [Fact]
    public async Task RunAsync_IpAddress_ReturnsWarning()
    {
        var result = await _check.RunAsync("http://192.168.1.1/login");

        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("IP address", result.Description);
    }

    [Fact]
    public async Task RunAsync_AtSymbol_ReturnsWarning()
    {
        var result = await _check.RunAsync("http://user@evil.com/login");

        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("@", result.Description);
    }

    [Fact]
    public async Task RunAsync_Punycode_ReturnsWarning()
    {
        var result = await _check.RunAsync("https://xn--80ak6aa92e.com/login");

        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("punycode", result.Description);
    }

    [Fact]
    public async Task RunAsync_ExcessiveSubdomains_DetectsWarning()
    {
        var result = await _check.RunAsync("https://a.b.c.d.example.com/login");

        Assert.Contains("subdomains", result.Description);
    }

    [Fact]
    public async Task RunAsync_LongUrl_DetectsWarning()
    {
        var longPath = new string('a', 200);
        var result = await _check.RunAsync($"https://example.com/{longPath}");

        Assert.Contains("long", result.Description);
    }

    [Fact]
    public async Task RunAsync_Typosquatting_DetectsLookalike()
    {
        // "g00gle" looks like "google" via leet speak
        var result = await _check.RunAsync("https://g00gle.com/login");

        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("similar", result.Description);
    }

    [Fact]
    public async Task RunAsync_TyposquattingCloseSpelling_DetectsLookalike()
    {
        // "gooogle" is edit distance 1 from "google"
        var result = await _check.RunAsync("https://gooogle.com/login");

        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
        Assert.Contains("google", result.Description);
    }

    [Fact]
    public async Task RunAsync_ReturnsCorrectType()
    {
        var result = await _check.RunAsync("https://example.com");

        Assert.Equal(SecurityCheckType.Phishing, result.Type);
    }

    [Fact]
    public async Task RunAsync_ReturnsCorrectTitle()
    {
        var result = await _check.RunAsync("https://example.com");

        Assert.Equal("Phishing", result.Title);
    }

    [Fact]
    public async Task RunAsync_EmptyBrands_StillWorks()
    {
        var check = new PhishingCheck(new PhishingSettings { TargetedBrands = [] });
        var result = await check.RunAsync("https://example.com");

        Assert.Equal(SecurityCheckSeverity.Pass, result.Severity);
    }

    [Fact]
    public async Task RunAsync_ExactBrandMatch_ReturnsPass()
    {
        // Exact brand match should NOT be flagged as typosquatting
        var result = await _check.RunAsync("https://google.com");

        Assert.Equal(SecurityCheckSeverity.Pass, result.Severity);
    }

    [Fact]
    public async Task RunAsync_DoubleProtocol_DetectsSuspicious()
    {
        // URL with embedded protocol after the initial https://
        var result = await _check.RunAsync("https://example.com/redirect?url=http://evil.com");
        Assert.Contains("protocol", result.Description);
    }

    [Fact]
    public async Task RunAsync_LeetSpeakBrand_DetectsTyposquatting()
    {
        // "fac3book" → normalizes to "facebook" which is close to "facebook"
        var result = await _check.RunAsync("https://fac3book.com/login");
        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
    }

    [Fact]
    public async Task RunAsync_MultipleSeverities_WarningIfMultiple()
    {
        // Long URL + excessive subdomains → 2+ warnings → Warning severity
        var longPath = new string('x', 200);
        var result = await _check.RunAsync($"https://a.b.c.d.evil.com/{longPath}");
        Assert.Equal(SecurityCheckSeverity.Warning, result.Severity);
    }

    [Fact]
    public async Task RunAsync_SingleInfoWarning_ReturnsInfo()
    {
        // Only excessive subdomains detected → single warning, no highSeverity → Info
        var result = await _check.RunAsync("https://a.b.c.d.example.com/");
        // subdomains only → Info level (not highSeverity, count=1)
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
    }
}
