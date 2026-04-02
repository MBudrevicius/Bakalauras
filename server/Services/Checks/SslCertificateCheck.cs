using System.Security.Cryptography.X509Certificates;
using server.Models;

namespace server.Services;

public class SslCertificateCheck : ISecurityCheck
{
    public SecurityCheckType Type => SecurityCheckType.SslCertificate;

    public async Task<SecurityCheckResult> RunAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return CheckResult(SecurityCheckSeverity.Info, "Could not parse the URL.");
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return CheckResult(SecurityCheckSeverity.Info, "Site does not use HTTPS, so no SSL certificate to validate.");
        }

        try
        {
            X509Certificate2? cert = null;

            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, certificate, _, sslPolicyErrors) =>
                {
                    if (certificate is not null)
                    {
                        cert = new X509Certificate2(certificate);
                    }
                    return true; // accept any certificate, it's validated below
                }
            };

            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            using var request = new HttpRequestMessage(HttpMethod.Head, uri);
            await client.SendAsync(request);

            if (cert is null)
            {
                return CheckResult(SecurityCheckSeverity.Info, "Could not retrieve SSL certificate.");
            }

            if (cert.NotAfter < DateTime.UtcNow)
            {
                return CheckResult(SecurityCheckSeverity.Warning, $"Certificate expired on {cert.NotAfter:yyyy-MM-dd}.");
            }

            if (cert.NotBefore > DateTime.UtcNow)
            {
                return CheckResult(SecurityCheckSeverity.Warning, $"Certificate is not valid until {cert.NotBefore:yyyy-MM-dd}.");
            }

            if (cert.NotAfter < DateTime.UtcNow.AddDays(30))
            {
                return CheckResult(SecurityCheckSeverity.Info, $"Certificate expires soon ({cert.NotAfter:yyyy-MM-dd}). Issuer: {cert.Issuer}.");
            }

            return CheckResult(SecurityCheckSeverity.Pass, $"Valid until {cert.NotAfter:yyyy-MM-dd}. Issuer: {cert.Issuer}.");
        }
        catch
        {
            return CheckResult(SecurityCheckSeverity.Info, "Could not retrieve SSL certificate.");
        }
    }

    private SecurityCheckResult CheckResult(SecurityCheckSeverity severity, string description) => new()
    {
        Type = Type,
        Severity = severity,
        Title = "SSL Certificate",
        Description = description
    };
}
