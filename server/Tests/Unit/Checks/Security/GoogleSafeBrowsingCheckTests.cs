using server.Checks.Security;

namespace server.Tests.Unit.Checks.Security;

public class GoogleSafeBrowsingCheckTests
{
    private static string InvokeFormatThreatType(string raw)
    {
        var method = typeof(GoogleSafeBrowsingCheck).GetMethod("FormatThreatType",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string)method.Invoke(null, [raw])!;
    }

    [Theory]
    [InlineData("MALWARE", "Malware")]
    [InlineData("SOCIAL_ENGINEERING", "Social Engineering / Phishing")]
    [InlineData("UNWANTED_SOFTWARE", "Unwanted Software")]
    [InlineData("POTENTIALLY_HARMFUL_APPLICATION", "Potentially Harmful Application")]
    public void FormatThreatType_KnownTypes_FormatsCorrectly(string raw, string expected)
    {
        Assert.Equal(expected, InvokeFormatThreatType(raw));
    }

    [Fact]
    public void FormatThreatType_UnknownType_ReturnsRaw()
    {
        Assert.Equal("SOME_NEW_THREAT", InvokeFormatThreatType("SOME_NEW_THREAT"));
    }
}
