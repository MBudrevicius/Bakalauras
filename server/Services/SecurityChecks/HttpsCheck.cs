using server.Models;

namespace server.Services;

public class HttpsCheck : ISecurityCheck
{
    public SecurityCheckType Type => SecurityCheckType.Https;

    public Task<SecurityCheckResult> RunAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Task.FromResult(CheckResult(SecurityCheckSeverity.Info, "Could not parse the URL."));
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
        {
            return Task.FromResult(CheckResult(SecurityCheckSeverity.Pass, "Site uses HTTPS."));
        }

        return Task.FromResult(CheckResult(SecurityCheckSeverity.Warning, "Site does not use HTTPS. Connection is not encrypted."));
    }

    private SecurityCheckResult CheckResult(SecurityCheckSeverity severity, string description) => new()
    {
        Type = Type,
        Severity = severity,
        Title = "HTTPS",
        Description = description
    };
}
