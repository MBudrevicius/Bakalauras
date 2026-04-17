using System.Text.RegularExpressions;
using HtmlAgilityPack;
using server.Helpers;
using server.Models;

namespace server.Checks.Security;

public partial class SuspiciousLinksCheck : ISecurityCheck
{
    public SecurityCheckType Type => SecurityCheckType.SuspiciousLinks;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SuspiciousLinksCheck> _logger;

    // TLDs frequently used in phishing / malware
    private static readonly HashSet<string> SuspiciousTlds = new(StringComparer.OrdinalIgnoreCase)
    {
        ".tk", ".ml", ".ga", ".cf", ".gq", ".top", ".xyz", ".buzz", ".club", ".work", ".click", ".loan", ".download", ".racing", ".win", ".bid", ".stream", ".gdn", ".icu", ".monster"
    };

    public SuspiciousLinksCheck(IHttpClientFactory httpClientFactory, ILogger<SuspiciousLinksCheck> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<SecurityCheckResult> RunAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var pageUri))
        {
            return CheckResult(SecurityCheckSeverity.Info, "Could not parse the URL.");
        }

        if (UrlValidator.IsPrivateOrReserved(pageUri))
        {
            return CheckResult(SecurityCheckSeverity.Info, "URL points to a private or reserved address.");
        }

        string html;
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; SecureWeb/0.1)");
            html = await client.GetStringAsync(pageUri);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch page for suspicious links check: {Url}", url);
            return CheckResult(SecurityCheckSeverity.Info, $"Could not fetch page: {ex.Message}");
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var warnings = new List<string>();
        var hiddenCount = 0;
        var suspiciousTldCount = 0;
        var mismatchCount = 0;
        var externalLinks = 0;

        // Find all anchor tags and elements with href/src
        var linkNodes = doc.DocumentNode.SelectNodes("//a[@href] | //link[@href] | //script[@src] | //iframe[@src]");
        if (linkNodes is null)
        {
            return CheckResult(SecurityCheckSeverity.Pass, "No links found on the page.");
        }

        var pageDomain = pageUri.Host.ToLowerInvariant();

        foreach (var node in linkNodes)
        {
            var href = node.GetAttributeValue("href", "") ?? node.GetAttributeValue("src", "");

            if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#') || href.StartsWith("javascript:"))
                continue;

            if (!Uri.TryCreate(pageUri, href, out var linkUri))
                continue;

            var linkHost = linkUri.Host.ToLowerInvariant();

            if (!linkHost.Equals(pageDomain, StringComparison.Ordinal) && !linkHost.EndsWith("." + pageDomain, StringComparison.Ordinal))
            {
                externalLinks++;
            }

            // Hidden links: display:none, visibility:hidden, opacity:0, tiny size
            if (IsHiddenElement(node))
            {
                hiddenCount++;
            }

            // Links to suspicious TLDs
            if (HasSuspiciousTld(linkHost))
            {
                suspiciousTldCount++;
            }

            // Mismatched anchor text vs actual URL
            if (node.Name == "a")
            {
                var displayText = node.InnerText?.Trim() ?? "";
                if (LooksLikeUrl(displayText) &&
                    Uri.TryCreate(displayText, UriKind.Absolute, out var displayUri) &&
                    !displayUri.Host.Equals(linkUri.Host, StringComparison.OrdinalIgnoreCase))
                {
                    mismatchCount++;
                }
            }
        }

        if (hiddenCount > 0)
            warnings.Add($"{hiddenCount} hidden link(s) detected");

        if (suspiciousTldCount > 0)
            warnings.Add($"{suspiciousTldCount} link(s) to suspicious TLDs");

        if (mismatchCount > 0)
            warnings.Add($"{mismatchCount} link(s) with mismatched display text (shows one URL, leads to another)");

        if (externalLinks > 20)
            warnings.Add($"High number of external links ({externalLinks})");

        if (warnings.Count == 0)
        {
            return CheckResult(SecurityCheckSeverity.Pass, $"No suspicious links detected ({linkNodes.Count} links analyzed).");
        }

        var severity = (hiddenCount > 0 || mismatchCount > 0)
            ? SecurityCheckSeverity.Warning
            : SecurityCheckSeverity.Info;

        return CheckResult(severity, $"{string.Join("; ", warnings)} (out of {linkNodes.Count} links).");
    }

    private static bool IsHiddenElement(HtmlNode node)
    {
        var style = node.GetAttributeValue("style", "").ToLowerInvariant();
        if (string.IsNullOrEmpty(style))
            return false;

        return style.Contains("display:none") || style.Contains("display: none")
            || style.Contains("visibility:hidden") || style.Contains("visibility: hidden")
            || style.Contains("opacity:0") || style.Contains("opacity: 0")
            || StyleHasTinySize(style);
    }

    private static bool StyleHasTinySize(string style)
    {
        return TinySizeRegex().IsMatch(style);
    }

    private static bool HasSuspiciousTld(string host)
    {
        foreach (var tld in SuspiciousTlds)
        {
            if (host.EndsWith(tld, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool LooksLikeUrl(string text)
    {
        return text.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("www.", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"(font-size|width|height)\s*:\s*0\s*(px|em|rem|%)?", RegexOptions.IgnoreCase)]
    private static partial Regex TinySizeRegex();

    private SecurityCheckResult CheckResult(SecurityCheckSeverity severity, string description) => new()
    {
        Type = Type,
        Severity = severity,
        Title = "Suspicious Links",
        Description = description
    };
}
