using server.Checks.Security;

namespace server.Tests.Unit.Checks.Security;

public class DomainAgeCheckTests
{
    private static DateTime? InvokeParseCreationDate(string whoisText)
    {
        var method = typeof(DomainAgeCheck).GetMethod("ParseCreationDate",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (DateTime?)method.Invoke(null, [whoisText]);
    }

    private static string InvokeExtractRegistrableDomain(string host)
    {
        var method = typeof(DomainAgeCheck).GetMethod("ExtractRegistrableDomain",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string)method.Invoke(null, [host])!;
    }

    [Fact]
    public void ParseCreationDate_IsoFormat_Parses()
    {
        var result = InvokeParseCreationDate("Creation Date: 2020-05-15");
        Assert.NotNull(result);
        Assert.Equal(2020, result!.Value.Year);
        Assert.Equal(5, result.Value.Month);
        Assert.Equal(15, result.Value.Day);
    }

    [Fact]
    public void ParseCreationDate_IsoWithTime_Parses()
    {
        var result = InvokeParseCreationDate("Creation Date: 2019-03-10T14:22:33Z");
        Assert.NotNull(result);
        Assert.Equal(2019, result!.Value.Year);
    }

    [Fact]
    public void ParseCreationDate_SlashFormat_Parses()
    {
        var result = InvokeParseCreationDate("Created: 2018/07/25");
        Assert.NotNull(result);
        Assert.Equal(2018, result!.Value.Year);
        Assert.Equal(7, result.Value.Month);
    }

    [Fact]
    public void ParseCreationDate_DotFormat_Parses()
    {
        var result = InvokeParseCreationDate("Registration Date: 2017.12.01");
        Assert.NotNull(result);
        Assert.Equal(2017, result!.Value.Year);
    }

    [Fact]
    public void ParseCreationDate_DdMmmYyyy_Parses()
    {
        var result = InvokeParseCreationDate("Registered on: 15-Jan-2021");
        Assert.NotNull(result);
        Assert.Equal(2021, result!.Value.Year);
        Assert.Equal(1, result.Value.Month);
    }

    [Fact]
    public void ParseCreationDate_NoMatch_ReturnsNull()
    {
        var result = InvokeParseCreationDate("No date information here");
        Assert.Null(result);
    }

    [Fact]
    public void ParseCreationDate_RegDateLabel_Parses()
    {
        var result = InvokeParseCreationDate("reg-date: 2022-01-15");
        Assert.NotNull(result);
        Assert.Equal(2022, result!.Value.Year);
    }

    [Fact]
    public void ParseCreationDate_WithTimezoneOffset_Parses()
    {
        var result = InvokeParseCreationDate("Creation Date: 2020-06-15T10:30:00+02:00");
        Assert.NotNull(result);
        Assert.Equal(2020, result!.Value.Year);
    }

    [Theory]
    [InlineData("www.example.com", "example.com")]
    [InlineData("sub.domain.example.com", "example.com")]
    [InlineData("example.com", "example.com")]
    [InlineData("localhost", "localhost")]
    public void ExtractRegistrableDomain_VariousHosts_ExtractsCorrectly(string host, string expected)
    {
        Assert.Equal(expected, InvokeExtractRegistrableDomain(host));
    }

    [Fact]
    public void ExtractRegistrableDomain_SingleLabel_ReturnsSame()
    {
        Assert.Equal("localhost", InvokeExtractRegistrableDomain("localhost"));
    }

    [Fact]
    public void ExtractRegistrableDomain_TwoLabels_ReturnsSame()
    {
        Assert.Equal("example.com", InvokeExtractRegistrableDomain("example.com"));
    }
}
