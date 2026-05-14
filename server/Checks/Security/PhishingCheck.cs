using System.Net;
using System.Text.RegularExpressions;
using server.Models;

namespace server.Checks.Security;

public partial class PhishingCheck : ISecurityCheck
{
    private readonly string[] _targetedBrands;

    public PhishingCheck(PhishingSettings phishingSettings)
    {
        _targetedBrands = phishingSettings.TargetedBrands.Length > 0
            ? phishingSettings.TargetedBrands
            : [];
    }

    public SecurityCheckType Type => SecurityCheckType.Phishing;

    public Task<SecurityCheckResult> RunAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Task.FromResult(CheckResult(SecurityCheckSeverity.Info, "Could not parse the URL."));
        }

        var warnings = new List<string>();
        var highSeverity = false;
        var host = uri.Host.ToLowerInvariant();
        var fullUrl = url.ToLowerInvariant();

        if (IPAddress.TryParse(host, out _))
        {
            warnings.Add("URL uses an IP address instead of a domain name");
            highSeverity = true;
        }

        if (uri.UserInfo.Length > 0)
        {
            warnings.Add("URL contains a '@' symbol which can mask the real destination");
            highSeverity = true;
        }

        if (host.Contains("xn--"))
        {
            warnings.Add("Domain uses punycode (internationalized characters), possible homograph attack");
            highSeverity = true;
        }

        if (host.Count(c => c == '.') > 3)
            warnings.Add("Domain has an unusually high number of subdomains");

        if (url.Length > 200)
            warnings.Add("URL is unusually long");

        var domainParts = host.Split('.');
        var registrableDomain = domainParts.Length >= 2
            ? domainParts[^2]
            : host;

        var (matchedBrand, distance) = FindClosestBrand(registrableDomain);
        if (matchedBrand is not null && distance > 0)
        {
            warnings.Add($"Domain '{registrableDomain}' looks similar to '{matchedBrand}' (edit distance {distance}, possible typosquatting)");
            highSeverity = true;
        }

        if (DoubleProtocolRegex().IsMatch(fullUrl[8..]))
            warnings.Add("URL contains a suspicious embedded protocol (possible redirect attack)");

        if (warnings.Count == 0)
        {
            return Task.FromResult(new SecurityCheckResult
            {
                Type = Type,
                Severity = SecurityCheckSeverity.Pass,
                Title = "Phishing",
                Description = "No phishing indicators detected."
            });
        }

        var severity = (highSeverity || warnings.Count >= 2)
            ? SecurityCheckSeverity.Warning
            : SecurityCheckSeverity.Info;

        return Task.FromResult(new SecurityCheckResult
        {
            Type = Type,
            Severity = severity,
            Title = "Phishing",
            Description = $"Detected {warnings.Count} indicator(s): {string.Join("; ", warnings)}."
        });
    }

    [GeneratedRegex(@"https?://", RegexOptions.IgnoreCase)]
    private static partial Regex DoubleProtocolRegex();

    private SecurityCheckResult CheckResult(SecurityCheckSeverity severity, string description) => new()
    {
        Type = Type,
        Severity = severity,
        Title = "Phishing",
        Description = description
    };

    private (string? Brand, int Distance) FindClosestBrand(string domain)
    {
        var normalized = NormalizeLeetSpeak(domain);

        string? bestBrand = null;
        var bestDistance = int.MaxValue;

        foreach (var brand in _targetedBrands)
        {
            if (domain == brand)
                return (null, 0);

            if (normalized == brand)
                return (brand, 1);

            var dist = LevenshteinDistance(normalized, brand);

            var rawDist = LevenshteinDistance(domain, brand);
            dist = Math.Min(dist, rawDist);

            var threshold = Math.Max(1, brand.Length / 3);
            if (dist > 0 && dist <= threshold && dist < bestDistance)
            {
                bestDistance = dist;
                bestBrand = brand;
            }
        }

        return (bestBrand, bestDistance);
    }

    private static string NormalizeLeetSpeak(string input)
    {
        var chars = input.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                '0' => 'o',
                '1' => 'i',
                '3' => 'e',
                '4' => 'a',
                '5' => 's',
                '7' => 't',
                '8' => 'b',
                '9' => 'g',
                '$' => 's',
                '@' => 'a',
                '!' => 'i',
                _ => chars[i]
            };
        }
        return new string(chars);
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];

        for (var i = 0; i <= n; i++) d[i, 0] = i;
        for (var j = 0; j <= m; d[0, j] = j++) { }

        for (var i = 1; i <= n; i++)
        {
            for (var j = 1; j <= m; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}
