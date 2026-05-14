using Microsoft.Extensions.Logging.Abstractions;
using server.Checks.Security;
using server.Models;

namespace server.Tests.Unit.Checks.Security;

public class SslCertificateCheckTests
{
    private readonly SslCertificateCheck _check = new(NullLogger<SslCertificateCheck>.Instance);

    [Fact]
    public async Task RunAsync_InvalidUrl_ReturnsInfo()
    {
        var result = await _check.RunAsync("not-a-url");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
        Assert.Contains("Could not parse", result.Description);
    }

    [Fact]
    public async Task RunAsync_HttpUrl_ReturnsInfo()
    {
        var result = await _check.RunAsync("http://example.com");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
        Assert.Contains("not use HTTPS", result.Description);
    }

    [Fact]
    public async Task RunAsync_PrivateIp_ReturnsInfo()
    {
        var result = await _check.RunAsync("https://10.0.0.1/page");
        Assert.Equal(SecurityCheckSeverity.Info, result.Severity);
        Assert.Contains("private", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Type_IsSslCertificate()
    {
        Assert.Equal(SecurityCheckType.SslCertificate, _check.Type);
    }

    [Fact]
    public async Task RunAsync_ValidHttpsSite_ReturnsCertInfo()
    {
        var result = await _check.RunAsync("https://example.com");
        Assert.True(result.Severity == SecurityCheckSeverity.Pass || result.Severity == SecurityCheckSeverity.Info);
        Assert.NotEmpty(result.Description);
    }
}
