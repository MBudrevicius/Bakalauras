using server.Helpers;
using server.Models;

namespace server.Checks.Security;

public class SecurityHeadersCheck : ISecurityCheck
{
    public SecurityCheckType Type => SecurityCheckType.SecurityHeaders;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SecurityHeadersCheck> _logger;

    private static readonly string[] ImportantHeaders =
    [
        "Strict-Transport-Security",
        "Content-Security-Policy",
        "X-Content-Type-Options",
        "X-Frame-Options",
        "Referrer-Policy",
        "Permissions-Policy"
    ];

    public SecurityHeadersCheck(IHttpClientFactory httpClientFactory, ILogger<SecurityHeadersCheck> logger)
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

        if (UrlValidator.IsPrivateOrReserved(uri))
        {
            return CheckResult(SecurityCheckSeverity.Info, "URL points to a private or reserved address.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            using var request = new HttpRequestMessage(HttpMethod.Head, uri);
            using var response = await client.SendAsync(request);

            var missing = new List<string>();
            var present = new List<string>();

            foreach (var header in ImportantHeaders)
            {
                if (response.Headers.Contains(header) || response.Content.Headers.Contains(header))
                {
                    present.Add(header);
                }
                else
                {
                    missing.Add(header);
                }
            }

            if (missing.Count == 0)
            {
                return CheckResult(SecurityCheckSeverity.Pass, $"All {ImportantHeaders.Length} security headers are present.");
            }

            var severity = missing.Count >= 4
                ? SecurityCheckSeverity.Warning
                : SecurityCheckSeverity.Info;

            return CheckResult(severity,
                $"Missing {missing.Count} of {ImportantHeaders.Length} security headers: {string.Join(", ", missing)}.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Security headers check failed for {Url}", url);
            return CheckResult(SecurityCheckSeverity.Info, "Could not check security headers.");
        }
    }

    private SecurityCheckResult CheckResult(SecurityCheckSeverity severity, string description) => new()
    {
        Type = Type,
        Severity = severity,
        Title = "Security Headers",
        Description = description
    };
}
