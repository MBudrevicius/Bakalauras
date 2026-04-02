namespace server.Models;

public enum SecurityCheckType
{
    Https,
    DomainAge,
    SuspiciousLinks,
    SslCertificate,
    Phishing,
    GoogleSafeBrowsing
}
