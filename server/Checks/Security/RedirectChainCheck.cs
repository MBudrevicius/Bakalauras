using server.Helpers;
using server.Models;

namespace server.Checks.Security;

public class RedirectChainCheck : ISecurityCheck
{
    public SecurityCheckType Type => SecurityCheckType.RedirectChain;

    private const int MaxRedirects = 10;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RedirectChainCheck> _logger;

    public RedirectChainCheck(IHttpClientFactory httpClientFactory, ILogger<RedirectChainCheck> logger)
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
            using var client = _httpClientFactory.CreateClient("NoRedirect");
            client.Timeout = TimeSpan.FromSeconds(10);

            var warnings = new List<string>();
            var hops = new List<string> { url };
            var currentUri = uri;
            var redirectCount = 0;
            var originalHost = uri.Host.ToLowerInvariant();

            while (redirectCount < MaxRedirects)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, currentUri);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                var statusCode = (int)response.StatusCode;
                if (statusCode < 300 || statusCode >= 400)
                    break;

                var location = response.Headers.Location;
                if (location is null)
                    break;

                var nextUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);
                redirectCount++;
                hops.Add(nextUri.ToString());

                // HTTPS -> HTTP downgrade
                if (currentUri.Scheme == Uri.UriSchemeHttps && nextUri.Scheme == Uri.UriSchemeHttp)
                {
                    warnings.Add($"Redirect #{redirectCount} downgrades from HTTPS to HTTP");
                }

                // Cross-domain redirect
                var nextHost = nextUri.Host.ToLowerInvariant();
                if (!nextHost.Equals(originalHost, StringComparison.Ordinal) &&
                    !nextHost.EndsWith("." + originalHost, StringComparison.Ordinal))
                {
                    warnings.Add($"Redirect #{redirectCount} goes to a different domain ({nextHost})");
                }

                // Prevent SSRF via redirects
                if (UrlValidator.IsPrivateOrReserved(nextUri))
                {
                    warnings.Add($"Redirect #{redirectCount} points to a private/reserved address");
                    break;
                }

                currentUri = nextUri;
            }

            if (redirectCount > 3)
            {
                warnings.Add($"Excessive redirect chain ({redirectCount} hops)");
            }

            if (warnings.Count == 0)
            {
                if (redirectCount == 0)
                    return CheckResult(SecurityCheckSeverity.Pass, "No redirects detected.");

                return CheckResult(SecurityCheckSeverity.Pass, $"Clean redirect chain ({redirectCount} hop(s)).");
            }

            return CheckResult(SecurityCheckSeverity.Warning,
                $"{string.Join("; ", warnings)} across {redirectCount} redirect(s).");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redirect chain check failed for {Url}", url);
            return CheckResult(SecurityCheckSeverity.Info, "Could not complete redirect chain check.");
        }
    }

    private SecurityCheckResult CheckResult(SecurityCheckSeverity severity, string description) => new()
    {
        Type = Type,
        Severity = severity,
        Title = "Redirect Chain",
        Description = description
    };
}
