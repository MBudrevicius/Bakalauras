using System.Globalization;
using System.Text.RegularExpressions;
using server.Models;
using Whois;

namespace server.Checks.Security;

public partial class DomainAgeCheck : ISecurityCheck
{
    public SecurityCheckType Type => SecurityCheckType.DomainAge;

    private readonly ILogger<DomainAgeCheck> _logger;

    public DomainAgeCheck(ILogger<DomainAgeCheck> logger)
    {
        _logger = logger;
    }

    public async Task<SecurityCheckResult> RunAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return CheckResult(SecurityCheckSeverity.Info, "Could not parse the URL.");
        }

        var host = uri.Host;
        var domain = ExtractRegistrableDomain(host);

        try
        {
            var lookup = new WhoisLookup();
            var response = await lookup.LookupAsync(domain);
            var raw = response.Content;

            if (string.IsNullOrWhiteSpace(raw))
            {
                return CheckResult(SecurityCheckSeverity.Info, "WHOIS returned empty response.");
            }

            var creationDate = ParseCreationDate(raw);

            if (creationDate is null)
            {
                return CheckResult(SecurityCheckSeverity.Info, "Could not determine domain creation date from WHOIS data.");
            }

            var age = DateTime.UtcNow - creationDate.Value;

            if (age.TotalDays < 30)
            {
                return CheckResult(SecurityCheckSeverity.Warning, $"Domain is very new (registered {creationDate.Value:yyyy-MM-dd}, {(int)age.TotalDays} days ago). This is a common phishing indicator.");
            }

            if (age.TotalDays < 180)
            {
                return CheckResult(SecurityCheckSeverity.Info, $"Domain is relatively new (registered {creationDate.Value:yyyy-MM-dd}, {(int)age.TotalDays} days ago).");
            }

            return CheckResult(SecurityCheckSeverity.Pass, $"Domain registered on {creationDate.Value:yyyy-MM-dd} ({(int)(age.TotalDays / 365)} years ago).");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WHOIS domain age check failed for {Domain}", domain);
            return CheckResult(SecurityCheckSeverity.Info, "WHOIS domain age check failed.");
        }
    }

    private static DateTime? ParseCreationDate(string whoisText)
    {
        var match = CreationDateRegex().Match(whoisText);
        if (!match.Success)
            return null;

        var dateStr = match.Groups[1].Value.Trim();

        string[] formats =
        [
            "yyyy-MM-dd",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.fZ",
            "yyyy-MM-ddTHH:mm:sszzz",
            "dd-MMM-yyyy",
            "yyyy/MM/dd",
            "dd/MM/yyyy",
            "MM/dd/yyyy",
            "yyyy.MM.dd",
            "dd.MM.yyyy"
        ];

        if (DateTime.TryParseExact(dateStr, formats, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return parsed;
        }

        if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var fallback))
        {
            return fallback;
        }

        return null;
    }

    private static string ExtractRegistrableDomain(string host)
    {
        var lastDot = host.LastIndexOf('.');
        if (lastDot <= 0) return host;

        var secondLastDot = host.LastIndexOf('.', lastDot - 1);
        if (secondLastDot < 0) return host;

        return host[(secondLastDot + 1)..];
    }

    [GeneratedRegex(@"(?:Creation Date|Created|created|Registration Date|Registered on|reg-date)[:\s]+(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex CreationDateRegex();

    private SecurityCheckResult CheckResult(SecurityCheckSeverity severity, string description) => new()
    {
        Type = Type,
        Severity = severity,
        Title = "Domain Age",
        Description = description
    };
}
