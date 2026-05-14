using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using server.Checks.Security;

namespace server.Tests.Unit.Checks.Security;

public class DomainAgeCheckBranchTests
{
    private static DateTime? InvokeParseCreationDate(string whoisText)
    {
        var method = typeof(DomainAgeCheck).GetMethod("ParseCreationDate",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (DateTime?)method.Invoke(null, [whoisText]);
    }

    private static string InvokeExtractRegistrableDomain(string host)
    {
        var method = typeof(DomainAgeCheck).GetMethod("ExtractRegistrableDomain",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)method.Invoke(null, [host])!;
    }

    [Fact]
    public void ParseCreationDate_StandardFormat_ParsesCorrectly()
    {
        var whois = "Domain Name: example.com\nCreation Date: 2020-05-15\nUpdated Date: 2024-01-01";
        var result = InvokeParseCreationDate(whois);
        Assert.NotNull(result);
        Assert.Equal(2020, result!.Value.Year);
        Assert.Equal(5, result.Value.Month);
        Assert.Equal(15, result.Value.Day);
    }

    [Fact]
    public void ParseCreationDate_IsoFormat_ParsesCorrectly()
    {
        var whois = "Creation Date: 2019-03-22T14:30:00Z";
        var result = InvokeParseCreationDate(whois);
        Assert.NotNull(result);
        Assert.Equal(2019, result!.Value.Year);
    }

    [Fact]
    public void ParseCreationDate_CreatedVariation_ParsesCorrectly()
    {
        var whois = "Created: 2018-01-10";
        var result = InvokeParseCreationDate(whois);
        Assert.NotNull(result);
        Assert.Equal(2018, result!.Value.Year);
    }

    [Fact]
    public void ParseCreationDate_RegistrationDate_ParsesCorrectly()
    {
        var whois = "Registration Date: 2021-11-05";
        var result = InvokeParseCreationDate(whois);
        Assert.NotNull(result);
        Assert.Equal(2021, result!.Value.Year);
    }

    [Fact]
    public void ParseCreationDate_RegisteredOn_ParsesCorrectly()
    {
        var whois = "Registered on: 2017-06-30";
        var result = InvokeParseCreationDate(whois);
        Assert.NotNull(result);
        Assert.Equal(2017, result!.Value.Year);
    }

    [Fact]
    public void ParseCreationDate_RegDate_ParsesCorrectly()
    {
        var whois = "reg-date: 2016-08-20";
        var result = InvokeParseCreationDate(whois);
        Assert.NotNull(result);
        Assert.Equal(2016, result!.Value.Year);
    }

    [Fact]
    public void ParseCreationDate_DdMmmYyyy_ParsesCorrectly()
    {
        var whois = "Creation Date: 15-Jan-2020";
        var result = InvokeParseCreationDate(whois);
        Assert.NotNull(result);
        Assert.Equal(2020, result!.Value.Year);
        Assert.Equal(1, result.Value.Month);
    }

    [Fact]
    public void ParseCreationDate_SlashFormat_ParsesCorrectly()
    {
        var whois = "Creation Date: 2022/04/10";
        var result = InvokeParseCreationDate(whois);
        Assert.NotNull(result);
        Assert.Equal(2022, result!.Value.Year);
    }

    [Fact]
    public void ParseCreationDate_DotFormat_ParsesCorrectly()
    {
        var whois = "Creation Date: 2023.07.15";
        var result = InvokeParseCreationDate(whois);
        Assert.NotNull(result);
        Assert.Equal(2023, result!.Value.Year);
    }

    [Fact]
    public void ParseCreationDate_NoCreationField_ReturnsNull()
    {
        var whois = "Domain Name: example.com\nRegistrar: Test\nExpiry Date: 2025-01-01";
        var result = InvokeParseCreationDate(whois);
        Assert.Null(result);
    }

    [Fact]
    public void ParseCreationDate_InvalidDateValue_ReturnsNull()
    {
        var whois = "Creation Date: not-a-date-at-all";
        var result = InvokeParseCreationDate(whois);
        Assert.Null(result);
    }

    [Fact]
    public void ParseCreationDate_IsoWithTimezone_ParsesCorrectly()
    {
        var whois = "Creation Date: 2020-01-15T10:30:00+02:00";
        var result = InvokeParseCreationDate(whois);
        Assert.NotNull(result);
        Assert.Equal(2020, result!.Value.Year);
    }

    [Fact]
    public void ParseCreationDate_IsoWithFractionalSeconds_ParsesCorrectly()
    {
        var whois = "Creation Date: 2021-06-01T12:00:00.0Z";
        var result = InvokeParseCreationDate(whois);
        Assert.NotNull(result);
        Assert.Equal(2021, result!.Value.Year);
    }

    [Theory]
    [InlineData("www.example.com", "example.com")]
    [InlineData("sub.blog.example.com", "example.com")]
    [InlineData("example.com", "example.com")]
    public void ExtractRegistrableDomain_SubDomains_ExtractsCorrectly(string host, string expected)
    {
        Assert.Equal(expected, InvokeExtractRegistrableDomain(host));
    }

    [Fact]
    public void ExtractRegistrableDomain_NoDot_ReturnsAsIs()
    {
        Assert.Equal("localhost", InvokeExtractRegistrableDomain("localhost"));
    }

    [Fact]
    public void ExtractRegistrableDomain_SingleDot_ReturnsAsIs()
    {
        Assert.Equal("example.com", InvokeExtractRegistrableDomain("example.com"));
    }
}
