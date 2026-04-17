using HtmlAgilityPack;
using server.Helpers;
using server.Models;

namespace server.Checks.Security;

public class MixedContentCheck : ISecurityCheck
{
    public SecurityCheckType Type => SecurityCheckType.MixedContent;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<MixedContentCheck> _logger;

    public MixedContentCheck(IHttpClientFactory httpClientFactory, ILogger<MixedContentCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<SecurityCheckResult> RunAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return CheckResult(SecurityCheckSeverity.Info, "Could not parse the URL.");
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return CheckResult(SecurityCheckSeverity.Info, "Site does not use HTTPS, mixed content check not applicable.");
        }

        if (UrlValidator.IsPrivateOrReserved(uri))
        {
            return CheckResult(SecurityCheckSeverity.Info, "URL points to a private or reserved address.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; SecureWeb/0.1)");
            var html = await client.GetStringAsync(uri);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var activeCount = 0;
            var passiveCount = 0;

            // Active mixed content (scripts, iframes, forms, objects)
            activeCount += CountHttpResources(doc, "//script[@src]", "src");
            activeCount += CountHttpResources(doc, "//iframe[@src]", "src");
            activeCount += CountHttpResources(doc, "//object[@data]", "data");
            activeCount += CountHttpResources(doc, "//embed[@src]", "src");
            activeCount += CountHttpForms(doc);

            // Passive mixed content (images, audio, video)
            passiveCount += CountHttpResources(doc, "//img[@src]", "src");
            passiveCount += CountHttpResources(doc, "//audio[@src]", "src");
            passiveCount += CountHttpResources(doc, "//video[@src]", "src");
            passiveCount += CountHttpResources(doc, "//source[@src]", "src");

            if (activeCount == 0 && passiveCount == 0)
            {
                return CheckResult(SecurityCheckSeverity.Pass, "No mixed content detected on this HTTPS page.");
            }

            var parts = new List<string>();
            if (activeCount > 0)
                parts.Add($"{activeCount} active (scripts/iframes/forms)");
            if (passiveCount > 0)
                parts.Add($"{passiveCount} passive (images/media)");

            var severity = activeCount > 0
                ? SecurityCheckSeverity.Warning
                : SecurityCheckSeverity.Info;

            return CheckResult(severity, $"Mixed content found: {string.Join(", ", parts)}.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Mixed content check failed for {Url}", url);
            return CheckResult(SecurityCheckSeverity.Info, "Could not complete mixed content check.");
        }
    }

    private static int CountHttpResources(HtmlDocument doc, string xpath, string attribute)
    {
        var nodes = doc.DocumentNode.SelectNodes(xpath);
        if (nodes is null) return 0;

        var count = 0;
        foreach (var node in nodes)
        {
            var value = node.GetAttributeValue(attribute, "");
            if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                count++;
        }
        return count;
    }

    private static int CountHttpForms(HtmlDocument doc)
    {
        var nodes = doc.DocumentNode.SelectNodes("//form[@action]");
        if (nodes is null) return 0;

        var count = 0;
        foreach (var node in nodes)
        {
            var action = node.GetAttributeValue("action", "");
            if (action.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                count++;
        }
        return count;
    }

    private SecurityCheckResult CheckResult(SecurityCheckSeverity severity, string description) => new()
    {
        Type = Type,
        Severity = severity,
        Title = "Mixed Content",
        Description = description
    };
}
