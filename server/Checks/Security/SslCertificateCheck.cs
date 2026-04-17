using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using server.Helpers;
using server.Models;

namespace server.Checks.Security;

public class SslCertificateCheck : ISecurityCheck
{
    public SecurityCheckType Type => SecurityCheckType.SslCertificate;

    private readonly ILogger<SslCertificateCheck> _logger;

    public SslCertificateCheck(ILogger<SslCertificateCheck> logger)
    {
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
            return CheckResult(SecurityCheckSeverity.Info, "Site does not use HTTPS, so no SSL certificate to validate.");
        }

        if (UrlValidator.IsPrivateOrReserved(uri))
        {
            return CheckResult(SecurityCheckSeverity.Info, "URL points to a private or reserved address.");
        }

        try
        {
            X509Certificate2? cert = null;
            SslPolicyErrors capturedErrors = SslPolicyErrors.None;

            using var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, certificate, _, sslPolicyErrors) =>
                {
                    capturedErrors = sslPolicyErrors;
                    if (certificate is not null)
                    {
                        cert = new X509Certificate2(certificate);
                    }
                    return true;
                }
            };

            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
            using var request = new HttpRequestMessage(HttpMethod.Head, uri);
            await client.SendAsync(request);

            if (cert is null)
            {
                return CheckResult(SecurityCheckSeverity.Info, "Could not retrieve SSL certificate.");
            }

            using (cert)
            {
                var warnings = new List<string>();

                if (capturedErrors.HasFlag(SslPolicyErrors.RemoteCertificateNameMismatch))
                    warnings.Add("Certificate hostname does not match the domain");

                if (capturedErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
                    warnings.Add("Certificate chain has trust errors");

                if (capturedErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable))
                    warnings.Add("Remote certificate was not available");

                if (cert.NotAfter < DateTime.UtcNow)
                    warnings.Add($"Certificate expired on {cert.NotAfter:yyyy-MM-dd}");

                if (cert.NotBefore > DateTime.UtcNow)
                    warnings.Add($"Certificate is not valid until {cert.NotBefore:yyyy-MM-dd}");

                if (warnings.Count > 0)
                {
                    return CheckResult(SecurityCheckSeverity.Warning, string.Join("; ", warnings) + $". Issuer: {cert.Issuer}.");
                }

                if (cert.NotAfter < DateTime.UtcNow.AddDays(30))
                {
                    return CheckResult(SecurityCheckSeverity.Info, $"Certificate expires soon ({cert.NotAfter:yyyy-MM-dd}). Issuer: {cert.Issuer}.");
                }

                return CheckResult(SecurityCheckSeverity.Pass, $"Valid until {cert.NotAfter:yyyy-MM-dd}. Issuer: {cert.Issuer}.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SSL certificate check failed for {Url}", url);
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
