using server.Checks.Security;

namespace server.Tests.Unit.Checks.Security;

public class PhishingCheckBranchTests
{
    private static string InvokeNormalizeLeetSpeak(string input)
    {
        var method = typeof(PhishingCheck).GetMethod("NormalizeLeetSpeak",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string)method.Invoke(null, [input])!;
    }

    private static int InvokeLevenshteinDistance(string s, string t)
    {
        var method = typeof(PhishingCheck).GetMethod("LevenshteinDistance",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (int)method.Invoke(null, [s, t])!;
    }

    // NormalizeLeetSpeak tests - covering all char branches
    [Theory]
    [InlineData("g00gle", "google")]
    [InlineData("f4cebook", "facebook")]
    [InlineData("amaz0n", "amazon")]
    [InlineData("app1e", "appie")]
    [InlineData("micro$oft", "microsoft")]
    [InlineData("pay pa1", "pay pai")]
    public void NormalizeLeetSpeak_CommonSubstitutions_Normalized(string input, string expected)
    {
        Assert.Equal(expected, InvokeNormalizeLeetSpeak(input));
    }

    [Theory]
    [InlineData('0', 'o')]
    [InlineData('1', 'i')]
    [InlineData('3', 'e')]
    [InlineData('4', 'a')]
    [InlineData('5', 's')]
    [InlineData('7', 't')]
    [InlineData('8', 'b')]
    [InlineData('9', 'g')]
    [InlineData('$', 's')]
    [InlineData('@', 'a')]
    [InlineData('!', 'i')]
    public void NormalizeLeetSpeak_IndividualChars_AllMapped(char input, char expected)
    {
        var result = InvokeNormalizeLeetSpeak(input.ToString());
        Assert.Equal(expected.ToString(), result);
    }

    [Fact]
    public void NormalizeLeetSpeak_NormalChars_Unchanged()
    {
        Assert.Equal("google", InvokeNormalizeLeetSpeak("google"));
    }

    // LevenshteinDistance tests
    [Theory]
    [InlineData("", "", 0)]
    [InlineData("a", "", 1)]
    [InlineData("", "b", 1)]
    [InlineData("abc", "abc", 0)]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("google", "googl", 1)]
    [InlineData("facebook", "facebo0k", 1)]
    public void LevenshteinDistance_KnownPairs_CorrectDistance(string s, string t, int expected)
    {
        Assert.Equal(expected, InvokeLevenshteinDistance(s, t));
    }

    [Fact]
    public void LevenshteinDistance_SameString_Zero()
    {
        Assert.Equal(0, InvokeLevenshteinDistance("example", "example"));
    }

    [Fact]
    public void LevenshteinDistance_CompletelyDifferent_MaxLength()
    {
        Assert.Equal(3, InvokeLevenshteinDistance("abc", "xyz"));
    }
}
