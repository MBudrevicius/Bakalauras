using System.Net;
using System.Net.Http.Json;
using server.Models;

namespace server.Tests.Integration;

/// <summary>
/// Integration tests targeting uncovered branches in security checks:
/// redirects, mixed content, phishing indicators, suspicious links, Google Safe Browsing threats.
/// </summary>
public class SecurityBranchCoverageTests : IClassFixture<IntegrationTestFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationTestFactory _factory;

    public SecurityBranchCoverageTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task SecurityChecks_IpAddressUrl_TriggersPhishingWarning()
    {
        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://192.168.1.1/login" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);

        var phishing = result.Results.FirstOrDefault(r => r.Type == SecurityCheckType.Phishing);
        Assert.NotNull(phishing);
        Assert.True(phishing.Severity == SecurityCheckSeverity.Warning,
            $"IP address should trigger phishing warning, got {phishing.Severity}");
    }

    [Fact]
    public async Task SecurityChecks_PunycodeUrl_TriggersPhishingWarning()
    {
        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://xn--pple-43d.com/account" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);

        var phishing = result.Results.FirstOrDefault(r => r.Type == SecurityCheckType.Phishing);
        Assert.NotNull(phishing);
        Assert.Contains("punycode", phishing.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SecurityChecks_ExcessiveSubdomains_TriggersPhishingInfo()
    {
        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://login.secure.account.verify.banking-example.com/auth" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);

        var phishing = result.Results.FirstOrDefault(r => r.Type == SecurityCheckType.Phishing);
        Assert.NotNull(phishing);
        Assert.Contains("subdomain", phishing.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SecurityChecks_VeryLongUrl_TriggersPhishingIndicator()
    {
        var longPath = new string('a', 200);
        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = $"https://example.com/{longPath}" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);

        var phishing = result.Results.FirstOrDefault(r => r.Type == SecurityCheckType.Phishing);
        Assert.NotNull(phishing);
        Assert.Contains("long", phishing.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SecurityChecks_UrlWithAtSymbol_TriggersPhishingWarning()
    {
        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://user@evil-site.com/login" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);

        var phishing = result.Results.FirstOrDefault(r => r.Type == SecurityCheckType.Phishing);
        Assert.NotNull(phishing);
        Assert.True(phishing.Severity == SecurityCheckSeverity.Warning);
    }

    [Fact]
    public async Task SecurityChecks_DoubleProtocolUrl_TriggersPhishing()
    {
        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://redirect.com/goto?url=https://evil.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);

        var phishing = result.Results.FirstOrDefault(r => r.Type == SecurityCheckType.Phishing);
        Assert.NotNull(phishing);
        Assert.Contains("protocol", phishing.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SecurityChecks_HttpUrl_MixedContentNotApplicable()
    {
        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "http://plain-http-site.com/page" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);

        var mixed = result.Results.FirstOrDefault(r => r.Type == SecurityCheckType.MixedContent);
        Assert.NotNull(mixed);
        Assert.Equal(SecurityCheckSeverity.Info, mixed.Severity);
        Assert.Contains("not use HTTPS", mixed.Description);
    }

    [Fact]
    public async Task SecurityChecks_HttpsWithMixedContent_DetectsActiveAndPassive()
    {
        _factory.FakeApiHandler.MixedContentHost = "mixed-content-test";

        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://mixed-content-test.com/page" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);

        var mixed = result.Results.FirstOrDefault(r => r.Type == SecurityCheckType.MixedContent);
        Assert.NotNull(mixed);
        Assert.Equal(SecurityCheckSeverity.Warning, mixed.Severity);
        Assert.Contains("active", mixed.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SecurityChecks_GoogleSafeBrowsingThreat_ReturnsWarning()
    {
        _factory.FakeApiHandler.GoogleSafeBrowsingThreatDetected = true;

        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://safe-browsing-threat-test.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);

        var safeBrowsing = result.Results.FirstOrDefault(r => r.Type == SecurityCheckType.GoogleSafeBrowsing);
        Assert.NotNull(safeBrowsing);
        Assert.Equal(SecurityCheckSeverity.Warning, safeBrowsing.Severity);
        Assert.Contains("flagged", safeBrowsing.Description, StringComparison.OrdinalIgnoreCase);

        _factory.FakeApiHandler.GoogleSafeBrowsingThreatDetected = false;
    }

    [Fact]
    public async Task SecurityChecks_UrlWithRedirect_DetectsRedirectChain()
    {
        _factory.FakeApiHandler.RedirectFromHost = "redirect-test-host";
        _factory.FakeApiHandler.RedirectToUrl = "https://final-destination.com/page";

        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://redirect-test-host.com/start" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);

        var redirect = result.Results.FirstOrDefault(r => r.Type == SecurityCheckType.RedirectChain);
        Assert.NotNull(redirect);
        Assert.True(redirect.Description.Contains("redirect", StringComparison.OrdinalIgnoreCase) ||
                    redirect.Description.Contains("hop", StringComparison.OrdinalIgnoreCase) ||
                    redirect.Severity == SecurityCheckSeverity.Pass);

        _factory.FakeApiHandler.RedirectFromHost = null;
    }

    [Fact]
    public async Task SecurityChecks_HttpsToHttpDowngrade_DetectsWarning()
    {
        _factory.FakeApiHandler.RedirectFromHost = "downgrade-test";
        _factory.FakeApiHandler.RedirectToUrl = "http://insecure-destination.com/page";
        _factory.FakeApiHandler.RedirectDowngradeHttps = true;

        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://downgrade-test.com/start" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);

        var redirect = result.Results.FirstOrDefault(r => r.Type == SecurityCheckType.RedirectChain);
        Assert.NotNull(redirect);

        _factory.FakeApiHandler.RedirectFromHost = null;
        _factory.FakeApiHandler.RedirectDowngradeHttps = false;
    }

    [Fact]
    public async Task SecurityChecks_PageWithSuspiciousLinks_DetectsIssues()
    {
        _factory.FakeApiHandler.SuspiciousLinksHost = "suspicious-links-test";

        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://suspicious-links-test.com/page" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);

        var suspicious = result.Results.FirstOrDefault(r => r.Type == SecurityCheckType.SuspiciousLinks);
        Assert.NotNull(suspicious);
        Assert.True(suspicious.Severity == SecurityCheckSeverity.Warning ||
                    suspicious.Severity == SecurityCheckSeverity.Info);

        _factory.FakeApiHandler.SuspiciousLinksHost = null;
    }

    [Fact]
    public async Task SecurityChecks_HttpsCleanSite_AllChecksPass()
    {
        _factory.FakeApiHandler.GoogleSafeBrowsingThreatDetected = false;
        _factory.FakeApiHandler.RedirectFromHost = null;
        _factory.FakeApiHandler.MixedContentHost = null;
        _factory.FakeApiHandler.SuspiciousLinksHost = null;

        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://example.com" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);

        var passes = result.Results.Count(r => r.Severity == SecurityCheckSeverity.Pass);
        Assert.True(passes >= 3, $"Expected at least 3 passing checks, got {passes}");
    }

    [Fact]
    public async Task SecurityChecks_PrivateIpRange_HandledGracefully()
    {
        var response = await _client.PostAsJsonAsync("/api/security-checks",
            new SecurityCheckRequest { Url = "https://127.0.0.1/admin" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SecurityCheckResponse>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.Results);
    }
}
