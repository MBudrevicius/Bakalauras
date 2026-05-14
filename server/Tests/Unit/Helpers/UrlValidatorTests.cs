using server.Helpers;

namespace server.Tests.Unit.Helpers;

public class UrlValidatorTests
{
    [Theory]
    [InlineData("http://127.0.0.1", true)]
    [InlineData("http://10.0.0.1", true)]
    [InlineData("http://192.168.1.1", true)]
    [InlineData("http://172.16.0.1", true)]
    [InlineData("http://172.31.255.255", true)]
    [InlineData("http://169.254.1.1", true)]
    [InlineData("http://0.0.0.0", true)]
    public void IsPrivateOrReserved_PrivateIps_ReturnsTrue(string url, bool expected)
    {
        var uri = new Uri(url);
        Assert.Equal(expected, UrlValidator.IsPrivateOrReserved(uri));
    }

    [Theory]
    [InlineData("http://8.8.8.8")]
    [InlineData("http://1.1.1.1")]
    [InlineData("http://93.184.216.34")]
    public void IsPrivateOrReserved_PublicIps_ReturnsFalse(string url)
    {
        var uri = new Uri(url);
        Assert.False(UrlValidator.IsPrivateOrReserved(uri));
    }

    [Fact]
    public void IsPrivateOrReserved_Localhost_ReturnsTrue()
    {
        var uri = new Uri("http://localhost");
        Assert.True(UrlValidator.IsPrivateOrReserved(uri));
    }

    [Theory]
    [InlineData("http://172.15.0.1")]   // just below 172.16
    [InlineData("http://172.32.0.1")]   // just above 172.31
    public void IsPrivateOrReserved_BorderCases_ReturnsFalse(string url)
    {
        var uri = new Uri(url);
        Assert.False(UrlValidator.IsPrivateOrReserved(uri));
    }

    [Fact]
    public void IsPrivateOrReserved_IPv6Loopback_ReturnsTrue()
    {
        var uri = new Uri("http://[::1]");
        Assert.True(UrlValidator.IsPrivateOrReserved(uri));
    }

    [Fact]
    public void IsPrivateOrReserved_IPv6LinkLocal_ReturnsTrue()
    {
        var uri = new Uri("http://[fe80::1]");
        Assert.True(UrlValidator.IsPrivateOrReserved(uri));
    }

    [Fact]
    public void IsPrivateOrReserved_PublicDomain_ReturnsFalse()
    {
        var uri = new Uri("http://example.com");
        Assert.False(UrlValidator.IsPrivateOrReserved(uri));
    }

    [Fact]
    public void IsPrivateOrReserved_NonExistentDomain_ReturnsFalse()
    {
        var uri = new Uri("http://this-domain-does-not-exist-xyz123abc.invalid");
        Assert.False(UrlValidator.IsPrivateOrReserved(uri));
    }

    [Fact]
    public void IsPrivateOrReserved_127Range_ReturnsTrue()
    {
        var uri = new Uri("http://127.0.0.2");
        Assert.True(UrlValidator.IsPrivateOrReserved(uri));
    }
}
